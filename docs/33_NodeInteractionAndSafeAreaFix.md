# v1.1.2 节点交互与安全区修正

## 改动

- MainMenu 的 Home 页增加「退出游戏」。手机/PC 构建调用 Application.Quit；编辑器停止 Play。
- 原节点交互/准备依赖 OnTriggerEnter2D/Exit2D 计数。NetworkAuthorityGate 会关闭客户端角色 Rigidbody2D.simulated，客户端无法可靠产生这些回调；对象显隐、网络/托管位移也不应依赖旧计数。
- Open 节点每帧只核对既有热点与 DS/HS 两个根点，不开启客户端物理，不修改热点碰撞体尺寸。位置变换到热点局部空间，使用 BoxCollider2D 的 offset/size 判断；旋转、缩放由 Transform 处理。交互时再次刷新，不能依赖上一帧的缓存。
- 现在以角色根点进入区域作为节点交互标准，主客端一致；不是依据图片边缘擦碰。修改空间范围仍调整现有热点 BoxCollider2D。
- 托管仅决定战斗/移动控制源，不转移强化消费或传送门准备的权限。离开传送门仍取消准备，双方都准备且都在圈内才离开。
- SessionOverlay、ExitConfirmation、ChapterSettlementPanel 的背景变为独立 FullScreenBackdrop 子图片。背景覆盖 root Canvas；其余控件继续使用 SafeArea。保留已有 Canvas 排序，关闭弹窗时背景随父对象关闭，不留下不可见拦截层。

## 已执行检查

- 主菜单实际显示退出按钮，布局截图：menu_quit_112.png。
- 编辑器真实创建房主，再注入 Playing 状态进行隔离回归（非两台真实设备）：关闭两角色 Rigidbody2D.simulated、清空历史触发记录，DS/HS × 托管开/关共四组均产生交互按钮且可 OpenForRole。
- 在上述无物理回调条件下，准备=True，离圈自动取消=True，双方准备后状态进入 Departing。
- 模拟安全区 x=9%..98%，根 Canvas 1327×626，菜单及确认背景角点均为 (0,0)..(1327,626)。截图：wide_safearea_112.png。
- 这些检查覆盖节点空间资格和 UI 层，不等于真实网络延迟、主客端请求传输、手机触控均已验收。需要新版本双机复测用户原流程。

## 定位边界

本轮隔离测试未复现房主端在物理正常时单凭托管开关就永久失效，已修复确定存在的触发记录依赖问题。若新版主机仍有该现象，应收集按钮是否显示、点击后提示和主机日志，继续排查，不能笼统宣称所有联机场景已通过。
