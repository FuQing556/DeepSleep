# 2026-10-05 UI 补全素材批次

本批正式输出 **16 张**：DeepSeek / Harness 主页标题各 1 张、主页静态背景各 1 张、局内 Buff 图标 12 张。所有美术由生图生成；这里只做原生透明检测、标题留白裁切与无损拷贝，不重画、不去色、不换色。

## 正式入口与选源

- `ready/` 是本批生产 PNG 入口；`production_manifest.json` 记录每张选用 raw、原始/输出 SHA256、尺寸、裁切偏移、Alpha 数据和正式路径。
- `Assets/_Project/Art/UI/Themes/{DeepSeek,Harness}/` 接收 `SPR_UI_{DS,HA}_TitleLogo.png`、`BG_UI_{DS,HA}_MainMenu.png`。
- `Assets/_Project/Art/UI/Buffs/` 接收 12 张 `ICO_BUFF_*.png`。
- DataCompression、RiceFan、RiceStorm 的正式文件 **仅选用 raw 中的 `_v2.png` 米粒版本**，输出名去掉 `_v2`。
- 同名不带 `_v2` 的三个 raw 是用户否决的三角海苔饭团废稿；保留原图与哈希作追溯，列于 manifest 的 `rejected_sources`，**从未复制到 ready / Assets，不可回填为正式资源**。
- `prompts.json`、`correction-prompts.json`、`remaining-prompts.json` 由主 agent 维护，本处理流程不修改。

12 个 Buff：DataCompression、FaultToleranceExpansion、RiceFan、RiceStorm、RiceGuidance、RiceSplash、RiceGuard、QuantumSword、TerminalAmplifier、TerminalArray、TerminalBurst、TerminalChain。

## 处理边界与尺寸

- 所有 raw 只读保留。脚本末尾重验全部正式选源及三张废稿哈希。
- 背景各为 1942×809，原样拷贝，无裁切、无重采样；用于静态主页，不是无缝循环关卡背景。
- Buff 全部保持原始 1254×1254 方画布和完整 PNG 字节；软光 Alpha 不作阈值清理。Unity 可自行设置导入分辨率，不能用非等比拉伸改图标形状。
- 标题按 **alpha>1 包围框 +16px 留白** 另存裁切稿，裁内 RGBA 完全不变；所有 alpha≥2 像素保留。DS 2172×724 →2094×618，移出 140 个 alpha=1 像素；HA 2172×724 →2157×559，移出 61 个 alpha=1 像素。精确源坐标见 manifest。
- 标题按等比显示；背景按等比 cover/crop，需分别检查 16:9 和超宽屏。未修改碰撞体、玩法或任何 `.meta`。

## 审计结果与限制

`visual_review.json` 是逐张实际目视记录。已查看源图、白/暗/天空底预览，以及所有 12 个 Buff 的真实 40px 预览；未发现假透明底、烘焙棋盘、明显黑白边或内部被误抠除。两张标题均完整读作 **DeepSleep**，字形与装饰未截断。

- 40px 下十二个图标的主要轮廓仍可区分；细小面板纹理、宝石切面和箭头不替代文字说明。
- RiceSplash 主体略小，40px 方框内约 26px 直径，但五米粒放射/中心爆点仍清楚；保持源画布，不擅自放大或重画。最终 HUD 的视觉比重由场景实测判断。
- 白底上的浅绿色心形亮部对比度偏低，深色/天空 HUD 底上更清楚；黑色碗和终端外壳均完整保留。
- PNG 审计不等于用户美术验收或 Unity 场景验收。当前 agent 未操作 Unity；导入参数、主题引用与屏幕布局由主 agent 实测后记录，不凭拷贝成功宣称场景通过。

预览入口：`previews/buffs_contact_{white,dark,sky}.png`、`previews/buffs_40px_white_dark_sky.png`；每张图的独立 Alpha JSON 与三底预览位于 `previews/alpha_audit/`。

## 重跑

`python docs/ArtProduction/20261005_UiCompletion/prepare_completion.py` 处理全部 16 张选定源并生成审计；加 `--copy-assets` 复制到指定正式目录。已有输出只接受相同像素/哈希，不覆盖不同正式文件；禁止把废稿传给 `--sources`。脚本复用已安装的 `deepsleep-sprite-matting/scripts/inspect_sprite.py`。
