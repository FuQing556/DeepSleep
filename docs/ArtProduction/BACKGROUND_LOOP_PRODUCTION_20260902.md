# P0 横向循环远景生产记录｜2026-09-02

## 结论

`BG_P0_Far_DataSky_Loop_v01.png` 是第一张按游戏循环规则生产的远景，不是完整场景插画。

| 项目 | 结果 |
|---|---|
| 正式文件 | `Assets/_Project/Art/Backgrounds/BG_P0_Far_DataSky_Loop_v01.png` |
| 生成源 | `docs/ArtProduction/Backgrounds/SOURCE_BG_P0_Far_DataSky_v01.png` |
| 两副本预览 | `docs/ArtProduction/Backgrounds/PREVIEW_BG_P0_Far_DataSky_Loop_v01_2X.png` |
| 可复用处理工具 | `docs/ArtProduction/Tools/BuildHorizontalLoopTile.ps1` |
| 正式尺寸 | 2048×1080，24-bit RGB，不透明 PNG |
| 正式文件 SHA-256 | `16BC5D3375D32B8F6D7E091E4923F0147C9BF3F3F91F30BA34F53F6878241855` |
| PPU / 世界尺寸 | 100 / 20.48×10.8u |
| Sorting | Background，Order -30 |
| 建议滚速倍率 | 0.08；最终值由背景配置填写，不写死在组件中 |
| 状态 | `PRODUCTION_READY`；离线检查通过，等待用户在 Unity 双副本滚动验收 |

## 循环硬验收

- 首列与末列：RGB 最大通道差 `0`，平均差 `0`。
- 首尾镜像 16px 带：RGB 最大通道差 `0`，平均差 `0`。
- 双副本预览：中央接缝无亮度竖线、云块断头或唯一地标跳变。
- 画面中央为低细节蓝色天空，给主角、饭团和弹幕保留可读空间。
- 图中无文字、Logo、水印、UI、月亮或只出现一次的大型地标。

离线检查不能冒充 Unity 实机通过。用户导入后仍需以两个 SpriteRenderer 相距 20.48u 连续滚动至少 60 秒，确认 Bilinear 采样、相机像素位置和缩放没有产生细线。

## Unity 导入建议（尚未由用户执行）

| 字段 | 值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | 100 |
| Mesh Type | Full Rect |
| Filter Mode | Bilinear |
| Compression | None |
| Generate Mip Maps | Off |
| Wrap Mode | Clamp（采用两个 SpriteRenderer 回绕，不依赖材质 Repeat） |
| Pivot | Center (0.5, 0.5) |

背景循环组件后续读取显式配置中的滚速与回绕宽度，不在代码里写 `20.48`。两个背景对象由用户在 Prefab/场景中明确装配，代码不运行时创建对象或补 SpriteRenderer。

## 生成方式

- 模式：Codex 内置图像生成工具，基于已有 `BG_Far_AICloud_v01.png` 只参考蓝色氛围和柔和程度。
- 后处理：保持纵横比 Cover 到 2048×1080；首尾 192px 平滑过渡到共同边界色带；首尾 16px 严格同带；输出双副本预览并逐像素检查。
- 旧三张背景没有删除，已统一降为 `DECOR_ONLY`。

## 原始提示词

```text
Use case: stylized-concept
Asset type: production source for a horizontally looping 2D side-scrolling game background
Primary request: Paint a wide anime-cartoon far-background tile for the prologue of a game about a flying blue whale-girl crossing an AI data sky. It must be designed specifically for endless horizontal repetition, not as a one-shot illustration.
Input images: Image 1 is a palette and softness reference only; do not copy its moon or exact cloud arrangement.
Scene/backdrop: a calm deep-blue-to-cyan vertical sky gradient, distant soft cloud banks shaped subtly like data waves, a few extremely faint network arcs and tiny low-contrast data lights. No characters, no foreground objects.
Style/medium: polished Japanese anime game background, simple cel-painted cloud masses, clean shapes, restrained detail, friendly chibi-world atmosphere.
Composition/framing: very wide landscape tile, side-scroller orthographic feel, no perspective vanishing point, no central landmark. Keep the central gameplay band quiet for character and projectile readability. All decorative clusters must be fully contained away from the left and right boundaries.
Seam requirement: the leftmost and rightmost 15 percent must both be unobstructed sky using the same vertical color gradient, with no clouds, nodes, stars, cables or distinct brush marks touching either side. Both side boundaries must have matching brightness and hue so two copies can join without a visible cut. Top and bottom do not need to tile.
Lighting/mood: soft luminous dawn/night transition, calm and airy, lower contrast than gameplay sprites.
Color palette: navy #10295E at top, medium blue #3F75C7, pale cyan #BCEBFF near lower distance; avoid pure white highlights.
Constraints: no text, no logos, no watermark, no border, no frame, no UI, no moon, no sun, no large star, no unique focal landmark, no objects touching left or right edges, no hard vertical lighting bands. This is a background texture source, not concept-sheet presentation.
```
