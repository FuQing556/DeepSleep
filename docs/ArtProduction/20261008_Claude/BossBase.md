# Claude 基础受击体与护罩 · 2026-10-08

最新纠正：橙色命中图是Claude攻击玩家的特效。BossBarrierFeedback2D只负责受击变暗/恢复，不再播它；DamageHitbox.UseReceiverHitFeedback=false，由DS/HS原武器系统播放攻击者命中特效，仍截断穿透束。现有橙色池转给三个Claude主动攻击共享。下文“Boss自有碎光”及UseReceiverHitFeedback开启为旧错误实现，已作废。检查与Kimi审计见docs/BossHitFeedbackAudit.md。

用户最终选择第一张护罩（exec-5cd34171-ff13-42e3-a314-05ce7241e88b.png）。正式与raw/VFX_CL_Barrier_v01.png SHA256均为e3bd9207dd519b9e4aed64bfbdceff88bdfca7f4e9a2a3d231da888f16889ecc；像素未改。raw下旧文件名RETIRED仅为本轮中途判断的历史，不代表最终退役。v02未接入。

使用deepsleep-art-generation、imagegen生图，deepsleep-sprite-matting检查Alpha；没有抠图/填边/全局改色。第一张护罩1254方图，Alpha0～254，中心Alpha1/255，因此严格clear-point检测未通过；白/暗/天空底人工检查显示中心近乎空透，不承诺逐像素完全透明。左右尖端贴边保留用户选择。报告与三底预览在previews/selected_barrier。命中图三底检查在previews/base_audit。

## 生成记录

护罩使用内置imagegen，新生成透明背景；参考实际Claude常态与Kimi护罩。实际提示词：

> Use case: stylized-concept. Generate one isolated 2D game VFX sprite, Claude's spherical protection barrier. Image 1 supplies ivory/orange/amber character palette only; image 2 supplies hollow barrier readability and restrained anime energy rendering only. Front-facing perfect circular hollow shield rim, warm ivory-white luminous edge with amber-orange precise geometric facets, concentric thin arcs and small polygonal light accents. Large genuinely transparent center, so the character face remains entirely visible. A soft thin translucent curved-shell suggestion confined close to the circumference; no filled glowing disk. Symmetric readable circular boundary with gentle light falloff, contained completely inside a square canvas with generous margin. No moon/crescent, character, platform, scene, UI or lettering. Clean detailed anime game effect, not an ornate massive magic diagram. Genuine transparent background including center.

命中图使用内置imagegen，新生成透明背景，无图片参考。原稿exec-5295377f-556d-4bef-a8e3-5a1174a79406.png，正式/raw哈希b5556de0e0fd0ec57011885b8368d360d53e89165caf62ef06a4a5d96d86ab06。实际提示词：

> Use case: stylized-concept. One isolated compact 2D anime game hit-flash sprite for an amber-orange scientific energy barrier. Small ivory-white central glint, a broken short circular arc and a few sharp orange polygonal fragments radiating outward, subtle warm translucent halo. Clearly asymmetric impact, crisp readable geometry with restrained bloom; not a whole magic shield and not a fire explosion. All fragments and halo stay well inside the middle 65% of a square canvas, generous genuinely transparent margin. No person, scenery, moon, text or UI. True transparent background.

## 接入范围

- Assets/_Project/Prefabs/Combat/Encounters/Claude/PF_CL_BossBase.prefab：人物/无人机/盾/命中池/残影分层；通过Unity API创建并连接原场景根，未写YAML。身体与Kimi有效高度同为1209px，PPU512、缩放1。球体radius2.6、offset(0,1.15603113)，Trigger，默认禁用。
- BossDamageBody2D仅接受未来遭遇提供HP和阶段阈值，不设置Claude隐藏默认HP；阶段技能边界解锁。ApplyReplica只提供基础接口，尚未接网络传输。
- DamageHitbox使用球面真实命中，不固定数字锚点；StopsPiercingBeams开启，UseReceiverHitFeedback开启。这是接口配置验收，不是DS/HS实战验收。
- BossBarrierFeedback2D常态Alpha0.8、受击0.5、恢复0.1游戏秒，命中放专属碎光；护罩跟随球体中心和尺寸。姿态残影复用SpritePoseTransition2D和Kimi现有配置，未复制完整Kimi技能系统。
- PF_VFX_CL_Hit与CFG_CL_Hit复用通用一次性表现池，32预热/128上限、时长0.28，基础worldDiameter0.4（Player全局倍率3，约1.2u）。这些仅表现初值，无伤害配置。

## 验证与缺口

ClaudeBossBaseChecks 17项、ClaudePresentationChecks 19项通过，Unity脚本编译无Error。测试100HP/.5阈值只是隔离fixture，不是批准的Claude数值。检查启动隐藏、配置引用、所选图、受击、暗闪恢复、锁血、解锁、死亡一次及Reset、Replica禁用伤害、旧出退场。

未做Play/设备/真实HS束线命中验收。章节20秒接管、正式BossHP/阈值、接触伤害、移动/技能、权限书、HUD、音效、网络消息均未接入；基础预制体默认隐藏，不改变当前四波结算。不是完整可玩Boss，不打包、不上传。
