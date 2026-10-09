# Claude雨夜与雨雾循环候选

## 当前实现（2026-10-08，覆盖下文历史状态）

独立可复用雨层已经创建，未挂入正式场景：

- `Assets/_Project/Prefabs/UI/Weather/PF_ClaudeRainOverlay.prefab`：完整实际屏幕Canvas，两层RawImage，不挡输入。默认透明，由显式引用的`RainOverlayView.SetWeatherActive(bool)`控制开关。
- `Assets/_Project/Scripts/Runtime/UI/RainOverlayView.cs`：现实时间滚动，暂停冻结，淡入淡出0.6秒；每层TileHeight、UnitsPerSecond、InitialPhase可调，透明度在RawImage Color Alpha中调。远层初值480/650/0/0.65，近层880/1250/0.37/0.35（纹理高度/每秒Canvas单位/相位/Alpha），均为待动态验收的视觉初值。
- `Assets/_Project/Shaders/UI/RainOverlap.shader`与预制体目录的`MAT_RainOverlap.mat`：纵向12%重叠交叉淡化，预乘Alpha混合；横向镜像重复。修改重叠时Material与View的SeamOverlap须一致。
- `Assets/_Project/Art/VFX/World02/Claude/TEX_CL_RainMist.png`：原稿字节不变，Clamp、无MipMap、无压缩，不进行去雾。长屏增加UV重复数，不非等比拉长雨线。

预制体Canvas Order=4，已只读检查World02触控Canvas=5、场景状态=20、HUD=900，雨位于它们下方；未包含GraphicRaycaster。Claude生命周期尚未存在，因此不对整关自动启雨。背景仍为候选，不擅自替换。没有更改概率、时间倍率、怪物或现有场景。

验证：脚本编译和Shader检查无Error；隔离Editor预制体12项断言通过，包含默认隐藏、淡入/淡出、暂停UV/Alpha不变、隐藏停滚、2400×1080与1920×1080等比UV、纹理与材质引用、非交互及重叠一致。Boot场景保持clean。此为逻辑/资产验证，不是动态游戏画面、手机或双端验收；上下重叠仍须实看雨丝接缝。

2026-10-08，使用deepsleep-art-generation、imagegen，内置工具。生图两次雨纹后停止同类抽图。曾准备按deepsleep-sprite-matting去底雾，用户明确要求保留灰雾，立即停止处理；源像素没有修改，仅借用白/暗/天空底透明检查方法生成预览。用户认可灰雾，不表示已确认动态循环。

## 产物

- raw/BG_W02_ClaudeRainNight_v01_CANDIDATE.png：以正式World02背景v03为编辑目标，保留构图与城市结构，冷暗夜色、湿面霓虹；未烘入动态雨丝，不是无缝背景。
- raw/TEX_CL_RainMist_v01.png：1254方图，保留用户喜欢的蓝灰底雾与细雨。Alpha范围13～210，全图半透明，没有完全透明空隙，这是保留的雾，不再按纯雨丝抠除。源SHA256 `825763a653142e15bd0650284d2873af215b4cfee16d557d78af1196a17605ad`。
- raw/TEX_CL_RainFine_v02_REJECTED.png：白块瑕疵，退役，不使用。
- previews/RainMist_VerticalPair.png 与 RainMist_JoinCloseup.png：同一雨纹上下双副本，白/暗/天空色检查底；仅检查图，不是游戏截图。脚本preview_rain_join.py不修改源图。

## 接缝结论与接入方案

上下边界预乘RGB/Alpha平均差5.5606，图内相邻行平均差0.2659；数值是边缘不匹配信号，不是美术通过指标。近看部分雨丝跨接缝断开，不能声称是原生无缝图。保留原图，计划渲染时两块短重叠区交叉淡化，跨边界UV衔接处不硬切，实测高速滚动后再验收，不在本轮伪称已修。

远近可共用该图，不同等比纹理尺度、透明度、速度和相位；不非等比拉长雨线。实际屏幕满幅覆盖，包含手机超长屏两侧，不限制UI安全区，不随游戏镜头震动露边；渲染位于场景之上、HUD之下。雨向下快速滚动，暂停冻结、场景倍速不改变雨速。该方案已经用户认可；具体速度/透明度仍需动态验收。没有接入Unity或编写雨Runtime组件。

生产卡：现有2066背景v03→雨夜光照→静态宽图不含雨→不透明→Boss场景候选；雨雾→垂直下落纹理→独立半透明重复层→动态下雨。未验证背景手机裁切或镜头余量；未替换正式资源。

## 雨夜背景实际提示词

```text
Use case: lighting-weather. Edit this exact wide cyberpunk city background into a darker rainy-night atmosphere while preserving its architecture, billboard positions, cables, central open space and wide panorama composition. Keep the same painted anime environment style. Deep blue-gray night, damp reflective surfaces, subtle cold haze, restrained cyan and magenta neon, occasional warm window lights. No human faces. Do NOT bake falling rain streaks into the picture: falling rain will be a separate animated overlay. No lightning flash, moon, boss, UI or new giant object. Preserve readable darker central combat space and scenery extending through both edges. Opaque full-bleed wide 3:1 background.
```

## 雨雾v01实际提示词

```text
Use case: stylized-concept. A seamless tileable transparent rain-overlay texture for a 2D game, square canvas. Many fine short cool white-blue rain streaks pointing straight vertically downward, varying lengths and low opacity, naturally scattered with uniform density throughout the tile. No scene or mist, ONLY thin rain streaks on genuinely transparent background. Crucial: seamless top-to-bottom repeating texture, streaks crossing the top edge continue exactly at the bottom, also tileable left-to-right; no blank margins, vignette, central cluster or gradient in density. Distant rain layer, delicate and subdued, no splashes, snow, rings, clouds, glass, text or UI.
```

## 纯雨v02退役实际提示词

```text
A transparent rain streak texture: ONLY scattered fine straight white vertical dashes, varying short lengths, sparse spacing. Pure empty transparent gaps between every rain streak. No colored haze, cloud, fog, noise or background fill whatsoever. Uniform distribution all across square canvas, tileable vertically and horizontally, top and bottom must join continuously. 2D anime game distant rain overlay. True transparent background. This is rain falling through air, not water on glass.
```
