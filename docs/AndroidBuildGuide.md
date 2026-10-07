# 安卓打包指南（2026-10-07）

工程：`D:\Unity Work\DeepSleep_Unity6`。用 Unity **6000.6.0f1**，不要打开旧工程 `DeepSleep`。

## 最短操作流程

1. 退出 Play，保存已修改的场景/Prefab，等 Unity 导入、编译结束；Console 不应有编译红错。
2. 打开 **File → Build Profiles**，选择 Android。沿用当前配置：APK（不勾 Build App Bundle）、非 Development Build、IL2CPP、ARM64；不要临时更换后端或清缓存。
3. 检查构建场景只有这四个，且 Boot 排第一：`Boot`、`MainMenu`、`Gameplay_Prototype`、`World01_EarlyInternet`（均在 `Assets/Scenes/`）。
4. Player Settings 中更新版本号及 Android Bundle Version Code（必须递增；最新已交付 **1.1.5 / 7**，下轮不能重复使用此code）。不改包名、签名或 SDK 设置。
5. 点 **Build**，输出到 `Releases/v<版本>/DeepSleep-Android-v<版本>.apk`。等构建完成，检查最终成功/失败信息。不是 Build And Run，不必连接手机。
6. 检查 APK 包名/版本/ARM64及签名，记录文件大小和 SHA256，然后交付文件。是否安装设备、实玩或上传 GitHub，另按用户指令。

构建门禁会自动检查场景装配；发现错误就报告具体错误，不要为了出包禁用门禁，也不要顺带扩展成整轮功能开发。用户明确延期的玩法/双端验证不属于每次打包必跑步骤。

## 为什么上次等了那么久

上次构建作业从 `2026-10-06 14:10:37.916 UTC` 到 `14:16:26.372 UTC`，**实际构建约 5 分 48 秒**。这段包括 SDK 检查、IL2CPP/C++ 编译、Gradle 和 APK 合并签名；另有构建前排查旧检查器漏识别 Kimi 自有对象池的时间，不能全部算成“打包”。

IL2CPP 编译和资源导入有缓存，保留缓存通常会缩短后续构建，但不是固定时长。首次构建、切平台、换 Unity/工具链或清 `Library` 都可能明显更慢。不要每次重装 SDK、清 Library、跑全套压力测试或反复发起构建。

Unity 构建时主线程忙，MCP 状态查询可能超时；**超时不等于构建失败**。查看编辑器进度或 `C:\Users\Administrator\AppData\Local\Unity\Editor\Editor.log`，等待当前作业，别立即再启动一份。

## 给其他 AI 的执行指令

> 仅打安卓 APK，不开发新功能，不运行本轮已延期的玩法/双端测试，不上传。打开 DeepSleep_Unity6，使用 Unity 6000.6.0f1，退出 Play、保存并等编译。保留现有 APK/IL2CPP/ARM64、包名和签名，递增版本号及 code。只打包启用的四个正式场景，沿用构建门禁，输出到 Releases/version。MCP 超时先查看 Editor.log，不重复启动构建。完成后检查 APK 签名、包名、版本、架构，报告大小、SHA256、构建警告和文件路径。遇到新权限或需要改变配置的情况先问我。

MCP 构建入口（先读取 `mcpforunity://custom-tools`）：

```text
manage_build: action=build, target=android,
output_path=Releases/v<版本>/DeepSleep-Android-v<版本>.apk,
development=false
```

现有工具链在 `D:\UnityAndroidToolchain_6000_6\`（SDK / NDK / OpenJDK）。不要重新下载。构建后可在 PowerShell 检查：

```powershell
$apkPath = 'D:\Unity Work\DeepSleep_Unity6\Releases\v<版本>\DeepSleep-Android-v<版本>.apk'
& 'D:\UnityAndroidToolchain_6000_6\OpenJDK\bin\java.exe' -jar 'D:\UnityAndroidToolchain_6000_6\SDK\build-tools\36.0.0\lib\apksigner.jar' verify --verbose $apkPath
& 'D:\UnityAndroidToolchain_6000_6\SDK\build-tools\36.0.0\aapt.exe' dump badging $apkPath | Select-String '^package:|^sdkVersion:|^targetSdkVersion:|^native-code:'
Get-Item -LiteralPath $apkPath | Select-Object Length, FullName
Get-FileHash -LiteralPath $apkPath -Algorithm SHA256
```

上次 SDK 下限 26、目标 36；本指南记录现状，不要求每次为打包升级 SDK。工具链或签名不可用时应停下报告，不能更换签名硬出包。
