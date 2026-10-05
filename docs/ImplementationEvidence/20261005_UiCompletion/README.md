# 主页、关卡入口与 Buff 生图接入验证

日期：2026-10-05。Unity 6000.6.0f1 编辑器；未构建安装包。

## 修改范围

- 正式素材 16 张：两套主页标题、两套静态背景、10 种现役强化图标、2 种主动技能图标。12 张图标导入 Max Size=512，标题/背景为 2048。
- DS 米粒图标参考实际弹体、命中及护航素材。DataCompression、RiceFan、RiceStorm 使用重画版本；3 张三角海苔饭团旧稿未进入 ready / Assets。[素材、选源与透明检查](../../ArtProduction/20261005_UiCompletion/README.md)。
- 黄昏关卡右侧入口补底板/边框/徽章，保留原整卡 Button 与热区。主页标题等比显示，背景等比铺满裁切。
- 两关共用目录 Icon；商店卡图与已获强化栏使用同一图。DS 6 槽、HS 5 槽，等级用文字角标；PlayerUpgradeHudView 订阅已有 Changed，无运行时装配。
- HUD 高230，强化行 y=-186；商店图标位于既有 UiButtonMotion.Visual 内。临时技能与保护图标读取原 HUD 快照，无新增计时器或网络协议。

## 检查结果及方法

1. 最终 Console Error=0；Editor playing=False、compiling=False。
2. `UiThemeSceneChecks.Run()`：4819 条绑定/属性断言通过，覆盖 4 个预览场景、12 个主题 Canvas、2 个 Prefab。断言数量不是场景或实战用例数。
3. World01、Gameplay_Prototype 的 Play 检查各 50 条断言通过：初始空槽；TryApply 后图标/等级；SetRankFromAuthority 与 NotifySnapshotApplied；RestoreSnapshot 清槽；技能 Active / Cooldown / Downed 与保护图标显隐。
4. 上述检查临时绕过选角画布门并注入 UI 状态，玩法一直停在选角。没有实际购买、通关或联机，不代表章节失败回滚、网络传输或技能实战已经验证。
5. 商店以固定预览钱包显示实际三张候选卡，未调用购买。主题预览只调用 Apply，不写 PlayerPrefs。
6. World01 最终复查再次通过 50 条断言；两 HUD 高230、三张商店图挂 UiButtonMotion.Visual 的检查通过。重复检查不计作新的实战覆盖。
7. 强化目录资产 diff 仅增加 10 个 `_icon` 引用，没有数值变化。

## 截图

- 16:9 主页：[DeepSeek](menu_ds-1.png)、[Harness](menu_ha.png)。
- 2340×1080（19.5:9）主页：[DeepSeek](menu_ds_wide.png)、[Harness](menu_ha_wide.png)。
- [两侧关卡入口](levels_ds.png)。
- 三张强化卡：[DS](shop_buffs_ds.png)、[HS](shop_buffs_ha.png)。
- [天空测试关最终230高 HUD](hud_final_prototype.png)。
- [黄昏关卡最终230高 HUD，Harness 主题](hud_final_world01_ha.png)。

其他同目录 HUD/菜单图片为过程截图；`menu_ds.png` 停在淡入 alpha=.043，不作为最终主页画面。

## 状态恢复与边界

- 两轮 `ProfileIsolation.RestoreAndVerify` 均通过，原 profile 内存引用/JSON、主档与备份 SHA256 完全不变；已退出 Play。
- GameView 恢复原选择。最终活动场景 Boot、dirty=True；未保存内容保留，未保存、未丢弃。另存 Temp 被安全检查拒绝，未执行。
- `UiBuffArtworkInstaller` 只拒绝它会修改的 Gameplay_Prototype / World01_EarlyInternet 未保存状态，不处理无关 Boot 编辑；原活动场景恢复逻辑保留。
- 未改玩法数值、碰撞范围或网络协议，未打包，未推送 GitHub。
- 未做 Android 实机、真实双设备联机、真实购买/通关或 GPU 性能测量；用户最终美术验收尚未完成。
