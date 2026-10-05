# 豆包词墙气泡候选

## 方向修正

- `raw/VFX_DB_WordPanel_Candidate_v01.png` 为废稿，不进入正式资源。它错误地把第一世界的怀旧城市语汇灌进了豆包机制，读感更像金属设备面板。
- 豆包与其他 AI 娘均为可跨关卡复用的遭遇；角色机制保持自己的视觉语言，承载关卡只提供背景、光照、出场位置与编排。

## 已采用版本

- 原始候选：`raw/VFX_DB_WordBubble_Candidate_v02.png`
- 透明裁边版：`ready/VFX_DB_WordBubble_v01.png`
- 正式资源：`Assets/_Project/Art/VFX/Doubao/VFX_DB_WordBubble_v01.png`
- 用途：单个空白对话气泡底板。中文短语由 Unity 独立覆盖；多个有独立耐久的气泡通过错位和局部重叠组成密集的俄罗斯方块式词墙。
- 风格：亲切的日用助手感，奶白文字面、浅天蓝与淡紫柔边、小对话尾巴；不绑定城市、旧电视、神殿或赛博关卡。
- 生成方式：内置图像生成。按规则请求了饱和异色底，但返回 PNG 已带真实 Alpha，因此不再做破坏性扣色。
- 状态：用户已确认，已进入项目并用于豆包词墙 Prefab。
- 导入：`Sprite (2D and UI)`、Single、PPU 512、Bilinear、MipMap 关闭、未压缩、九宫格边界 `(210, 150, 150, 135)`。
- 正式资源 SHA-256：`F6889D232410674C9A6D77847B998473654D62A8D41E8DA7645C7553F32F6EDB`。
- 文字不烘焙进图片，由 Unity `TextMesh` 独立覆盖；首版 `characterSize = 0.06`，因此短语可以继续改而不需要重画底板。
