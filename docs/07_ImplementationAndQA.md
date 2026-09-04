# 07｜P0+C1 垂直切片实现与回归测试 v3.0（历史：停止执行）

> 2026-09-04：旧里程碑已被 `20_Unity6MigrationAndDeliveryPlan.md` 取代；长期回归用例可作为检查灵感，但旧扑翼、双路线和首章验收无效。

> 本文件只负责序章与第一章双路线垂直切片。完整 C0–C6、隐藏关、美术音频和开源发布流水线由 `16_FullDevelopmentPlan.md` 负责。原则：每个里程碑必须形成可运行、可验证的增量；验收失败就修当前里程碑，不允许靠后续系统掩盖。

## 1. Definition of Done

任一任务只有同时满足以下条件才算完成：

- 功能行为符合权威规格，没有“差不多”。
- 相关配置/Prefab/Scene 已保存，不依赖开发者本机临时状态。
- Console 0 Error；新增 Warning 有解释。
- 对应 EditMode/PlayMode 测试通过。
- 运行时稳定阶段 GC Alloc/Frame=0B 或有批准的例外。
- Inspector 引用无 Missing、无运行时 Find。
- 美术导入、Sorting/Physics Layer、Collider 符合清单。
- QA 能用明确步骤复现成功与失败。
- 文档已同步，不留注释“以后再对”。

## 2. 里程碑总览

| ID | 只做什么 | 完成后玩家能看到什么 |
|---|---|---|
| M0 | 工程骨架、输入、层、校验、测试框架 | 空场景可启动，校验全绿 |
| M1 | 流程、玩家飞行、相机、暂停 | 鲸鱼占位图可稳定扑翼 |
| M2 | 生命、伤害、障碍、关卡时间轴骨架 | 能撞门、扣血、死亡、重开 |
| M3 | DS 自动射击、HA 手动选区、敌人、对象池、XP | 两路线都能按各自规则打怪 |
| M4 | 路线资源、护盾/能量、两套 Buff | 蓝线充盾、黑线管能量并三选一成长 |
| M5 | 第一章两条独立飞行时间轴 | 两路线 0–125s 均可复现并通关 |
| M6 | Gemini 双路线三阶段 Boss | 同一 Boss 有两套破招与资源节拍 |
| M7 | 前端、三难度、正式 UI、音频、VFX、统计 | 形成完整 P0+C1 双路线切片 |
| M8 | 切片正式美术、性能、兼容与试玩包 | 可交付 P0+C1 开源试玩包 |

## 3. M0｜工程骨架

### 实现清单

1. 安装 Input System，配置 Action Maps。
2. 创建目录、asmdef、Bootstrap/FrontEnd 和序章场景；其余六章与隐藏关到对应里程碑再创建。
3. 创建 Physics Layer、Sorting Layer、Collision Matrix。
4. 建立 AppContext、GameplaySceneContext、SignalHub、PoolRegistry 空骨架。
5. 创建 `GameBalanceConfig` 与 ID 类型。
6. 建立 EditMode/PlayMode 测试程序集。
7. 实现 `DeepSleep/Validate Project` 最小校验器。

### 验收

- `Packages/manifest.json` 包含 Input System。
- 输入 Action 可在 Input Debugger 中触发。
- Build Settings 场景顺序正确。
- 校验器能故意检测到一个错误 Sorting Layer，修复后变绿。
- Runtime asmdef 不依赖 Editor。
- 空场景 Play/Stop 10 次无 Error、无残留对象。

## 4. M1｜流程与玩家飞行

### 实现清单

- AppFlow 与 GameplayFlow：Boot/FrontEnd/Loading/Briefing/Tutorial/Flight/BossIntro/BossFight/Paused/Result。
- InputReader Action Map 切换。
- PlayerMotor、VisualRoot 旋转、相机。
- MainMenu、Pause、临时 Result UI。
- 上下边界与无伤调试模式。

### 验收用例

| Test ID | 步骤 | 通过条件 |
|---|---|---|
| PLY-001 | 静止不按键 2s | Y 速度不低于 -8.5，人物不穿底边 |
| PLY-002 | 每 0.3s 按空格 10 次 | X 保持 -5.2±0.05，Y 响应无延迟 |
| PLY-003 | 同物理步模拟两次 Flap | 只施加一次 6.8u/s |
| PLY-004 | 左键与空格分别测试 | 行为完全一致 |
| PLY-005 | Esc 暂停 3s | 玩家、时间轴冻结；UI 动画仍运行 |
| PLY-006 | 暂停时按 Flap 再继续 | 不缓存“幽灵扑翼” |

## 5. M2｜伤害与障碍

### 实现清单

- PlayerHealth、DamageInfo、无敌帧。
- 管道 Prefab 与显式 GapLipAnchor。
- EncounterRunner 支持 Gate/Tip/End 指令。
- 死亡、结果、重开完整清理。

### 验收用例

| Test ID | 场景 | 通过条件 |
|---|---|---|
| DMG-001 | 一帧内两个障碍 Collider 同时接触 | 只扣 2 心 |
| DMG-002 | 受伤后 0.79s 再碰弹 | 不扣血；0.81s 后可扣 |
| DMG-003 | 撞障碍剩 1 心 | 不直接死亡 |
| DMG-004 | 1 心时撞障碍 | HP 钳制 0，只触发一次死亡 |
| OBS-001 | 门缝 3.6u | Debug 锚点与美术边缘误差 <0.05u |
| OBS-002 | 相邻中心差设置 1.36u | 校验器报 Error |
| RST-001 | 死亡后重开 20 次 | HP/位置/时间轴均重置，无池泄漏 |

## 6. M3｜双路线战斗、敌人与池

### 实现清单

- DS：TargetingService、Obstacle Linecast、AutoShooter、饭团弹池。
- HA：指针转世界坐标、选区钳制、HarnessAttackController、能量/冷却与一次性范围结算。
- 三种敌人策略与预警。
- EnemyHealth、掉 Token、XP。
- Stats 统计击破、伤害源和路线攻击效率。

### 验收用例

| Test ID | 步骤 | 通过条件 |
|---|---|---|
| CMB-001 | 基础射击 10s | 30±1 发，间隔均匀 |
| CMB-002 | 1 发穿过 Boss 两个 Collider | 只伤害一次 |
| CMB-003 | 子弹出屏/过期/命中 | 全部回池，状态重置 |
| ENM-001 | 击杀低等模型 | 只掉 1 Token、只计 1 kill |
| ENM-002 | 冲刺虫出现 | 红线完整 0.6s 后才有伤害 |
| PERF-001 | 连续射击/刷怪 5 分钟 | 池稳定后 GC Alloc/Frame=0B |
| POOL-001 | 故意耗尽敌弹池 | 跳过发射并仅报限频 Warning，不 Instantiate 风暴 |
| DS-LOS-001 | 敌人被管道完全挡住 | 不锁定、不发饭团；露出后 0.12s 内恢复 |
| DS-LOS-002 | 已发饭团撞管道 | 饭团回池，障碍不受伤，后方敌人不掉 HP |
| HA-AIM-001 | 右键拖到屏外/超过 12u | 圆心投影到合法边界，世界不减速 |
| HA-AIM-002 | 一圈覆盖 3 个目标并穿过管道 | 三目标各结算一次，管道不受伤 |
| HA-AIM-003 | 能量不足、冷却中或取消瞄准 | 不结算伤害；取消不扣能量 |
| INPUT-ROUTE-001 | DS 按右键、HA 按 Shift | 两者均无玩法效果，不触发另一路线技能 |

## 7. M4｜路线资源、护盾/能量与 Buff

### 实现清单

- 三类拾取物与磁吸：Token、DS 白米饭、HA 算力凭证。
- DS ShieldController 与 HA RouteEnergy；Prefab 互不挂错组件。
- ExperienceController、升级三选一、DS 6 个 Buff、HA 6 个 Buff。
- Buff 候选按路线隔离，并按加减值→乘法→钳制顺序计算。

### 验收用例

| Test ID | 步骤 | 通过条件 |
|---|---|---|
| SHD-001 | 捡 5 份白米饭 | 能量恰好 100 |
| SHD-002 | 能量 99 按盾 | 不启动；只抖一次能量条 |
| SHD-003 | 护盾开启后连续碰 10 弹 | 3s 内 0 伤害，HP 不变 |
| SHD-004 | 护盾中捡米 | 为下一轮累积，不丢失 |
| SHD-005 | 护盾中顶住管道 | 不扣血但被分离，结束后不嵌入 |
| BUF-001 | XP=5/12/21 | 分别弹三次升级，第四次不弹 |
| BUF-002 | 三选一卡池 | 同一组无重复，暂停时世界冻结 |
| BUF-003 | Parameter×3 | 伤害按 10×1.25³ 四舍五入 |
| BUF-004 | Quant×2 | 射速和伤害按固定顺序，无浮点漂移 |
| BUF-005 | Context | 最大 HP+1 并回 1 心，只可一次 |
| HA-ENG-001 | 初始 90 能量静置 10s | 恰好 130；暂停/演出不恢复 |
| HA-ENG-002 | 捡 1 张凭证 | 基础 +30；HA_CACHE 按层再 +5 |
| HA-BUF-001 | HA_QUANT×2 | Cost=44，伤害为基础×0.95²；不低于钳制 |
| HA-BUF-002 | HA_PARAM×2 | Radius=2.15×1.12²，且不超过 2.70u |
| BUFF-ROUTE-001 | 两路线各开卡 50 次 | DS 不出现 HA 卡；HA 不出现白饭/盾卡 |

## 8. M5｜第一章双路线飞行时间轴

### 实现清单

- 完成 `03_Level01EncounterScript.md` 的 DS/HA 两套全部指令，禁止共享同一条 Encounter 数组。
- authored seed 与 seed=0 QA 模式。
- Debug checkpoint 下拉启动。
- 清场和 BossIntro 转换。

### 验收

- 录制 seed=0 的 0–125s 参考视频；每个事件时间误差 ≤0.05s。
- CP_TUTORIAL_END、CP_FIRST_UPGRADE、CP_SECOND_UPGRADE、CP_THIRD_UPGRADE、CP_PRE_BOSS 均能直接启动。
- 连续完整跑 20 局没有屏内生成、必死墙、丢 XP、卡对象。
- 44–60s 豆包段两路线、三难度均无敌人、敌弹和额外障碍；曲折气泡通道是唯一玩法实体。
- 现有豆包河/浪和 Opus 参数山如被复用，必须无 Collider 且标记 `DECOR_ONLY`。
- 新手 5 人每人 3 局，至少 70% 局数能到 Boss；若样本不足，先内部 30 局代理测试并记录。

## 9. M6｜Gemini 双路线 Boss

### 实现清单

- BossIntro、RouteBossVariant、两套 HP/阶段/真身规则。
- DS/HA 各自的 Twin、Banana、American Doubao Pattern。
- 预警、清弹、路线资源掉落、狂暴。
- Boss 胜利演出与结果。

### 验收用例

| Test ID | 步骤 | 通过条件 |
|---|---|---|
| BOS-DS-001 | DS 900→601 HP | 仍是阶段一；到 600 后完成最短循环再转换 |
| BOS-002 | 阶段转换期间射击 | Boss 不受伤，旧 Pattern 已取消 |
| BOS-DS-003 | DS 每次阶段转换 | 恰好掉 2 饭，不因命中额外掉 |
| BOS-004 | 香蕉弧 | 黄预警先于弹 0.55s，轨迹颜色一致 |
| BOS-005 | 话术块 | 白虚线无伤、红实线有伤，Collider 与视觉一致 |
| BOS-006 | 基础无 Buff 命中率 75% | 40–55s 击破 |
| BOS-007 | 70s 未击破 | 只弹速×1.15，不增数量、不秒杀 |
| BOS-008 | Boss 死亡同帧有敌弹命中玩家 | 先清伤害，胜利不被翻转成失败 |
| BOS-HA-001 | HA 360→241 HP | 仍是阶段一；到 240 后完成最短循环再转换 |
| BOS-HA-002 | HA 每次阶段转换 | 恰好掉 2 张凭证，总计 +60 |
| BOS-HA-003 | HA 攻击假身/真身 | 假身无伤且反馈明确；真身每次只结算一次 |
| BOS-HA-004 | 基础无 Buff、允许两次空放 | 42–58s 内仍可击破 |

## 10. M7｜前端、难度、UI、音频、VFX 与统计

### 实现清单

- FrontEnd 的路线/章节/难度选择，以及 Gameplay 全部正式面板、键鼠/触屏导航、Safe Area、文本本地化表。
- Casual/Standard/Hard Profile 与白名单校验；休闲 BossIntro 内存快照。
- 三首音乐、SFX 路由、音量设置。
- 命中、拾取、护盾、阶段 VFX。
- 结果统计与失败原因。

### 验收

- 纯键盘可完成开始、升级选择、暂停、重开、返回。
- Android 触屏可用两根手指同时维持飞行与路线技能/瞄准，任一手指抬起不误停另一动作。
- 1920×1080、2560×1440、1280×720 HUD 不裁切；18:9/20:9 只扩展背景，窄屏按规定 Letterbox；UI 全部位于 Safe Area。
- 快速暂停/继续不会叠加音乐或卡低通。
- 同类 SFX 限制并发，连续射击不削波。
- 结果数字与调试 Stats 一致，失败原因非 Unknown。
- 结果页按路线显示白饭/锁定效率或凭证/执行效率，不出现串线字段。
- 三难度只修改批准字段；豆包互斥规则在困难也不失效。

## 11. M8｜垂直切片正式美术、性能与发布

### 实现清单

- 只替换 P0+C1 必需占位资源，逐项通过 `06_ArtAssetManifest.md`；其余章节按 `16_FullDevelopmentPlan.md` 后续生产。
- 加无障碍选项：屏闪减弱、镜头震动 0–100%、敌弹高对比。
- Development/Profile/Release 三种运行配置。
- README、许可证、第三方来源、开源仓库忽略文件。

### 发布门槛

- Windows x64 干净机器解压即运行，无需 Unity/VC 调试环境。
- Android 签名 APK 可在目标真机清装和覆盖安装；暂停恢复、触屏多指和本地存档正常。
- 30 分钟 Soak Test 无崩溃、无对象池持续增长、无 MissingReference。
- Boss 最大弹幕 P95 帧时间 <16.6ms；稳定战斗 GC=0B/frame。
- 所有外部/社区参考有作者与许可证记录；未授权图片不随包分发。
- 键鼠/触屏输入、音量、重开、退出全可用。
- 0 Error、无可见占位方块、无调试 Gizmo、无 Console Overlay。

## 12. 必须长期回归的 Bug

每次涉及物理、对象池、状态机、关卡或 Buff 的改动都重跑：

1. 同一物理步多 Collider 重复扣血。
2. 无敌帧晚一帧设置。
3. 池对象二次租借保留旧速度/透明度/协程/订阅。
4. 暂停期间时间轴偷跑。
5. 升级弹窗关闭后错误解除玩家原本的暂停。
6. Boss 阶段切换旧协程继续发弹。
7. Boss 与玩家同帧死亡时裁决顺序错误或结果翻转。
8. 门缝配置与图片嘴唇不一致。
9. 结果页失败原因 Unknown。
10. 重开后 seed、统计、Buff、HP 没重置。

## 13. 禁止用“调数值”掩盖的错误

- 玩家经常穿障碍：先修碰撞/FixedUpdate，不要把碰撞体缩成点。
- 弹幕看不清：先修颜色/预警/层级，不要单纯把伤害降为 0。
- Boss 过快：先核对 DPS、命中、HP 与阶段门槛，不要随手加到 10000 HP。
- 关卡必死：先核对组合净通道，不要让玩家护盾变永久。
- 帧率低：先 Profile、对象池与分配，不要直接删一半特效却保留泄漏。

## 14. 交付报告模板

每轮实现只需提交一份短报告，但必须完整：

```text
完成里程碑/任务：
修改的文件：
可见行为：
配置/Prefab 装配变更：
运行的测试及结果：
仍存在的问题：
用户如何在 1 分钟内验证：
```

不得只说“代码已完成，请测试”。
