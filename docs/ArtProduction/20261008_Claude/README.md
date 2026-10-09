# Claude 常态母图候选

雨夜背景与雨雾v01见 `RainNight.md`。用户明确保留灰雾；纯雨v02退役。上下双副本检查发现接缝不严密，未来通过重叠淡化衔接，未接入，不称原生无缝。

能量释放v02与单条切割预警已生成候选，见 `ReleaseAndWarning.md`，包含首版素材缺口及未批准的节奏建议。释放v01头发贴边退役；尚未导入/实现。

最新巨型能量弹设计与放射核心/逸散素材见 `EnergyExplosion.md`：飞行可打碎，约四角色大小，爆炸面积安全区五分之一，原地留存2～3秒后渐隐；未接入。逸散v01贴边退役，v02候选。

最新切割v02修腿与能量弹分层制作见 `EnergyLayers.md`。切割v01腿姿被用户否定；外光圈v01退役。新增素材仍为候选，非实战接入。

后续核心攻击素材：见 `CoreAttacks.md`，新增切割手诀、双手蓄力、独立空间切痕、能量核心四张候选。尚未导入或接入技能。

### 捏诀施法 v01实际提示词

```text
Use case identity-preserve: create a new full-body transparent 2D sprite pose of EXACTLY the Claude character in the reference. Preserve her face, amber eyes, orange long hair, flower and black ribbon, same ivory/black/orange layered scholar dress, boots, clean dark outlines, cel shading and petite chibi head/body proportions. New action is focused two-handed hand-seal spellcasting: one hand at chest height with index and middle fingers held together upright, other hand naturally supports the base of that hand. Compact readable gesture, anatomically connected shoulder-elbow-wrist chains, no extra hands or tangled fingers. Both elbows comfortably bent in front of chest, hands do not hide her face. Coherent three-quarter facing screen LEFT: head, chest, pelvis, skirt front and boots have the same orientation, no counter-twist. Both legs hang naturally with slight separation, no crossed legs. Calm concentrated expression; hair floats gently to the right without huge swirls. Complete head-to-toe silhouette centered with generous transparent margins. Character ONLY, true transparent background. No book, drone, platform, spell circles, aura, beams, shadow, scene, UI or text.
```

### 权限书 v01实际提示词

```text
Create a single isolated opened floating spellbook prop for Claude in a 2D anime chibi game. Image 1 defines rendering style and ivory/black/orange palette only. Image 2 defines the original Claude book's dark brown binding, gold-orange edging and flower/starburst emblem only; do not include any person or screenshot content. One opened hardbound book hovering in three-quarter view, two ivory pages facing the viewer clearly, thick charcoal-brown cover visible around the edges, elegant orange-gold corner hardware, small orange ribbon bookmark. Faint precise geometric line diagrams around the page margins, uncluttered central page areas for future permission icons added in game. Clean dark outline and polished cel shading, readable silhouette at small game scale. Restrained luminous orange binding details only, no large aura. Complete book centered with transparent margin. Real transparent background, no person, platform, scene, UI, readable text, multiple books, ground or cast shadow.
```

### 悬浮平台 v01实际提示词

```text
Create a single isolated 2D game prop: Claude's compact hovering drone foot-platform. Reference image defines ONLY the clean anime chibi rendering style and ivory/black/orange palette; do not draw the character. A modest oval disk in shallow three-quarter view, top deck visible for a character to stand on, ivory ceramic upper shell, charcoal mechanical underside, precise narrow orange luminous inset ring, a few small recessed hover vents. Elegant scientific geometric design, readable at small sprite scale, clean dark outlines and polished cel shading matching reference. Not a quadcopter, no exposed propellers, no weapons, no huge light bloom. Whole object centered with ample margins. True transparent background. No character, book, scene, floor, cast shadow, UI, letters or separate decorative objects.
```

## 当前状态（以本段为准）

用户确认常态v02，后续动作以它保持身份，不再沿用v01扭转姿态。新增三张候选：

- `raw/SPR_CL_HoverDrone_v01_CANDIDATE.png`：独立象牙白/黑/橙悬浮平台，侧下方推进口，非四旋翼。
- `raw/SPR_CL_PermissionBookOpen_v01_CANDIDATE.png`：展开权限书，页面中央预留游戏内权限标识。
- `raw/SPR_CL_SealCast_v01_CANDIDATE.png`：完整全身捏诀施法，人物不含平台、书本或光效。

上述使用内置image_gen、真实透明请求，参考已批准常态v02；书本另参考 `docs/AiSister/Claude.jpg` 的书籍身份。三张已展示，仍为候选。目视平台/书完整，施法躯干朝向一致、手臂相连；平台和书有柔光边缘，导入前须检查实际Alpha及分层观感。没有完成全图透明/色边验收，不因角点透明宣称可生产。未导入Unity、未指定游戏尺寸或碰撞、未修改代码。

## v02 姿态重画

用户认可v01身份大致方向，但明确否定头脚向左/胸向右的扭转姿态。v01保留作退役候选，不作为姿态母图。v02为 `raw/SPR_CL_IdleHover_v02_CANDIDATE.png`，内置image_gen真实透明请求；参考v01仅保持身份服装、HS仅保持画风，重新画完整躯干与腿部，而非局部修补。头、胸、腰与裙前朝向统一，双腿自然下垂，去除交叉拧转，仍待用户确认。未导入Unity，未改碰撞或代码。

v02实际提示词：

```text
Use case: identity-preserve. Redraw this Claude game character's COMPLETE BODY POSE, not a local patch. Image 1 is identity/clothing/color/chibi-face reference ONLY; its twisted pose is rejected and must not be copied. Image 2 is game drawing-style reference only, do not borrow its character features or props. Keep Claude's orange long hair, amber eyes, calm face, orange flower and black ribbon, same ivory/black/orange scholar dress, boots and petite chibi proportions. New pose: simple upright gentle hover in coherent three-quarter view facing SCREEN LEFT. Her head, shoulders, chest, waist, pelvis, knees and toes all share that SAME left-front orientation, like the entire character turned together; neck and spine relaxed, no counter-rotation. Both legs hang naturally downward with a small comfortable separation, knees softly relaxed, no crossing, no corkscrew, no backward bend. Arms rest naturally down and slightly away from her body, elbows and wrists relaxed, hands not spread dramatically. The blouse center, waist center and skirt front continue along one coherent body orientation. Hair and skirt have only gentle floating motion, no huge swirl hiding anatomy. One full-body character centered with transparent margin, complete silhouette. True transparent background. No book, drone, platform, scene, floor, effects, shadow, text or UI. Preserve the approved character identity and outfit, but replace the entire old pose.
```

2026-10-08，内置 image_gen，透明请求。使用 deepsleep-art-generation 与 imagegen 技能。

产物：`raw/SPR_CL_IdleHover_v01_CANDIDATE.png`。是候选，不是已批准的正式素材，未导入Unity。无人机、书本、VFX均未烘进人物，后续单独制作。

## 生产前核对

Claude/常态悬浮→Claude.jpg身份，DS/HS现有Sprite画风与头身→自然悬浮准备施法→三分之四朝左、完整全身→人物与平台/书/VFX拆层→方形真实透明背景→待用户确认的Boss动作母图。

身份原图：`docs/AiSister/Claude.jpg`。

画风参考：`Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png`、`Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png`。不使用旧Opus巨大半身候选指导动作。

技术检查：1254×1254，Format32bppArgb，两个对角像素alpha为0；未进行全图Alpha/色边验收，尚不能据此宣称可直接生产。目视全身完整、双手自然张开、脚部与裙摆未裁切。没有无人机/书本/残影/特效。PPU、Pivot、游戏大小和碰撞体均未改；导入前需要按身体标尺而非整张长发包围盒比较。

## 实际提示词

```text
Use case: stylized-concept. Asset type: single transparent full-body 2D game character sprite, Claude idle candidate. Input image 1 defines Claude's identity and clothing ONLY: orange long hair, orange flower with black ribbon hair ornament, amber eyes, ivory/black/orange scholar dress. Input images 2 and 3 define this game's chibi proportions, cute faces, clean dark outlines and polished cel shading ONLY; do not borrow their whale ears, tails, maid headbands or props. Draw Claude as a petite chibi matching the body-to-head scale of images 2 and 3, calmly confident, full body in a gentle airborne hover, three-quarter facing left toward opponents, hair and layered dress gently floating. One foot extended slightly downward for resting on a future separately-rendered hovering platform, other leg relaxed. Hands clearly visible in a natural relaxed ready-to-cast posture near waist, not holding anything; books, drone and magical effects will be separate sprites. Preserve the elegant ivory/black/orange dress identity but simplify fine pattern density for a small game sprite, ankle boots visible, no giant half-body and no teenager proportions. One character only, complete unclipped silhouette centered on square canvas with generous transparent margin; real transparent background, no scene, rain, ground, shadows, platform, book, spell circles, text or UI.
```
