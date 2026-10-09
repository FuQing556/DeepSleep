# 递归果冻母图候选 v01

2026-10-07，仅制作单个常态母图，等待用户确认；未导入 Unity，未更改代码、碰撞或出怪配置。

工具：内置 image_gen，transparent_background=true。

参考（仅用于画风，不复制角色身份）：
- Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png
- Assets/_Project/Art/Enemies/404Window/SPR_EN_404Window_Idle_v01.png

候选：raw/SPR_EN_RecursiveJelly_Idle_v01.png。

用户机制：大体死亡沿四个对角方向生成四只中体；中体死亡向左右生成两只小体；小体不再分裂。移动和受击的 Q 弹通过代码变形贴图实现，不制作帧动画。此次未实现这些机制。

实际提示词：

> Use case: stylized-concept. Create ONE isolated 2D enemy sprite candidate for DeepSleep, a recursion jelly monster, neutral idle mother artwork only, square canvas with genuine transparent background. The two input images are STYLE references only: match their polished cute anime game linework, controlled cel shading and readable silhouettes; do not reproduce the girl or the window. Subject: a single broad squat rounded semi-transparent yellow-green gelatinous living blob, substantial heavy thick jelly, bulging soft sides and a slightly flattened heavy underside, floating without ground or cast shadow. Dark green rim and shaded inner thickness, restrained cool pale highlights and a few internal bubbles, translucent body with enough solid color to remain readable against neon backgrounds. Inside the center, large very clearly readable dark green exact text 'f()' suspended in the jelly, lowercase f and a pair of round parentheses. This function symbol is its identity, no additional face or eyes, no arms, no accessories, no city scenery. Cute organic personality in its slightly lopsided weighty jelly silhouette, not a rigid glass sphere, not photorealistic and not 3D rendered. One intact resting body, no splitting babies, no motion streaks or effects baked into it. Body occupies around 75 percent of canvas width with clear transparent padding all around. Intended to squash and stretch as one image in Unity later; preserve a simple continuous contour and uncluttered center. No captions, no watermark, no checkerboard painted in background.

检查：实际文件 1254×1254，32 位 RGBA，左上角 Alpha=0，采样主体内部 Alpha=253。单体轮廓完整，f() 可读，没有背景/动作残影。成图横向占幅比提示词预期大，后续若作为生产资源使用需确认留边。果冻的透光质感不等于主体已具有合适的游戏内 Alpha；透明程度、缩小后的效果和变形手感尚未在游戏中验证。
