# Claude 核心攻击素材候选

2026-10-08。使用 deepsleep-art-generation 与 imagegen 技能、内置 image_gen、真实透明背景请求。四张分别生成，不将VFX烘进人物。统一参考已确认 `raw/SPR_CL_IdleHover_v02_CANDIDATE.png`，人物图为身份/服装/画风参考，VFX仅采用配色和视觉语言。

生产卡：切割手诀→常态v02→双指前指切割→左前方全身悬浮→人物独立→透明→切割动作候选；蓄力→同母图→双手聚拢留空→左前方悬浮→能量球独立→透明→蓄力候选；切痕→橙白视觉→单条瞬时空间切割→长幅→独立于整屏布局→透明→可复用切痕；核心→橙白视觉→圆形压缩能量→正视→不含外部轨道环→透明→能量弹核心。

目视检查：两动作头身服装延续母图，肩肘腕连接可见，脸未被手遮住，完整全身；蓄力双手之间留空。切痕实际略向上倾斜，并非提示词要求的严格水平，导入时需要校准轴向/明确素材局部轴，不能直接用贴图长边推算碰撞。核心圆形，内部几何较密，仍需游戏量级检查。未做母图与候选游戏量级并排验收、全图Alpha/色边检查；均仍候选，不能宣称生产完成。

尚未生成：独立约束环、释放姿态、切割预警/布局、能量命中、雨夜背景及其余动作/UI。未导入Assets、未修改Runtime/场景/碰撞/数值，未打包上传。

## 切割手诀

文件：raw/SPR_CL_ScreenCutCast_v01_CANDIDATE.png

实际提示词：

```text
Use case: identity-preserve. Single transparent full-body sprite of the SAME Claude in reference: same face, chibi proportions, orange hair and scholar outfit, linework and cel shading. New action: a decisive cutting spell gesture, one arm extended toward SCREEN LEFT at chest height with index and middle fingers together pointing left, other arm bent comfortably near waist. Body leans slightly into gesture, head/chest/pelvis oriented coherently left-front; legs relaxed in airborne hover. Focused composed expression. Natural connected shoulders/elbows/wrists, complete head-to-boots silhouette with margins. Do not copy the idle arm pose. Character only, genuine transparent background, no blade, aura, book, drone, scene, shadow or text.
```

## 能量蓄力

文件：raw/SPR_CL_EnergyCharge_v01_CANDIDATE.png

实际提示词：

```text
Use case: identity-preserve. Same Claude as reference, same face, orange hair, chibi proportions, ivory/black/orange scholar outfit and cel-shaded linework. New full-body airborne energy-charge pose: both arms naturally bent forward, hands cupped toward each other with a CLEAR EMPTY GAP between palms at upper-waist height, as if compressing a future separately drawn energy sphere. Fingers relaxed curved, palms inward, two wrists visibly connected to sleeves, no tangled interlocking hands. Body slightly leaning forward, coherent left-front three-quarter orientation, legs softly bent and separated naturally, focused expression looking toward the space between her palms. Complete silhouette with generous transparent margin. No actual sphere, light, aura, book, platform, drone, ground, shadow or text. Real transparent background. This is a distinct charge action, not idle or upright finger-seal.
```

## 空间切痕

文件：raw/VFX_CL_SpatialCut_v01_CANDIDATE.png

实际提示词：

```text
Use case: stylized-concept. A SINGLE isolated straight energy-cut VFX sprite for Claude's instantaneous irregular screen-cut attack. Reference defines ivory-white/orange palette and polished anime-game visual language only, no character. Long horizontal narrow surgical incision of white-hot light, sharp tapered tips at both ends, orange glowing borders, subtle displaced double-edge and a few tiny geometric triangular fragments near the center. Precision scientific spatial cut, not curved sword swoosh, not projectile sword, not fire or lightning. Clear thin central incision dominates, faint restrained soft halo fading to genuine alpha. Wide canvas, whole effect with transparent margin. One cut only, no sheet/grid of cuts, no screen frame, environment, person, weapon or text. Transparent background.
```

## 能量核心

文件：raw/VFX_CL_EnergyCore_v01_CANDIDATE.png

实际提示词：

```text
Use case: stylized-concept. Single isolated spherical energy CORE for Claude's giant energy projectile in a polished 2D anime game. Reference supplies white/amber-orange palette only; no character. Dense round white-hot luminous center surrounded by amber-orange translucent concentric spherical layers and fine geometric facets, scientific compressed energy, circular readable silhouette, balanced radial internal currents, restrained soft orange halo fading into real transparency. Large dangerous energy mass, not a fireball, no flames, no directional comet tail, no external orbital rings (rings will be separate asset), no scenery, hands, person, platform, lettering or UI. Centered on square transparent canvas with generous empty transparent border; whole halo contained.
```
