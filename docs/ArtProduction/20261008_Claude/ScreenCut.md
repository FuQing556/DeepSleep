# 全屏切割补充素材

2026-10-08，使用 `deepsleep-art-generation` + `imagegen` 内置生图；使用 `deepsleep-sprite-matting` 的只读inspect_sprite.py做Alpha与白/暗/天空底检查，无二次抠图、无像素编辑。参考为用户确认的 `raw/VFX_CL_SpatialCutWarning_v01_CANDIDATE.png`，不是追踪切割斜光刃。

生产卡：Claude全屏空间切割 → 用户确认直线裂痕 → 橙白切下后0.1秒裂开 → 水平中段由代码旋转铺开 → 亮线/裂痕/人物分离 → 3:1透明画布 → 覆盖全屏固定长刀线。

## 原稿与检查

| 原稿raw/ | SHA256 | Alpha | 尺寸 |
| --- | --- | --- | --- |
| VFX_CL_ScreenFracture_B_v01.png | 8d2f842d7a3caa0ed1e7a2058cdec12916bfeee0fbe36e7fcf7986d558e97070 | 0–254 | 2172×724 |
| VFX_CL_ScreenFracture_C_v01.png | ae722d3701c397788e453e2925700c15f2d9b5db02c5ee741ae7c5f1a60adcc1 | 0–254 | 2172×724 |
| TEX_CL_ScreenCutFlash_v01.png | d227e0b44a8c306ff8cd7778bb361369062db2f5d33ea364e58cd279fb88f5de | 0–254 | 2172×724 |

检查产物：`previews/screen_cut/*_alpha_report.json` 及white/dark/sky三底预览，三底均实际查看。裂痕分支有透明过渡，无烘入棋盘；光线连续。首尾非逐像素一致，不标为严格无缝。正式Assets原样复制三张，无覆盖此前素材。Unity隔离相机截图见 `previews/ScreenCut_Fracture_Clean_*`；完成的是独立模块，不是整场Boss验收。

## 实际提示词

首次较长分叉提示请求连接失败，无产物；简化后重试一次成功。以下为三个成功调用的完整提示词。前两张使用上述直线裂痕作style reference，不是edit target；第三张无输入，均请求transparent_background=true。

### 分叉B

Transparent game VFX sprite, reference is style only. One perfectly horizontal amber-orange spatial fissure through canvas center, irregular forked cracks above and below and scattered tiny glowing fragments. Thin central seam with restrained white highlights. Wide 3:1 canvas. Reusable middle segment, seam continues to left and right edges without pointed caps. No scene, text or frame. More varied branching than reference, anime science-magic effect.

### 分叉C

Create a second transparent game VFX fracture sprite in this reference's orange science-magic style. Wide 3:1 canvas, perfectly straight horizontal luminous orange seam through the center. Distinct variation: sparse long jagged forked branches, small fractured polygon shards, asymmetrical branch groups; less orange fog, more crisp cracks with transparent gaps. Reusable middle segment with seam continuing to both horizontal edges, no taper end caps. Real transparent background, no text, character or environment.

### 独立橙白光线

A single transparent 2D game VFX texture: perfectly straight horizontal orange-white cutting light, centered on a wide 3:1 canvas. A slender continuous solid white core with warm pale orange rim and restrained soft amber glow, even thickness across the whole width. Repeating beam middle segment: extends through left and right canvas edges, NO tapered end caps, NO cracks, sparks, branches, objects or text. Clear anime science-magic slash beam, transparent above and below. Only this one line.
