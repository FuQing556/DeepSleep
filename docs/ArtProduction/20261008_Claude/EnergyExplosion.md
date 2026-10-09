# 巨型能量弹：放射核心与逸散层

2026-10-08，deepsleep-art-generation + imagegen，内置生成、真实透明背景请求。生产卡：现有核心作为配色/几何材质参考→自然向外炸开的放射核心→正视独立透明→爆炸中心层；淡色能量流纹→向外逸散滞留→正视独立透明→更大的范围层。不包含背景或完整演出。

## 最新用户确认

- 飞行期间可被攻击打碎，打碎就在当前位置引爆；碰玩家/边界仍引爆。
- 飞行视觉约四个角色大小，具体身体标尺/直径倍率尚须接入时校准，不解释为四倍面积。
- 爆炸范围面积约安全区面积的五分之一，不是宽度的五分之一。圆形范围参考公式为 r=sqrt(0.2*W*H/pi)，使用当前逻辑安全区，不使用超长屏背景面积；触边爆炸可被边界裁切，不默默扩大半径以补足可见面积。
- 飞行层可多层嵌套，较大淡色下层与核心反向旋转。爆炸时球壳/光圈渐隐，同时出现放射核心与大范围逸散、滞留；原地留存约2～3秒后渐隐。具体渐隐时长及游戏/现实时间口径未定。
- 能量球HP、可受击来源、爆炸伤害/频率、滞留是否持续伤害未定，不能从视觉擅自推出玩法数值。

## 候选检查

放射核心v01具有亮中心、不规则长短射线，没有沿用球壳圆环；右侧柔光近边，正式使用前仍需全图边缘检查。逸散v01贴边退役；v02留有明显透明边距、颜色更淡、流纹更稀疏，不和中心爆闪烘在一起。三张均保留原始输出。未做Unity叠层/游戏量级验收、全图Alpha色边检查，未导入或接入Runtime，透明角点不等于完整验收。

## VFX_CL_EnergyRadialBurst_v01_CANDIDATE.png

参考：raw/VFX_CL_EnergyCore_v01_CANDIDATE.png，仅配色/材质参考，不复制原球形。

实际提示词：

```text
Use case: stylized-concept. Single transparent 2D game VFX sprite: a natural RADIAL ENERGY EXPLOSION CORE. Reference is amber-white color and luminous geometric material reference only, do not reproduce its sphere or rings. Dense small white-hot central burst releasing irregular tapered rays, ribbons of amber light and a few angular sparks outward in all directions. Unequal ray lengths, organic explosive branching, not a uniform star icon or symmetrical magic circle. Bright compact center with clearly separated outward streaks and transparent gaps. No intact spherical shell, no concentric rings. Full radial burst contained within the central 70 percent of square canvas; generous transparent border including faint glow. Genuine transparent background, no scene, ground, smoke cloud, text or UI. This is the explosive center layer, separate from a much larger lingering energy haze.
```

## VFX_CL_EnergyDissipation_v01_REJECTED.png

参考：raw/VFX_CL_EnergyCore_v01_CANDIDATE.png，仅配色/材质参考，不复制原球形。

实际提示词：

```text
Use case: stylized-concept. Single transparent 2D game VFX layer: lingering ENERGY DISSIPATION after a huge amber-white energy orb explodes. Reference supplies color and scientific luminous material only; do not copy the intact sphere, concentric rings or bright central core. A roughly circular wide field of pale peach-ivory translucent wisps, softly curling radial currents, sparse fine drifting angular particles and faint streaks spreading away from the center. Center is quiet and translucent, leaving room for a separate bright radial explosion core. Diffuse, airy, uneven natural edges, not flames, smoke, a solid disk or magic circle. The cloud is lower contrast and less dense than the reference and can linger for 2-3 seconds before fading. Whole field comfortably contained with broad empty transparent margins on all sides. Genuine transparent background, no ground, scene, text, UI, central flash or spherical shell.
```

## VFX_CL_EnergyDissipation_v02_CANDIDATE.png

参考：无输入图，修正边距后重新生成；颜色和用途由提示词延续。

实际提示词：

```text
A small, isolated soft peach-white energy mist VFX on a LARGE transparent square canvas. The effect occupies only the middle HALF of the canvas, with wide completely empty transparent space all around it. Loose curling wisps spreading radially, sparse glowing geometric dust, irregular diffuse edge. Pale translucent low-contrast energy, not fire, no solid circle, no rings, no bright central flash. An anime game explosion's lingering dissipation layer. Genuine transparent background. All wisps fade away well before any canvas edge.
```
