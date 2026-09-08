# DS 比例调整与 Harness 光剑母版候选

2026-09-08。使用内置 image_gen，未调用外部 API。用户指定现有 Harness 为后续全角色美术的风格与比例基准；其他角色仍保留各自身份，不复制 Harness 的配色、服装和动作。

## 生产前核对

- 身份：两位既有虎鲸娘；不根据名称重新生成动物。DS 原图作为身份母版，Harness IdleFly_v03 作为风格和头身比例基准。
- DS：蓝白、短蓝发、蓝眼、星形胸饰、双手饭碗、飞行姿态、三分之二侧向右。仅温和收小头部，保留体量与服装。
- Harness：黑白红、长发、眼镜、红眼、原服装与虎鲸尾；飞行持光剑向右，非站立。终端被光剑替换，巨大剑气独立制作。
- 原生产贴图 PPU 均为512，Bilinear，默认平台不压缩。新图不能直接沿用PPU而忽略主体像素体量；导入前需按既有角色世界体量校准。
- 不改碰撞体；光剑图片和辉光不定义命中几何，不作为持续伤害碰撞体。正式挥斩用独立区域、单次结算。

## 当前交付状态

- DS_Idle_Proportion_Candidate_v02.png：比例候选；1254×1254 RGB，棋盘格烘焙在背景中。透明提取重试仍未成功，**不是可直接导入的生产Sprite**。
- HA_SwordReady_Candidate_v02.png：持剑设计候选；剑尖留白已改善。1254×1254 RGB，同样有烘焙棋盘格，**不是生产Sprite**。
- 不覆盖原图，不写入 Assets，不修改任何 Prefab/Collider。两式挥剑和独立剑气尚未生成，需以确认的持剑母版继续。
- 后续统一DS睡眠等状态的一致性，但本次没有改现有睡眠图。

## 输入核对范围

已读取 PlayerCommand 与 AimIntent：有主次技能、ConfirmAim、带坐标语义的Aim；不能据此声称手机控件或持续近战已经接好。下一步需继续核对具体命令源、消费端与救援取消链。本次未写输入或技能代码。

## 初始生成提示词

### DS

Use case: style-transfer / precise character proportion edit. Make ONE transparent-background full-body 2D game character sprite, no sheet no text. Image 1 is the EDIT TARGET DeepSeek blue-white orca maid flying with a rice bowl. Image 2 is Harness, the authoritative STYLE AND HEAD-TO-BODY PROPORTION reference, NOT a character to include. Preserve DeepSeek identity: blue bob hair, curved ahoge, blue eyes, white maid headband, blue-white fin ears, navy blue maid dress, white apron, blue bow and gold star brooch, dark bowl with gold rim full of white rice, blue-white orca tail, socks and blue shoes. Preserve her original gentle cheerful expression, both hands carrying bowl, hovering with feet off ground and legs bent, facing three-quarter right. Main change: subtly reduce her disproportionately large head and hair mass roughly 12-15 percent relative to torso, naturally redraw neck/shoulder connections; match Harness reference balanced chibi head/body proportion and finer darker linework and restrained cel shading, still extremely cute not realistic adult proportions. Do NOT scale entire body down; keep a comparable full-body visual footprint, feet and crown fitting with clean margins. Do not copy Harness glasses, black hair, red palette, long hairstyle, tablet, pose. All silhouette fully visible including ahoge fins feet tail, no clipping, no ground shadow, no background, actual transparent alpha not checkerboard painting. Sharp polished anime game sprite. One blue character only.

### Harness

Use case: identity-preserve. ONE transparent full-body 2D game sprite for Harness lightsword READY stance. Input image is the authoritative exact character/style/proportion mother sprite. Keep precisely same girl identity, small glasses, red eyes, long flowing black hair, red flower hair clip, curved ahoge, white maid headband, black-white fin ears, black-white-red maid dress and apron, red ribbons, black shoes and socks, black-white orca tail, same balanced chibi head/body ratio and detailed thin anime linework/cel shading. She is flying toward three-quarter RIGHT, feet lifted and knees bent, never standing on ground. Replace handheld terminal completely with one elegant sci-fi energy sword: compact black-red technological hilt, straight white-hot core blade with controlled red glow, readable sword silhouette, no logos no text. Ready-to-fight posture: grip sword in front of her toward upper-right, free arm balancing, not slashing yet. Preserve cute composed focused expression. Match original body size and face scale, do not enlarge head. Entire body, tail, hair and blade fully contained in square canvas with margins; avoid making body tiny to fit enormous sword: blade approximately torso-plus-head length, huge attack wave will be SEPARATE asset later. No slash arc, no magic circle, no terminal, no scenery, no floor, no shadow. Genuine transparent alpha background. Single consistent character sprite, not concept sheet.

## 修订提示

- DS第二轮只要求移除棋盘格、保持人物全部像素设计与干净alpha边缘；结果未得到alpha。
- Harness第二轮只要求完整剑尖和红光留白，不变形、不改角色；留白改善但输出失去alpha。保留作设计候选，不掩盖技术问题。
