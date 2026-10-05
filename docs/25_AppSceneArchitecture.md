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

## 2026-10-05 原生 uGUI 双主题

最新进展：通用分层批次之后，用户指出右侧黄昏关卡入口漏掉装饰，并要求主页标题、背景
和局内 Buff 都使用生图。本批新增 2 张标题、2 张主页背景、10 种强化及 2 种主动技能图标，
共 16 张；右侧入口补齐分层美术，两关已装配 Buff 展示。Console 无错误；4819 条
绑定/属性断言和两关各 50 条注入式 UI 状态断言通过，已查看双主题 16:9 / 19.5:9 主页。
真实购买、实战联机及用户最终美术验收未完成，详见下方验证范围。
本批选源与透明记录见 [补全素材记录](ArtProduction/20261005_UiCompletion/README.md)。

此前两套共 14 张通用独立透明层已接入，不是完整样板截图或程序网格。
源图、提示词与裁边记录见 [分层生产记录](ArtProduction/20261005_UiLayers/README.md)。
保留 uGUI、主题记忆和原有交互；其他专属功能图标、各页最终美术及手机实机仍需分别验收。

原型覆盖现有 Boot 通知、主菜单、关卡/模式选择、选角、联机大厅、战斗 HUD、
暂停/退出确认、节点强化商店、结算、鲸元券商店/背包/成就与触控控件。全部保留原生
uGUI（Canvas、Graphic、Button、Text、InputField、ScrollRect），没有引入 UI Toolkit
或网页层；原按钮对象、业务监听和触控命中矩形保留，世界空间交互碰撞体没有改动。

### 主题记忆及共用边界

`真人选角成功 → UiThemePreferences → 本机 PlayerPrefs + Changed → UiThemeView 显式绑定`。

- 初次默认 DeepSeek，存储键为 `DeepSleep.UI.ThemeRole`。成功选择 Harness 后保存，
  返回主菜单、进入其他关卡仍保持；下一次成功选择 DeepSeek 才切回。写入时调用 Save，
  不只存在某场景的内存中；本轮未做设备重启测试。
- 写入口只有单人选角按钮，以及联机创建房间成功后的“我选 DS/HS”。加入房间后的
  自动角色分配、远端角色和 AI 托管不算真人再次选择，不覆盖本机偏好。
- `TrySelect` 公共玩法入口本身不写主题。关卡角色身份、伤害数字来源角色配色、
  当前局的控制权不由界面主题决定；DS/HS 选角卡及 HUD 各自的人物肖像也不会互换。
- `UiThemeView` 在启用或主题变化时应用显式数组，不每帧遍历场景、不临时补组件。
  两关和主菜单使用相同实现，不复制每关一套主题系统。

### 当前分层美术与修改入口

配色资产：

- `Assets/_Project/Configs/Presentation/UI/CFG_UI_DeepSeek.asset`：蓝色水纹底板、银白细框和稻米徽章。
- `Assets/_Project/Configs/Presentation/UI/CFG_UI_Harness.asset`：炭黑底板、暖白细框和低饱和红花徽章，避免密集尖刺。

Inspector 中七个通用 Sprite 引用为 `ButtonBase / ButtonFrame / ButtonOrnament / ButtonGlow /
PanelFrame / CircleBase / CircleFrame`；本批新增 `TitleLogo / MenuBackground` 绑定。
文字和遮罩配色用 `Text / MutedText / Accent / OnAccent / Background`；主页背景 Image
不再当纯色底板染色。`Portrait` 仅用于主菜单随主题切换的角色图。旧 Panel/Border/CornerRadius
等原型字段暂为兼容保留，不再驱动生成图片。角色和关卡缩略图沿用既有确认素材。

原按钮根仍负责 Button/InputField、原业务监听和透明命中 Image；独立 `ThemeArtwork`
子根负责视觉。短按钮包含 Base、Frame、Ornament、Highlight、Glow；最后两层复用独立
生成的柔光图，分别控制透明度。大卡片使用 PanelFrame，圆盘使用 CircleBase/CircleFrame。
全部装饰 Image 不接收射线。边框/底板使用经测量的九宫格，徽章与圆形图保持宽高比。
183 处旧装饰已迁移；旧 `UiSurfaceGraphic` 无正式实例，旧安装器已停用，不作缺图回退。

动效配置在按钮根 `UiButtonMotion`：`PressScale=.97`、`ReboundScale=1.015`、响应 `.09s`、
回弹每段 `.10s`，选中呼吸周期 `2.8s`；鼠标悬停提亮，手机触摸释放后不留下悬停。
`WholeControlGroup` 显式引用同根 CanvasGroup，禁用时整组 alpha 乘 `.45`。
缩放仅作用视觉子根，原热区不缩小；无运行时补组件、无新补间库。主要面板
`UiPanelMotion` 淡入 `.16s`；两者都用 unscaled 时间，菜单在游戏暂停时仍有反馈。

布局在场景的 Canvas / SafeArea 下直接调整。桌面和超宽手机不按同一宽度硬拉伸：
参考高度统一 1080，卡片定宽居中，战斗信息靠边，触控区保留原安全区/命中范围。
商店、背包、成就的长列表使用 ScrollRect；商品卡明确提供 LayoutElement 高度，
避免动态生成后被布局压扁。手机宽屏扩展背景的镜头算法本轮不改。

当前显式装配入口为 `DeepSleep.Editor.Setup.UiLayeredArtworkInstaller.Install(6, 6)`，覆盖
4 场景、12 个主题 Canvas 和 2 个动态卡片 Prefab。两个参数是按钮/面板九宫格的
pixelsPerUnitMultiplier。正式图导入 PPU=100，边界测量详见生产 manifest。装配拒绝 Play
及未保存场景，不重做业务布局；重复执行只校准生成层/动效引用，仍会覆盖对应视觉参数，
用户手调这些参数后不要随意重跑。主题切换不需要执行安装器。

### 本批主页与 Buff 补全

- `UiMenuArtworkInstaller.Install()`：两套 `DeepSleep` 标题使用等比透明 Image；主页静态背景
  使用 AspectRatioFitter 等比铺满并裁切，不拉伸为超宽，也不作为关卡循环图。右侧入口在
  原整卡 Button 的视觉子根内补 Base / Frame / Ornament，文字统一为“进入关卡”；没有
  新增重叠 Button，也没有改变原热区、点击监听和按压动效归属。
- `UpgradeDefinition.Icon` 绑定 10 种现役强化生图；商店和战斗 HUD 复用同一语义图，
  不按每一级复制图片。旧枚举但已退出现役目录的卡不因此重新启用。
- `PlayerUpgradeRuntimeState.Changed → PlayerUpgradeHudView.Refresh()`：只在购买、等级
  变化或权威快照应用后更新预装配槽；按目录顺序紧凑显示 rank>0 项，用文字角标表达等级。
  无每帧轮询，无运行时补组件；网络仍复用既有 `NotifySnapshotApplied()` 事件。
- `PlayerCombatHudView` 消费现有快照：护航/光剑图仅在对应技能 Active 且未倒地时显示；
  受击无敌与复活/续关保护共用已有生图盾，保留两类文字区别，不创建新的 Buff 计时器。
  护航图位于 DS 技能栏，不把 DS 开技能误等同于远处 HS 一定处于保护范围。
- `UiBuffArtworkInstaller.Install()` 已在 `Gameplay_Prototype`、`World01_EarlyInternet`
  显式装配，DS 6 槽、HS 5 槽。HUD 保持原 480 宽与边缘锚点，高度调整为 230，强化行
  位于底部；技能整行显示，武器状态移至生命行右侧。商店卡内加 96px 图并调整文字布局，
  原卡片点击矩形不变；图标挂入既有 `UiButtonMotion.Visual`，随卡片按压/回弹。
  所有图标 Simple / preserveAspect / 非射线目标。
- DS 米粒类图标以实际弹体、命中和护航素材为依据，不是三角海苔饭团。DataCompression、
  RiceFan、RiceStorm 仅选用重画的 `_v2` 原图；3 张错误旧稿只留 raw 追溯，未进入
  ready / Assets。正式文件、原图哈希与提示词见本批生产目录。

本批 12 张图标导入 Max Size=512，标题/背景为 2048。`UiThemeSceneChecks.Run()` 的
4819 条绑定/属性断言覆盖 4 个预览场景、12 个主题 Canvas、2 个 Prefab；数量不是场景数。
两关 Play 各 50 条断言检查空槽、TryApply、权威等级通知、RestoreSnapshot、技能状态
和保护图标。测试只向 UI 注入状态并临时绕过选角画布门，玩法保持选角；商店截图使用
固定预览钱包，没有购买。这些记录不能作为真实网络、消费或章节回滚通过的证据。

截图与恢复记录见 [本批验证](ImplementationEvidence/20261005_UiCompletion/README.md)。
World01 最终再次通过 50 条断言；两 HUD 高230、三张商店图挂 Visual 检查通过。
两轮档案恢复校验均通过，原内存引用/JSON、主档与备份 SHA256 不变，已退出 Play。
GameView 已恢复原选择，主题预览只调用 Apply，不写偏好。Boot 的未保存内容保留，
未保存或丢弃；安装器的 dirty 检查只针对它会修改的两关。不改数值、角色碰撞或网络协议，
不打包、未推送 GitHub；Android、真实双机及用户最终美术验收仍未完成。

### 上一批通用分层与动效验证

- Unity 编译无错误；4806 项绑定/属性断言覆盖 4 预览场景、12 主题 Canvas、2 Prefab；
  主题偏好 11 项通过并还原原键。断言数量不是独立游戏场景数量。
- Play 中 15 项定向动效检查通过：固定热区、鼠标悬停/按压/回弹、外来触点不释放按压、
  双触点所有权、触摸松开无残留辉光、键盘选中/提交、禁用整体淡化和恢复。
  实测按压最小 `.97`、回弹峰值 `1.015`；全程 `Time.timeScale=0`，面板仍完成淡入。
- 已实际查看生成分层的双主题主菜单、列表和关卡卡片；前一批宽屏、选角记忆测试见下方。
  本批没有重跑 Android 硬件触控、双设备联机或 GPU 测量；不能用事件注入代替实机验收。
- 验证和截图入口：[本批证据](ImplementationEvidence/20261005_UiLayers/README.md)。不出包。

### 原型历史验证与边界（不代表视觉验收）

- Unity 编译通过；主题偏好自检 11 项、场景/Prefab 引用和布局属性检查 1595 项通过。
  后者是绑定/属性断言计数，不是 1595 个独立玩法场景。
- 实际 Play 点击验证：Harness → 返回主菜单 → 另一关仍为 Harness → 点击 DeepSeek
  才切回；开关 AI 不改主题；联机房主选择 HS 创建成功后立即换肤。
- 原有 `ChapterFlowLifecycleChecks` 两关流程回归通过，原档案与备份校验不变。
- 已查看 16:9、24:9 编辑器画面、商店动态卡片、退出确认和触控层；安全区检查含不对称
  刘海用例。未进行 Android 实机、双设备联机或 GPU 性能测量，不把编辑器验证当作实机验收。
- 触控层经 uGUI Raycast 检查摇杆/技能入口，并注入两个独立 pointer 验证同时按住、
  释放移动不释放技能、最终松开无粘键；这仍是编辑器事件注入，不是手机硬件多点触控。
- 手动 UI 截图期间暂停编辑器曾触发网络退出的实时超时保护；恢复编辑器后重试退出成功，
  没有因此修改会话/退出底层，也不把此人工暂停情形记作真实联机已验证。
- 无新安装包；没有平衡怪物、Token、强化数值或改休息节点碰撞范围。

已否定的原型画面，仅供追溯：[DeepSeek 主菜单](ImplementationEvidence/20261005_UiThemes/deepseek_mainmenu_final.png)、
[Harness 主菜单](ImplementationEvidence/20261005_UiThemes/harness_mainmenu_final.png)、
[联机大厅](ImplementationEvidence/20261005_UiThemes/deepseek_online_lobby_final.png)、
[鲸元券商店](ImplementationEvidence/20261005_UiThemes/deepseek_voucher_shop.png)、
[Harness 触控层](ImplementationEvidence/20261005_UiThemes/harness_touch_final.png)。
同目录 `initial` 文件是过程截图，`harness_mainmenu_24x9.png` 因切场景尚未结束而拍到旧玩法画面，
不作为主菜单证据。上述文件名中的 `final` 仅是当时截图命名，不表示用户接受或当前美术定稿。
