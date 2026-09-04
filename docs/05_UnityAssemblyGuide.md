# 05｜Unity 工程装配手册 v3.1（历史：停止执行）

> 2026-09-04：本手册面向Unity 2022和旧扑翼输入，不能用于Unity 6新工程。后续每一步装配以 `20_Unity6MigrationAndDeliveryPlan.md` 及当轮说明为准。

> 面向第一次接手项目的人。严格按序执行；每节完成后运行对应检查，不得跳到后面“先看看效果”。

## 0. 开始前

1. 用 Unity Hub 打开 `D:\Unity Work\DeepSleep`。
2. 确认 Editor 显示 `2022.3.48f1c1`，不要自动升级大版本。
3. 若 Unity 提示 Safe Mode，先解决 Console 编译错误；不得在红错状态继续拖 Prefab。
4. 当前目录不是 Git 仓库。开始正式实现前应建立版本控制，但不要把 `Library/`、`Temp/`、`Logs/` 提交。

## 1. 安装并启用新 Input System

当前项目没有安装，按以下菜单执行：

1. `Window > Package Manager`。
2. 左上筛选选择 `Unity Registry`。
3. 搜索 `Input System`，安装与 Unity 2022.3 兼容的已验证版本。
4. 若弹窗询问启用新输入后重启，选择 `Yes`。
5. 重启后打开 `Edit > Project Settings > Player > Other Settings > Active Input Handling`。
6. 选择 `Input System Package (New)`；只有确需旧插件时才用 `Both`，本项目默认不用旧输入。
7. 创建 `Assets/_Project/Settings/DeepSleepInput.inputactions`。
8. 建立 Action Map：`Gameplay` 与 `UI`。Gameplay 中两路线动作共存，但运行时按当前路线屏蔽不适用动作。

`Gameplay` Actions：

| Action | Type | Control Type | Bindings |
|---|---|---|---|
| Flap | Button | Button | `<Keyboard>/space`, `<Mouse>/leftButton` |
| Shield | Button | Button | `<Keyboard>/leftShift` |
| HarnessAim | Button | Button | `<Mouse>/rightButton` |
| Point | Pass Through | Vector2 | `<Pointer>/position` |
| Pause | Button | Button | `<Keyboard>/escape` |

硬规则：鼠标右键只属于 HarnessAim，绝不触发 DeepSeek 护盾；Left Shift 只属于 DeepSeek Shield。Flap 与 HarnessAim 不得重绑为同一按键。HarnessAim 必须能读取 Press/Hold/Release 三相位，Point 每帧只写输入快照，不直接执行攻击。

Android 不把所有操作绑定到 `<Touchscreen>/primaryTouch`，因为它会丢失飞行+技能的第二根手指。后续由场景中显式装配的 `MobileTouchInputSource` 追踪 touchId：飞行区拥有一个触点，护盾按钮或 Harness 瞄准区拥有另一个触点；区域矩形和 UI 引用全部在 Inspector/配置中填写。它与 `DesktopInputSource` 都只向 `PlayerInputRouter` 提交同一种动作快照。

`UI` 使用 Input System 默认 UI Actions 或从模板创建，必须接到 `InputSystemUIInputModule`。

验收：`Packages/manifest.json` 出现 `com.unity.inputsystem`；Player Settings 不再是旧输入值；Console 无输入系统冲突。

## 2. 创建目录

只创建以下结构，不得在 Assets 根目录随手堆文件：

```text
Assets/_Project/
├─ Art/
│  ├─ Characters/{DeepSeek,Harness,Bosses,Helpers,Mascots}/
│  ├─ Enemies/
│  ├─ Obstacles/
│  ├─ Projectiles/
│  ├─ Pickups/
│  ├─ VFX/
│  ├─ Backgrounds/
│  └─ UI/
├─ Audio/{Music,SFX}/
├─ Data/{Balance,Buffs,Characters,Chapters,Difficulties,Enemies,Encounters,Bosses,Projectiles}/
├─ Fonts/
├─ Localization/
├─ Prefabs/{Characters,Enemies,Obstacles,Projectiles,Pickups,VFX,UI,Systems}/
├─ Scenes/
├─ Scripts/{Runtime,Editor}/
├─ Settings/
└─ Tests/{EditMode,PlayMode}/
```

`ThirdParty/` 只放许可证清楚的外部资源，并保留 `LICENSES.md`。网页下载的图不得直接混进正式 Art。

## 3. 设置 Physics Layer

打开 `Edit > Project Settings > Tags and Layers`，按固定槽位填写：

| Layer 槽 | 名称 | 用途 |
|---:|---|---|
| 6 | Player | 玩家碰撞体 |
| 7 | PlayerProjectile | 玩家弹 |
| 8 | Enemy | 普通怪与 Boss 身体 |
| 9 | EnemyProjectile | 敌弹 |
| 10 | Obstacle | 真实管道、事件伤害体、边界；旧山/河装饰禁止放此层 |
| 11 | Pickup | 白米饭、算力凭证、Token |
| 12 | Trigger | 非伤害关卡触发器 |

然后打开 `Project Settings > Physics 2D > Layer Collision Matrix`：

| A \ B | Player | PlayerProj | Enemy | EnemyProj | Obstacle | Pickup | Trigger |
|---|---:|---:|---:|---:|---:|---:|---:|
| Player | 否 | 否 | 是 | 是 | 是 | 是 | 是 |
| PlayerProjectile | — | 否 | 是 | 否 | 是 | 否 | 否 |
| Enemy | — | — | 否 | 否 | 是 | 否 | 否 |
| EnemyProjectile | — | — | — | 否 | 否 | 否 | 否 |
| Obstacle | — | — | — | — | 否 | 否 | 否 |
| Pickup | — | — | — | — | — | 否 | 否 |
| Trigger | — | — | — | — | — | — | 否 |

说明：敌弹与 Player 的 Trigger Collider 触发伤害；PlayerProjectile 与 Obstacle 相撞后回池。敌弹不被障碍挡，避免不同地图导致 Boss Pattern 失真。

## 4. 设置 Sorting Layer

仍在 Tags and Layers 中按顺序创建：

1. Background
2. Gameplay
3. Foreground
4. UIWorld

`Default` 保留但正式 Sprite 不得使用。uGUI Canvas 使用 Screen Space，不依赖 Sorting Layer。

Gameplay 内固定 Order：

| 内容 | Order |
|---|---:|
| 障碍后部 | 0 |
| 玩家 | 20 |
| 普通怪 | 30 |
| Boss | 35 |
| 拾取物 | 40 |
| 玩家弹 | 50 |
| 敌弹 | 55 |
| 角色前景特效 | 60 |

背景：Far=-30，Mid=-20，Near=-10。前景遮挡物 Foreground=0。Order 值和 Physics Layer 完全不是一回事，不得混用。

## 5. 2D 与物理全局设置

1. `Project Settings > Time > Fixed Timestep = 0.02`。
2. `Project Settings > Physics 2D > Gravity Y = -9.81`。
3. `Project Settings > Physics 2D > Queries Hit Triggers` 保持开启。
4. `Project Settings > Player > Resolution and Presentation`：默认 1920×1080，Fullscreen Window；开发期允许窗口化。
5. `Project Settings > Quality`：关闭不需要的 3D 阴影；VSync=Every V Blank，目标帧率运行时设 60。

## 6. 创建场景

### 6.1 Bootstrap.unity

层级固定：

```text
Bootstrap
├─ AppContext
│  ├─ SaveService
│  ├─ SettingsService
│  ├─ LocalizationService
│  ├─ AudioService
│  └─ SceneFlow
├─ LoadingCanvas
│  └─ FadeImage
└─ EventSystem
```

`AppContext` 在本场景创建并跨场景常驻，只包含 Save、Settings、Localization、Audio、SceneFlow。它不保存玩家、HUD、Boss 或本章对象池引用。

### 6.2 FrontEnd.unity

层级固定：

```text
FrontEnd
├─ FrontEndContext
├─ MainCamera
├─ ScreenCanvas
│  ├─ MainMenuPanel
│  ├─ RouteSelectPanel
│  ├─ ChapterSelectPanel
│  ├─ DifficultyPanel
│  ├─ GalleryPanel
│  ├─ SettingsPanel
│  ├─ CreditsPanel
│  └─ LoadingBlocker
└─ EventSystem
```

前端 Presenter 只读取 Save/Settings 快照并发送显式命令；不得预先实例化玩家、Boss 或 Gameplay 对象池。

### 6.3 每个关卡场景

`P0_Prologue`、`C1_Gemini` 至 `C6_ChatGPT`、`H1_Neuro`、`H2_VoiceMuseum` 使用同一层级结构和同一套公共 Prefab；名称中的 `<LevelRoot>` 替换为场景文件名：

```text
<LevelRoot>
├─ __Context
│  ├─ GameplaySceneContext
│  ├─ SignalHub
│  ├─ PoolRegistry
│  ├─ GameplayFlowController
│  ├─ DifficultyRuntime
│  ├─ EncounterRunner
│  ├─ AudioDirector
│  └─ VfxDirector
├─ World
│  ├─ Background
│  │  ├─ BG_Far
│  │  ├─ BG_Mid
│  │  └─ BG_Near
│  ├─ Obstacles
│  ├─ Enemies
│  ├─ Pickups
│  ├─ Projectiles
│  ├─ VFX
│  ├─ BossAnchor
│  ├─ PlayerSpawn
│  └─ DespawnBoundary
├─ Player
├─ MainCamera
├─ UI
│  ├─ ScreenCanvas
│  ├─ BriefingPanel
│  ├─ HudPanel
│  ├─ BossNamePanel
│  ├─ UpgradePanel
│  ├─ PausePanel
│  └─ ResultPanel
└─ EventSystem
```

所有动态对象在对应容器下生成。禁止让对象池租借物散落在场景根节点。

## 7. 相机

MainCamera：

| 字段 | 值 |
|---|---|
| Projection | Orthographic |
| Size | 5.4 |
| Position | `(0,0,-10)` |
| Rotation | `(0,0,0)` |
| Clear Flags | Solid Color |
| Background | 与最远天空色匹配 |
| Culling Mask | 除 UI 外的所有游戏层 |

不挂 Rigidbody/Collider，不让相机跟随玩家 Y 抖动。Boss 震屏通过相机视觉偏移组件，结束必须回 `(0,0,-10)`。

## 8. Canvas 与 UI 锚点

ScreenCanvas：

| 字段 | 值 |
|---|---|
| Render Mode | Screen Space - Overlay |
| Canvas Scaler | Scale With Screen Size |
| Reference Resolution | 1920×1080 |
| Screen Match Mode | Match Width Or Height |
| Match | 0.5 |

HUD 安全边距 48px：

- 生命：左上 `(48,-42)`，每颗心 64×64，间距 10。
- 白饭能量：左上心下方，420×28。
- XP：底部中央，760×18，距底 36。
- Boss 血条：顶部中央，900×32，距顶 44。
- Buff 图标：右上，56×56，最多 6 个，横向。
- 教学提示：屏幕下中，最大宽 900，不能挡玩家。

所有文字用 TMP，字体 fallback 必须覆盖简体中文、英文和数字。禁止把文字画进按钮 PNG。

## 9. Prefab 装配

### 9.1 PF_Player_DeepSeek

```text
PF_Player_DeepSeek [Layer=Player]
├─ VisualRoot
│  ├─ BodyRenderer
│  ├─ ShieldRenderer
│  └─ Muzzle
├─ HurtCollider
├─ PickupCollider
└─ GroundProbe（仅调试，可关闭）
```

根节点组件：Rigidbody2D、CapsuleCollider2D、PlayerFacade、PlayerMotor、PlayerHealth、ShieldController、AutoShooter。

| 组件 | 关键字段 |
|---|---|
| Rigidbody2D | Dynamic, GravityScale=2.35, Interpolate, Continuous, FreezeRotation Z |
| CapsuleCollider2D | Size 0.90×0.72, Offset (0,-0.08), 非 Trigger |
| PickupCollider | CircleCollider2D, Radius=1.2, IsTrigger=true；独立子物体 Layer=Player |
| BodyRenderer | Gameplay Order 20 |
| ShieldRenderer | 默认禁用，Gameplay Order 60 |
| Muzzle | LocalPosition (0.85,0.05,0) |

`PF_Player_DeepSeek` 禁止挂 `HarnessAttackController` 或 Harness 能量组件。

### 9.2 PF_Player_Harness

```text
PF_Player_Harness [Layer=Player]
├─ VisualRoot
│  ├─ BodyRenderer
│  └─ ExecuteOrigin
├─ HurtCollider
├─ PickupCollider
└─ AimVisualRoot
   ├─ RangeCircle
   ├─ LegalOutline
   └─ TargetCountLabel
```

根节点组件：Rigidbody2D、CapsuleCollider2D、PlayerFacade、PlayerMotor、PlayerHealth、RouteEnergy、HarnessAttackController。Motor、Health、碰撞体数值与蓝线一致。

| 组件 | 关键字段 |
|---|---|
| RouteEnergy | Max=180, Start=90, PassiveRegen=4/s |
| HarnessAttackController | Cost=60, Cooldown=2.4s, Radius=2.15u, ExecuteDelay=0.28s |
| AimVisualRoot | 默认禁用；瞄准合法红黑、非法灰色；Sorting Order 60 |
| PickupCollider | CircleCollider2D, Radius=1.2, IsTrigger=true |

Harness Prefab 禁止挂 `ShieldController`、`AutoShooter` 或 Muzzle；范围伤害查询忽略 Obstacle，但玩家身体碰撞仍与蓝线完全相同。

### 9.3 PF_Enemy_LowModel

根：Layer=Enemy，Rigidbody2D Kinematic，Collider 非 Trigger，EnemyFacade、EnemyHealth、GroundCrawler、ContactDamage。视觉 Gameplay Order 30。

### 9.4 PF_Enemy_Hallucination

根：Layer=Enemy，Rigidbody2D Kinematic，CircleCollider2D，EnemyHealth、SineFlyer、EnemyShooter、ContactDamage。枪口是子节点，不能用 Sprite 中心猜。

### 9.5 PF_Enemy_ContextBug

根：Layer=Enemy，Rigidbody2D Kinematic，CapsuleCollider2D、TelegraphedDash、ContactDamage。预警线单独 SpriteRenderer，默认禁用，Gameplay Order 10。

### 9.6 PF_Obstacle_PipeGate

```text
PF_Obstacle_PipeGate [Layer=Obstacle]
├─ TopPipe [BoxCollider2D]
│  └─ GapLipAnchor
├─ BottomPipe [BoxCollider2D]
│  └─ GapLipAnchor
└─ DebugGapGizmo
```

根组件 `PipeGateView` 根据关卡给定 `gapCenterY/gapHeight` 放置上下部分。Collider 非 Trigger。`GapLipAnchor` 必须恰好位于可通行边缘；代码按锚点对齐，不按图片透明边界猜。

### 9.7 PF_Boss_Gemini

根：Layer=Enemy，Rigidbody2D Kinematic，CapsuleCollider2D，BossFacade、BossHealth、GeminiBossController、BossPatternRunner、ContactDamage。

子节点：VisualRoot、UpperMuzzle、LowerMuzzle、EditPenAnchor、SpeechAnchor、DropAnchorTop、DropAnchorBottom。所有锚点由美术/Pefab 人工摆好，控制器不得用常数拼位置。

### 9.8 Projectile Prefab

- 根节点 Layer 对应 PlayerProjectile/EnemyProjectile。
- Rigidbody2D Kinematic，Collision Detection=Continuous。
- Collider IsTrigger=true。
- SpriteRenderer 对应 Order。
- 组件：Projectile、Poolable、可选 TrailRenderer。
- 不在 Prefab 写死伤害；租借上下文来自 ProjectileDefinition。

## 10. Sprite 导入预设

正式 2D 资产统一：

| 字段 | 值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single 或 Multiple（按清单） |
| Pixels Per Unit | 100 |
| Mesh Type | Full Rect（碰撞不用自动轮廓） |
| Extrude Edges | 1 |
| Filter Mode | Bilinear；像素特效另注明 Point |
| Compression | None（角色/UI）；Low Quality（大背景可评估） |
| Generate Mip Maps | Off |
| Alpha Is Transparency | On |

使用 `06_ArtAssetManifest.md` 指定 Pivot。导入后先在 Sprite Editor 检查透明边缘，再挂 Prefab。

## 11. Build Settings

`文件（File）> 生成设置（Build Settings）` 场景顺序：

1. `Assets/Scenes/Bootstrap.unity`
2. `Assets/Scenes/FrontEnd.unity`
3. `Assets/Scenes/P0_Prologue.unity`
4. `Assets/Scenes/C1_Gemini.unity`
5. `Assets/Scenes/C2_Claude.unity`
6. `Assets/Scenes/C3_Kimi.unity`
7. `Assets/Scenes/C4_Grok.unity`
8. `Assets/Scenes/C5_GLM.unity`
9. `Assets/Scenes/C6_ChatGPT.unity`
10. `Assets/Scenes/H1_Neuro.unity`
11. `Assets/Scenes/H2_VoiceMuseum.unity`

尚未实现的关卡不提前创建、不加入生成列表；本表表示最终顺序，不是当前必须一次完成的任务。

开发包：Development Build + Script Debugging 只在诊断时开。正式体验包关闭调试标记。正式发布分别建立 Windows x64 ZIP 和 Android 签名 APK；具体 Player Settings、签名与构建步骤在进入发布里程碑时拆成独立人工步骤，不在 M0 随手填写临时值。

## 12. 每次装配后的 3 分钟检查

1. 运行 `DeepSleep > Validate Project`，Error 必须为 0。
2. Play：前端选择 DS 后空格扑翼、Shift 护盾不足反馈；选择 HA 后右键显示选区、拖动、松开执行；两条路线均用 Esc 暂停。
3. 交叉输入：DS 右键无玩法效果，HA Shift 无玩法效果；Flap 在瞄准期间仍可响应。
4. Scene 视图打开 Gizmos：玩家 Collider 不盖住头发/鲸尾；门缝 Debug 线与图片嘴唇重合。
5. Game 视图切 16:9、20:9、16:10、4:3：20:9 只增加左右背景，其他比例完整保留 16:9 玩法区，HUD 不越 Safe Area。
6. Android 真机里同时按住飞行区并拖动 Harness 选区；抬起任一手指，另一动作继续。
7. 退出 Play 后确认 Inspector 资产没有被运行时数值污染。
