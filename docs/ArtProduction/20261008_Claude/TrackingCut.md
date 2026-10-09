# Claude锁定光圈素材记录

使用deepsleep-art-generation和imagegen生图；使用deepsleep-sprite-matting检查原生透明通道，不重抠、不修改源像素。

实际查看DS、HS、Kimi的既有光圈，沿用空心圆环/方向刻度；成功生成输入参考为Kimi和HS。首次三参考请求连接失败，一次重试成功。

工具：内置image_gen.imagegen，transparent_background=true。

成功提示词：

```text
Use case: stylized-concept. Create one transparent game targeting reticle for Claude. The two input images are style and readability references only: Kimi violet and Harness red. Match their clear hollow circular targeting silhouette and anime game polish, but design a distinct amber-orange and ivory-white scientific geometry ring. Four directional pointers, segmented concentric arcs, fine geometric tick marks and small diamond accents, restrained soft glow. Keep the entire central opening genuinely transparent so the player remains visible. Balanced circular front view, square canvas with comfortable transparent margins, no character, background, lettering, formulas or filled central disk. A single finished reticle sprite.
```

源图：raw/VFX_CL_TargetReticle_v01.png，1254² RGBA，SHA256 044975afe4df97cc867239c303d96c56162f751db8410fe71b15783341104b84。

已看target_audit白/暗/天空三底预览：中心透明，无假棋盘；少量柔光靠近画布边缘，未宣称有宽裕留白。正式PNG原样复制至Assets/_Project/Art/Characters/Claude/VFX_CL_TargetReticle_v01.png，PPU512、Max4096、无压缩、FullRect。

追踪斜光刃复用用户确认raw/VFX_CL_SpatialCut_v01_CANDIDATE.png，原样导入VFX_CL_TrackingCut_v01.png（2172×724）；源像素亮线轴在Y向下坐标近似y=-0.126667987x+500.824017。配置储存两端归一化Y向上坐标，等比摆正，不修改图片。

接入与验证见docs/ClaudeTrackingCut.md。
