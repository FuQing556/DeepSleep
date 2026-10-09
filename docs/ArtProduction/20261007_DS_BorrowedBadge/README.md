# DS「借来的工牌」简易皮肤候选

## 最新状态：2026-10-07已接入

以下候选记录保留追溯，不再代表当前上线配置。v02常态/睡眠作为不露牌版；常态Visible_v03与睡眠Visible_v04作为自然露牌版，四张图已复制到`Assets/_Project/Art/Characters/DeepSeek/Skins/BorrowedBadge/`。生产图1254方图、PPU512、真透明，不改玩家碰撞。皮肤商店10券、DS专属，存档/佩戴/事件随机/联机已实现。静态展示用不露牌常态。原有饰品参数保留，为四张新图追加锚点，最终实玩视觉仍待验收。详见`docs/PlayerSkins.md`，本批未打包上传。

### 自然露牌版实际提示词（常态、睡眠各自以已确认不露牌图为参考）

Edit target: this transparent DS game sprite. Make a second natural lanyard variant with the Claude badge visible. Change ONLY the neck lanyard and badge; preserve the entire character pose and proportions exactly. The ID card hangs freely from the orange neck lanyard onto the cream hoodie torso below/beside the bent forearms, as if it has naturally swung free. It must NOT be on or in front of the sleeve: put it against the lower chest/upper abdomen sweatshirt fabric, with a modest tilt and size, partially overlapping existing hoodie pocket flower if necessary. It is a small wearable card reading "Claude", not a presentation placard. Do not reposition arms, hands, bowl (if present), skirt, hair, face, ears, tail or legs. Same whole silhouette and square framing, real transparent background.

睡眠露牌的首次生成错误地增加头像，未用于生产；以该结果局部修订得到Visible_v04，实际提示词：

Only edit the ID badge in this transparent sleeping DS sprite. Keep its existing position and natural neck lanyard. Make the badge SMALLER and simple: a small square white card with a thin orange border and just the word "Claude", no portrait, no icon, no extra symbols. The badge is hanging freely, not sewn to her sleeve. Preserve all other pixels and the sleeping pose, whole body, clothing, hair, face and transparent background.

---

## 历史候选记录

2026-10-07 用户授权：先画常态和倒地两张换装图，不替换任何攻击/技能特效；皮肤、皇冠、翅膀均定价10鲸元券，三种功德下架。

## 本批边界

- 候选只存于本目录，尚未导入生产目录或上线皮肤购买/佩戴。
- 母图：Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png、SPR_DS_DownedSleep_v01.png。
- 服装参考：用户第三张梗图 codex-clipboard-422e243f-89e1-4299-a014-b17a72fd94bf.jpg。
- 保留母图身份、比例、悬浮姿态、耳鳍、蓝白分叉鲸尾；常态继续捧原饭碗，倒地为睡眠姿态。
- 换奶白卫衣、橙色蝴蝶结和短裙、橙鞋、奶白腿套、橙色星芒发饰、Claude工牌，不加耳机。
- 使用 deepsleep-art-generation + imagegen 技能，内置 imagegen，真实透明背景；未使用CLI或本地重绘。
- raw保存生成原图，ready为原样副本，无缩放、裁剪、抠图或碰撞改动。下一步须确认画面，再核对身体标尺/导入/饰品锚点并实现购买、保存和联机展示。

## 实际提示词

两张候选均1254×1254、32位ARGB，角落Alpha=0；真实透明，不是棋盘背景。已目视检查完整人物/尾巴、睡眠手臂衔接及服装一致性。尚未进行Unity显示尺寸和饰品佩戴验收，不能据此声称皮肤系统完成。

### 常态（身份编辑；参考：原常态、用户服装图）

Use case: identity-preserve. Create ONE transparent full-body game sprite, a costume skin edit of image 1. Image 1 is the authoritative DS character identity, exact chibi proportions, polished cel-shaded drawing style, floating pose and framing. Image 2 is clothing reference ONLY, not proportions or pose. Keep image 1 face, blue eyes, blue hairstyle, whale fin ears and blue white-bellied forked whale tail, same head size, same hovering bent legs and hands holding the SAME original black rice bowl. Replace maid outfit and maid headband with image 2 cream hoodie, orange bow, orange pleated short skirt with white hem, orange shoes and cream legwarmers, orange starburst hairpin and hanging white badge with exact text "Claude". The joke is DeepSeek wearing Claude clothes. No headset, no phone, no floor, no scene, no shadow, no effects. Complete unclipped silhouette with transparent margin on square canvas, true transparent background. Do not make a taller teenage body: match image 1 body-to-head ratio precisely.

### 倒地（身份编辑；参考：原睡眠、本批常态）

Use case: identity-preserve. ONE transparent full-body game sprite. Image 1 is authoritative sleeping DS pose and framing: same chibi head size, face, blue whale fin ears, long blue white-bellied forked whale tail, diagonal horizontal floating sleep with hands together under cheek and naturally bent legs, closed eyes and peaceful smile. Image 2 is authoritative costume skin and art finish: replace image 1 maid outfit and headband with exactly image 2 cream hoodie, orange bow, orange short pleated skirt white hem, cream legwarmers, orange shoes, orange starburst hairpin and dangling Claude name badge. Same character and same proportional scale as image 1. No bowl in sleeping pose. Complete silhouette including tail and feet, generous transparent margins, square canvas. Not standing, no bed, floor, background, shadow, ghost, effects, text outside name badge. Real transparent background.

## 商店实际变更

## v02 工牌自然遮挡修订

用户指出v01为了展示工牌，错误地将其画在袖子前面。按用户要求，仅将工牌放回胸前，并允许手臂/饭碗遮住；不改变姿势，不以文字可读性为优先。使用deepsleep-art-generation和内置imagegen局部编辑各自v01，真实透明，保留v01原文件。v02仍是未接入的候选。

常态实际提示词：

Edit target: this transparent DS costume game sprite. Change ONLY the orange lanyard and Claude ID badge. The current badge wrongly floats in front of her forearm. Put the badge naturally against the CENTER of her hoodie chest, hanging from the neck lanyard BEHIND her bent forearms and the rice bowl. Her existing arm and bowl obscure most or even all of the badge; obscured Claude text is perfectly acceptable. Do not move the badge sideways or below the arms to show it off. No badge attached to sleeve. Remove the old visible badge over the sleeve, restore the cream sleeve beneath it. Preserve exact pose, hands, bowl, face, hair, clothes, tail, proportions, framing and all other details. True transparent background.

倒地实际提示词：

Edit target: this transparent sleeping DS costume game sprite. Change ONLY the orange lanyard and Claude ID badge. The current badge is wrongly displayed on the OUTSIDE of her sleeve. The badge belongs against the CENTER of her hoodie chest, hanging from a neck lanyard UNDER her crossed/bent sleeping arms. Her arms should naturally hide most or all of the badge; no readable Claude text is necessary. Do not reposition her arms or reveal the badge artificially. Remove the badge from the sleeve and restore the cream sleeve fabric there. Keep exact peaceful sleeping pose, hands under cheek, face, hair, clothes, tail, body proportions, silhouette and framing unchanged. True transparent background.

### 已完成商店配置（与v02美术修订无关）

- Unity序列化保存MainMenu的商品列表，只保留皇冠和翅膀；不删除功德资产和既有存档。
- 两个饰品配置_price=10；装配器创建默认价同步10，主页重建也过滤三种功德。
- 新皮肤价格已由用户确定为10，尚未加入可购买列表，避免出现买了却不能佩戴的商品。
- 未打包或上传GitHub。
