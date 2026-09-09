# 复活后短暂无敌 HUD 图标

用途：复活后蓝白护盾+治疗十字标识，旁边显示真实保护剩余秒数。不是角色动作，不是治疗圈替代物。

风格参考：
- Assets/_Project/Art/VFX/Shared/Revive/VFX_SH_ReviveHealCrossSheet_v01.png
- Assets/_Project/Art/VFX/Shared/Revive/VFX_SH_ReviveConvergeRing_v01.png

生成提示词：
Create one polished 2D anime game HUD status icon for DeepSleep: post-revival temporary invulnerability. References are STYLE REFERENCES only: cyan blue white crystalline healing energy, crisp beveled luminous edges and little digital squares. Draw one front-facing small rounded shield with a bright white healing plus centered inside translucent icy blue facets, a subtle upward renewal arc and a few restrained diamond sparks. Clear readable chunky silhouette at 56 pixels; compact, cute and refined, not a photoreal metal shield. No letters, numbers, text, characters, shadow or UI frame. One icon centered, occupies about 75% of square canvas with generous clear margins. IMPORTANT production background: perfectly flat saturated MAGENTA #FF00FF everywhere outside the icon (also in gaps), no checkerboard, no transparency, no gradient. The icon itself uses cyan/blue/white only, no magenta/pink/purple; only a tight soft blue edge glow. This solid contrasting background will be removed locally. 1024x1024 square.

实际输出1254×1254；原图未覆盖，SHA256见matte_report.json。底色实际有轻微波动，因此配方中backdrop_tolerance=60；只去掉红蓝均高、绿低的背景区，并对混合边缘做去色。此方法只适用当前无粉紫色前景的蓝白图标，不能直接套用HS红黑人物或其他含紫色素材。

运行：`python docs/ArtProduction/Tools/prepare_sprite_batch.py docs/ArtProduction/20260909_ReviveProtection/recipe.json`。

透明结果：776970全透明像素，71371半透明像素，724175不透明像素；四角透明，可见bbox=(118,72,1147,1173)，无贴边裁切。生产输出为ready目录同名PNG的原样拷贝，Unity按独立UI图标导入，不走战斗特效倍率。
