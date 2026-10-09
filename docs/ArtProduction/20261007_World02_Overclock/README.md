# 第二世界超频边缘特效

工具：内置 imagegen，生成模式，transparent_background=true；无 CLI/API 调用。

用途：第二世界“2066”超频预警与持续阶段的屏幕边缘 uGUI 特效。参考现有 BG_W02_ChaosCity_Panorama_v03 的青蓝／粉紫霓虹方向；不是角色专属素材，无人物输入。按 DeepSleep 美术生成技能拆为独立透明边缘层；动画由代码负责，不生成帧表。

原稿：raw/VFX_W02_Overclock_Edge_v01.png。
正式资产：Assets/_Project/Art/VFX/World02/VFX_W02_Overclock_Edge_v01.png，与原稿相同。

技术：1254×1254 RGBA；中心及内侧采样 Alpha=0，发光边缘带真实 Alpha。PPU100，Bilinear、Clamp、无 Mipmap、无损纹理；四边 Border=210。uGUI Image.Sliced，FillCenter=false，PixelsPerUnitMultiplier=4，直接铺满全屏 Canvas；九宫格保留拐角尺度，不把整张图非等比拉长。没有碰撞体。不改变角色、HUD安全区或第一世界。

实际提示词：

> Use case: stylized-concept. Production asset for a 2D anime cyberpunk game, a transparent screen-edge overclock energy frame for Unity uGUI 9-slice. Square canvas. Only a narrow neon electrical border hugging all four outer edges with restrained cyan and hot pink energy, small angular circuit accents at four corners. The middle 80 percent of canvas must be completely transparent and empty. Corner art confined to outer 10 percent squares; straight middle portions of each edge simple uninterrupted narrow glow strips so they can be nine-sliced without distorting ornaments. Border reaches exact outer pixel edges, not an inset panel. Delicate painted luminous texture matching a dark dense neon cyber city, good readable energy not ornate spikes. Alpha feathered inward, bright slim white-cyan energy core, sparse magenta accents. No fill, no background, no text, no symbols, no characters, no UI controls, no opaque checkerboard. Genuine transparent RGBA.

没有抠图、重绘或裁切。技术透明检查不代替用户审美确认；实际终端仍需验收。
