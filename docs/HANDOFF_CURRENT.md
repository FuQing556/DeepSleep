# DeepSleep 当前交接说明

> **2026-09-08 当前入口：`docs/23_ImplementationReviewAndNextSteps.md`。请先完整阅读该文件。**
> 下方 2026-09-06 内容保留作历史记录，其中“激光无法发射”“生命、敌人尚未实现”“下一步重做输入”等结论均已过期。不得据此修改当前工程。当前代码、配置和用户最新验收优先。

> 最后更新：2026-09-06（Asia/Shanghai）  
> 当前工程：`D:\Unity Work\DeepSleep_Unity6`  
> Unity：`6000.6.0f1`  
> 当前场景：`Assets/Scenes/Gameplay_Prototype.unity`  
> 用途：接手者的第一读取入口。本文件记录本次中断时的实际状态；与旧交接内容冲突时，以本页顶部的 2026-09-06 状态、当前工程事实及权威文档为准。

## 0. 给接手 AI 的最短指令

```text
工作目录是 D:\Unity Work\DeepSleep_Unity6。
先完整读取 docs/HANDOFF_CURRENT.md 与 docs/agent.md；再只读查看 git status。
不要重做程序集、输入、移动、双角色控制分配、背景、DeepSeek 普攻或 Harness 激光已有代码。
当前首要故障是 Harness 激光新增字段尚未完成 Prefab 装配；先向用户讲清当前链路和一个设计问题，得到确认后每次只修一个可验收切片。
禁止直接提交整个脏工作区，禁止手改 Unity YAML，禁止猜测 Inspector 引用，禁止用拉伸 Sprite 或 LineRenderer 实现正式激光。
```

## 1. 最新合作方式（覆盖旧交接中的“用户操作全部 Unity”）

- Agent 已可通过 MCP 操作 Unity；以后由 Agent 完成代码、素材、Prefab/场景/Inspector 配置和测试，用户不再充当机械搬运工。
- 主目标是让用户真正理解项目每个重要结构、数据流和取舍；次目标才是交付完整闭环游戏。
- 每次只推进一个小而完整、可以在 Play Mode 验收的切片。
- 实现前应先询问用户的设计判断；随后解释专业做法、优缺点和推荐结论。关键事实不确定时必须问，禁止猜。
- 用户会关注具体碰撞体、数值、节点层级和代码职责，不能用“已经能跑”代替解释。
- 沟通必须及时：几十秒内发现根因就先汇报，不得为了所谓完整审计连续读取大量无关文件、文档或 MCP 数据。
- 禁止运行时代码隐式装配：不用 `AddComponent`、`RequireComponent`、`Find`、`Resources.Load` 或名称猜测。
- 禁止硬编码玩法数据；速度、伤害、冷却、边界、滚动速度、Prefab 和 LayerMask 等进入显式配置。
- Unity 资源必须通过 Unity/MCP 正常编辑并保存；不要直接手改 `*.unity`、`*.prefab`、`*.asset`、`*.meta`、`*.inputactions` 或 `*.asmdef` 的 YAML。

## 2. 2026-09-06 中断点：Harness 终端激光

### 2.1 已存在的完整代码边界

- 输入层：`UnityInputCommandSource` 把鼠标位置和左键转换为 `PlayerCommand.Aim` 与 `ConfirmAim`。
- 控制归属：场景中 `PlayerControlAssignment._initialLocalPlayerRole = Harness (1)`，因此当前本地输入确实分配给 Harness，不是“控制错角色”。
- 玩法入口：`HarnessTerminalLaserController` 负责选目标、校准时序、朝向决策和创建一次开火请求；它不渲染也不扣血。
- 时序：`HarnessTerminalLaserCycle` 目前为 `Ready -> Calibrating -> Cooldown`。
- 几何：`HarnessTerminalLaserSnapshotFactory` 在校准完成时，根据同一个 `LaserOrigin`、方向、逻辑战斗区域和配置冻结 `BeamFireSnapshot`；束宽、长度、伤害和未来多束都位于快照中。
- 表现：`HarnessTerminalLaserPresenter` 分发给瞄准线/锁定环、炮口与束体、角色姿态三个独立视图。
- 束体：`BeamTiledMeshView2D` 已采用动态四边形网格，沿局部 X 轴重复 UV；这符合项目禁令，不得退回 Sprite 非等比拉伸或 LineRenderer。
- 姿态：`HarnessLaserPoseView2D` 的代码已实现“新状态当帧立即显示，旧状态复制到唯一 Ghost 后淡出”的正确语义。
- 素材已存在：Harness 待机图、激光动作图、炮口、锁定环、束身、命中、过载层与外框素材均已进入工程或生产目录。

### 2.2 已确认的装配故障（不是推测）

当前 `Assets/_Project/Prefabs/Players/PF_Player_Harness.prefab` 落后于新增脚本字段：

1. `HarnessTerminalLaserController._facingController` 未序列化配置，且 Prefab 根节点没有 `PlayerFacingController2D`。控制器会在 `Awake()` 校验失败后禁用；这直接解释“点击敌人后不转身、也不发射”。
2. `AimGuide` 与 `BeamLane_0` 上的 `BeamTiledMeshView2D._sortingReferenceRenderer` 均未配置。即使控制器恢复，两条动态网格视图也会在 `Awake()` 校验失败后禁用。
3. `HarnessTerminalLaserPresenter._poseView._ghostRenderer` 未配置，Prefab 也没有 Ghost 子节点。表现器会校验失败并禁用，单残影无法工作。
4. `LaserOrigin` 已存在，位于 `MovementTiltRoot` 下，本地位置约 `(1.08, -0.08, 0)`；但 Harness 目前没有独立 `FacingRoot`。若只翻角色贴图而不镜像发射点，背向开火会从错误一侧出现。因此不能只补一个组件引用，必须先确定正确节点层级。
5. 代码搜索确认：目前只有表现层订阅 `HarnessTerminalLaserController.FireRequested`，尚无伤害执行器。修复上述装配后可以出现激光，但仍不会扣血，后续需要独立的束线伤害解析器消费同一份 `BeamFireSnapshot`。

### 2.3 当前配置数值

- 校准时间 `0.2s`
- 开火冷却 `1.25s`
- 点选半径 `0.8u`
- 最大锁定距离 `20u`
- 目标层为第 8 层（`m_Bits: 256`，当前敌人层）
- 基础束宽 `0.28u`
- 主目标伤害 `2.5`
- 沿线其他敌人伤害倍率 `0.4`
- 当前 1 条基础束线
- 束体表现总时长约 `0.26s`（淡入 `0.025s`、保持 `0.075s`、淡出 `0.16s`）
- 炮口世界直径 `0.52u`

### 2.4 下一步不要直接开工，先讨论的一个设计分叉

需要用户决定终端激光的方向更新规则。推荐方案是：

- 点中目标后进入校准，校准期间细线、炮口、角色朝向持续跟随该目标；目标跨过角色时，`FacingRoot` 与 `LaserOrigin` 一起翻转。
- 校准完成的那个固定模拟刻冻结开火快照；束体出现后的约 `0.26s` 不再追踪目标，即使目标继续移动也不弯折。

优点：瞄准反馈自然，最终伤害与视觉严格同源，容易进行网络同步，也避免持续束体在目标高速穿越角色时突然甩尾。缺点：开火后目标可能离开束线，这是低频预判攻击应承担的操作结果。

备选方案是束体显示期间持续跟踪目标；观感更“黏”，但会让判定时点、贯穿路径、网络回放和升级后的多束同步复杂很多，不建议用于这个瞬发点杀普攻。

用户确认前，不要修改 Prefab 或代码。

### 2.5 MCP 已知问题

- Unity MCP 的层级、Prefab 信息、场景查询等接口可用。
- 不要调用 `mcpforunity://scene/gameobject/{instanceID}/components`：当前 MCP 包在 Unity 6.6 上会进入 `UnityEditor.EditorUtility.InstanceIDToObject`，抛出 `NotImplementedException`。这是 MCP 兼容问题，不是项目组件损坏。
- 组件详情可通过 Prefab API、Inspector/MCP 其他编辑接口和只读资源文件核对；不要反复触发上述已知错误。
- 本轮该错误只写入了 Console，没有修改工程。

### 2.6 Git 与工作区警告

- 工作区存在大量已修改和未跟踪文件，其中包括场景、两个玩家 Prefab、输入、Harness 激光代码/素材/配置、MCP 包和设计文档。
- 这些是连续开发中的有效内容，归用户所有；不要 reset、checkout、clean 或整批覆盖。
- 本轮审计没有修改工程文件；本次仅更新本交接文档。
- 下一位 AI 在提交前必须先按系统边界审查 diff，不能因为“要备份”就盲目 `git add .`。

## 3. 产品与玩法已锁定方向

- 游戏已从扑翼小游戏改为横屏双人合作射击，不再迁移旧 `Flap/Shield/HarnessAim` 设计。
- 目标端为 Windows x64 与 Android 横屏，未来支持电脑↔手机、电脑↔电脑、手机↔手机跨端联机。
- 单人时玩家任选 DeepSeek 或 Harness，另一角色由同伴 AI 接管；双人不能选择重复角色。
- DeepSeek 蓝鲸鱼娘负责持续火力、自动索敌饭团和稳定压制；攻击会被实体障碍物阻挡。
- Harness 黑红鲸鱼娘负责清弹和爆发，频率更低、消耗更高，手动选择大范围释放并可无视障碍。
- 双方生命与角色资源独立，升级经验共享；有限回血“大米饭”、防御道具“不锈钢盆”等需要队友权衡。
- 队友故障后由另一人执行“热重连”复活；两人共同倒地才失败。
- 世界默认向左滚动。Boss 事件配置可选“定点停景”或“连续飞行”；ChatGPT 龙娘等移动 Boss 可继续滚动背景。
- 正式主线是序章加 C1–C6，共七个章节切片；Neuro / Evil Neuro 是不可遗漏的隐藏 Boss。
- 美术统一为二次元 Q 版小人；还原广泛认知特征，玩法表现优先，非色情。

当前权威规则：

- `docs/DesignSpec_v5.md`
- `docs/18_CoopShooterGameplaySpec.md`
- `docs/19_OnlineAndHotUpdateArchitecture.md`
- `docs/20_Unity6MigrationAndDeliveryPlan.md`
- `docs/15_CharacterArrangementAndSkills.md`
- `docs/02_ContentMemeBible.md`

## 4. 当前 Unity、Git 与工程事实

- Unity：`6000.6.0f1`，Universal 2D / URP 2D Renderer。
- 当前场景：`Assets/Scenes/Gameplay_Prototype.unity`。
- 模板 `SampleScene.unity` 已由用户删除；删除和新场景都还在未提交工作区中。
- Git 分支：`main`；唯一提交：`64c4dce chore: establish Unity 6 project baseline`。
- 远端：`https://github.com/FuQing556/DeepSleep.git`。
- Git LFS 已配置；推送前仍需重新检查 LFS 跟踪结果。
- 当前工程变化应完整保留；不得 reset、checkout 或覆盖用户装配。本轮提交前需检查 LFS、场景引用和 Unity 编译日志。
- 当前没有 Runtime 测试程序集或测试代码；只保留 Unity 模板 Welcome asmdef，不能把讨论过的测试说成已落库。

## 5. 已有实现，禁止从头重复

### 5.1 输入命令契约

正式输入资产为 `Assets/_Project/Input/DeepSleepInputActions.inputactions`。Gameplay Map 已有：

- `Move`
- `Aim`
- `PrimarySkill`
- `SecondarySkill`
- `ConfirmAim`
- `CancelAim`
- `Reconnect`
- `Pause`

Windows 当前绑定包括 WASD、方向键、鼠标、Space、Q、Left Shift、Enter、R、Escape。触摸适配尚未实现，但未来必须继续产出同一 `PlayerCommand`，不能另写移动逻辑。

命令代码位于：

- `Assets/_Project/Scripts/Runtime/Input/Commands/`
- `Assets/_Project/Scripts/Runtime/Input/Local/UnityInputCommandSource.cs`
- `Assets/_Project/Scripts/Runtime/Input/Local/InputActionButtonBuffer.cs`

### 5.2 双角色身份与控制分配

已有 `PlayerRole`、`PlayerCharacterDefinition`、`PlayerActor`、`PlayerControlAssignment`、`PlayerCommandDispatcher` 和 `FixedSimulationLoop`。

当前数据流：

```text
LocalPlayerInput / UnityInputCommandSource
  → PlayerControlAssignment 按 PlayerRole 绑定角色
  → PlayerCommandDispatcher
  → FixedSimulationLoop 每个 FixedUpdate 调度
  → IPlayerCommandConsumer
  → PlayerMovementMotor2D
  → Rigidbody2D.velocity
```

场景 `GameSimulation` 已引用 DeepSeek 与 Harness 两个 `PlayerActor`，固定循环也包含两个 Dispatcher。当前 `_initialLocalPlayerRole = Harness`，用户已验证选择 Harness 的本地控制路径。未来角色选择 UI 应调用同一分配逻辑，不能复制控制器。

`_companionCommandSourceComponent` 当前为空，所以未被本地玩家选择的角色目前不会移动。这是同伴 AI 尚未实现的明确缺口，不是 Harness 不可玩。

### 5.3 自由二维移动与轻量表现

已有 `PlayerMotorConfig`、`PlayerMovementMotor2D`、`PlayerMovementTiltConfig`、`PlayerMovementTiltPresenter`。

当前配置：

- `CFG_PlayerMotor_Default.asset`：最大速度 6.5、加速 40、减速 50、逻辑边界 `(-9.6,-5.4,19.2,10.8)`。
- `CFG_PlayerMovementTilt_Default.asset`：最大倾斜 7°、倾斜速度 70°/s、回正 50°/s。

移动根使用 Rigidbody2D，不旋转物理根；只旋转 `MovementTiltRoot`，实现前后移动时的轻微前倾/后倒。用户已多次反馈编译和 Play Mode 无报错，移动与倾斜已通过当前灰盒验收。

### 5.4 两个玩家 Prefab

- `Assets/_Project/Prefabs/Players/PF_Player_DeepSeek.prefab`
- `Assets/_Project/Prefabs/Players/PF_Player_Harness.prefab`

两者都有显式 Rigidbody2D、CapsuleCollider2D、MovementMotor、Dispatcher、PlayerActor、TiltPresenter 和 SortingGroup，Physics Layer 为 `Player`。

DeepSeek 已使用 `Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png`。

Harness 已使用正式透明图 `Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png`：黑红鲸鱼女仆操作终端，拥有独立悬浮姿势。该图已经过透明、深浅底、游戏蓝底和同 PPU 尺寸检查。

### 5.5 Y 排序基础

- Renderer 2D 的透明排序已配置为 Y 轴方向。
- 两个玩家根都有 SortingGroup，Sorting Layer=`Gameplay`，Order=20。
- 这提供前后遮挡基础，但尚未用重叠障碍完成专门验收；不要重复寻找不存在的 `Sort At Root` 页面。

### 5.6 循环背景

正式文件：`Assets/_Project/Art/Backgrounds/BG_P0_Far_DataSky_Loop_v01.png`。

代码与配置：

- `LoopingBackgroundLayerConfig.cs`
- `LoopingBackgroundLayer2D.cs`
- `CFG_BG_P0_Far_DataSky.asset`

配置为单片宽 20.48u、速度 0.24u/s。场景已有：

```text
World
└─ Background
   └─ BG_Far_DataSky
      ├─ Tile_A
      └─ Tile_B
```

两片使用 Background / -30，由 `LoopingBackgroundLayer2D` 左移和回绕。`SetScrollMultiplier(0)` 用于定点 Boss 停景，`1` 为正常滚动。场景引用已保存，但旧线程没有收到用户对接缝和长时间循环的最终明确验收。

## 6. 尚未实现

- 第二个玩家的 AI 命令源。
- 触摸输入和 Safe Area UI。
- 玩家生命、故障、救援、共鸣和角色状态机。
- DeepSeek 饭团攻击、索敌、对象池和障碍阻挡。
- Harness 手动选区、预算、清弹和爆发攻击。
- 敌人、弹幕、掉落、强化、章节时间轴和 Boss Runtime。
- 联机、房间、中继、断线 AI 接管。
- 资源/代码热更新、存档、商店、正式 HUD、音频和发布流程。

当前没有通用静态 Singleton。不要把两个 `PlayerActor` 做成单例；它们天然有两个实例。未来若需要唯一 GameSession/AppRoot，应在对应里程碑通过显式场景装配建立。角色状态机也应在生命/技能状态实现时按领域拆分，不塞进移动脚本。

## 7. Harness 正式素材基准

参考：

- `docs/ArtProduction/Characters/Harness/CONCEPT_HA_Turnaround_v01.png`
- `docs/References/UserProvided/REF_User_Harness_BlackWhale_20260831.jpg`

两张都是造型参考，不是飞行动作 Sprite。目标素材必须：

- 与 DeepSeek Q 版画风、头身比和游戏内视觉大小一致。
- 黑红鲸鱼女仆，保留鲸尾、鲸鳍/耳、红色强调和高效昂贵的终端感。
- 使用独立姿势：向右前方动态悬浮/飞行，身体略前倾，头发和鲸尾向后扬，操控紧凑终端。
- 不照抄 DeepSeek 抱饭碗姿势；不色情。
- 透明背景，角色完整不裁切；先进入 `docs/ArtProduction/Characters/Harness/`，用户确认后再进正式 Assets。
- 使用“每个语义状态一张主图 + Transform/透明度/VFX”。切换时旧图可短暂淡出，但需限制残影数量。

## 8. 已解决的工具故障

- 旧线程曾因工作区 Git 目录权限导致普通沙箱初始化失败；权限修复后，普通命令、Git 读取与本地图片查看均已恢复。
- 内置生图服务随后恢复。Harness 正式候选图已生成；由于生成器输出了伪透明棋盘格，用户明确授权后采用绿幕生成与本地确定性抠图，最终得到真实 RGBA 文件。
- 当前不得再沿用“无法读取本地图片”或“Harness 尚未生成”的旧结论。

## 9. 聊天导出状态

- 已尝试使用 Codex 只读线程快照分享功能。
- 上传超过两分钟持续无返回，已主动终止，未获得链接；可能与线路或超长线程有关，但证据不足，不能确定。
- `docs/CHAT_DECISIONS_EXPORT_20260905.md` 是本地决策级导出，足以恢复目标、偏好和主要纠偏。
- 若用户在界面中手动“分享”成功，可在新线程把快照作为附件导入；快照不会自动继承原线程状态。

## 10. 唯一建议下一步

完成本轮配置审计与首次远端推送后，实现“角色选择结果 -> 双角色生成/控制分配”的正式入口。该入口必须让本地玩家可选择 DeepSeek 或 Harness，并为未来单人 AI 同伴与双人网络命令源复用；不得把某个角色写死为玩家。
