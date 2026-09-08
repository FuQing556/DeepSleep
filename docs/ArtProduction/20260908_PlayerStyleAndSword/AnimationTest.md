# Harness 六帧挥剑实验

用户明确授权尝试帧动画，原一状态一图不作为本次实验的限制。内置 image_gen 生成；未改生产Assets与Unity装配。

- HA_Swing6_Test_v01.png：3×2六帧，蓄势、起挥、斩击、跟随、收剑、回待机。
- AnimationPreview.html：浏览器背景位移播放原图，可暂停、逐帧与调速，没有用代码补画或掩盖原图。
- 初看动作顺序成立，身份大致连续；帧1/4剑尖跨格，头部与身体锚点漂移，帧6与帧1不完全相同，正式循环可能跳动。
- 灰色背景为本轮有意使用的测试底色，不是透明素材。剑气烘焙在部分帧中，正式可升级剑气仍需独立层。
- 未验证正式Unity动画、判定帧或生产可用性。下一步须先看播放结果，再决定修关键帧还是继续扩帧，不以静态帧表冒充完成动作包。

## 生成提示词

Use case: stylized-concept. Asset: ONE six-frame 2D sprite animation sheet, exactly 3 columns by 2 rows of equally sized cells. Landscape canvas, preferably 1536x1024, each cell 512x512. No drawn grid lines, no labels, no text. Plain solid neutral light gray background, NO checkerboard. Input 1 is Harness lightsword design reference. Input 2 is authoritative identity and proportions/style reference. Same cute black-haired red-eyed glasses orca maid in ALL SIX FRAMES, black white red dress/apron/ribbons, white maid headband, red flower, long hair and black-white orca tail, two legs, shoes. Facing three-quarter right, airborne with bent knees. This is ONE CONTINUOUS TWO-HANDED DIAGONAL SWORD CUT followed by recovery, not six unrelated poses. Fixed camera, fixed character size, head size unchanged, pelvis at same relative cell coordinate (50%,60%), full body and complete sword inside each cell with margins. Do NOT auto-fit each pose; use same scale in every cell. Chronological order left to right top row then bottom row: frame 1 READY sword raised diagonally upper-right, two hands on hilt; frame 2 ANTICIPATION draw sword up and back above shoulder with small torso twist; frame 3 STRIKE swing blade forward/down through upper-right quadrant with a thin red motion arc; frame 4 FOLLOW THROUGH sword now lower-right, arms extended forward, torso leaning slightly into completed stroke, hair and tail lag behind; frame 5 RECOVERY draw hands and sword back toward ready, hair settles; frame 6 RETURN READY closely matches frame 1 for repeatable playback. Sword is always same straight energy blade with white core red rim and same black-red technological hilt. Maintain glasses, eyes, costume patterns, number of limbs, long tail every frame. Fine dark anime linework and restrained cel shading from input 2. No background scenery, no floor, no cast shadow, no giant effects obscuring anatomy. This is a practical animation experiment, prioritize temporal consistency, distinct progressive sword angles and equal framing.
