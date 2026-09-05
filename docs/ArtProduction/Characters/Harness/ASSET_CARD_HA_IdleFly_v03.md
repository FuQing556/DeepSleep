# Harness 待机飞行图交付卡 v03

## 身份

- Asset ID：`SPR_HA_IdleFly_v03`
- 当前状态：用户已确认；透明生产图已进入 `Assets` 并完成 Unity 导入与 Harness Prefab 替换
- 主文件：`SPR_HA_IdleFly_v03_CANDIDATE_ALPHA.png`
- Unity 文件：`Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png`
- 对比预览：`PREVIEW_DS_HA_GameScale_v01.png`
- 来源/作者：OpenAI ImageGen 生成；Codex 依据项目角色参考进行色键背景去除与边缘去色
- 许可记录：项目自产 AI 候选素材；未直接嵌入第三方原图

## 美术规格

- 角色：Harness，红黑鲸鱼娘
- 用途：玩家角色默认待机/飞行状态
- 朝向：面向右侧；头发、鲸尾向左形成飞行拖曳
- 动作区分：双手操作红黑终端，不复用 DeepSeek 抱饭碗姿势
- 关键识别点：黑发、红瞳、圆框眼镜、鲸鳍耳、黑白粗鲸尾、黑白女仆装、红色饰件、红黑终端
- 画风：二次元 Q 版，约 2.5–2.7 头身，粗净轮廓，高对比块面上色
- 画布：1254 × 1254 px，RGBA 32 位真透明
- 可见范围（Alpha ≥ 8）：X 79–1130，Y 60–1127
- 可见尺寸：1052 × 1068 px
- 安全留白：左 79 / 上 60 / 右 123 / 下 126 px

## Unity 导入建议

- Texture Type：Sprite (2D and UI)
- Sprite Mode：Single
- Mesh Type：Tight；当前透明轮廓与游戏缩放检查通过，可减少大面积透明区域的过度绘制
- Pixels Per Unit：512
- Pivot：Center `(0.5, 0.5)`，与 DeepSeek 基准一致
- Filter Mode：Bilinear
- Generate Mip Maps：关闭
- Alpha Is Transparency：开启
- Max Size：2048 或更高，禁止压到 1024
- Compression：首轮原型设为 None；移动端压缩在真机清晰度测试后再定
- 同 PPU 可见世界尺寸：约 2.055 × 2.086 units
- Sorting Layer：Gameplay
- Sorting Group：挂在玩家根对象；基础 Order 20，运行时由统一 Y-sort 组件接管前后顺序
- Physics Layer：Player
- Collider：与 DeepSeek 共用公平命中盒，只包身体核心，不包头发、耳鳍、终端和鲸尾；`CapsuleCollider2D Size (0.39, 0.90)`、`Offset (0.26, -0.04)`，当前素材叠图检查通过

## 动画/状态

- 帧数：1 帧基础立绘
- FPS：不适用
- 循环：由程序驱动的轻微浮动、移动倾斜与状态残影淡出构成，不在本图内烘焙
- 后续派生：受击、爆发施法、清弹幕、倒地/待复活；均以本图的脸型、服装和尾部比例为一致性基准

## 技术检查

- 已检查：真实 Alpha、透明像素、深色底、浅色底、游戏蓝底缩放
- 已通过：发丝、鲸尾白边、围裙、鞋、终端轮廓无明显绿色污染或破洞
- 与 DeepSeek 同 PPU：Harness 略矮、横向轮廓更宽，符合重型清屏/爆发位的视觉职责
- 已知废稿：`SPR_HA_IdleFly_v02_CANDIDATE_ALPHA.png` 及其 v02 预览存在色键残点，只保留作过程记录，不得导入游戏

## 生成摘要

以 DeepSeek 游戏内小人作为严格画风与头身比例基准，以 Harness 转面稿和用户提供的黑色鲸鱼娘图作为身份参考；生成一名向右飞行的红黑鲸鱼女仆，操作红黑终端，头发与鲸尾向左拖曳，姿势明显区别于 DeepSeek。生成阶段使用高饱和绿幕，随后本地提取真实透明通道并去除绿色边缘污染。
