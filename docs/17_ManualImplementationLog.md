# 17｜人工装配步骤与验收日志（Unity 2022旧工程档案）

> 2026-09-04：旧工程完整保留作档案，本日志不再驱动新工程。相机、图层、Input System和旧Input Actions虽已由用户完成，但新Unity 6工程会逐项重新建立和验收。

> 本文件记录用户在 Unity Editor 中执行的每一个小步骤。Agent 只读检查工程文件、提供操作说明；用户确认通过后才把步骤标为完成。  
> 当前 Unity：2022.3.48f1c1。

## STEP-001｜双端视图预设、相机范围与图层骨架

**状态**：`WAITING_USER_VALIDATION`  
**本步唯一目标**：确定 16:9 逻辑玩法范围、建立桌面/手机/平板检查视图，并建立后续渲染/物理分组名称。  
**明确不做**：不安装 Input System、不写代码、不改碰撞矩阵、不创建正式场景/Prefab、不挂任何玩法组件。

### A. 已读取的当前状态

| 项目 | 当前值 | 本步目标 |
|---|---|---|
| Unity | 2022.3.48f1c1 | 保持 |
| Player 默认分辨率 | 1920×1080 | 已正确，不改 |
| SampleScene/Main Camera Projection | Orthographic | 已正确，不改 |
| SampleScene/Main Camera Size | 5.0 | 改为 5.4 |
| Camera Position | (0,0,-10) | 已正确，不改 |
| Physics User Layers | 全空 | 填写 6–12 |
| Sorting Layers | 只有 Default | 添加 Background/Gameplay/Foreground/UIWorld |
| Input System | 未安装 | 留到后续步骤 |

### B. 设置 Game View 比例

1. 打开 `Window > General > Game`。
2. 点击 Game 面板左上角当前显示 `Free Aspect` 的下拉框。
3. 点击列表底部 `+`。
4. 添加第一项：
   - Label：`DS 16:9`
   - Type：`Aspect Ratio`
   - Width：`16`
   - Height：`9`
5. 再点击 `+` 添加第二项：
   - Label：`DS 1920x1080`
   - Type：`Fixed Resolution`
   - Width：`1920`
   - Height：`1080`
6. 添加手机宽屏检查项：
   - Label：`DS Phone 20:9`
   - Type：`Aspect Ratio`
   - Width：`20`
   - Height：`9`
7. 添加平板窄屏检查项：
   - Label：`DS Tablet 16:10`
   - Type：`Aspect Ratio`
   - Width：`16`
   - Height：`10`
8. 日常调试选择 `DS 16:9`；检查 UI 像素时切换到 `DS 1920x1080`；移动适配回归时分别看 `DS Phone 20:9` 与 `DS Tablet 16:10`。

说明：Game View 自定义比例属于本机 Editor 布局，不作为项目玩法数据。20:9 以后会露出 16:9 逻辑区两侧的额外背景；16:10 以后会完整显示 16:9 玩法区并留装饰边。STEP-001 尚未实现适配器，所以本步只建立测试入口，不要求当前空场景已经出现最终留边效果。

### C. 设置 SampleScene 相机

1. 打开 `Assets/Scenes/SampleScene.unity`。
2. 在 Hierarchy 选择 `Main Camera`。
3. Inspector 的 Camera 组件填写：

| 字段 | 值 |
|---|---|
| Projection | Orthographic |
| Size | 5.4 |
| Viewport Rect | X=0, Y=0, W=1, H=1 |
| Target Display | Display 1 |

4. Transform 保持：Position `(0,0,-10)`、Rotation `(0,0,0)`、Scale `(1,1,1)`。
5. `Ctrl+S` 保存场景。

验算：Orthographic Size 是半高，所以世界可视高度为 `5.4×2=10.8u`；在 16:9 下宽度为 `10.8×16/9=19.2u`。

### D. 创建 Physics Layers

1. 打开 `Edit > Project Settings > Tags and Layers`。
2. 展开 `Layers`。
3. 只填写下列 User Layer 槽位，其他槽位保持空白：

| 槽位 | 名称 | 未来用途 |
|---:|---|---|
| 6 | `Player` | 两位可玩角色的物理根 |
| 7 | `PlayerProjectile` | DeepSeek 饭团等玩家弹体 |
| 8 | `Enemy` | 普通敌人和 Boss 身体 |
| 9 | `EnemyProjectile` | 敌弹与 Boss 弹幕 |
| 10 | `Obstacle` | 真实管道、事件伤害体、边界 |
| 11 | `Pickup` | 白米饭、算力凭证、Token |
| 12 | `Trigger` | 无伤害关卡触发器 |

本步不修改 `Physics 2D > Layer Collision Matrix`。等 STEP-001 验收后，碰撞矩阵会作为独立小步骤配置，避免一次改动无法定位错误。

### E. 创建 Sorting Layers

仍在 `Tags and Layers` 中展开 `Sorting Layers`，使用右下角 `+` 添加并拖动为以下从上到下顺序：

```text
Default
Background
Gameplay
Foreground
UIWorld
```

- `Default` 无法删除，暂时放最上方；正式 Sprite 不使用它。
- Unity 按 Sorting Layer 列表顺序参与渲染排序；同一 Sorting Layer 内再由 SpriteRenderer 的 `Order in Layer` 决定，数字较大者覆盖数字较小者。
- 本步没有正式 SpriteRenderer，因此只创建名称和顺序，不创建占位对象。

后续固定 Order 预算（本步不用填写）：

| Sorting Layer | 内容 | Order in Layer |
|---|---|---:|
| Background | Far / Mid / Near | -30 / -20 / -10 |
| Gameplay | 障碍后部 / 玩家 / 普通怪 / Boss | 0 / 20 / 30 / 35 |
| Gameplay | 拾取 / 玩家弹 / 敌弹 / 角色 VFX | 40 / 50 / 55 / 60 |
| Foreground | 无碰撞前景装饰 | 0 |
| UIWorld | 世界空间提示、选区、安全描边 | 按对应 Prefab 另填 |

### F. 用户验收

完成后不要继续安装 Input System。依次确认：

1. Game View 下拉中存在 `DS 16:9`、`DS 1920x1080`、`DS Phone 20:9`、`DS Tablet 16:10`，当前选择 `DS 16:9`。
2. Main Camera 为 Orthographic、Size=5.4、Position=(0,0,-10)。
3. Layers 6–12 名称和大小写完全一致。
4. Sorting Layers 从上到下与本页一致。
5. Console 没有红色 Error。
6. 保存场景并关闭再打开 Project Settings，确认设置仍存在。

请回传以下任一种证据：

- Game View 下拉、Main Camera Inspector、Tags and Layers 三张截图；或
- 逐项回复“B/C/D/E 已完成”，并附 Console 红错（若有）。

验收通过后，本页状态改为 `PASSED`，再开始下一小步。
