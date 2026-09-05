# DeepSleep 当前交接说明

> 交接时间：2026-09-05（Asia/Shanghai）  
> 当前工程：`D:\Unity Work\DeepSleep_Unity6`  
> 用途：新 Codex 窗口的第一读取入口。本文件记录实际工作区状态；如与旧文档冲突，以本文件和当前权威文档为准。

## 1. 新窗口第一条指令

把本文件和 `CHAT_DECISIONS_EXPORT_20260905.md` 一起附加到新线程，然后发送：

```text
当前工作目录是 D:\Unity Work\DeepSleep_Unity6。
先完整读取 docs/HANDOFF_CURRENT.md、docs/CHAT_DECISIONS_EXPORT_20260905.md、
docs/agent.md、docs/DesignSpec_v5.md 和交接文件列出的当前权威文档。
先只读核对工程与 git status，不修改任何文件，不重复已经完成的程序集、输入、移动和 Prefab 步骤。
先测试普通沙箱能否读取 ProjectSettings/ProjectVersion.txt，再汇报当前进度和唯一下一小步。
```

## 2. 用户与 Agent 的职责边界

- 用户执行所有 Unity Editor 操作：场景、GameObject、Prefab、组件、Inspector、Input Actions、Project Settings、导入设置、Test Runner 和 Play Mode 验收。
- Agent 负责 C#、测试、配置结构、设计文档、素材生成和逐步配置说明。
- 未经用户针对具体文件授权，Agent 不直接改 `*.unity`、`*.prefab`、`*.asset`、`*.meta`、`*.inputactions`、`*.asmdef`、`ProjectSettings/*` 或 `Packages/manifest.json`。
- 禁止运行时代码隐式装配：不用 `AddComponent`、`RequireComponent`、Find、Resources 路径或名称猜测。
- 禁止玩法数据硬编码；速度、伤害、冷却、边界、滚动速度、Prefab 和 LayerMask 等进入显式配置。
- 每轮只推进一个可验证小步。Agent 写完后给 Unity 中文界面路径、Inspector 字段和验收方法，等待用户确认。
- 沟通直接、高效；不要反复解释已经确认的基础内容，不要擅自引申法律问题。

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
