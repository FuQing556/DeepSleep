# Harness 战斗素材生产状态（2026-09-06）

视觉母版：`Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png`。

## 已通过并进入正式资产目录

- `Assets/_Project/Art/Characters/Harness/SPR_HA_LaserFire_v01.png`：来自 `CANDIDATE_HA_LaserFireFly_v02_ALPHA.png`；1280×1280 透明生产源，前倾飞行、双手压住终端，姿势与待机图有明确差异。
- `Assets/_Project/Art/VFX/Harness/Laser/VFX_HA_TargetReticle_v01.png`：512×512 透明黑红锁定环。
- `Assets/_Project/Art/VFX/Harness/Laser/TEX_HA_LaserBeamBody_v01.png`：1024×256 透明、无端帽横向束身；双拼检查未见明显断缝，最终以动态四边形网格重复采样，不能直接按整张 Sprite 拉伸。
- `Assets/_Project/Art/VFX/Harness/Laser/VFX_HA_LaserMuzzle_v01.png`：512×512 终端发射端闪光。
- `Assets/_Project/Art/VFX/Harness/Laser/VFX_HA_LaserHit_v01.png`：512×512 沿线穿透命中闪光。
- `Assets/_Project/Art/VFX/Harness/Laser/TEX_HA_LaserOverclock_v01.png`：1024×256 上下边缘过载碎片叠加层；中央透明，不参与伤害几何。
- `Assets/_Project/Art/VFX/Harness/Laser/TEX_HA_LaserSurgeFrame_v01.png`：1024×512 双轨宽幅能量外框；由原先误判废的第一版过载图重新定职，叠在基础束身外侧，承担高等级/蓄满爆发的体量感。

`Characters/Harness/CANDIDATE_HA_LaserAim_v04_ALPHA.png` 仍是手动锁定期间的独立姿势候选，尚未进入正式目录；本批通过的是开火姿势和激光特效组。

## 重新定职

- `VFX/Harness/Laser/CANDIDATE_TEX_HA_LaserOverclock_v01.png`：不适合作为稀疏过载纹理，但画面本身通过，已重新定职为 `TEX_HA_LaserSurgeFrame_v01.png` 的生产源。候选原名保留以维持生成记录。

## 明确废稿

- `Characters/Harness/CANDIDATE_HA_LaserFireFly_v01_RAW.png`
- `Characters/Harness/CANDIDATE_HA_LaserFireFly_v01_ALPHA.png`
  - 废稿原因：与母版待机姿势差异过小，无法承担开火状态辨识。
- `Characters/Harness/CANDIDATE_HA_FieldAimProjection_v01_RAW.png`
- `Characters/Harness/CANDIDATE_HA_FieldAimProjection_v02_ALPHA.png`
  - 废稿原因：人物呈站立姿态；错误地把人物按独立透明 Sprite 生产；缺失用户要求的圆形内盘背景。
- `VFX/Harness/CANDIDATE_VFX_HA_WhaleTail_v01_RAW.png`
- `VFX/Harness/CANDIDATE_VFX_HA_WhaleTail_v02_ALPHA.png`
- `VFX/Harness/CANDIDATE_VFX_HA_WhaleFall_v01_RAW.png`
- `VFX/Harness/CANDIDATE_VFX_HA_WhaleFall_v02_ALPHA.png`
  - 废稿原因：脱离已导入 Harness 人物母版另造鲸类结构；其中完整鲸鱼明显成为须鲸；无法自然融入既定天空横版、Q版平视镜头。

废稿保留用于追溯，不删除、不移动到 `Assets/_Project/`、不参与 Unity 导入。

## 本批生产后的剩余依赖

1. 在 Unity 中核对生产资产的导入类型、Alpha、Wrap Mode 和压缩设置，再接入动态网格与伤害几何。
2. `CHR_HA_FieldAimDisc`：正面飞行取景姿势与暗色终端背景合成的圆形内盘；只清除圆盘外区域。
3. `VFX_HA_RingInner`、`VFX_HA_RingOuter`：内盘下方反向旋转的独立透明外圈。
4. `VFX_HA_ExecutePulse`：承担清弹结算的透明电磁脉冲。
5. `VFX_HA_OrcaTailProjection`：仅在高价值清算中出现，严格复用母版尾部轮廓的全息放大扫击；不画完整动物。
