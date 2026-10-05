# DS / Harness 分层 UI 与轻动效验证

日期：2026-10-05。Unity 6000.6.0f1 编辑器；本批没有构建安装包。

## 实际修改

- 用户确认样板方向后分别生成两套共 14 张透明 PNG，不将完整样板当控件贴图。
- 原生 Alpha 检查和裁边依照项目美术/透明技能；仅裁去边缘空白及 alpha=1 的远端噪点，保留裁内 RGBA，不重绘、不整体去色。原图留档。
- Unity API 导入 Sprite 和装配 4 场景、12 主题 Canvas、2 动态卡片 Prefab。183 处程序几何装饰迁移为 Image 层；根控件、业务监听、命中矩形保留。
- UiThemeView 显式换图；UiButtonMotion 管视觉子根缩放/高光/整组禁用透明度；UiPanelMotion 管短淡入。没有引入补间库、运行时组件自动装配或每帧全局查询。
- 源图、精确提示词、导入边界及逐图确认状态见 [美术记录](../../ArtProduction/20261005_UiLayers/README.md)。

## 检查结果

1. 编译检查：本批最终 Console error=0。
2. `UiThemeSceneChecks.Run()`：4806 条绑定/属性断言通过，覆盖 4 预览场景、12 主题 Canvas、2 Prefab。包括非空 Sprite、正确九宫格/保持比例、装饰不拦截点击、显式动效引用和同根 CanvasGroup。数量不是独立玩法用例数。
3. `UiThemePreferenceChecks.Run()`：11 条通过；原 PlayerPrefs 键恢复。实际选角/跨关卡记忆验证沿用前一批记录，本批未改变主题写入入口。
4. 在 MainMenu 的真实“开始游戏”控件上以编辑器回调注入事件，真实 Update 推进；全程 `Time.timeScale=0`，没有调用其业务 onClick。15 项通过：面板初始透明、面板淡入完成、固定热区、鼠标悬停、按压、外来触点不能释放、回弹、回弹归位、触摸所有权、触摸松开无残留高光、键盘选中、键盘提交达到按压值、禁用整组淡化、禁用清高光、重新启用恢复透明度。实测缩放最小 0.97，回弹峰值 1.015。
5. 最终 Play=False；活动场景 Boot、dirty=False；`DeepSleep.UI.ThemeRole` 键仍不存在，与本批前一致。没有购买物品、进入战斗或修改存档。

## 视觉检查

- [DS 主菜单](DS_MainMenu_final.png)
- [HS 主菜单，首按钮悬停](HA_MainMenu_hover.png)
- [HS 关卡卡片](HA_LevelCards_final.png)
- [DS 动态列表](DS_Shop_final.png)
- [HS 动态列表](HA_Shop_final.png)

截图为 1113×626 GameView。手动预览使用 Apply，不写 PlayerPrefs；因此早期 `HA_Shop_layers.png` 中新生成商品卡仍取 DS 偏好，属于预览方法造成的混色，不能作为正式主题行为证据。最终截图在动态实例生成后统一 Apply。`DS_MainMenu_initial.png` 拍到切场景首帧，不作有效最终画面。

截屏工具会暂停编辑器；第一轮恢复播放时曾出现 Unity 内部 PlayerLoop 递归错误提示，停止/重新播放后未复现，本批最终 Console 无错误。未为该工具行为更改游戏业务逻辑。

## 复查入口与边界

- 从 Boot 播放，鼠标悬停/按住/松开主菜单按钮；只视觉子层缩放，文字布局和热区不跳动。
- 选中按钮后可观察轻呼吸；禁用按钮整组淡化；切菜单页观察 0.16 秒淡入。
- Inspector 调 UiButtonMotion / UiPanelMotion 公开参数。美术引用位于 Configs/Presentation/UI 两个主题资产；无需重跑安装器。
- 通用按钮、面板和圆形底板已接入；独立功能图标、完整页面背景和各页最终美术验收没有被本批宣布完成。
- 未做 Android 实机多触点、设备重启、真实双设备联机或 GPU 性能测量。前批宽屏/玩法回归不等于本批新美术已在手机验收。
- 本批未打包、未推送 GitHub；推送仍等待既有具体仓库/范围确认。
