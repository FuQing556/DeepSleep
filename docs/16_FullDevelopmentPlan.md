# 16｜完整开发流程与任务计划 v1.1（历史：停止执行）

> 2026-09-04：本计划被 `20_Unity6MigrationAndDeliveryPlan.md` 取代。不得继续执行Unity 2022、扑翼、双路线或单机首发里程碑。

> 目标：从当前“只有参考/候选美术与 SampleScene”的状态，推进到可开源的 Windows x64 + Android 横屏完整单机版。  
> 本计划按依赖和验收门排列，不绑定虚假日历。任何阶段未通过门禁，不进入下一阶段批量生产。
> 实施必须遵守 `agent.md`：用户执行全部 Unity Editor 装配，Agent 只交付代码、测试、配置类型、素材与逐步操作清单；禁止硬编码玩法数据和隐式添加组件。

## 1. 当前起点

- Unity 2022.3.48f1c1，Built-in 2D。
- 只有 `Assets/Scenes/SampleScene.unity`，无玩法 C#、Prefab、配置资产和正式场景。
- Input System 未安装，仍为旧输入模式。
- Physics/Sorting Layer 未建立。
- 现有 Unity 美术全部保留，多数状态为 `PLACEHOLDER_ACTIVE` 或 `DECOR_ONLY`。
- 角色候选视觉已全部通过；正式状态图按当前玩法小步生产，不再预制多帧动作包。
- 当前目录不是 Git 仓库；正式编码前必须建立版本控制和忽略文件。

## 2. 总体路线

```text
D0 设计冻结
  → E0 工程基线
  → E1 核心飞行/流程
  → E2 双路线战斗
  → E3 关卡/Boss 框架
  → E4 UI/存档/难度/无障碍
  → C0 序章灰盒
  → C1 第一章垂直切片
  → C2..C6 逐章完成
  → H1/H2 隐藏内容
  → A0 正式资产与音频总替换
  → Q0 性能/兼容/发布
```

## 3. 开发门禁

### Gate A｜设计可实现

- 用户决策已登记。
- 每个机制有输入、预警、伤害、失败来源、路线解法和无障碍方案。
- 角色安排与“一状态一主图”预算完成。
- 现有素材复用表完成，零删除。

### Gate B｜工程可扩展

- 入口/菜单/序章场景、asmdef、Input、Layer、校验器和测试框架通过；后续每关独立场景。
- 没有全局服务定位器、字符串事件、反射行为配置。
- 没有 Runtime `AddComponent`、`RequireComponent`、缺件自动创建或编辑器自动改场景/Prefab。
- 所有玩法数值来自显式配置，所有依赖由用户在 Inspector/组合根装配。

### Gate C｜垂直切片可玩

- P0+C1 两路线、三难度、Gemini、结果/重试完整。
- 使用占位图也可以，但流程无断点、无 Unknown 失败。

### Gate D｜单章可发布

- 该章双路线、三难度、Boss、正式角色/环境/音频、自动测试和 20 局 Soak 通过；Windows 与 Android 输入均可完成。

### Gate E｜完整发布

- C0–C6、隐藏关、图鉴、设置、存档、许可证、双端性能、Windows 干净机与 Android 真机安装验证全部通过。

## 4. D0｜设计冻结

### D0.1 文档一致性

- [x] 更新 `DesignSpec.md` 权威表。
- [x] 清除所有旧输入、旧 Gemini 本体、Opus 旧玩法与豆包并发冲突描述；历史/禁用说明保留用于防错。
- [x] `01` 补 Harness Buff、三难度、路线结果统计。
- [x] `04/05/07` 明确入口、菜单与每关独立场景的职责；`07` 只管切片，完整版由本文件负责。
- [x] 清除当前资产表和视觉正典中的未决确认状态；旧版归档中的历史文本不作执行依据。

### D0.2 数据可表达性审查

- [x] 在 `12_SystemsAndUXSpec.md` 列出每种 SegmentType 和 PatternType。
- [x] 确认 C0–C6 使用显式策略 ID，不允许字符串反射或任意脚本名。
- [x] 在 E3 Pattern 策略任务中规定：表外行为必须新增代码、测试和策略登记。

### D0.3 资产准备

- [x] 全部现有文件按 `13_AssetReuseAndProductionPlan.md` 打状态标签，不删除。
- [x] 候选角色只登记语义状态，不为未实现玩法批量生成动画帧。
- [x] 参考图作者/许可证/未知边界进入 `08_ResearchLedger.md`；未知不等于获准再分发。

**状态（2026-08-31）**：D0 完成，Gate A 通过；可以进入 E0。未知许可证仍是 Q0 发布门禁，不阻止原创重绘和灰盒实现。

## 5. E0｜工程基线

### E0.1 版本控制

- [ ] 初始化 Git。
- [ ] Unity `.gitignore` 排除 Library/Temp/Logs/Obj/Build。
- [ ] 建立 `main` 与短期功能分支规则；禁止把生成缓存提交。
- [ ] 首次基线提交包含项目设置和设计文档。

### E0.2 包和设置

- [ ] 安装兼容 Unity 2022.3 的 Input System、TMP、Test Framework。
- [ ] Active Input Handling 设为 New。
- [ ] Fixed Timestep 0.02，重力 -9.81，目标 60 FPS。
- [ ] 建 Physics Layer、Sorting Layer、Collision Matrix。
- [ ] Android 只允许横屏；应用标识、最低 API、签名与架构等值先记录，具体填写拆为独立人工步骤。

### E0.3 目录与程序集

- [ ] Runtime、Editor、EditModeTests、PlayModeTests 四 asmdef。
- [ ] Data/Prefabs/Scenes/Settings/Audio/Localization 完整目录。
- [ ] Runtime 不引用 Editor；测试不进入 Player build。

### E0.4 场景和服务

- [ ] Bootstrap、FrontEnd、P0_Prologue 三个首批场景；C1–C6 与 H1/H2 到对应里程碑逐个建立。
- [ ] AppContext：Save/Settings/Localization/Audio/SceneFlow。
- [ ] GameplaySceneContext：Flow/Input/Player/Pool/Encounter/Boss/UI/VFX。
- [ ] Build Settings 顺序固定。

### E0.5 校验器

- [ ] `DeepSleep/Validate Project` 菜单。
- [ ] Layer、Input、Definition ID、Prefab、Sprite、Encounter、DamageSource、Decor Collider 校验。
- [ ] 有 Error 阻止 Development Build。

**退出条件**：空场景 Play/Stop 20 次 0 Error；校验器故意制造/修复错误测试通过。

## 6. E1｜流程、飞行和伤害

### E1.1 App/Game Flow

- [ ] AppFlow 和 GameplayFlow 两级状态机。
- [ ] Pause、UpgradeChoice 覆盖状态互斥。
- [ ] Loading、Briefing、Result 可跳转且无重复初始化。

### E1.2 输入

- [ ] Flap/Shield/HarnessAim/Pause/UI Actions。
- [ ] Press/Hold/Release 快照；Map 切换清按住状态。
- [ ] 重绑定冲突检查。
- [ ] Desktop 与 MobileTouch 两个输入源汇入同一快照；Android 两指并发飞行+技能/瞄准。
- [ ] TouchId 所有权、手指取消、应用失焦与旋转/分辨率变化后状态清空测试。

### E1.3 PlayerMotor

- [ ] 点按、长按、下落限速、VisualRoot 旋转。
- [ ] X 固定、边界、Continuous/Interpolate。
- [ ] DS/HA 共用同一 Motor；禁止复制代码。
- [ ] Current/Ghost 两个显式 SpriteRenderer 的单残影状态表现；过渡参数来自配置，残影不影响物理根。

### E1.4 Health/Damage

- [ ] DamageInfo、sequenceId、阵营、伤害顺序。
- [ ] 3 心、护盾、无敌帧、同物理步去重。
- [ ] DamageSourceId 和结果建议映射。

### E1.5 重开清理

- [ ] 玩家、相机、统计、输入、协程、池、音频快照完全重置。

**退出条件**：飞行/暂停/伤害/死亡/重开自动测试通过；使用现有 DS 图和 Harness 色块占位可玩。

## 7. E2｜双路线战斗与成长

### E2.1 DeepSeek

- [ ] TargetingService、Obstacle Linecast、目标优先级。
- [ ] AutoShooter、饭团转向、撞墙回池。
- [ ] 白饭能量与 ShieldController。

### E2.2 Harness

- [ ] Pointer world conversion、屏内/距离钳制。
- [ ] Aiming/QueuedExecute/Cooldown 状态机。
- [ ] 一次 Physics2D Overlap 查询、去重目标、忽略障碍。
- [ ] 能量、凭证、退款、空放统计。

### E2.3 Pooling

- [ ] 玩家弹、敌弹、敌人、拾取、VFX、气泡池。
- [ ] OnRent/OnReturn 全状态重置。
- [ ] 池耗尽策略和限频日志。

### E2.4 XP/Buff

- [ ] Token、阈值、候选生成、暂停选择。
- [ ] DS 六 Buff、HA 六 Buff、路线 UI 文案。
- [ ] 加减值/乘法/钳制顺序测试。

**退出条件**：DS 挡墙测试、HA 一圈三目标测试、6+6 Buff 组合测试、5 分钟稳定战斗 0B/frame。

## 8. E3｜关卡和 Boss 框架

### E3.1 EncounterRunner

- [ ] ChapterDefinition/RouteDefinition/SegmentDefinition。
- [ ] authored seed、暂停不推进、Debug checkpoint。
- [ ] Segment 禁止并发标签验证。

### E3.2 Segment 实现

- [ ] Combat、BubbleCorridor、GiantJump、TimelinePreview、VisibilityPatch、RuleWall、BlueprintPreview、Boss。
- [ ] 事件开始/结束负责清理自身 Collider/VFX/音频。

### E3.3 Boss Runtime

- [ ] BossFacade/Health/Phase/PatternRunner。
- [ ] 可取消 Pattern，阶段切换清旧任务。
- [ ] RouteBossVariant 选择 HP、弱点与奖励；不复制整套框架。
- [ ] Boss 超时和胜负同帧裁决。

### E3.4 Pattern 策略

- [ ] Straight/Fan/Arc/Bezier/Radial/Predicted。
- [ ] MirrorTwin、MovingGap、WeakpointSet、VisibilityRect、ReplayTrack、RuleWindow、Sweep。
- [ ] 新策略逐个有 EditMode 数学测试和 PlayMode 判定测试。

**退出条件**：用几何占位完整跑一个 Bubble、Opus、Replay、Fog、RuleWall 和三阶段测试 Boss。

## 9. E4｜前端、难度、存档和无障碍

### E4.1 FrontEnd

- [ ] MainMenu、Route、Chapter、Difficulty、Gallery、Settings、Credits。
- [ ] 键盘/鼠标与触屏都能完整导航；点击目标位于 Safe Area。

### E4.2 HUD/Result

- [ ] 共用 HUD、DS/HA 专用 HUD、Boss HUD。
- [ ] 结果徽记、路线统计、失败建议、下一章/重试。
- [ ] 休闲 Boss 快照与 A 评级上限。

### E4.3 Difficulty

- [ ] Casual/Standard/Hard Profile。
- [ ] 白名单字段验证；Bubble 永不叠怪测试。
- [ ] Hard 解锁和记录。

### E4.4 Save

- [ ] settings/progress 分文件、tmp 原子写、bak 恢复、版本迁移。
- [ ] 清进度二次确认。

### E4.5 Accessibility

- [ ] Shake、Flash、Contrast、ForegroundOpacity、HoldSensitivity、AimAssist。
- [ ] 无障碍不降评级。

**退出条件**：删主存档可从备份恢复；三分辨率/三难度/两路线 UI 无裁切和输入冲突。

## 10. 每章统一制作流水线

每章严格经过六步：

1. **Graybox**：几何、占位角色、无正式特效。
2. **Mechanic Lock**：新机制判定/预警/失败来源通过。
3. **Dual Route**：DS/HA 分别编排和无 Buff 通关。
4. **Difficulty**：三档 Profile/Variant 验证。
5. **Production Art/Audio**：按当前机制所需状态逐张替换；轻量动画优先用 Transform/颜色/VFX。
6. **Polish/QA**：20 局 Soak、性能、本地化、徽记阈值。

任一步失败回到当前步；禁止靠下一步素材或数值掩盖。

## 11. C0｜序章

- [ ] Codex ScanPath/ScanTarget/PatchNode。
- [ ] DS 挡墙教学、HA 三目标/穿墙教学。
- [ ] 路线换角无损返回。
- [ ] 无升级、无 Boss、2.5–3.5 分钟。

**验收**：首次玩家 90% 能完成；两路线关键规则各能口述一条。

## 12. C1｜日用助手堵塞

- [ ] `03` 两时间轴全部实现。
- [ ] 豆包曲线/互斥；旧河/浪作 Decor。
- [ ] Gemini 两路线三阶段。
- [ ] 现有旧 Gemini 只作失败复制体。
- [ ] 第一章三升级和完整结果。

**验收**：DS 40–55s、HA 42–58s Boss；44–60s 只有豆包；20 局无必死组合。

## 13. C2｜最高规格审稿

- [ ] Perplexity 引用、Opus 跳起、RuleWall。
- [ ] 旧参数山背景/前景无 Collider。
- [ ] Claude 三阶段、授权退款规则。
- [ ] C2 新背景、Boss、纸张/盖章音频。

**验收**：每条规则先无伤示范；HA 穿墙不等于无脑攻击；Opus 安全口≥2.05u。

## 14. C3｜Agent 工厂

- [ ] Claude Code 多修补/权限门。
- [ ] Codex 引路、OpenCode 隔离、Zcode 蓝图。
- [ ] Kimi 友军、过载、三阶段 Boss、恢复。
- [ ] 辅助状态和 Boss 状态使用同一身份但不同控制器。

**验收**：Kimi 先友后敌再友剧情清楚；预演轨迹无 Collider；两路线都能识别真专家。

## 15. C4｜生成影像迷雾

- [ ] 即梦未来两秒回放。
- [ ] MiniMax 声画双提示。
- [ ] Grok 圣光、X 时间线、反骨三阶段。
- [ ] ReducedFlash/ForegroundOpacity/Grok 层级测试。

**验收**：关音频仍可玩；雾中保留安全描边；没有色情素材；预警不撒谎。

## 16. C5｜百模开源夜市

- [ ] Qwen 机制问答、一次工具 Buff。
- [ ] Perplexity 来源线。
- [ ] GLM 休眠带、知识图谱、巨尾三阶段。
- [ ] 控制削弱≤1.5s，仍保留 Tap/执行。

**验收**：题目只考本局规则；错误不秒杀；GLM/Qwen/Zcode 轮廓不混淆。

## 17. C6｜模型王座

- [ ] 五个旧机制精简复现，段间 4 秒恢复。
- [ ] ChatGPT 四阶段和白龙巨大半身。
- [ ] 谄媚印章、模型切换、回滚、龙翼/尾/法阵。
- [ ] 最终演出、双路线结局记录、Credits 解锁。

**验收**：回滚不改 HP/Buff/阶段；DS 可主动断锁，HA 可控制印章；最终净通道≥2.15u。

## 18. H1｜Neuro / Evil Neuro

- [ ] 两种解锁路径。
- [ ] 官方角色视觉考据和来源登记。
- [ ] Shared HP、主讲环、Neuro/Evil/双人三阶段。
- [ ] 独立直播间背景、音乐和奖励。
- [ ] 不接真实直播、不读取弹幕。

**验收**：主讲切换清楚；DS/HA 都不会攻击错误目标后陷入不可通关；隐藏关不提供永久数值。

## 19. H2｜语音助手博物馆

- [ ] Siri/小爱视觉与历史来源核对。
- [ ] 波形门、听错命令、家居设备机关。
- [ ] 视觉和音频双提示。

**验收**：3–4 分钟、无 Boss、无主线阻塞、无“老用户/老人很蠢”嘲讽。

## 20. A0｜正式美术和音频

- [ ] 按 `13` 主角/Boss/事件状态预算生产，不跨阶段囤逐帧图。
- [ ] 每章 Far/Mid/Near/Foreground 分层；所有滚动层通过像素首尾与双拼检查。
- [ ] 现有资产不删除，更新状态标签。
- [ ] 透明 Alpha、PPU、Pivot、9-slice、缩小剪影验收。
- [ ] 每章音乐、Boss 层、关键 SFX、角色短音。
- [ ] 未授权参考不进 Build。

**退出条件**：无 `_TEMP_CROP`、无错误 Gemini 本体、无假透明棋盘、无文字烘焙按钮。

## 21. Q0｜性能、兼容和发布

### Q0.1 自动化/回归

- [ ] EditMode/PlayMode 全绿。
- [ ] 所有章节两路线三难度 smoke test。
- [ ] 重开、暂停、升级、同帧死亡/胜利、阶段切换回归。

### Q0.2 性能

- [ ] Windows 1080p 与目标 Android 真机 Boss 峰值优先 P95<16.6ms。
- [ ] 稳定战斗 GC Alloc=0B/frame。
- [ ] 30 分钟 Soak 无池增长、MissingReference、音源泄漏。

### Q0.3 兼容

- [ ] 1280×720、1920×1080、2560×1440。
- [ ] 16:10/4:3 Letterbox、18:9/20:9 宽屏扩展和刘海/圆角 Safe Area。
- [ ] 键盘鼠标、触屏多指、重绑定、无障碍组合。
- [ ] Windows 干净机解压运行。
- [ ] Android 两台不同宽高比真机完成安装、覆盖升级、清装、暂停恢复、本地存档与 30 分钟游玩。

### Q0.4 开源交付

- [ ] README：玩法、控制、构建、目录、贡献规范。
- [ ] LICENSE、ThirdParty/References 来源表。
- [ ] 未授权参考图从发行包和公开仓库排除或替换为链接/文字记录。
- [ ] Development/Profile/Release 配置。
- [ ] GitHub Releases 同时提供 Windows ZIP 与签名 APK；准备粉丝群文件和国内网盘镜像，校验文件哈希一致。
- [ ] 首发包不包含 WebGL/微信/TapTap/4399 SDK、登录、广告、支付或联网权限。

## 22. 风险登记

| 风险 | 早期信号 | 处理 |
|---|---|---|
| 角色很多导致动画爆量 | Graybox 未过就开始批量生图 | 一状态一主图；只按当前机制逐张生产，追加帧必须有实机理由 |
| 双端后补导致返工 | 先按鼠标单指和 16:9 UI 写死 | 从 E0 建逻辑玩法区、Safe Area 与桌面/触屏输入源；每个里程碑双端冒烟 |
| 两路线退化为换皮 | Encounter 引用相同数组 | 校验器 Error；单独录像验收 |
| Harness 太烧钱无法通关 | 无 Buff 模拟能量不足 | BossIntro 资源下限、阶段凭证、调整窗口而非无限能量 |
| 弹幕/遮挡看不清 | 玩家死因 Unknown/误判高 | 统一颜色语言、高对比、描边、录像复盘 |
| 回滚/预演状态泄漏 | 阶段切换旧伤害体仍在 | 可取消序列、段落 Owner 清理、池生命周期测试 |
| 存档损坏 | 异常退出后 JSON 半写 | tmp+replace+bak+迁移测试 |
| 参考图版权不明 | 参考文件进入 Build | Import Label + Build 校验阻止 |
| Neuro 被遗漏/乱改 | 主线结束仍无 H1 任务 | H1 是正式里程碑；官方人格/视觉验收单 |

## 23. 每个任务的交付格式

```text
Task ID：
目标行为：
权威文档：
修改文件：
Prefab/配置：
自动测试：
人工验收步骤：
性能/GC：
现有素材如何复用：
未完成/风险：
```

没有自动测试或人工验收步骤的任务不能标完成；“看起来差不多”不是验收。
