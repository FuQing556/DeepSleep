# 能量释放与切割预警

2026-10-08。使用deepsleep-art-generation与imagegen、内置image_gen真实透明请求。生产卡：已确认常态v02身份→双掌前推释放→全身悬浮→人物不含球/光效→透明→能量释放动作；橙色空间切割→细裂缝预告→水平长幅→单线而非整屏图→透明→共用布局的预警素材。

释放v01右侧头发贴边退役；v02补边，人物全身完整，双掌前推区别于双手蓄力，头发最右仍边距偏窄，不能直接宣称游戏量级通过。预警水平核心线、较暗橙色边缘与小裂纹，较正式切痕减弱。未做全图Alpha/游戏量级/预警可读性验收；未导入或改Runtime。

## 首版素材缺口

已生成候选：常态、召书捏诀、切割、蓄力、释放、无人机、打开书、切痕/预警、能量核心/下层环/碎裂/放射/逸散。只有常态明确获用户母图确认，其他候选是否满意应以最新反馈为准。

首版还需：雨夜背景（依据当前2066场景构图修改）、权限书破碎、权限识别图标及封禁/解封表现、穿屏提示、转阶段/败退表现、Boss血条/封禁UI、音效。移动/闪避可先用现有常态配代码位移，是否新增专属动作待用户选择，不能声称完整美术已完成。禁止为了数量自行扩展未锁定的引力核/斥力/炮台招式。

## 待确认的初版节奏建议（不是已批准配置）

切割每次先预告1.2游戏秒，伤害闪现0.15秒；二阶段三次各重新预告，不用第一次预告覆盖三次不同布局。能量蓄力2游戏秒后推出一颗；爆炸滞留先用2.5游戏秒、再0.6秒淡出，仅爆炸瞬间造成一次伤害，滞留暂只视觉，避免和封禁移动叠成必死区。权限书1秒真实战斗时间预警，本批最长12秒后自动归还，清空后间隔10秒再抽。上述只是便于开始实战的方案，尚未更改设计的已确认部分或代码。BossHP/阶段点、球HP、书HP须对照现有满Buff输出另算，不在本轮拍数。

## SPR_CL_EnergyRelease_v01_REJECTED.png

参考：raw/SPR_CL_IdleHover_v02_CANDIDATE.png，身份参考

实际提示词：

```text
Same Claude character as the reference, preserving her face, chibi proportions, clothes and anime drawing style. Full-body hovering energy-release action: she pushes both open palms forward toward screen left, elbows still softly bent, body naturally follows the push. Calm determined expression. Legs hang comfortably beneath the skirt, separate and relaxed. Full silhouette with transparent margins. Character alone on genuine transparent background; no energy orb, glow, drone, book or shadow.
```

## SPR_CL_EnergyRelease_v02_CANDIDATE.png

参考：最近一张释放v01，编辑目标

实际提示词：

```text
Keep this exact Claude energy-release pose, face, hands, clothes and drawing. Fit the complete character into a larger transparent square canvas with generous empty space on every side. Restore the very end of the flowing hair at the right so nothing is clipped. Do not redesign the pose. Genuine transparent background, no added objects.
```

## VFX_CL_SpatialCutWarning_v01_CANDIDATE.png

参考：无输入图；延续橙色空间切割视觉

实际提示词：

```text
Use case: stylized-concept. One isolated long thin horizontal amber spatial-fracture warning VFX for an anime game. A precise straight central incision with faint orange double-edge light and a few small angular stress cracks nearby. Calm restrained glow, much thinner and quieter than an actual explosive slash. No bright central burst or large fragments. A single warning line, not a grid or full-screen composition. Wide transparent canvas, full line fits with empty margins at both ends and all glow contained. Genuine transparent background, no scene, text, symbols, weapon or UI panel.
```
