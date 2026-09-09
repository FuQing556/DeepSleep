# 开局角色选择：首轮灰盒实现

## 本轮边界（2026-09-09）

本轮只实现本地单人开局时选择自己控制 `DeepSeek` 或 `Harness`。不生成UI素材，不确定最终视觉风格，不实现同伴AI、章节选择、存档、联网房间或角色介绍动画。

当前灰盒使用正式 uGUI 结构，不使用 `OnGUI`。未来更换字体、面板素材、角色立绘和过渡动画时，不需要改写角色控制权分配。

## 数据流

```text
DS按钮 / HS按钮
        ↓ PlayerRole
OpeningCharacterSelectionController
        ↓ TrySelectLocalPlayerRole
PlayerControlAssignment
        ├─ 所选角色 ← UnityInputCommandSource
        └─ 未选角色 ← CompanionCommandSource（当前为空，未来接同伴AI）
```

两个角色实体始终同时存在。选角只切换 `ICommandSource`，不会销毁、重建、隐藏或复制玩家对象，因此以后接入AI、网络接管和断线交还时可以继续复用同一个角色状态。

## 场景结构

```text
UI_OpeningCharacterSelection
├─ Dimmer
└─ SafeArea
   └─ CharacterSelectionPanel
      ├─ Title
      ├─ Hint
      ├─ SelectDeepSeek
      │  └─ Label
      └─ SelectHarness
         └─ Label

UI_EventSystem
```

- Canvas：`Screen Space - Overlay`，Sorting Order `1000`。
- CanvasScaler：`Scale With Screen Size`，Reference Resolution `1920×1080`，Match `0.5`。
- `SafeAreaRectFitter`：只在分辨率或 `Screen.safeArea` 改变时更新锚点，适配手机刘海与圆角安全区域。
- `InputSystemUIInputModule`：鼠标、触摸和导航输入共用 Unity Input System，不另写一套手机按钮逻辑。
- 文本暂用 Unity 内置英文字体，避免最终字体和中文本地化尚未确定时引入临时字体资产。

## 暂停与状态边界

选角控制器在 `Awake` 保存进入前的 `Time.timeScale`，随后将其设为 `0`，所以玩家、物理和敌人不会在菜单背后提前运行。选择成功后恢复原值，而不是固定写回 `1`；这样以后即使从其他暂停状态进入，也不会破坏上层暂停语义。

若面板被外部关闭或对象销毁，控制器也会归还自己持有的暂停，避免场景永久卡在零时间缩放。

`PlayerControlAssignment.Start` 仍会应用场景的初始调试角色，但此时游戏已经暂停；最终按钮选择会覆盖它。等正式主菜单/章节流程出现后，可以把选择结果在进入玩法场景前注入，而不用改变玩家实体。

## 代码与装配

- `Runtime/UI/CharacterSelection/OpeningCharacterSelectionController.cs`：暂停、一次性选择、提交角色与关闭面板。
- `Runtime/UI/Common/SafeAreaRectFitter.cs`：安全区锚点适配。
- `Editor/Setup/OpeningCharacterSelectionSceneInstaller.cs`：用户主动菜单装配器；不进入玩家构建。
- `DeepSleep.Runtime.asmdef` 新增对 `UnityEngine.UI` 的显式程序集引用。
- `Gameplay_Prototype.unity` 已通过 `DeepSleep/设置/装配开局角色选择` 装配并保存。

## 实机验证结果

- 面板在开局出现，背景玩法暂停。
- 选择 `Harness`：`Harness: command=True`，`DeepSeek: command=False`，`Time.timeScale=1`，面板关闭。
- 选择 `DeepSeek`：`DeepSeek: command=True`，`Harness: command=False`，`Time.timeScale=1`，面板关闭。
- 两个方向运行期间控制台均为零 Error/Warning。
- 截图：`docs/ImplementationEvidence/20260909_OpeningCharacterSelection/opening_character_selection.png`。

## 人工验收

1. 打开 `Assets/Scenes/Gameplay_Prototype.unity` 并进入播放模式。
2. 确认开局出现两个按钮，背景角色和敌人保持静止。
3. 先选 `DeepSeek`，确认面板关闭且键盘只控制DS。
4. 退出再进入播放模式，改选 `Harness`，确认键盘只控制HS。
5. 2026-09-09 后续增量：未选角色已由同伴AI接管，会移动、攻击及救援；当前行为与调参见 `27_CompanionAI.md`，不再预期静止。

## 后续完整闭环仍缺少

- 为未选角色提供遵循同一 `ICommandSource` 契约的同伴AI。
- 正式角色卡、中文字体、本地化、选中/确认反馈与转场。
- 章节选择、节点快照、设置与存档。
- 联机房间中的双端角色占位、冲突处理与准备状态重置。
