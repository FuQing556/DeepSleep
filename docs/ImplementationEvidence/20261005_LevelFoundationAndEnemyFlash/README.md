# 关卡管理 A 批与怪物受击短闪

日期：2026-10-05。Unity 6000.6.0f1，本机 Windows Editor。内容版本 `20261005-level-foundation-hitflash-1`，协议 **4**。未打包、提交、推送或部署 Relay；未改历史通关/奖励存档。

## 实施范围

本批落实 [单关卡管理架构](../../35_LevelManagementArchitecture.md) 的身份与普通敌人登记基础，并补用户要求的怪物短闪；不是完成 B—D，也没有重新编排黄昏故都。

- `MetaLevelDefinition` 引用关内流程和普通敌人清单，`LevelSceneBindings` 显式映射服务/运行模块。Editor 从统一登记生成章节计数、节点清场、固定步、奖励、网络、权限和感知依赖，不在运行时补线。
- 修正 World01 的旧 PrototypeSky 身份错绑。两关分别创建 `CFG_ChapterRun_PrototypeSky/World01_EarlyInternet` 与 `CFG_LevelContent_PrototypeSky/World01_EarlyInternet`；RunConfig 复制既有数值，不改段数/日程/难度。旧原型配置未删。场景和 Prefab 全通过 Unity API 装配保存，未手写 YAML。
- Chapter 在初始化捕获已验证配置；选角/房间/开战与结算均校验身份。现有 NGO 网络根进入 DDOL 时，只允许同一关卡 Selection、同一明确会话根内的服务；不能把任意跨场景引用视为合法。
- Welcome 携带严格 UTF-8 LevelId（最多 128B）；LAN/Relay 连接批准前也将 Base64 LevelId 加入原有版本范围，避免错关卡客人先锁定重连票据。LAN 批准数据有界 512B；Relay 仍用现有不透明 `version` 字段，保留服务端 100 字符限制并在本地拒绝超限。没有新握手状态机或跨场景联机迁移。
- 普通敌人 Health 与豆包本体发布实际扣血事实。`SpriteHitFlash2D` 复用角色覆盖材质：暖白短闪 .16s，标准峰值 .65，减弱 .18；尊重本机“受击闪光”偏好。独立 Overlay 跟随身体 sprite/姿态/透明度，不改碰撞体、原精灵颜色、攻击或无敌规则；枪口和残影不闪。
- 404、机械蛇 Actor Prefab、豆包 Prefab/World01 实例、普通敌人镜像已装配。Sources 完整保留基础身体/枪口/残影，首项是身体，Overlay 不进入基础快照。新命中序号可靠发送，龄期/序号防止同快照反复点亮；池复用清动画、不清权威序号。豆包仍有独立复制适配器。气泡视为障碍，本批不加短闪。

## 本轮发现并修正的集成问题

1. 登记诊断最初用 JSON 整体恢复 Preview 组件，Unity 重解引用后可能指向正式场景，导致多弹池负例报错原因不对。夹具改为只恢复实际改动字段的原对象句柄，不放松生产验证；两场重新通过。
2. 首帧测试原先在 `timeScale=0` 下直接调用 LateUpdate，不能有效验证低帧率保护。抽出原有推进方法，显式注入同帧 1s 正 dt 及后续 .08s/.08s，验证首帧保持/淡出；Boss 加 37°、1.7 倍缩放的世界姿态断言。
3. 真实 Play 发现 NGO 已把会话根移入 DDOL，过严同场景检查导致 Chapter 初始化失败。按上述明确归属修正，重新从 Boot 跑两关，确认 Chapter 初始化、正确 LevelId、DDOL 根及 16 站往返通过。第一次失败运行不作为开战验收通过证据。
4. 错关卡 Welcome 虽会断开，NGO 原来在批准连接时便锁 guestTicket，可能阻止另一位正确客人加入。关卡兼容性前移到批准前；隔离真实 Approve 检查错误客人不占位、正确客人能进、已有客人断线后仍保留重连身份。

## Edit Mode 验证

调用入口位于 `Assets/_Project/Scripts/Editor/Diagnostics/`，均由 Unity 实际执行，不只是源码编译。

| 入口 | 结果 | 边界 |
|---|---|---|
| `NetworkProtocolChecks.RunCatalog()` | 897 项，23 类方向化消息，协议 4 | 有效/非法 UTF-8、长度、截断、方向与实际 Beam codec；不是双机 |
| `NetworkProtocolChecks.RunCombined()` | 21 项 | 隔离真实 Session.Receive/节点/豆包，不改现场 |
| `MonsterHitFlashNetworkChecks.Run()` | 29 项 | 新命中可靠发送、基础层不含 Overlay、跨实体迟到命中不回退姿态、旧/重复/回绕/复用、坏包原子拒绝 |
| `LevelRegistrationChecks.RunScenePath(path)` | 两关各 25 项 | 幂等、漏清场/模拟、顺序、重复身份、错启动、多弹池拒绝；Preview 不保存 |
| `LevelStartGateChecks.Run()` | 14 项 | 缺绑定时单人/主客各开战入口拒绝与退出，不开真实连接 |
| `LevelStartGateChecks.RunWrongLevelScenePath(path)` | 两关各 8 项 | 错关卡加入/重连，缺 scope/拒 scope，scope 设置必须先于 Start |
| `LevelStartGateChecks.RunNetworkOwnershipScenePath(path)` | 两关各 6 项 | 本关正例、别关选角/根外服务/Editor 跨场景根拒绝、恢复；Play 正例另由真实入关检查 |
| `LevelTransportApprovalChecks.Run()` | 35 项 | 真实 NGO 批准器/Prepare 与 Relay Hello 编码，无 Socket/远程服务器 |
| `GameplayFoundationAudit.RunScenePath(path)` | 原型 988、World01 1037 项，均 0 errors / 0 warnings | 只读装配检查；不是玩法或设备验收 |

## Play Mode 回归

从 Boot/MainMenu 经真实 Router 进入；不直接 Play 缺少应用根的玩法场景。修正 DDOL 归属后，两关均确认 Chapter 已初始化、完整配置有效，会话根处于 `DontDestroyOnLoad`。World01 的当前身份是 `world01_early_internet`。

| 入口 | 每关结果 | 具体覆盖 |
|---|---|---|
| `EnemyHitFlashLocalChecks.Run()` | 43 项 | 两个真实池租还、Hitbox 实际非致命扣血、无效伤害/治疗/重置不闪、原视觉不变、首帧保持、淡出、复用订阅；独立豆包实例的致命离场/镜像/世界姿态 |
| `NetworkWriterCatalogChecks.Run()` | 6 个真实 writer 包 | 玩家、武器、两敌池、饭团、敌弹，通过发送预检和目录 |
| `PlayerHitFeedbackLocalChecks.Run()` | 40 项 | 两角色实际非致命扣血、本人震屏、无敌/复活保护/吸收拦截、状态恢复 |
| `PlayerHitFeedbackNetworkChecks.Run()` | 25 项 | 人工扣血事实经真实 Session.Receive，主客角色、AI、旧包/回绕/重连等 |
| `CombatFeedbackRegression.Run()` | 15 项 | 旧 HS/DS 命中特效、数字、客机表现门控、重复/重连 |
| `CombatFeedbackRegression.RunAiToggles()` | 20 项 | 真实控制请求/路由，主客 DS/HS 反复人→AI→人；不是双端实战 |
| `CompanionNodeGoalChecks.Run()` | 27 项 | 只读节点目标规则与临时姿态；不等同真实导航闭环 |

`SceneExitLifecycleChecks.Start()`：16 站全部通过。包含菜单清理、World01 单人进入/返回/再入与选角开战、直接 World01→Prototype、线上选角、本机 DS 房主、离房、同端口 HS 房主、房主 Router 返回、loopback 客机连接中退出。每站 gameplay 的 Session/Manager/退出参与者为 1/1/1，菜单为 0/0/0；未遗留旧根/单例。没有第二台设备、Relay 实际连接或完整通关。

## 画面核对

- [命中前](enemy_flash_before-1.png) 与 [实际扣血峰值](enemy_flash_peak.png)：World01 摄像机渲染，404 5→4 HP、机械蛇 8→7 HP、独立豆包实例 3→2 HP。截图暂停在峰值，实际播放应在 .16s 淡出，不是持续发光。
- 场景仍在选角暂停；临时把两真实池实例与豆包副本移到可见位置，为稳定截图关闭这两个池实例的刚体插值。没有改正式 Prefab/碰撞数值；之后用 RunReset 回收、销毁副本并返回菜单。图片不是自然战斗难度或网络验收。
- `enemy_flash_before.png` 是第一次临时布置时两普通怪重叠的未采用捕获，不作为前后对照；正式对照用带 `-1` 的文件。

## 性能冒烟

`NetworkHotPathBenchmark.Run(64,16)`：隔离真实序列化/目录预检，不含真实 Socket、Physics 或渲染帧。当前线程字节 API 校准仍不可用，不能把返回 0 当零分配；ProfilerRecorder 以空操作 0、保活数组 1 校准。

| 操作 | GC.Alloc 次/操作 | 本次平均 μs |
|---|---:|---:|
| 30B 反馈发送 | 1 | .51 |
| 64 个单层实体 World.Publish | 64 | 79.48 |
| 240 气泡快照发送（5793B） | 3 | 97.13 |
| 反馈接收 + 8 观察者 | 7 | 1.81 |
| 240 气泡接收 + 7 观察者 | 249 | 259.36 |

主要发送分配次数未比上轮增加；本轮世界/豆包线格式新增命中字段，不能再声称它们与协议 3 字节不变。本次非命中压力基准也不涵盖大量连击可靠发送的带宽峰值；不推导手机 FPS/GPU 性能。

## 仍未验证 / 未实现

- 真实 PC/手机主客互换、网络抖动与 Relay；历史 v1.1.2 偶发 HS 特效问题仍需新统一包现场验收，不借模拟通过宣布完整根因已查清。
- 普通怪致命命中保持原即时退池，不额外延迟死亡来展示短闪；本轮不重做死亡效果。玩家致命/倒地/复活全流程、完整关卡通关和新版本存档迁移未做专项回归。
- 没有改太阳/云独立图层、战斗内太阳曲线、下一节点背景切换、夜景和天台正式素材；耀斑暂缓。完整阶段/交互/任务/检查点与制作窗口仍按架构 B—D 推进。
- 未验证全新第三种怪物整链路接入；目前提供的是现有两类的登记、派生、装配检查及故障注入，不宣传任意新机制已自动支持。

最终从 MainMenu 正常退出 Play，回到 Boot；最终批次完整 Edit 回归通过，Console error 为 0。`git diff --check` 无空白错误，另有两个既存美术工具文件的换行提示；大量既存工作树修改不归为本批产物。
