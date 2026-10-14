# HS 悬浮副炮阵列

## 已接入

- 主炮保留；终端阵列改为四级，每级增加一门独立自动副炮。
- 1 门上中，2 门上下中，3 门上两角/下中，4 门四角正方形。
- 2026-10-14：上下左右跟随 HS；每门独立限制在安全区内，贴边允许阵形压缩，不整体平移保形。阵形半高为安全区高度×(0.5−0.28)，四边边距0.5世界单位。
- 节点购买直接按升级等级刷新待机炮口，不等待攻击模拟；不因此在节点开火。
- 待机漂浮半径0.16、速率1.4，呼吸幅度8%；跟随Lerp速率4、归队12（指数换算帧率无关权重）。蓄力/连射锁定世界位置，最后开火再驻留0.26秒覆盖束线淡出，随后归队。实际开火快照与炮口位置共用。
- 副炮不依赖摆烂开关；自主分配目标，真人明确点选时集中。目标失效释放订阅，封禁攻击清除蓄力；沿用现有战斗输入门禁。
- 从各自真实炮口构造束线，复用连锁、挡板截断、伤害和命中特效，不新增伤害通路。束线渲染在角色下层。
- 副炮不会反复切换 HS 攻击姿势，也不会打断主炮蓄力音。
- 四级价格 50 / 75 / 100 / 125 Token；DS 与 HS 全升级总价均为 1340。近战回声仍最多两道，后两级只增加副炮。

## 配置与维护

- `Assets/_Project/Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset`：AuxiliaryVerticalInset / AuxiliaryEdgeMargin及AuxiliaryFollowRate、AuxiliaryReturnRate、AuxiliaryDriftRadius、AuxiliaryDriftSpeed、AuxiliaryShotHoldSeconds、AuxiliaryBreathAmount。
- `HarnessTerminalLaserPresentationConfig`：AuxiliaryIdleAlpha=0.6、AuxiliaryIdleScale=0.85，复用现有炮口素材。
- `HarnessAuxiliaryInstaller.Install()`：定向安装预制体和三个战斗场景中的四个显式炮口引用、升级和网络版本。不要为此运行完整旧升级安装器。
- 协议21，内容版本`20261014-hs-auxiliary-motion-1`；同步四门实际位置，新旧版本不能混房。

## 本轮验证边界

2026-10-14追加：111项布局/价格/引用、2234项协议检查通过。Play节点0/1/2/3/4/2/0等级刷新无战斗命令/无射击；四个真实渲染器立即显示。受控蓄力/开火驻留与Lerp归队检查通过。HS瞄准后退贴图与恢复检查通过；Claude战由动作表现器启用该覆盖。镜头已有SmoothDamp，本轮未修改。未双端设备验收。

- Unity 编译及停止 Play 后控制台无错误；78 项布局、价格和三场景引用检查通过。
- 2202 项网络协议断言通过；尚未双端设备实测。
- Play 内使用已有 404 预制体受控测试：无自动/手动输入时有副炮射击、无主炮射击；封禁后四门停火。
- 手动目标状态受控测试 16 次副炮射击全部命中所指定的目标引用。
- 初版截图 `ArtProduction/20261013_HarnessAuxiliary/previews/FourPorts_Editor.png` 是休息节点内诊断摆放，不代表节点正常会开火。截图发现顶排被 HUD 遮挡，最终高度边距已从 20% 调为 28%；该截图不是最终布局验收图。
- 未打包、未上传、未改永久购买存档；仍需正常战斗及手机布局试玩确认体感。
