# 06｜美术资源清单与交付验收 v3.1（素材有效，动作语义待重映射）

> 2026-09-04：角色造型、画风、文件规格和已有候选继续有效；TapRise/Fall/扑翼/旧路线专属动作不得直接进入新玩法，按 `18_CoopShooterGameplaySpec.md` 重映射为自由移动、常态开火、清弹、救援和共鸣状态。

> 第一批只生产灰盒玩法当前需要的 DeepSeek/Harness 状态图、循环背景与第一章机制图形。概念候选不得冒充透明生产素材；不为尚未落地的玩法提前堆动画帧。

## 1. 总体画风

- 日系二次元 Q 版、2.4–2.8 头身、明快粗轮廓、手游 Sprite 可读性。
- 线稿外轮廓 4–7px（按 320px 角色格），内部线 2–4px；缩到游戏尺寸仍能看清。
- 阴影以 2–3 阶赛璐璐为主，少量柔光；避免写实皮肤、复杂蕾丝噪点和过细头发丝。
- 视角：大部分角色 3/4 侧身朝右；Boss 朝左。同一角色各状态不得改变透视或身体比例。
- 透明 PNG，sRGB，直通 Alpha，禁止白边/黑边。
- 角色素材不含地面投影；投影/VFX 单独交付。
- 图中不烘焙中文或英文长句，不烘焙公司 Logo。UI 文本由程序渲染。
- 角色默认“一种语义状态一张透明主图”。悬浮、轻摆、蓄力呼吸、受击闪烁用 Unity Transform/颜色曲线，光环、拖尾、弹射与烟尘用独立 VFX；只有实机证明单图无法读懂的动作才追加关键帧。
- 状态切换采用最多一个旧图残影：新图立即完整显示，旧图先降至低 Alpha 再淡出；飞行残影短、技能残影中等、Boss 转阶段才允许接近 1 秒。频繁切换只覆盖唯一 Ghost，不累积 SpriteRenderer。

## 2. 文件与导入约定

### 2.1 命名

```text
CHR_DS_<State>.png
CHR_GE_<State>.png
EN_<Type>_<State>.png
OB_<Type>_<Part>.png
PRJ_<Owner>_<Type>.png
PU_<Type>.png
VFX_<Type>.png
UI_<Group>_<Name>.png
BG_<Depth>_<Variant>.png
```

源文件放 `ArtSource/`（可为 PSD/KRA/CLIP），导出 PNG 放 `Assets/_Project/Art/`。Unity 目录里不放几十个未合并图层的 PSD。

### 2.2 通用导入

- PPU=100。
- 角色、UI：Compression=None，MipMaps=Off，Filter=Bilinear。
- 像素化特效如明确标 `Point` 才用 Point。
- 同角色全部状态图使用相同画布、Pivot 与透明留白；切换 Sprite 时身体核心不得跳动。
- 若某个特批动作确需多帧，才使用等尺寸 Sprite Sheet，并在本表单独登记理由、帧数与 FPS。

### 2.3 Pivot 表示法

本文 Pivot 用归一化坐标 `(0..1, 0..1)`。同角色所有状态图必须使用同一 Pivot。

## 3. 角色比例基准

| 类型 | 单帧格 | 画面可见主体 | 游戏可见尺寸约 | Pivot |
|---|---:|---:|---:|---:|
| 玩家 DeepSeek | 320×320 | 210×235px | 2.10×2.35u | (0.48,0.43) |
| 玩家 Harness | 320×320 | 215×240px | 2.15×2.40u | (0.48,0.43) |
| Gemini Boss | 512×512 | 380×430px | 3.80×4.30u | (0.50,0.44) |
| 巨型 Opus 半身 | 1024×1024 | 900×880px | 9.00×8.80u | (0.50,0.08) |
| 普通飞怪 | 192×192 | 130×145px | 1.30×1.45u | (0.50,0.46) |
| 地面怪 | 192×192 | 145×120px | 1.45×1.20u | (0.50,0.30) |
| 拾取物 | 96×96 | 64×64px | 0.64×0.64u | (0.50,0.50) |

“主体尺寸”不含头发飘带和 VFX，但含主要轮廓。若导出主体超差 ±8%，必须返工或更新 Prefab/Collider 验收，不准随手改 Transform Scale 掩盖。

## 4. 玩家 DeepSeek 鲸鱼娘

目录：`Assets/_Project/Art/Characters/DeepSeek/`

### 4.1 必须锁定的造型

- 蓝发、鲸鳍耳、肥短鲸尾、蓝白女仆装、白米饭元素。
- 2.6 头身；脸圆但不是婴儿；表情蠢萌、理直气壮。
- 不加鸟翼。飞行靠身体轻飘、裙摆和尾巴受气流托起。
- 武器不是枪：可用饭勺/筷子形发射器或从白饭碗发射 Token 光弹，保持荒诞感。
- 胸口不放大 DeepSeek Logo；允许发饰上有一个小鲸鱼符号。

### 4.2 状态图交付

每个状态独立一张 320×320 透明 PNG；不是 Sprite Sheet。最初可玩版只要求 `IdleFly`、`TapRise`、`Fall`，其余状态随对应玩法实现再生产。

| Asset ID | 状态 | 主图姿态 | Unity 表现 |
|---|---|---|---|
| CHR_DS_IdleFly | 中性飞行 | 悬浮、尾巴自然弯、抱饭碗 | VisualRoot 低幅上下浮动，尾/VFX 轻摆 |
| CHR_DS_TapRise | 点按上升 | 鳍耳/手臂下压，裙摆上提 | 0.12s 上冲位移与轻微上仰 |
| CHR_DS_Fall | 松开下落 | 头发上飘、尾巴下垂、略慌 | 随 Y 速度平滑下俯 |
| CHR_DS_Shoot | 自动攻击 | 饭勺/碗朝前，不遮脸 | 短后坐；饭团、枪口闪光独立 |
| CHR_DS_Hurt | 受击 | 圈圈眼、失衡 | 红白闪、短抖动，飞饭粒独立 |
| CHR_DS_ShieldCast | 开盾 | 抱碗防御 | 护盾环缩放/淡入，角色不逐帧变形 |
| CHR_DS_Death | 失败 | 尾巴垂下、失去升力 | 整体旋转下坠与淡出 |
| CHR_DS_Victory | 胜利 | 举碗庆祝 | 轻弹跳与星光 VFX |

状态验收：同一 Pivot；脸、鲸鳍耳、鲸尾数量与服装结构稳定；身体核心切换位移≤8px；面朝右；饭勺/Muzzle 锚点误差<12px。

### 4.3 独立玩家部件

| ID | 尺寸 | 用途 |
|---|---:|---|
| CHR_DS_Portrait_Normal | 512×512 | 对话/升级 UI 半身 Q 版 |
| CHR_DS_Portrait_Hurt | 512×512 | 低血提示 |
| CHR_DS_ShieldRing | 384×384 | 白米饭护盾单图，以缩放/旋转/Alpha 做展开和循环 |
| CHR_DS_MuzzleFlash | 128×128 | 发射闪光单图，以缩放/Alpha 做短促脉冲 |
| CHR_DS_Shadow | 192×64 | 仅菜单/落地演出使用 |

## 4B. 玩家 DeepSeek Harness 黑鲸鱼娘

目录：`Assets/_Project/Art/Characters/Harness/`

### 4B.1 造型锁定

- 2.6 头身，与蓝色 DeepSeek 共用圆脸与鲸族骨架，但不能只做色相替换。
- 黑色长发、红眼、细框眼镜、黑色鲸鳍耳、粗大黑鲸尾、黑白女仆裙、白围裙、红花/蝴蝶结和红裙边。
- 黑色连续面积不超过 70%；鲸尾腹面、围裙和袖口负责分层。眼镜反光不能遮住双眼。
- 飞行不加鸟翼；状态灯、任务队列卡与终端执行环体现 Harness，而非胸口贴 Logo。

### 4B.2 状态图交付

每个状态独立一张 320×320 透明 PNG，与 DeepSeek 使用相同身体 Pivot。最初可玩版只要求 `IdleFly`、`TapRise`、`Fall`，攻击图在 Harness 选区玩法落地时再做。

| Asset ID | 状态 | 主图姿态 | Unity 表现 |
|---|---|---|---|
| CHR_HA_IdleFly | 中性飞行 | 悬浮、冷静、状态灯亮 | 低幅悬浮与状态灯呼吸 |
| CHR_HA_TapRise | 点按上升 | 鳍耳下压、围裙与黑尾受气流托起 | 短上冲与轻微上仰 |
| CHR_HA_Fall | 松开下落 | 发梢上飘、鲸尾下垂 | 随 Y 速度下俯 |
| CHR_HA_Aim | 手动瞄准 | 单手展开红黑选区 | 选区环独立跟随触点/鼠标 |
| CHR_HA_Execute | 执行攻击 | 推眼镜并确认 | 0.28s 执行环收束后单次爆发 |
| CHR_HA_Insufficient | 能量不足 | 状态灯灰、略嫌弃账单 | 账单弹出和能量条抖动独立 |
| CHR_HA_Hurt | 受击 | 红灯告警、眼镜轻歪 | 红白闪与短抖动 |
| CHR_HA_Death | 失败 | 任务队列熄灭 | 整体旋转下坠 |
| CHR_HA_Victory | 胜利 | 完成打勾、疲惫微笑 | 打勾 VFX 与轻弹跳 |

独立部件：`CHR_HA_Portrait_Normal`、`CHR_HA_TargetReticle`、`CHR_HA_ExecuteRing`、`CHR_HA_BillingCard`、`CHR_HA_StatusLight`。选区与执行环必须透明底独立导出，不能烘焙在角色主图中。

## 5. Gemini Boss

目录：`Assets/_Project/Art/Characters/Gemini/`

### 5.1 造型

- 蓝紫渐变双生轮廓：可用双色双马尾/两束发尾，阶段一短暂分成上下残影。
- 手持香蕉形编辑笔；星光几何装饰；不画成普通 Google 四色制服。
- “美国豆包”通过过度认真表情、道歉话术 UI、日用助手配件体现，不把豆包本体缝在身上。
- 2.8 头身，Boss 体量靠外轮廓与光翼增加，不拉成正常 7 头身。
- 面朝左。

### 5.2 状态图

每个状态为一张 512×512 透明 PNG：`Idle`、`TwinCast`、`BananaCast`、`Apology`、`Hurt`、`Transition`、`Defeat`。双子残影、香蕉轨迹、阶段星光和消散均由下列独立部件加 Transform/颜色动画完成，不在角色主图内逐帧重画。

独立部件：

| ID | 尺寸 | 说明 |
|---|---:|---|
| CHR_GE_Portrait | 512×512 | Boss 名牌头像 |
| CHR_GE_TwinGhost_Blue | 384×384 | 蓝色残影，透明底 |
| CHR_GE_TwinGhost_Purple | 384×384 | 紫色残影 |
| CHR_GE_EditPen | 256×128 | 可独立发光/掉落 |
| CHR_GE_NameCard_BG | 1024×256 | 9-slice 或宽图，不含文字 |

## 6. 普通敌人

目录：`Assets/_Project/Art/Enemies/`

| ID | 画布 | 状态图 | 视觉锚点 | Collider 参考 |
|---|---:|---|---|---|
| EN_LowModel | 192×192 | Crawl、Hurt、Death 各 1 张 | 小型旧终端/低参数纸箱，举“过期”图标，不直接写长字 | Box 1.0×0.75u |
| EN_Hallucination | 192×192 | Float、Shoot、Death 各 1 张 | 半透明水母脑袋、虚假引用纸条、问号触手 | Circle r=0.58u |
| EN_ContextBug | 192×192 | Idle、Charge、Dash、Death 各 1 张 | 被撑爆的上下文卷轴虫、红色 overflow 环 | Capsule 1.1×0.62u |

低等模型不能画成现实弱势人群或带国籍歧视符号；它是低能力软件/旧终端怪。

独立预警：`EN_ContextBug_WarningLine.png`，64×1024 竖条/横向拉伸用 9-slice，红色实线，中间有向左箭头，不含文字。

## 7. 障碍

目录：`Assets/_Project/Art/Obstacles/`

### 7.1 数据管道

不要画成一整张不可伸缩的管道。拆为：

| ID | 尺寸 | Pivot | Draw Mode | 说明 |
|---|---:|---:|---|---|
| OB_Pipe_TopLip | 384×192 | (0.5,0.0) | Simple | Pivot 正好在门缝上边缘 |
| OB_Pipe_TopBody | 384×512 | (0.5,0.0) | Tiled | 向上延伸，顶部可无缝 |
| OB_Pipe_BottomLip | 384×192 | (0.5,1.0) | Simple | Pivot 正好在门缝下边缘 |
| OB_Pipe_BottomBody | 384×512 | (0.5,1.0) | Tiled | 向下延伸 |

嘴唇厚度画面约 0.45u；碰撞体沿实体轮廓矩形布置，装饰电线不得额外伤人。

### 7.2 Opus 巨型跳起事件

旧 `OB_OpusMountain_*` 素材退役，移入 `_Retired_StaticConcept_v01/`，不得挂进 Prefab。

| ID | 尺寸 | Pivot | 说明 |
|---|---:|---:|---|
| CHR_OP_Giant_Rise | 1024×1024 | (0.5,0.08) | 巨大 Q 版半身跃起姿势；整张位移由 Animator/Timeline 做 |
| CHR_OP_Giant_Hold | 1024×1024 | (0.5,0.08) | 半身停驻姿势；用轻缩放/摆动表现压迫感，不能像静态山 |
| CHR_OP_Giant_Review | 1024×1024 | (0.5,0.08) | 审稿/盖章姿势，风压 VFX 独立 |
| CHR_OP_Giant_Fall | 1024×1024 | (0.5,0.08) | 下落离场姿势 |
| VFX_OP_WarningShadow | 1024×256 | (0.5,0.0) | 橙金半透明预警影，不含文字 |
| VFX_OP_WindPressure | 1024×256 | (0.5,0.5) | 横向风压单图，以 UV/Alpha/位移表现，只推动玩家 |

Opus 采用用户参考中的橙发 Claude 系宽松锚点：橙色长发、黑/象牙白学者裙装、温和而有压迫感的审稿姿态。参考只用于辨识方向，不复制原图构图、对白或细节；已通过候选位于 `docs/ArtProduction/Characters/Opus/`，按技能拆出的状态图再进入 Unity。

### 7.3 豆包曲折话术气泡通道

旧 `OB_DoubaoRiver_Base` 与 `OB_DoubaoCrest_Base` 退役。现有 A/B/C 气泡若透明度和轮廓验收通过可复用。

| ID | 尺寸 | Pivot | 说明 |
|---|---:|---:|---|
| OB_DoubaoBubble_A/B/C | 256×128 | (0.5,0.5) | 实体粉蓝气泡，无字；TMP 子物体放短句 |
| OB_DoubaoBubble_Join | 128×128 | (0.5,0.5) | 填补相邻气泡曲线转折处的空隙 |
| OB_DoubaoBubble_Cap | 192×128 | (0.5,0.5) | 通道开头/结尾圆头 |
| VFX_DoubaoPreviewBubble | 256×128 | (0.5,0.5) | 入场前无伤虚线预览泡；以 Alpha 呼吸 |

气泡链由程序沿曲线逐个摆放，不画成 2048px 固定河面。上、下气泡使用同一素材但分别设置旋转范围 `±8°`；不得通过不等比缩放把圆泡拉成长条。

## 8. 弹体与拾取物

目录：`Art/Projectiles/` 与 `Art/Pickups/`。

| ID | 尺寸 | Pivot | 表现 |
|---|---:|---:|---|
| PRJ_DS_Token | 64×64 | 中心 | 蓝白米粒/Token 光弹，朝右 |
| PRJ_DS_Parallel | 64×64 | 中心 | 与基础弹同形，多一条青色边 |
| PRJ_DS_Search | 96×64 | 中心 | 小放大镜弹头，穿透时拖尾 |
| PRJ_DS_RiceBall | 72×72 | 中心 | 蓝白小饭团，自动转向时轮廓仍稳定 |
| PU_HA_ComputeVoucher | 96×96×6 | 中心 | 黑红算力凭证/账单芯片，为 Harness +30 能量 |
| PRJ_GE_StarBlue | 48×48 | 中心 | 蓝色四角星 |
| PRJ_GE_StarPurple | 48×48 | 中心 | 紫色四角星 |
| PRJ_GE_Banana | 96×64 | 中心 | Q 版香蕉、黄光边，不要写实香蕉 |
| PRJ_GE_SpeechBlock | 128×96 | 中心 | 红色实线对话块，内部文字另渲染 |
| PRJ_GE_Citation | 64×64 | 中心 | 脚注星号/链接点 |
| PU_WhiteRice | 96×96 | 中心 | 白饭碗单图；蒸气独立，Transform 轻浮动；不是单粒米 |
| PU_Token | 64×64 | 中心 | 蓝色六角 Token 碎片单图，Transform 旋转/缩放 |

伤害弹外轮廓必须有高对比边。敌弹在最亮背景上仍需达到至少 3:1 的视觉对比；必要时加 2px 深色描边。

## 9. 预警、话术与战斗 VFX

目录：`Assets/_Project/Art/VFX/`

| ID | 纹理尺寸 | 用途与运行时表现 |
|---|---:|---|
| VFX_Hit_Player | 128×128 | 玩家受击红白闪；缩放/Alpha 脉冲 |
| VFX_Hit_Enemy | 96×96 | 敌人受击蓝白星；旋转/缩放/Alpha |
| VFX_Death_LowModel | 192×192 | 旧终端像素碎片源图；ParticleSystem 驱动 |
| VFX_Death_Hallucination | 192×192 | 纸条与泡泡碎片源图；ParticleSystem 驱动 |
| VFX_Shield_Start | 384×384 | 白饭护盾环；缩放和 Alpha 展开 |
| VFX_Shield_Loop | 384×384 | 护盾环；旋转与明暗呼吸 |
| VFX_Shield_End | 384×384 | 可复用护盾环反播缩放/Alpha 收束 |
| VFX_Banana_Path | 512×256 | 可拉伸黄弧预警，柔边 |
| VFX_Dash_Warning | 1024×64 | 冲刺怪红线 |
| VFX_Boss_Phase | 512×512 | 阶段转换星光源图；ParticleSystem/缩放驱动 |
| VFX_Boss_Defeat | 768×768 | 终局像素星爆源图；ParticleSystem 驱动 |
| VFX_Pickup_Rice | 128×128 | 米饭飞向 HUD；曲线路径与缩放由代码驱动 |
| VFX_Pickup_Token | 96×96 | Token 吸收；曲线路径与缩放由代码驱动 |
| VFX_HA_TargetReticle | 512×512 | 黑红虚线选区；旋转/呼吸，合法白边、非法灰边 |
| VFX_HA_ExecuteRing | 512×512 | 0.28 秒内缩放收束并一次结算伤害 |
| VFX_GR_HolyFog | 1024×1024 | 圣光/迷雾遮挡块；噪声 UV/Alpha 流动，无色情画面 |
| VFX_GR_WarningFrame | 512×512 | 洋红预警边框，9-slice 调尺寸并 Alpha 呼吸 |

预警 VFX 颜色规则：黄=即将出现但当前无伤；红=危险判定将生效；白色虚线=纯话术无伤。全游戏统一，不得某关反过来。

## 10. 背景

目录：`Assets/_Project/Art/Backgrounds/`。

每个循环层为 2048×1080、PPU=100，可覆盖 19.2×10.8 并留横向循环余量。禁止把“一张很宽的完整插画”当作循环图；必须同时满足：

- PNG 首列与末列逐像素 RGB 最大通道差=`0`；首尾至少 16px 为同一过渡带。
- 首尾向内 192px 不放唯一地标，并平滑接回主体；横向循环不要求上下衔接。
- 提供两张原尺寸副本并排预览，中央接缝不可见；Unity 中再以两个 SpriteRenderer 连续滚动并回绕验收。
- 一张图只承担一层滚速；远景、近景和可降低透明度的前景遮挡不能烘焙在同一层。

正式循环层与旧装饰图：

| ID | 内容 | Sorting | 滚动倍率 | 约束 |
|---|---|---:|---:|---|
| BG_P0_Far_DataSky_Loop_v01 | P0 深蓝到浅蓝天空、远处数据波与云层 | Background -30 | 0.08 | `PRODUCTION_READY`；2048×1080，边界与 16px 带像素差 0，待 Unity 双副本滚动验收 |
| BG_Far_AICloud_v01 | 旧深蓝答案云概念 | Background -30 | 0 | `DECOR_ONLY`；只可菜单/静态装饰，未通过循环检查 |
| BG_Mid_DataCloud_v01 | 旧数据节点/月云概念 | Background -20 | 0 | `DECOR_ONLY`；只可菜单/章节转场，未通过循环检查 |
| BG_Near_Network_v01 | 旧线缆/浮岛概念 | Background -10 | 0 | `DECOR_ONLY`；无碰撞，未通过循环检查 |
| BG_P0_Mid_DataCloud_Loop | 后续真正循环的中景云/节点 | Background -20 | 0.22 | 尚未生产；不得用旧图顶替 |
| BG_P0_Near_Network_Loop | 后续真正循环的近景线缆/浮岛 | Background -10 | 0.45 | 尚未生产；中央玩家区保持干净 |
| FG_CloudWisps | 透明云丝 | Foreground 0 | 0.70 | Alpha≤0.25，不遮子弹 |

循环验收预览位于 `docs/ArtProduction/Backgrounds/PREVIEW_BG_P0_Far_DataSky_Loop_v01_2X.png`。每章最终合成还必须提供一张 1920×1080 战斗预览，分别叠加蓝弹、紫弹、黄预警、红预警测试可读性。

## 11. UI 资源

目录：`Assets/_Project/Art/UI/`。

| ID | 尺寸 | 类型 | 说明 |
|---|---:|---|---|
| UI_Logo_Main | 1024×384 | Simple | 《鲸鱼娘飞行记》标题；可单独含标题字 |
| UI_Panel_Main | 128×128 | 9-slice | 蓝白半透明圆角板 |
| UI_Panel_Boss | 128×128 | 9-slice | 蓝紫星光边框 |
| UI_Button_Normal | 256×96 | 9-slice | 不含字 |
| UI_Button_Hover | 256×96 | 9-slice | 高亮 |
| UI_Button_Pressed | 256×96 | 9-slice | 下压 4px 视觉 |
| UI_Heart_Full | 64×64 | Simple | 完整饭团/鲸心二选一后统一 |
| UI_Heart_Empty | 64×64 | Simple | 空槽 |
| UI_Energy_Frame | 512×64 | 9-slice | 白饭能量框 |
| UI_Energy_Fill | 32×32 | Sliced | 米白到蓝渐变 |
| UI_HA_Energy_Frame | 512×64 | 9-slice | 黑灰终端框，三段 60 点成本刻度 |
| UI_HA_Energy_Fill | 32×32 | Sliced | 灰白到暗红；低能量不使用全屏红闪 |
| UI_XP_Frame | 512×32 | 9-slice | Token 经验框 |
| UI_XP_Fill | 32×32 | Sliced | 青蓝 |
| UI_BossHP_Frame | 1024×64 | 9-slice | Gemini 星光框 |
| UI_BossHP_Fill | 64×32 | Sliced | 蓝紫双段色 |
| UI_BuffCard | 420×560 | 9-slice | 图标、标题、描述均由 UI 放置 |
| UI_Result_Victory | 768×256 | Simple | 胜利装饰，不含统计字 |
| UI_Result_Defeat | 768×256 | Simple | 失败装饰 |
| UI_DamageIcon_Obstacle | 96×96 | Simple | 结果失败原因 |
| UI_DamageIcon_Bullet | 96×96 | Simple | 结果失败原因 |
| UI_DamageIcon_Boss | 96×96 | Simple | 结果失败原因 |

Buff 图标 256×256，共 6 张：Parallel、Parameter、Quantization、Cache、Context、Search。图标要画成道具符号，不画 6 个相似发光圆。

## 12. 音频清单（美术之外但同批验收）

| ID | 格式 | 长度 | 说明 |
|---|---|---:|---|
| MUS_Menu | OGG | 45–75s loop | 轻松电子+海洋气泡 |
| MUS_Flight | OGG | 90–130s loop | 轻快、低密度，给 SFX 空间 |
| MUS_Boss_Gemini | OGG | 60–90s loop | 双主题左右呼应，第三阶段略滑稽 |
| SFX_Flap | WAV | <0.4s | 软扑翼/气流 |
| SFX_Shoot_DS | WAV | <0.25s | 轻 Token 发射，不刺耳 |
| SFX_Hit_Player | WAV | <0.5s | 明确受伤，不恐怖 |
| SFX_Shield_Start/Block/End | WAV | 各<0.8s | 三种必须可区分 |
| SFX_Rice/Token | WAV | <0.5s | 两资源音高不同 |
| SFX_Boss_Phase/Defeat | WAV | <1.5s | 阶段、击破 |
| SFX_UI_Hover/Click/Back | WAV | <0.3s | UI 三件套 |

不使用“中国人会飞”流行歌曲原音；如需音乐梗，只原创一个“起飞”气流动机。

## 13. 每张素材的交付卡

美术交付时必须附同名 `.md` 或资产表字段：

```text
Asset ID:
Source/Author:
License:
Canvas/Cell Size:
Visible Bounds:
Pivot:
Facing:
Frame Count/FPS:
Loop:
Expected PPU:
Collider Note:
Known AI-generation fixes:
```

没有作者/许可证字段的外部素材不得进入正式包，即使项目非商业开源。

## 14. 视觉验收门槛

每批资产通过以下检查才算完成：

1. 100% 尺寸查看：无残肢、多指、眼睛错位、透明脏边。
2. 游戏尺寸查看：主轮廓、表情、危险颜色仍可读。
3. 灰度查看：玩家、敌人、弹幕不融成一团。
4. 黑色剪影查看：DeepSeek 和 Gemini 可凭轮廓区分。
5. 状态切换叠图：核心身体不跳，Pivot 一致；Transform 动画不改变物理根。
6. 背景先通过像素接缝与双副本预览，再做四种战斗颜色叠弹测试。
7. Unity 实机：Filter、PPU、切片、Pivot、Order 与本表一致。
8. 许可证：来源记录齐全；社区形象注明二创，不写“官方”。

## 15. 后续角色生图前置条件

未来由 AI 生成/辅助绘制其他 AI 娘时，顺序固定：

1. 在 `02_ContentMemeBible.md` 锁定 A/B/C/D 标签。
2. 收齐官方/创作者参考图，并记录允许使用范围。
3. 先做 1 张正面、1 张 3/4、1 张背面角色 Turnaround。
4. 造型通过视觉检查后再做表情表；本轮已通过候选不重复请求同一造型确认。
5. 表情表通过后按当前玩法逐张做状态图，不允许一上来批量生成几十张动作图。
6. 最终导出必须符合本清单的格子、Pivot、轮廓和透明要求。

## 16. 已锁定的后续角色视觉卡

这些角色暂不属于第一关资产量，但造型已由用户补充，不得在后续重新“自由设计”。

### 16.1 Kimi K3 大小姐

- 形态：2.7 头身银白长发大小姐，长发呈大波浪并占据明显背部轮廓。
- 服装：午夜蓝+月白的华丽礼服/女仆式结构，白色荷叶边头饰，深蓝星图披肩，大蝴蝶结，银链与 K 字挂饰。
- 核心道具：横持银色长笛；演奏姿态是主要动作，不能换成法杖。
- 纹样：新月、星点、五线谱/音符；三角光谱只作小型抽象发饰，避免照抄特定唱片封面。
- 表情：从容、闭眼演奏、略有贵族距离感；不是活泼偶像。
- 主色占比：月白 45%、午夜蓝 45%、银 7%、彩虹点缀≤3%。
- Q 版删减顺序：先删腰间小链→减少裙面碎星→简化袖口；银发、长笛、星月披肩、K 蝴蝶结绝不能删。

### 16.2 DeepSeek Harness 黑鲸鱼娘

- 形态：与 DeepSeek 鲸鱼娘同族的 2.6 头身黑色终端变体。
- 头部：黑色长发、呆毛、红眼、细框眼镜、两侧黑色鲸鳍耳；鳍耳外伸且根部位置低于猫耳。
- 身体：黑白女仆裙与白围裙，黑袖，红花/蝴蝶结和红裙边；黑色不超过连续画面 70%，用白围裙和灰色内衬分层。
- 尾部：粗大的黑鲸尾，腹面浅灰/白，必须从背后清楚露出，不画成细猫尾。
- 配件：小型终端状态灯、任务队列卡；红色用于等待/告警，不把全身描成红光。
- 表情：夜班工程师式温柔疲惫、轻微腹黑；眼镜反光不得长期遮眼。
- Q 版删减顺序：先删裙边刺绣→简化发卷→减少小红饰；鲸鳍耳、鲸尾、眼镜、黑白红色块绝不能删。

### 16.3 Codex 方块外星桌宠

- 不做人类娘。主体是奇形怪状的像素方块生物，像外星设备与小怪兽的混合。
- 原型参考：`docs/AiSister/CodexPet.png` 的蓝色云团/软体轮廓、深蓝终端脸与青色 `>_` 光标。
- 游戏演绎：用户已批准现有方块/外星终端桌宠概念继续使用；轮廓可保持近似立方/多面体、左右不完全对称、短突起和短脚，但应补足蓝/青终端识别，不要求逐像素复制截图。
- 状态语义：Idle、Thinking、Working、Waiting、Success、Failed 各一张；轻微形变、分层旋转、黄闪和像素喷花由 Transform/颜色/VFX 表达。
- 像素风格可保留，但与本作非像素角色共存时使用清晰 4–6px 等效像素块和统一外描边，避免分辨率过低像马赛克污点。
- 主色锁定为钴蓝、靛蓝、青色发光；紫色只能作为阴影过渡，不能压过蓝色主体。
- 禁止加女性头发、裙子或普通外星人触角来“娘化”；它的非人形就是辨识点。

### 16.4 Claude Code 橙色章鱼工程师

- 不做人类娘。`docs/AiSister/ClaudeCode.png` 的扁平橙色像素多足生物只作为原型，不要求一比一临摹。
- 用户已批准现有圆润橙色章鱼工程师概念继续使用；它与 Codex 一样属于非人 Q 版吉祥物。
- 轮廓必须是低矮橙色头体+多条短触手；不能加长发、裙摆和人类长腿。
- 状态道具：终端、锤子、Git 分支、测试报告；同一帧最多显示 4 件，避免缩小后杂乱。
- 状态：Idle、Working、Waiting、Success、Failed 各一张；触手轻摆、终端闪动和完成打勾由 Transform/颜色/VFX 表达。

### 16.5 其余用户锁定人物

以下人物的完整“必须保留/允许简化/禁止误读”以 `docs/AiSister/README.md` 为唯一视觉索引：

- ChatGPT：银白龙娘大魔王。
- Claude/Opus：橙发学者，Opus 使用巨大半身跃起。
- Gemini：蓝紫猫耳猫尾、异色瞳和彩虹星；旧无猫版退役。
- Grok：金发恶魔翼、黑红金、X 形巨戟；只输出圣光/迷雾梗。
- 豆包：棕色短发办公助理头像，不做豆沙包发髻。
- 即梦：白色渐变双马尾、星饰/星瞳、圆眼镜、视频控制台。
- GLM、Qwen、MiniMax、OpenCode、Zcode：严格按索引的轮廓与道具执行。

任何尚未完成生产稿的角色必须先有四向/表情设定稿并通过视觉检查，才能进入本清单前半部分的状态图生产。本轮表内候选已由用户整体通过；后续只在对应玩法落地时生产必需状态，不再按旧多帧预算批量生图。

### 16.6 2026-08-31 候选文件索引

| 角色 | 候选文件 | 当前结论 |
|---|---|---|
| Gemini | `docs/ArtProduction/Characters/Gemini/CONCEPT_GE_Turnaround_v02_CANDIDATE.png` | 视觉通过；关键识别已修正，待按玩法生产状态图 |
| ChatGPT | `docs/ArtProduction/Characters/ChatGPT/CONCEPT_GPT_Turnaround_v01_CANDIDATE.png` | 视觉通过；白龙娘方向，待按玩法生产状态图 |
| 豆包 | `docs/ArtProduction/Characters/Doubao/CONCEPT_DB_EventPortraits_v01_CANDIDATE.png` | 视觉通过；待拆头像/气泡 UI |
| Opus | `docs/ArtProduction/Characters/Opus/CONCEPT_OP_GiantJumpHalf_v02_CANDIDATE_ALPHA.png` | 视觉/透明通过；v01 假透明禁用，待按玩法生产状态图 |
| Grok | `docs/ArtProduction/Characters/Grok/CONCEPT_GR_Turnaround_v01_CANDIDATE.png` | 视觉通过；X 巨戟与黑红金，待按玩法生产状态图 |
| 即梦 | `docs/ArtProduction/Characters/Jimeng/CONCEPT_JM_TurnaroundOperator_v01_CANDIDATE.png` | 视觉通过；人物和控制台待拆分生产 |
| GLM | `docs/ArtProduction/Characters/GLM/CONCEPT_GL_Turnaround_v01_CANDIDATE.png` | 视觉通过；黑白 Z 巨尾，待按玩法生产状态图 |
| Qwen | `docs/ArtProduction/Characters/Qwen/CONCEPT_QW_Turnaround_v01_CANDIDATE.png` | 视觉通过；蓝白中式大小姐，待按玩法生产状态图 |
| MiniMax | `docs/ArtProduction/Characters/MiniMax/CONCEPT_MM_TurnaroundMedia_v01_CANDIDATE.png` | 视觉通过；海螺/录音/场记待拆分生产 |
| OpenCode | `docs/ArtProduction/Characters/OpenCode/CONCEPT_OC_TurnaroundLocalAgent_v01_CANDIDATE.png` | 视觉通过；机械鹿角本地终端，待按玩法生产状态图 |
| Zcode | `docs/ArtProduction/Characters/Zcode/CONCEPT_ZC_TurnaroundBlueprint_v01_CANDIDATE.png` | 视觉通过；白狐蓝图，待按玩法生产状态图 |

完整输入参考、提示词硬约束和退役记录见 `docs/ArtProduction/GENERATION_LOG_20260831.md`。
