# DeepSleep 项目结构与维护审计

核对日期：2026-10-05。本文描述当前工作树的实际结构，供项目维护和后续内容开发查阅；不是未来架构的完成声明。已有共用底座，不是每关各写一套。用户确认后，本批已统一装配规则、删除 Chapter 镜像敌人数组并精简完整校验链；两关已装配并重新验证。部分控制器职责仍偏重，本批没有拆分，也不把它作为下一次内容任务的隐藏前置。

音效入口为 [docs/36_AudioDesignAndCoverage.md](docs/36_AudioDesignAndCoverage.md)。2026-10-05 核心首版已接入：`Audio/CFG_GameAudio.asset` 管 42 个 cue、56 个 WAV 和原生 Mixer；Boot 的 `GameAudioService` 管 24+2 声源；每关 `CombatAudioPresenter` 消费实际战斗/可靠网络事件，`SceneAudioPresenter` 管阶段和环境；`UI/Common/UiAudioFeedback` 与 `AudioSettingsPanel` 管操作音和本机音量。两关共用这套实现，不各建一套。Editor 中 `CoreAudioInstaller`、`AudioUiInstaller` 负责显式装配，两个 Audio 检查文件只做诊断，不进包。完整覆盖/待细化项及试听边界见音效文档；没有新增音频中间件、运行时素材搜索或通用事件总线。

## UI 改版前的代码量审计快照

以下为 2026-10-05 底层精简完成、UI 改版之前的统计快照，不是后续工作树实时计数。统计 `Assets/_Project/Scripts` 下全部 C# 的物理行，包含注释和空行；不含 Unity/第三方包、Library、生成的工程文件、美术/场景 YAML、文档及独立 Relay Python 服务。文件多、行数多本身不等于冗余，以下数字不能当作有效逻辑行或运行开销。

| 部分 | 当前文件数 | 当前行数 | v1.1.2 基线行数 | 是否进入 Player 编译 |
|---|---:|---:|---:|---|
| Runtime 游戏逻辑 | 258 | 28,039 | 23,030 | 是 |
| Adapters 平台网络适配 | 4 | 411 | 326 | 是 |
| Editor 装配和诊断 | 73 | 14,291 | 4,617 | 否 |
| 合计 | 335 | 42,741 | 27,973 | 游戏侧共 28,450 行 |

基线为本地提交 `23d4a32`，日期 2026-09-13。当前比该基线累计增加 14,768 行，其中 Editor 增加 9,674 行，约占增量 66%。这是整个未提交工作树与基线的差额，不是上一轮修改量，也不能全部归因于某一次任务。

Editor 中 Diagnostics 为 41 文件、8,112 行，Setup 为 23 文件、5,224 行，其余为联网装配和调试菜单。它们不拖入游戏包，但仍增加阅读、编译和维护成本；不能以“不进包”为理由无限增加工具。也不能把全部测试认定为可删除的冗余。

本批相对修复前：Runtime 减少 76 行；Editor 增加 127 行（现有诊断增加 75 行，其他工具净增 52 行），总计净增 51 行。仅新增一个 79 行的共享权限规则文件，没有新增 Runtime 文件或测试框架。收益是去掉两份可编辑镜像数组和分散规则，不宣称总源码大幅缩减。

## 目录地图

2026-10-06 新增怪物入口见 [敌人与碰撞调参](docs/37_InternetEnemies.md)：下载怪独立小状态机，360 复用数据蛇追逐，双方继续走共同 EnemyActor/池/生命/掉落/网络镜像。`Combat/Damage/PlayerAttackBlocker2D` 与 `AttackBlockerQuery2D` 是共用实体盾查询，三类玩家攻击接入。`LevelSceneInstaller` 现在也从同一敌人清单派生 `CombatAudioPresenter.EnemyPools`。没有新建关卡专用运行框架。

```text
Assets/
├─ Scenes/                       Boot、MainMenu、Gameplay_Prototype、World01_EarlyInternet
└─ _Project/
   ├─ Scripts/
   │  ├─ Runtime/                共用游戏实现及可选内容模块
   │  │  ├─ AppFlow/             应用根、菜单、启动意图、场景路由
   │  │  ├─ Simulation/          固定模拟步与明确调用顺序
   │  │  ├─ Input/               命令数据、键鼠、触屏输入源
   │  │  ├─ Players/             角色控制、移动、倒地/救援、AI
   │  │  ├─ Combat/              武器、伤害、敌人、弹体、感知、可选遭遇
   │  │  ├─ Progression/         关卡登记、章节流程、经济、强化、永久档案
   │  │  ├─ Networking/          会话、协议、主客权限、状态/表现复制
   │  │  ├─ Presentation/        受击、跳字、姿态、特效、背景、Audio 播放与事件表现
   │  │  ├─ World/               相机、玩法范围、滚动、休息节点
   │  │  └─ UI/                  选角、战斗界面、安全区
   │  ├─ Adapters/Networking/    NGO/LAN、Relay 及传输选择适配
   │  └─ Editor/                装配、迁移、诊断、构建检查，不在游戏中运行
   ├─ Audio/                     WAV、42-cue 目录和原生 AudioMixer
   ├─ Configs/                   ScriptableObject 调参和关卡定义
   ├─ Prefabs/                   玩家、敌人、弹体和表现的组件组合
   └─ Art/                       正式美术资源
Server/relay/                    独立 Python 中继服务及其测试
docs/                           设计、专项说明、历史证据、美术生产记录
HANDOFF.md                      近期状态和来源限制，不是新任务授权
PROJECT_STRUCTURE.md            当前结构、定位入口和精简建议
```

程序集实际只有 `DeepSleep.Runtime`、`DeepSleep.Networking.Unity` 两个项目自定义边界。Editor 目前通过 Unity 的 Editor 特殊目录进入编辑器程序集，没有专属 asmdef。Runtime 的业务目录是组织边界，不是编译器强制的模块隔离；不能把这张目录图说成依赖已经完全解耦。适配层依赖 Runtime 的接口和 Unity 网络包，运行时代码没有反向引用项目 Adapters/NGO。

## 系统由谁负责

以下路径相对 `Assets/_Project/Scripts/Runtime`，均为现有类型，不是建议新建的管理器。

| 工作 | 当前入口 | 状态及责任边界 |
|---|---|---|
| 启动、退出、换场景 | `AppFlow/GameAppRoot`、`GameSceneRouter`、`GameLaunchContext` | AppRoot 保留档案/路由等应用服务；玩家、敌人不是常驻应用数据 |
| uGUI 主题与生成美术 | `UI/Common/UiThemePreferences`、`UiThemePalette`、`UiThemeView` | 本机选择记忆、显式 Sprite/文字配色资产、绑定应用。14 张通用透明层及本批各 2 张标题/主页背景；背景等比铺满裁切，标题等比显示。无逐帧主题扫描，不管理玩法或页面路由；旧 `UiSurfaceGraphic` 只为迁移类型兼容保留，正式场景/Prefab 已无实例 |
| uGUI 轻动效 | `UI/Common/UiButtonMotion`、`UiPanelMotion` | 显式引用视觉子根/高光/CanvasGroup；unscaled 时间驱动按压、回弹、选中呼吸、禁用淡化和面板淡入；不修改原控件热区或业务事件，无运行时补组件 |
| 局内强化/临时状态图标 | `UI/Combat/PlayerUpgradeHudView`、`PlayerCombatHudView`、`Progression/Upgrades/UpgradeDefinition`、`RestNodeUpgradePanelView` | 10 个目录 Icon 共用于商店和已获强化栏；`PlayerUpgradeRuntimeState.Changed → PlayerUpgradeHudView` 事件驱动图标/等级，无 Update 或运行时装配。主动护航/光剑及保护图标消费既有 HUD 快照，不另造计时器或同步协议 |
| 描述一关 | `Progression/Meta/MetaLevelDefinition` | 关卡身份、场景、奖励，引用本关 RunConfig 和 ContentManifest |
| 什么怪及何时出现 | `Progression/Levels/LevelContentManifest`、`Run/ChapterRunConfig` | 清单登记普通怪模块；战斗段配置控制频道启用和节奏；不是任意任务编辑器 |
| 本场景接到哪些实例 | `Progression/Levels/LevelSceneBindings` | 显式敌人和服务绑定；不自行开战、不运行模拟 |
| 当前处于哪一段 | `Progression/Run/ChapterRunController` | 唯一章节推进者，控制目标/失败、节点经济、检查点和结算；但还混有 HUD 文案和网络读写 |
| 停刷怪和回收战斗对象 | `Progression/Run/ChapterCombatWorld2D` | 读取 Bindings 普通怪/敌弹，显式 Rice 和特殊停止参与者；无第二套阶段计时器 |
| 节点显示及进入出口 | `World/Nodes/RestNodePrototypeController2D` | 云/神殿表现、热点、提示、角色占用、准备/离场及网络交互；仍是偏重控制器 |
| 钱包和商店内容 | `Progression/Economy/TokenWallet`、`Upgrades/RestNodeUpgradeController`、`PlayerUpgradeRuntimeState` | 钱包、商店候选/购买/刷新、强化等级各保存具体状态；Chapter 决定事务提交时点 |
| 谁控制角色及如何行动 | `Players/Commands/PlayerCommandDispatcher`、`Companion/`、`Input/` | 真人、AI、网络使用命令接口，不建立每个平台/每关的独立角色实现 |
| 生成、伤害及击败 | `Combat/Enemies/`、`Damage/`、`Health/`、`Projectiles/`、`Weapons/` | 池管理实例，武器形成攻击，伤害入口裁决；奖励和纯表现消费事实 |
| 主客会话及复制 | `Networking/CoopSessionController`、`NetworkAuthorityGate`、各 Channel | Session 管连接和控制权，从显式 LevelBindings 读取身份；Channel 管各类复制，不经选角 UI 读取关卡 ID |
| 豆包 | `Combat/Encounters/Doubao/`、`Run/DoubaoChapterEncounterDriver2D` | 可选遭遇模块，驱动把它接入指定章节段；不是所有关卡都必须启用 |

定位原则：改怪物行为先看 Combat；改出场时机先看本关配置；改胜负/重试先看 Chapter；改节点视觉先看 Node/Presentation；改联机事实复制看对应 Channel，不把所有修改继续塞进 Chapter 或 Session。

双主题用于 Boot、MainMenu、两关及两个动态卡片 Prefab，不是黄昏专用。`Editor/Setup/UiLayeredArtworkInstaller.Install(6, 6)` 是通用分层显式装配/校准入口；旧 `UiThemeInstaller.Install()` 已停用。补全批次通过 `UiMenuArtworkInstaller.Install()` 绑定两套主页标题/背景、补齐黄昏关卡入口装饰，通过 `UiBuffArtworkInstaller.Install()` 在两关接入 10 种强化和 2 种技能图标、DS 6 / HS 5 个预装配已获槽，保留数值和按钮热区。`Configs/Presentation/UI/` 引用主题美术，`CFG_UpgradeCatalog_Default` 的 Icon 引用语义图标，图标不随页面主题混换角色含义。米粒类参考实际弹体/命中/护航素材；三张海苔饭团错误稿未入 Assets。源图、提示词和选源记录在 `docs/ArtProduction/20261005_UiLayers/` 与 `20261005_UiCompletion/`。本批导入和两关装配完成，Console 无错误；4819 条绑定/属性断言、两关各 50 条注入式 UI 状态断言通过，未验证真实购买或联机。安装器仅拒绝两目标场景的未保存编辑，其他场景保持原样。详情见 [应用场景架构](docs/25_AppSceneArchitecture.md) 与 [本批验证记录](docs/ImplementationEvidence/20261005_UiCompletion/README.md)。

## 第一关与第二关如何共用

按当前文件对应：天空测试关是 `Gameplay_Prototype` / `prototype_sky`，黄昏故都是 `World01_EarlyInternet` / `world01_early_internet`。文档中的“第一关/第二关”不用于代码分支；黄昏文件名 World01 不表示另有一套底层。

| 内容 | 天空测试关 | 黄昏故都 | 后续关卡 |
|---|---|---|---|
| Chapter、CombatWorld、Bindings、Session、休息节点逻辑 | 已装配 | 已装配，同一批脚本 GUID | 正确绑定后复用，不复制源码 |
| 联机命中特效通道、角色受击表现 | 已装配 | 已装配，同一批脚本 GUID | 显式配置通道/表现引用后复用 |
| 普通怪登记、伤害、武器、角色/AI代码 | 共用代码和现役模块 | 共用代码，按本关配置启用 | 可选用已有模块；新行为仍需对应实现 |
| 关卡流程、普通怪清单、关卡身份 | 独立的 PrototypeSky 配置资产 | 独立的 World01 配置资产 | 新建本关定义和配置，不能继续共享一份可变关卡配置 |
| 豆包驱动 | 无 | 已装配 | 按需显式登记，不自动出现 |
| 河流有限全景层和场景美术 | 不使用该黄昏场景内容 | 已装配有限全景层 | 算法可用，美术/锚点/镜头预算需本关配置 |

实核两场景的 ChapterRunController、ChapterCombatWorld2D、LevelSceneBindings、CoopSessionController、RestNodePrototypeController2D、NetworkCombatFeedbackChannel 均各 1 个，PlayerHitFeedbackPresenter2D 均 2 个；豆包驱动与有限全景层仅 World01 有。检查方法为脚本 meta GUID 对照场景序列化引用；不是凭目录名推断共用。

“通用代码”不等于“未来新建空场景自动获得功能”。未来关卡仍需独立定义/配置、显式场景绑定和装配校验；当前没有完整新关卡生成器。共享关卡代码修复会同时影响使用者；黄昏资源、锚点和遭遇配置变化不会自动替换天空关。

## 本批已修的重复与仍有的维护问题

### 完整校验与敌人镜像数组：已精简

[ChapterRunController](Assets/_Project/Scripts/Runtime/Progression/Run/ChapterRunController.cs) 初始化时组合完整校验；[Bindings](Assets/_Project/Scripts/Runtime/Progression/Levels/LevelSceneBindings.cs) 校验本关定义及绑定。已删 Chapter 中重复的元数据、RunConfig 和频道遍历。身份上下文比较与定义内容验证分开，完整校验不再嵌套重复进入同一份定义。

Chapter 已删除序列化 `_spawnDirectors/_enemyPools` 及 `TryValidateRegisteredEnemies`；初始化后从 Bindings 捕获敌人列表，调参和订阅从同一来源读取。隐藏 `_level/_config` 仍作为旧场景导入兼容接线保留；没有宣称所有派生引用都已消失。

### 会话身份读取：已脱离界面链

当前为 `Session.LevelBindings → Chapter.TryValidateLevelStart → 身份上下文/网络根归属`；ID 直接从同一 Bindings 的 Level 读取。Awake 完整校验成功后，运行时边界检查初始化/退出、启动关卡与场景、本局捕获 ID/配置引用和网络根归属，不反复遍历敌人和段落图。选角 UI 仍负责选角与就绪，不再提供 Session 的身份读取接口。静态配置不支持开战后任意热修改；Editor 完整校验仍保留。

### 编辑器重叠写入：已统一生成规则

[LevelSceneInstaller](Assets/_Project/Scripts/Editor/Setup/LevelSceneInstaller.cs) 是普通敌人池到奖励/世界复制等消费者的唯一装配生成规则。Foundation 旧入口已转调；Coop、Chapter、Token 的旧入口遇到已登记场景也先转调并返回，不进入旧扫描/创建/调参路径。未登记旧场景的独有初始化保留，但不会覆盖已有共享网络资产，完成后须显式导入关卡。

权限分类/过滤集中在 [NetworkAuthorityRules](Assets/_Project/Scripts/Editor/Networking/NetworkAuthorityRules.cs)，装配和审计共同引用；保留未知模块及相对顺序。反馈和闪光安装器仍负责自身组件创建/绑定，不再各自维护权限类型黑名单。共享规则仅在 Editor，不是新的运行时管理层。

### 流程控制器仍然过重

RestNodePrototypeController2D 为 1,065 行，同时处理表现、占用、提示、准备和消息；ChapterRunController 为 892 行，仍处理流程、检查点协调、奖励、HUD 字符串、网络序列化及装配验证。长文件不是自动定罪，但这里确实含多个修改理由。本批没有把它们整理成单一职责，不能把“测试过了”说成“结构已全干净”。

### 编辑器工具增量比游戏逻辑更快

本地基线后的 C# 增量约 66% 在 Editor。测试能防回归，但应区分长期回归、一次性迁移和旧调试入口，不再每次修复新增一套相似工具。结构地图与未来架构方案此前也混在一起，容易把未实现计划当成当前能力或默认任务。

## 精简范围与完成状态

用户确认后已执行第 1、2 项；第 3 项未执行，仍是独立可选工作。本批未扩大玩法、改数值/美术或网络协议，没有引入通用事件总线或万能管理器。

| 顺序 | 最小改动 | 明确完成标准 |
|---|---|---|
| 1 收口工具写入权 | 普通敌人登记派生只由 LevelSceneInstaller 写；历史基础迁移菜单退出日常入口，必要部分转调当前规则；权限分类规则集中一处，各表现安装器仅负责自身组件 | 同一派生字段只有一套权威生成规则；重复应用无差异；旧入口不会重排或覆盖登记；独有组件安装能力仍保留 |
| 2 消除重复数据及整图复验 | Chapter 从已验证 Bindings 捕获本局引用，移除独立序列化的敌池/刷怪器镜像数组和对应一致性检查；静态装配校验集中组合一次；Session 从明确的关卡身份入口读取，不绕选角 UI | 同一登记只有一个可编辑来源；一次静态校验不重复进入同一子校验；连接、开战、结算仍检查各自动态条件；错关卡/错归属继续被拒绝 |
| 3 拆开表现与业务代码 | 沿用已有 HUD/网络组件承接文案与复制职责；节点拆开表现和准备交互；保留 Chapter 唯一推进权，不另加平行状态机 | 修改节点视觉不触碰商店/准备规则；修改 HUD 不触碰检查点；独立状态、序列化入口和总重复逻辑减少，而非只把一个长文件切成多个互相依赖的小文件 |

第 1、2 项已在两关落盘；第 3 项不应成为黄昏内容推进的隐形前置。后续维护仍先列精确删除/保留边界，用现有回归验证，不重造相似框架。完成看重复状态/规则是否减少，不能以新增文件数和测试数量代替交付。

应保留的防护：外部网络包格式/长度/序号验证、传输批准和 Welcome 的关卡隔离、实际开战就绪、结算身份、检查点整组验证、对象池清理和 NGO 常驻根归属。它们面对不同信任边界/生命周期时点，不因看起来相似而直接删掉。可复用规则，不可删掉安全职责。

## 查阅和维护约定

这份文件记录当前结构；[docs35](docs/35_LevelManagementArchitecture.md) 记录更长期的内容生产架构；[docs22](docs/22_ChapterRunAndCheckpoint.md) 记录章节流程与最近一次验证；[HANDOFF](HANDOFF.md) 记录交接上下文。不要再用未来方案覆盖实际能力。

新增关卡或系统时更新本文件的责任入口及适用范围；迁移工具改变时标明现役/历史，不允许两个入口各自生成同一规则；统计值只表示上述核对日期，不自动成为代码量上限。

本批实际 Unity 复测：两关登记 42/45、装配 928/982（均零错误/警告）；每关身份隔离 9、归属 7，隔离开战门 14、组合协议 22，通过。原有两关各 7 组流程和 16 站退出重跑通过；另验证初始化后不重跑静态内容图、但本局 ID 变化仍拒绝。主档/备份未变。装配各补一项 `Session.LevelBindings`，再次 Apply 均 0 变化；移除字段经 Unity 保存清理。审计数量减少包含合并重复权限检查，不代表漏项。

协议仍为 4，内容标识 `20261005-registration-simplify-1`。未出包、提交或推送。本机隔离/Play 检查不等于真实 PC/手机双端验收；旧偶发 HS 反馈症状不能据此宣称全部解决。
