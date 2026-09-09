# 米饭护航绿幕重制

本批交付入口为 ready/VFX_DS_RiceGuardCircle_v02.png 和 ready/SPR_DS_RiceGuardBowlBroken_v02.png。旧 v01 抠图未通过，不作交付。

采用内置 imagegen 编辑旧 raw 原图，再以 matte_green.py 调用项目 green_screen 方法本地去底。源图、绿幕中间图均保留。输出均为 1254×1254 RGBA，未裁剪、未缩放；源 hash 与 Alpha 统计见 green_matte_report.json。

实际提示词（两张仅对象描述不同）：

Edit the provided image. Preserve the existing [blue-white circular rice-themed defensive magic ring / broken navy-and-gold rice bowl, all white rice grains, ceramic shards and blue-white impact energy] design, colors and composition. Replace ALL gray checkerboard background, including every interior gap, with a perfectly uniform solid saturated chroma green #00FF00 background. No checkerboard anywhere, no texture, no gradient in the background. The foreground remains blue/white/navy/gold, with no green tint or green reflections. Clean separated silhouettes and compact blue glow. Keep the whole asset centered and fully inside a square canvas with a little margin. This is a green-screen production image, intentionally NOT transparent.

处理参数：cutoff=3、backdrop_tolerance=26、emission_edges=False。去除绿色背景并近似解混边缘；保留白色米粒、深蓝陶瓷和金色装饰。生成编辑会轻微改变细节，不能称为原图像素无损还原。蓝光变得较紧凑。

已经检查白底、暗底和天空底：未见旧棋盘残留，米粒阴影及碗身完整；保留半透明边缘。尚未用户验收，也尚未导入 Unity；本记录不表示动画实现完成。
