# 试玩优化：角色动作生产（进行中）

使用内置 imagegen，真实透明背景；不覆盖旧人物图，不烘入饰品和技能特效。2026-10-12 已导入12张动作到 `Assets/_Project/Art/Characters/PlayerActions/`，三套配置在 `Configs/Presentation/CFG_PlayerActions_*`，三战斗场景显式绑定。raw 保留原始版本，工牌前飞正式采用 v02；v01 不导入。尚未完成真机、双端和所有姿态视觉验收。

## 母图

- DS：Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png
- HS：Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png
- 工牌皮肤：Assets/_Project/Art/Characters/DeepSeek/Skins/BorrowedBadge/SPR_DS_BorrowedBadge_Idle_Hidden.png

## 已产出候选及提示词

### SPR_DS_Glide_v01

Use case: stylized-concept. Create a single full-body transparent game character sprite: the exact same cute blue-haired orca girl in the reference, same face, head-to-body proportions, maid outfit, rice bowl, fine clean anime cel shading. Change her action to a gentle forward glide toward screen right: torso leans forward slightly, knees comfortably bent trailing behind, both hands steady the existing rice bowl, blue tail flowing behind. Natural coherent anatomy, a clearly different flying pose but not an extreme dash. Keep original costume and character size within a square canvas with ample clear border; whole hair, feet and tail visible. Character only, genuinely transparent background, no ground, no motion trails, no crown or wings, no added text. This is one movement variation for the existing game, not a redesign.

### SPR_HA_Hover_v01

Use case: stylized-concept. One full-body transparent game sprite of the exact same black-haired orca girl as the reference, preserving her face and head/body proportions, glasses, red eyes, black-white maid outfit and small terminal. A fresh relaxed hovering movement pose: she floats more upright with one knee gently raised, looking toward screen right, holding her terminal comfortably near her waist. Her hair and tail drift naturally behind. Clear silhouette and coherent anatomy; match the reference's clean detailed chibi anime shading. Full hair, tail and feet inside a square canvas with transparent margin. Character only, genuine alpha background, no scenery, no wings/crown, no attack effects or text. Do not redesign her clothing or make her taller.

### SPR_DS_BorrowedBadge_Glide_Hidden_v01

Use case: stylized-concept. Single transparent game character sprite, the exact same blue-haired orca girl in the cream hoodie and orange skirt reference. Preserve the reference face, proportions, clothing, orange hairclip, boots and rice bowl. New action: gentle forward glide toward screen right, body leaning a little forward, legs comfortably trailing bent, both hands holding the bowl steady; hair and tail flow back naturally. The bowl and arms naturally obscure her chest badge as in the reference; do not force it into view. Same clean detailed cute anime style. Full body inside a square canvas with clear margins, genuine transparent background. No extra wings, crown, scenery, speed lines or text.

### SPR_DS_Rescue_v01

Single full-body transparent game sprite of exactly the same blue-haired orca girl from the reference, identical cute face, head/body proportions, maid dress and clean anime linework. New action: hovering and helping a teammate, she cradles the existing rice bowl against herself with one arm and reaches her free open hand gently forward and slightly downward, kind concentrated expression. Natural connected arm and hand, relaxed floating legs; preserve costume and tail. Character only on genuinely transparent background, complete silhouette with margin. No second character, no magic or particles, no wings or crown, no text. This is a rescue-channel pose in the same game.

## 待办

新手引导仍未实现。动作后续还需细看逐姿态饰品锚点、移动切换重心、真机和双端表现；当前不等于整轮交付。强化暂复用庆祝图，持续0.75有效现实秒，胜利保持庆祝图；技能/近战和倒地优先。受击0.22有效现实秒，救援跟随实际引导状态。随机移动在现有常态与新增动作间选择，每2.5～4.5秒重选，不使用战斗随机源。

工牌显隐事件不变：仅隐藏工牌的常态启用隐藏版前飞变体，露牌常态继续保留原图；事件动作结束恢复已经抽到的常态，不重抽。尚未补露牌版前飞变体，不能称两种工牌移动版本都完成。主菜单展示不使用本组件。

## 本次补图提示词（内置 imagegen，transparent_background=true）

### 工牌前飞 v02

输入1：raw/SPR_DS_BorrowedBadge_Glide_Hidden_v01.png（修改目标）；输入2：工牌隐藏常态母图（身份与比例参考）。

Edit image 1, the forward-flying game sprite. Her head is too large relative to her torso: reduce the head and attached upper hair silhouette by roughly 15%, reconnect the neck and flowing hair naturally. Image 2 is the approved identity and proportion reference. Preserve the forward glide, clothes, face identity, hands carrying rice bowl, naturally obscured badge, legs and tail. Full character on a square transparent canvas with margin, matching clean anime sprite art. No added objects or effects.

### 工牌受击 v01

输入：工牌隐藏常态母图。

Use case: stylized-concept. Single full-body game sprite of the same blue-haired orca girl in the reference, same face and head/body proportions, cream hoodie and orange skirt. Brief hurt reaction while floating: a small backward flinch, eyes squeezed shut, shoulders drawn up as both arms hug her rice bowl safely, legs naturally bend with recoil. Badge remains naturally hidden behind arms and bowl. Preserve the reference costume and clean anime shading. Complete hair, tail and feet with margin on square transparent canvas. No wounds, tears, hit effects, added accessories or text.

### DS 庆祝

Single full-body transparent game sprite of the identical blue-haired orca girl in the reference, same proportions, face and maid dress. A cheerful victory gesture while floating: one arm hugs her rice bowl, the free hand raises a small triumphant fist beside her head, a happy open smile, legs comfortably bent. Natural connected anatomy, modest flowing skirt and hair. Same clean detailed anime style, full silhouette with margin on square transparent canvas. No scenery, extra accessories, words or visual effects.

### HS 庆祝

Single full-body transparent game sprite of the identical black-haired orca girl in the reference, same face, glasses, proportions and black-white-red maid dress. Victory reaction while floating: she holds her little terminal securely with one arm, raises a small triumphant fist beside her head with the other, smiling happily; knees bend naturally and hair flows behind. Preserve the reference clean detailed anime style. Complete hair tail and feet inside square transparent canvas with margin. No weapons, added accessories, scenery, text or effects.

### 工牌庆祝

Single full-body transparent game sprite of the same blue-haired orca girl in the reference, same head/body proportions, face, cream hoodie and orange skirt. Happy victory gesture while floating: cradle the rice bowl against her chest with one arm, raise a small triumphant fist beside her head with her free hand, smile brightly, legs naturally bent. Keep the badge obscured naturally by the bowl. Preserve clothing and clean detailed anime art. Full silhouette with margin on square transparent canvas. No scenery, extra accessories, text or effects.

## 接入与检查

- 导入器：`PlaytestPlayerActionsInstaller`；逐图PPU和初始饰品锚点记录在导入器。只增新锚点，不覆盖用户旧姿态参数。不改碰撞体。
- 81项资产/重开场景引用检查通过，Unity无编译错误。运行会话实际扣血验证DS工牌/HS受击切图、恢复；实际开始救援验证工牌救援图，额外工牌抽取次数均为0。检查是定向验证，不是完整自然对局。
- 内容版本更新为 `20261012-playtest-actions-1`，协议仍19；新动作走既有网络Sprite快照，双方需同版本。未打包上传。
- 原皮肤49项回归通过，饰品检查改为覆盖完整配置而非硬编码9张：25姿态、426项通过。原用户调参未覆盖。

## 补录前批候选提示词

### HS 救援（参考 HS 常态）

One full-body transparent game character sprite of the exact same black-haired orca girl in the reference: same cute face, glasses, red eyes, maid outfit, body proportions and fine anime shading. Rescue action: hovering while holding her little terminal close to her body in one arm, reaching her other open hand forward and slightly down toward a teammate, looking attentive and caring. Natural arm/hand connection, relaxed floating legs, hair and tail gently flowing behind. Complete character with clear margin on square transparent canvas. No teammate, no glow, no magic, no scenery, no crown or wings, no text.

### 工牌救援（参考工牌隐藏常态）

One full-body transparent game sprite of exactly the same blue-haired orca girl in the reference, cream hoodie and orange skirt, same face, proportions, hairclip and fine anime style. Rescue action while hovering: cradle the rice bowl in one arm and extend the other open hand gently forward and slightly downward to help a teammate. The sleeves and arms connect naturally; relaxed floating legs, gentle focused smile. Keep the chest badge naturally obscured by the bowl/arm rather than displaying it. Full character and tail inside square transparent canvas with margin. No other character, magic particles, wings, crown, scenery or added text.

### DS 受击（参考 DS 常态）

A single transparent full-body game sprite of the identical blue-haired orca girl in this reference, same face, head/body ratio, blue-white maid costume and rice bowl. Brief hurt reaction while flying: she flinches backward slightly, eyes squeezed shut, a small startled mouth, shoulders drawn up while hugging the bowl securely, legs naturally bending with the recoil. Cute non-graphic reaction, no injury or tears, no dramatic distortion. Keep her full tail, hair and feet inside the square with margin. Match reference clean cel-shaded chibi art. Genuine transparent background; character only, no hit flashes, debris, words, crown or wings.

### HS 受击（参考 HS 常态）

Single full-body transparent game sprite of the same black-haired orca girl from the reference, preserving her face, glasses, red eyes, head/body proportions and black-white-red maid outfit. Brief hurt flinch while floating: shoulders curl protectively, eyes closed in a small wince and mouth slightly open, both arms clutch her terminal safely near her chest, knees bend naturally with a slight backward recoil. Cute non-graphic reaction, no wounds or tears, no extreme pose. Entire tail, hair and feet inside square canvas with margin. Same detailed clean anime cel shading; genuine transparent background, no effects, words, crown or wings.
