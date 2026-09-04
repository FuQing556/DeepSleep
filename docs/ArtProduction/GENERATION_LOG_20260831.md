# 2026-08-31 角色候选生成日志

> 工具：Codex 内置 `image_gen`（imagegen 技能的默认模式）。  
> 用户已通过本页全部人物候选。除 Opus v02 明确检查过 Alpha 外，其余设定表仍不是可直接切片的透明生产素材；“视觉通过”不等于“Sprite 已生产”。

## 1. 公共提示规格

以下公共规格应用于本轮所有人物设定表：

```text
Use case: stylized-concept
Asset type: 2D Unity game character turnaround / expression concept sheet
Scene/backdrop: clean pale neutral studio sheet, no screenshot UI
Style/medium: polished Japanese anime chibi game concept art; 2.4–2.8-head proportions; crisp deep-colored outer line; clean cel shading; readable silhouette
Composition: full body visible; separated front, three-quarter, side and back views; same identity and outfit in every view
Constraints: no labels, no watermark, no unrelated characters; preserve the supplied identity reference
Avoid: photorealism, 3D render, sexualized clothing, role-specific traits from other AI characters
```

每项的角色专用提示如下；它与公共规格合并后构成最终提示。

## 2. 生成记录

| 输出 | 输入参考 | 角色专用提示/硬约束 | 状态 |
|---|---|---|---|
| `Characters/Gemini/CONCEPT_GE_Turnaround_v02_CANDIDATE.png` | `AiSister/Gemini.jpg` | 蓝紫长发、猫耳猫尾、异色瞳、四角彩虹星、紫蓝白服装；黄色香蕉编辑笔≤5%；禁鲸鳍、龙角、无猫普通法师 | 候选；旧 v01 已归档 |
| `Characters/ChatGPT/CONCEPT_GPT_Turnaround_v01_CANDIDATE.png` | `AiSister/ChatGPT.jpg` | 银白长发、白弯龙角、淡紫眼、白紫龙翼、粗大鳞片龙尾、白银淡紫礼服、结纹饰；终章女王/大魔王气质；禁黑绿魔女 | 候选 |
| `Characters/Doubao/CONCEPT_DB_EventPortraits_v01_CANDIDATE.png` | `AiSister/Doubao.png` | 棕色短波波头、暖棕眼、简单黑上衣；认真、过度自信、道歉、敷衍、惊讶、结束六表情；三种空白气泡；禁豆沙包发髻 | 候选 |
| `Characters/Opus/CONCEPT_OP_GiantJumpHalf_v01_CANDIDATE.png` | `AiSister/Claude.jpg` | 橙色长发、橙花、象牙/橙/黑学者裙、厚书；巨大半身从下方猛跃；禁静态山与章鱼 | 历史候选；背景棋盘被烘焙，禁入 Unity |
| `Characters/Opus/CONCEPT_OP_GiantJumpHalf_v02_CANDIDATE_ALPHA.png` | Opus v01 作为编辑目标 | 仅移除棋盘背景，人物、姿势、书、头发、线稿和构图保持；真透明 Alpha、无光边 | 候选；1402×1122，四角 Alpha=0 |
| `Characters/Grok/CONCEPT_GR_Turnaround_v01_CANDIDATE.png` | `AiSister/Grok.jpg` | 金发蓝眼、小型黑红恶魔翼、黑红金哥特裙、完整 X 巨戟；PG；禁色情、紫发猫娘与速写本 | 候选 |
| `Characters/Jimeng/CONCEPT_JM_TurnaroundOperator_v01_CANDIDATE.png` | `AiSister/Jimeng_A.jpg`、`Jimeng_B.jpg` | 白色双马尾、蓝紫渐变、星夹/星瞳、圆眼镜、白蓝服、橙领带；独立视频时间轴控制台；禁截图字幕与红黑导演娘 | 候选 |
| `Characters/GLM/CONCEPT_GL_Turnaround_v01_CANDIDATE.png` | `AiSister/GLM.jpg` | 黑长发、黑白兽耳、Z 眼罩、铃铛、黑白巨型蓬松尾、黑白哥特裙；禁冰蓝研究员与白狐配色 | 候选 |
| `Characters/Qwen/CONCEPT_QW_Turnaround_v01_CANDIDATE.png` | `AiSister/Qwen.jpg` | 蓝紫长发与编发、中式帽、折扇、手袋、蓝白中式大小姐服；禁翡翠普通学者 | 候选 |
| `Characters/MiniMax/CONCEPT_MM_TurnaroundMedia_v01_CANDIDATE.png` | `AiSister/Minimax.jpg` | 珊瑚橙发、米粉贝雷帽、海螺、录音器、场记板、影像屏；禁仅有麦克风的普通偶像 | 候选 |
| `Characters/OpenCode/CONCEPT_OC_TurnaroundLocalAgent_v01_CANDIDATE.png` | `AiSister/Opencode.jpg` | 黑长发、机械分枝鹿角、方形瞳、黑银分层赛博服、本地终端与仓库补丁卡；透明外层下必须有不透明底衣 | 候选 |
| `Characters/Zcode/CONCEPT_ZC_TurnaroundBlueprint_v01_CANDIDATE.png` | `AiSister/Zcode.jpg` | 白发白狐耳、Z 眼罩、铃铛、白/冰蓝工程服、巨大卷起和展开蓝图；禁黑色 GLM 配色 | 候选 |

## 3. 明确保留的既有概念

下列素材未重画，也未归档：

- `Characters/ClaudeCode/CONCEPT_CL_Turnaround_v01.png`：用户批准继续使用圆润橙色章鱼工程师；像素参考只作原型。
- `Characters/CodexPet/CONCEPT_CO_Turnaround_v01.png`：用户批准继续使用方块/外星终端桌宠；蓝色云团终端图只作原型。
- `Characters/DeepSeek/`、`Characters/Harness/`、`Characters/KimiK3/`：已锁定的方向继续保留。

## 4. 退役记录

以下文件移动到 `_Retired_WrongReference_20260831/`，保留可恢复历史但不再作为生成参考：

- Gemini v01 turnaround/action：缺少猫耳、猫尾、异色瞳。
- Style Master v01：包含错误 Gemini，继续使用会污染新角色。

## 5. 下一步准入条件

1. 用户已确认候选角色轮廓和主色。
2. 对应玩法落地后才单独生成透明正交状态图；默认每个语义状态一张，以 Unity Transform/颜色/VFX 表现，不从本页拼接或自动抠图冒充正式资产。
3. 透明图逐张检查 Alpha；棋盘格烘焙、白底和黑底都视为不合格。
4. 进入 `Assets/_Project/Art/` 前补齐 `06_ArtAssetManifest.md` 的尺寸、Pivot、帧数和用途。
