# DeepSleep 当前开发交接（2026-09-14）

工程：`D:\Unity Work\DeepSleep_Unity6`  
Unity：`6000.6.0f1`，Universal 2D  
分支：`main`  
当前本地 HEAD：`23d4a32 feat: complete playable prototype milestone v1.1.2`  
当前远端 `origin/main`：`650f511 feat: add meta progression and level selection loop`

> 本文是新窗口的唯一当前交接入口。根目录旧版交接原本停留在 2026-09-04 建工程阶段，已整体替换。设计细节仍以正式文档和用户最新口径为准。

### 2026-09-14 补充：联机命中反馈修复，待随大更新打包

- 用户要求修复手机联机 HS 激光命中特效和伤害数字丢失，并补充手机作为房主时也会发生；本轮不出新包。
- 已新增可靠的 `NetworkCombatFeedbackChannel`，同步激光/近战命中特效与 DS/HS 跳字；修正客人端 HS 命中特效被 AuthorityOnly 禁用的问题。
- 已通过 Editor Installer 装配测试关和 World01；各完成 15 项编辑器 Play 回归，新鲜 Console 0 error、0 warning。未做手机实机复测，手机房主是否另有设备端问题仍需下一轮包验证。
- 证据与打包后复测项：`docs/ImplementationEvidence/20260914_NetworkCombatFeedback/README.md`。
- 改动未提交，未构建或发布任何包；请随下一轮大更新一起打包。
- 用户随后明确“电脑房主/客人都正常，只有手机联机异常，双方单机正常”。因此该实机 Bug **尚未确认解决**，已修同步缺口不能直接当作完整根因。包内相关 266 方法比对仅 3 处 Wi-Fi 发现平台差异；电脑日志确认用 v1.1.2 EXE，手机安装版本未确认，ADB 暂无设备。详见上述证据中的补充调查，后续优先获取手机现场信息。
- 后续最新证据覆盖上一条设备状态：ADB 已连通，手机确认 v1.1.2/版本码4；用户又在**电脑客人**复现，怀疑 AI 托管期间发生，故不是已证实的手机独有问题。托管真实控制路由回归在两个场景各通过20项；未发现托管直接禁用表现，旧包缺少逐次托管/命中日志，触发顺序仍未知。现场细节见证据文档最新小节，实机问题仍待包含修复的新包验收。

## 1. 新窗口必须先知道的工作方式

- 用户希望连续完成一批有意义的工作，不要每改一点就停下来询问、重新读图、重复检查。
- 开工前简短说一声即可；只有会实质改变设计、存在破坏风险或确实缺少决定时才停下来问。
- 不要凭聊天记忆宣称完成。先检查工作树、现有代码、场景装配和实际预览。
- 工作树很脏，包含今天尚未提交的第一世界内容以及用户已有修改。禁止 `git reset --hard`、`git checkout --`、批量删除或覆盖无关文件。
- 当前用户没有要求提交或推送。完成并验证前不要 commit/push。
- 对本项目的角色、敌人、特效、背景生图，必须先完整阅读：
  - `C:\Users\Administrator\.codex\skills\deepsleep-art-generation\SKILL.md`
  - 抠图时再读 `C:\Users\Administrator\.codex\skills\deepsleep-sprite-matting\SKILL.md`
- 用户已确定的素材流程：生成时优先使用饱满、与主体反差大的纯色底（通常绿色），之后本地抠图；不要依赖生成模型直接给透明图。
- 角色简称：DeepSeek 为 DS，Harness 为 HS。不要再称 Harness 为 HA（代码中历史命名仍可能保留 `Harness/HA`）。
- Unity 场景、Prefab、配置资产现在可以通过项目内 Editor Installer 显式修改；不要手改 YAML，不要运行时偷偷 `AddComponent` 或用全局 `Find` 掩盖缺引用。

## 2. 项目大状态

游戏已经拥有一个可玩的三段测试闭环，不是刚起步工程：

- Windows / Android 横屏双端。
- 单人搭档 AI、局内 AI 托管。
- 局域网发现与房间、公网 WebSocket 中继的合作联机基础。
- DS 与 HS 两套战斗、倒地复活、复活保护、节点强化、Token、战败回节点、三段结算。
- 主菜单、选关、商店、背包、成就占位与本地持久化。
- 测试关卡闭环已作为 `v1.1.2` 本地里程碑提交。

不要在本轮重做这些系统。当前开发线已经进入“第一世界：互联网初期黄昏城市”的真实内容生产。

## 3. 当前第一世界的主题与已定构图

正式设计文档：`docs/34_World01_DoubaoAndCityPlan.md`。

主题不是普通赛博城市：

- 互联网初期的希望与欣欣向荣。
- 黄昏、下班放学后的自由，以及一天过去却一事无成的淡淡哀伤。
- 对遥远大城市的向往。
- 旧电视、普通住宅、真挚感情、单纯朴素的人情味。
- 避免霓虹赛博、豪华神殿、废墟末世和机械对称构图。

场景分层口径：

- 远景：可水平循环的黄昏天空和遥远城市。
- 固定前景：不是盖满整个底边的一条楼，而是左下、右下若干独立城市剪影块；角色可以飞到后面，主要承担遮挡和角色站位。
- 独立太阳：黄昏段从右侧缓慢向左下移动，模拟落山；本段背景光影不动态重绘，进入下一个节点后才切换下一时刻。
- 环境云：半透明、偏写实、狭长流动，与豆包圆气泡完全不同；从右侧屏外随机高度生成，慢慢向左飘，离屏回收。暂时只做视觉遮挡，没有伤害或特殊效果。
- 夜晚背景、月亮、更低能见度的云和 Kimi 遭遇以后制作。

## 4. 豆包遭遇：当前已经实现的部分

新场景及接入文件已经存在，但尚未提交：

- `Assets/Scenes/World01_EarlyInternet.unity`
- `Assets/_Project/Configs/Progression/Meta/CFG_META_Level_World01_EarlyInternet.asset`
- `Assets/_Project/Scripts/Runtime/Progression/Run/DoubaoChapterEncounterDriver2D.cs`
- `Assets/_Project/Scripts/Runtime/Progression/Run/IChapterCombatObjective.cs`
- `Assets/_Project/Scripts/Editor/Setup/DoubaoEncounterInstaller.cs`
- `Assets/_Project/Scripts/Editor/Setup/DoubaoRoundBubbleInstaller.cs`

运行时核心：

- `Assets/_Project/Scripts/Runtime/Combat/Encounters/Doubao/DoubaoWordWallConfig.cs`
- `Assets/_Project/Scripts/Runtime/Combat/Encounters/Doubao/DoubaoWordWallEncounter2D.cs`
- `Assets/_Project/Scripts/Runtime/Combat/Encounters/Doubao/DoubaoWordWallBlock2D.cs`
- `Assets/_Project/Scripts/Runtime/Combat/Encounters/Doubao/DoubaoBoss2D.cs`
- `Assets/_Project/Scripts/Runtime/Combat/Encounters/Doubao/DoubaoWordWallReplicaView2D.cs`
- `Assets/_Project/Scripts/Runtime/Networking/DoubaoEncounterNetworkChannel.cs`
- `Assets/_Project/Scripts/Runtime/Combat/Projectiles/IEnemyProjectileBlocker2D.cs`

已经完成/已装配的行为：

- 豆包词墙使用对象池，由固定模拟驱动。
- 服务器/房主权威生成、碰撞、伤害；客户端使用无碰撞 Replica 表现。
- 第一波按逻辑下落时间触发豆包本体，不能因第一波提前被清掉而卡死。
- 豆包出现后持续生成，击败后停止并清空所有存活气泡，完成章节附加目标。
- 圆气泡使用 `CircleCollider2D`；单个气泡显示一个字。
- 玩家或怪物本体碰到气泡：尝试结算一次伤害并立即消耗气泡。
- DS 饭团和敌方弹体的墙体交互已经接入现有伤害/拦截入口；HS 激光沿现有贯穿逻辑工作。
- 气泡碰撞角色播放命中特效，气泡被打碎播放碎裂特效。
- 豆包有常态/讲解动作切换、动作残影以及沮丧离场淡出。
- 网络内容版本目前由安装器写为 `20260914-doubao-round-3`。

现有验证记录：`docs/ImplementationEvidence/20260914_DoubaoEncounter/README.md`。注意其中部分截图和文字对应上一版长词块，只能证明基础遭遇链路；不能证明最新迷宫密度已经合格。

## 5. 豆包美术：哪些已经可以直接用

正式 Assets：

- `Assets/_Project/Art/Characters/Doubao/SPR_DB_RooftopExplain_v01.png`
- `Assets/_Project/Art/Characters/Doubao/SPR_DB_Lecture_v01.png`
- `Assets/_Project/Art/Characters/Doubao/SPR_DB_Dejected_v01.png`
- `Assets/_Project/Art/VFX/Doubao/VFX_DB_RoundBubble_v01.png`
- `Assets/_Project/Art/VFX/Doubao/VFX_DB_BubbleImpact_v01.png`
- `Assets/_Project/Art/VFX/Doubao/VFX_DB_BubbleBreak_v01.png`
- `Assets/_Project/Art/Foregrounds/World01/BG_W01_ForegroundCity_v01.png`

生产源、抠图报告和深浅背景预览：

- `docs/ArtProduction/20260914_DoubaoBattleSprite/`
- `docs/ArtProduction/20260914_DoubaoRoundBubble/`
- `docs/ArtProduction/20260914_DoubaoWordWall/`（旧气泡候选，仅留档）
- `docs/ArtProduction/20260914_World01Foreground/`

已确认口径：

- 气泡必须是近圆形，不要对话框箭头/尾巴；用户此前说“箭头在右边”是描述方向时造成误会，随后已明确取消角。
- 视觉约 70%—80% 不透明，首版取 75%；气泡文字本身保持清晰。
- 每个气泡只放一个字，3—4 个气泡组成一个短语组。
- 豆包战斗时常态与“闭眼抬头、手指晃动”的贴图交替，切换带现有项目统一残影。
- 击败后使用沮丧/垂头丧气贴图淡出离场。

## 6. 当前最重要的未完成项：气泡布局仍然是错的

`DoubaoWordWallEncounter2D.SpawnWave()` 目前还是“每行 4 组，组间留 1—1.5 个 HS 宽度”的均匀分布。用户已经否定这一版：它看起来只是散开的气泡组，不是迷宫；早一版更密的方案又完全无路可躲。

用户最新硬口径：

- 把横向玩法区想成约 7 个离散单位。
- 一行通常只有少量空位，其余多数单位都有 3—4 个气泡组成的墙。例如 1、3、4、5、7 有墙，只能从 2 或 6 通过。
- 纵向也类似，不能每排之间都有一整条安全横带。
- 必须先规划一条连通路线，再填充其余气泡墙；开口少，但玩家一定能从上到下/随下落持续穿行。
- 迷宫持续从上往下落，玩家在通道中躲避，同时清怪/拆墙。
- 不能让普通小怪轻易把整个机制清空；密度和通路应由布局而不是固定死坐标体现。

推荐的首版落实（这是交接时的设计推导，尚未写进代码）：

1. 配置层增加 `mazeColumns = 7`、每排开口数、排间距、路径横移上限等显式字段；不要把 7 和数值散落硬编码在生成函数。
2. 维护一个主通路列。每生成下一排，主通路只允许保持或横移一列。
3. 为保证横移时连通，当前排同时留出旧主通路列和新主通路列；如果没有横移，再补一个随机支路开口。
4. 其余约 5 列全部生成随机 3/4 气泡组。各组内部继续复用现有 Pattern 和单字短语逻辑。
5. 行距设为“组高 + 很小净空”，避免整行安全带；实际值必须看 HS 尺寸和游戏预览。
6. 7 列覆盖 19.2 世界单位时，单空格的净宽大约能落在 1—1.5 个 HS 宽度附近；需要用实际 HS 常态贴图/碰撞尺寸复核，不可只算公式就宣称合格。
7. 活跃气泡数会从当前 96 上升，预计池和网络视图上限需要提高到约 160；修改前先按屏内最多行数计算。

不要继续微调旧 `GroupsPerWave + GapCharacterRange` 方案，它的结构与用户目标不一致。

## 7. 当前第二个未完成项：黄昏背景尚未成为真循环图

当前 Assets 中：

- `Assets/_Project/Art/Backgrounds/BG_W01_Far_DuskCity_Loop_v01.png`

但用户已经指出新画的非对称黄昏城市左右无法拼接，不能作为最终循环背景。不要因为文件名含 `Loop` 就当它已通过。

正在制作的 v02 源文件：

- `docs/ArtProduction/20260914_DuskSeam/raw/DuskCity_source.png`
- `docs/ArtProduction/20260914_DuskSeam/raw/DuskCity_offset.png`
- `docs/ArtProduction/20260914_DuskSeam/seam.py`

当前精确状态：

- 新黄昏城市原图已生成，用户评价“挺好看”，构图不再镜像对称。
- `seam.py prepare` 已执行：把原始左右边界平移到画面中心，得到 `DuskCity_offset.png`。
- **尚未执行**中心接缝的生成式修补。
- **尚不存在** `raw/DuskCity_offset_repaired.png`。
- **尚未执行** `seam.py finalize`。
- **尚未生成/验收** `ready/BG_W01_Far_DuskCity_Loop_v02.png` 与三连预览。
- **尚未替换** World01 场景里的 v01。

正确续做流程：

1. 用图像编辑模式引用 `DuskCity_offset.png`，只修复中央较宽竖带，让云带、蓝紫渐变、地平线/水面和城市自然跨过中心；左右外侧尽量原样保留。
2. 明确禁止镜像、对称、重复楼群、太阳、月亮和文字。
3. 将编辑结果保存为 `docs/ArtProduction/20260914_DuskSeam/raw/DuskCity_offset_repaired.png`。
4. 用项目内已可用的带 Pillow Python 运行 `docs/ArtProduction/20260914_DuskSeam/seam.py finalize`。脚本只混合中心 44%、羽化外侧 8%，反向平移并生成三连预览。
5. 必须实际查看 `previews/triple_small.jpg` 和 `previews/join_center.png`。边缘像素相等只是最低门槛；三张并排不能出现明显接缝、镜像感或机械周期重复。
6. 验收通过后再复制为 Assets 中的 v02，并用 Editor Installer/Unity API 替换场景引用，不手改 `.unity` YAML。

若中心修补仍有明显接缝，应继续修 v02，不要用左右镜像蒙混过关。用户明确不要镜像循环。

## 8. 背景之后的未完成表现

### 用户新增待优化项（2026-09-14，仅记录，后续统一修复）

- **AI 托管自动准备前往下一关**：目前角色进入托管后，不会自行点击准备前往下一关，仍需真人操作。后续补齐托管状态下的节点准备/前往下一关流程。本次用户明确只记录，不立即实现。

以下已经获得用户许可继续，但尚未完成：

- 把整条底边前景改成左下、右下独立楼体块；当前 `BG_W01_ForegroundCity_v01.png` 仍可能覆盖底边过多。
- 实现狭长半透明环境云：右侧随机高度生成、慢速左移、离屏复用。
- 独立太阳与轻量耀斑表现；太阳缓慢落向左下。
- 为这些元素建立显式配置、对象池/复用和正确渲染层级。
- 检查超宽手机比例，远景和装饰层不能再次露出深蓝色空条。

这些环境云不参与豆包气泡迷宫碰撞，不能复用豆包圆泡逻辑。

## 9. 近期修改中应保留的其他内容

工作树中还有本轮之前已经实现但未提交的内容，包括：

- DS 饭团/HS 激光强化和连锁表现相关配置及代码。
- 敌方弹体阻挡接口与机械蛇配置调整。
- 主菜单、关卡路由、章节运行和第一世界关卡注册。
- `ProjectSettings/EditorBuildSettings.asset`、`TagManager.asset` 的场景/层配置。
- 美术生产工具：`BuildHorizontalLoopTile.ps1`、`prepare_sprite_batch.py`、`trim_alpha.py`、`requirements.txt`。
- 简历资料位于 `docs/Resume/`，与当前玩法任务无关，禁止顺手删除或改写。

接手时先运行：

```powershell
git status --short
git diff --stat
```

不要假设所有未跟踪文件都由当前任务产生，也不要把它们清理掉。

## 10. 验证要求

当前最新圆泡和豆包动作装配后，尚缺一次完整的新鲜验证。新窗口完成迷宫与背景后至少检查：

- Unity Console 新鲜状态为 0 error；警告也要逐条确认，不拿旧 Console 冒充结果。
- 从 `Boot/MainMenu` 进入 World01，不直接从单场景启动后把入口注入缺失当新 bug。
- 单人 DS、单人 HS：气泡迷宫可读、通路连通、不是整行安全带、不是无解密墙。
- 气泡命中玩家有反馈；被攻击清除有碎裂反馈；文字与圆泡层级正确。
- 普通怪不会在豆包出现前轻易把整个场地清空。
- 首波被提前清掉时豆包仍会按逻辑时间出现。
- 豆包击败后停止生成、当刻清场、沮丧淡出、章节目标完成。
- 房主和客户端看到相同气泡 ID、位置、文字、消耗、豆包状态与离场；客户端 Replica 无碰撞。
- 黄昏 v02 三连图无明显接缝，并在 16:9 和超宽手机视野都无漏底。

## 11. 新窗口建议执行顺序

1. 完整阅读本文件和 `docs/34_World01_DoubaoAndCityPlan.md`。
2. 阅读项目美术 skill；检查当前 `git status`，确认 Unity 是否仍在 Play/Pause，编辑场景前先退出播放模式。
3. 先完成黄昏背景 v02 的中心修补、三连预览和人工视觉验收。
4. 再重构豆包生成器为 7 列、少量开口、主路线连通的滚动迷宫；同步提高池/网络容量。
5. 运行 Editor Installer 装配 v02 与新配置，修复新鲜编译错误。
6. 从主菜单完成一轮实际游戏验收并保存证据。
7. 再做左右前景块、环境云和独立太阳。
8. 向用户一次汇报一整个完成批次；未获指示不要提交或推送。

## 12. 可直接复制给新窗口的首条消息

> 请先完整阅读根目录 `HANDOFF.md` 和 `docs/34_World01_DoubaoAndCityPlan.md`，再检查 `git status --short`。不要重做已完成系统，也不要清理脏工作树。当前先把 `docs/ArtProduction/20260914_DuskSeam/` 中的黄昏城市完成为非镜像、真正可水平循环的 v02，并查看三张并排预览；随后把豆包气泡生成从均匀散组重构为约 7 列、每排只留少量开口且主路线必定连通的下落迷宫。完成一整个批次后再汇报，不要每改一点就停下来问我。
