# 豆包词墙首个可玩切片验证

## 已验证

- Unity 脚本编译：0 error。
- 豆包遭遇显式配置：通过 `TryValidateConfiguration`。
- 固定模拟环：8 个世界步骤，0 Missing、0 重复；重装遭遇不会残留失效引用。
- 章节附加目标：1 个，0 Missing。
- 气泡文字：正式 Prefab `characterSize = 0.06`，五字短语不再挤出底板。
- 气泡同阵营保护：不会撞碎其他气泡，也不会误伤豆包本体。
- 击败清场：测试伤害被接受后，遭遇进入 `Complete`，活跃气泡从 4 变为 0，豆包隐藏。
- 联机表现：场景已装配 `DoubaoEncounterNetworkChannel`；客户端气泡镜像 Prefab 无 Collider，只有主机执行生成、碰撞与伤害。
- 联机内容版本：`20260914-doubao-1`。
- 前景适配：相机锁定、等比 Cover；豆包锚点随楼体保持构图关系。

## 画面证据

- `doubao_bubble_visual_check_scaled.png`：对话气泡与调整后的字号。
- `world01_foreground_doubao_alignment_v01.png`：前景楼体与豆包楼顶锚点。
- `world01_doubao_encounter_composition_v01.png`：角色、豆包、词墙和前景的合成检查。

## 说明

直接从 `World01_EarlyInternet` 单场景点击 Play 会按既有架构提示必须从 `Boot/MainMenu` 启动，并缺少由入口注入的章节 Profile；这不是豆包功能新增错误。完整流程测试应从主菜单进入第一世界。
