# 应用场景架构

> 本页负责应用级场景与退出边界。所有关卡共用的内容登记、装配、场景表现变化、交互和生命周期目标架构见 [35_LevelManagementArchitecture.md](35_LevelManagementArchitecture.md)。该架构文档不代表新管理工具已经实现。

## 场景流

```text
Boot (Build Index 0)
  └─ MainMenu
       ├─ 商店 / 背包 / 成就
       └─ 关卡选择 → 模式选择
                         └─ 关卡配置指定玩法场景（Prototype / World01）
                              ├─ 单人：选角 → 战斗
                              └─ 联机：房间 → 战斗
```

`Boot` 不展示内容，只创建 `AppRoot`，随后加载 `MainMenu`。

## 常驻对象与场景对象

| 生命周期 | 对象 | 职责 |
|---|---|---|
| 应用全程 | `GameAppRoot` | 常驻服务根与重复实例保护 |
| 应用全程 | `LocalPlayerProfileStore` | 鲸元券、背包、通关与成就记录 |
| 应用全程 | `AchievementService` | 接收稀疏事件、解锁成就并发出通知 |
| 应用全程 | `GameLaunchContext` | 当前关卡、单人/联机模式、返回菜单页 |
| 应用全程 | `GameSceneRouter` | 唯一场景切换入口 |
| MainMenu | `MainMenuController` | 主页、关卡、模式、商店、背包、成就页面路由 |
| Gameplay | `GameplayEntryFlow` | 单人选角/联机房间到战斗的入口 |
| Gameplay | 玩家、敌人、HUD、节点、会话 | 只属于一次玩法运行 |

禁止把玩家、敌人、碰撞体、战斗 HUD 或节点放到 `AppRoot`。这些对象必须在
离开玩法场景时一起销毁，避免下一关继承脏状态。

## 为什么玩法会话不能跨局保留

`CoopSessionController` 现在直接引用 DS、HS、AI、选角器和输入源。这些都是
玩法场景对象。如果只对控制器调用 `DontDestroyOnLoad`，它会在切场景后保留
已经销毁的引用。

所以当前流程先进入对应玩法场景，再建立房间。注意 NGO 自身会把根 NetworkManager
放入 DontDestroyOnLoad，这不等于业务会话获准跨局常驻：Router 必须在离开时显式
停止传输、等待关闭、销毁会话根后才加载下一场景。未来一个章节需要跨越
多个 Unity 场景时，应拆成两层：常驻的纯传输/连接层，以及每个玩法场景加载后
重新注册的角色与战斗同步层。这不是把现有控制器直接常驻。

## 当前验证结果

- `Boot` 会自动加载 `MainMenu`。
- 主菜单的商店、背包与档案不会进入玩法场景。
- 单人入口加载玩法场景并打开选角。
- 联机入口加载玩法场景并打开房间。
- 玩法返回时可以指定恢复到“模式选择”或“关卡选择”。
- 当前启用场景为 `Boot`、`MainMenu`、`Gameplay_Prototype`、`World01_EarlyInternet`；Boot 保持入口。

## 2026-10-05 统一退出契约

`CoopSessionController.Awake` 向 Router 登记 `ISceneExitParticipant`，OnDestroy 撤销。
所有返回按钮、章节返回和直接 `StartLevel` 都经过同一条退出链。会话进入 exiting
后拒绝新控制/收发，transport 通过 `ITransportShutdownStatus` 报告停止完成；8 秒
未完成则停留原场景暂停并记录错误，不继续加载。销毁根后再等一帧，确认实际释放。

异步加载期间保持暂停。新玩法选角保存开战后的倍率 1，而不是把 Router 的临时 0
当作正常倍率；旧场景 OnDisable 不能恢复全局时间。过渡期间选角/开房/加入无效。
主菜单加载完成恢复 1；新玩法等待正常选角/准备。`TransitionCompleted` 刷新新 UI。

目前跨玩法场景会结束房间；不承诺保持在线连接跨场景旅行。扩展此能力前需要拆分
纯连接层和场景绑定层，不能简单去掉销毁或把更多玩法对象设为 DontDestroyOnLoad。

## 新内容接入与检查

新增敌人继续复用 `EnemyActorPool2D`、伤害/感知和现有固定步系统，并显式登记到章节、
奖励、网络 `EnemyPools[]`、权限列表。感知必须遵守组件的实际生命周期：普通敌人在池创建
Actor 时登记，回池由可观测状态过滤，销毁时撤销；不能回池注销却不在出池恢复。
客户端只保留镜像/表现，不执行权威伤害。初始及所有可能切换的 Sprite 都需要稳定网络目录 ID。

新增遭遇复用 `IChapterCombatObjective` 的重置/完成契约，列入章节目标数组并配置有效
段；明确由固定步还是驱动推进，不能双方重复推进。新增危险几何要接 AI 可见感知，
目前的移动圆适配不能替代任意长条/旋转机关的精确危险描述。

新增网络消息先登记 `NetworkMessageCatalog` 的方向、ID、格式，再补真实 writer 与
坏包/组合回归。线格式不兼容时推进协议版本；发布内容兼容标识统一在
`NetworkBuildRevision.CurrentContentVersion` 推进，不能由旧 Installer 写历史字符串。

`GameplayFoundationAudit` 可只读检查已加载或预览场景；`GameplayFoundationBuildCheck`
和 `NetworkProtocolBuildCheck` 会在后续出包前阻断已知装配/协议错误。可单独调用
`CheckEnabledBuildScenes()` 检查，不需要也不会打包。警告需要人工判定，不自动修场景。

验证记录与限制见 `ImplementationEvidence/20261005_FoundationHardening/README.md`。
