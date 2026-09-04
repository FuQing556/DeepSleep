# 项目协作说明（开发 Agent 必读）v4.0

## 1. 开工前固定读取

1. `docs/DesignSpec_v5.md`：唯一当前入口与权威顺序。
2. `docs/18_CoopShooterGameplaySpec.md`：合作射击、角色、AI和章节规则。
3. 涉及联机/热更新时完整读取 `docs/19_OnlineAndHotUpdateArchitecture.md`。
4. `docs/20_Unity6MigrationAndDeliveryPlan.md`：当前门禁与下一小步。
5. 当前任务对应的仍有效分册；不能从旧扑翼文档猜实现。

`DesignSpec.md`、`DesignSpec_legacy_v1.md` 与 `agent_legacy_v1.md` 均为历史版本。旧 `01/03/04/05/07/09/10/12/16/17` 不再是实现权威，禁止复制其中的扑翼、双路线、单机和旧输入规则。

## 2. 当前工程事实

- 当前工作目录是全新的 Unity 6.6（6000.6.0f1）通用二维（Universal 2D）工程；Unity 2022.3.48f1c1 旧工程完整保留作档案。
- 正式目标为 Windows x64 与 Android 横屏，支持电脑↔手机、电脑↔电脑、手机↔手机跨端双人合作。
- 玩法逻辑视野固定为 16:9、19.2×10.8u；更宽屏幕只扩展无碰撞背景，UI 必须进入 Safe Area。
- 新工程已安装 Input System 1.20.0；模板自带的 `InputSystem_Actions.inputactions` 不是正式玩法输入，正式 Actions 尚未由用户创建。新工程尚无玩法代码和 Prefab。
- 旧 `Flap/Shield/HarnessAim` 动作已失效且不迁入新工程；Unity 6新工程基础验收后由用户在Input Actions编辑器中重新创建。
- 当前目录已初始化 Git，分支为 `main`，Git LFS 与 GitHub `origin` 已配置；提交和推送状态必须以当轮 `git status` 为准。
- U0 Unity 6.6 新工程验收与首次本地 Git 基线已于 2026-09-04 完成：诊断脚本成功执行，Unity/VS 编译链路正常，Console 为 0 Warning、0 Error。下一里程碑为 U1 输入命令契约。

## 3. 人机职责边界（2026-08-31 用户锁定）

### 3.1 Agent 负责

- 编写和修改 C# Runtime、Editor 校验器与测试代码。
- 设计配置类型、接口、数据校验规则和组件依赖图。
- 生成角色、背景、UI、VFX、纹理等素材；候选先放 `docs/ArtProduction/`，通过技术验收且用途已由用户明确的生产资产才进入 `Assets/_Project/Art/`。
- 每一小步提供完整 Unity 编辑器操作说明、Inspector 字段表和人工验收步骤。
- 根据用户返回的完整 Console 错误、Test Runner 结果或录屏继续修正。

### 3.2 用户负责

- 所有 Unity Editor 操作：安装包、Project Settings、Layer、Sorting Layer、Input Actions、场景、GameObject、Prefab、ScriptableObject、挂组件、拖引用、填写 Inspector、Sprite Import Settings、Build Settings、运行测试和 Play Mode 验收。
- 决定候选素材是否进入正式资产目录。
- 把 Unity 实际结果反馈给 Agent；Agent 不得把静态检查冒充 Unity 实机通过。

### 3.3 Agent 默认禁止直接修改

- `*.unity`、`*.prefab`、`*.asset`、`*.meta`。
- `ProjectSettings/*`、`Packages/manifest.json`、`*.inputactions`、`*.asmdef`。
- Unity 自动生成目录与缓存：`Library/`、`Temp/`、`Logs/`、`Obj/`。

只有用户明确点名授权某一文件时才可例外；例外仍需先说明影响。禁止手写 Unity YAML 来“省几步编辑器操作”。

## 4. 零隐式装配规则

- Runtime/Editor 代码禁止调用 `AddComponent` 自动补组件。
- 禁止 `[RequireComponent]`，因为挂脚本时会隐式添加组件。
- 禁止因引用为空而 `new GameObject()`、创建替代组件或悄悄降级运行。
- 禁止 `GameObject.Find`、`FindObjectOfType`、`FindAnyObjectByType`、Tag/名称查找和全局服务定位器。
- 禁止 `Resources.Load`、字符串路径加载和从 Prefab 名称解析玩法 ID。
- 允许对象池/Spawner 实例化用户已装配好的 Prefab；实例化后不得改变它的组件构成。
- 所有依赖通过 `[SerializeField]`、显式 `Initialize(...)` 或构造函数暴露。缺失依赖时输出带对象路径和字段名的 Error，禁用当前功能，不自动修复。
- `OnValidate`、自定义 Validator 和菜单工具默认只检查/报告，不添加组件、不改 Layer、不写引用、不改数值。
- `CreateAssetMenu` 只提供用户主动创建配置的菜单入口，允许使用；代码不得自行创建并保存资产。

## 5. 零玩法硬编码规则

下列内容必须来自 ScriptableObject、Prefab 序列化字段、Input Actions 或本地化表，禁止写在运行时代码常量中：

- HP、伤害、速度、重力倍率、冷却、能量、半径、持续时间、预警时间、Buff 数值、难度倍率。
- 关卡时间、生成坐标、掉落、Boss 阶段阈值、Pattern 参数、安全口、对象池容量。
- LayerMask、Sorting、Prefab/Sprite/Audio 引用、场景名、输入绑定、文案和本地化文本。
- 章节、路线、角色、伤害来源等运行时 ID 的实际数据映射。

允许写在代码中的只有稳定算法结构：枚举成员、状态转换规则、数学公式、接口契约、无法调参的安全 epsilon 与数组边界检查。即使是算法常量也必须有命名和“为什么不可配置”的注释。

执行方式：

1. Agent 先写配置类和读取逻辑，不预填隐藏默认玩法值。
2. Agent 在交付中列出要由用户创建的配置资产、每个字段和值及其权威文档出处。
3. 用户在 Unity 创建并填写资产、拖入引用。
4. Validator 检查缺失/越界，但绝不替用户改值。
5. Runtime 在开始章节时把配置复制为只读运行时快照；不写回原资产。

## 6. 代码规则

- Runtime：`Assets/_Project/Scripts/Runtime/`，程序集 `DeepSleep.Runtime`。
- Editor：`Assets/_Project/Scripts/Editor/`，程序集 `DeepSleep.Editor`。
- 类型/方法 PascalCase；私有序列化字段 `_camelCase`；常量 `UPPER_SNAKE_CASE`。
- 中文注释解释“为什么”；公共 API 写 XML summary。
- 禁止 `Find`、`FindObjectOfType`、`SendMessage`、字符串事件名、静态服务定位器。
- 局部一对一依赖用 Inspector/初始化直接引用；跨域一对多才用类型化 SignalHub。
- 高频对象必须池化；池对象 Rent/Return 必须清理速度、协程、订阅、透明度、Collider 与 Animator。
- 热路径禁 LINQ、装箱、字符串拼接、每帧 GetComponent；稳定战斗 GC=0B/frame。
- ScriptableObject 保存共享调参；Prefab 保存稳定组件图；本局状态保存在运行时实例。不得混写。
- 新“数值变体”可配置；新“行为策略”必须写代码与测试，禁止用反射类型名伪装零代码扩展。

所有 MonoBehaviour 必须明确列出它需要用户挂载的同物体/子物体组件。不得因为“反正可以 GetComponent”而隐藏装配关系；只允许在初始化阶段缓存已明确存在的组件，热路径禁止 GetComponent。

## 7. 场景与 2D 规则

- Physics Layer 和 Sorting Layer 是两套概念，按 `05_UnityAssemblyGuide.md` 创建。
- 正式 Sprite 不使用 Default Sorting Layer。
- 相机 Orthographic Size=5.4，对应 19.2×10.8 世界视野。
- 玩家改为无重力自由二维移动；物理根不因视觉俯仰旋转，只旋转/偏移 VisualRoot。
- 玩家 Collider 不覆盖头发、鲸尾、鳍耳。
- 实体障碍的碰撞和通道来自关卡配置与显式Anchor；不得从 Sprite bounds 反推玩法区域。
- Android、Windows、同伴AI和网络玩家必须共用同一 `PlayerCommand` 与玩法状态机；设备输入只是命令来源，不得复制移动端或网络专用PlayerMotor。
- 触屏必须支持移动手指与技能/瞄准手指同时按住；不能用单一“当前触点”吞掉第二根手指。
- 横向循环背景的 PNG 首列与末列必须逐像素一致，并提供双副本预览；未经检查的宽幅插画只可标为 `DECOR_ONLY`。

## 8. 文档与数值

- 当前玩法规则唯一来源：`18_CoopShooterGameplaySpec.md`；具体数值等灰盒测试后进入新的配置表，不再读取旧 `01_GameplaySpec.md`。
- 联机与热更新唯一来源：`19_OnlineAndHotUpdateArchitecture.md`。
- 开发门禁与下一步唯一来源：`20_Unity6MigrationAndDeliveryPlan.md`。
- 素材规格继续参考 `06_ArtAssetManifest.md` 与 `13_AssetReuseAndProductionPlan.md`，但旧扑翼动作语义必须按v5重映射。
- AI 梗与造型唯一来源：`02_ContentMemeBible.md`。
- 发现冲突先停止复制，修正文档；不得自行选择“看起来顺眼”的值。

## 9. 每轮只推进一个可验证小步

每轮开始时先声明本轮唯一目标和明确不做的内容。交付必须按以下顺序：

1. 本轮完成内容与修改文件。
2. 新增类的职责、依赖和数据流。
3. Unity 编辑器操作：精确到菜单路径、场景层级、GameObject 名、`Add Component` 名、拖拽来源。
4. Inspector 字段表：字段名、值、引用对象、权威出处。
5. Test Runner 操作与预期结果。
6. Play Mode 人工步骤、可见结果与失败表现。
7. 明确停止点；用户确认通过前不继续下一系统。

如果本轮代码尚未由用户在 Unity 编译/测试，报告必须明确写“静态完成，Unity 未验证”，不得声称功能已完成。

涉及 Prefab/美术时必须同时给出：文件 ID、像素/世界尺寸、PPU、Pivot、Facing、Sorting、Physics Layer、Collider、切片/帧率。不能只说“把图拖进去”。

素材流程固定为：参考核对 → 候选图 → 用户确认 → 透明/分层生产稿 → 技术检查 → 用户设置 Import → 场景实测。角色默认每个语义状态一张主图，以 Transform/颜色/VFX 做轻量动画；不再把多帧 Sprite Sheet 当通用交付要求。Agent 不删除旧素材、不覆盖用户未批准的正式文件、不手写 `.meta`。

## 10. 报错协作

用户反馈错误时优先提供：Unity 版本、完整 Console 第一条红错及堆栈、出错对象 Inspector 截图、复现步骤。Agent 先修根因，不让用户通过反复重挂组件或随便改数值碰运气。

同一问题修复后必须重跑本轮原验收，不在红错状态继续添加新系统。

## 11. 长期回归

任何物理、池、Boss、时间轴、暂停、Buff、联网或热更新改动，都重测：同帧重复伤害、无敌帧同步、池状态残留、暂停偷跑、旧Boss Pattern未取消、Boss/玩家同帧死亡、重开未重置、失败原因Unknown、网络事件重复、AI接管丢状态、跨端版本不兼容和半份资源被错误启用。

## 12. 联机与热更新附加红线

- 客户端不得自行决定伤害、掉落、Boss阶段、清弹结果或技能最终消耗；由主机权威裁决。
- Windows和Android不建立分叉战斗代码；网络消息不得传平台按键、像素坐标或本地资源路径。
- 本地输入、远端输入与同伴AI只能通过 `ICommandSource` 生成统一命令，不直接操作玩法组件。
- 具体联网、资源和热更新第三方包只能存在于适配器层；玩家/Boss/关卡程序集不得直接依赖云服务SDK。
- 热更新下载必须临时写入、完整校验、原子切换并保留上一可用版本；不得覆盖式下载到当前有效内容。
- `ClientVersion`、`ProtocolVersion`、`ContentVersion` 分开；任何协议破坏性修改都必须拒绝旧版本混房。
