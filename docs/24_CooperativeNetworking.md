# 双人联机实现与验收

本文件描述 2026-09-09 新增实现，不把代码接入等同于公网、手机真机和完整视觉验收通过。

## 入口

### 2026-09-09 大厅与横屏修订（优先于下方旧入口说明）

启动显示独立的主菜单界面：Single Player → 选角色 → 战斗；Online Co-op → 创建/加入私人房间 → 双方 Ready → 战斗。当前仍预载同一玩法场景，以不透明背景和暂停门隔离菜单，不是已经完成独立场景加载框架。大厅未开始时隐藏战斗 HUD 和触控操作，战斗后中央顶部 Session 可打开会话面板并返回主菜单。

`OpeningFrontEnd` 只管理界面路由，订阅选角和会话事件，不负责网络模拟；`CoopSessionMenu` 负责房间按钮；`CoopSessionController` 保留会话权威。建房失败不再提前完成选角。场景通过 `OpeningFrontEndSetup` 显式装配，没有运行时补组件，未修改用户碰撞体。

Android 仅允许左右横屏，关闭 Portrait 和 PortraitUpsideDown。已验证编辑器主菜单暂停、单人选择后 HUD/战斗恢复、HS 建房暂停及 DS 建房后离开返回主菜单，Console 无错误。截图在 `ImplementationEvidence/20260909_FrontEnd`。实际手机布局/触控及跨设备连接仍待用户真机验收。

本轮构建：Windows 18:51 完成，0 错误/1 警告；Android 18:55 完成，0 错误/3 警告，生成清单为 `userLandscape`。新版 Windows 双进程自动回归通过准备开战、输入快照、倒地复活、托管及退出，日志 `host-lobby-regression.log` / `client-lobby-regression.log`。受限进程的 PlayerPrefs 身份保存仍有降级警告。ZIP 为 `Builds/DeepSleep-Windows-Lobby.zip`；手机包为 `Builds/NetworkTest/DeepSleep.apk`，约100 MiB。此处成功不代表公网或手机真机测试通过。

局域网测试：两台设备使用本轮新版，连接同一 Wi-Fi；电脑 Online Co-op → Host as DS 或 Host as HS；手机 Online Co-op → 地址填电脑 Wi-Fi IPv4 → Join；双方 Ready。保持 Local network (LAN)，不要切 Internet。UDP 7777，不需要 Python relay 或 Cloudflare。Windows 若弹防火墙提示，仅允许所用可信网络；访客 Wi-Fi/AP 隔离可能阻断设备互访。

公网事实：Server/relay 已单独提交并推送（fad7e90）；Render 部署卡在银行卡验证，用户取消。Cloudflare 临时隧道多次在边缘 TLS 握手失败，已停止隧道，未完成公网验收；不应把临时分配的域名当作可用地址。

### 后续大厅易用性（不阻塞本里程碑）

- 房主在 LAN 大厅直接显示并可复制本机局域网地址，不再要求玩家手动查系统设置。
- 手机热点场景明确区分房主设备 IP、热点网关与 `127.0.0.1`；手机开房时让加入方获得可填写地址。
- 两台手机同路由器、手机热点宿主与连接设备分别做真机测试，并识别访客网络或 AP 隔离造成的不可互访。
- 公网模式获得稳定入口后改为分享邀请信息；局域网 IP 与公网房间码继续保持不同语义。

打开原 `Assets/Scenes/Gameplay_Prototype.unity`，运行后点右上角 **Network**。

在开局选角之前建房/加入；已开始的单机战斗不直接改造成网络对局，先点 Leave / Return to Menu 回到开局。这样不会把单机已生成的敌人和联机镜像混在一起。

- LAN UDP：房主点 Host as DS / Host as HS；客人填房主局域网 IPv4，点 Join。两人 Ready 后开始。UDP 7777，Windows 防火墙需要允许游戏接收入站连接。
- WSS relay：离线时点底部传输方式按钮。填写中继地址（公网必须 `wss://`，仅本机允许 `ws://127.0.0.1:8765`）。房主的房间码可留空自动生成，再分享房间码给客人。客人在上方原地址栏填房间码，两端使用同一中继地址。
- Let AI Play / Take Control：把自己的同一角色实例交给 AI，或收回控制。不重建角色，不重置血量、技能和冷却。
- 客人断线：主机继续，客人角色交给 AI。客人用原房间/地址重新 Join；本机重连票据保存在 PlayerPrefs，不写日志。票据不是云账号，不能用于付费鉴权。
- 房主退出：对局终止，不做主机迁移。Leave 会结束传输并重新载入当前场景回到选择入口。

房主必须先成功建房，客人才能加入；不存在的房间会明确拒绝，不自动创建。

## 数据与执行边界

`Runtime/Networking` 不引用 NGO / UTP。SDK 放在单独 `Adapters/Networking/DeepSleep.Networking.Unity` 程序集。

1. `CoopSessionController` 管房间、准备、角色槽位、真人/AI控制权和输入 epoch。
2. `RemoteCommandSource` 去重、限制队列长度、消耗按键边沿一次、输入超时归零。倒地停用分发器时确认并丢弃输入，不能复活后补放技能。
3. `NetworkAuthorityGate` 的场景显式列表停用客人战斗逻辑与角色物理；不修改 Collider 几何参数。
4. 主机仍走原命令分发器、技能、伤害和救援系统。客人不提交伤害值，不做第二次攻击/格挡/复活结算。
5. `NetworkPlayerSnapshotChannel` 同步两人的位置、动作外观、血量、冷却、护盾、救援状态。
6. `NetworkMovementPrediction` 只预测自己角色的位置，收到主机 ACK 后重放未确认输入。与主机 `PlayerMovementMotor2D` 共用 `PlayerMovementStep` 的速度与边界计算。倒地禁止预测；托管和远端角色使用插值。
7. `NetworkWorldSnapshotChannel` 从显式对象池读取敌人/弹体；每次租借具有新生成号，镜像 ID 不复用。客人的 `PF_NetworkEntityView` 只有表现层，没有伤害 Collider、Health、AI。
8. `NetworkWeaponChannel` 用主机激光几何快照、剑气事件及技能状态驱动既有表现器；角色贴图由玩家快照唯一负责，避免两个 Presenter 重复缩放。
9. `NetworkEffectEventChannel` 可靠去重触发命中/死亡等一次性特效。粒子的随机旋转、淡出在本地播放，不逐个粒子传输。
10. `NetworkSpriteCatalog` 使用资源 GUID + localFileID 的稳定哈希标识；不发送本地文件路径。当前内容兼容性由配置版本号校验，修改资源/协议时须同步递增版本。

## 传输与成本取舍

- LAN：NGO 2.13.2 + Unity Transport，可靠输入/事件、不可靠位置快照。
- 公网备选：可自部署 WebSocket 中继，Windows/Android 的 ClientWebSocket；只转发数据，不运行 Unity。WSS 省去双方家庭网络端口映射，但 TCP 丢包会产生队头阻塞，不能承诺与优质 UDP 中继相同的弱网手感。
- `SelectableTransportAdapter` 切换两者，玩法与 UI 不需要绑定某云账号。
- `Server/relay/relay.py` 是独立服务器，不会自动上传或部署。默认只监听本机；Docker/Caddy 示例见同目录 README。
- 当前是私人邀请房间，不是公开匹配/账号平台。需要上线大众用户时还应接入鉴权、配额、监控、滥用防护、费用报警和多实例房间路由。
- 服务器费、域名/TLS配置、真实公网与移动网络验证尚未完成。不能把“付钱”说成上线的全部剩余工作。

## 配置位置

- `Assets/_Project/Configs/Networking/CFG_Network.asset`：UDP端口、协议/内容版本、20 Hz快照、输入超时、队列限制、插值速度。
- `CFG_NetworkSprites.asset`：允许同步的精灵目录。
- 场景 `NetworkSession`：会话、两条传输、各同步通道、主机权限列表。
- 玩家根：NetworkPlayerReplica、NetworkMovementPrediction；引用原移动配置和原 Collider，只读取边界尺寸。
- `UI_NetworkSession`：临时双端房间UI。
- `UI_TouchControls`：安全区内左摇杆、技能/取消按钮、场景触摸瞄准。点敌人锁定，近战按住并拖动瞄准。桌面继续使用已有键鼠输入。`TouchCommandSource.ForceTouchForTesting` 仅供编辑器触摸布局检查。

## 已有测试证据（逐步追加）

- 编辑器 8 项确定性协议检查：量化往返、首包、重复序号拒绝、按键边沿只执行一次、清队列、序号回绕、NaN拒绝、屏幕坐标拒绝。
- `Server/relay/test_relay.py`：真实 WebSocket 双向转发、满房/版本拒绝、原票据重连、不同票据拒绝、房主离开、非法帧与重复房间。
- 最终 Windows 开发构建：0 编译错误；最终运行包完成 WSS 重连与 UDP 弱网双进程回归。
- 两个真实 Windows 进程走 LAN：完成准备、移动同步、主动托管/收回、客人退出后 AI 接管。
- 两个真实 Windows 进程走本地 WebSocket 中继：`Builds/NetworkTest/host-relay3.log` / `client-relay3.log`，完成开局、角色位置、动态敌人、技能与救援状态同步；HS 血量从 3 到 0，再复活为 1。
- 自动抓取实际相机画面后，能看见双方角色、404、护航圈和救援特效。隐藏窗口直接 ScreenCapture 是黑屏，已改为开发专用离屏相机请求。离屏首次创建 URP 会报告被剥离的未使用后处理 Shader 警告，不能将其记为“运行零警告”。

## 本机已签收的项目

- Windows 中继断线回归：`host-rejoin.log` / `client-rejoin.log`，客人主动模拟连接中断，主机转 AI，1.5 秒后原票据重连成功；血量保持复活后的 1，不重置。倒地阶段客人位置与主机一致，主机 ACK 继续前进，旧输入不积压。
- Windows 角色互换：LAN 的 `host-reverse.log` / `client-reverse.log`，房主 HS / 客人 DS 完成准备、移动、托管/收回及断线重连。
- Android IL2CPP 开发包 `Builds/NetworkTest/DeepSleep.apk` 已构建成功（0 错误、4 条资源/诊断警告）。第一次失败来自含中文的 Unity 工具链路径，三条外部工具路径统一映射到纯英文目录后通过；这证明 Android 代码与打包链可用，不等于 Android 真机联机已经验收。
- 最终 WSS 双进程回归：连接、准备、移动与世界快照、HS 倒地/复活、断线转 AI、原身份重连恢复真人、主动离开均通过。日志为 `host-final.log` / `client-final.log`。
- 最终 UDP 弱网回归：通过本地代理注入双向约 60ms 延迟、±20ms 抖动和 3% 随机丢包，连接、移动、倒地/复活与退出均完成。日志为 `host-weak-final.log` / `client-weak-final.log`。
- 受限环境无法写 PlayerPrefs 时，重连身份现在退化为进程内票据，不再让联机初始化崩溃；同一运行中的断线重连仍有效，但退出应用后不能保留席位。

Android 真机触摸/后台行为、跨设备网络、公网 WSS/TLS、长时间弱网/性能/带宽压测、所有技能逐帧视觉一致性均需要继续验收。热更新、章节节点快照、升级卡/掉落等尚未存在的玩法，不在此次现有战斗联机适配中伪造实现。

当前序列化仍有逐包分配，不能声称 GC=0；私人双人原型已有限队列和包长，商用品质必须实测高密度弹幕负载后优化。

### 本轮环境事项

目录联接 `D:/UnityAndroidToolchain_6000_6` 指向原 Unity 安装的 `Editor/Data/PlaybackEngines/AndroidPlayer`；Unity 的 JDK、SDK、NDK 外部工具路径均改到该纯英文入口。没有移动或删除原工具链。此机器设置不在 Git 中；换电脑时若 Unity 安装目录含非 ASCII 字符，应重新建立英文入口，或直接把 Unity 安装到纯英文路径。

NGO 自动生成资源的位置已改为正式配置目录 `Assets/_Project/Configs/Networking/DefaultNetworkPrefabs.asset`，不保留根目录第二份空资产。构建生成的 PerformanceTestRun JSON、临时 `.utmp` 以及 URP 构建时缓存改写均已清理；当前改动仍未提交、未推送。

联网期间开启 `Application.runInBackground`，离开后恢复原值；`CoopSessionController.AutoTakeoverOnFocusLoss` 控制切后台/失焦时自动托管，默认开启。移动操作系统仍可能强制挂起房主进程，不能用该选项承诺房主后台不断线。

开发测试只在 Development Build 且显式 `-ds-network-host` / `-ds-network-client` 参数下启动，不影响普通运行。可追加 `-ds-relay-test`、`-ds-reconnect-test`、`-ds-reverse-roles`。构建产物与日志留在被 Git 忽略的 Builds；未自动推送 GitHub。
