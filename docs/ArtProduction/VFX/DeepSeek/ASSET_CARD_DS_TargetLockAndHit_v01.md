# DeepSeek 锁定与命中特效生产交付卡 v01

## 状态

- 当前状态：`APPROVED / INTEGRATED`；用户已确认视觉稿，已进入正式 Art、Config、Prefab 与运行时链路。
- 生成日期：2026-09-08
- 来源/作者：OpenAI 内置 ImageGen；Codex 依据项目现有 DeepSeek 角色、饭团弹与同类战斗 VFX 制作提示词并检查输出。
- 许可记录：项目自产 AI 候选素材；参考图均来自当前项目，没有直接嵌入第三方原图。

## 生产前核对卡

- 角色身份：DeepSeek 蓝白虎鲸娘；本批只画角色专属 VFX，不重画人物。
- 生物原型：虎鲸；本批特效不绘制动物或人物轮廓，只继承蓝白、米饭与检索语义。
- 最高视觉母版：`Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png`。
- 攻击参考：`Assets/_Project/Art/Combat/Projectiles/DeepSeek/SPR_DS_RiceProjectile_v01.png`。
- 主配色：白、天蓝、钴蓝、青色辉光；不得使用敌方危险红色，不以紫色为主。
- 空间状态：锁定标记覆盖敌人中心；命中特效固定在成功伤害的世界命中点。
- 玩法范围：两者均为纯表现；锁定标记不改变索敌范围，命中特效不改变弹体、碰撞体或伤害范围。
- 遮挡规则：两张素材均为独立透明 Sprite；中心目标和敌人轮廓必须保持可读。
- 运行时变化：锁定标记由 Transform 做轻微旋转/呼吸；命中特效做短时旋转、缩放和 Alpha 淡出。

## 候选 A：手动锁定标记

- Asset ID：`VFX_DS_TargetReticle_v01`
- 文件：`CANDIDATE_VFX_DS_TargetReticle_v01.png`
- 画布：1254 × 1254 px，RGBA 32 位真透明。
- Alpha≥8 可见范围：X 32–1223，Y 24–1218；可见尺寸 1192 × 1195 px。
- 构图：四个圆润角标、蓝白检索环、少量白米粒轨迹与数据刻度；中心透明。
- Facing：无方向性，中心对称。
- Frame Count/FPS：1 帧；运行时动画，不使用 Sprite Sheet。
- Unity 基础世界直径：1.45 units，叠加 8% 呼吸；旋转 72°/s。
- 正式文件：`Assets/_Project/Art/VFX/DeepSeek/Targeting/VFX_DS_TargetReticle_v01.png`。
- Unity 导入：Sprite/Single、PPU 256、Pivot Center、Bilinear、Mip Maps Off、Alpha Is Transparency On、Compression None、Gameplay/Order 60、无 Collider。

## 候选 B：饭团弹命中特效

- Asset ID：`VFX_DS_RiceHit_v01`
- 文件：`CANDIDATE_VFX_DS_RiceHit_v01.png`
- 画布：1254 × 1254 px，RGBA 32 位真透明。
- Alpha≥8 可见范围：X 227–1044，Y 166–1026；可见尺寸 818 × 861 px，四周保留充足淡光空间。
- 构图：白色四角星核心、青蓝软冲击环、圆润能量瓣与少量飞散米粒。
- Facing：无方向性，径向冲击。
- Frame Count/FPS：1 帧；运行时旋转、缩放、Alpha 淡出。
- Unity 透明画布基础世界直径：0.64 units；可见 Alpha 主体约占画布 65%，属于“玩家产生的战斗特效”，继续响应 50%–500% 可访问性倍率。
- 正式文件：`Assets/_Project/Art/VFX/DeepSeek/Rice/VFX_DS_RiceHit_v01.png`。
- Unity 导入：Sprite/Single、PPU 256、Pivot Center、Bilinear、Mip Maps Off、Alpha Is Transparency On、Compression None、Gameplay/Order 60、无 Collider。
- 正式 Prefab：`Assets/_Project/Prefabs/Combat/VFX/DeepSeek/PF_VFX_DS_RiceHit.prefab`。
- 表现配置：`Assets/_Project/Configs/Presentation/DeepSeek/CFG_VFX_DS_RiceHit_Default.asset`；0.20s 播放、0.45→1.20 缩放、25°–70° 随机旋转、预热 8、上限 24。

## 技术检查

- 两图均为 `Format32bppArgb`，抽样 Alpha 范围 0–255，背景存在真实 Alpha=0 像素。
- 没有文字、Logo、人物、碗、方形面板或场景背景。
- 锁定标记中心留空；不会把敌人本体盖死。
- 命中特效轮廓比 Harness 黑红激光命中更圆、更轻，符合 DeepSeek 高频饭团火力。
- 静态装配校验已通过：锁定视图、命中特效池、事件表现器、贴图导入和 Gameplay/60 排序均有效；Play Mode 启动 0 错误。

## 运行时职责闭环

- `DeepSeekTargetLockView2D` 只读取现有手动锁定结果；没有新建物理搜索，也不修改朝向或开火。
- `RiceProjectile` 仅在 `DamageHitbox2D.TryReceiveDamage` 返回成功后，上报 `RiceProjectileHitConfirmed`。
- `RiceProjectilePool` 负责集中转发命中事实；`DeepSeekRiceHitEffectPresenter2D` 将事实交给通用一次性 Sprite 对象池。
- 普通饭团每次成功伤害各播放一次；碰到障碍但未造成伤害时不播放。未来大饭团的范围爆炸使用独立结算与独立表现，不污染本链路。

## 最终提示词

### Target Reticle

```text
Use case: stylized-concept
Asset type: production candidate for a 2D Unity game target-lock VFX sprite
Input images: DeepSeek character style reference only; DeepSeek rice projectile palette/glow reference; Harness reticle clarity/spacing reference only
Primary request: create one original DeepSeek manual-target lock marker, centered and symmetrical, readable around a small enemy in a side-scrolling shooter
Subject: an airy circular search ring with four rounded corner brackets, a few tiny white rice-grain orbit marks and subtle data-search ticks; the middle must stay mostly empty so the enemy remains visible
Style/medium: polished chibi-anime 2D game VFX, crisp clean silhouette, soft luminous rendering consistent with the provided DeepSeek art
Composition/framing: single centered square sprite, generous transparent padding, circular footprint, no cropping
Color palette: white core highlights, sky blue, cobalt blue, cyan glow; no red, orange, or purple-dominant palette
Constraints: genuinely transparent background; no character, weapon, text, letters, numbers, logo, watermark, panel, square frame, black backdrop, checkerboard, scenery, or opaque background; suitable for runtime rotation and pulse scaling; avoid excessive tiny detail
```

### Rice Hit

```text
Use case: stylized-concept
Asset type: production candidate for a 2D Unity game enemy-hit VFX sprite
Input images: DeepSeek rice projectile palette/rendering reference; Harness hit polish reference only; existing enemy-hit readability reference only
Primary request: create one original compact impact burst for a DeepSeek rice projectile hitting an enemy
Subject: bright white four-point impact star at the center, a soft cyan-blue circular shock ring, several short rounded energy petals, and a small number of tiny white rice-grain fragments radiating outward
Style/medium: polished chibi-anime 2D game VFX, clean painterly sprite, soft yet punchy, matching the DeepSeek projectile
Composition/framing: single centered square sprite, radial impact, generous transparent padding, no cropping, strong readability when reduced to a small high-frequency hit effect
Color palette: white center, sky blue and cyan energy, cobalt-blue outline accents; no red, orange, green, or purple-dominant palette
Constraints: genuinely transparent background; no character, bowl, text, logo, watermark, panel, square frame, black backdrop, checkerboard, scenery, or opaque background; no huge explosion cloud; suitable for runtime rotation, scale-up, and alpha fade
```
