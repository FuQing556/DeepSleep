# 第一世界 · 黄昏城远景交付

- 用途：第一世界战斗场景的最远循环背景层。
- 母图参考：`BG_P0_Far_DataSky_Loop_v01.png`，仅继承干净空域、柔和层次与角色可读性。
- 生成要求：互联网初生年代的中国城市黄昏；安稳、真挚、充满希望，同时保留一天无所建树的淡淡哀伤；禁止赛博朋克、废墟、文字、人物与近景遮挡。
- 图层约束：城市仅占底部约 20%-28%，中上部留作战斗空域；太阳不烘焙在循环层中，后续作为独立层控制。
- 原始生成图：`raw/BG_W01_Far_DuskCity_noSun_source.png`。
- Unity 成品：`Assets/_Project/Art/Backgrounds/BG_W01_Far_DuskCity_Loop_v01.png`，2048×1080，Sprite、100 PPU、无压缩。
- 循环处理：使用 `BuildHorizontalLoopTile.ps1 -MirrorLoop` 构造严格连续的镜像循环；双瓦片检查见 `previews/BG_W01_Far_DuskCity_Loop_double.png`。
- 局限：远景为了保证无缝采用镜像组织，视觉中心较对称；后续近景楼体、太阳、云层会作为独立非循环层打破对称。
