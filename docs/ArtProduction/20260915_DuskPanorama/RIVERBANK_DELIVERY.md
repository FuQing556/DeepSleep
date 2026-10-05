# 河岸前景接入与菜单居中记录

日期：2026-09-15。用户否定河流背景前叠住宅楼群，随后同意：左侧近岸草木、右侧堤岸小院平台，豆包从楼顶改站平台。该确认覆盖旧楼群与楼顶构图要求。

## 已修改

- 使用内置 image_gen，引用已生成河流全景作为配色、透视与画风参考，生成独立透明河岸素材；未重画背景或角色。
- 首图 raw/FG_W01_Riverbanks_clipped_v01.png 外侧树冠被裁切，未采用。编辑后 raw/FG_W01_Riverbanks_v02.png 作为本轮候选接入，保留生成器透明通道，没有本地抠图、拉伸或镜像。
- 正式路径：Assets/_Project/Art/Foregrounds/World01/FG_W01_Riverbanks_v02.png。两片由 Unity Sprite 切片独立装配，PPU 100、底部中心 Pivot、Bilinear、无 Mipmap、Clamp；Gameplay Order 30。源图 1774×886，两片分别取 (0,40,887,650) 与 (887,40,887,650)。
- World01 已使用有限河流全景；旧循环组件与旧瓦片渲染停用，未删除原资产。旧 ResidentialClusters 前景子节点保留但 inactive，不再展示。
- 左侧河岸宽 6.4u、中心 X=-7.8；右侧宽 5.8u、中心 X=7.1，底部 Y=-5.7。豆包根锚点 (5.9,-3.7)，按合成脚底/平台石面校准，未改人物缩放或碰撞体。
- 历史对象名 World01_ForegroundCity、DoubaoRooftopAnchor 为保持引用暂留，现语义为河岸与平台，不代表楼房仍有效。
- SafeAreaRectFitter 增加默认关闭的对称避让选项；两个玩法场景与 MainMenu 的 9 个非触控安全区根已显式启用。UI_TouchControls 保持原始设备安全区。全屏遮罩继续使用原有 FullScreenBackdrop，不称为本轮新增修复。

## 检查与边界

- 50 组有限全景覆盖数学测试通过。
- 12 组安全区居中/保持触控范围数学测试通过。
- 两个玩法场景中真实 ExitConfirmation RectTransform 层级，各测试左右不对称避让，共 4 项通过；触控策略未变，原全屏遮罩组件存在。
- composition_riverbank_v02/ 共 6 张合成预览：16:9 和 22:9、镜头左/中/右。使用场景真实角色 Sprite，豆包临时显示于平台；不包含动态气泡或 UI。
- 从 Boot/MainMenu 正常路由进入 World01 后，在 Play 模式暂停时间、临时激活豆包、执行两个比例各 121 个相机横移位置检查（共 242）：背景 bounds 始终覆盖视野，豆包根始终位于平台锚点。检查后恢复相机、时间和本体状态并退出 Play。不是完整遭遇或联机实机测试。
- 本轮运行后 Console error 查询为 0。工程此前有旧 Editor API 弃用 warning，未顺手清理，也不报告 0 warning。
- 尚未做手机安装包验收、菜单实际刘海设备截图、完整战斗可读性/联机体验验收。保留候选质量判断与后续用户反馈空间，不称最终美术批准。
- 未制作耀斑、未实现迷宫重构、未处理 AI 自动准备；未构建、提交或推送。

## 实际生图提示词

首次生成（河流全景仅作风格与视角参考）：

```text
Use case: stylized-concept. Input image is ONLY a palette, painting style and perspective reference, NOT an edit target. Generate a new transparent 2D game FOREGROUND SPRITE SHEET with TWO SEPARATE low riverbank landscape pieces, left and right, wide empty transparent space between them. Match the reference's natural detailed hand-painted Chinese countryside at dusk: subdued olive foliage, blue-purple shadows, gentle peach highlights, no thick cartoon outlines. LEFT piece: a low irregular near-bank slope with overlapping shrubs, reeds and a modest leafy tree canopy; height tapers toward its inner right edge. RIGHT piece: a low grassy river embankment with a modest weathered stone/concrete landing platform, a small lived-in courtyard fragment at the outer right edge (low wall, one simple wooden stool and one clay pot; no whole building). Its INNER left half has a broad clear flat walkable stone surface for a character to stand on, top surface visible from the same moderately elevated viewpoint as reference. Plants grow around the stone edges so platform belongs to the bank. Ground under both pieces extends solidly to each piece's bottom edge, not floating islands, pedestals or detached boulders. These will sit partly below the bottom of the camera frame: concentrate detail on their upper silhouettes. Short wide pieces, not tall trees or cliffs. Full silhouettes fit within canvas with transparent margin, separate pieces each roughly 40% canvas width. Real transparent background, no sky, water backdrop, city, river across gap, shadow cast outside pieces, characters, letters, UI or lens flare. Landscape 2:1 sheet.
```

外轮廓编辑（引用 clipped_v01）：

```text
Edit the supplied riverbank sprite sheet. Keep its hand-painted dusk palette, grassy left bank and right stone landing with stool and pot. Correct ONLY the framing and outer silhouettes: make both pieces self-contained with complete irregular leafy outer edges. NOTHING should touch the left or right canvas edges. Reduce the two pieces enough to leave generous transparent margins on both far sides and a transparent gap between. Complete the missing tree crown at far left and shrub/wall silhouette at far right. Keep the clear flat standing area on the inner half of the right platform. Outer foliage should taper into low grass instead of ending at a straight vertical cut. The bases remain low earthy banks, not floating islands. Use clean genuinely transparent alpha, remove any unnatural red or yellow fringe around foliage. No sky, water, buildings, text, people or new scenery. This is two isolated placeable foreground patches, NOT a full-screen frame.
```
