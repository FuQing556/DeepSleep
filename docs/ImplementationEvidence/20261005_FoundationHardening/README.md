# 2026-10-05 系统基础加固

## 范围与结论

用户在联机问题及系统现状复核后授权继续优化基础，以便后续增加关卡、怪物和机制。本轮处理已确认的跨系统冲突、会话生命周期、短表现寿命和装配防回归；不是重写整个项目，也不表示全部系统、手机性能或历史偶发故障已经验收。

未打包、未提交、未推送；未运行会覆盖玩法数值的旧安装器。Unity 6000.6.0f1。当前协议 **3**，内容版本 **20261005-foundation-1**。

## 确认的问题与修正

### 1. 同方向消息重号

原 `DoubaoEncounterNetworkChannel.Snapshot` 和 `RestNodePrototypeController2D.NETWORK_STATE` 都是 40，且都订阅 `AuthorityMessage`。Session 对每个订阅者重置 reader 后调用，因此豆包快照序号的低字节 0–4 可以被休息节点误读为状态。节点的短消息又会使豆包读取截断。

这是当前开发分支的确定冲突，会错误切换节点/背景/战斗门控；不能据此倒推旧 v1.1.2 的 HS 命中反馈偶发丢失根因。反馈 41 与准备请求 41 在相反方向，不是同一种冲突。

修正：

- `NetworkMessageCatalog` 统一管理 23 个“方向 + 编号”定义。休息节点保留 40，豆包改 47。
- `CoopSessionController` 收、发两端都先校验完整包的方向、长度、关键枚举、布尔及有限数值，再分发；异常订阅者不能中断其他订阅者。
- 节点完整读入后才改状态。豆包完整解析、检查唯一非零 ID 和容量后才更新接收序号/Boss/镜像；先回收消失气泡再租新气泡，避免满池换批丢显示。
- 协议由 2 升 3，旧协议客户端必须拒绝混房。`NetworkBuildRevision` 集中内容版本；9 个旧安装入口不再写回历史版本。版本是显式维护的发布兼容标识，不是资源内容自动哈希。
- 校验仍不等于所有玩法语义都被证明正确；例如奖励合法性、章节推进等由各自系统负责。

### 2. 退出和跨场景清理

NGO 会把根 NetworkManager 放进 DontDestroyOnLoad；业务会话又引用本场景的玩家/UI。旧的选角返回、章节返回、联机菜单返回使用不同清理路径，Offline 单机及直接 StartLevel 存在旧根遗留窗口。

修正：

- 会话在 Awake 显式登记为 `ISceneExitParticipant`，所有运行时场景切换统一走 `GameSceneRouter`。
- 先标记退出，停止收发/控制路由，通知模块清理；调用 transport Stop，等待 `ITransportShutdownStatus` 完成，再销毁会话根，等待实际 OnDestroy，最后异步加载。
- 8 秒等待超时会停止切换、保留原场景暂停并明确报错；不强制带着旧网络对象加载新场景。
- 退出期间持续暂停；新玩法场景仍由选角持有暂停，选角完成恢复 1；菜单加载后恢复 1。旧选角卸载不再覆盖 Router 的暂停。过渡窗口不能选角/开房/加入。
- NGO 停止时注销消息接收；SelectableTransportAdapter 清掉 pending relay 意图，防止迟到回调重新启动已退出会话。
- 正常菜单返回路径已统一；并未实现跨 Unity 场景保留同一在线房间、主机迁移或完整中继异常矩阵。

### 3. 命中表现与节点销毁

- 新 `PresentationLifetime` 让 HS 短命中效果和跳字在出生帧不消耗整帧 deltaTime；下一帧才正常计龄，同帧重复更新不会双计。避免长出生帧在首次渲染前回收短效果。不延长配置寿命，不改玩法时间。
- 这只修正一个确定的短效果风险；不能单独解释“HS 特效和较长寿命跳字同时长期丢失”。
- 热点回调使用 Unity 对象有效性检查，不用 `?.` 绕过已销毁对象的 fake-null；节点禁用清理重叠记录，离开回调和 UI 刷新检查初始化、状态、启用和 UI 存活。独立夹具重现销毁顺序，不破坏真实节点。
- 新增会话的逐消息 sent/received/rejected 计数、最近 24 个控制/连接事件；通道有 accepted/played/duplicate/phaseDrop，表现池有 requested/played/disabledDrop/capacityDrop。正常包不产生逐包文本日志。
- 正常退出时输出 `[NetworkSessionDiagnostics]` 与在线会话的 `[NetworkCombatFeedback] Session closed.` 摘要，无地址、身份票据、玩家输入内容或命中坐标。通道按会话计数、表现池按场景实例寿命累计。异常强杀未必来得及输出退出摘要。
- 跳字池当前仍预热 16、上限 48；旧日志出现过满池，只新增可区分的计数，不用盲目扩池掩盖手机负载。HS 池策略未改。

### 4. 后续内容接入保护

- `NetworkWorldSnapshotChannel` 从固定 Windows/Snakes 改为显式 `EnemyPools[]`。`FoundationHardeningInstaller.InstallAll()` 已通过 Unity API 迁移两张现役玩法场景，并更新协议/内容配置；无手写 Unity YAML。
- `GameplayFoundationAudit` 检查角色与 AI 引用、唯一会话/循环、固定步漏登与重复、敌人池的章节/奖励/网络/权限/感知登记、客户端表现误关、目标漏登记与重复推进、初始 Sprite 目录和层容量。
- `GameplayFoundationBuildCheck` 在以后构建前只读审计启用场景，真正错误阻断、警告报告；`NetworkProtocolBuildCheck` 同样校验协议。不在运行时猜测补组件。
- 不是所有危险形状或特殊敌人都已通用化。现有 AI 障碍接口处理移动圆；长条激光、矩形/旋转机关需要新适配及专项测试。网络 Sprite 审计仅覆盖初始帧，不代表所有动画切帧均已登记。

## 本轮验证记录

均由 Unity 编辑器实际执行，除特别说明外不是人工目测，也不是真实双设备：

| 检查入口 | 结果与边界 |
| --- | --- |
| `NetworkProtocolChecks.RunCatalog()` | 23 类消息、831 断言通过：完整长度/各处截断/尾字节/方向、真实 beam codec、密集豆包格式。首轮抓到 InvalidDataException 未被 IOException 捕获，补独立捕获后复测通过。 |
| `NetworkProtocolChecks.RunCombined()` | 21 项通过；隔离的真实 Session.Receive + 节点/豆包读取器，序号低字节 0–4、不合法/旧 ID/坏包不部分更新。不连接外部网络。 |
| `GameplayFoundationBuildCheck.CheckEnabledBuildScenes()` | 4 个启用场景；Boot/MainMenu 按非玩法跳过，Prototype 881、World01 915 项，各 0 错误/0 警告。只执行门禁函数，没有执行构建。 |
| 审计反例 | 一次性预览中移除两个敌人网络池、把克隆配置降成旧协议/错误内容，准确得到 4 个错误；未保存预览/配置。 |
| `SceneExitLifecycleChecks.Start()` | 16 站通过，真实异步场景加载、NGO 本机房主/连接中退出；逐站核对旧实例销毁、新实例唯一、Singleton、登记数、暂停恢复。同一端口再次起房成功。 |
| `NetworkWriterCatalogChecks.Run()` | 两场景各 6 个真实 writer 包通过 Session 发送校验：双角色、武器状态、2 种敌人、饭团、敌方子弹。临时 inactive 通道借只读对象引用，不伪造完整快照布局。 |
| `CombatFeedbackRegression.Run()` / `RunAiToggles()` | 两场景各 15 / 20 项通过：主客角色、可靠事实路由、托管往返、重复/重连。人工命中事实 + 内存传输。 |
| `PlayerHitFeedbackNetworkChecks.Run()` | 两场景各 25 项通过：角色、AI、坏包、序号回绕/重连/生命隔离。不是双端。 |
| `PlayerHitFeedbackLocalChecks.Run()` | 两场景各 40 项真实非致命扣血回归通过，测试后恢复状态；不含完整致命/倒地/救援链。 |
| `PresentationLifetimeChecks.RunMath()` / `RunPlay()` | 9 项纯计龄断言通过；两场景各验证现役 HS/跳字池出生帧双 LateUpdate，池顺序及状态还原。实测调用帧 dt=0；长 dt 来自纯函数注入，不能说测过手机掉帧。 |
| `RunRestNodeTeardownPlay()` | 两场景各 5 个临时节点案例通过：禁用回调及 prompt/label/button 销毁顺序。 |
| `CompanionNodeGoalChecks.Run()` | 两场景各 27 项通过；目标与准备规则未改变。路径数学、障碍登记 22 项在 World01 回归通过。 |
| `CompanionPortalMotorChecks.Run(false)` | World01 本轮附加回归通过；因未完成选角，solo voluntary 个案明确跳过，不计为本轮全覆盖。之前完整覆盖见 AI 证据文件。 |

编译和上述测试执行后未发现 error；项目仍有旧诊断/安装器使用弃用 Unity 查找 API 的编译 warning。`git diff --check` 无空白错误，另有旧美术工具文件换行提示。本轮新诊断使用当前查找 API。

最终版再次通过 16 站生命周期矩阵；新增日志口后又在 World01 复跑真实 writer、15/20/25 项反馈回归，并确认两种退出摘要确实进入 Unity 日志。此时诊断里的消息 46 rejected=10 是测试主动注入的坏包，不能误认为真实网络异常。最终正常回 MainMenu，session/manager/registered=0/0/0、timeScale=1；随后退出 Play，Boot 场景未变脏，最终读取 error=0。未清除历史日志或保存测试现场。

### 16 站顺序

主菜单清理 → World 单机选角 → 选角返回 → 单机再进入 → 选角后章节返回 → World → 直接切 Prototype → 回菜单 → 联机选角 → DS 本机房主 → 联机菜单退出 → 联机再进入 → HS 同端口房主 → Router 返回 → 客户选角 → loopback Connecting 中退出。

回菜单时 session/manager/registered 为 0/0/0；玩法内为 1/1/1。此矩阵不含真实客人已连接、中继、丢包、手机后台或弱网。

## 下一步仍需完成

1. 下一轮获准统一出包后，Windows/Android 主客交换、真人→AI→真人、密集连锁命中、节点往返、后台恢复及重连的真实双端复测。现有手机旧包不包含本轮修复，不能现在宣布旧症状消失。
2. AI 密集场景的双脑调度、卡住判定与清障选择继续优化；先量实际手机耗时，再定预算。之前桌面单次规划约 3.3ms 不能充当手机性能数据。
3. 新怪物所有动画帧目录、特殊机制网络状态、复杂形状危险适配和完整倒地/救援组合，需要随实际新内容加入对应测试；现有装配审计不是万能行为验证器。
4. 关卡重设计、太阳/云层节点推进、天台节点、耀斑、AI 自动准备等原待办未因“基础加固”自动完成或改变优先级。没有自动消费、自动选奖励或新包。
