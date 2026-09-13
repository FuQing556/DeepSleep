# DeepSleep伤害数字素材批次

日期：2026-09-13。

## 视觉规格

- DS与HS均为白色内芯、深色描边，不能使用普通字体换色替代。
- DS沿用饭团命中与锁定特效：深蓝轮廓、冰蓝液态高光、少量米粒/光点。
- HS沿用激光命中与炮口特效：黑红多层轮廓、白色能量芯、红色碎晶尖角。
- 两套分别绘制0—9与小数点，完整数值由Unity逐位拼接。
- 数字共同使用440px高母画布，并按亮色主体笔画而非外围特效校准。DS主体高260px，HS主体高278px，以补偿HS黑红碎晶更密集造成的视觉收缩；二者主体基线均为370px。两个小数点的实际可见轮廓高度统一为48px，基线为378px。横向画布只保留左右各4px安全边，不携带额外透明占位。

## 生产来源

- `raw/DS_DamageDigits_StyleSheet_v01.png`：参考DS饭团命中与锁定特效生成的0—9原稿，饱和洋红底。
- `raw/HS_DamageDigits_StyleSheet_v01.png`：参考HS激光命中与炮口特效生成的0—9原稿，饱和绿底。
- `raw/DS_DamageDecimal_v01.png`：以上述DS数字原稿为母图单独生成的冰蓝液态小数点。
- `raw/HS_DamageDecimal_v01.png`：以上述HS数字原稿为母图单独生成的黑红碎晶小数点。

生成阶段只负责美术内容，不要求透明背景。所有原稿通过`build_damage_digit_atlases.py`进行本地色键恢复、透明边处理、等比缩放、共同基线对齐与横向裁切。脚本同时输出正式Unity资源、交付副本和天空/棋盘预览；不会重画字形。

## 输出与验证

### 字体编辑入口（2026-09-13小数排版修正）

Unity顶部 `Tools > DeepSleep > 伤害数字编辑器`。DS/HS分别提供显示大小0%—200%、小数点倍率、小数点两侧间距、数字间距以及实际Sprite预览；点击“保存字体设置”保存到两份样式资产。100%保留已校准角色倍率，0%隐藏现有跳字并跳过新跳字出池，200%把整体等比放大两倍。该编辑器属于Unity开发工具，当前没有新增游戏内设置面板。

小数点默认放大至1.6倍，以素材底部基线为缩放中心；两侧使用独立1px间距，不再套用数字之间的-6px负字距。格式化使用InvariantCulture，避免不同设备语言产生逗号而没有对应Sprite。`decimal_spacing_preview.png`为使用正式跳字组件排出的1.1、8.8、10.5、123.4（上DS，下HS）；不是另做一套静态排版。

- Unity正式资源：`Assets/_Project/Art/UI/DamageNumbers/DeepSeek`与`Harness`。
- 透明交付副本：`ready/DeepSeek`与`ready/Harness`。
- 并排预览：`previews/damage_digits_pair_sky.png`、`previews/damage_digits_pair_checker.png`。
- 最新Alpha检查报告：`inspection_core_calibrated/`。全部22张Sprite均为RGBA，四角透明，存在完整0/255 Alpha范围且自动检查无失败项。
- GameView证据：`docs/ImplementationEvidence/20260913_DamageNumbers/damage_numbers_core_calibrated_hs110.png`。

Unity由`DamageNumberSceneInstaller`统一导入为单Sprite、关闭MipMap、使用无损压缩设置，并重建8位池化跳字预制体。DS与HS共用71px布局字高和-6px紧凑字距；DS视觉倍率为1.0，HS因白色字芯较窄、外围碎晶较密，使用1.10最终视觉补偿。数字不随玩家或敌人命中特效倍率变化。
