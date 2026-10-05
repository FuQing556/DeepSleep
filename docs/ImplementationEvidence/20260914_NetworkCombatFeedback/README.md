# 联机 HS 命中特效与伤害数字修复

日期：2026-09-14。按用户要求仅修改工程，不构建、不发布新包，随下一轮大更新打包。

> 最新状态：已修正代码中确认存在的反馈同步缺口，但用户报告的“仅手机联机异常，电脑房主/客人均正常，手机单机正常”尚未定位根因，不能标记该实机问题已解决。

### 后续现场修正：电脑客人也复现，用户怀疑 AI 托管

- 手机 USB 调试已连接，设备 vivo V2520A / iQOO Neo11。已安装包确认 versionName=1.1.2、versionCode=4，最后更新 2026-09-13 18:49:09。未安装新包。
- 手机本次 22:42 启动后，用户先表示反馈恢复正常，随后在电脑复现，并确认电脑是客人，怀疑发生在 AI 托管期间。先前“仅手机异常”的观察已被本次复现修正，不能继续按纯 Android 故障定性。
- 电脑现场日志来自 v1.1.2 EXE，没有对应的托管/激光异常。手机日志在 22:44:17.998 有 `RestNodePrototypeController2D.SetActionButtonActive -> RefreshNearestPrompt -> NotifyExited` 空引用；调用栈不在反馈或托管路径，单独记录，不将其直接定为本问题根因。
- 旧包未记录逐次托管状态、命中和表现事件，现有日志不能还原准确触发顺序。手机更早的异常日志未找到，读取到的 Unity 日志始于本次启动。
- 审查确认：`SetLocalAi/BindSources` 更换命令来源，`ReleaseControl/ResetIntent` 清理 AI 意图，不直接禁用命中特效；客人托管仍由房主 AI 驱动、房主结算。故此次客人反馈缺失与已经修正的同步缺口一致，但 AI 是否独立触发因素尚未证实。
- 新增 `CombatFeedbackRegression.RunAiToggles()`，在测试关、World01 各通过 20 项回归（共 40 项）：真实 `SetLocalAi`，客人控制包经主机会话 `Receive`，实际 `BindSources` 验证真人/AI 来源，反复关闭/开启/关闭托管，验证房主本地及客人收包后的激光命中特效与数字池实例。
- 检查仍使用内存传输和注入成功命中事实，不等于旧 EXE 进程内状态检查或实机修复验收。此次只扩充诊断回归，没有另改 AI 行为，没有新包；Console 0 error、0 warning，已退出编辑器 Play。

## 补充调查：按平台复核

- 用户明确：电脑无论房主还是客人都正常；手机无论房主还是客人都丢失；两端单机均正常。先前的客户端权威解释不足以覆盖这些现象。
- 本地保留 APK：`Releases/v1.1.2/Android/ICanFly-v1.1.2.apk`，aapt 读取 versionName=1.1.2、versionCode=4，文件时间 2026-09-13 18:44:16。尚未确认手机实际安装的是此文件。
- 本机当前/上一份《中国AI会飞》Player 日志明确指向 `Releases/v1.1.2/Windows/ICanFly_Data`，可确认近期电脑运行的是该 EXE 包，而非仅编辑器。
- 用 Cecil 比对 Windows 包内 Runtime DLL 与 Android 同次构建保留的 Managed Runtime DLL，在 Networking、DamageNumber、HarnessLaserHit、TerminalLaserDamage/Hit 相关顶层类型中比较 266 个方法指令。仅 `LanRoomDiscovery.Update/Open/Close` 三处不同，涉及 Android Wi-Fi 锁/平台异常处理；检查到的命中与数字方法一致。这不等于证明两端场景数据、渲染后端或运行时状态一致。
- 电脑上一份日志中有 6 次“跳字池已满，本次只省略表现”。该证据只说明电脑曾发生跳字容量溢出，不解释手机现象，也不能解释 HS 命中特效同时消失；未盲目扩池。
- 计时候选：HS 特效持续 0.18 秒，播放当帧 LateUpdate 即累加整帧 deltaTime，长帧可能导致首次渲染前结束。但跳字持续 0.68/0.74 秒，项目 maximumDeltaTime 为 0.33333334；因此不能把一次长帧解释为两者必然同时消失。未据此改动时序。
- `adb devices -l` 未发现设备，当前无法采集手机 Unity 日志、核对安装版本或复现手机房主状态。已询问用户具体版本/连接条件。
- 本次补查没有再改运行时代码、没有新建包。下一步应在旧手机包现场复现，核对逻辑主机身份、异常、命中触发和显示状态后决定修复；不能用编辑器注入测试代替这一步。

## 已确认原因

- Gameplay_Prototype 的 `NetworkAuthorityGate.AuthorityOnly` 包含 `HarnessLaserHitEffectPresenter2D`，客人进入联机后该组件被禁用。World01 同步修正装配。
- `NetworkWeaponChannel` 传递激光束体和技能状态，没有传递成功命中信息。
- 跳字只订阅本地饭团、激光、近战伤害来源；客户端的伤害执行器按权威规则停用，因此不能产生这些本地命中事实。
- 通用 `NetworkEffectEventChannel` 仅支持 `OneShotSpriteEffectPool2D`，不覆盖 HS 专用红晶命中池与伤害数字池。

用户另报告手机当房主时也有丢失。本次代码排查确认上述缺口；未取得手机日志或复现手机房主画面，不能断言不存在额外的设备端问题。

## 修改

- 新增 `NetworkCombatFeedbackChannel`，用独立可靠消息 41 传递成功命中的位置、方向、金额、宽度和反馈类别；按序列去重，开关会话时重置。
- 房主保留原有本地表现订阅，只额外发送消息；客户端直接调用表现接口，不重新查碰撞、不造成伤害、不依赖目标 Hitbox 仍存活，也不回发事件。
- 覆盖 DS 饭团跳字、HS 激光跳字与红晶、HS 近战/剑气跳字及命中红晶。
- 用 `CombatFeedbackSceneInstaller.InstallAll()` 显式装配 Gameplay_Prototype 和 World01，并从 AuthorityOnly 移除相关表现组件。原联网安装器的筛选也排除这些表现类型。
- 不修改既有武器数值、伤害算法与激光几何，不构建 APK/Windows 包。

## 实际验证

Unity 6000.6.0f1，从 Boot 进入 MainMenu 初始化服务，配置在线启动上下文后依次加载两个游戏场景。

每场景运行 `DeepSleep.Editor.Diagnostics.CombatFeedbackRegression.Run()`，均返回 PASS，各 15 项检查：

- 房主选择 DS 和 HS 时，激光本地显示 1 个命中特效和 1 条跳字，同时发送 1 条可靠消息。
- 饭团、近战跳字与近战命中反馈进入各自现有表现池。
- 真实 AuthorityGate 切换客人后，伤害执行器关闭，命中特效与跳字组件保持开启。
- 内存传输截获主机数据，通过真实 `CoopSessionController.Receive` 分发到客户端，出现对应的特效和数字实例。
- 重复/旧包不重复播放；客户端不回发。
- 重连后允许新的序列从头开始；客人退出并切回房主后，本地订阅恢复且无重复表现。
- 离线本地反馈仍播放且不发网络消息。

两场景执行后的新鲜 Console 均为 0 error、0 warning。完成后退出 Play，回到原 Boot 编辑场景。

边界：以上是编辑器 Play 中的事件路由与表现池回归，测试注入成功命中事件，不替代真实激光碰撞测试、双设备传输测试或 Android 渲染验收。未构建新包，未进行手机实机复测。

## 下一轮大更新打包时

使用包含本修复的同版本两端，覆盖手机房主/电脑客人、电脑房主/手机客人，各自选择 DS/HS，验证激光贯穿与致死命中、近战/剑气和饭团跳字。尤其检查用户报告的手机房主画面；若仍丢失，采集当次手机日志和画面进一步定位。更新包才会包含此次修复，已安装旧包不会改变。
