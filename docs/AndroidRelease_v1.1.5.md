# Android v1.1.5 交付记录

- 日期：2026-10-07。
- 游戏源码提交：`7667684c9153ed9015b4b36374e38645873686ad`，已上传 `FuQing556/DeepSleep` 的 `main`。本轮LFS上传18个对象、约23MB。
- APK：`Releases/v1.1.5/DeepSleep-Android-v1.1.5.apk`（本地交付，不加入Git）。
- Unity：6000.6.0f1；APK、非Development、IL2CPP、ARM64，四个原有正式场景。
- 包名：`com.DefaultCompany.DeepSleep_Unity6`；版本1.1.5，versionCode 7；minSdk 26，targetSdk 36。
- 构建作业：`build-753133f744`，2026-10-07 09:21:51–09:25:18（北京时间），206.416秒，Succeeded，0错误、4警告。
- 实际APK大小：195259350字节（约186.2MiB）。构建报告总产物约1630MB包含其他构建文件，不能当成APK大小。
- SHA256：`053D8556BC75EB65CDE1015A1AEE886C6563DF6B93DEE6EB5D22215D40614D26`。
- `apksigner verify --verbose`通过，v2签名、1位签名者；沿用原签名配置（非自定义keystore）。`aapt dump badging`确认上述包名/版本/SDK和`arm64-v8a`。

包含本批Kimi技能/护罩/截束与锁血调整、图鉴快速挑战和居中布局、饰品及编辑器、双方UI和联机反馈改动。两个罩常态80%、命中50%、0.1秒恢复；水平光刃波间通常约2秒；五束激光从上到下。

4条构建警告：Diagnostics Data建议提供SymbolTable/Full符号；GameSceneRouter旧字段未使用（CS0414）；Unity启动Logo的PVRTC压缩已不支持；启动Logo因此使用未压缩版本。未为消除警告改变发布配置。构建成功后Unity另有在线服务证书Curl35日志，不影响已经完成的APK构建/签名验证。

没有启动手机/双端实玩验收，也没有更改用户碰撞/饰品姿态/激光发射点微调。私人`docs/Resume`、Unity本地`Assets/_Recovery`及meta、缓存和构建产物未上传。
