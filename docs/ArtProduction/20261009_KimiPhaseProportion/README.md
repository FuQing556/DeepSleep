# Kimi 转阶段头身比例修正 · 2026-10-09

用户指出旧转阶段头偏小。本批使用 `deepsleep-art-generation` 与内置 `image_gen.imagegen` 编辑模式，以原转阶段图为动作目标、当前常态图为头身比例参考，重新生成一张闭眼托笛姿态。不增加云台、特效或新动作。

- 编辑目标：`Assets/_Project/Art/Characters/Kimi/SPR_KI_PhaseChange_v01.png`（修改前）。
- 比例参考：`Assets/_Project/Art/Characters/Kimi/SPR_KI_Idle_v01.png`。
- 旧图备份：`raw/SPR_KI_PhaseChange_original.png`。
- 新版原生透明稿：`ready/SPR_KI_PhaseChange_v02.png`。
- 正式替换同名 `SPR_KI_PhaseChange_v01.png` 内容，保留 meta、GUID、Sprite fileID、全部姿态引用。未修改配置、根缩放、云台、碰撞、护罩或转阶段时长。

## 实际提示词

> Edit image 1, the full-body Kimi phase-change game sprite. Image 2 is the approved same-character head-to-body proportion and drawing-style reference. Correct the undersized head in image 1: use image 2's larger cute chibi head and compact body proportions. Retain image 1's closed eyes, calmly raised chin, both hands holding the silver flute at chest height, and gently flowing hair/cape. Same silver-lavender-haired girl, same midnight-blue moon/star outfit and head accessories. Natural connected anatomy, full character visible with transparent margin. Keep her feet near the bottom center, approximately the same total character height as the reference, square canvas. No cloud, scenery, glow, particles or shadow. Genuine transparent background. One finished sprite.

调用 `transparent_background=true`，两个绝对路径参考。生成源为 `C:/Users/Administrator/.codex/generated_images/01a09de0-b0b2-7ac0-8982-9b5e3fda84af/exec-81f7e2c5-66dd-487c-ad2a-c42805fb58ec.png`。直接复制原生 Alpha 输出，未抠图、裁剪或后期缩放。

## 验证与边界

新旧均1254×1254；Alpha≥20的可见范围（左、上、右、下，源图像素）旧图 `(21,15,1238,1226)`，新图 `(29,17,1243,1244)`。新版完全透明像素728653，原生半透明边缘保留；头发、裙摆、脚完整。脚底较旧图低18像素（512 PPU约0.035单位），未额外改锚点掩盖姿态差异。

Unity强制重新导入通过，PPU512，底部中心Pivot归一化 `(0.5,0.028)`（像素627,35.112），无MipMap，Single，来源有Alpha。GUID `eb688d1cf9793494ea3c11b62e5a6450`，Unity实际读取Sprite fileID `21300000`（不是meta内旧切片表的ID），CFG_KI_Boss的PhaseChange引用仍有效、配置校验通过，控制台无错误。

已查看全身并与常态参考对比。未自然实战或双端验收、打包上传；最终观感待用户在游戏中确认。
