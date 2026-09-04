# 04｜技术架构与数据边界 v3.1（历史：停止执行）

> 2026-09-04：旧单机/双路线架构已被 `19_OnlineAndHotUpdateArchitecture.md` 取代。零隐式装配等通用规则已迁入 `agent.md`，不得从本文件复制旧模块划分。

> 目标：组件化，但不把组件化理解成“所有东西都丢进全局 EventBus”。每个状态只能有一个写入者，每个依赖在 Inspector 或构造/初始化入口可见。

## 1. 技术基线

- Unity `2022.3.48f1c1`。
- Built-in Render Pipeline，2D Renderer/SpriteRenderer，不引入 URP。
- 新 Input System；当前项目尚未安装，M0 首先完成。
- uGUI + TextMeshPro。
- 目标平台 Windows x64 与 Android 横屏；逻辑画面 1920×1080、16:9，目标 60 FPS。
- 宽于 16:9 的额外区域只显示无碰撞背景；UI 由 Safe Area 适配，玩法边界不随设备宽高比变化。
- 逻辑物理 Fixed Timestep `0.02s`。
- 不接真实模型 API，不联网，不保存用户输入。

## 2. 程序集

| asmdef | 目录 | 允许依赖 |
|---|---|---|
| DeepSleep.Runtime | `Assets/_Project/Scripts/Runtime` | Unity、InputSystem、TMP |
| DeepSleep.Tests.EditMode | `Assets/_Project/Tests/EditMode` | Runtime、Unity Test Framework |
| DeepSleep.Tests.PlayMode | `Assets/_Project/Tests/PlayMode` | Runtime、Unity Test Framework |
| DeepSleep.Editor | `Assets/_Project/Scripts/Editor` | Runtime、UnityEditor |

Runtime 不得引用 Editor。测试程序集不得被 Runtime 引用。

## 3. 场景与组合根

采用“公共入口场景 + 每关独立场景”，而不是把六章内容全部塞进一个 `Gameplay.unity`：

- `Bootstrap.unity`：创建常驻 `AppContext`，初始化存档、设置、本地化、音频与场景流，然后进入 FrontEnd。
- `FrontEnd.unity`：标题、路线/章节/难度选择、图鉴、设置和制作名单；离开前端时整场景卸载。
- `P0_Prologue.unity`：序章教学。
- `C1_Gemini.unity` 至 `C6_ChatGPT.unity`：六个主线关卡各自保存独有环境、演出锚点和 Boss 装配。
- `H1_Neuro.unity`、`H2_VoiceMuseum.unity`：隐藏关与语音助手博物馆，解锁后进入 Build。

每个关卡场景引用同一套 `GameplaySceneRoot` Prefab 和公共配置类型，不能复制六套 PlayerMotor、HUD 或 Boss 基类。关卡场景只保存该关独有的背景层、演出锚点、路线配置引用和 Boss Prefab；每次进入关卡都重建场景，防止上局状态泄漏。

`AppContext` 只拥有跨场景服务：`SaveService`、`SettingsService`、`LocalizationService`、`AudioService`、`SceneFlow`。它不得持有玩家、Boss、HUD 或本章池对象引用。

`GameplaySceneContext` 位于每个关卡共用的 `GameplaySceneRoot` Prefab，只做四件事：

1. 校验必需引用不为空。
2. 构造纯 C# 运行时对象。
3. 按固定顺序初始化系统。
4. 场景销毁时按反序 Dispose。

它不得提供 `Get<T>()`、静态 `Instance` 或随处查服务的方法；也不得包含玩法 Update 巨型方法。

## 4. 功能域划分

| 域 | 主要类 | 唯一写入的数据 | 外部只读/信号 |
|---|---|---|---|
| AppFlow | SceneFlow | 当前前端/游戏场景 | SceneChanged |
| GameplayFlow | GameplayFlowController | 当前章节流程状态 | FlowChanged |
| Input | PlayerInputRouter, DesktopInputSource, MobileTouchInputSource | 当前帧输入快照、触点所有权 | IPlayerInput |
| Player | PlayerMotor, PlayerHealth | 玩家位置/速度、HP | HealthChanged, PlayerDied |
| RouteCombat | ShieldController, AutoShooter, HarnessAttackController, RouteEnergy | 路线攻击、护盾、能量 | PlayerEnergyChanged, RouteAttackResolved |
| Combat | Projectile, DamageReceiver, TargetingService | 子弹生命周期、伤害请求、视线/目标查询 | EnemyKilled, BossHealthChanged |
| Encounter | EncounterRunner | 关卡时间、已执行条目 | FlightCompleted |
| Spawn | EnemySpawner, ObstacleSpawner, PickupSpawner | 对象出入池 | 不广播每次 Spawn |
| Progression | ExperienceController, BuffController | XP、等级、Buff 栈 | LevelUpRequested, BuffApplied |
| Boss | BossRuntime, BossPatternRunner, 角色策略控制器 | Boss HP、阶段、攻击循环 | BossDefeated |
| UI | HudPresenter, MenuPresenter, ResultPresenter | 仅 UI 显示状态 | 不写玩法数据 |
| Audio/VFX | AudioDirector, VfxDirector | 表现播放 | 只订阅必要信号 |
| Pooling | PoolRegistry | 池实例与租借状态 | 诊断计数 |
| Difficulty | DifficultyRuntime | 本章白名单倍率快照 | DifficultyLoaded |
| Persistence | SaveService, SettingsService | 进度、记录、设置 | SaveRecovered, SettingsChanged |

`PlayerInputRouter` 只把当前启用的输入源转换为同一份命令快照；`PlayerMotor`、`ShieldController` 和 `HarnessAttackController` 不得出现 `Application.platform` 分支。Android 触控以 touchId 绑定飞行/技能所有权，支持两指并发；坐标转换使用注入的玩法相机和 16:9 逻辑矩形。

画面适配由独立 `PresentationBounds` 提供逻辑玩法矩形、实际屏幕矩形与 Safe Area。生成器、边界、Harness 选区只读取逻辑玩法矩形；背景与非交互装饰可以读取完整屏幕矩形。

## 5. 通信规则

### 5.1 直接调用

满足任一条件就直接引用接口/组件：

- 父组件拥有子组件，例如 `PlayerFacade` 调用 `PlayerMotor.Flap()`。
- 一对一命令，例如 `AutoShooter` 向 `ProjectilePool` 租借子弹。
- 需要返回值，例如 `ShieldController.TryActivate()`。
- 调用顺序必须明确，例如伤害流水线。

### 5.2 类型化信号

只有一对多、跨功能域、无需返回值时广播：

| Signal | Payload | 发布者 | 订阅者 |
|---|---|---|---|
| GameFlowChanged | from, to | GameFlowController | HUD, Audio, Encounter |
| PlayerHealthChanged | current, max, delta, source | PlayerHealth | HUD, VFX, Audio |
| PlayerEnergyChanged | route, current, max | ShieldController/RouteEnergy | HUD |
| RouteAttackResolved | route, targetCount, damage, wasEmpty | AutoShooter/HarnessAttackController | Stats, HUD, VFX |
| PlayerDied | damageSourceId | PlayerHealth | Flow, Result, Audio |
| EnemyKilled | enemyId, position, xp | EnemyHealth | Progression, Stats, VFX |
| ExperienceChanged | current, threshold | ExperienceController | HUD |
| LevelUpRequested | level, candidates | ExperienceController | Flow, UpgradeUI |
| BuffApplied | buffId, stacks | BuffController | HUD, PlayerVisual |
| BossHealthChanged | current, max, phase | BossHealth | BossHUD, Audio |
| BossDefeated | fightDuration | BossHealth | Flow, Stats |

实现可用场景级 `SignalHub`，但：

- 没有字符串事件名。
- 订阅必须返回/保存解绑句柄，OnDisable/Dispose 解绑。
- 事件 Payload 为只读 struct，不传可变组件引用。
- 禁止“万能 Dictionary<string, object>”。
- 高频事件（每帧位置、每颗弹移动）不得广播。

## 6. 配置、Prefab 与运行时状态

三者职责不能混：

### 6.1 ScriptableObject 配置

保存跨实例共享、可调、只读的设计数据：速度、HP、伤害、图标、策略枚举、池容量。运行时不得改原资产。

### 6.2 Prefab

保存稳定组件图与局部引用：SpriteRenderer、Animator、Collider、Rigidbody、伤害接收器、视觉子节点、枪口和锚点。Prefab 不是纯图片壳。

### 6.3 运行时状态

保存本局变化：当前 HP、XP、Buff 栈、阶段计时、租借状态。退出本局销毁，不写回配置。

## 7. 配置资产定义

### 7.1 GameBalanceConfig

字段必须对应 `01_GameplaySpec.md`：相机、世界速度、玩家飞行、生命、伤害、无敌帧、两路线攻击/能量、经验阈值、磁吸范围。每个字段带 `[Min]` 或自定义范围校验。

### 7.2 EnemyDefinition

```text
id: EnemyId
prefab: EnemyView
maxHealth: int
contactDamage: int
moveStrategy: EnemyMoveStrategyId
moveSpeed: float
firePattern: FirePatternDefinition? 
xpDrop: int
poolPrewarm: int
```

`moveStrategy` 只能选择已实现的策略：GroundCrawler、SineFlyer、TelegraphedDash。新增完全不同的移动方式必须新增代码和测试。

### 7.3 ProjectileDefinition

```text
id, prefab, faction, speed, damage, lifetime,
movementStrategy, canPierce, pierceCount, poolPrewarm
```

已实现的移动策略：Straight、ArcBezier、Radial、PredictedStraight。禁止在 ScriptableObject 里塞反射类型名。

### 7.4 BuffDefinition

```text
id, titleKey, descriptionKey, icon,
maxStacks, effectType, additiveValue, multiplier
```

已实现 EffectType 分路线注册：

- DeepSeek：MultiShot、DamageMultiplier、FireRateDamageTradeoff、RiceAndMagnet、MaxHealthHeal、EveryNthPiercingShot。
- Harness：FollowupExecute、AreaMultiplier、CostDamageTradeoff、EnergyRegenAndVoucher、MaxEnergyRestore、WeakpointExecute。

`BuffDefinition` 必须声明 `allowedRoute`；候选生成器禁止跨路线抽卡。

### 7.5 ChapterRouteDefinition / EncounterDefinition

```text
ChapterRouteDefinition:
chapterId, routeId, briefingKey, encounter, bossVariant, resultThresholds

EncounterDefinition:
seed, duration, segments[]

SegmentDefinition:
startTime, type, parameters, exclusiveTags[], cleanupPolicy
```

DS/HA 必须引用两份独立的 `ChapterRouteDefinition`，不得共享同一个 segments 数组。条目用 SerializeReference 多态或显式 enum+参数结构；优先显式 enum，减少 Unity 序列化故障。已批准 SegmentType 见 `12_SystemsAndUXSpec.md`；编辑器验证必须检查时间排序、ID、门缝、中心变化和互斥标签。

### 7.6 BossDefinition / RouteBossVariant

`BossDefinition` 保存共享身份、Prefab、阶段语义和 Pattern 列表；`RouteBossVariant` 保存路线 HP、阶段阈值、弱点规则、资源掉落和策略参数。通用生命周期由 `BossRuntime` 驱动，Gemini/Claude/Kimi/Grok/GLM/ChatGPT/Neuro 等只实现自己的策略组合，禁止复制一套 Boss 基类。完整数值见 `15_CharacterArrangementAndSkills.md`。

### 7.7 DifficultyProfile

只保存 `01_GameplaySpec.md` 批准的敌弹速度、Boss HP、通道净高、预警、路线资源和 Pattern Variant。加载后生成本章只读快照；禁止修改玩家碰撞体、输入、基础重力或豆包段并发规则。

### 7.8 SaveData / SettingsData

进度与设置分文件保存，带 `schemaVersion`。两端都写入 `Application.persistentDataPath`，通过 `ISaveStorage` 封装平台文件操作；写入必须采用临时文件 → 替换 → `.bak` 备份，平台不支持同语义原子替换时由存储实现保证“旧主档或备份至少一份可读”。损坏时先恢复备份，再建立默认文件。只保存章节解锁、路线/难度记录、图鉴、挑战和外观，不保存永久伤害、生命或射速成长。首发不做跨设备同步。

## 8. 对象池

必须池化：玩家弹、敌弹、普通怪、拾取物、短时 VFX、无伤害话术泡。

| 类型 | 预热数量 | 扩容上限 | 上限后行为 |
|---|---:|---:|---|
| 玩家弹 | 32 | 64 | 回收最老的已出屏弹；不得影响屏内弹 |
| 敌弹 Straight | 96 | 160 | 本次发射跳过并记录告警 |
| 敌弹 Banana/Arc | 32 | 64 | 本次发射跳过并记录告警 |
| 普通怪 | 每种 8 | 每种 16 | 跳过生成，QA 失败 |
| 路线资源（白米饭/算力凭证） | 各 24 | 各 40 | 跳过生成，QA 失败 |
| Token | 32 | 64 | 合并为高价值 Token，不能静默丢 XP |
| VFX | 每种 8 | 每种 24 | 回收最老的非关键 VFX |

池对象必须实现明确生命周期：

```text
OnRent(context) → Active → OnReturn(reason)
```

`OnRent` 重置位置、旋转、速度、Collider、Trail、Animator、透明度、计时器、订阅；`OnReturn` 取消订阅并停止协程。禁止只 `SetActive(false)` 期待状态自动清空。

## 9. 物理与伤害契约

定义接口：

```text
IDamageSource { DamageInfo CreateDamage(); }
IDamageReceiver { DamageResult TryApply(DamageInfo info); }
```

`DamageInfo` 至少包含：sourceId、faction、amount、kind、worldPosition、sequenceId。`sequenceId` 用于阻止同一发弹多个 Collider 重复伤害。

阵营：Player、Enemy、Environment。玩家弹不能打玩家，敌弹不能误伤 Boss，Environment 只伤玩家。

## 10. 时间与协程

- 玩法计时使用缩放时间；UI 动画使用 unscaled time。
- 禁止在池对象中启动无法追踪的无限协程。
- 每个 Controller 持有自己的 Cancellation/Coroutine 句柄并在 Return/Dispose 停止。
- Boss Pattern 用单个可取消序列驱动；阶段转换先取消旧 Pattern，再清弹、演出、启动新 Pattern。
- 不用 `Invoke(string)`、`SendMessage` 或基于字符串的方法调用。

## 11. Update 预算

- 不给每颗弹挂独立复杂 Update 行为；直线弹可由轻量组件或批量 ProjectileSystem 更新。
- 热路径禁 LINQ、装箱、字符串拼接、每帧 `GetComponent`。
- 禁用 `FindObjectOfType`、`GameObject.Find`、Tag 字符串查找做依赖注入。
- Windows 1080p 与目标 Android 真机在 Boss 第三阶段最大弹量时优先 60 FPS；稳定战斗 GC Alloc/Frame=0B。若低端 Android 需要 30 FPS 降级，只能通过质量配置降低装饰/VFX，不改变弹幕数量、判定或关卡时序。

## 12. UI 架构

玩法层只暴露数值和信号；Presenter 格式化文本。按钮调用显式命令接口：

- `IFlowCommands.StartGame()`
- `IFlowCommands.OpenRouteSelect()`
- `IFlowCommands.StartChapter(chapterId, routeId, difficultyId)`
- `IFlowCommands.Restart()`
- `IFlowCommands.ReturnToMenu()`
- `IUpgradeCommands.Select(BuffId)`

UI 不查找玩家组件、不直接改 HP、不读 ScriptableObject 后自行推断当前等级。所有 HUD 与触控控件挂在 Safe Area 容器内；纯装饰背景可延伸到刘海/圆角区域。

## 13. 校验器

M0 必须提供 Editor 校验菜单 `DeepSleep/Validate Project`，一次检查：

- Bootstrap、FrontEnd、所有已启用关卡场景及 Build Settings 顺序。
- Input System 包、Active Input Handling 与 Flap/Shield/HarnessAim 冲突。
- 必需 Sorting Layer/Physics Layer。
- 所有 Definition ID 唯一、引用非空、数值范围合法。
- Encounter 时间有序、ID 可解析、门缝/中心变化合法。
- 两路线不得共享 Encounter 数组；豆包 Segment 必须拥有禁止敌人/敌弹/障碍的互斥标签。
- DifficultyProfile 只能修改白名单字段。
- Prefab 必需组件、Collider Trigger、Layer、Sorting Layer/Order。
- Harness Prefab 不得包含 ShieldController；DeepSeek Prefab 不得包含 HarnessAttackController。
- 所有 Decor 素材无 Collider，所有伤害体均有 DamageSourceId。
- Sprite PPU、Filter Mode、Compression、Pivot 和切片网格。
- 池预热总量不超过预算。

校验结果按 Error/Warning 分级；存在 Error 时禁止打包 Development Build。

## 14. 不采用的模式

- 全局单例服务定位器。
- 全能 EventBus。
- 继承十层的 `BaseEnemyBaseActorBaseEntity`。
- 一个 2000 行 Boss 脚本同时控制 UI、音频、掉落和弹幕。
- 为每个参数复制一份 MonoBehaviour 配置。
- 用 Animator StateMachineBehaviour 写核心伤害逻辑。
- 在 Prefab 名称中解析玩法 ID。
- 把文案直接写死在 C#。
