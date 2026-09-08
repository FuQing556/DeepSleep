# 角色、风格与素材语义

所有工程路径相对工作区根。以下是参考入口，不是将当日资源永久锁死；用户明确认可新母图后更新入口，不凭最大版本号或文件修改时间推断已通过。

## 最高优先级是实际已确认 Sprite

| 对象 | 当前参考入口 | 用法 |
| --- | --- | --- |
| HS / Harness / 旧命名 HA | `Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png` | 全项目角色画风、头身与 HS 身份母版 |
| HS 现有激光姿态 | `Assets/_Project/Art/Characters/Harness/SPR_HA_LaserFire_v01.png` | 同一人物动作、透视参考；不要仅因用户口称“瞄准”另找不存在的文件 |
| HS 睡眠 | `Assets/_Project/Art/Characters/Harness/SPR_HA_DownedSleep_v01.png` | 睡眠造型及对应状态大小比较 |
| DS 常态 | `Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png` | 蓝白身份和已更新头部比例；同名内容可能更新，实际看图 |
| DS 睡眠 | `Assets/_Project/Art/Characters/DeepSeek/SPR_DS_DownedSleep_v01.png` | 已确认睡眠原图；只抠图时绝不重新生成 |
| HS 近战上挑已确认候选 | `docs/ArtProduction/20260908_HarnessQuantumSword/raw/HA_MeleeUpCommand_v02.png` | 2026-09-08 用户明确认可，仅作此动作来源，不替换身份母版 |
| 其他 AI 角色 | `docs/AiSister/README.md` 中对应条目 | 只读当前角色条目并实际查看相关已确认图，不加载全部人物设定 |

HS 是聊天中的简称，现有文件仍可为 Harness/HA；不因此重命名工程 API、目录或 GUID。DS 与 HS 都是虎鲸拟人小女孩，不是须鲸，不从官方鲸鱼图标推导新人体或另画写实鲸落。官方图标仅说明梗/原型；已有导入人物才决定形象。

## 统一的是视觉语言，不是复制姿势

- 日系二次元 Q 版，精致可爱、干净明确的轮廓、分层赛璐璐阴影和克制柔光；以 HS 母图为准。旧文字约 2.4–2.8 头身只作描述，不拿尺子强扭姿态，不强行套旧 320px 线宽。
- DS 蓝发蓝白服装、饭碗饭粒元素；HS 黑长发、红眼、细框眼镜、鲸鳍耳、黑尾浅色腹面、黑白女仆装和红花细红纹。保持母图的配件数量/结构，不因过长列举另造装饰。
- 全身飞行姿态与天空横版场景契合，无站地姿态、鸟翼和地面投影；睡眠状态例外但仍漂浮。新 AI 敌人按自己的设定，可以借助平台/机械/悬浮物，不强制全部会飞或全部是鱼。
- 头大小、躯干粗细与四肢风格要相近，但 DS 竖向悬浮与 HS 前倾飞行的高度/宽度天然不同。不要为了总包围盒一致把一个角色压扁或缩小一圈。
- 裙子延续母图黑/蓝裙、白围裙、蕾丝与细纹；自然飘动、不突增多层褶裙，不掀大裙摆抢走动作重心。手臂与衣袖必须接得上肩膀。
- 睡眠用可爱自然的睡姿，不混搭 X 眼、惊悚残肢和兽化。状态图应能读出用途，不能只换眼色就冒充完整新动作；但用户明确只改光效时，也不得扩大改动范围。

## 特效参考要实看，不能仅记“红色”

HS 风格参考：`Assets/_Project/Art/VFX/Harness/Laser/` 内 `VFX_HA_LaserMuzzle_v01.png`、`VFX_HA_LaserHit_v01.png`、`TEX_HA_LaserBeamBody_v01.png`、`TEX_HA_LaserOverclock_v01.png`。只选本次所需图查看。

- HS：黑红量子/科技风、红色碎晶、白色高亮能量芯、深色结构与细节，近战剑与剑气沿用这套语言。不是普通玩具红棍或另一个魔法体系。
- DS：`Art/VFX/DeepSeek/Targeting/VFX_DS_TargetReticle_v01.png` 与 `Art/VFX/DeepSeek/Rice/VFX_DS_RiceHit_v01.png`（前缀均为 `Assets/_Project/`），蓝白米粒、检索角标。清晰可爱，不硬贴 HS 红黑晶体。
- 机械蛇：红色攻击光效；橙金金属不随眼灯一起换色，死亡熄灯不代表涂黑整个机身。
- 404：漫无目的漂流的软件窗口碎屑，不强加生物设定；撞击与击毁反馈需可区分。
- 剑/晶体等含不透明黑色结构的素材不能作为“纯发光图”去黑；光晕要柔，结构仍需可见。

## 当前 HS 近战组合

近战待机 + 下劈/上挑/横劈三个指挥动作；人物与剑在 Unity 内组合，剑按轨迹平移/旋转/等比缩放，独立更大剑气负责视觉。人物可用手势控制悬浮剑，不强求握住长剑，不为把武器塞进画布挤压人体。动作需有区别，但具体构图保持自由、解剖连贯。当前帧动画计划暂停，不能重新加载用户撤回的第三方动画/骨架约束包。
