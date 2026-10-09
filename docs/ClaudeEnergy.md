# Claude 巨大能量弹接入 · 2026-10-08

最新用户修正：内层可见半径减半（直径6.4→3.2u）、碰撞半径2.8→1.4u；外层可见半径缩为2/3（直径7.424→4.949333u），纯视觉且无碰撞。爆炸半径独立固定3.712u，等于缩小前外圈可见半径，替代下文旧20%面积公式。核心与外环共同按alpha>8可见bbox中心校准导入Pivot=(625.5,639)/1254，消除画布中心偏差在反向旋转时造成的偏心，不修改PNG像素。57项检查通过（追加Pivot/比例/单一内层碰撞及仅碰外圈无伤测试）；最新静态预览为EnergyFlight_Adjusted_16x9.png与EnergyExplosion_Adjusted_16x9.png。下文原尺寸、原面积及53项为历史。

用户已确认首版：一/二阶段100/200HP；2游戏秒蓄力，锁向后直线飞行，不追踪；速度=固定战斗区宽度/4游戏秒（当前4.8u/s，靠近边界发射会更早触边）。被击碎、触人、触边均在当前球心爆炸，爆炸2点伤害，包括玩家主动打碎；仅爆炸瞬间结算，滞留2.5游戏秒后0.6秒淡出，不持续伤害。二阶段仍单颗，仅增加球HP。

## 装配与修改入口

- `Assets/_Project/Configs/Combat/Encounters/Claude/CFG_CL_Energy.asset`：数值、速度时间、视觉直径、碰撞半径、自转速度、玩家碰撞层。视觉直径初值6.4u（约四个角色横向宽度的表现初值），判定半径2.8u，可独立调整。软外环不作为额外碰撞。
- `Assets/_Project/Prefabs/Combat/Encounters/Claude/PF_CL_EnergyPattern.prefab`：单个固定复用弹体，Kinematic Rigidbody、Trigger圆、共享DamageHitbox与Perception、五个独立Sprite层。Enemy层8，特效使用已有Gameplay排序层及URP Sprite-Unlit材质，不新增层/材质。
- `Assets/Scenes/World02_2066.unity / ClaudeEnergyPattern`：场景覆盖显式绑定Boss、DS/HS实际受击入口和碰撞体/生命、Session、感知和AI障碍登记表、姿态残影组件。该预制体是固定技能模块，不是运行时临时实例化的独立敌人；场景引用由实例装配。
- `ClaudeVisualRoot / EnergyMuzzle`：本地位置(-1.2,0.3,0)，可微调；球心须位于战区内缩球半径后的矩形内，不合法则拒绝施法。人物动作不带球图；蓄力/释放切换复用原姿态残影。
- 已追加现有FixedSimulationLoop世界步骤。默认Idle且隐藏，不自行触发；未来Claude遭遇轴调用`Begin(phaseTwo,target)`，取消/失败/退出调用`Cancel()`，完成事件由调度消费。未接正式Boss章节、技能池或联机状态复制，不能称第四波Boss可玩。

## 机制边界

蓄力期间球不可攻击，不造成伤害；飞行期间注册共享攻击目标及AI接触障碍。扫掠圆沿实际轨迹取最近玩家碰撞/边界交点，避免高速穿人。接触只引爆，没有额外接触伤害。破球立刻退出索敌/AI障碍、关闭碰撞；爆炸每个接收者至多一次，使用现有Blockable伤害入口，遵守DS护盾拦截、无敌与倒地规则。不产出Token/鲸元券。

爆炸半径=`sqrt(逻辑战区宽*高*0.2/pi)`，当前约3.633u。不使用手机扩展背景/镜头尺寸，边界爆炸不补面积；战区外的角色不计入爆炸。飞行内缩边界由碰撞半径决定，软光可以延伸过边界。

五层：Core正转55°/s，Halo反转-32°/s且Alpha0.42；爆炸时原层渐隐，叠BrokenCore与RadialBurst，Dissipation留存后淡出。图层等比缩放，按本批alpha>8可见bbox/PPU512校准，未裁源图。以后换Sprite画布或PPU须重校可见尺寸，不能只换图后假定尺寸一致。

## 素材与透明检查

使用 **deepsleep-sprite-matting** 技能：原生透明PNG只读检查，实际查看全部七张白/暗/天空底，不去白、去黑、去雾或硬化发光，不生成新图。源1254×1254；报告完整hash、Alpha统计、可见bbox在`ArtProduction/20261008_Claude/previews/energy_audit/*_alpha_report.json`。正式七图与对应原稿SHA256全部相同，原像素未改，Unity PPU512、最大2048、无压缩。极淡光晕靠近部分画布边缘，未宣称所有Alpha>0均有大留白；主要可见能量形状完整，保留原画布。

正式图均在`Assets/_Project/Art/Characters/Claude/`：

| 正式文件 | 原稿 |
| --- | --- |
| VFX_CL_EnergyCore.png | VFX_CL_EnergyCore_v01_CANDIDATE.png |
| VFX_CL_EnergyOuterHalo.png | VFX_CL_EnergyOuterHalo_v02_CANDIDATE.png |
| VFX_CL_EnergyCoreBroken.png | VFX_CL_EnergyCoreBroken_v01_CANDIDATE.png |
| VFX_CL_EnergyRadialBurst.png | VFX_CL_EnergyRadialBurst_v01_CANDIDATE.png |
| VFX_CL_EnergyDissipation.png | VFX_CL_EnergyDissipation_v02_CANDIDATE.png |
| SPR_CL_EnergyCharge.png | SPR_CL_EnergyCharge_v01_CANDIDATE.png |
| SPR_CL_EnergyRelease.png | SPR_CL_EnergyRelease_v02_CANDIDATE.png |

## 验证及限制

菜单`DeepSleep/Diagnostics/Claude Energy`：53项隔离Editor检查，含真实DamageHitbox、共享玩家伤害门与Blockable拦截测试、无敌、两人只各伤一次、真实Physics2D扫掠接触、大步长/小步长边界一致、锁向不追踪、生命周期清理/12次复用、图层与面积数值。PreviewScene不参加全局Physics2D查询，因此只将其两玩家副本迁移到临时正常物理场景，finally关闭并恢复原活动场景；不改正式玩家/存档。拦截测试验证共同入口，不等于实际DS护盾实战。另回归权限1790项、Boss基础17项、出退场19项，Console无Error。

`previews/EnergyCharge_16x9.png`、`EnergyFlight_16x9.png`、`EnergyExplosion_16x9.png`、`EnergyLinger_16x9.png`已实际查看，是隔离Editor相机手动推进构图，不含完整ScreenSpace雨/HUD，不是自然Play、手机或联机截图。测试Boss1000HP/.5仅fixture，未写入Claude正式数值。仍待遭遇调度、网络复制、专属音效以及真人/AI实战；未打包上传。
