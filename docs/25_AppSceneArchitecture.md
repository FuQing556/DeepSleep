# 应用场景架构

## 场景流

```text
Boot (Build Index 0)
  └─ MainMenu
       ├─ 商店 / 背包 / 成就
       └─ 关卡选择 → 模式选择
                         └─ Gameplay_Prototype
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

## 为什么当前联机控制器不常驻

`CoopSessionController` 现在直接引用 DS、HS、AI、选角器和输入源。这些都是
玩法场景对象。如果只对控制器调用 `DontDestroyOnLoad`，它会在切场景后保留
已经销毁的引用。

所以当前流程先进入 `Gameplay_Prototype`，再建立房间。未来一个章节需要跨越
多个 Unity 场景时，应拆成两层：常驻的纯传输/连接层，以及每个玩法场景加载后
重新注册的角色与战斗同步层。这不是把现有控制器直接常驻。

## 当前验证结果

- `Boot` 会自动加载 `MainMenu`。
- 主菜单的商店、背包与档案不会进入玩法场景。
- 单人入口加载玩法场景并打开选角。
- 联机入口加载玩法场景并打开房间。
- 玩法返回时可以指定恢复到“模式选择”或“关卡选择”。
- Build Settings 顺序固定为 `Boot`、`MainMenu`、`Gameplay_Prototype`。
