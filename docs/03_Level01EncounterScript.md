# 03｜第一章《日用助手堵塞》双路线执行脚本 v3.0（历史：停止执行）

> 2026-09-04：双路线时间轴已失效。豆包、Gemini和事件素材概念仍可参考，但新合作章节必须在双鲸灰盒手感通过后重新编排。

> 本文件拥有第一章生成顺序与 Boss 节拍。两条路线是独立时间轴，不允许运行时读取一份表后按角色乘倍率。  
> 所有时间使用未缩放玩法时间；暂停和升级界面不推进时间轴。

## 1. 共用环境参数

| 参数 | 值 |
|---|---:|
| WorldScrollSpeed | 4.2u/s |
| SpawnX / DespawnX | 11.2 / -11.2 |
| CameraRect | X=-9.6..9.6，Y=-5.4..5.4 |
| PlayerTargetX | -5.2 |
| AuthoredSeed | 20260831 |
| TutorialEnd | 10.0s |
| FlightEnd | 125.0s |
| BossIntroDuration | 5.0s |
| BossFightStart | 130.0s |

可用垂直锚点：`LOW=-3.2`、`MID_LOW=-1.6`、`MID=0`、`MID_HIGH=1.6`、`HIGH=3.2`。

## 2. 时间轴指令

第一章只实现以下数据指令。指令背后可以调用已实现策略，不允许在 ScriptableObject 里塞任意 C# 类型名或反射回调。

| 指令 | 必填字段 | 行为 |
|---|---|---|
| SHOW_TIP | textId, duration | 显示教学/预警文本 |
| SET_SCROLL | speed, blendTime | 修改世界滚动速度 |
| SPAWN_PICKUP_LINE | pickupId, y, count, spacing | 排列路线专属拾取物 |
| SPAWN_ENEMY | enemyId, y, variant | 生成单个敌人 |
| SPAWN_CLUSTER | enemyId, centers[], coverId? | 生成可被 Harness 选区覆盖的一组目标 |
| SPAWN_GATE | gapCenterY, gapHeight, coverEnemy? | 数据管道；可选在后方放敌人演示挡弹 |
| START_BUBBLE_CORRIDOR | duration, curveId, gapHeight | 启动豆包上下气泡链独占段 |
| START_CLEAR | duration | 停生成、清弹、回收普通敌人 |
| START_BOSS_INTRO | bossId | 进入 Boss 入场状态 |

`START_BUBBLE_CORRIDOR` 自动添加互斥标签 `Enemy|Obstacle|EnemyProjectile`；持续时间内任何其他生成请求必须被验证器判错，而不是静默忽略。

## 3. DeepSeek 蓝线时间轴 `L01_DS`

目标：学会点按/长按飞行、自动索敌、饭团会被实体挡住、寻找视线继续输出。

| 时间 | 指令 | 参数 | 验收意图 |
|---:|---|---|---|
| 0.0 | SHOW_TIP | `TIP_FLY_TAP_HOLD`, 3.8s | 明示“点一下飞一下，按住持续上升” |
| 1.0 | SPAWN_PICKUP_LINE | Rice, y=-1.2→1.2, 4, 0.9 | 用米饭画上升弧 |
| 4.2 | SPAWN_ENEMY | LowModel, y=0, Dummy | 首个靶子无伤；自动索敌标记可见 |
| 6.0 | SHOW_TIP | `TIP_DS_AUTO_RICE`, 3.0s | 明示饭团自动找敌、墙会挡住 |
| 10.0 | SPAWN_ENEMY | LowModel, LOW, Ground | 开始正常掉 Token |
| 14.0 | SPAWN_ENEMY | Hallucination, MID_HIGH, SlowShot | 单枚慢弹 |
| 18.0 | SPAWN_GATE | gapCenter=0.8, gapHeight=3.6 | 第一扇宽门，无藏怪 |
| 24.0 | SPAWN_GATE | gapCenter=-1.0, gapHeight=3.6, coverEnemy=LowModel@MID_HIGH | 敌人在遮挡后不可锁，露出后恢复锁定 |
| 30.0 | SPAWN_PICKUP_LINE | Rice, MID_LOW, 3, 0.85 | 补护盾能量 |
| 34.0 | SPAWN_ENEMY | ContextBug, HIGH, Dash | 0.6 秒红线预警 |
| 39.0 | SPAWN_GATE | gapCenter=1.25, gapHeight=3.4 | 进入气泡段前最后障碍 |
| 42.8 | SHOW_TIP | `TIP_DOUBAO_CORRIDOR`, 2.0s | 三枚无伤虚线预览泡 |
| 44.0 | START_BUBBLE_CORRIDOR | duration=16.0, curve=DB_NORMAL_A, gapHeight=2.35 | 纯稳定飞行；无小怪、无弹、无其他障碍 |
| 60.0 | SET_SCROLL | 4.2, blend=0.6 | 气泡链结束并回中线 |
| 62.0 | SPAWN_PICKUP_LINE | Token, MID, 4, 0.8 | 奖励通过 |
| 67.0 | SPAWN_ENEMY | Hallucination, LOW, SlowShot | 恢复低压战斗 |
| 70.0 | SPAWN_ENEMY | Hallucination, HIGH, SlowShot | 与上一只错峰，不同时封中线 |
| 76.0 | SPAWN_GATE | gapCenter=-1.3, gapHeight=3.35, coverEnemy=Hallucination@HIGH | 第二次视线考试 |
| 84.0 | SPAWN_ENEMY | ContextBug, MID_LOW, Dash | 红线预警 |
| 89.0 | SPAWN_PICKUP_LINE | Rice, HIGH→MID, 4, 0.8 | 引导下降 |
| 94.0 | SPAWN_GATE | gapCenter=1.4, gapHeight=3.25 | 最窄门，但不并发敌弹 |
| 101.0 | SPAWN_ENEMY | LowModel, LOW, Ground | 连续输出收尾 |
| 103.0 | SPAWN_ENEMY | LowModel, MID_HIGH, Float | 与上一只不共用遮挡线 |
| 109.0 | SPAWN_ENEMY | Hallucination, MID, SlowShot | 最后战斗组合 |
| 114.0 | SPAWN_PICKUP_LINE | Rice, MID, 5, 0.75 | Boss 前保证至少一盾 |
| 120.0 | START_CLEAR | duration=5.0 | 停生成、清弹、回收 |
| 125.0 | START_BOSS_INTRO | Gemini | 进入共用入场，随后加载 DS Boss 策略 |

硬检查：`coverEnemy` 被管道遮住时 `TargetingService` 必须返回无合法目标；敌人中心露出后 0.12 秒内重新获得标记。饭团不能穿过管道边缘命中。

## 4. Harness 黑线时间轴 `L01_HA`

目标：学会飞行与右键选区并行、一次覆盖多个目标、攻击无视遮挡、能量预算和不空放。

| 时间 | 指令 | 参数 | 验收意图 |
|---:|---|---|---|
| 0.0 | SHOW_TIP | `TIP_FLY_TAP_HOLD`, 3.8s | 与蓝线共用飞行教学 |
| 1.0 | SPAWN_PICKUP_LINE | ComputeVoucher, y=-1.2→1.2, 3, 0.9 | 初始 90+90，补满 180 |
| 4.0 | SPAWN_CLUSTER | LowModelDummy, centers=[(5,0),(5.9,0.55),(5.9,-0.55)], noDamage | 三个靶子落在一个 2.15u 选区内 |
| 4.2 | SHOW_TIP | `TIP_HA_AIM_RELEASE`, 4.5s | 右键按住选区、拖动、松开执行 |
| 9.0 | SHOW_TIP | `TIP_HA_COST`, 2.8s | 每次 60；冷却 2.4 秒；别空放 |
| 12.0 | SPAWN_CLUSTER | LowModel, centers=[(11,1.2),(11.7,1.8)], cover=ThinDataWall | 第一次穿障清理，两只共区 |
| 18.0 | SPAWN_PICKUP_LINE | ComputeVoucher, LOW, 2, 0.8 | 恢复 60，明确资源 |
| 23.0 | SPAWN_CLUSTER | Hallucination, centers=[(11,-1.1),(12,0),(11,1.1)] | 三目标纵向覆盖；子弹在 1.2 秒后才发 |
| 30.0 | SPAWN_GATE | gapCenter=-0.9, gapHeight=3.5 | 只考飞行，不要求攻击 |
| 35.0 | SPAWN_CLUSTER | ContextBug, centers=[(11.2,2.4),(12.0,2.0)], cover=PipeTop | 应在预警结束前选中整组 |
| 40.0 | SPAWN_PICKUP_LINE | ComputeVoucher, MID_LOW, 2, 0.8 | 气泡段前恢复 |
| 42.8 | SHOW_TIP | `TIP_DOUBAO_CORRIDOR`, 2.0s | 三枚无伤虚线预览泡 |
| 44.0 | START_BUBBLE_CORRIDOR | duration=16.0, curve=DB_NORMAL_A, gapHeight=2.35 | 禁止目标；专心平飞，攻击能量被动恢复 |
| 60.0 | SET_SCROLL | 4.2, blend=0.6 | 气泡链结束并回中线 |
| 62.0 | SPAWN_PICKUP_LINE | ComputeVoucher, MID, 2, 0.8 | 奖励通过 |
| 67.0 | SPAWN_CLUSTER | LowModel, centers=[(11,-2.3),(11.8,-1.7),(12.1,-2.8)], cover=PipeBottom | 教一次隔墙整组击破 |
| 74.0 | SPAWN_CLUSTER | Hallucination, centers=[(11,1.5),(12,1.0)], noCover | 不值得连续空放两次 |
| 80.0 | SPAWN_PICKUP_LINE | ComputeVoucher, HIGH, 1, 0 | 引导上升，单次恢复 |
| 85.0 | SPAWN_CLUSTER | ContextBug, centers=[(11,-1.6),(11.8,0),(11,1.6)] | 半径无法全包，要求选择优先目标 |
| 92.0 | SPAWN_GATE | gapCenter=1.35, gapHeight=3.25 | 冷却期纯飞行 |
| 98.0 | SPAWN_CLUSTER | LowModel, centers=[(11.0,0.4),(11.7,0.9),(11.8,-0.3)], cover=ThinDataWall | 最优一次覆盖三只 |
| 105.0 | SPAWN_PICKUP_LINE | ComputeVoucher, MID_LOW→MID_HIGH, 3, 0.8 | Boss 前至少增加 90 |
| 112.0 | SPAWN_CLUSTER | Hallucination, centers=[(11,-2.0),(12,2.0)] | 故意分散，不能一圈全收 |
| 118.0 | START_CLEAR | duration=7.0 | 停生成；被动恢复继续到 BossIntro |
| 125.0 | START_BOSS_INTRO | Gemini | 进入共用入场，随后加载 HA Boss 策略 |

硬检查：黑线全程必须存在至少 5 次“一个选区覆盖两个以上敌人”的机会；穿障伤害只忽略遮挡，不摧毁管道。新手合理命中率下，进入 Boss 时能量目标为 120–180。

## 5. 豆包气泡曲线 `DB_NORMAL_A`

```text
CenterY(t) = 0.70*sin(2πt/7.5) + 0.24*sin(2πt/3.8 + 0.8)
GapHeight   = 2.35u
AnchorStep  = 0.72u
MaxDeltaY   = 0.34u per anchor
```

入场 0–1 秒和离场最后 2 秒分别用 SmoothStep 从/向 `CenterY=0` 混合。气泡短句从本地化表循环抽取，但相邻两泡不能重复。碰撞体固定，气泡呼吸动画只作用 VisualRoot。

## 6. Gemini 共用入场

| 相对时间 | 行为 |
|---:|---|
| 0.0 | 世界滚动在 0.8 秒内降至 0；清除残留伤害体 |
| 0.6 | 蓝紫双星从右上/右下划入，停在 X=6.4 |
| 1.2 | Gemini 巨大 Q 版半身跟随双星合体入场 |
| 2.0 | 名牌：`GEMINI`；副标题按语言包显示，不把“美国豆包”写成官方称号 |
| 3.1 | 双生残影上下分开，再合回本体 |
| 4.4 | UI 显示当前路线专属 Boss 血条；0.6 秒后开战 |

## 7. Gemini：DeepSeek 路线 Boss `B01_GE_DS`

| 字段 | 值 |
|---|---:|
| MaxHP | 900 |
| BodyDamage | 2 |
| TargetTime | 40–55s |
| Phase | 900–601 / 600–301 / 300–0 |

### 7.1 阶段循环

| 阶段 | 9.6 秒循环 |
|---|---|
| 双子 | 0.0s 上残影三连星；1.4s 下残影三连星；3.0s 两侧镜像慢弹；5.2–6.8s 本体移动到中央，给连续输出窗口；7.4s 双排弹留 2.4u 缺口 |
| Nano Banana | 0.0s 黄色弧线预警 0.55s；0.6s 香蕉沿弧飞行；2.4s 复制一条旧弧但颜色降低；4.2–6.6s 本体露出；7.0s 两枚编辑块临时形成会挡饭团的掩体 |
| 美国豆包 | 0.0–1.6s 白色虚线无伤话术泡；1.8s 一枚红框有效方块；3.2s 引用星点四连；5.0–7.0s 道歉鞠躬并露出；7.4s 自信慢弹三枚 |

阶段转换 1.4 秒无敌并在屏幕中央生成两碗白米饭；所有 Boss 掩体在转换时回收。任何时刻必须存在至少一条从玩家到 Boss 的可获得视线，持续遮挡不得超过 2.2 秒。

## 8. Gemini：Harness 路线 Boss `B01_GE_HA`

| 字段 | 值 |
|---|---:|
| MaxHP | 360 |
| BodyDamage | 2 |
| TargetTime | 42–58s |
| Phase | 360–241 / 240–121 / 120–0 |

Harness 攻击不受掩体影响，因此 Boss 用“真目标窗口、诱饵与能源节拍”反制，禁止简单增加无敌时间。

| 阶段 | 10.0 秒循环 |
|---|---|
| 双子 | 0.0s 两个分身展开；只有脚下带白色细环的真身受伤；2.0s 真身交换；4.0–6.0s 两个分身靠近到一个 2.15u 圈可同时覆盖，但只结算真身；7.0s 镜像弹留中线缺口 |
| Nano Banana | 0.0s 三个香蕉形选区诱饵；1.2s 真弱点以红黑方括号锁定 1.8 秒；3.8s 编辑笔复制上一次玩家选区为无伤残影；6.0–8.0s 开放稳定攻击窗 |
| 美国豆包 | 0.0–1.8s 话术框遮住部分 Boss，但范围攻击仍可穿过；2.0s 有效红框落下；4.0–6.5s Boss 鞠躬露出；7.0s 三枚慢弹逼迫飞行，瞄准不得让世界减速 |

阶段转换生成两张算力凭证（总计 +60），Boss 无敌 1.4 秒。基础状态通关需要 8 次有效攻击；若玩家空放两次仍应存在通关可能，但会接近 58 秒目标上限。

## 9. 胜负与自动验收

Boss HP 到 0：当帧停伤、清弹；0.2 秒双子残影分开；0.7 秒香蕉编辑笔掉落；1.2 秒像素星光消散；1.8 秒结果面板。

玩家生命到 0：当帧停伤与攻击；角色失去升力并下坠；0.9 秒后冻结并结算。

每次修改时间轴后必须验证：

1. 两路线在 44.0–60.0 秒只有豆包气泡通道，不存在敌人、敌弹或其他障碍。
2. 气泡通道任意采样点净高≥2.35u，相邻锚点 Y 差≤0.34u。
3. DS 遮挡测试期间饭团无法穿障；露出后 0.12 秒内重新锁敌。
4. HA 至少 5 组选区可覆盖多个目标，且穿障攻击不删除障碍。
5. 基础无 Buff、合理操作下，DS Boss 40–55 秒、HA Boss 42–58 秒完成。
6. HA 瞄准期间时间流速始终为 1，飞行输入仍工作。
7. 所有失败原因可归类为气泡、障碍、敌弹、Boss 身体或边界，不出现 `Unknown`。
