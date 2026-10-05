# 黄昏河流全景：首轮候选与覆盖原型

> 后续进展见 RIVERBANK_DELIVERY.md：全景现已装配，住宅楼群被用户否定后改为河岸草木/平台，菜单对称避让已接入。下文是首轮快照，不再代表当前“尚未装配”的状态。

日期：2026-09-15。状态：候选已生成，有限全景组件编译与隔离预览通过，尚未替换 World01 正式场景；不是全批任务完成。

## 来源与文件

- 用户确认方向：左侧村庄、河流、右侧远城；手机横屏比电脑显示更多两侧景色，必须覆盖镜头移动；有限全景，不强行循环地标。
- 工具：内置 image_gen，新图生成，无输入参考。没有沿用失败接缝图。
- 原图：raw/BG_W01_DuskRiver_Panorama_v01.png，2172×724，3:1；请求较高分辨率但实际工具返回此尺寸，不宣称 4K。
- 预览：previews/ 下 1280×720、1600×720、1760×720 各 left/center/right，共 9 张；由 Unity 隔离 PreviewScene 正交相机渲染，不修改原图。
- 目前天空、河流和远景城市同属有限全景层。太阳、耀斑、近处左右小片楼群不在本批中。

## 覆盖计算与验证

场景核对：World01 正交相机半高 5.4，前视最大偏移 0.45；固定玩法区 19.2×10.8，不随屏幕扩展。

22:9 可见宽 26.4，加双向前视范围 0.9 后至少 27.3。FinitePanoramaLayer2D 按固定设计宽高及额外边距等比缩放，当前 3:1 图约覆盖 33.6×11.2 世界单位。16:9 与 22:9 不分别拉伸图片或改变角色几何，而是取同一场景的不同宽度。

全景轻微跟随相机，默认 15%；不随时间无限横移。超出设计宽高比时等比扩展覆盖；镜头超预算时钳制背景中心，保证边缘不进入视野。此兜底不等于任意比例都已通过构图验收。

- PanoramaCoverageChecks.Run：50 组覆盖数学检查通过，覆盖 16:9、20:9、22:9、3:1、32:9；高度 10.8/12，偏移 -2/-0.45/0/0.45/2。
- 查看了电脑居中、22:9 左极限及右极限相机预览；均保留村落、河流与都市，没有露出诊断底色。
- 首次 PreviewScene 相机没有指定 scene，输出纯诊断底色，属于预览工具错误；已显式绑定相机 scene 并重渲染覆盖，不将首次输出当成验收通过。
- 编译 Console 查询 0 error；存在旧 Editor 脚本的 CS0618 弃用 API warning，不宣称 0 warning。
- 没有实机运行验证、前景/角色/UI 合成验收、动态移动录像或正式场景装配。组件要求独立无旋转且父级单位缩放，目前仅用于隔离验证。

## UI 调查（未修复）

World01 的退出确认位于 MenuCard → SessionOverlay → SafeArea 链路。SafeAreaRectFitter 直接采用设备安全区域，导致不对称安全区可能传递到菜单中心。触控按钮和菜单应采用不同布局策略；当前仅确认结构，尚未复现截图对应设备状态或修改 UI。

## 后续

完成左右小片楼群与角色合成后，再决定远景的景物高度/对比是否需要收敛。本候选下半部细节较丰富，不能单凭风景好看就判定战斗可读性。然后显式装配有限全景，并处理 UI 居中和全屏遮罩，不重写旧关卡的循环背景。耀斑仍暂缓；没有打包、提交或推送。

## 实际提示词

```text
Use case: stylized-concept. Brand new panoramic game background painting, very wide 3:1 canvas, ideally 3840x1280. Beautiful hand-painted anime environment, nostalgic ordinary Chinese outskirts at dusk in the early internet era, hopeful with gentle wistfulness. Elevated distant view, level horizon, natural undistorted perspective. Broad tranquil blue-lavender sky blending into pale apricot near horizon fills upper 65-70 percent, sparse fine horizontal cloud wisps. In the lower part, a SMALL winding river enters at bottom near center and recedes into distance. On the left bank a lived-in village of modest tiled roofs, low houses, trees, antennas and a few warm windows; on the right bank farther away rises a modern metropolitan skyline of varied slender skyscrapers, softened by atmospheric haze. A few modest transitional apartment blocks connect the two worlds; subtle small bridge far away. The emotional contrast is warm familiar village and distant aspirational city, not ruined poverty versus threatening cyberpunk. IMPORTANT COMPOSITION: the central 60 percent of this ultrawide panorama must already contain village, river and distant skyscrapers together. Outer left and right fifths extend the landscape naturally as camera overscan; no essential landmark exclusively at the edges. River confined to the lower landscape, not a huge central lake, no dramatic centered beam of light. Low-contrast landscape leaves sky usable for combat. No close foreground rooftop framing, no giant buildings at corners, no sun disk, moon, lens flare, characters, UI, writing or watermark. Asymmetric and coherent, no fisheye, arched clouds, tilted skyline, mirror symmetry. This is a finite panorama, NOT a repeating tile; do not sacrifice composition for matching edges.
```
