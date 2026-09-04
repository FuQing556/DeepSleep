# DeepSleep Unity 6 交接文档

更新日期：2026-09-04  
目标工程：`D:\Unity Work\DeepSleep_Unity6`

## 1. 本次迁移目标

项目正在从旧 Unity 工程 `D:\Unity Work\DeepSleep` 迁移到全新的 Unity 6 工程 `D:\Unity Work\DeepSleep_Unity6`。

新工程用于继续开发横屏 2D 双人合作射击游戏：

- 支持 Windows 与 Android 跨端游玩。
- 单人模式由 AI 接管另一名角色；双人模式中两名玩家分别选择 DeepSeek 与 Harness，角色不能重复。
- DeepSeek（蓝色鲸鱼娘）负责持续火力、自动索敌和常态输出。
- Harness（红黑色鲸鱼娘）负责清弹幕、手动释放和高消耗爆发。
- 双方生命、能量和拾取资源独立，升级经验共享。
- 有限恢复物包括“大米饭”，防御物包括“不锈钢盆”；倒地后需要队友实施复活。
- 其他 AI 主要作为 Boss、障碍、事件、支援或道具出现。

详细玩法以 `docs` 中的正式设计文档为准，优先阅读：

1. `docs/agent.md`
2. `docs/20_Unity6MigrationAndDeliveryPlan.md`
3. `docs/18_CoopShooterGameplaySpec.md`
4. `docs/19_OnlineAndHotUpdateArchitecture.md`
5. `docs/15_CharacterArrangementAndSkills.md`

## 2. 当前工程状态

- Unity 版本：`6000.6.0f1`
- 工程模板：`Universal 2D`
- Visual Studio 2026 已能打开 Unity 生成的解决方案。
- Input System 包已由模板安装，版本为 `1.20.0`。
- Universal Render Pipeline 包版本为 `17.6.0`。
- 旧工程的 `docs` 已复制到新工程，共核对 83 个文件，复制时源文件与目标文件哈希一致。
- 新工程已初始化本地 Git 仓库，当前分支为 `main`，并已配置 Git LFS。
- `origin` 已指向 `https://github.com/FuQing556/DeepSleep.git`；远端为空，没有旧历史需要迁移。
- U0 开发环境门禁已由用户在 Unity 中实际验证通过：诊断菜单正常执行，Unity/VS 编译链路正常，Console 为 0 Warning、0 Error。
- 旧工程尚未删除。没有验证 Git 迁移和新工程可运行以前，禁止删除旧工程。

## 3. “两个 Scenes”说明

新工程中确实存在两个 `.unity` 文件，但它们不是两个游戏关卡：

1. `Assets/Scenes/SampleScene.unity`
   - Universal 2D 模板创建的示例场景。
   - 当前可作为启动检查用的临时场景。

2. `Assets/Settings/Scenes/URP2DSceneTemplate.unity`
   - Universal 2D 模板自带的新建场景模板源文件。
   - 它配合 `Assets/Settings/Lit2DSceneTemplate.scenetemplate` 使用。
   - 它不是游戏关卡，不会因为存在于工程中就自动进入构建。

这两个文件均由 Unity 的 Universal 2D 模板生成，并非 Codex 创建。当前不要删除第二个模板文件；是否清理模板内容应在正式场景建立并验证后单独决定。

`Assets/Welcome` 同样是 Unity 模板生成的欢迎页内容。截图中 Visual Studio 打开的 `Welcome2DScript.cs` 是 Unity 模板脚本，不是本项目刚创建的诊断脚本。

## 4. 当前由 Codex 新增的工程文件

目前只新增了一个 C# 文件：

`Assets/_Project/Scripts/Editor/Diagnostics/DevelopmentEnvironmentProbe.cs`

用途：

- 验证 Unity 能编译本项目脚本。
- 验证 Visual Studio 与 Unity 的文件关联。
- 在 Unity 顶部添加 `DeepSleep → 诊断 → 验证开发环境` 菜单。
- 点击后在控制台打印 Unity 版本、活动构建目标和 C# 运行时。

它位于 `Editor` 目录，只参与编辑器编译，不会进入游戏构建；不需要挂组件，不修改场景，不创建游戏对象。

## 5. 目录管理规则

`Assets` 是 Unity 强制存在的资源根目录，不能去掉。`Assets/_Project` 是 DeepSleep 自有内容的唯一一级入口，用于与 Unity 模板和第三方插件隔离，不应再创建 `Project` 或 `Projects` 套娃目录。

当前只创建实际使用到的目录：

```text
Assets/
├─ _Project/
│  └─ Scripts/
│     └─ Editor/
│        └─ Diagnostics/
├─ Scenes/                 # Unity 模板生成
├─ Settings/               # URP 与模板设置，Unity 模板生成
└─ Welcome/                # Unity 欢迎页，Unity 模板生成
```

后续的 `Runtime`、`Tests`、`Art`、`Prefabs`、`Audio`、`Data` 等目录只在第一次确实需要文件时创建，不预先铺设空目录。

`docs` 放在项目根目录而不是 `Assets` 内，避免 Unity 把全部设计资料和参考图导入游戏资源数据库。

## 6. 协作边界

- 用户负责所有 Unity 编辑器操作和 Inspector 配置。
- Codex 负责代码、素材、设计文档以及逐步配置说明。
- 每轮只推进一个可验证的小步骤。
- Unity 中文界面名优先，同时在必要处附英文名称。
- 不在运行时代码中使用 `AddComponent` 偷挂组件。
- 不使用 `Find`、`Resources.Load` 或相似的隐式全局查找来掩盖配置缺失。
- 不把玩法数值、场景引用或资源引用硬编码进代码；数据应通过 Inspector、配置资产或明确的依赖注入提供。
- 不由 Codex 擅自编辑场景、Prefab、`.meta` 和 ProjectSettings。
- 不删除现有素材；清理 Unity 模板内容也必须作为独立步骤说明并获得确认。
- 不确定设计意图时直接询问，不猜测角色梗、玩法或美术特征。

## 7. 新窗口接手后的执行顺序

### 第一步：U0 环境门禁（已完成）

2026-09-04 已通过：`DevelopmentEnvironmentProbe` 成功编译并执行，Unity 为 `6000.6.0f1`，活动构建目标为 `StandaloneWindows64`，Console 为 0 Warning、0 Error。

### 第二步：建立仓库基线（本地已完成）

只读审计已确认 GitHub 远端为空。新工程已经创建 Unity 专用 `.gitignore`、LFS `.gitattributes`、本地 `main` 和 `origin`，首次本地基线提交已经完成。尚未推送，旧工程继续保留。

### 第三步：开始 U1 最小运行时实现

Git 工作区确认后，再按 `docs/20_Unity6MigrationAndDeliveryPlan.md` 推进。第一小步应是建立与输入设备无关的玩家命令契约，例如 `PlayerCommand` 和 `ICommandSource`，并先保持为纯 C# 数据/接口；不要直接开始角色移动、联网或堆叠场景组件。

## 8. 尚未完成或不得误判的事项

- 诊断菜单已经在 Unity 中实际验证通过。
- 尚未配置正式场景、构建场景列表、分辨率和安全区。
- 尚未创建运行时程序集定义与测试程序集定义。
- 尚未开始角色移动、战斗、联机、存档或热更新代码。
- 尚未把美术生产文件导入 `Assets/_Project`。
- Git 本地初始化、LFS、远端配置和首次本地提交已完成；尚未推送 GitHub。
- 尚未授权删除旧工程、`Assets/Welcome` 或任何 Unity 模板文件。

## 9. 建议给新 Codex 窗口的首条消息

> 请先完整阅读项目根目录的 `HANDOFF.md`、`docs/agent.md` 和 `docs/20_Unity6MigrationAndDeliveryPlan.md`。当前先完成 U0 环境门禁，然后只读检查旧仓库与新工程的 Git 状态，给出保留原 GitHub 历史的迁移方案；未经我确认不要删除旧工程、移动 `.git`、编辑场景或提前创建一堆空目录。
