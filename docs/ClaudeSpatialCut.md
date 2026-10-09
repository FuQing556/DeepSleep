# Claude全屏切割（2026-10-08）

本页记录用户最新确认及已落地的独立模块，不代表Claude完整遭遇已经接通。

## 最新规则

- 一阶段一次全屏切割，二阶段三次。每刀20条线，角度和相对战区中心的法线偏移随机，不全部穿过中心。
- 首刀1.5游戏秒预警；二阶段切割瞬间在1.5/3.5/5.5游戏秒。后两刀的2秒间隔同时用于新布局预警。
- 橙白线出现的瞬间判伤一次，每玩家每刀最多1点，交叉处不叠加。共用玩家无敌帧/护盾拦截。
- 橙白线0.5秒淡出；切下0.1秒后出现橙色裂痕，初始透明度倍率从80%提高到100%，从出现时起1秒淡出，不持续判伤。源图仍有柔光Alpha，不硬化PNG。
- 固定刀线长67.2u，是当前背景可见宽33.6u的两倍。背景长度变更后可改配置，不每帧按镜头裁切。
- 被封移动玩家周围留可见安全口；预警中新增移动封禁则重新排当前刀线，并重给完整1.5秒预警。这个规则不靠隐藏免伤解决组合冲突，未动权限封禁规则。

## 文件与调参

配置：`Assets/_Project/Configs/Combat/Encounters/Claude/CFG_CL_SpatialCut.asset`。

预制体：`Assets/_Project/Prefabs/Combat/Encounters/Claude/PF_CL_SpatialCutPattern.prefab`。

场景：`Assets/Scenes/World02_2066.unity` 根实例 `PF_CL_SpatialCutPattern`，已显式绑定Boss、姿态残影、玩法边界、两玩家受击体/生命/门禁、Session，并加入FixedSimulationLoop。默认隐藏，无自动施法；调用 `Begin(phaseTwo, seed)`、固定刻 `Simulate(dt)`、退出/转阶段 `Cancel()`。

数值：LineCount20、LineLength67.2、OffsetExtentFraction0.85、DamageWidth0.16、WarningWidth0.035、FlashWidth0.65、FractureWidth1.4、FrozenClearance0.25。Width是几何带的宽度，位图有透明留白，不能拿它直接当可见裂痕宽/伤害宽。

运行时职责：

- `ClaudeSpatialCutPattern2D`：一次技能的三个固定快照、时序与瞬间伤害，主机/离线权威才可启动。复用BeamHitResolver2D，整刀额外按最终receiver去重。姿态切换复用SpritePoseTransition2D。
- `ClaudeCutBatchView2D`：20条线合成一个动态网格；材质按线索引交替。固定7个视图：1预警、3亮线、3裂痕，不运行时创建GameObject。裂痕最多各2个子材质，旧刀独立淡出。纹理按照源图宽高比重复UV，未将整张图非等比拉长。几何与伤害消费同一份BeamLaneSnapshot。
- `ClaudeSpatialCutConfig`：可调数据。材质复用已存在的Harness Laser Tiled shader，不复制伤害或创建新通用框架。

## 素材语义（不要再混淆）

`VFX_CL_SpatialCut_v01_CANDIDATE.png`是斜向的**追踪切割**单道光线，未用于本模块。

`VFX_CL_SpatialCutWarning_v01_CANDIDATE.png`虽旧名含Warning，用户明确它是**全屏切割裂痕**。保留作风格参考；由于两头收尖，未直接作为循环中段。本轮新增两种无端帽中段和独立亮线，正式原样导入 `Assets/_Project/Art/Characters/Claude/`：

- `VFX_CL_ScreenFracture_B_v01.png`：较密分叉。
- `VFX_CL_ScreenFracture_C_v01.png`：长枝、碎片。
- `TEX_CL_ScreenCutFlash_v01.png`：纯橙白亮线，无裂痕。
- `SPR_CL_ScreenCutCast_v02.png`：此前已修正腿的切割姿态，本轮原样导入。

三张VFX为2172×724真实RGBA，Default Texture、Repeat U/Clamp V、Bilinear、无mipmap、不压缩、Max4096。网格采样不使用PPU或Sprite Pivot。角色PPU512、中心Pivot、Max2048。表现Gameplay层45/46/47，不配置Collider，不改变玩家/Boss碰撞。

原始PNG未改。生成模式、提示词、hash与Alpha预览入口见本批 `ScreenCut.md`。不宣称首尾像素严格无缝；重复中段已在实际相机预览中检查。

## 检查与未完成范围

菜单 `DeepSleep/Diagnostics/Claude Spatial Cut`：最新调参后2621项隔离Editor检查通过，包含128种子几何、确定性、三刀/大步长时序、100%初始透明度倍率、0.1/0.5/1秒表现、交叉只扣1HP、护盾/无敌帧、封移动安全口、事件内取消、退出清空和复用。首版能量弹57项、权限1790项、表现19项回归通过；此次仅重跑切割检查，Console无Error。首版检查后Editor显示场景dirty，但保存副本与正式场景全文一致；确认无序列化变化后保存清除dirty，临时副本已删除。

`previews/ScreenCut_Fracture_Clean_16x9.png` 与 `ScreenCut_Fracture_Clean_Long.png` 是首版调参前隔离场景指定时间的相机静态截图，分别1920×1080、2400×1080；不是最新Alpha/时序预览、自然Play、手机、完整uGUI雨层或双端验收。较早两张非Clean预览误显示旧Kimi，仅保留为过程记录，不作为交付入口。

仍缺Claude正式章节驱动、移动/穿屏与AI避刀威胁、技能池/追踪切割、正式生命/阶段配置、HUD、音效与网络表现复制。本模块客机不会自行启动或判伤；不能称已完成联机同步或可完整挑战Claude。未打包/上传。
