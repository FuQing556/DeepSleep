# 2026-10-05：DeepSeek / Harness 分层 uGUI 素材

本批沿用用户已确认的双主题方向，使用内置生图工具独立生成 **14 张原生透明 PNG**，不是从整张界面截图拆出的拼贴。用户确认的是美术方向与分层方案，尚未逐张验收最终资产。

## 图层与来源

DeepSeek（DS）与 Harness（文件代码 HA）各 7 张：`ButtonBase`、`ButtonFrame`、`ButtonGlow`、`ButtonOrnament`、`PanelFrame`、`CircleBase`、`CircleFrame`。底板、边框、辉光、装饰为独立图片，交互文字与动态数字仍由 uGUI 实时渲染。

- [prompts.json](prompts.json)：逐张生成提示、风格参考及原始生成文件来源。
- `raw/`：生图原始文件，只读保留。
- `ready/`：本批正式裁切入口，14 张均已导入 `Assets/_Project/Art/UI/Themes/DeepSeek/` 或 `Harness/`。

## 透明与裁边范围

本批不需要色键抠图。每张独立采用 `alpha > 1` 的边界框向外扩 16 像素，仅裁去框外透明画布与极淡的 `alpha = 1` 漂点；没有按 `alpha > 8` 截断光晕。14 张合计裁去 13,948 个非零像素，最高 alpha 为 1；保留区域 RGB/alpha 与原图逐字节一致，没有改色、修形、重采样或内部去点。原图 SHA256 均未改变。

白底、暗底、天空底已检查，未发现烘焙底色、棋盘残留或明显裁断。框内部仍保留零星 alpha=1 像素，不声称内部每个像素都为零透明。

- [裁切与正式导入清单](previews/alpha_audit_final/production_crop_manifest.json)：源/输出 SHA256、尺寸、裁切偏移、裁去数量、光学边距、九宫格边界、正式资产路径及 Unity 导入参数。
- [Alpha 测量](previews/alpha_audit_final/layer_measurements.json)与[透明验收结论](previews/alpha_audit_final/audit_result.json)。
- 预览：[白底](previews/alpha_audit_final/contact_sheet_white.png)、[暗底](previews/alpha_audit_final/contact_sheet_dark.png)、[天空底](previews/alpha_audit_final/contact_sheet_sky.png)。`alpha_audit_v01/v02` 是前 10 张的过程记录，以 `alpha_audit_final` 为最终测量入口。

## Unity 接入边界

14 张使用 Sprite / Single / FullRect、PPU 100、原生 Alpha、关闭 Mipmap、Bilinear、Clamp、Uncompressed、Max Size 2048。九宫格边界按清单的 `suggested_border_lbrt_px` 原值导入，单位为包含透明余量的源像素。

底板、边框与辉光须按各自可见轮廓对齐；不能只把不同画布强制铺满同一矩形。按钮框主要横向拉伸，大面板使用专用 `PanelFrame`；圆形和装饰图使用 `Simple + preserveAspect`，不得压扁。实际布局与动效由 uGUI 接入代码负责，素材导入本身不等于游戏内验收通过。

本批仅交付上述双主题通用分层素材；**不代表所有 UI 图标、数字、独有界面美术或完整 UI 系统已经完成**。
