# 数据驱动战斗段

局外关卡入口、结算奖励与本地档案见
[24_MetaProgressionAndLocalSave.md](24_MetaProgressionAndLocalSave.md)。

## 结构分工

- `ChapterRunConfig` 保存章节包含哪些战斗段，不保存运行中的计时与击杀。
- `ChapterCombatSegmentDefinition` 保存一段的名称、时长、击杀目标与刷怪规则。
- `EnemySpawnChannelDefinition` 是敌人刷怪器的稳定身份。配置通过资产引用匹配频道，而不是依赖场景数组顺序或敌人名称字符串。
- `EnemySpawnDirector2D` 保留该敌种的基础刷怪日程，并在进入每段时接收本段的启停、初始延迟、间隔倍率与场上数量上限。
- `ChapterRunController` 只负责选择当前段、推进状态和应用配置，不包含 404 或机械蛇的专用分支。

这种分层让“新增一段”只修改配置；“新增敌种”需要一个刷怪器与频道资产，但不需要扩充章节状态机枚举。

## 当前天空原型三段

| 段落 | 时长 | 目标 | 404 漂流窗口 | 数据爬虫机械蛇 |
|---|---:|---:|---|---|
| 漂流碎屑 | 45 秒 | 10 杀 | 间隔 ×1.15，上限 5 | 关闭 |
| 爬虫接入 | 55 秒 | 12 杀 | 间隔 ×2.2，上限 3 | 间隔 ×1.0，上限 3 |
| 数据拥塞 | 60 秒 | 16 杀 | 间隔 ×0.85，上限 7 | 间隔 ×0.75，上限 4 |

间隔倍率乘在敌种自己的基础随机间隔上：大于 1 代表更慢，小于 1 代表更快。每次切段会重新应用初始延迟，避免上一段刷怪器剩余计时泄漏到下一段。

## 调参位置

主资产：`Assets/_Project/Configs/Progression/CFG_ChapterRun_Prototype.asset`

Inspector 中 `_segments` 的每个元素对应一段。常用调节顺序：

1. 先用 `Duration Seconds` 与 `Required Defeats` 决定任务压力。
2. 再用 `Enabled` 决定这一段出现哪些敌人。
3. 用 `Interval Multiplier` 调整单位时间出生量。
4. 最后用 `Maximum Alive Count` 限制场面峰值，避免只加速刷新造成无限堆积。

基础随机间隔仍在各敌人的 `SpawnSchedule` 资产中；它描述敌种默认习性，章节段落倍率描述关卡导演对这种习性的调整。
