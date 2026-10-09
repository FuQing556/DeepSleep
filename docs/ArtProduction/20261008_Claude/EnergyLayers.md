# 切割腿部修正与分层能量弹

使用 deepsleep-art-generation 和 imagegen，内置生图真实透明请求。旧切割v01腿姿被用户否定，不作为最终姿态。v02简化限制，只改腿靴，保留上半身；目视腿部不再交叉，仍需用户确认。VFX参考已生成的 EnergyCore_v01，作为配色与几何风格参考，碎裂图以其为编辑目标。

## 用户确认的机制

现有球图就是中心核心。飞行时下层铺颜色更淡、尺寸更大的光圈，与核心逆向旋转，保持独立图层。能量弹触碰边界或玩家后，在爆炸点留下更大的爆炸范围，并展示核心碎裂。范围半径、持续时间、伤害/频率、触边是否采用球缘或中心、逆转速度均未定，不能写成已实现。接入时须单次触发爆炸，不能同帧边界和玩家各生成一次。

## 产物与限制

外光圈v01退役：过亮、外缘贴边。v02颜色减淡且留边，仍须叠加检查透明度，避免抢过核心。爆炸范围v01柔光靠画布边缘，暂为候选，正式生产前需要留边处理/透明验收；没有宣称可直接导入。核心碎裂有分离球壳块和碎片，透明间隙可见。全部未做全图Alpha/游戏量级组合验收，未导入Unity，没有旋转、爆炸或伤害Runtime改动。

生产卡：切割v01编辑→腿部自然悬浮→人物独立透明→动作修正；核心风格参考→更大淡色空心光圈→正视独立透明→下层反转；核心风格参考→范围残留→正视独立透明→命中范围表现；核心编辑→壳块裂开→独立透明→爆炸中心碎裂。

## SPR_CL_ScreenCutCast_v02_CANDIDATE.png

实际提示词：

```text
Edit the reference Claude sprite. Keep her face, hair, outfit, upper body and forward two-finger casting gesture. Redraw only the legs and boots into a comfortable airborne pose: both legs hang gently below the skirt with a little space between them, following the body's orientation, no crossing. Keep the same chibi scale and drawing style. Complete full body with transparent margin, genuine transparent background.
```

## VFX_CL_EnergyOuterHalo_v01_REJECTED.png

实际提示词：

```text
Single transparent 2D VFX layer: a large pale amber geometric energy halo placed BEHIND the reference energy core. Reference is palette/style only; do not reproduce its central sphere. Front-facing circular annulus, wide empty transparent center, delicate pale cream-orange arcs and angular scientific facets around the circumference, restrained semi-transparent glow. A little asymmetry in the arcs so rotation is visibly readable. Light and airy, substantially softer than the dense white-hot reference. Complete ring and halo within canvas margins, true transparent background, no core, text, environment or UI.
```

## VFX_CL_EnergyOuterHalo_v02_CANDIDATE.png

实际提示词：

```text
Single pale geometric energy ring VFX, front view on genuine transparent background. Match the reference core's warm ivory/amber scientific style, but use thin delicate ivory-peach arcs and sparse angular facets, not dense glowing material. Wide transparent central hole. Faint translucent soft halo, low-intensity pastel color. Entire ring occupies only the middle 70 percent of the square canvas, leaving a broad empty transparent margin on ALL sides so no light is clipped. No central orb, no fire, no text. Standalone underlay for counter-rotation beneath the reference core.
```

## VFX_CL_EnergyBlastField_v01_CANDIDATE.png

实际提示词：

```text
A standalone circular explosion-residue field VFX for an anime 2D game, matching reference amber-white geometric energy. Front view: broad translucent pale amber energy disk, clearly readable circular outer boundary made of broken luminous arcs, sparse angular radial fracture lines inside and soft dissipating light. No central intact sphere, leave center relatively quiet for separately overlaid broken core. This field will be larger than the flying projectile and linger at impact. Entire effect contained with empty transparent margin. Genuine transparent background, no ground crater, environment, text, UI or flames.
```

## VFX_CL_EnergyCoreBroken_v01_CANDIDATE.png

实际提示词：

```text
Edit the reference energy core into its BROKEN state for a 2D game VFX sprite. Keep the amber-orange and white geometric crystalline energy style. The sphere has shattered into several clearly separated curved shell chunks and smaller angular shards spreading a short distance outward around the same center, with visible transparent gaps. Break the continuous circular shell and uninterrupted internal circles; no intact sphere remains. White luminous edges on broken facets, fading inner light, readable large fragments rather than a dense glitter cloud. Entire debris cluster fits comfortably in the middle of a square canvas with generous transparent border. True transparent background, no scene, text, outer explosion field or fire.
```
