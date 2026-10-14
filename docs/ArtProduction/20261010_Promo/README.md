# 横屏宣传图 v01

## v02 正式游戏名修正

- 用户确认正式名称为《中国AI会飞》，DeepSleep是工程名，不作为本张宣传图标题。
- 文件：`raw/ChinaAI_Promo_Landscape_v02.png`。
- 工具：内置 imagegen 编辑；目标为v01宣传图。
- 实际提示：仅替换左侧DeepSleep标题为准确的“中国AI会飞”，可排成“中国AI / 会飞”两行，沿用冰蓝白发光标题；保持双主角、服饰、道具、背景和横屏构图，无其他文字。
- 已查看文字为“中国AI会飞”，保留旧版本，没有修改游戏配置。

## 商店规格导出

- 文件：`ChinaAI_Promo_1920x1080.jpg`，1920×1080，904456字节，小于平台4MB上限。
- 使用`Export-StorePromo.ps1`对v02进行高质量双三次放大，等比铺满16:9画布，极微量居中裁边，无拉伸，不重新生图；JPEG质量95。
- 已重新查看导出图，标题、人物保留。此为尺寸适配，不代表原生新增细节或平台审核已通过。

- 成品：`raw/DeepSleep_Promo_Landscape_v01.png`。
- 内置 imagegen 生成；不透明横屏宣传插画，非游戏截图，未修改正式游戏素材。
- 身份参考：`Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png`、`Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png`。
- 场景参考：`Assets/_Project/Art/Backgrounds/BG_W01_DuskRiver_Panorama_v01.png`、`Assets/_Project/Art/Backgrounds/World02/BG_W02_ChaosCity_Panorama_v03.png`。
- 提示词：横屏16:9游戏宣传主视觉，保持DS和HS的Q版身份、服饰、鲸鳍和尾巴；双主角悬浮在右侧，DS端饭碗、HS使用黑红平板，左侧大标题DeepSleep。黄昏河畔村落与右侧污浊霓虹城市用云雾及蓝紫天空连接，少量蓝色米粒光与红色几何粒子；赛璐璐人物、精细环境，无其他文案、UI、水印或圆角边框。
- 提示请求1920×1080，实际生成1672×941，近似16:9，未拉伸或裁切。
- 已查看：双主角身份、服饰、动作、完整外轮廓与标题；生成式插画并非原贴图逐像素拼接，最终接受以用户确认及平台尺寸要求为准。
