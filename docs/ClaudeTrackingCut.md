# Claude追踪切割与锁定光圈

2026-10-08：已由正式World02遭遇轴调度，权威几何与两个锁定光标通过整场快照复制。

## 规则与入口

- 在目标位置斩出一枚短斜光刃，不从Claude发射，不贯穿屏幕；只有锁定光圈预警，没有长线。
- 前0.8游戏秒跟随目标，在跟随结束瞬间读取实际世界移动速度：二/四象限为左上—右下，一/三象限为右上—左下，只有±45度两种刀线。静止或轴向移动保留本次技能最近有效象限，尚无有效方向时默认右上—左下。
- 后0.4秒锁住斩击中心、方向和光圈，玩家移开能躲避；二阶段每刀独立追踪并重新取方向。
- 出刀瞬间判伤1点，每玩家每刀一次，共用护盾与无敌帧。刀图0.5秒淡出，无持续碰撞。
- 一阶段三轮，每轮随机选择一个合格目标；二阶段三轮，同时锁定两名合格玩家，各斩一道。每轮重新追踪并给完整预警；单名玩家倒地或移动被封禁时只攻击另一名安全目标。无遮挡、目标始终合格时，出刀为1.2/2.4/3.6游戏秒；二阶段共六道，两道重叠也不会同一轮对同一玩家重复扣血。
- 被封移动者不作目标，刀线也不能穿过另一位被封移动玩家。中途目标失效或新增封禁则重新选目标并完整预警；无安全目标则结束技能。
- 使用单道斜光刃，和全屏切割的亮线/裂痕为不同技能，不共用其裂痕时序。
- 默认隐藏，调用Begin(phaseTwo, seed)、Simulate(gameDt)、Cancel()。主机/离线权威启动，客机仅还原几何/光标/淡出而不裁决伤害。协议15、内容20261008-claude-dual-tracking-1；快照403～1363字节，包含两套光标和六个刀槽。

配置：Assets/_Project/Configs/Combat/Encounters/Claude/CFG_CL_TrackingCut.asset。

预制体：Assets/_Project/Prefabs/Combat/Encounters/Claude/PF_CL_TrackingCutPattern.prefab。

场景：Assets/Scenes/World02_2066.unity，同名根实例，显式绑定Boss、姿态、两玩家生命/受击/移动电机/门禁/Session，加入FixedSimulationLoop。不再绑定EnergyMuzzle。

最新可调表现参数：用户要求光刃等比放大1.5倍，Length2.8→4.2u、DamageWidth0.35→0.525u，斩击中心不变。MarkerDiameter2.4u、MarkerSpin35度/秒、FrozenClearance0.25u、MovementDirectionEpsilon0.01不变。直径/转速参考Kimi现有光圈，长度是局部斜斩初值，不是全屏切割数值。实际速度已包含颠倒效果；固定循环先执行玩家移动，再执行本技能。

光圈在独立UIWorld排序层8，不放玩家SortingGroup内；刀图Gameplay47。三个刀图固定复用，运行时不创建对象。斜光刃原图像素不改，按实测刀线轴等比旋转、缩放和定位；不把斜向贴图直接当水平线判定。

旧长线Warning节点、束线引用与对应孤立Prefab覆盖已移除；确认无引用后把本轮新增的长线预警材质与2×2技术白纹理移入回收站，不涉及已有生成素材，可从回收站恢复。

## 检查与边界

菜单DeepSleep/Diagnostics/Claude Tracking Cut：44项隔离Editor检查通过，包含四象限、跟随结束时取方向、锁定后不转、静止/轴向回退、二阶段逐刀取方向、真实扣血一次、锁向后躲避、暂停、封禁重选、安全路线、阶段次数、固定刻/大步长一致、取消回调、源图两端对齐与排序。

回归：Claude Spatial Cut 2621项、Claude Energy 57项通过，Console零Error。正式场景保存后保持clean。

最新1.5倍预览：docs/ArtProduction/20261008_Claude/previews/TrackingCut_LocalSlash_1p5_16x9.png。TrackingCut_LocalSlash_16x9.png是放大前版本。此前TrackingCut_Locked_16x9.png和TrackingCut_Fired_Long.png是误读需求的长射线版本，已作废，不再代表实现。

这些是隔离相机静态截图，不是完整自然Play、uGUI雨层、手机或联机验收。正式遭遇调度、移动穿屏、AI预判、HUD、音效与网络仍待接入。
