# 双端状态栏与HS丢失目标恢复（2026-09-09）

## 用户验收补充：世界护盾与HS呆毛（2026-09-09）

用户明确复活保护不仅是HUD图标，还要罩在角色上的半透明盾。已新增 `World_ReviveProtection` 根及DS/HS两个独立Sprite对象，`PlayerReviveProtectionView2D`读取各自伤害门的IsReviveProtected；真实保护结束即隐藏，普通受击无敌不显示此盾。不新增碰撞/免伤规则。

场景层级：`World_ReviveProtection/DeepSeek_ReviveShield`、`Harness_ReviveShield`。独立于玩家SortingGroup，使用当前最高世界排序层UIWorld、Order100（HUD仍在其上）。复用蓝白盾Sprite，用户验收后将等比Scale从1.3缩至0.8，使其主要覆盖角色上半身；Offset保持(0,0)，Opacity保持0.3。大小/透明度可分别在Transform和脚本Inspector调节，不参与双方攻击特效倍率。

HS睡姿仅清理呆毛内圈棋盘残留，原画布/PPU/Pivot/角色缩放/碰撞不变。生产PNG原路径覆盖，原文件备份及局部脚本、检查记录在 `docs/ArtProduction/20260909_HSSleepCleanup/`；三种底色检查发圈与头饰完整。初次保护点(465,110)实际位于背景孔中，记录为错误取点；最终验收改为已从源图确认的四个发圈/头饰点，见audit_final，不是扩大删除范围。

实际复活后双盾截图：`docs/ImplementationEvidence/20260909_CombatHUD/world_revive_shields.png`。

## 本轮行为

- 用户更正：一直索敌的是HS，不是DS。DS攻击策略未改。
- HS人机在激光冷却完成后等待0.25秒，再提交下一次点选。该值来自 `Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset` 的 `Harness Aim Rest Seconds`，不是修改激光原冷却，真人预选行为不变。
- 一旦选中目标，记录最后有效世界位置。目标死亡、失活或入池，不取消已开始的瞄准；继续充能并朝此位置发射，然后进入冷却、结束瞄准表现。
- `DamageHitbox2D.BecameUnavailable` 在失活时解除活对象跟踪，避免同一个池对象在别处复用后拖走射线。
- 目标引用允许为空，但开火快照仍必须有效。伤害照常查询实际射线带内的敌人，不对已死亡的位置直接扣血。炮口、束体与命中仍消费原同一快照。
- 取消瞄准、倒地、进入近战等已有显式中断仍然有效。

## HUD结构与双端边界

`UI_CombatHUD / SafeArea / DS、HS` 是新增的独立场景根，不放在选角菜单下面。Canvas排序900，低于选角1000；选角完成前隐藏。

数据流：玩法组件 → `PlayerCombatHudSource` → 值快照 `PlayerCombatHudSnapshot` → `PlayerCombatHudView`。

- Source只读取真实HP、生命状态、复活进度、无敌剩余时间和两个主动技能/激光状态，不维护额外计时器。
- View依赖 `IPlayerCombatHudSource`。后续网络适配器可提供同一快照，不在UI引入具体网络SDK。当前仍只是本地读数，不代表联网完成。
- `PlayerDamageReceiver2D.IsReviveProtected` 区分复活保护与普通受击短暂无敌。蓝白图标仅在复活保护期间出现；倒地/救援进度另用文字显示。
- 1920×1080参考CanvasScaler，宽高匹配0.5；屏幕安全区由现有 `SafeAreaRectFitter` 处理。两栏锚定安全区左上/右上，各占约34%宽度，保留中央及下部触控区域。
- 所有Graphics关闭raycastTarget，CanvasGroup不拦输入；本HUD没有技能按钮，不把只读状态栏冒充触屏控制系统已完成。
- 暂沿用开局菜单的内置字体和简短英文，占位UI不生成整套皮肤。正式中文字体/本地化、美术化布局另行设计。
- 不改变角色大小、武器宽度、敌人或玩家碰撞体；图标尺寸也不受战斗命中特效倍率影响。
- 文字按显示精度变化重建（倒计时0.1秒），不是每帧不断格式化；未做Profiler，不能宣称零GC。

显式装配菜单：`DeepSleep/设置/装配双端战斗状态栏`。当前场景已装配，无需再点；若发现已有HUD则拒绝重复创建。

## 图标

- `Assets/_Project/Art/UI/Status/ICO_SH_ReviveProtection_v01.png`
- 1254×1254 RGBA，PPU512，中心Pivot，Bilinear，无Mipmap，无压缩，最大2048。uGUI Image等比显示42×42参考像素；没有Collider和世界Sorting Layer，使用Canvas排序。
- 饱满洋红底生成，再用项目本地脚本去底及去边，原始图保留。沿用治疗十字与治疗收圈的蓝白晶体、数字碎片风格。
- 原图/配方/透明报告/多底色预览：`docs/ArtProduction/20260909_ReviveProtection/`。

## Unity实际验收

测试在Play Mode冻结时间后对真实场景组件进行受控推进；测试状态未保存回场景。

1. 选中目标后令对象失活，再把同一对象移到(-6,-3)并重新启用：仍向原(4.04,1.04)开火1次，请求有效，PrimaryTarget为空，结束为Cooldown且HasAimPoint=false。
2. 对真正已锁定的敌人扣致死伤害：激光仍发射1次，另一只位于路径上的敌人HP从8降至5.5；没有卡在瞄准。
3. HS人机Ready且有可观察目标，ResetIntent后分别推进0.1、0.1、0.06秒：攻击命令依次None、None、Pressed，证实0.25秒休息生效。
4. DS、HS分别扣到0血：HUD快照Downed=true；经真实Life.TryRevive恢复1/3最大生命（当前3→1），读到1.5秒保护和图标标志。
5. 两者实际施放技能：DS Active=8秒、6次；HS Active=16秒。结束后分别读到DS Cooldown=15.9秒、HS Cooldown=8秒，未另改技能数据。
6. 截图核对1920×1080、2400×1080，以及在20:9里模拟左右不等安全边距，文字/血条/图标完整，底部无覆盖。截图目录 `docs/ImplementationEvidence/20260909_CombatHUD/`。
7. 运行控制台0 Error、0 Warning。退出播放并恢复原Game View选项；未保存测试对象、血量、模拟安全区或时间倍率。
8. 场景按fileID逐块比对：所有原有对象数据保持不变，只新增HUD对象和SceneRoots引用；Git行级diff的删除/新增错位来自新对象穿插，不是删除背景/旧UI。

限制：上述为Unity编辑器运行和屏幕比例验收，不是Android实机/触摸、网络、性能验收。超宽背景边缘仍沿用现有场景表现，本轮只处理HUD。

## 用户验收和下一步

1. 播放，任选DS/HS，检查两栏YOU/COMPANION、HP与技能状态。
2. 选DS观察HS人机每发之间出现待机间隙；点选目标后让其先被其他攻击杀死，HS应继续射出而非保持瞄准不动。
3. 用现有调试菜单令一名角色倒地，另一名站定救援；看救援百分比→恢复1血→护盾图标及剩余秒数→消失。
4. 用技能检查持续与CD切换。手机验收仍需Android包与触屏控制配置，不以编辑器截图代替。

本轮停止于用户验收；下一步按用户安排讨论网络联机，读取19的主机权威/统一命令边界后再实现。没有提交或推送本轮改动。
