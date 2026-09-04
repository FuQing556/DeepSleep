# 12｜系统、UX 与运行时规格 v1.0（部分历史）

> 2026-09-04：旧输入、路线选择、单角色HUD和单机状态流程失效。通用音画/无障碍条目仅作参考；新双人房间、跨端HUD和版本流程以v5规格为准。

> 本文把完整设计落成程序、UI、音频和 QA 可以共同执行的系统契约。具体飞行/伤害/首章数值仍由 `01_GameplaySpec.md` 拥有。

## 1. 场景结构

完整版使用三个场景，不为每章复制一个 Gameplay 场景：

| 场景 | 内容 | 生命周期 |
|---|---|---|
| `Bootstrap.unity` | AppContext、SaveService、SettingsService、LocalizationService、AudioService、SceneFlow | 启动后常驻 |
| `FrontEnd.unity` | 标题、路线/章节/难度选择、图鉴、设置、制作名单 | 每次回前端重建 |
| `Gameplay.unity` | 场景组合根、世界容器、玩家、HUD、对象池、关卡/Boss | 每次进入章节重建 |

规则：

- `AppContext` 只允许持有跨场景服务；不持有玩家、Boss、EncounterRunner 或池对象。
- `GameplaySceneContext` 只组装当前章节；退出时反序 Dispose。
- 禁止静态 `Instance` 和全局 `Get<T>()` 服务定位。
- 章节差异来自 `ChapterRouteDefinition`、Prefab 和背景集合，不复制场景。

## 2. 流程状态机

### 2.1 AppFlow

`Boot → FrontEnd → LoadingGameplay → Gameplay → LoadingFrontEnd → FrontEnd`

### 2.2 GameplayFlow

`Briefing → Tutorial/Flight → SignatureEvent → Flight → BossIntro → BossFight → Victory/Defeat → Result`

`Paused` 是覆盖状态，保存进入前状态；`UpgradeChoice` 也是覆盖状态。两个覆盖状态不能同时打开：

- 若升级触发时玩家已暂停，升级请求排队，恢复后下一帧打开。
- 若升级界面打开时按 Pause，只记录请求，不叠第二层菜单。
- BossIntro、Victory、Defeat 不允许升级请求。

## 3. 输入

### 3.1 Action Map

`Gameplay`：

| Action | Type | 默认绑定 | 路线 |
|---|---|---|---|
| `Flap` | Button | Space、Mouse Left | 共用 |
| `Shield` | Button | Left Shift | 仅 DeepSeek |
| `HarnessAim` | Pass Through/Pointer | Mouse Right + Pointer Position | 仅 Harness |
| `Pause` | Button | Escape | 共用 |

`UI`：Navigate、Submit、Cancel、Point、Click、ScrollWheel。

硬规则：

- Mouse Right 不绑定 Shield。
- HarnessAim 的 Press/Hold/Release 必须保留完整相位；不能只监听 performed。
- 当前路线不使用的动作仍存在于 asset，但由 `InputReader` 拒绝路由。
- UI Map 启用时 Gameplay Map 禁用；关闭 UI 后清空所有按住状态，防止幽灵输入。
- 重绑定校验禁止 `Flap` 与 `HarnessAim` 使用同一 Control Path。

### 3.2 Harness 指针状态

```text
Idle
  → RMB Press: Aiming
  → RMB Hold: update clamped target
  → RMB Release:
       valid + enough energy + cooldown ready → QueuedExecute
       otherwise → RejectedFeedback
  → ExecuteDelay ends → ResolveOnce → Cooldown
```

Pause、升级或死亡会取消 `Aiming`，不扣能量。`QueuedExecute` 在玩家死亡同帧取消且退款；Boss 已死亡则取消不退款统计中的空放，因为战斗已经结束。

## 4. 配置资产

### 4.1 `ChapterDefinition`

```text
id
titleKey
briefingKey
backgroundSet
musicSet
deepSeekRoute: ChapterRouteDefinition
harnessRoute: ChapterRouteDefinition
boss: BossDefinition?
unlockRule
galleryEntries[]
```

### 4.2 `ChapterRouteDefinition`

```text
routeId
encounterSegments[]
pickupBudget
upgradeThresholds[]
routeBossVariant
resultMedalThresholds
debugCheckpoints[]
```

DS 与 HA 禁止引用同一个 `EncounterSegment[]` 实例。校验器发现同引用直接 Error。

### 4.3 `EncounterSegment`

| Type | 必填数据 | 默认禁止并发 |
|---|---|---|
| Combat | entries, seed, scroll | 无；由条目验证通道 |
| BubbleCorridor | curve, duration, gap | Enemy、Obstacle、EnemyProjectile |
| GiantJump | profile, safeGap | Enemy、EnemyProjectile、OtherSignatureEvent |
| TimelinePreview | previewDelay, recordedEntries | OtherSignatureEvent |
| VisibilityPatch | rects, alpha, duration | HighSpeedUntelegraphedProjectile |
| RuleWall | ruleId, windows | OtherRuleWall |
| BlueprintPreview | futurePath, delay | OtherSignatureEvent |
| Boss | bossDefinition | Enemy、Obstacle、PickupTimeline |

### 4.4 `BossPatternType` 固定策略表

配置只能选择下表中已实现的策略 ID；禁止填写任意类名、脚本路径或反射字符串。角色 Boss 通过组合这些策略和参数表达：

| PatternType | 负责行为 | 当前使用角色/机制 |
|---|---|---|
| Straight / Fan / Radial | 直线、扇形、环形弹幕 | 通用、Gemini、Grok、Neuro |
| ArcBezier | 香蕉弧、曲线路径、音符轨迹 | Gemini、Kimi、Neuro |
| PredictedStraight | 预判玩家位置后锁定直射 | 通用、ChatGPT Think |
| MirrorTwin | 上下/左右镜像源与交替真身 | Gemini |
| MovingGap | 两侧实体或弹幕形成移动安全口 | Claude、ChatGPT 龙翼 |
| WeakpointSet | 多目标真假标记、命中/退款规则 | Gemini HA、Claude、GLM、ChatGPT |
| SpeechBlock | 白虚线无伤块与红实线伤害块 | 豆包表现、Gemini |
| RhythmTrack | 按谱线、节拍或声画提示生成轨迹 | Kimi、MiniMax、Neuro/Evil |
| BranchFork | 分支节点、权限门、修补目标 | Claude Code、OpenCode、Zcode |
| RuleWindow | 条件窗、拒绝墙、合规印章 | Claude、Qwen |
| VisibilityRect | 圣光/雾/内容折叠遮挡与安全轮廓 | Grok |
| TimelineRecordReplay | 记录伤害体轨迹后预演或逆放 | 即梦、Kimi、ChatGPT |
| StatusBand | 区域内限时修改一个白名单操作参数 | GLM 休眠带 |
| Sweep | 巨尾、X 戟、巨型身体横/纵扫 | Opus、Grok、GLM、ChatGPT |
| ModeCycle | 按固定表轮换攻击模式并提前显示图标 | ChatGPT 模型选择器 |
| SpeakerSwap | 共享血条下轮换当前可受伤主讲者 | Neuro/Evil Neuro |

每个 Pattern 实例必须声明 `telegraphDuration`、`damageSourceId`、`safeGapMetadata`、`timeoutPolicy` 和 `cleanupOwnerId`。阶段/段落退出时只由 Owner 回收自己的伤害体、预览层、协程和音源。无法由本表表达的新行为必须先新增代码策略、自动测试和本表条目，不能在 Inspector 临时挂匿名脚本绕过。

### 4.5 `DifficultyProfile`

只允许修改：敌弹速度、Boss HP、普通通道净高、预警时长、路线资源量、装饰遮挡 Alpha、允许的 Boss Pattern Variant。

禁止修改：玩家碰撞体、基础重力、输入延迟、伤害预警颜色、互斥标签、豆包段敌人数量、Boss 必需机制。

## 5. 三档难度

### 5.1 解锁

- 休闲、标准初始开放。
- 任一路线标准难度通关 C1 后解锁困难。
- 难度按章节选择，不锁整份存档。

### 5.2 修改原则

- 休闲提高反应时间和容错，不自动替玩家攻击。
- 标准是设计、QA、素材预警长度的权威基线。
- 困难更改节奏和安全口，不把 Boss 变成纯血牛。
- 各难度仍使用同一 Sprite/Collider；差异由配置和 Pattern Variant 表达。

### 5.3 Boss 重试

仅休闲在 BossIntro 保存内存快照。快照包含：当前 HP、路线能量、XP、Buff、统计、随机状态；不写入磁盘。重试 Boss 后评级最高为 A，结果页显示“使用 Boss 重试”。

## 6. 局内 Buff

### 6.1 候选生成

1. 过滤已达上限 Buff。
2. 过滤当前路线不支持 Buff。
3. 使用 authored seed 洗牌。
4. 取前 3 个不同 ID。
5. 候选不足 3 时显示 2；禁止复制同卡填满。

### 6.2 Harness 数值基线

已写入 `01_GameplaySpec.md` 的权威值如下，本页仅作为系统实现对照：

| ID | 精确效果 | 上限 |
|---|---|---:|
| HA_PARALLEL | 主攻击命中普通目标后，0.18s 后打击最近未命中目标，普通伤害 45%；对 Boss 只播视觉 | 1 |
| HA_PARAM | 选区半径 ×1.12 | 2 |
| HA_QUANT | 成本 -8，普通/Boss 伤害 ×0.95 | 2 |
| HA_CACHE | 被动恢复 +1.5/s，算力凭证 +5 | 2 |
| HA_CONTEXT | 最大能量 +30，并立刻恢复 30 | 2 |
| HA_SEARCH | 对明确真弱点 Boss 伤害 ×1.25；ExecuteDelay -0.08s | 1 |

倍率顺序：先基础数值，后加减值，再乘法，最后钳制。攻击成本最低 40，ExecuteDelay 最低 0.12s，选区半径最高 2.70u。

## 7. HUD 布局

参考分辨率 1920×1080。48px 是 16:9 桌面设计边距；Android 实际布局必须先取 `Screen.safeArea`，再在该矩形内应用缩放后的设计边距，禁止只靠固定像素躲刘海。

### 7.1 共用

| 控件 | 锚点/尺寸 | 行为 |
|---|---|---|
| Hearts | 左上，64px/心 | 受伤先闪对应心，不抖整个 HUD |
| XP | 底中，760×18 | 升级前 1 Token 时轻脉冲 |
| Buffs | 右上，56×56，最多 6 | 层数角标用 TMP |
| ChapterProgress | 顶部细线，520×8 | Signature/Boss 节点用图标标记 |
| BossHP | 顶中，900×32 | 仅 BossIntro 后出现 |
| Tutorial | 下中，最大宽 900 | 不盖玩家和选区 |

### 7.2 DeepSeek HUD

- 白饭能量条：左上心下，420×28；每 20 一格。
- 满能量后技能图标变亮并播放一次提示音；Windows 对应 Shift，Android 对应独立护盾按钮；不循环鸣叫。
- 自动锁定：目标脚下蓝鲸标；被障碍挡住时鲸标断成虚线并在 0.3 秒后淡出。
- 饭团撞墙只在第一次/每 1.5 秒显示小型“被挡住”图标，避免刷屏。

### 7.3 Harness HUD

- 能量条：左上心下，540×30；按每 60 成本显示主刻度。
- 冷却环：围绕当前瞄准圆心；Windows 跟随鼠标，Android 跟随拥有瞄准操作的触点；灰色不可用、白色合法、红黑执行中。
- 圆心上方显示预计覆盖普通目标数；Boss 时显示真弱点/假目标状态，不显示误导伤害数字。
- 松开前在能量条上预览扣费后的剩余值。
- 空放只统计“场上存在可命中目标但选区未命中”；纯飞行/清场段不统计。

## 8. 前端界面

### 8.1 主菜单

顺序：继续、开始/章节、图鉴、设置、制作名单、退出。无存档时“继续”隐藏而非灰色占位。

### 8.2 路线选择

每张卡必须有 4 秒循环演示：

- DS：自动饭团锁敌 → 被墙挡 → 绕墙命中。
- HA：右键圈中三目标 → 扣 60 能量 → 穿墙执行。

路线卡同时显示操作复杂度：DS 1/3，HA 3/3；不是强弱评级。

### 8.3 章节选择

- 横向线性节点 P0–C6。
- 每节点显示 Boss/事件剪影、DS/HA 最高徽记、三个难度记录。
- 未解锁节点只显示编号和一条前置线，不提前剧透角色全图。
- 隐藏关在解锁前完全不显示空槽。

### 8.4 图鉴

四个页签：角色、机制、梗来源、素材画廊。

- 角色页明确标记 A/B/C/D 证据等级。
- 梗来源页只写短摘要和外部链接，不复制长文。
- 机制页可播放无伤演示。
- Neuro/Evil 页面在隐藏关首次进入后解锁，不用普通模型参数比较。

## 9. 结果、统计与失败来源

### 9.1 `DamageSourceId`

必须是稳定枚举/ID：

```text
BoundaryTop, BoundaryBottom,
Pipe, Bubble, OpusBody, RuleWall,
EnemyContact, EnemyProjectile,
BossBody, BossProjectile,
GeminiBanana, GeminiSpeechBlock,
ClaudeSeal, KimiContextTrack,
GrokVisibilityHazard, GrokXSlash,
GlmSleepBand, GlmTailSweep,
ChatGptPraiseSeal, ChatGptRollback, ChatGptDragonTail,
NeuroNote, EvilKnifeNote
```

新增伤害机制没有 `DamageSourceId` 时校验器 Error。

### 9.2 失败建议

结果页按来源给一句不带嘲讽的建议：

- Bubble：保持平飞，小修正，不连续长按。
- OpusBody：先看地面黄影，再向上留通道。
- DS projectile blocked：换高度寻找射线。
- HA empty cast：松开前看覆盖数字和白框。
- Grok：记住洋红框位置，遮挡后跟安全描边。

## 10. 音频系统

### 10.1 组件

- `AudioService`：跨场景总线、设置、Music 状态。
- `GameplayAudioDirector`：按阶段请求音乐层与一次性 Cue。
- `SfxPool`：短 SFX AudioSource 池。
- `VoiceChirpPlayer`：角色 0.15–0.6 秒短音，不播放长对白。

### 10.2 并发

| 类别 | 最大并发 | 超限处理 |
|---|---:|---|
| 玩家连射 | 4 | 替换最老且音量最低 |
| 普通命中 | 6 | 合并 50ms 内同类事件 |
| 敌弹预警 | 4 | 高危险优先 |
| UI 点击 | 2 | 丢弃重复 |
| 角色短音 | 1/角色 | 新音打断旧音 |
| Boss 关键 Cue | 2 | 不丢弃，侧链音乐 |

## 11. VFX 与屏幕效果

- 所有伤害 VFX 必须在 Collider 生效前有预警或与实体边缘一致。
- `ReducedFlash` 模式不改变时间，只把全屏闪改成局部轮廓膨胀。
- 屏震用一个 `CameraImpulseMixer` 合并；不允许多个脚本直接改 Camera transform。
- 全屏颜色层顺序：世界 → Grok 机制遮挡 → 安全轮廓 → 受伤暗角 → HUD。
- Harness 选区永远在 Grok 遮挡之上，但真实目标白框可按机制只保留 0.6 秒。

## 12. 存档服务

### 12.1 槽位

首版单自动槽。设置与进度分文件：`settings.json`、`progress.json`，各自有 `.bak`。

### 12.2 写入时机

- 设置修改后 0.5 秒防抖写入。
- 章节结算确认后写进度；Boss 死亡瞬间不写，避免结果演出崩溃留下半状态。
- 解锁隐藏关/图鉴时与结算同事务写入。
- 正在 Play 时不每帧写盘。

## 13. 本地化

- Key 命名：`UI_*`、`TIP_*`、`CHxx_*`、`BOSS_*`、`MEME_*`。
- 中文标点和换行由语言表拥有，不在 C# 拼句。
- 豆包气泡短句从 `LocalizedBubbleLine[]` 抽取；相邻不重复，文本超出时缩放到最小 70%，仍超出则换下一句。
- 英文版保留梗解释但不直译成误导性产品称号。

## 14. 无障碍实现

| 设置 | 运行时影响 | 不得影响 |
|---|---|---|
| ScreenShake | Impulse 幅度乘 0–1 | Collider/判定 |
| ReducedFlash | 替换全屏闪 | 预警时间 |
| ProjectileContrast | 敌弹额外描边 | 弹体大小/碰撞 |
| ForegroundOpacity | 装饰遮挡 Alpha | Grok 真实机制时长 |
| HoldSensitivity | HoldLift×0.8/1/1.2 | TapRiseVelocity |
| AimAssist | 目标吸附≤0.35u | 能量/伤害/半径 |

辅助设置不降低成绩；难度是内容选择，无障碍是可访问性，不得混成“开辅助只能拿 C”。

## 15. 校验器新增规则

- 三个必需场景存在并进入 Build Settings。
- 每章两路线定义不共享同一 Encounter 数组引用。
- BubbleCorridor 的禁止并发标签完整。
- DifficultyProfile 未修改禁用字段。
- 每个伤害体有 DamageSourceId。
- 每个 Boss Phase 有预警时长、安全口元数据和超时处理。
- 所有前景装饰 Collider 为空。
- Harness Prefab 不包含 ShieldController；DeepSeek Prefab 不包含 HarnessAttackController。
- Input bindings 不冲突。
- 图鉴引用的外部来源条目存在于 ResearchLedger 映射表。

## 16. 自动测试最低集合

1. 输入相位：Press/Hold/Release、暂停取消、UI Map 切换。
2. DS 视线和饭团挡墙。
3. HA 选区钳制、一次结算、能量、冷却、取消退款。
4. 三难度只修改白名单字段。
5. BubbleCorridor 互斥验证。
6. 休闲 Boss 快照恢复完全一致。
7. 存档原子写入、备份恢复、版本迁移。
8. 所有 DamageSourceId 可映射到本地化建议。
9. 前景装饰无 Collider，降低 Alpha 设置生效。
10. Neuro/Evil 隐藏关解锁条件两条路径均有效。
