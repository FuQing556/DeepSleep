# 13｜现有素材复用与完整生产计划 v1.1（已有资产保留，生产顺序待v5重排）

> 2026-09-04：所有已有素材保留；旧双路线和扑翼动作预算失效。新素材优先级以双鲸灰盒战斗、合作技能和跨端HUD为准。

> 用户要求：现有素材全部保留，不删除。旧方案图可以转为背景、前景遮挡、无伤残影、占位或历史资料。  
> 本文决定资产“怎么用”，不授权把未经许可的参考图直接打包发行。

## 1. 资产状态

| 状态 | 含义 | 能否进入 Gameplay |
|---|---|---|
| `PRODUCTION_ACTIVE` | 尺寸、Alpha、Pivot、判定和风格通过 | 是 |
| `PRODUCTION_READY` | 文件与离线技术检查通过，等待用户在 Unity 导入/滚动或透明实测 | 用户验收后转 ACTIVE |
| `VISUAL_APPROVED` | 造型通过，但未生成正式动作/透明 Sprite | 否 |
| `PLACEHOLDER_ACTIVE` | 灰盒或垂直切片可用，正式版前复核 | 仅开发包 |
| `DECOR_ONLY` | 无 Collider 的背景/前景/无伤残影 | 是 |
| `HISTORY_KEEP` | 旧方案或错误参考，保留追溯 | 否 |
| `REFERENCE_ONLY` | 用户/网络参考，许可证未清 | 否 |

禁止使用 `Legacy`、`Old`、`Temp2` 这类不说明用途的状态名。

## 2. 当前 Unity 美术复用表

### 2.1 背景

| 文件 | 状态 | 用途 |
|---|---|---|
| `BG_P0_Far_DataSky_Loop_v01.png` | PRODUCTION_READY | P0 远景；滚动系数 0.08；2048×1080；左右边界和 16px 带像素差 0 |
| `BG_Far_AICloud_v01.png` | DECOR_ONLY | 菜单/章节卡静态远景；不参与横向循环 |
| `BG_Mid_DataCloud_v01.png` | DECOR_ONLY | 菜单/转场的数据云装饰；不参与横向循环 |
| `BG_Near_Network_v01.png` | DECOR_ONLY | 无碰撞框景/静态装饰；不得遮玩家；不参与横向循环 |

旧三张是 1672×941 的不透明 RGB 完整构图，没有通过首尾像素接缝检查，所以不得再作为滚动层；它们全部保留，可用于菜单、章节卡、低透明框景。新远景的生成源、双拼预览与可复用构建工具位于 `docs/ArtProduction/Backgrounds/` 和 `docs/ArtProduction/Tools/`。

### 2.2 角色/Boss

| 文件 | 状态 | 用途 |
|---|---|---|
| `SPR_DS_Idle_Base_v01.png` | PLACEHOLDER_ACTIVE | DS 路线卡、章节对话头像、灰盒飞行；正式飞行动作完成后仍保留为菜单立绘 |
| `SPR_GE_Idle_Base_v01.png` | DECOR_ONLY | C1 Nano Banana “编辑失败的无耳尾复制体”无伤残影；Alpha≤0.45、持续≤0.8s，不能作为 Boss 本体 |

新版 Gemini 本体从已通过 v02 设定稿生产；旧图不删除、不冠以正式 Gemini Idle。

### 2.3 普通敌人

| 文件 | 状态 | 用途 |
|---|---|---|
| `SPR_EN_LowModel_Idle_Base_v01.png` | PLACEHOLDER_ACTIVE | 不会搜索的低等模型；序章/C1 |
| `SPR_EN_Hallucination_Idle_Base_v01.png` | PLACEHOLDER_ACTIVE | 幻觉漂浮敌；C1 起复用 |
| `SPR_EN_ContextBug_Idle_Base_v01.png` | PLACEHOLDER_ACTIVE | 上下文溢出冲刺敌；C1/C3 |

现有单图通过后可继续保留作图鉴立绘；正式 Gameplay 按需要补移动/受击/击破三种状态图，每种状态默认一张，不要求逐帧动画。

### 2.4 障碍与装饰

| 文件 | 状态 | 最终用途 | Collider |
|---|---|---|---|
| `OB_DoubaoBubble_A/B/C_v01.png` | PLACEHOLDER_ACTIVE | 豆包走廊实体气泡；A/B/C 循环避免重复 | 手工 Capsule/Box |
| `OB_DoubaoRiver_Base_v01.png` | DECOR_ONLY | C1 气泡走廊下方景深带，速度 0.78，Alpha 0.45–0.70 | 禁止 |
| `OB_DoubaoCrest_Base_v01.png` | DECOR_ONLY | 气泡段入/离场前景浪头，Alpha≤0.55；降低前景设置可降至 0.25 | 禁止 |
| `OB_OpusMountain_Bottom_v01.png` | DECOR_ONLY | C2 下方参数栈山脉远景，滚动 0.35，Alpha 0.35–0.55 | 禁止 |
| `OB_OpusMountain_Top_v01.png` | DECOR_ONLY | C2 上方短前景框景，不与 Opus 主体同时遮安全口 | 禁止 |
| `OB_Pipe_TopBody/TopLip/BottomBody/BottomLip_v01.png` | PLACEHOLDER_ACTIVE | P0/C1 数据管道门；Body 平铺、Lip 对齐 GapAnchor | 手工 Box |

任何 `DECOR_ONLY` 物体挂 Collider 都是校验 Error。

### 2.5 拾取物

| 文件 | 状态 | 用途 |
|---|---|---|
| `PU_WhiteRice_Base_v01.png` | PLACEHOLDER_ACTIVE | DS 护盾能量、章节引导线 |
| `PU_Token_Base_v01.png` | PLACEHOLDER_ACTIVE | 两路线 XP |

Harness 尚缺正式 `PU_ComputeVoucher`；不能把白饭染黑冒充。

### 2.6 发射物

| 文件组 | 状态 | 用途 |
|---|---|---|
| `PRJ_DS_Token/Search/Parallel_v01.png` | PLACEHOLDER_ACTIVE | DS Buff 特殊弹头/VFX；基础饭团仍需独立素材 |
| `PRJ_GE_StarBlue/StarPurple_v01.png` | PLACEHOLDER_ACTIVE | Gemini 双子阶段 |
| `PRJ_GE_Banana_v01.png` | PLACEHOLDER_ACTIVE | Nano Banana 弧线弹 |
| `PRJ_GE_Citation_v01.png` | PLACEHOLDER_ACTIVE | 引用星点/后续 Perplexity 可复用色变版本 |
| `PRJ_GE_SpeechBlock_v01.png` | PLACEHOLDER_ACTIVE | Gemini 第三阶段红实线有效方块 |

每张现有大图需要输出游戏尺寸副本；原始大图保留，不覆盖。

### 2.7 UI

| 文件组 | 状态 | 用途/检查 |
|---|---|---|
| Hearts、Energy、XP、BossHP | PLACEHOLDER_ACTIVE | HUD；Frame 必须验证 9-slice 边框不拉伸 |
| Buff 六图标、BuffCard | PLACEHOLDER_ACTIVE | DS/HA 共用图形语言，标题和效果按路线加载 |
| Button Normal/Hover/Pressed | PLACEHOLDER_ACTIVE | 所有前端按钮；文字必须 TMP 动态渲染 |
| Panel Main/Boss | PLACEHOLDER_ACTIVE | 前端/入场框；验证 16:9 和 16:10 |
| Result Victory | PLACEHOLDER_ACTIVE | 胜利横幅；失败、评级、Boss 重试状态仍需新增 |

任何 UI 图中残留错误文字时只用作背景框，文字层隐藏。

## 3. 已通过概念状态

| 角色 | 状态 | 下一资产阶段 |
|---|---|---|
| DeepSeek | VISUAL_APPROVED | 当前实现需要的主角状态图 |
| Harness | VISUAL_APPROVED | 当前实现需要的主角状态图、选区/执行 VFX |
| Gemini | VISUAL_APPROVED | C1 Boss 状态图 |
| ChatGPT | VISUAL_APPROVED | C6 四阶段 Boss 状态图 |
| Doubao | VISUAL_APPROVED | 7 表情头像、气泡角标 |
| Opus | VISUAL_APPROVED | v02 真透明半身为状态母图；补盖章层 |
| Grok | VISUAL_APPROVED | C4 Boss 状态图与 X 戟分层 |
| 即梦 | VISUAL_APPROVED | 控制台操作与时间轴事件 |
| GLM | VISUAL_APPROVED | C5 Boss 状态图、尾巴分层 |
| Qwen | VISUAL_APPROVED | 问答门主持/开扇动作 |
| MiniMax | VISUAL_APPROVED | 场记、录音动作 |
| OpenCode | VISUAL_APPROVED | 本地终端动作 |
| Zcode | VISUAL_APPROVED | 蓝图展开动作 |
| Kimi K3 | VISUAL_APPROVED | 援助+过载+Boss+恢复四类状态图 |
| Claude Code | VISUAL_APPROVED | 非人橙色章鱼状态图 |
| Codex | VISUAL_APPROVED | 非人终端桌宠状态图 |

## 4. 主角状态图预算

### 4.1 DeepSeek

| 状态 | 主图数 | 运行时表现 |
|---|---:|---|
| IdleFly | 1 | 尾巴/裙摆以 Transform 低幅缓动 |
| TapRise | 1 | 整体短促上仰和上冲 |
| Fall | 1 | 随 Y 速度下俯 |
| Shoot | 1 | 主图短后坐；饭团和闪光分层 |
| Hurt | 1 | 闪色/抖动，不改变碰撞体 |
| ShieldCast | 1 | 角色与护盾环分层 |
| Victory | 1 | 抱饭姿态轻弹跳 |
| Defeat | 1 | 整体下坠，不做猎奇 |

完整状态上限 8 张；第一轮只生产 `IdleFly/TapRise/Fall` 3 张。HoldRise 复用 TapRise 或 IdleFly 的轻量动画，除非手感测试证明必须新增状态。

### 4.2 Harness

| 状态 | 主图数 | 运行时表现 |
|---|---:|---|
| IdleFly | 1 | 终端状态灯呼吸 |
| TapRise | 1 | 与 DS 共用时间曲线，不共用图 |
| Fall | 1 | 随 Y 速度下俯，眼镜不可丢 |
| Aim | 1 | 角色只摆手势；选区独立跟随指针 |
| Execute | 1 | 推眼镜确认；执行环收束后爆发 |
| Insufficient | 1 | 状态灯变灰，账单 UI 弹出 |
| Hurt | 1 | 红灯告警/闪色 |
| Victory | 1 | 任务完成勾与轻弹跳 |
| Defeat | 1 | 任务队列报错并下坠 |

完整状态上限 9 张；第一轮同样只生产 `IdleFly/TapRise/Fall` 3 张。选区、方括号、账单、执行环全部是独立 VFX/UI。

## 5. 主线 Boss 状态预算

| Boss | 必需语义状态 | 主图上限 |
|---|---|---:|
| Gemini | Idle、Entry、TwinSplit、BananaEdit、Apology、Transition、Hurt、Defeat | 8 |
| Claude | Idle、Entry、RuleWall、RefusalShield、Stamp、GiantOpus、Hurt、Defeat | 8 |
| Kimi K3 | AllyFlute、GuideCast、Overload、BossIdle、ContextReplay、Experts、DarkMoon、Recover、Hurt | 9 |
| Grok | Idle、Entry、Curtain、XSlash、RebelPattern、Transition、Hurt、Defeat | 8 |
| GLM | Idle、Entry、SleepBand、GraphCast、TailSweep、Transition、Hurt、Defeat | 8 |
| ChatGPT | Entry、Idle、PraiseSeal、ModelSwitch、Rollback、WingOpen、TailSweep、KnotCast、Transitions、Hurt、Defeat | 11 |

每个状态默认一张主图。Boss 身体与大型道具尽量分层：Gemini 编辑笔、Claude 书/印章、Kimi 长笛、Grok X 戟、GLM 巨尾、ChatGPT 翼/尾/法阵分别输出，以便复用身体并用 Timeline/Animator 做位移、旋转、缩放与颜色变化。

## 6. 事件/辅助状态预算

| 角色 | 必需状态 | 主图数 |
|---|---|---:|
| 豆包 | 7 个头像表情 | 7 张，不做全身战斗 |
| Codex | Idle、Scan、Working、Waiting、Success、Failed | 6 |
| Claude Code | Idle、MultiRepair、Permission、Success、TangledFail | 5 |
| OpenCode | Idle、LocalTerminal、IsolationStart、IsolationEnd | 4 |
| Zcode | Idle、UnrollBlueprint、PointRoute、RollBack | 4 |
| 即梦 | Idle、Operate、PreviewPlayback、CutTransition | 4 |
| MiniMax | Idle、Clap、Record、AudioCue | 4 |
| Qwen | Idle、FanOpen、FanClose、Correct、Wrong | 5 |
| Perplexity | Idle、CitationLink、Verified、BrokenSource | 4；尚缺视觉设计 |

## 7. Neuro / Evil Neuro 隐藏关资产

不得遗漏，且必须在主线资产完成后作为独立包制作：

| 角色 | 必需状态 | 主图数 |
|---|---|---:|
| Neuro | Idle、SingNote、GamePattern、Tease、Hurt、Defeat/Outro | 6 |
| Evil Neuro | Idle、KnifeNote、PlotPattern、Laugh、Hurt、Defeat/Outro | 6 |
| 双人构图 | SyncIntro、SwapSides、Argument、Reconcile/Exit | 最多 4 张，优先组合两人的单图 |

视觉必须查官方角色参考并记录许可证/可再分发边界；不能仅凭文字描述自由改发色、服装和耳朵。

## 8. 环境资产计划

| 章节 | 可复用 | 必须新增 |
|---|---|---|
| P0 | 新 `BG_P0_Far_DataSky_Loop_v01`、现有管道；旧 AICloud/DataCloud/Network 只作静态装饰 | 真循环 Mid/Near、教程箭头、Codex 扫描线、终点门 |
| C1 | P0 背景色变、豆包泡/河/浪、Gemini 弹 | 新 Gemini 本体、气泡文字组件、Boss VFX |
| C2 | Opus 参数山上下图 | 审稿室三层背景、规则墙、引用脚注、Claude Boss |
| C3 | ContextBug、部分管道 | Agent 工厂三层背景、修补节点、本地终端区、蓝图路径、Kimi 月轨 |
| C4 | 部分星点/引用弹 | 视频时间轴三层背景、录像残影、MiniMax 音波、Grok 雾/X 带 |
| C5 | 引用弹色变 | 开源夜市三层背景、Qwen 问答门、GLM Z 带/图谱/巨尾 VFX |
| C6 | 历代事件精简复用 | 模型王座三层背景、ChatGPT 结纹法阵、龙翼/尾判定 VFX |
| Hidden | 不复用主线角色身体 | 直播间背景、观众弹幕层、双人 Boss 弹幕 |

每章背景按 Far/Mid/Near 三层设计；Foreground 单列，不能把遮挡烘焙进 Near 导致无障碍设置无法降低 Alpha。每个滚动层必须独立通过“2048×1080、首末列像素差 0、首尾 16px 同带、双副本接缝不可见”四项门槛；否则只能标 `DECOR_ONLY`。

## 9. UI 新增清单

- 路线选择两张大卡及 4 秒演示框。
- P0–C6 章节节点、连线、锁定/完成/三难度徽记。
- 休闲/标准/困难图标和说明框。
- Harness 能量条、60 成本刻度、冷却环、覆盖数、余额预览。
- 失败横幅、C/B/A/S 徽记、三枚条件徽章。
- 图鉴四页签、证据等级 A/B/C/D 标记、机制演示播放器。
- 设置滑条、开关、重绑定行、二次确认框。
- Neuro/Evil 隐藏关解锁演出与节点。

## 10. 生产顺序

1. 完整机制和章节冻结。
2. 用现有素材/纯色块完成全部章节灰盒，不等待正式图。
3. 每章跑通 DS/HA、难度和失败来源。
4. 只为当前落地机制生产必需状态图；一状态一张，先靠 Unity 动效验证读感。
5. 当前小步主角/环境 → C1 → C2 → C3 → C4 → C5 → C6 → Hidden 顺序生产，不跨章囤图。
6. 每张透明资产做 Alpha、边缘、PPU、Pivot、缩小剪影检查；循环背景另做像素接缝和双拼预览。
7. 实机证明某动作靠单图无法读懂时，先登记原因，再追加最少关键帧；禁止恢复整套 6–12 帧预算。
8. 导入后才做最终特效配色；不能用概念图光效覆盖不清楚的判定。

## 11. 文件纪律

- 原图不覆盖；正式派生使用新版本号。
- 旧图不删除；状态写入本文件和 Import Label。
- `REFERENCE_ONLY` 不进入 Build。
- `DECOR_ONLY` 禁 Collider。
- 任何从概念稿裁出的临时图文件名必须包含 `_TEMP_CROP`，发布校验器发现即 Error。
- 最终 Sprite 文件名必须含角色/用途/动作/方向/版本，禁止 `final_final2.png`。
