# 中国AI会飞 v1.1.0 测试版

> 更正：v1.1.0 APK 实际为 fullUser（13），并没有锁横屏；此前“横屏”的交付说明未经最终 Manifest 验证。Android 请使用 v1.1.1 修复包，Windows 不受影响。

日期：2026-09-13。

## 发布约定

- 产品名称：中国AI会飞。可执行文件和安装包前缀：ICanFly。
- 旧 PlayerSettings 版本为 1.0；本次统一为 1.1.0，Android versionCode 从 1 升至 2。
- Android 包名保持 `com.DefaultCompany.DeepSleep_Unity6`，不改公司名或清除存档。
- 图标来源：用户本轮提供的 DS 表情图，原图入库 `Assets/_Project/Art/App/AppIcon.png`。Windows 默认图标和 Android adaptive 图标均配置该图。
- 产出统一放在仓库根目录 Releases/v1.1.0，构建产物不入 Git。
- Android 为 APK；Windows 为完整目录和 ZIP，不能只分发 exe。
- 更换 Windows productName 会改变 Unity 默认存档目录。旧 DeepSleep_Unity6 目录不删除；如需继承旧存档，可在两端游戏都关闭时将旧目录的 profile.json/profile.backup.json 复制到新产品目录。Android 包名不变，不受 Windows 目录变更影响。

## 本次修复

前置/额外炮口可以出现在角色移动边界外；此时朝外发射或与区域平行的射线可能不与逻辑矩形相交。旧快照工厂因此拒绝整次开火并报错。

现在：相交的射线仍用原出口距离；不相交的合法射线采用逻辑区域对角线作为有限射程，再乘已有长度倍率。保留实际炮口位置和方向，不移动玩家边界，不分别计算表现/伤害几何；非法非有限输入仍拒绝。

回归入口：DeepSleep.Editor.Diagnostics.LaserBoundaryRegression.Run()。60 组中心/边缘/区域外、五个方向、单/多炮口组合通过，原相交射程保持一致，NaN 输入被拒绝。

本版本还包含上一轮中文局内菜单、单人托管、局域网发现及此前的强化和溅射更新。手机触屏、双机同热点仍需用户实际设备验收，不把构建成功等同于双机联机验收通过。

## 构建核验结果

- Android ARM64 APK 构建成功（非 Development）；用 aapt 核对名称、中国AI会飞，版本1.1.0/code2，原包名保持一致；Wi-Fi/多播权限进入最终清单。
- 用 apksigner 校验 APK 签名有效，且证书与本地旧版 Builds/NetworkTest/DeepSleep.apk 一致，可以覆盖更新。仍为本机 debug 签名的测试分发包，不是应用商店正式签名。
- Windows x64 构建成功（非 Development），app.info 产品名正确，exe 图标提取确认与用户提供图片一致。
- Windows 成品执行 12 秒 headless 启动检查，进程存活并载入场景，无游戏脚本异常；Unity 云诊断端点受网络限制产生 Curl 提示，不作为公网联机已通过的证据。
- Windows 完整目录打 ZIP，IL2CPP/符号备份目录移动到 Builds/ReleaseSymbols/v1.1.0，不随游戏分发。
- Android 构建的 3 条警告为诊断符号配置和 Unity 启动 Logo 的旧 PVRTC 压缩回退；构建错误为0。
