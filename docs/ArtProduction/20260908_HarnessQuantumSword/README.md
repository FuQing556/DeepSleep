# HS 近战透明素材候选集 · 2026-09-08

当前交付目录是 `ready/`，共 8 张 RGBA PNG。尚未导入 Unity，也未改动场景、预制体、碰撞体或角色缩放。

## 素材清单

| 文件 | 用途 | 说明 |
| --- | --- | --- |
| HA_MeleeIdle_v01.png | 近战待机 | 本地去除假棋盘背景 |
| HA_MeleeDownCommand_v01.png | 下劈指挥动作 | 本地去除假棋盘背景 |
| HA_MeleeUpCommand_v02.png | 上挑指挥动作 | 保留用户确认的画面，仅抠背景，不重画人物 |
| HA_MeleeSweepCommand_v03.png | 横劈指挥动作 | 新裙子候选，等待美术验收；生成后本地去绿底 |
| HA_QuantumSword_v01.png | 独立量子剑 | 保留原生透明像素，不与人物烘焙合并 |
| HA_QuantumSlashDown_v01.png | 下劈剑气 | 去假棋盘并重建红色半透明光晕 |
| HA_QuantumSlashUp_v01.png | 上挑剑气 | 去假棋盘并重建红色半透明光晕 |
| HA_QuantumSlashSweep_v01.png | 横劈剑气 | 保留原生透明像素 |

四张人物均保留 1254 × 1254 源画布，不通过拉伸统一姿态轮廓。Unity 中各姿态的视觉尺寸、锚点与武器轨迹仍需在接入时核对，PNG 完成不代表这些配置已完成。

## 本轮裙子重画

使用内置 imagegen 编辑工具，无 CLI/API 回退。第一张输入是上一版横劈候选，第二张服装参考为 `Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png`。

实际提示词：

> 重画第一张图的裙子，服装以第二张HS常态图为准。保留第一张的脸、头身比例、双手横扫姿势和飞行构图。裙子恢复成第二张那种整洁的黑色女仆短裙、清晰的白围裙、细红色花纹和简洁白蕾丝裙边；裙摆只随横扫轻轻飘起，自然遮住髋部，不要大幅翻卷，不要蓬成蛋糕裙，不增加多层荷叶边。整个人物依旧精细可爱，服装与第二张保持一致。保持全身完整、均匀纯绿背景。不要画武器或特效。

生成原图已保存在 `raw/HA_MeleeSweepCommand_v03.png`，透明版在 `ready/`。其他七张沿用已有原图，本轮没有重新生成。

## 透明处理与限制

- `finish_characters.py`：中性背景连通抠图、局部封闭缝隙处理和绿幕去边；不重画脸、服装或动作。
- `finish_effects.py`：原生透明剑与横劈剑气保留像素；下劈、上挑剑气从不透明源图重建透明边缘，因此红色光晕比源图收敛，不是原始 alpha 的无损恢复。
- `ready/character_report.json`、`ready/effects_report.json`：尺寸、透明通道及源图校验记录。
- `previews/REVIEW_Characters.jpg`、`previews/REVIEW_Effects.jpg`：人物及深浅底特效预览，不是可导入素材。

旧 `cutouts/`、`recipe.json`、`matte_report.json` 和 `repair_sweep_hand.py` 保留为失败尝试记录，**不是本次交付入口，不能据此批量覆盖 ready 或导入 Unity**。原始素材全部保留。
