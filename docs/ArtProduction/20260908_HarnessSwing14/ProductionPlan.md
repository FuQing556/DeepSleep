# Harness 14帧挥剑试作

> 已取消：用户暂停帧动画并撤回相关第三方技能。下文仅为旧试作记录，不继续执行，不作为新动作设计依据。

状态：制作中；非已导入资产。内置 image_gen 生成动作，本地后处理仅抠图、切分、统一画布和验收。

## 母版与不变量

身份母版为 SPR_HA_IdleFly_v03；持剑外观沿用 HA_SwordReady_Candidate_v03，黑白红虎鲸娘、眼镜红眼、红花、发饰、长发、鳍耳、完整尾部、女仆服、双腿黑鞋。三分之二侧向右，始终飞行屈膝。

双手握同一剑柄：近侧手在护手后，远侧手在柄尾，不套单手动作模板、不切换武器归属。近侧前臂有遮挡，不虚构解剖左右；按画面前后手及肩连接检查。人物主体和剑刃同图，巨型剑气独立，不烘焙伤害范围。

主色无青色，因此选择纯青 #00FFFF 作一次性抠像底；不得删除白色围裙、剑芯或红色光效。保留全部原始图。正式画布与世界尺寸分开，不修改碰撞体。

## 节拍

00/13同一持剑待机母版；02上举蓄势；05前下方斩击；08低位随挥；11收剑回位。01/03/04/06/07/09/10/12由模型按相邻关键姿态补画。仅00/13允许按循环约定完全相同，不复制关键帧充数。

保持统一像素体量与骨盆锚点，飞行脚部自然运动，不强制脚底对齐。目标14帧不等于固定14fps，最终预览分别提供正常节奏和慢放。生成失败不可通过逐帧拉伸、肢体拼接或隐藏背景蒙混。

## 待机母版生成提示词

Use case: identity-preserve. Prepare ONE master ready-pose sprite for a 2D flying sword animation. Image 1 is the exact accepted Harness two-handed sword design and pose to preserve. Image 2 is original identity reference only. Reproduce image 1's character and pose faithfully, do not redesign. Square 1024x1024 canvas. Place complete girl AND complete sword within central 70% of canvas, leave large flat gutters on all four sides. Fixed pelvis anchor at (512,590), head width about 190px, ahoge-to-shoes body height about 560px; the weapon can extend higher/right but never beyond 90% canvas. AIRBORNE bent knees with two separate intact legs and two shoes, not standing. Both hands stay gripping the same technological black-red hilt: foreground/near hand nearest blade guard, far hand toward pommel; no hand swap. Sword straight white core with narrow red rim points diagonally up-right, controlled small red halo. Exact black flowing hair, red eyes, small round glasses, white maid headband, red flower on screen-left hair, black/white orca fin ears and orca tail, black white red maid outfit apron and ribbons. Same cute face and balanced chibi proportions, precise dark thin anime linework. Background MUST be perfectly uniform saturated cyan #00FFFF, NO checkerboard, no gradient, no shadow, no cyan reflected light or cyan outlines on character. This cyan is disposable chroma key, not part of the art. No labels/text, no ground, no dust, no sword arc, no detached effects. Maintain full figure with clear empty gutters, no cropping.
