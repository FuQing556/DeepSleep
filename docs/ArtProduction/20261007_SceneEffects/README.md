# 2066场景状态边缘素材

按 deepsleep-art-generation 与 imagegen 技能生成，2026-10-07。用户确认采用独立素材拼接，不生成整框。使用内置image_gen，六次独立调用，均设置transparent_background=true。无参考人物、无文字、无代码绘制替代生成素材。

原稿存于本目录raw；运行资产存于 `Assets/_Project/Art/VFX/World02/SceneEffects/`，文件内容未二次绘制。RGBA实际透明通道已检查，不是假棋盘背景。

| 文件 | 尺寸 | 原始生成文件 |
| --- | --- | --- |
| TEX_SceneFast_Edge_v01.png | 2172×724 | exec-24549882-e785-4630-9b21-91736fe22460.png |
| VFX_SceneFast_Corner_v01.png | 1254×1254 | exec-b86b27f0-4006-43c1-8502-1b5e0d94ad82.png |
| TEX_SceneSlow_Edge_v01.png | 2172×724 | exec-b4f04bb6-87f8-4b42-bc68-0383342f54c1.png |
| VFX_SceneSlow_Corner_v01.png | 1261×1247 | exec-39dbef44-aebd-4ac7-877f-cb0f2bd3bb72.png |
| TEX_SceneReverse_Edge_v01.png | 2172×724 | exec-766ba7f2-cfaf-427d-aa44-af124258f7e1.png |
| VFX_SceneReverse_Corner_v01.png | 1262×1246 | exec-a955b57e-7d27-4d3e-9802-e3c00d69864c.png |

边条按Default Texture导入，U Mirror/V Clamp，关闭mipmap、Bilinear、alphaIsTransparency、最大2048、CompressedHQ；角落Sprite Single、PPU100、Clamp，关闭mipmap。角落非精确方形，UI preserveAspect=true保留比例。

虽然提示词要求循环，但原稿不保证左右像素精确匹配，所以实际用Mirror往返采样，不声明是无缝Repeat贴图。uGUI等比重复边条UV，超长屏增加重复数，不拉长素材。四角独立旋转/镜像组合。实际动态、时钟与透明度配置见../../SceneBattleEffects.md。截图与渲染缺陷修复经过记录于该文档。

## 六次完整提示词

### Fast edge

Production transparent game VFX texture, horizontal wide 4:1 canvas. ONE orange-red accelerating energy edge strip for a cyberpunk anime game. Dense bright warm orange light concentrated along the exact TOP edge, flowing sideways with broad luminous streaks and sparks, feathering downward to fully transparent at bottom. Strong visible broad glow, NOT a thin line or UI frame. No corners, no border box, no text, no icons, no objects, no solid backdrop. Left and right ends visually seamless for horizontal repeating UV scroll. Fill the entire width, energetic streaks extend naturally beyond the sides. True transparent RGBA with a soft alpha gradient. This is a single edge texture, not a full frame.

实际输出3:1，未强行拉成提示词4:1。

### Fast corner

Single transparent VFX corner accent for a 2D anime cyberpunk game, square canvas. An orange-red energy flare erupting from the TOP LEFT corner toward the lower right. Bright concentrated yellow-orange light, orange sparks, curved acceleration streaks, broad translucent glow feathering into nothing. Art touches top and left canvas edges, empty transparent lower right. No frame or thin border, no text, no icons, no objects. Energetic detailed painted light, true transparent RGBA. It will be rotated and mirrored at screen corners independently of an edge strip.

### Slow edge

Production transparent game VFX horizontal edge texture, wide 3:1 canvas. ONE ice-blue cyan slowing-time edge strip for an anime cyberpunk game. Broad luminous cool mist trails flowing sideways, smooth layered wavelets and icy particles, brighter across exact TOP edge, fading downward smoothly to fully transparent bottom. NOT a frame or UI panel, no corners. Visible rich pale cyan and deep blue painted light. Left and right sides should join visually for repeating horizontal UV scroll. No text, no icons, no dark backing, no solid background. Genuine transparent RGBA, soft alpha wisps, all width filled.

### Slow corner

ONE square transparent game VFX corner for slowing-time status. Ice blue cyan flowing wave and mist bloom emanating from exact TOP LEFT corner, touches top and left edges. Detailed luminous icy filaments with a few crystalline particles, curved waves reaching toward lower right and fading to completely transparent. Broad readable gentle painted glow, anime cyberpunk VFX quality. Lower right mostly empty. No frame, no UI box, no text, no icons, no opaque background. True transparent RGBA. Will be combined in Unity at four corners.

### Reverse edge

ONE production transparent horizontal 3:1 edge texture for reversed controls in an anime cyberpunk game. Broad violet-magenta crossed wave filaments, crossing and twisting sideways near exact TOP canvas edge, feathering downward to transparent bottom. Luminous purple ribbons and tiny glitch shards; clear rhythmic overlapping interference wave texture, NOT arrows, NOT frame, NOT UI box. Left/right texture edges visually join for repeating UV scrolling. No lettering, icons, characters, solid backdrop or checkerboard. True transparent RGBA, brilliant readable purple light, moderate soft glow. No corners: only one edge strip.

### Reverse corner

ONE square transparent VFX corner accent for reversed movement in an anime cyberpunk game. Violet-magenta interference flare starts at exact TOP LEFT corner, touches top and left canvas edges, with luminous crossed looping ribbons and a few geometric glitch shards flowing diagonally inward. Broad visible painted light feathering away into a fully transparent lower right. No entire frame, no UI box, no arrows, no text, no objects, no solid background. True transparent RGBA. Matches violet crossed-wave edge strip, independently combined at screen corners.
