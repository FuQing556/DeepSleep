# Kimi月光受击罩

使用 `deepsleep-art-generation` 与内置 `imagegen`，2026-10-07。

参考1（角色配色，非重画目标）：`Assets/_Project/Art/Characters/Kimi/SPR_KI_Idle_v01.png`。
参考2（特效风格）：`Assets/_Project/Art/VFX/Kimi/VFX_KI_Hit_v01.png`。
正式素材：`Assets/_Project/Art/VFX/Kimi/VFX_KI_MoonShield_v01.png`。
原图备份：`raw/exec-44ce798c-a467-4d68-b0b5-fed512be42c8.png`。保留生成Alpha，无本地重画/抠图。

实际提示词：

> Use case: stylized-concept. A single standalone transparent-background game VFX sprite: Kimi's moonlight protective aura, matching the provided violet-silver anime magical effect style reference (image 2); image 1 is character palette reference ONLY, do not draw the girl. A delicate spherical moonlight bubble seen front-on, almost circular, thin silver-white and lavender luminous rim with a small crescent gleam at upper left and restrained prism glints near the edge. The inner 75 percent of the disc must be EMPTY and fully transparent, no central starburst, no opaque fill, no face-obscuring ornament. Gentle fine moon shimmer restricted to the outer ring; elegant and readable at small game scale. One aura only, centered with generous transparent margin, complete rim not cropped, no character, no text, no background. Real alpha transparency.

生成结果的光带比提示词要求宽；接入时用5.2u画布宽和0.8峰值Alpha，让中心空白包住人物脸/身体。当前PPU512、中心Pivot、等比缩放、Gameplay人物SortingGroup内身体+3、无Collider。受实际扣血触发，0.3s淡出；不是新增防御盾，不改变HP、次数盾、碰撞或伤害。

本轮仅完成素材/代码/正式装配及编译检查。依用户要求，实际战斗、手机与联机画面验证留到下一轮，不能据此宣称脸部可读性已实玩验收。
