# 角色动作尺寸检查（2026-10-13）

同日追加：三套玩家共28张图已重新按PPU检查。工牌前飞/后退及DS后退用生图修正偏大的头部，工牌其余姿态做等比导入校准；最新玩家对照图已更新。详见ArtProduction/20261013_DirectionalFlight/README.md。下方Boss观察保留，不表示本轮替换了Boss动作。

本轮检查现有43张角色贴图：DS 6、HS 11、借来的工牌DS 8、Kimi 10、Claude 8。按导入PPU绘制同世界单位比例对照图，而非把原图外框统一拉伸。对照图见 `ArtProduction/20261012_PlayerActions/previews/scale_audit/`，生成脚本为该批目录中的 `MakeScaleAudit.ps1`。

## 结果及边界

- DS、HS大多数动作视觉体量接近，但不能保证所有姿态逐像素一致。倒地、挥砍姿势的外包框不同不应直接判成大小错误。
- 工牌DS前飞v02相比旧版比例已改善；倒地两个版本及庆祝姿势的头部观感仍值得进一步校准。
- Kimi的TideRelease头身比例偏小最明显，MoonCast与LaserRelease也比Idle显小。只放大整张图会连同身体、衣裙放大，不能解决头身比例。
- Claude的EnergyRelease整体体量及头部偏小；ScreenCutCast也存在头部观感偏小。需与IdleHover、Move对照修订。
- 以上是视觉审核发现，不是已完成重绘或尺寸修复。未改碰撞体、飞行器、用户手调偏移及饰品旧锚点。

## 运行时因素

源图检查不等于所有实战姿势验收。DS正常与倒地存在既有表现缩放；HS近战配置另有Idle 0.95、Down 0.92、Up 0.94、Sweep 0.96的姿势倍率。本轮保留这些参数。Kimi和Claude的SetPose复用SpritePoseTransition，不能假定运行时会自动校准头身比例。

## 同轮UI

装扮、鲸元券商店、成就增加右侧竖向滚动条，复用选关横条及主题素材。MainMenu场景已保存；三页运行时Scrollbar值0/1分别推动内容至底/顶。截图见 `ArtProduction/20261011_PlaytestUI/previews/*vertical_scrollbar*.png`，以verified装扮截图及商店、成就截图为准；早期装扮截图捕获了淡入中间态，不用于验收。未打包或上传。

## 仅预览手机布局

当前Unity 6000.6.0f1已有Device Simulator，无需下载安装。打开 `Window > General > Device Simulator`（窗口 > 常规 > 设备模拟器），选择手机型号并旋转横屏，从Boot场景点击编辑器顶部播放。若开始播放后切回Game标签，再点模拟器标签。仅切Android构建目标不会把Game窗口自动改成手机模拟器。

普通Game窗口也可自定义横屏分辨率查看比例，但不等于手机输入模拟。项目TouchCommandSource提供ForceTouchForTesting；其触控Canvas在Awake初始化，不能假定进局后临时勾选就完成初始化。正式场景不应为预览永久强制触控。

Build生成安装包，Build And Run还会安装并启动设备，两者都不是本次所需。模拟器用于布局、安全区、基本触控检查，不代替手机性能与联机验收。
