# 递归果冻（2026-10-07）

当前用于“2066”全部四波，与快应用共同生成；第一世界不变。下方首版配置用于说明基础属性，当前四波刷新、任务与生命倍率以 `World02_2066.md` 的完整表为准。

## 首版配置

- 第一波标题「递归」，45秒，任务「击败递归个体13」。大/中/小实际击败均计数，接触消耗/出屏/重置不伪造击杀。
- 关闭原四频道第一波日程，豆包仅在2/3/4波启用，第一波没有豆包或气泡。
- 只刷大体：1秒后开始，4–5秒间隔（原8–10秒减半），大体最多3。中/小日程全部关闭，仅由分裂生成；后代容量不足仍暂停新大体，实际生成量受上限约束。
- 大/中/小基础HP=20/15/10，完整一族总HP160，显示宽2.4/1.45/.85u。
- 三尺寸实际受击采用其他普通怪同款淡红短闪：RGB(1,.72,.65)，峰值alpha .65，时长.16秒；开启弱化闪光则沿用.18。原黄绿色.35配置已替换，覆盖层跟随蠕动/受击拉伸和前移，Q弹保留，Kimi不改。
- 追逐改为一步一顿的蠕动，不再左右摇晃。每步.9秒：前28%只前伸贴图，随后42%收回后部并移动物理根，最后30%停顿。每步距离大/中/小=.42/.30/.22u，一步内锁向。受击Q弹保留；Speed=.85仅用于出生分离混合，普通追逐改由步距/周期决定。
- 大体击败后四个对角方向生成4中体；中体击败后左右生成2小体；叶体直接死亡。完整一族13个。
- 后代出生位置夹在逻辑战区内，.24秒关闭碰撞和受击目标，.5秒减速分离后追逐。
- 接触攻击后保留本体，1秒冷却后可再次攻击；实际扣血时向外击退玩家1.8u/.22s，护盾/无敌帧不击退。碰撞不再作为离场或任务计数，不分裂；只有击败才分裂，RunReset/出屏也不分裂。详见docs/ContactMelee.md。
- 每个击败个体沿用1–5 Token随机掉落；完整一族平均约39 Token，不新增奖励系统。
- 池初始容量3/12/24，最大容量4/16/32（四波递进更新）；大体入口按族谱预留完整后代容量。空间不足暂停新大体，不吞已有后代。

## Unity调参路径

Prefab目录：`Assets/_Project/Prefabs/Combat/Enemies/RecursiveJelly/`

- `PF_Enemy_Recursive_Large.prefab`
- `PF_Enemy_Recursive_Medium.prefab`
- `PF_Enemy_Recursive_Small.prefab`

根BoxCollider2D是判定；RecursiveJelly2D.Renderer指向身体显示，代码只拉伸/前移该贴图，不缩放物理根或左右倾斜。新增三怪初始框为显示宽度×(.82,.5)，offsetY=-宽度×.075；原怪/玩家/节点碰撞未动。

配置目录：`Assets/_Project/Configs/Combat/Enemies/RecursiveJelly/`

- `CFG_Recursive_<Large/Medium/Small>_Health.asset`：基础HP。
- `CFG_Recursive_<Large/Medium/Small>_Motion.asset`：步距/周期/前伸与收回阶段占比、分裂方向/出生偏移、自身分离速度/时间、保护、移动伸缩、受击恢复、透明度。
- `CFG_Recursive_Large_Schedule.asset`：4–5秒刷新间隔。
- 章节第一段：`Assets/_Project/Configs/Progression/CFG_ChapterRun_World02_2066.asset`，任务/时长/频道开关/日程倍率/上限。

贴图：`Assets/_Project/Art/Enemies/RecursiveJelly/SPR_EN_RecursiveJelly_Idle_v01.png`，1254²、Sprite Single、PPU512、中心Pivot、Bilinear、无MipMap、Max2048。保留高分源图，遵循项目美术技能的显示/判定分离；本敌人的非等比Q弹由用户明确授权。身体alpha=.86，透光质感另有绘画层次，未做帧动画。

死亡/接触已使用确认的专属生图，位于`Assets/_Project/Art/VFX/Enemies/RecursiveJelly/`。死亡`VFX_EN_RecursiveJelly_Death_v01.png`中心Pivot，.36秒、尺寸=身体标尺×1.25、等比缩放.75→1.2、15%时开始淡出，alpha .85；四对角果冻碎块与破碎函数符号。接触`VFX_EN_RecursiveJelly_ContactHit_v01.png`自定义Pivot(.7,.49)对齐亮核，朝右为基准，按TravelDirection旋转，.24秒、尺寸=身体标尺×.7、缩放.7→1.1、5%时开始淡出，alpha .9；使用既有Enemy命中特效倍率，不影响判定。两张1254方图、PPU512、Bilinear、无MipMap、Max2048，非循环Gameplay特效，沿用既有池/事件复制，不添加实体快照Sprite。大中小共6套原特效Prefab和配置已替换，池容量与场景接线不变；接触/击败分别播放，正常出屏/重置不播，后代生成机制不改。

## 代码边界

- RecursiveJelly2D：既有EnemyMotor契约内的追逐/分裂/视觉变形；复用EnemyActor生命、接触、奖励及AI感知。
- RecursiveJellyConfig：分层级参数；RuntimeBinder显式注入后代池与Session，并核算大体准入容量。
- IEnemySpawnAdmission2D：EnemySpawnDirector新增一个可选引用。旧怪不配置，旧日程原样；检查在随机抽签之前。
- RecursiveJellyInstaller：一次性显式装配，发现已有资产拒绝重跑，避免覆盖人工调参。
- 三层各登记World02的Manifest/Bindings；由现有LevelSceneInstaller派生固定步、清场、感知、奖励、声音与网络池清单。
- 客机不独立分裂。已有世界镜像包含Sprite/位置/scale/rotation/alpha，直接承载Q弹；新增近战击退使用玩家快照中的速度/剩余时间，协议11，最新内容`20261007-contact-knockback-1`，旧包不能混房。

## 验证

- 专属VFX接入：编辑态隔离6个效果Prefab，正确图、定向/锚点、Enemy倍率引用、扩散/淡出、完成关闭和回池复位检查通过；未冒充自然实战或双端效果验收。
- RunAssets：41项通过；首波唯一日程/无豆包、后续三波原规则不变、装配/图目录/派生清单。
- 独立Play/临时存档RunPlay：42项通过；真实池和生命1→4→8、四对角出生、出生保护、叶体终止、受击变形/固定碰撞、回池复位、重置/接触不分裂、后代满容量准入、网络写入Sprite和实际scale；新增前伸、收回、停顿及无摇摆检查。
- 蠕动修订后的真实Physics2D模拟：前伸阶段根部不移动，收回阶段完整移动.42单位，停顿阶段不移动，大体HP10；临时存档验证后真实profile哈希不变。
- 真正Physics2D模拟验证追逐位移；累积实际测试击杀后推进45秒及清场，进入Node，无旧豆包任务阻塞。
- 关卡装配2082项零错警告，协议目录1526断言通过，最终Console零错警告。
- 首次验证发现物理出生坐标与Transform显示延迟，修正出生立即对齐后重跑通过。
- 第一世界场景/ChapterRun SHA256不变，真实profile哈希不变，测试后退出Play。没有打包/上传。
- 未做手机/真实双端、自然45秒难度或专属声音手感验收，网络写入检查不冒充实际联机。
