# 快应用死亡 / 接触命中特效 v01

使用 deepsleep-art-generation 和 imagegen 技能，内置生图模式。身份与画风母图为本目录 raw/SPR_EN_QuickApp_Idle_v01.png（用户已确认）；生成前实际查看。未更改母图。

- raw/VFX_EN_QuickApp_Death_v01.png：广告卡片、转盘、按钮与关闭叉破碎，中心金白亮核，周围青色像素碎片。独立死亡爆散图。
- raw/VFX_EN_QuickApp_ContactHit_v01.png：从左向右的碰撞冲击，红粉短条、金色弧与金白亮核，少量金币和青色碎片。不带完整怪物。亮核实际在画布偏右约78%位置；正式接入时应按亮核设Pivot，而非用画布中心误对齐。

两图均1254×1254、32位RGBA，保留原始透明输出；死亡图四角Alpha为0/0/1/0（左下角有1/255的极弱残留），接触图四角均0。未抠图、未缩放。仅素材候选，未导入正式Art、未装配对象池或改碰撞/战斗参数。死亡图保留部分广告文字碎片，接触图不含文字。生成检查不等于Unity播放验收。

## 死亡实际提示词

```text
Use case: stylized-concept. Asset: standalone death VFX sprite for DeepSleep enemy 快应用. Attached image is the confirmed ENEMY IDENTITY AND ART STYLE reference, not the output composition. Draw the instant this advertising amalgam is destroyed: its red/gold central popup frame breaks into several recognizable chunky pieces, the pink video card, colorful prize wheel and yellow coupon tear into fragments flying outward radially around a small warm white-gold flash. Recognizable partial button lettering and a detached close-X fragment, not a complete intact enemy. Open broken silhouette with transparent gaps through the center; restrained cyan digital pixel shards, gold coin fragments and coral/pink strips. Clean dark outlines, polished cel-shaded cartoon game rendering exactly like reference; readable at small scale. A short crisp shattering burst, not a fireball or smoky realistic explosion. One single effect on a square genuinely transparent canvas, full debris inside margins. No character, human face, background, ground, shadow, arrows, movement trail, sheet of frames, watermark. Do not include a huge opaque glow disc covering the whole effect.
```

## 接触命中实际提示词

```text
Use case: stylized-concept. Asset: a standalone directional CONTACT HIT VFX sprite for DeepSleep enemy 快应用 striking a player. Attached image is the enemy identity/palette/cel-shaded ART STYLE reference only; do NOT draw the full enemy. Make one compact crisp impact burst directed from LEFT TO RIGHT: a small brilliant warm white-gold impact core near 65 percent canvas width, flattened golden crescent shock arc on its right, three short red/coral and pink tapered impact streaks trailing to the left, a handful of tiny broken popup-frame chips, one small gold coin chip and a few cyan square digital sparks. A punchy advertising-themed collision flash with a clear direction, much simpler and more airy than the enemy death breakup. Preserve polished anime-game cel shading, clean navy outlines on solid fragments, restrained soft alpha glow around the core. No intact advertisement panels or readable text, no people/faces, no arrows, no giant fireball or smoke cloud, no enormous full circle halo, no body or other character being hit. Single effect fully contained with generous padding on a square genuinely transparent background. Not a sprite sheet, no scene, floor, ground shadow, motion afterimages or watermark. This is an independent brief hit overlay to be rotated in code along the actual collision direction.
```
