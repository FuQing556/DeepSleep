# 简易皮肤：DS「借来的工牌」

2026-10-07 已接入源码和三个正式场景；未打包、未上传，手机和真实双端实玩仍待验收。

## 玩家规则

- 商店10鲸元券永久购买，背包中点击「DS穿戴」，再次点击换回原版；HS不能穿戴。
- 皇冠、小翅膀均10鲸元券，原有前饰/背饰独立槽不变，可与服装叠加。
- 三种功德从商品列表移除，保留旧资产和已持有的存档记录。
- 仅换常态和倒地角色贴图，不换攻击、技能、UI，不改变数值与碰撞。
- 常态、倒地各有露牌/不露牌两张，每次抽取各50%。入局、复活、进入和离开节点抽常态；倒地时抽倒地。移动或攻击不重抽。进入节点在开始揭示时抽一次，不在揭示结束时再抽。
- 商店图标和选角固定用不露牌的常态图。随机结果由房主产生，客机消费同一个角色贴图快照。

## 配置与实现入口

- 服装配置：`Assets/_Project/Configs/Progression/Meta/CFG_Skin_DS_BorrowedBadge.asset`，配置预览图和两组随机贴图。
- 商品配置：`Assets/_Project/Configs/Progression/Meta/Products/CFG_META_Product_ds_borrowed_badge.asset`，价格/描述/专属角色由配置提供。
- 四张生产贴图：`Assets/_Project/Art/Characters/DeepSeek/Skins/BorrowedBadge/`。
- `PlayerDownedVisual2D`保留原角色姿态流程，只在对应事件替换贴图，使用独立外观随机源，不消费战斗随机数。
- `PlayerSkinSessionPresenter`负责角色所有者的佩戴选择、入局/节点事件与联机。`PlayerSkinImageView`负责固定局外展示。
- 主页ThemePortrait编辑态为空、由UiThemeView运行时填入，因此通过该视图的PlayerSkins显式绑定目录；穿戴/摘下和主题切换一起刷新，DS服装不改变HS主题角色图。
- `LocalPlayerProfileStore`版本5，兼容旧档；购买所有权与DS佩戴ID独立保存，不改饰品槽。
- `PlayerSkinInstaller.Install()`显式装配MainMenu、Gameplay_Prototype、World01_EarlyInternet。安装时保留已有饰品姿态参数，为四张新图追加初始锚点。可在原饰品编辑器继续微调新图。

## 联机与验证

协议10、内容版本`20261007-ds-skin-1`。新增两字节皮肤ID选择消息；实际随机贴图复用原玩家快照，四张图追加到原Sprite目录，不改已有ID。旧安卓包不能与本批源码混房。

编辑器隔离检查：皮肤49项（含Awake顺序、首次入局抽取、主页实际主题填图和还原）、饰品75项、协议1526项、UI5525项通过；三个场景引用检查通过，Unity编译无错误。测试使用Temp中的独立存档，不替玩家购买或装备。尚未验收手机实玩、真实双端和所有姿态饰品的最终视觉位置。

美术使用deepsleep-art-generation与imagegen，沿用DS母图比例、饭碗、鲸尾和真实透明背景；来源及实际提示词见`docs/ArtProduction/20261007_DS_BorrowedBadge/README.md`。
