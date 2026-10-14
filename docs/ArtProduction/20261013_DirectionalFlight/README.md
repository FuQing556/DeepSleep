# 定向飞行与角色比例修订

2026-10-13。用户本轮范围：菜单重叠、三外观定向动作、工牌 DS 不切图、Claude 战自由转向、角色大小复核。新手引导与隐私明确暂缓；不打包、不上传。

## 生图与文件

使用内置 imagegen，透明 PNG，1254×1254。原稿保留在 raw，生产资源在 Assets/_Project/Art/Characters/PlayerActions。为保持已有 Sprite/GUID、网络编号及引用，批准修订后的生产图沿用原文件名，版本历史由 raw 保存。

- DS 后退：母图 DeepSeek/SPR_DS_Idle_Base_v01；raw/SPR_DS_Backflight_v02.png → SPR_DS_Backflight_v01.png，PPU 560。
- HS 前飞：母图 Harness/SPR_HA_IdleFly_v03；raw/SPR_HA_ForwardFlight_v01.png → 同名生产图，PPU 560。
- 工牌 DS 后退：以工牌前飞确认服装；raw/SPR_DS_BorrowedBadge_Backflight_v02.png → SPR_DS_BorrowedBadge_Backflight_v01.png，PPU 512。
- 工牌 DS 前飞：raw/SPR_DS_BorrowedBadge_Glide_Hidden_v03.png → SPR_DS_BorrowedBadge_Glide_Hidden_v02.png，PPU 512。

新增动作实际提示要点：保持参考同一人物、头身与服装；DS 后退朝右轻微后仰、双腿自然前伸并捧碗；HS 前飞向右前倾、腿后收、头发尾巴向左飘，保留终端；工牌 DS 后退保留白卫衣橙裙、工牌自然被碗和手遮挡。均为完整全身透明单人 Sprite，无残影、速度线或附加特效。

比例修订实际提示：第一张为动作修改目标，第二张普通 DS 为比例参考；工牌两张头部相对身体缩小约18%，DS后退约15%，自然衔接颈肩头发，保留身体尺寸、动作、服装与饭碗。比例百分比是生图意图，不作为像素级精确测量结论。

另校准工牌 DS 常态/倒地 PPU 560，庆祝/救援575，受击560。保留既有角色根、碰撞、存活/倒地倍率；无非等比拉伸。重新检查三套共28张玩家图，按PPU同世界比例输出对照，不用含尾巴的外包围盒强行统一人物高度。

对照图：../20261012_PlayerActions/previews/scale_audit/{DS,HS,Badge}.png。新图头部锚点随修订调整，其他用户既有锚点保留。原始图片未删除。

## 运行规则

移动相对当前朝向：前进用前飞、后退用后退，停止回本次选中的常态，纯上下移动用前飞。取消随机移动切图及工牌露牌常态门槛。工牌常态露牌随机保留，不因移动重抽。

受击、救援、强化/胜利、HS攻击/近战仍有各自优先级，移动图不会强行盖掉技能动作。Claude 接管战斗后，无显式瞄准时随水平移动转向，攻击瞄准仍优先。非Claude战斗与节点原有规则保留。

## 验证记录

- 三场景动作引用、三外观前后方向选择、透明导入、饰品与网络编号：123项通过。
- Play中受控速度检查：普通DS/工牌DS/HS各前进、后退、停止共9条实际Sprite结果正确；Claude接管后两角色左移面对左且选前飞。该检查直接驱动运行组件，不冒充完整手动游玩。
- 实际启动第二世界、选DS、打开局内菜单；进入休息节点后自动购买出现。menu_final-1.png为战斗菜单，menu_rest_verified.png为节点菜单；早期menu_runtime.png与menu_final.png不是最终验收图。
- 回归：保护24、难度127、辅助瞄准15、近战输入4项通过。Console最后读取无错误，git diff --check通过。
- 截图是编辑器GameView，不是真机截图。未进行本轮双端设备联机、安卓打包或新手引导/隐私实现。
