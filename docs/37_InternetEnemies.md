# 下载冲撞怪与 360 持盾怪

2026-10-06。用户确认的两种普通敌人；共用现有 EnemyActor、对象池、生命、伤害、感知登记、掉落和网络镜像，不另建关卡框架。

## 行为与首版参数

后续调整：下载箭头现为左右侧随机出生，各约50%；沿用固定逻辑战斗区边界，不读取某一联机设备的镜头宽度。左侧向右入场、右侧向左入场；进入战区后再蓄力追踪。开关在 `CFG_Download_Schedule` 的 `Spawn From Both Sides`，其他三种普通怪仍右侧出生，刷怪间隔和总量不变。

- 下载冲撞怪：入场后原地蓄力 1.1 秒，期间瞄准最近可攻击玩家；随后按最后方向以 30 单位/秒直冲 0.65 秒，不转向。用户体验后要求速度和距离各翻倍：速度原为 15，时长不变，未碰撞/提前出界时距离由 9.75 增至 19.5 单位。未离场则恢复 0.7 秒，再次蓄力。常态/蓄力/冲刺使用三张生图，切换复用现有单残影。基础生命 6。
- 360：复用数据蛇的慢速追逐运动，速度 0.65–0.85。盾始终在正左或正右，换边延迟 0.16 秒，水平分量死区 0.18，转身有单残影。基础生命 8。
- 两者复用现有一次性接触攻击：本体碰到合法玩家后尝试扣血并回池，玩家处于保护期也会消耗该敌人；不是持续贴身重复扣血。盾本身不承担接触伤害。
- 盾不带生命、不能锁定，不是可击碎的次数盾。米粒在盾前回池；激光冻结为截至盾面的几何，显示与伤害读取相同结果；近战/剑气和米粒溅射的伤害查询检查遮挡。剑气整张美术仍按原方式播放，没有做局部贴图裁切。
- 实体受击闪烁、击杀/接触特效、伤害与死亡声音沿用现有实现。盾有短暂亮色反馈；本批未另制冲撞蓄力/起冲/金属挡盾专用音频。

仅调整黄昏关。用户确认重排后：360 提前到第一波 18 秒，第二波突出；下载从第三波开始，第四波增多。第一关不受影响。生命继续读取公共波次倍率 100%/150%/200%/300%，不是敌人内部硬编码。普通怪掉落沿用现有规则，不修改豆包的 100 Token。

### 当前四波配置（2026-10-06 重排）

每格为「首次延迟秒 / 后续随机间隔秒 / 同屏上限」，不是固定生成总数。现有随机高度保留，首轮错时不等于保证编队或全程空间间距。配置唯一日常调整入口是 `Configs/Progression/CFG_ChapterRun_World01_EarlyInternet.asset` 的各段 SpawnRules；不要重跑一次性安装器来调数值。

| 波次 | 404 | 数据蛇 | 下载箭头 | 360 |
|---|---|---|---|---|
| 一 | 0.8 / 0.633–1.035 / 10 | 关 | 关 | 18 / 10–14 / 1 |
| 二 | 0.8 / 0.66–1.08 / 12 | 5 / 1.44–2.08 / 10 | 关 | 1.5 / 4–5.6 / 3 |
| 三 | 0.5 / 0.242–0.396 / 28 | 3 / 0.495–0.715 / 16 | 7 / 2.1–3 / 3 | 1.5 / 7–9.8 / 2 |
| 四 | 0.5 / 0.22–0.36 / 28 | 2.5 / 0.383–0.553 / 20 | 5 / 1.4–2 / 4 | 1.5 / 8.5–11.9 / 2 |

后两波 404 的生成频率较初版下降约 36% / 43%，部分压力转给蛇与箭头。时长 45/55/60/65 秒、击杀要求 10/12/16/22 保持不变。受控即时清怪模拟生成总量 56/105/303/393（非实战承诺）；同屏限额、玩家击杀速度、遭遇限流和出界都会改变实际数量。单只普通怪期望掉落3 Token不变，但后两波减少出怪会降低潜在总收益，不能据此宣称经济不变；本批不补涨掉落或改商店价，需实战验收。

## 在 Inspector 调碰撞体

目录：`Assets/_Project/Prefabs/Combat/Enemies/Internet/`。

| 内容 | 对象与字段 | 当前值 |
|---|---|---|
| 下载本体 | `PF_Enemy_Download` 根 `BoxCollider2D.Size / Offset` | 1.4 × 0.7 / 0 |
| 高速冲撞补扫 | `Configs/Combat/Enemies/Internet/CFG_Download_Charge` 的 `ContactSweepRadius` | 0.35；调本体时一起看，避免视觉外命中 |
| 360 本体 | `PF_Enemy_SecurityGuard` 根 `BoxCollider2D.Size / Offset` | 0.92 × 1.28 / 0 |
| 360 盾 | 子节点 `Shield` 的 `BoxCollider2D.Size / Offset` | 0.36 × 0.85 / 0 |
| 盾与本体的间距 | 根 `SecurityGuardFacing2D.ShieldOffset` | 0.65；不要只改 Shield 的 X，运行时会按此值重新放置 |
| 豆包本体 | `Prefabs/Combat/Enemies/PF_EN_Doubao`，`DoubaoBoss2D._hitCollider` 指向的 Collider | 本批保持原样 |
| 豆包气泡 | `DoubaoWordWallConfig` 的各气泡槽 `Width` | 运行时据此计算半径，单改气泡 Prefab 半径会被覆盖 |

保持 Collider 的 Trigger 勾选。视觉大小在 `VisualRoot/Visual` 等比缩放，本体根保持 Scale=1；不要用整个根缩放来混调图片与碰撞体。新素材统一 512 PPU；常态下载可见宽约 1.9 单位，360 本体可见高约 1.7，盾高约 0.9。动作图保留生成源尺寸，未拉伸。

## 维护入口

- 行为：`DownloadChargeMotor2D` / `DownloadChargeConfig`；外观：`DownloadChargeVisual2D`。
- 360 追逐沿用 `DataCrawlerSnakeMotor2D`；换边：`SecurityGuardFacing2D`。
- 共用挡板：`PlayerAttackBlocker2D` / `AttackBlockerQuery2D`，Layer16 `PlayerAttackBlocker`。新增盾类敌人可复用。
- 武器接入：RiceProjectile 扫掠、HarnessLaserChainBuilder 截束、HarnessMeleeDamageExecutor2D 遮挡查询。
- 场景登记仍由 LevelContentManifest + LevelSceneBindings 驱动；LevelSceneInstaller 同时派生音效的敌人池数组，避免以后漏掉新怪死亡音。
- `InternetEnemyInstaller` 是一次性素材/Prefab 装配工具，安装过会拒绝覆盖，避免冲掉人工碰撞调参。后续直接调已有资产。
- 新增五个精灵登记到原网络目录，沿用实体多图层快照和命中特效通道，不增加网络协议类型。重排后的内容版本 `20261006-enemy-waves-2`，旧包不可混联。

验证范围见 `ImplementationEvidence/20261006_InternetEnemies/verification.txt`。编辑器受控验证不替代手机与双设备实战验收。
