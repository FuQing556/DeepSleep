# Kimi 夜晚堤岸与 Claude 移动图 · 2026-10-09

## 采用结果

- `ready/FG_W01_Riverbanks_Night_v01.png`：1774×887，原生 Alpha。正式新增到 `Assets/_Project/Art/Foregrounds/World01/`，左右切片沿用黄昏图的 Rect `(0,40,887,650)` / `(887,40,887,650)`、PPU 100、底部中心 Pivot。不是运行时压暗，也没有覆盖黄昏图。
- `ready/SPR_CL_Move.png`：1254×1254，原生 Alpha。替换同名正式移动图，保留已有 meta/GUID、PPU 512 和中心 Pivot；没有改角色根缩放、盾、飞行器或碰撞。旧图只读备份在 `raw/ClaudeMove_original.png`。
- `raw/ClaudeMove_rejected_v01.png` 未采用：头身比例改善不足，而且带多余光晕。第二次仅用常态母图作参考，不再让旧移动图带偏比例。
- 原生透明无需抠图或重新色键，未调整内部 RGB、非等比缩放或裁剪。使用项目透明素材检查技能输出白/暗/蓝底及 Alpha 报告，并逐张查看。

SHA256：夜晚图 `912339283a18c5d3ac02597a961d674be8a5df783c7f1952b22b749ea4e911f9`；新移动图 `609b0a78afaf2cddf14c9e0ffb912530c59cab175ebd2d9d8d457ae34623919b`；旧移动备份 `4adb94f946278aad71109c4fe5904d4cbf5753ebb3ac7f98c30ed62694e655fb`。

## 生成方式与实际提示词

均使用内置 `image_gen.imagegen` 图片编辑模式、绝对路径参考、`transparent_background=true`。遵循 `deepsleep-art-generation` 的现有母图/比例对齐要求。

夜晚图参考：`FG_W01_Riverbanks_v02.png`（编辑目标）与 `BG_W01_MoonRiver_Panorama_v01.png`（月光色调）。提示词：

> Edit the first reference image into a moonlit NIGHT version of this transparent foreground atlas. Preserve the two riverbank islands' exact placement, silhouettes, scale and composition: tree and shrubs on the left, stone bank, bushes, steps, stool and jar on the right, with the wide empty transparent center. Match the cool blue navy moonlight of the second reference panorama, subtle silver light on foliage and stone edges, no golden sunset lighting. Keep the original painted game-background style and all bank geometry. No sky, water, background or new objects; true transparent background. This is an aligned night replacement for the original 2:1 atlas, not a new scene.

采用的 Claude 移动图仅参考 `SPR_CL_IdleHover.png`。提示词：

> Make a NEW movement-pose game sprite of this exact character, following the approved reference's art style and proportions closely. Same orange-haired scholarly chibi girl, face, flower ornament, dress and boots. Keep the SAME head size relative to the torso and legs as in the reference. Full body gently leaning left while gliding on a separate platform, hair flowing behind to the right, hands relaxed and legs naturally balanced. Do not make the head larger. Isolated character only on fully transparent background. No glow, no aura, no shadow, no drone.

## 接入与验证边界

`KimiNightForegroundInstaller.Install()` 显式绑定两个堤岸前景及其子物体旧图渐变层，保留原摆位；覆盖 World01 正式与图鉴共用场景，以及 World02 里保留的旧 Kimi 装配。World02 的两个堤岸 Renderer 原本禁用，保留禁用状态；没有给 Claude 单独增加月夜堤岸切换机制，也不启用原本禁用的前景。

Kimi 权威接管与客机 ApplyReplica 共用前景切换，沿用背景渐变时长；胜利回黄昏，取消/退场立即复原。渐变层无碰撞，随父堤岸的位置和缩放。

验收：夜晚切片几何、渐变中点/终点、重复副本不重播、回黄昏、即时取消；Claude 原出退场与本体/HUD 隔离回归。未自然实战或双端设备验证，未打包、未上传。

`previews/KimiNight_scene_static.png` 是正式场景副本中仅开启背景与左右堤岸的 Unity 相机静态渲染，不是 Boss 实战截图。初次诊断截图清理时未先解除 Camera.targetTexture，留下了一条释放 RenderTexture 的 Console 记录；后续截图已修正清理顺序，生产代码未使用该截图逻辑。

`previews/ClaudeMove_comparison.png` 从左到右：常态、旧移动、新移动；三图按同一画布比例展示。

美术仍待玩家在实际对局中确认。

## 后续退出异常修复

用户实测发现前景已销毁后驱动/网络 `OnDisable` 仍调用复位，导致 `MissingReferenceException`，不是前面的截图 RenderTexture 记录。已修正即时复位与初始缓存对已销毁 Renderer 的访问；仍存活的前景恢复黄昏，渐变层关闭，正常切换不变。没有吞异常或删除清理行为。

`VerifyExitCleanup()` 在两份场景副本中覆盖不同销毁顺序、缓存前退出及驱动/网络重复退出；36项通过，日志监听没有新异常。原昼夜18项继续通过。先前61项隔离检查不代表已覆盖退出生命周期，本轮补齐上述范围；仍未做自然实战或双端设备验收。控制台修复前的旧异常记录未清除。
