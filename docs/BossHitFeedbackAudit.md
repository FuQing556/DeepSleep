# Boss命中特效语义与接线审计

2026-10-08。用户明确：Claude橙色命中图用于Claude攻击玩家；Claude被攻击时播放DS/HS攻击者的武器特效。前轮将其当作Boss受伤自有碎光是误读。

## 本轮纠正Claude

- BossBarrierFeedback2D只保留受击变暗/恢复，不再消费橙色命中池。
- PF_CL_BossBase及World02实例的DamageHitbox.UseReceiverHitFeedback=false，允许原DS饭团/HS激光/HS近战的攻击特效；StopsPiercingBeams仍为true，不取消球罩截断、跳字或阶段锁血。
- 现有ClaudeHitEffectPool/PF_VFX_CL_Hit复用于三个主动攻击：全屏切割、追踪局部斜斩、能量弹爆炸。各模块显式绑定同一池，仅TryReceiveDamage返回成功后播放；Blockable护盾拦截返回成功，因此有效挡伤接触也有特效；无敌拒绝、躲避、无伤滞留不播。
- 没有新增素材、复制伤害/特效池系统或修改攻击数值。护盾接触攻击仍缺，尚不能宣称未来所有伤害入口已覆盖；Claude正式遭遇/网络仍未接。

验证：Claude Boss Base17、Tracking Cut47、Spatial Cut2622、Energy60项隔离Editor检查通过，含实际成功命中触发池Played、躲避不播、淡出不重复、交叉去重、两人爆炸各一次及护盾拦截。不是实际DS/HS完整武器、Play/设备/联机视觉验收。

## Kimi检查结果（本轮未修改）

World01_EarlyInternet、World02_2066中的PF_KI_MoonBladeModule，以下入口均绑定同一SharedHitEffects，素材PF_VFX_KI_Hit，联机事件NetworkEffectEventChannel ID124，Session引用完整。

| 伤害来源 | 现有特效入口 | 结论 |
| --- | --- | --- |
| 水平月光刃 | EnemyProjectile2D→EnemyProjectilePool2D.NotifyImpact | 已接Kimi池 |
| 巨大潮汐刃（含二阶段三发） | 同一通用敌弹链 | 已接Kimi池 |
| 锁向激光（含二阶段五分叉） | KimiLaserPattern2D.ResolveLane成功命中后TryPlay | 已接Kimi池 |
| 棱光弹射球 | KimiPrismOrb2D.Move成功后NotifyImpact | 已接Kimi池 |
| Kimi球罩接触伤害/击退 | KimiEncounter2D.SimulateContact只有TryReceiveDamage | 漏命中特效调用 |
| 大招360援兵碰撞 | 360本体EnemyContactAttack2D | 当前是360自身特效，不是Kimi直接攻击 |

通用敌弹在实际碰撞时即播放Impact，即使玩家无敌而没有扣血也会播；激光/棱光球在伤害入口接受（包括护盾拦截）后播。两者语义不同，但每次真正扣血的四种直接技能都有命中特效路径。棱光在镜面反射时也用同一池。

Kimi自身护罩受击表现保持原实现；本轮用户明确纠正的是Claude，不擅自迁移Kimi护罩或修改其玩法。

## 可复用范围

- EnemyProjectilePool2D的命中通知、OneShotSpriteEffectPool2D的复用及Played事件。
- NetworkEffectEventChannel：复用池的播放事件，不另造逐特效网络协议。
- Kimi接触的Sphere.Distance重叠、两玩家分别冷却、Blockable伤害及实际扣血后击退；近战强度/间隔来自配置，Claude不能直接复制Kimi整套遭遇或数值。
- DS/HS武器特效继续由各自HitConfirmed与UseReceiverHitFeedback路由，不让Boss受伤特效冒充攻击者特效。
