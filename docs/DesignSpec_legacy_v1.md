# 【已归档，禁止作为实现依据】《鲸鱼娘飞行记》设计文档 v1

> 本文件仅保留历史方案。2026-08-31 起，唯一入口改为 `docs/DesignSpec.md`；实现人员不得从本文件抄数值、架构或素材规格。

- 版本：v1.0
- 项目：`D:\Unity Work\DeepSleep`（Unity 2022.3.48f1c1）
- 用途：**本文件是 DeepSleep 项目的唯一权威设计文档**。取代 Fly Ai 目录下的旧文档，实现与美术均以本文档为准，旧文档不再参考。
- 状态：设计定稿中，标注【待定】的项等待用户补充确认
- 协作规则见 `docs/agent.md`

---

## 1. 项目概述

| 项 | 内容 |
|---|---|
| 类型 | 2D 横版飞行生存 + 弹幕 Boss 战融合 |
| 平台 | PC / Windows / 1920×1080 / 16:9 |
| 引擎 | Unity 2022.3 LTS + **内置 2D 渲染管线（Built-in，不装 URP）** + **新 Input System** |
| 画风 | 卡通二次元，AI 娘题材 |
| 核心体验 | 单键扑翼贯穿全程；前半程躲障碍、打怪物、攒能量成长，后半程弹幕 Boss 战 |
| 范围 | 垂直切片：1 个完整关卡（飞行段 + Boss 段），架构按多关卡、多 Boss、多 Buff 扩展设计 |
| 程序集 | `DeepSleep.Runtime` / `DeepSleep.Editor` |

## 2. 已锁定的设计决策（不得擅自更改）

1. 平台 PC 横屏，1920×1080，16:9
2. 碰撞规则为混合制：撞障碍扣 2 心、弹幕扣 1 心、护盾可挡，生命归零死亡（不是一碰即死）
3. 操作全程保持"点一下飞一下"（tap 扑翼），Boss 战不切换操作方式
4. 范围只做垂直切片（1 关），但数据全部走配置，为多关卡预留
5. 主角攻击为全程自动吐饭团弹（无需按键），玩家专注躲避
6. 技能键为双击空格（判定窗口 0.3 秒）
7. 白米饭只回能量；回血靠怪物掉落的心
8. 怪物击杀给经验，经验满升级，升级 3 选 1 Buff
9. 首发 Boss 为 Gemini 娘（3 阶段）；后续 Boss 候补：ChatGPT、Claude
10. 渲染管线：内置 2D（Built-in），不装 URP
11. 输入系统：新 Input System，经 InputReader 门面转发，玩法层不碰底层输入 API
12. **核心是 AI 梗**：娘化 + 自然奇观化，贯穿世界观 / 障碍 / 怪物 / Boss / 文案
13. **配置唯一数据源**：玩法数值全部存在于 ScriptableObject 配置资产，代码禁止硬编码（见 §9）

## 3. 世界观与 AI 梗（核心）

### 3.1 世界观（提案，待用户确认）

数据之海 / 云端数据中心。鲸鱼娘是一个被遗忘的 AI 模型化身，在数据的海洋与云层之间扑翼飞行，穿过一座座 AI 化身而成的自然奇观，最终挑战各 AI 娘的本体。

### 3.2 AI 梗角色对照

| 游戏内形象 | 对应真实 AI | 梗 / 设计 |
|---|---|---|
| 鲸鱼娘（主角） | AI 本体（娘化） | "AI 在知识的海洋里游泳"→ 鲸鱼娘。元气、贪吃（吃米饭=吃数据）。**梗源待用户确认【待定】** |
| 常规管道 | 数据中心 / 服务器 | "数据中心管道"主题，基础节奏障碍 |
| Opus 山 | Claude Opus（Anthropic 最强型号） | "最高的山"：静止时低矮可过，玩家接近突然弹起封路（最强的模型偏要拦路） |
| 豆包河 | 豆包（字节跳动 AI） | "最长的河"：超长水平障碍，内部散布补给。**梗出处待用户确认【待定】** |
| AI 小兵（怪物） | AI 公司元素（拟人/logo 化） | **形象待用户确认【待定】** |
| Gemini 娘（Boss） | Gemini（Google DeepMind） | 双子座，3 阶段弹幕 |
| ChatGPT 娘（候补 Boss） | ChatGPT（OpenAI） | 后续扩展 |
| Claude 娘（候补 Boss） | Claude（Anthropic） | 后续扩展 |
| 敌方弹 | 数据包 / 比特 / 星点 | 与数据中心主题统一的弹幕视觉语言 |

> 梗是这个游戏的灵魂。§3.2 中标注【待定】的三处（鲸鱼娘梗源、豆包河梗出处、小兵形象）请用户确认后再定稿。

## 4. 核心玩法

### 4.1 核心循环

```
进入关卡 → 飞行段（躲障碍/弹幕、打怪物、吃米饭攒能量、升级选 Buff）
         → Boss 段（弹幕战，自动攻击）→ 胜利结算
任意时刻生命归零 → 失败结算 → 重试
```

单关目标时长 2~3 分钟，无检查点。

### 4.2 操作（PC）

| 输入 | 行为 | 说明 |
|---|---|---|
| 空格 / 鼠标左键 | 扑翼 | 全程唯一移动操作，点一下飞一下 |
| 双击空格 | 释放技能（护盾） | 能量满才可放，双击判定窗口 0.3 秒 |
| Esc | 暂停 | 暂停面板 |

### 4.3 主角：鲸鱼娘

| 属性 | 初值 | 说明 |
|---|---|---|
| 水平位置 | 屏幕左 1/3 固定 | 世界左移模拟前进 |
| 移动 | 刚体默认重力（−9.81）+ 扑翼赋竖直速度 8 | 扑翼用速度赋值不用施力（手感确定、与质量无关） |
| 最大下落速度 | −12 | 防止无限加速 |
| 自动攻击 | 饭团弹 10 伤害，3 发/秒，自动瞄准最近目标 | 弹对障碍无效（碰撞即消失） |
| 生命 | 3 心 | 障碍 −2，弹幕/怪物 −1，撞 Boss −1 并击退 |
| 受击无敌 | 1 秒 | 闪烁表现 |
| 能量 | 0~100 | 小份米饭 +20，大份米饭 +40，仅用于技能 |
| 技能 | 护盾 | 持续 3 秒，免疫一切伤害，能量满才可放 |
| 死亡 | 坠出屏幕底部 或 生命归零 | |

### 4.4 飞行段（约 70% 时长）

- 世界以恒定速度左移，初始 4 单位/秒，随距离线性递增至上限 8
- 障碍从右侧生成，生成间隔 1.6~2.4 秒随机，随速度提升成比例缩短

| 障碍 | 设计 | 玩法目的 |
|---|---|---|
| 常规管道 | 上下成对，"数据中心管道"主题 | 基础节奏 |
| Opus 山 | 出现前 1.5 秒预警；静止时低矮可过，玩家接近时突然弹起封路 | 反应力考验（"最高的山"） |
| 豆包河 | 超长水平障碍，8~10 秒通过，内部散布米饭补给 | 续航考验（"最长的河"） |

- 弹幕：飞行段后半程出现稀疏直射弹（右侧来）
- 怪物：密度随距离递增（同屏上限 5）
- 白米饭沿路径成串分布引导走位；大份米饭约每 30 秒保底一次
- 结束条件：飞行距离达标 → Boss 入场，世界滚动停止

### 4.5 怪物与成长系统

| 怪物 | 血量 | 行为 | 掉落 |
|---|---|---|---|
| 基础小怪 | 20（饭团弹 2 发） | 右侧进场，直线/正弦移动 | 必掉经验 20；25% 掉米饭，10% 掉心 |
| 高速小怪（可选） | 10 | 快速直线突进 | 同上 |

- 经验条 0~100%；升级需求 = 100 × 1.2^(等级−1)
- 升级时暂停弹出 3 选 1 Buff 卡（从 Buff 池随机抽 3 个）

**Buff 池（首发 4 个，架构预留扩展）**

| Buff | 效果 |
|---|---|
| 连发 | 射速 +30% |
| 重弹 | 饭团弹伤害 +50% |
| 轻盈 | 扑翼速度 +15% |
| 坚韧 | 护盾持续 +1 秒 |

### 4.6 Boss 段（约 30% 时长）

- 战场锁定，Boss 从右侧入场，玩家保持 tap 扑翼
- 攻击方式：鲸鱼自动吐饭团弹，玩家专心躲避弹幕
- Boss：Gemini 娘，血量 300，3 阶段（100% / 60% / 25% 血量切换）
  - P1 扇形弹
  - P2 扇形 + 直射混合
  - P3 旋转弹幕
- Boss 受击掉落小份米饭（"边打边吃"），每阶段结束掉大份米饭 ×1 + 心 ×1（保底）
- 胜利：Boss 血量归零 → 结算

### 4.7 UI 流程

主菜单（开始/退出）→ HUD（心、能量条、经验条、Boss 血条、分数）→ Buff 选择面板 → 暂停面板 → 胜利/失败结算

分数 = 飞行距离 + 米饭×10 + 击杀×20 + 对 Boss 伤害×2

## 5. 数值汇总表（初值，实机后调）

| 参数 | 初值 |
|---|---|
| 重力 / 扑翼速度 / 最大下落速度 | −9.81 / +8 / −12（世界单位/秒） |
| 世界滚动速度 | 4 → 8，每飞行 1 单位 +0.02 |
| 生成间隔 | 1.6~2.4 秒，随速度反比缩短 |
| 缝隙 | 基准值=预制体实测；抖动 ±10%；相邻缝隙中心差 ≤1.5；中心范围由摄像机视野推导（管道边不超屏） |
| 饭团弹 | 伤害 10，射速 3 发/秒 |
| 敌方弹速 | 6~8 |
| 护盾时长 / 无敌时长 | 3 秒 / 1 秒 |
| 能量 | 小份米 +20，大份米 +40，上限 100 |
| Boss 血量 | 300 |
| 怪物血量 / 经验 | 20 / 20 |
| 升级需求 | 100 × 1.2^(等级−1) |
| 摄像机视野 | 约 22×11 世界单位（16:9） |

## 6. 系统架构与组件化设计

设计原则：**一个职责一个组件**；数值全部从配置注入；组件之间不互相认识，只通过 EventBus 发事件；表现层只订阅事件。

### 6.1 玩家（鲸鱼娘）组件组合

| 组件 | 职责 | 数据来源 |
|---|---|---|
| `PlayerFlight` | 扑翼：tap 赋竖直速度、重力、最大下落上限 | PlayerConfig |
| `Health` | 通用血量组件：当前值/上限/加减血/死亡，只发事件不碰 UI | PlayerConfig |
| `HitInvulnerability` | 订阅 Health.Damaged → 无敌 1 秒 + 闪烁，**扣血当帧置位** | PlayerConfig |
| `EnergyMeter` | 能量 0~100，吃米加、技能扣 | PlayerConfig |
| `AutoShooter` | 自动吐饭团弹：射速/发射点，方向由 AutoAim 提供 | PlayerConfig + ProjectileConfig |
| `AutoAim` | 找最近目标给方向，无目标平射 | — |
| `SkillUser` | 双击判定 → 扣能量 → 触发护盾 | SkillConfig |

### 6.2 射击系统拆分（弹幕/子弹组件化）

```
Shooter             通用射击组件：射速/间隔/发射点/弹药池引用
AutoAim             最近目标 → 方向，喂给 Shooter
Projectile          弹体：移动/伤害/敌我团队/碰撞规则/生命周期
ObjectPool<Projectile>  池，复用零 GC
BulletPatternPlayer Boss 与飞行段弹幕：按模式发弹
BulletPatternConfig 扇形/直射/旋转弹幕模式，全部数据化
```

→ 新增弹幕 = 新建一个 `BulletPatternConfig`，零代码。玩家饭团弹与敌方弹共用同一套 Projectile + 池，仅配置不同。

### 6.3 血条/生命系统拆分

```
Health             逻辑：数值 + 事件，玩家/怪物/Boss 共用
HealthView         表现层，只订阅事件
  ├─ HeartsView        玩家 3 心（UI 心图标）
  ├─ BossHealthBar     Boss 血条
  └─ EnemyHealthBar    小怪血条（可选）
伤害规则（障碍−2/弹幕−1/撞怪−1/撞Boss−1+击退）由各自接触组件决定，统一调 Health.TakeDamage
```

### 6.4 怪物 / Boss / 障碍 / 掉落 / 世界 / UI 组件

| 对象 | 组件组合 |
|---|---|
| 怪物 | `Health + EnemyMover(直线/正弦，策略) + ContactDamage + DropOnDeath(掉落表)` |
| Boss | `Health + BossPhaseMachine(血阈值切阶段) + BulletPatternPlayer + DeathReward` |
| 障碍-管道 | `ObstaclePair(缝隙实测，SpriteRenderer bounds) + HazardContact(触发器扣血)` |
| 障碍-Opus山 | `OpusMountain(静止→预警→弹起 状态机) + HazardContact` |
| 障碍-豆包河 | `DoubaoRiver(长条 + 内部补给生成点) + HazardContact` |
| 掉落物 | `Pickup + PickupType(小米/大米/心/经验) + 收集触发器` |
| 世界 | `WorldScroller + BackgroundScroller(视差循环) + ObstacleSpawner + EnemySpawner + PickupSpawner + BulletSpawner` |
| UI | `HeartsView / EnergyBar / ExpBar / BossHealthBar / BuffPanel / PausePanel / ResultPanel`，全走 EventBus 订阅 |

### 6.5 模块与设计模式总表

| 系统 | 核心类型 | 模式 / 技术点 |
|---|---|---|
| 游戏流程 | `GameStateMachine`（Menu/Playing/GameOver/Victory） | 状态机 |
| 事件通信 | `EventBus`（类型安全泛型事件） | 观察者 |
| 输入 | `InputReader` 门面 + 新 Input System（双击检测） | 门面 |
| 对象管理 | `ObjectPool<T>` | 对象池 |
| 生成调度 | 配置资产 + `Spawner` | 数据驱动 + 工厂 |
| 怪物行为 | `EnemyMover` 基类 + 子类（直线/正弦） | 组件 + 模板方法 |
| Buff | `BuffConfig` + `IBuffEffect` | 策略 + 数据驱动 |
| 技能 | `ISkill`（首发 ShieldSkill） | 策略 |
| 弹幕 | `BulletPatternPlayer` + `BulletPatternConfig` | 数据驱动 |
| UI | `UIManager` + `UIBase` 面板基类 | 面板状态管理 |
| 可测试逻辑 | 伤害计算/掉落表/升级曲线/分数（纯 C#） | 单元测试 |

### 6.6 EventBus 主要事件清单

```
PlayerFlapped / PlayerDamaged(amount, source) / PlayerDied
EnergyChanged(value) / ExpChanged(current, level) / LevelUp(choices)
HealthChanged / EnemyDied(def, position) / DropRequested(dropTable, position)
BossPhaseChanged(phase) / BossDied
BulletFired / BulletHit / ProjectileReturned
GameStateChanged(state) / ScoreChanged
BuffSelected(buff)
```

## 7. 排序图层完整表（唯一真相源）

Project Settings → Tags and Layers → Sorting Layers，自底向上：

```
Default      兜底（最底），不实际使用
Background   0 远背景（视差层 1）
             1 近背景（视差层 2）
Gameplay     0 障碍（管道 / Opus 山 / 豆包河）
             5 鲸鱼娘（玩家）
             6 怪物
             7 Boss
             8 掉落物（米饭 / 心 / 经验）
             10 弹幕（敌我双方）← 最上，躲弹可读性优先
             12 护盾 / 受击特效
Foreground   0 前景飘浮物 / 粒子（视差层 3，可选）
UI           0 HUD（心 / 能量 / 经验 / 分数）
             1 面板（Buff 选择 / 暂停 / 结算）
```

- 任何 2D 物体必须显式指定 Sorting Layer；忘设的兜底在最底层，不允许"背景盖角色"
- 图层名称由代码中 `SortingLayers` 常量类引用，禁止魔法字符串
- 该表在项目启动时一次性配好（TagManager），新建物体的排序由 Editor 工具/导入工具强制

## 8. 美术资源清单与交付规格

### 8.1 交付总则（美术必读）

1. 格式：**PNG 透明底**，`Texture Type = Sprite (2D and UI)`，sRGB 开
2. **PPU 统一 100**（导入设置由 Editor 工具统一强制，美术不需手动设）
3. **pivot**：按 8.2 各资源表；表中未列出的默认中心
4. **命名**：用英文 snake_case，文件名 = 8.2 表里的"文件名"列；**交付后不改名**（会断引用）
5. 排序图层/Order 由 Editor 工具自动设置，美术不必关心，但需知道视觉层次（见 §7）
6. 尺寸上限：普通精灵 ≤ 512×512；背景 ≤ 2048×2048；Boss 立绘 ≤ 1024×1024
7. **背景左右边缘必须无缝**（Unity 里 Tiled 平铺），上下边缘不必
8. 掉落物与弹幕尺寸小，但辨识度第一——在场景背景上必须一眼能认出
9. 禁止中文文件名与特殊字符
10. 素材放入 `Assets/_Project/Art/` 对应子目录（见 §8.3）

### 8.2 资源清单

#### 背景（Sorting Layer: Background）

| 文件名 | 内容 | 尺寸(px) | pivot | Order | 备注 |
|---|---|---|---|---|---|
| `Bg_Far.png` | 远背景层（视差层 1） | 1920×1080 | 中心 | 0 | Tiled 平铺覆盖约 22×11 世界单位；左右无缝 |
| `Bg_Near.png` | 近背景层（视差层 2） | 1920×1080 | 中心 | 1 | Tiled 平铺；左右无缝；主题：数据中心/云层/数据缆 |

#### 主角鲸鱼娘（Gameplay / 5）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `WhaleGirl_Idle.png` | 常态帧 | 512×512 内 | 中心 | 整体视觉约 4×3 世界单位 |
| `WhaleGirl_Flap1.png` | 扑翼帧 1（翅上） | 512×512 内 | 中心 | 与 Flap2 循环表现扇翅 |
| `WhaleGirl_Flap2.png` | 扑翼帧 2（翅下） | 512×512 内 | 中心 | 同上 |
| `WhaleGirl_Hit.png` | 受击帧 | 512×512 内 | 中心 | 受击闪白用 |
| `FX_Shield.png` | 护盾特效 | 512×512 内 | 中心 | 半透明能量罩，Gameplay/12 |

> 共 5 张。扑翼循环 = Idle→Flap1→Flap2→Idle，扇翅节奏约 0.15s/帧。

#### 障碍（Gameplay / 0）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `Pipe_Up.png` | 上管道（从顶下垂） | 宽 100~150 × 高 600~800 | 底部中心 | 数据中心管道主题；高 ≥ 800 保证覆盖垂直视野 |
| `Pipe_Down.png` | 下管道（从底上长） | 宽 100~150 × 高 600~800 | 顶部中心 | 同风格，上下成对视觉自然 |
| `Opus_Dormant.png` | Opus 山·静止态 | 约 400×400 | 底部中心 | 低矮可过；"最高的山"反差 |
| `Opus_Spring.png` | Opus 山·弹起态 | 约 400×800 | 底部中心 | 弹起封路，雪山/峰造型 |
| `Doubao_River.png` | 豆包河 | 高 300~400，宽不限 | 中心 | Tiled 可重复拼接；**内部补给不在图上画，逻辑生成** |

#### 怪物（Gameplay / 6）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `Enemy_Basic.png` | 基础小怪（AI 小兵） | 256×256 内 | 中心 | 受击闪白代码实现，可不另绘 |
| `Enemy_Speed.png` | 高速小怪（可选） | 256×256 内 | 中心 | 与基础怪颜色/形状区分明显 |

#### Boss（Gameplay / 7）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `Gemini_Boss.png` | Gemini 娘立绘 | 1024×1024 内 | 中心 | 二次元立绘；双子座元素 |
| `Gemini_Boss_Hit.png` | 受击帧 | 1024×1024 内 | 中心 | 受击闪白用 |

#### 弹幕（Gameplay / 10）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `Bullet_Rice.png` | 玩家饭团弹 | 32×32 | 中心 | 白色饭团 |
| `Bullet_Enemy.png` | 敌方弹 | 32×32 | 中心 | 数据包/比特/星点造型，数据中心主题 |
| `Bullet_Enemy2.png` | 敌方弹·变体（可选） | 32×32 | 中心 | 区分不同弹型/阶段 |

#### 掉落物（Gameplay / 8）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `Pickup_Rice.png` | 小份米饭 | 64×64 | 中心 | 辨识度优先 |
| `Pickup_RiceLarge.png` | 大份米饭 | 96×96 | 中心 | 明显大于小份 |
| `Pickup_Heart.png` | 心（回血） | 64×64 | 中心 | 红色/爱心 |
| `Pickup_Exp.png` | 经验球 | 64×64 | 中心 | 颜色区别于米饭与心 |

#### UI（Sorting Layer: UI）

| 文件名 | 内容 | 尺寸(px) | pivot | 备注 |
|---|---|---|---|---|
| `UI_Heart.png` | 心图标（满） | 64×64 | 中心 | HUD 显示 |
| `UI_Heart_Empty.png` | 心图标（空） | 64×64 | 中心 | 同上 |
| `UI_EnergyBar_BG.png` | 能量条底 | 400×32 | 中心 | 可九宫格 |
| `UI_EnergyBar_Fill.png` | 能量条填充 | 400×32 | 中心 | 可九宫格 |
| `UI_ExpBar_BG.png` | 经验条底 | 400×32 | 中心 | 可九宫格 |
| `UI_ExpBar_Fill.png` | 经验条填充 | 400×32 | 中心 | 可九宫格 |
| `UI_BossBar_BG.png` | Boss 血条底 | 800×32 | 中心 | 可九宫格 |
| `UI_BossBar_Fill.png` | Boss 血条填充 | 800×32 | 中心 | 可九宫格 |
| `UI_Buff_Card.png` | Buff 卡底框 | 256×320 | 中心 | 3 选 1 面板 |
| `UI_BuffIcon_Flap.png` | Buff 图标·连发 | 128×128 | 中心 | 射速 |
| `UI_BuffIcon_Heavy.png` | Buff 图标·重弹 | 128×128 | 中心 | 伤害 |
| `UI_BuffIcon_Light.png` | Buff 图标·轻盈 | 128×128 | 中心 | 扑翼 |
| `UI_BuffIcon_Tough.png` | Buff 图标·坚韧 | 128×128 | 中心 | 护盾 |
| `UI_Menu_BG.png` | 主菜单背景 | 1920×1080 | 中心 | 主题图 |
| `UI_Panel_BG.png` | 面板底框（暂停/结算共用） | 800×600 | 中心 | 可九宫格 |

#### 字体（UI）

| 文件名 | 内容 | 备注 |
|---|---|---|
| 中文字体（TTF/OTF） | UI 全中文文案 | 推荐开源思源黑体/Noto Sans SC；放入 `Art/Fonts/` |

#### 音频（可选，后补）

| 文件名 | 内容 |
|---|---|
| `SFX_Flap` / `SFX_Eat` / `SFX_Hurt` / `SFX_Skill` / `SFX_Kill` / `SFX_BossHit` / `SFX_BossDead` | 音效 |
| `BGM_Menu` / `BGM_Flight` / `BGM_Boss` | 背景音乐 |

### 8.3 素材目录映射

```
Assets/_Project/Art/
├── Backgrounds/    Bg_Far, Bg_Near
├── Player/         WhaleGirl_*, FX_Shield
├── Obstacles/      Pipe_*, Opus_*, Doubao_River
├── Enemies/        Enemy_Basic, Enemy_Speed
├── Bosses/         Gemini_Boss*
├── Projectiles/    Bullet_*
├── Pickups/        Pickup_*
├── UI/             UI_*
└── Fonts/          中文字体
```

素材交付后，由 Editor 导入工具统一设置 PPU / 排序层 / pivot / 碰撞体，减少人工装配出错。

## 9. 配置驱动规范（数据唯一数据源，最高级约束）

> 血泪教训：上一次开发，组件被硬编码导致全部配置失效。本规范为最高级约束，不可违反。

### 9.1 六条铁律

1. **代码里禁止出现玩法数值**。速度、伤害、血量、掉落、生成间隔、升级曲线、Boss 阶段……一律从注入的配置资产读取。结构性常量（排序层名、事件名、动画名）集中在 `SortingLayers` / `GameEvents` / `AnimNames` 常量类。
2. **对象即配置实例**。怪物 / 障碍 / Boss / 弹幕 / Buff 全部是"定义资产 + 工厂"模式。生成器不认识具体对象，只读定义 → **新增内容 = 新建一个配置资产，零代码**。
3. **禁止一切隐式获取**。`Find` / `FindObjectOfType` / 运行时链式 `GetComponent` 全禁。场景中 `GameRoot` 唯一装配点，显式注入所有引用。
4. **预制体只放美术与碰撞**。玩法参数一律不进预制体。几何参数（如管道缝隙）运行时从 `SpriteRenderer` 实际包围盒实测，代码永不假设尺寸 / pivot。
5. **配错必须报错，不许静默**。`BaseConfig.Validate()` + Editor 校验器：配置缺失 / 非法直接弹报错。"静默用默认值"就是硬编码的另一种形式，禁止。
6. **系统间只走 EventBus**。表现层只订阅事件，玩法模块互不引用；纯逻辑（掉落表 / 伤害 / 升级曲线 / 分数）写成无 Unity 依赖的纯 C# + 单元测试。

### 9.2 配置资产清单（Assets/_Project/Data/）

| 配置 | 用途 | 内容示例 |
|---|---|---|
| `PlayerConfig` | 主角数值 | 扑翼速度/最大下落/生命/无敌时长/射速/伤害/能量规则 |
| `WorldConfig` | 世界滚动 | 初始速度/上限/加速系数/飞行长度阈值 |
| `SpawnConfig` | 生成调度 | 间隔范围/抖动比例/相邻缝隙差上限/同屏上限 |
| `UpgradeConfig` | 成长 | 升级经验曲线 |
| `SkillConfig` | 技能 | 护盾时长/能量消耗 |
| `ProjectileConfig[]` | 弹药 | 饭团弹/敌方弹：速度/伤害/生命周期 |
| `EnemyDefinition[]` | 每种怪物 | 血量/行为类型/掉落表 |
| `ObstacleDefinition[]` | 每种障碍 | 类型/生成权重/预警时长 |
| `BossConfig[]` | 每个 Boss | 血量/阶段阈值/各阶段弹幕模式引用 |
| `BulletPatternConfig[]` | 弹幕模式 | 扇形/直射/旋转：数量/速度/角度/间隔 |
| `BuffConfig[]` | 每个 Buff | 类型/数值倍率/图标 |

### 9.3 校验机制

- `BaseConfig : ScriptableObject`，带 `Validate()` 抽象校验 + `[ContextMenu]` 手动触发
- Editor 校验器 `ConfigValidator`：扫描 Data/ 下所有配置资产，检查字段范围（正值、非空引用、掉落概率和=1），启动时与菜单触发，非法即报错

### 9.4 装配

- 场景唯一 `GameRoot` 持有全部系统引用 + 配置引用，负责依赖注入
- 游戏启动即校验；校验失败不进入游戏

## 10. 工程规范

### 10.1 目录结构

```
Assets/
└── _Project/
    ├── Art/                # Sprites / 素材（§8.3）
    ├── Audio/
    ├── Data/               # ScriptableObject 配置资产（§9.2）
    ├── Prefabs/            # Player/Obstacles/Enemies/Boss/Projectiles/UI
    ├── Scenes/
    ├── Scripts/
    │   ├── Runtime/        # asmdef: DeepSleep.Runtime
    │   └── Editor/         # asmdef: DeepSleep.Editor（单向依赖 Runtime）
    └── Settings/
```

### 10.2 程序集

- `DeepSleep.Runtime`：游戏逻辑与表现，asmdef 依赖 UnityEngine.* / 新 Input System
- `DeepSleep.Editor`：校验器 / 导入工具 / 编辑器工具，单向依赖 Runtime

### 10.3 命名约定

- 类/方法 `PascalCase`；私有字段 `_camelCase` + `[SerializeField]`；常量 `UPPER_SNAKE`
- 中文注释解释"为什么"；公共 API 用 `/// <summary>`
- 图层名/事件名/动画名用常量类引用，禁止魔法字符串

### 10.4 已知坑（不许再犯）

1. **无敌帧标志必须在扣血当帧同步置位**——协程下一帧才置位会导致同一物理步内多碰撞体重复判定（一对管道一次带走两心）
2. **相邻管道缝隙必须有最大竖直差上限**（可达性），否则随机缝隙会生成必死墙
3. **管道贴缝必须按 SpriteRenderer 实际包围盒计算**——禁止假设 pivot/图片尺寸约定
4. **缝隙基准值由预制体实测**：代码与配置里禁止出现任何缝隙绝对值；预制体是美术数据的唯一来源
5. **数值单一数据源**：禁止"预制体摆一套数值 + 代码默认值一套"的双源定义
6. **2D 排序图层在项目启动时一次性配好**，每个新物体明确指定所属层
7. 障碍碰撞体用触发器（穿透 + 扣血），玩家碰撞体非触发器——受击后无敌期间自然穿过管道
8. 每帧零 GC：高频对象（弹幕/米饭/怪物/掉落）全部对象池；热路径禁 LINQ、禁字符串拼接

## 11. 里程碑与验收标准（按序实现，每项验收通过再进下一项）

| 里程碑 | 内容 | 验收标准 |
|---|---|---|
| M0 | 工程骨架 + 配置框架 | 目录/asmdef/排序图层/Input System 接入；新建各配置资产、改 Inspector 值 → 代码读到新值；校验器能报出"配错"；GameRoot 装配链路通 |
| M1 | 主角扑翼 + 世界滚动 | 按空格/点鼠标点一下飞一下；背景无缝循环滚动；坠出屏幕底部死亡；数值全从配置读 |
| M2 | 障碍生成 + 碰撞 + 生命 | 管道随机缝隙从右来；缝隙与预制体一致（实测基准）；撞管道 −2 心、闪烁无敌 1 秒可穿过；相邻缝隙差 ≤1.5 无必死墙；生命归零死亡重开 |
| M3 | 米饭 + 能量 + 护盾 | 吃米饭涨能量；能量满双击空格开护盾 3 秒免伤 |
| M4 | 怪物 + 经验 + Buff | 自动吐饭团击杀怪物；怪物掉落心/米饭/经验；升级弹 3 选 1 Buff 卡，4 个 Buff 全部生效 |
| M5 | 弹幕系统 | 飞行段后半程直射弹；弹幕扣 1 心；对象池无 GC 尖峰 |
| M6 | Boss 战 | Gemini 3 阶段弹幕；自动攻击击杀；阶段掉落；胜利/失败结算 |
| M7 | 全 UI | 主菜单、HUD、Buff 面板、暂停、结算全流程 |
| M8 | 打磨 | 多层视差、特效、音效、数值平衡 |

## 12. 参考文档

- `docs/agent.md`：项目协作规则与约定（AI 必读）
- 旧项目 `D:\Unity Work\Fly Ai\docs\`：历史版本，**仅作参考，不以其为准**
