# 【已归档】项目协作说明 v1

> 2026-08-31 后请读取新的 `docs/agent.md`。本文件含已废弃的全局 EventBus、双击空格和旧图层/数值规则。

## 项目
- 《鲸鱼娘飞行记》：2D 横版飞行 + 弹幕 Boss 战，Unity 2022.3.48 + 内置 2D 渲染（非 URP）+ 新 Input System，PC 1920×1080
- 设计文档：`docs/DesignSpec.md`（**唯一权威**，一切以它为准）
- 项目目录：`D:\Unity Work\DeepSleep`

## 协作分工
- AI：写全部 C# 代码 + 配置资产创建；每批代码用中文简要解释（简洁，不写长文）
- 用户：美术素材（放 `Assets/_Project/Art/` 对应子目录）、Unity 内建预制体/场景、挂组件、Inspector 调参
- 模式：小步增量——每轮只做一个可验证目标，用户验证通过再进下一步

## 技术约定（不可违反，除非用户明确要求改）
1. 代码全部在 `Assets/_Project/Scripts/`，程序集 `DeepSleep.Runtime`（asmdef）；Editor 工具在 `DeepSleep.Editor`
2. 命名：类/方法 `PascalCase`，私有字段 `_camelCase` + `[SerializeField]`，常量 `UPPER_SNAKE`
3. 注释：中文，解释"为什么"；公共 API 用 `/// <summary>`
4. 禁止 Find / FindObjectOfType / 字符串查找；引用一律 Inspector 序列化注入，场景由 `GameRoot` 唯一装配
5. 高频对象（弹幕/米饭/怪物/掉落）必须对象池；热路径禁 LINQ、禁字符串拼接
6. 玩法系统间通信走 EventBus（观察者）；表现层只订阅事件
7. 输入经 InputReader 门面（新 Input System），玩法层不碰底层输入 API
8. **数值全部走 ScriptableObject 配置资产（Data/ 目录），代码零硬编码**——这是最高级约束（见 DesignSpec §9）
9. 新增内容（怪物/障碍/Boss/弹幕/Buff）= 新建配置资产，不改代码

## 2D 基础配置（每步交付前必须覆盖，禁止遗漏）
- 排序图层（唯一真相源见 DesignSpec §7）：Default → Background → Gameplay → Foreground → UI
- 任何 2D 物体必须显式指定 Sorting Layer；忘设的兜底在最底层——不允许"背景盖角色"
- Gameplay 层内 Order：障碍 0 / 鲸鱼娘 5 / 怪物 6 / Boss 7 / 掉落 8 / 弹幕 10 / 特效 12
- 图层名称代码里用 `SortingLayers` 常量类引用，禁止魔法字符串
- 背景：SpriteRenderer Draw Mode = Tiled，Size 覆盖摄像机视野（约 22×11 世界单位），图片左右可无缝拼接
- 素材 PPU 统一 100，由 Editor 导入工具强制

## 里程碑（完成一个勾一个）
- [ ] M0 工程骨架 + 配置框架（当前）
- [ ] M1 主角扑翼 + 世界滚动
- [ ] M2 障碍生成 + 碰撞 + 生命
- [ ] M3 米饭 + 能量 + 护盾技能
- [ ] M4 怪物 + 经验升级 + Buff
- [ ] M5 弹幕系统
- [ ] M6 Boss 战
- [ ] M7 全 UI + 结算
- [ ] M8 打磨（视差/特效/音效）

## 交付规范（每次代码交付必须满足，用户已明确要求）
- 涉及美术/预制体的交付，必须给出完整配置规格：pivot、尺寸（像素+世界单位）、锚点约定、排序图层、碰撞体/触发器设置（以 DesignSpec §8 为准）
- 能由代码强制的基础配置（触发器/排序层/依赖组件）一律代码强制，减少人工装配出错面
- 涉及运行时行为的机制，必须同时解释行为周期/公式（如背景回卷周期），不只给代码
- 已知坑（不许再犯，详见 DesignSpec §10.4）：
  1. 无敌帧标志必须在扣血当帧同步置位，否则同一物理步多碰撞体重复判定
  2. 相邻管道缝隙必须有最大竖直差上限（可达性），否则出现必死墙
  3. 管道贴缝必须按 SpriteRenderer 实际包围盒计算，禁止假设 pivot/尺寸约定
  4. 2D 项目必须覆盖排序图层
  5. 缝隙基准值由预制体实测（运行时从 SpriteRenderer bounds 计算）：代码与配置里禁止出现任何缝隙绝对值
  6. 玩法数值禁止硬编码进代码——一律 ScriptableObject

## 沟通
- 中文，简洁直接；不讨论求职安排与时间线话题（用户已明确）
