# 2026-10-05 性能优化与等价性验证

状态：本轮代码与下述 Editor 回归完成；内容标识 `20261005-performance-1`，协议仍为 3。没有构建、提交或推送；不是全项目、手机或双端性能验收。

## 范围与计量约束

- 用户要求继续优化系统，包含性能；本轮聚焦 AI 导航/转向 CPU 与联机序列化/分发临时对象。
- 不改变 AI 决策频率、固定步、候选方向、搜索上限、碰撞形状、伤害、掉落、准备/消费规则，也不出包。
- 环境：Unity 6000.6.0f1 Windows Editor / Mono，i7-13700H，20 逻辑核，约 32 GB RAM，Iris Xe；编辑器观测不能外推手机帧率。
- CPU 测量排除夹具、反射、测试报告与断言；网络 CPU 和 GC.Alloc 次数分两轮，避免探针开销混入 CPU 比较。
- 本机 `GC.GetAllocatedBytesForCurrentThread()` 对静态保活的 `new byte[4096]` 也返回 0；早期基线中的 `0 bytes` 无效，已明确标注，不作为零分配证据。
- 替代探针为 Unity `ProfilerRecorder` 的当前线程 `GC.Alloc` 事件数：空区间必须 0，已知数组分配必须 1；记录容量溢出则结果作废。marker 的 Value 单位是时间，不是字节。探针不改变全局 Profiler 开关并在 finally/Dispose 释放。
- 原始输出见 [baseline.md](baseline.md)，其中早期字节计数已作废；有效的网络分配次数取最后的校准组。

探针接口参考 Unity 官方 [CollectOnlyOnCurrentThread 示例](https://docs.unity3d.com/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html)；是否可用以本机空操作/保活数组校准为准。

## 实际改动与结果

AI：候选位集跳过零位且保持原顺序；每次寻路按需缓存访问到的网格坐标（每实例新增约 36 KiB 固定缓冲）；连续碰撞的常见路径先计算 X 轴拒绝，再算 Y 和原精确几何；切预测窗口分支不变。末端转向复用常量方向/本次预测参数，感知复用调用内 bounds；无倒地队友时不计算未使用的救援风险，HS 非近战不做未使用的近战范围扫描。没有跨固定步危险缓存、降频或减少预测样本。

首次尝试全网格坐标预计算令密集基准略退，已改为按需计算；未保留该候选方案。

| 相同受控负载 | 优化前 | 优化后 | 解释 |
| --- | --- | --- | --- |
| 连续刷泡 DS，TryGetCommand 平均 | 0.4091 / 0.4096 ms | 0.2981 / 0.2957 ms | 两次独立复跑，下降约 27% |
| 连续刷泡 HS，TryGetCommand 平均 | 0.4101 / 0.4101 ms | 0.2974 / 0.2951 ms | 两次独立复跑，下降约 27%–28% |
| 连续刷泡决策调用 P95 | DS 3.21 / HS 3.24 ms 左右 | 两角色约 2.33 ms | 是调用分布，不是整帧耗时 |
| 256 障碍冷规划 p50 | 0.3988 ms | 0.3216 ms | 路径、结果和展开数指纹相同 |
| 发 1 个 30 B 反馈包，GC.Alloc 次数 | 15 | 1 | 保留 Transport 独立 byte[] |
| 发 64 个单渲染层实体，GC.Alloc 次数 | 1600 | 64 | 减少 96%；每个实体仍独立包 |
| 发 240 气泡快照，GC.Alloc 次数 | 981 | 3 | 包长仍 5785 B，没有减少字段 |
| 收反馈 +8 观察者，GC.Alloc 次数 | 8 | 7 | 仅去掉每包订阅数组 |
| 收 240 气泡 +7 观察者，GC.Alloc 次数 | 250 | 249 | 字符串/接收流仍有分配，未称解决 |

联机：Session 复用 MemoryStream/BinaryWriter 工作区，最终仍 ToArray；嵌套发送使用独立工作区。分方向缓存不可变委托的订阅列表，本包使用局部快照，保持订阅变更、嵌套接收和异常隔离语义。World 按索引遍历显式池并缓存每个 Source 的写回调/Transform。`NetworkBinaryWriter` 只重写 float 输出：位重解释后使用标准 int 小端写法；未变更其他类型编码、可靠性或目录校验。

实测预分配标准 BinaryWriter 的 uint/string 写入各 0 次分配，float 每次 1；新 float 写入 0 次。原始 signaling NaN `7FA12345` 在本机 Mono 进入 Write 前被 quiet 为 `7FE12345`，最初测试错误地要求恢复输入整数位型而失败。已修正该测试假设，仍保留所有 4113 原始位型和标准/新 writer 逐字节比较；Runtime 没有为通过测试更改 NaN 行为。

三个真实 writer 固定负载指纹优化前后不变：反馈 `CD825C3DAE13BFA0`、实体 `039F044342BDC52C`、豆包 `4A70ADCFA0B5F403`。网络 CPU 测试有明显 Editor/GC 波动，主要收益证据使用稳定的分配次数，不声称接收端 CPU 已改善。

## 回归入口

- `CompanionNavigationBenchmark.Run("DA204AB3282AEE38")`：9 类固定输入，逐路径/结果/展开节点指纹一致；包含冷规划、动态与缓存路线。
- `CompanionNavigationHotPathChecks.Run()`：候选位集原索引升序完整性。
- `CompanionNavigationCollisionChecks.Run()`：101620 随机/边界几何与冻结旧算法一致；同进程交替 A/B 远泡/混合短步成本比 0.761 / 0.890，切窗约 0.983。
- `CompanionSteeringEquivalenceChecks.Run()`：512 组固定场景输出逐分量一致；此处使用当前碰撞入口，碰撞等价性由上一独立检查覆盖。
- `CompanionPerceptionBenchmarks.Run()`：新鲜 Play 选角暂停下离屏合成障碍/弹体，感知与末端转向分项；P95 为批次均值分位，不是整帧尖峰。
- `CompanionMazeBrainChecks.Run(true)`：新鲜 World01 选角暂停，真实脑/电机与连续刷泡各角色 45.02 秒；只消费移动，不消费攻击/技能，玩家不发生真实碰撞伤害。
- `CompanionMazeBrainChecks.Run(false, true)`：另跑同一流程的分配次数，不与 CPU-only 耗时混比。
- `NetworkHotPathBenchmark.Run()`：隔离实体、真实 writer/catalog/receive；消息字节与可靠性前后相等。World 指标为直接 Publish，不包含 LateUpdate 的池枚举。
- `NetworkSessionBufferChecks.Run()`：数组所有权、嵌套收发、写异常恢复、订阅快照变更、坏帧恢复、方向隔离。
- `NetworkBinaryWriterChecks.Run()`：4113 位型与混合编码对照及校准后 float 分配检查。

## 回归完成情况

完整原始输出见 [results.md](results.md)、[final-confirmation.md](final-confirmation.md)。

- 连续刷泡 DS/HS 各 2251 步，两次 CPU 复跑和一次独立 GC 复跑：距离 32.31 / 45.01 u、移动步 573 / 749、0 几何重叠、泡峰 110、2189 个真实下落样本均与本轮基线一致。不能与历史文档使用不同上下文的距离混作 A/B。
- 独立 GC 次数跑中，每角色预热后的 2151 次 command 调用未观测到分配；encounter+physics 各有 528 次，仍待细分。这不等于游戏整体零 GC。
- 两场景各 6 真实 writer、15 反馈、20 托管路由、25 受击网络、40 非致命本地受击、27 节点目标、10 组数值入门和 6 组绕墙闭环通过；World 另测双托管编队。数值测试的 solo voluntary 与无活泡 live snapshot 分支明确跳过；连续刷泡由专用测试单独覆盖。
- 协议 831 断言、节点/豆包组合接收 21、缓冲语义 8、World 表现寿命检查通过。
- 真实场景往返 16 站通过，包括本机开房/复用端口及 loopback 客户连接中退出；不是双机/中继测试。
- 两场景只读装配审计 881 / 915 项，0 error / warning。最终 Unity Console error 0；退出 Play，Boot，Profiler 恢复关闭；git diff --check 通过，只有原有工具文件换行提示。

后续优先按设备测量展开：移动端 IL2CPP 帧时间/GPU；豆包接收字符串与 UI/特效池实际开销；encounter+physics 的 528 次事件来源；密集或新增机制的同固定步 AI 峰值。不要先扩池、降精度或削减表现，也不要把这些待查项写成已确定的卡顿原因。

## 不包含的验收

移动端 IL2CPP/GPU/温升/内存峰值、真实双端网络与历史偶发特效症状仍待后续统一新包验证。合成 CPU 微基准不能代替实战或设备验收。没有承诺“后续新内容不会再出 Bug”。
