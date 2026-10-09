# Claude 本体、动作与血条 · 2026-10-08

## 本轮完成范围

用户确认正式首版10000HP、半血转二阶段、球罩接触1伤；接触间隔1游戏秒、击退0.65u/0.18秒，沿用Kimi当前值。新增CFG_CL_Boss；不会将测试fixture生命当平衡值。

ClaudeBossActor2D复用BossDamageBody2D生命、SpritePoseTransition2D残影、PlayerDamageReceiver2D实际扣血击退和现有Claude橙色命中池。只有正式遭遇调用BeginAuthority/SimulateContact才开始；不是另一套自动战斗循环。阶段接口先不可受伤/转阶段姿态，完成后提交阶段解锁。死亡切战败姿态并关闭球体，Reset回常态。生命只有Body持有，Actor没有第二份HP。

新增Move/PhaseChange/Defeated三个单图；此前SealCast候选检查后正式导入。8个姿态已绑定。PPU512、中心Pivot；原图像素保留，人物和无人机拆层。移动姿态已准备，但躲避/穿屏移动算法仍未实现。

PF_UI_CL_BossHud沿用Kimi原有uGUI布局、SafeAreaRectFitter和RectMask2D，不修改Kimi组件或UI。Claude新生成橙金边框和橙色Fill，暗底复用已生成Kimi素材。Frame仅Unity SpriteRect取可见范围(17,273,2139,236)，Fill(42,455,1505,89)，源PNG不裁改。InnerSlotMask583×16.5像素；默认隐藏，直接读取Body生命/阶段。

KimiEncounter2D球罩接触伤害成功后追加Laser.HitEffects（已装配的共享Kimi池），未改伤害、冷却、护盾拦截和击退参数。360援兵依旧自己的特效。

## 验证和限制

ClaudeActorChecks19项通过：装配/用户数值、HP血条绑定及裁切、默认隐藏、10000HP、5000锁血、阶段解锁、移动/战败姿态、真实球体接触1伤/击退/冷却/命中特效、清理无幽灵伤害、护盾挡伤时不击退但播放命中。基础17/短斩47/全屏2622/能量60回归通过；编译Console无Error。

本次只完成本体段，World02默认仍不自动启动Claude。技能轴、移动躲避/四边穿屏、20秒章节接管、阶段时长、独立权限轴频率、胜利/失败全模块清理、音效、AI、联网、图鉴仍需后续串联。无自然Play/手机/双端验收，不称Boss已完成。ClaudeBossBase_HUD.png的Editor静态相机尝试未捕获uGUI且混入其他载入场景角色，不交付为血条验收图；待整场接入后GameView检查。Kimi本轮只改接触命中特效，未做整场Kimi新验收。未打包上传。

## 美术生产记录

使用deepsleep-art-generation + imagegen，deepsleep-sprite-matting inspect_sprite.py检查原生Alpha，不重抠。三底预览在previews/poses_audit及hud_audit；已逐张实际查看。母图为Assets/_Project/Art/Characters/Claude/SPR_CL_IdleHover.png；UI各自以Kimi Frame/Fill作参考，均实际查看后使用。

新图SHA256：
- Move:4adb94f946278aad71109c4fe5904d4cbf5753ebb3ac7f98c30ed62694e655fb
- PhaseChange:ed0926f19bc7b8034d25ec7fa85ae8f6dcc44d8e4d446b5847d65d9f65c68189
- Defeated:75b3301e6f58dba524528eb12a1d734fefcb77a59e6fc1aacdf7e32a62cd8461
- Seal（旧候选）:9a2eb988a9d501859e4457ece059d9587017b6a6352e54d137bc124d35498d42
- HealthFrame:0ae9ac35a50bee0b96207c4e773c767f056b2535c8aa1394d4ab028768d816e4
- HealthFill:40533e377c96179012289e0181d40a5059cda27d5ed37a8c0b2e912cf809c0bf

Alpha报告保存尺寸/bbox/hash。人物主体Alpha>8高度1155/1192/1188px，对照常态1209px，保持同PPU512，未用单图Transform缩放补大小。

实际生成提示词：

### Move

Create a single transparent-background full-body game sprite of the exact orange-haired chibi girl in the supplied reference. Keep her face, hair flower, black-white-orange dress, boots and rendering style. New action: gliding quickly toward the left, a slight coherent forward lean, hair and skirt gently trailing right, arms held naturally for balance, legs together with a small natural bend. Natural untwisted anatomy, both boots visible. Character only, no drone, no ground, no motion streaks or magic. Preserve large head and small body proportions from reference, centered on a square canvas, ample clear margins, entire body visible.

### PhaseChange

One transparent full-body game sprite, a new phase-transition pose of the orange-haired chibi girl in the reference. Exact same character identity, black-white-orange dress and flower hair accessory, same anime game rendering and chibi proportions. She floats upright, closes her eyes with a calm resolute expression, brings her two hands together just before her chest in a small composed spell gesture. Hair softly lifted, feet hanging naturally, anatomically coherent. No drone, no magic or glow, no ground, no background. Entire body visible with clear margin on square canvas.

### Defeated

Single transparent full-body game sprite of the exact chibi orange-haired girl in the supplied reference, same identity, orange flower, detailed white-black-orange dress and white boots, same anime rendering and proportions. A defeated but dignified floating pose: she gently lowers her head, tired eyes closed, shoulders relaxed, hands hanging loosely near the skirt. Legs naturally hanging close together. Slight soft droop in her hair. No injuries, no sitting, no ground, no drone, no effects. All body parts visible, square canvas with clear margins.

### HealthFrame

Edit the supplied transparent game boss health-bar frame. Preserve the very long slim horizontal frame layout and its large empty transparent interior opening, transparent canvas, fine anime-game craftsmanship. Replace the purple/navy moon decoration with Claude's amber-orange scientific geometric identity: ivory and champagne-gold metal rim, muted warm black-bronze ends, central small orange faceted star/flower geometric crest. No crescent moon. Restrained glow, thin frame, no text, no filled bar, no background. Keep the long inner opening clean for a separately layered health fill.

### HealthFill

Edit this isolated transparent boss health fill sprite. Keep the same long slim rounded bar geometry, same position and margins on the canvas and transparent background. Replace all blue-purple energy by luminous warm amber-orange, ivory-white highlights and delicate gold geometric threads, matching Claude's scientific orange effects. Soft restrained brightness, not fire. No frame, no crest, no letters. Only the filled bar itself on transparency.
