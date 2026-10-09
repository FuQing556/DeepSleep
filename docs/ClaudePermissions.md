# Claude 权限书接入 · 2026-10-08

最新书页标识（覆盖下文旧按钮标签描述）：复用主菜单PortraitHalo的DS/HS CircleBase与CircleFrame，不生成新素材。PageInscription移到书本中心(local 0,0.12,0)，圆圈Alpha 0.48、外圈0.42，文字仅“移动／攻击／技能”，身份按受封角色固定蓝/黑红、不跟全局菜单主题切换。右上/右下只镜像书图，文字及圆圈不翻转；每批按抽取角落重新设置flipX，复用不会留错朝向。碰撞、HP和封禁时长不变。1790项隔离Editor检查通过；PermissionPagesCentered_16x9.png为静态布局预览，非完整战斗/手机验收。此前使用deepsleep-sprite-matting检查现有圆圈Alpha，没有重抠或改像素。

本轮只落实已批准的权限书链，不宣称完成Claude战斗。数值：一/二阶段书100/200HP；预警1.5真实战斗秒，然后封禁10真实战斗秒；破书/到期/对应角色倒地归还该书权限。预警阶段可攻击书本并提前阻止封禁。计时由未来遭遇驱动输入暂停感知现实战斗秒数，不受2倍/0.5倍场景状态改变。已有护航/剑挥击仍由原模拟器结算；HS技能模式被封时不能起新刀。

## 文件与职责

- `Runtime/Combat/Encounters/Claude/ClaudePermissionConfig.cs`与`Assets/_Project/Configs/Combat/Encounters/Claude/CFG_CL_Permissions.asset`：获批准数值与两角色/三权限/预警封禁文案。
- `ClaudePermissionBook2D`：一书一角色一权限，固定实例复用，状态Hidden→Warning→Sealed→Hidden；书本自身作为行动门禁owner，清理不影响救援/倒地或其他书。通过生命事件即时清理；共享DamageHitbox、AI感知及SpriteHitFlash。
- `ClaudePermissionModule2D`：显式绑定四书、四角、DS/HS的生命与门禁、Session/Registry。BeginBatch(phaseTwo,seed)均匀抽合法权限集合，再无放回抽角落；不会叠加未结束的上一批，不在角色已倒地时封禁她，不封掉仅剩的普攻路线。仅一人存活时四书批次无合法组合，会返回false，交由遭遇轴等待救援后重试。Advance(realSeconds)与Clear由遭遇生命周期驱动，不含隐式Update/首抽/轮次间隔。
- `Players/Actions/PlayerActionBlock`新增PrimaryAttack和Skill，保留所有旧bit；DS饭团/手动锁定及HS激光属于普攻，DS护航/HS光剑属于技能，旧ActiveCombat/AutomaticCombat限制仍生效。新IPlayerBlockedCommandConsumer仅在权限封普攻时由实际Dispatcher调用，HS取消未发射锁定并推进冷却，不积攒攻击；已发出的束线/饭团不撤回。未改输入协议或网络消息。

## Unity 装配与微调

场景`Assets/Scenes/World02_2066.unity`根`ClaudePermissionModule`：Books_0～3绑定四个`PermissionBook_0～3`；Corners顺序左上、右上、右下、左下，初值(-7.2,3.2)/(7.2,3.2)/(7.2,-2.7)/(-7.2,-2.7)。这是固定玩法区内的视觉布局初值，不贴物理屏幕边沿，不随震屏或超长屏扩大。

预制体`Assets/_Project/Prefabs/Combat/Encounters/Claude/PF_CL_PermissionBook.prefab`：BoxCollider2D Trigger尺寸(1.45,1)，原生透明书Sprite、受击短闪、感知、WorldSpace Canvas。书图1536×1024，PPU512/中心Pivot/Visual等比0.55，有效图约1.37×1.01u；Layer/Sorting/material继承现有迅雷配置，不新增Layer。标签沿用生成的ButtonBase、独立ButtonFrame和ButtonGlow时间条，uGUI Text及UiThemeView跟随全局DS/HS主题；没有GraphicRaycaster，所有Image/Text不吃输入。Collider、书图scale、标签位置和Corner Transform均可独立微调；不是按贴图拉伸去改判定。

书只作为可攻击目标，不造成接触伤害或阻碍救援，Perception.PassiveAttackTarget开启，TargetValue视觉/AI首版初值30。已注册共享感知，但未实战验收AI是否能在Boss技能组合中可靠破书。未新增Token/鲸元券掉落，不改变前三波。

## 素材检查

使用deepsleep-sprite-matting检查，不重抠、不生成新图、不修改原像素。源raw/SPR_CL_PermissionBookOpen_v01_CANDIDATE.png与正式Art/Characters/Claude/SPR_CL_PermissionBookOpen.png哈希相同：e3eff9d640b41dc238b906dcec54117c9f86dd62653ebdfa6abb18861b723ff8。Alpha0～254，有效bbox(alpha>8)=(160,51,1432,993)，四角透明；白/暗/天空底已实际查看，没有硬化发光。报告在ArtProduction/20261008_Claude/previews/book_audit。

`PermissionBooks_16x9.png`仅隔离Editor相机静态布局：手动推进夜景/人物出场与一批四书，隐藏预览场景中未Awake清理的Kimi/豆包影像。未正确覆盖ScreenSpace uGUI雨层/完整HUD，非自然Play、非手机或联机截图。首次试绘因预览场景剔除遮罩为空失败，后用已核对的Camera.overrideSceneCullingMask修正；RT释放前解绑Camera，最终无新错误。

## 验证与下一步

菜单`DeepSleep/Diagnostics/Claude Permissions`：隔离场景校验数值、预警时序、真实受击入口、预警可破、单owner归还、10秒边界、大步长、实际生命事件订阅/倒地事件fixture、实际Dispatcher路由、旧救援限制、128批组合及四角/不重叠/重置、唯一幸存者普攻、DS/HS类别和主题图片切换。生命事件fixture不等于真实倒地救援实战验收。另回归Claude基础17项、出退場19项、DS护航规则与HS近战几何/变速。

仍缺正式Claude章节接管与权限轴轮次间隔/首抽、BossHP和阶段阈值、移动/技能、破书/解封专属VFX与声音、屏幕HUD封禁图标、AI实战、权限书网络状态复制。客户端不会自行Open或结算书伤害；网络完整接入前不得对外称联机权限书完成。模块默认隐藏、无自动启动，不把半份Boss塞进当前第四波。未打包上传。
