# 睡眠比例与持剑腿部修订

内置 image_gen 编辑生成，原素材未覆盖；不修改 Unity 配置、碰撞体或预制体。

- DS_Sleep_Proportion_Candidate_v02.png：微调头部与枕手衔接，保留蜷身睡眠与角色身份。等待目测确认，不标为已导入。
- HA_SwordReady_Candidate_v03.png：扩大横向画布容纳完整剑身、修订腿部轮廓。生成意外改为双手握剑，不能声称只修了腿；作为候选等待确认。最终角色大小应按本体与既有母版对齐，不按剑尖到脚的总尺寸设置。
- 两式挥剑与独立剑气仍未生成；母版未定，不在不同武器/手势之间盲目扩展。
- 输出仍可见棋盘格，按技术候选处理，未完成干净透明生产稿。

## 完整提示词

### DS

Use case: precise-object-edit. Image 1 is the EDIT TARGET: existing DeepSeek blue-white orca maid sleeping curled up midair. Image 2 is her approved newer awake proportion reference ONLY, and image 3 is the Harness global anime style reference ONLY. Produce ONE revised sleeping sprite, not a sheet. Preserve image 1 sleeping pose, closed eyes gentle smile, blue bob hair, ahoge, maid headdress, fin ears, navy-white apron dress gold star, blue shoes socks, blue-white orca tail, no rice bowl. Reduce the oversized head together with its hair/headdress by approximately 12 percent relative to unchanged torso and legs, matching image 2's more balanced chibi proportions. Naturally reposition hands beneath cheek and repair neck/shoulder transitions; don't simply paste a small head. Maintain cute round face, do not make adult proportions. Thin polished linework with subtle cel shading consistent with image 3. Keep same diagonal floating curled silhouette and overall body size, tail fully visible, no ground/pillow/bed/new props. Actual transparent alpha PNG background, empty transparent space around full silhouette; do NOT paint a checkerboard or white backdrop. No text, no border.

### Harness

Use case: identity-preserve / precise-object-edit. Image 1 is the authoritative Harness character mother sprite for exact identity, costume, face, glasses, head/body proportion and body scale. Image 2 supplies ONLY the already approved lightsword design and ready hand pose. Create one corrected Harness sword-ready flying sprite. Preserve exact black-haired red-eyed glasses orca maid: headband, ahoge, red flower, fin ears, long wind-swept hair leftward, black white red dress, white apron, ribbons, orca tail, socks black shoes. Replace original terminal with image 2's same black-red tech hilt and white-core red energy blade held diagonally upper-right. Correct lower anatomy clearly: exactly two legs, near thigh emerges naturally from near side of skirt, knee visibly gently bent; far leg slightly behind with separate knee and ankle, neither fused nor crossed unnaturally. Both feet hanging back in airborne pose, never standing, same youthful chibi proportions as image 1, do not elongate legs. Body head-to-feet projected height should remain comparable to image 1, not shrink to fit sword. Allocate extra transparent canvas width to right if necessary: character body roughly 82 percent of canvas height, generous right margin for sword tip and glow. Full sword tip and halo uncut. No slash wave yet, no tablet, no extra props. Actual transparent alpha background, not a painted checkerboard. Single full-body sprite, no sheet, no text. Crisp fine dark linework and restrained cel shading exactly as image 1.
