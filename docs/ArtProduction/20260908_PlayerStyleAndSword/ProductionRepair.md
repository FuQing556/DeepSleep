# 六帧动作生产检查（2026-09-08）

## 本轮结果

- 使用内置 image_gen 修订原六帧表，输出保存在同目录 `HA_Swing6_Repair_Candidate_v02.png`；原稿保留。
- 已去掉原帧表中大部分独立剑气弧线，角色仍保留实体剑刃及红色发光。
- PNG 文件头 Color Type=2、Bit Depth=8，即 RGB，无 Alpha 通道；背景棋盘格是实际像素，不是查看器透明预览。
- 第四帧剑尖仍跨越512像素格边界；人物位置仍有漂移，首尾姿态不完全相同。未达到生产验收，不导入 Assets、不改角色或碰撞体。
- 不能按等宽网格直接切片并声称可用，也不能用隐藏背景的临时 Shader 充当合格透明图。

## 下一步建议与停点

需用户确认改用本地精确图像处理：去背景、独立提取完整帧轮廓、统一画布并按躯干锚点对齐。必须保护服装白色和半透明剑刃边缘，不可全局按白色删除。以实际效果验收，不能保证自动抠图无需修边。

确认后先完成可播放的透明动作包，再接 Unity 动作表现；当前没有完成近战攻击、伤害或清弹。输入源码核对显示 CommandButtonState 已有 Pressed/Held/Released，UnityInputCommandSource 已传递 ConfirmAim 按钮保持状态，不需因为帧动画另建一套设备输入。

动画播放和玩法命中应分离：动作配置提供帧序列与时长，攻击逻辑在明确的命中时刻仅结算一次；不按每张图片重复扣血。具体实现尚未编写。

## 完整提示词

Use case: identity-preserve. Production repair of the six-frame flying sword animation sheet in reference 1. Reference 2 is the authoritative original character identity/style; retain her precise black-white-red orca maid design, small glasses, red eyes, long black hair, red flower, fin ears, tail, clothing and cute proportions. Output ONE clean 3-column by 2-row sprite sheet on a GENUINELY TRANSPARENT alpha background, NOT gray, white or checkerboard pixels. Six equal square cells on a 1536x1024 canvas. Same continuous two-handed diagonal sword cut sequence as reference 1: ready, overhead windup, forward-down strike, lower-right followthrough, recovery, ready. Keep flying bent legs. Critical repairs: every frame including whole sword and its glow stays inside its own 512x512 cell with at least 24px padding; use same character scale and same pelvis anchor at relative cell (50%,62%) in ALL cells. Leave room for sword by modestly reducing all six figures uniformly, never individually. Final ready frame matches first ready frame precisely. Keep actual blade in hands but REMOVE the large baked slash crescents/trails; separate sword-wave VFX will be rendered by game. No text, numbers, grid borders, backdrop, ground, shadows. Crisp consistent thin dark anime linework, no extra limbs. Actual transparent PNG alpha is essential, do not draw a transparency checkerboard.
