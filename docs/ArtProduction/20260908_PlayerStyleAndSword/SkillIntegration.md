# 2dimg2motion 安装与适配检查

> 已停用，仅保留历史记录：2026-09-08 用户要求卸载。不得再将下文当作当前制作流程或加载依据。

日期：2026-09-08。用户同意先安装检查，再以 Harness 挥剑动作试验；本轮未生成新图、未修改 Assets 或 Unity 配置。

## 来源与安装

- 上游：https://github.com/WU-HAOTIAN34/2dimg2motion
- 固定提交：`d4d73e243b6ff64b7cbb3879e45e951f972a7e62`。
- 安装位置：`C:/Users/Administrator/.codex/skills/2dimg2motion`。
- 使用 Codex 自带 skill-installer，从固定提交下载；没有替换现有 imagegen、MCP 或其他技能。
- 检查过本版本全部三个 Python 脚本：standardize_baseline、fullframes_to_gif、validate_14frame_pattern。它们使用本地 Pillow 处理，没有发现网络上传、外部进程执行或凭据读取代码；这是定向检查，不是完整安全审计。

## 运行环境

系统 `D:/python/python.exe` 缺少 Pillow，没有修改该环境或全局安装依赖。

可用解释器：`C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`。

实际版本：Python 3.12.14，Pillow 12.3.0。预览与标准化脚本的 `--help` 成功；验证模块导入成功。未据此声称完整素材流水线验收通过。

## 发现的问题（未修改上游脚本）

1. `standardize_baseline.py` 命令行强制 subject-max 在300–400之间。禁止用它的默认值覆盖高清正式母版；需要缩小的生成参考图必须独立保存，不能冒充最终生产图。
2. 标准化脚本用近白阈值估计包围盒，但没有真正把裁剪范围内部的白背景转为透明。它不是完整抠图器。白色围裙、发饰与剑芯也需要保护，不能全局删除近白颜色。
3. `validate_14frame_pattern.py` 的 `_pixel_equal` 使用 RGBA 差图的默认 `getbbox()`。在当前 Pillow 下，仅 RGB 不同而 Alpha 相同会漏检。内存测试：2×2全红和全绿、不透明的两图返回 True；一致图片也返回 True。此问题未修复，禁止单凭该脚本的 OK 放行，后续需补全通道严格比较及反例测试。
4. 原验证器固定14帧并检查每帧最底端像素对齐。我们的飞行动作需按躯干/骨盆锚点检查，脚和尾部可随动作变化；不能为了通过脚底检查让身体上下跳动。
5. 主技能要求清理失败图，项目要求保留旧素材；项目规则优先：只区分 source/candidate/final，不删除旧素材。

## 后续接入边界

- 采用身份、武器归属、动作节拍、关键帧与中间帧的检查流程，不将默认参数当作项目设计。
- 生成创作由 image_gen 负责；本地脚本仅用于已授权的图像后处理、组织和验收，不通过拼接肢体或程序形变伪造新动作。
- 正式素材保持项目画风与角色世界体量。剑气独立层，图片不决定伤害范围；不动用户碰撞体。
- 不把当前六帧试验复制凑成14帧，不自动扩大生产范围。先确定母版和真正可切分的关键姿态，再按动作需要补帧。
- 上游技能完整流程仍需按其要求读取相关 references；本轮仅安装与检查，没有执行生图工作流。
