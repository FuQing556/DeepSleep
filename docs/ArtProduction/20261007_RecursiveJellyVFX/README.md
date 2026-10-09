# 递归果冻死亡与攻击命中特效候选

更新：用户已确认并授权落实，两图已复制到Assets/_Project/Art/VFX/Enemies/RecursiveJelly，Unity导入并替换大/中/小原死亡和接触特效Prefab。原始候选与提示词保留。接入参数见docs/RecursiveJelly.md；下方“未导入/等待确认”仅为最初候选交付记录，已失效。

2026-10-07，用户要求制作两张贴图。使用 deepsleep-art-generation 与 imagegen 技能，内置 image_gen，两次独立生成，transparent_background=true。不是帧动画，不改碰撞/伤害/刷怪，不替换玩家攻击命中特效。

参考：Assets/_Project/Art/Enemies/RecursiveJelly/SPR_EN_RecursiveJelly_Idle_v01.png，已确认且已接入的递归母图，仅用于身份、材质和画风参考。

交付：
- raw/VFX_EN_RecursiveJelly_Death_v01.png：四对角散开的厚实果冻碎块、液滴、分开的函数符号，区别于完整小体。
- raw/VFX_EN_RecursiveJelly_ContactHit_v01.png：朝右的压扁撞击飞溅，接触核心偏右，无函数符号。用于怪物攻击撞中玩家，不是 DS/HS 打中怪物。

两张均实际查看，轮廓完整，无背景/脸/地面。保留生成器原始分辨率与 Alpha，不做本地重画或抠图。本次仅候选制作；未导入正式 Art 或接入池化效果，仍需用户确认。命中贴图实际核心偏右明显，若后续接入需显式对齐命中锚点，不能默认中心 Pivot 就是碰撞点。

技术检查：两张1254×1254、32位RGBA，四角Alpha均0；死亡中心Alpha252，命中中心253，具备真实透明通道，不是烘焙棋盘。

## 实际提示词：死亡

Use case: stylized-concept. Asset type: single 2D game enemy death VFX sprite, square genuine transparent canvas. Input image is the approved recursion jelly identity and rendering reference, not an edit target. Match its yellow-green thick translucent gelatin, dark olive contour, cream cel-shaded highlights and internal bubbles. Draw one readable central burst of a defeated jelly breaking apart: four large soft irregular jelly chunks spreading diagonally, smaller round droplets, a broken dark green f and separated parentheses suspended among the fragments. A hollow opening where the body has collapsed, not an intact living jelly and not four new monsters. Heavy elastic rounded fragments, polished cute anime game illustration, compact clear silhouette at small game size, restrained glow. The single frozen effect will expand and fade briefly in Unity, no ground, no shadow, no scenery, no face, no blood, no fire, no UI, no sprite sheet. All fragments fully within the frame, generous transparent padding.

## 实际提示词：攻击命中

Use case: stylized-concept. Asset type: single 2D game enemy contact-attack hit VFX sprite, square genuine transparent canvas. Input image is the approved recursion jelly material and style reference, not an edit target. Create a compact directional gelatinous impact splash where this yellow-green heavy jelly collides with a player: a small bright creamy contact core slightly right of center, a broad compressed soft crescent of translucent yellow-green jelly behind it on the left, five short rounded lobes and scattered small viscous droplets radiating out. Dark olive edges and controlled cel-shaded cream highlights matching the reference. Strong clear elastic squishy collision, NOT a whole monster, no f() text, no character, no weapon, no blood, no fire, no giant starburst, no opaque backdrop, no floor or shadow. One isolated effect, no sprite sheet. Crisp painted game-art structure with restrained soft glow and transparent gaps, enough empty margin for rotation; intended to rotate to contact direction and quickly fade in Unity.
