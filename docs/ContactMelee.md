# 接触近战与玩家击退

2026-10-07，用户要求从统一的“碰撞就消失”改为可配置生命周期；用户确认只在实际扣血时击退。

## 当前参数

| 来源 | 玩家击退距离/持续时间 | 攻击后 | 重复间隔 |
| --- | --- | --- | --- |
| 404 | 1.8u / .22s | 消失 | 一次性 |
| 迅雷 | 3.6u / .24s | 消失 | 一次性 |
| 360 | 1.8u / .22s | 保留 | 1s |
| 递归大/中/小 | 1.8u / .22s | 保留 | 1s |
| Kimi本体球罩接触 | .65u / .18s | 保留 | 原每玩家1s |

击退径向远离敌人中心，中心重叠时用敌人当前移动方向。无敌/护盾不弹开；404与迅雷即使被挡仍消耗自身，360/递归攻击冷却照常。360与递归冷却目前是每敌人共用1秒，不是两个玩家各自一份。递归出生保护期间碰撞禁用；正常击败才分裂，接触不会分裂。远程弹体伤害不新增击退。

参数位于：
- 原404配置：`Assets/_Project/Configs/Combat/Enemies/404Window/CFG_EN_404Window_ContactDamage_Default.asset`
- 迅雷/360独立配置：`Assets/_Project/Configs/Combat/Enemies/Internet/CFG_EN_Download_ContactDamage.asset`、`CFG_EN_SecurityGuard_ContactDamage.asset`
- 三递归共享：`Assets/_Project/Configs/Combat/Enemies/RecursiveJelly/CFG_Recursive_ContactDamage.asset`
- Kimi：`Assets/_Project/Configs/Combat/Encounters/Kimi/CFG_KI_Boss.asset`，ContactKnockbackDistance/Seconds。

## 实现边界

EnemyContactDamageConfig保存是否回收/冷却/击退距离与时间；EnemyContactAttack2D共用接触算法，持续怪由EnemyActor已有固定步清单驱动冷却，TriggerStay允许重叠后重试。没有新增逐帧查找或另一个模拟循环。持续怪的ActorContactImpacted是独立表现事件，复用现有事件数据结构，但不触发ActorDespawned，因此不会回池、生成奖励或计入图鉴/章节离场。现有死亡特效呈现器和普通/专属音效各自消费接触事件，避免持续攻击丢效果或重复播放；现有特效/音效网络事件仍适用。

DamagePacket携带可选击退参数（默认0不影响其他攻击）。PlayerDamageReceiver2D仅在实际Health扣血之后提交给显式绑定的PlayerMovementMotor2D；拦截器返回成功不能被误当成实际扣血。倒地已停用移动器，不移动倒地尸体。

移动器短时间覆盖移动输入，以线性减速积分位移，沿原战区与玩家碰撞框夹紧。PlayerMovementStep主机/客人预测共用；主动蓄力移动锁不能吞击退，结束后停下，无倒地/复活/检查点残留。没有缩放或改动碰撞框。

玩家快照新增每人击退速度XY与剩余时间共12字节，两人消息242→266字节。网络协议10→11，内容20261007-contact-knockback-1；旧包不可混房。客人只预测主机给出的击退状态，不自行判定受击。已有乱序/ACK入口沿用，无新可靠伤害事件或重复击退消息。

## 验证范围

- ContactMeleeChecks.Run：82项隔离Prefab/实际生命接收检查，涵盖DS/HS、404/迅雷/360/三递归、一次性与持续事件、重复冷却、护盾与无敌、相反移动输入、边界、重置。
- NetworkProtocolChecks.RunCatalog：1550断言通过，协议11。
- 三玩法场景共6个玩家伤害门与网络击退源引用有效。
- 最终装配检查：测试场1541、第一世界1923、第二世界2103项，均0错误0警告；一次性来源也登记共用接触固定步，符合组件清单契约。
- 临时存档独立Play：RecursiveJellyChecks.RunPlay 42项回归通过；真实Physics2D模拟六种接触来源，实际弹开距离/池存活或回收/持续重复与撞击特效通过；真实双玩家快照266字节经协议校验通过。
- 真实profile哈希未变，已退出Play。未冒充自然玩法难度、手机或双设备联机手感验收；未打包上传。
