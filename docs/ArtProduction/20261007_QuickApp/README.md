# 快应用：广告混合体候选 v01

工具：内置 imagegen，单张透明底候选。尚未导入正式素材、Prefab或战斗配置，等待用户确认。

输出：raw/SPR_EN_QuickApp_Idle_v01.png，1254×1254 RGBA，左上角Alpha为0。未进行抠图、缩放或覆盖旧素材。

风格参考（已实际查看）：
- Assets/_Project/Art/Enemies/404Window/SPR_EN_404Window_Idle_v01.png
- Assets/_Project/Art/Enemies/DownloadCharger/SPR_EN_Download_Dash.png

使用 deepsleep-art-generation / imagegen 技能，对齐既有怪物线条、赛璐璐阴影和轮廓。红包/金币主弹窗，后方播放、抽奖和免费广告卡片组成紧凑整体，无真人脸、箭头或烘焙残影。关闭叉比提示中的“小叉”更醒目；作为候选保留，不盲目重画。大S曲线、四边穿屏、拖影和强击退留待后续代码实现。

实际提示词：

```text
Use case: stylized-concept. Create one standalone 2D enemy sprite for the Unity game DeepSleep, enemy name “快应用”. The two attached images are STYLE REFERENCES ONLY: match their clean dark outlines, polished anime game cel shading, chunky readable shapes and restrained highlights; do not copy the arrow or 404 identity.
Subject: a compact floating advertising amalgam, formed by one dominant rounded rectangular popup card and three or four smaller crooked advertising cards tightly overlapping behind it, all physically forming ONE cohesive enemy silhouette, not a sheet of separate assets. Main front card is bright red/coral with warm golden trim; a large gold-and-cream button reading exactly “立即领取”, a red packet with gold coins as the central pictogram, and a comically tiny close X tucked into its corner. Behind it, a pink video-ad card with a simple white play triangle, a small colorful prize-wheel card, and a coupon banner reading exactly “免费”. Balance tawdry red/gold/pink ad colors with a little cyan electronic edge light. Busy cheeky intrusive advertising personality without any human face, people, limbs or mascot face. Rounded edges and slight tilts make it lively. Mostly front facing, shallow illustrated thickness, no realistic phone casing. Tight compact roughly square body with clear outer boundary and generous transparent margin. Make the large button and red packet readable when reduced to small game size; avoid excessive tiny copy and loose debris.
Deliver just this single complete enemy, high quality square sprite on genuinely transparent background. No background, floor, shadow on ground, arrows, movement trail, afterimages, motion blur, explosion, watermark, or surrounding UI. The eventual large S movement and trails will be implemented in code, not depicted in this image.
```
