# 迅雷冲刺拖影

只在 Dashing 状态采样；进入、蓄力、恢复阶段不产生拖影。原有姿态切换单残影不变。

调参：`Assets/_Project/Prefabs/Combat/Enemies/Internet/PF_Download_MotionTrail.prefab` 的 `SpriteMotionTrail2D`。

- Interval Seconds：0.045，采样间隔。
- Fade Seconds：0.18，线性淡出时间。
- Alpha：0.32，再乘身体实际透明度。
- Renderers：4 枚预装配精灵，循环复用；无碰撞体。

复制冲刺贴图、材质、世界位置/旋转/缩放、翻转与排序。拖影独立于怪物移动和回池；冲撞、死亡或退出后自然淡出。RunReset 清空所有租借及已回池对象的拖影；场景卸载销毁。暂停不推进淡出或采样，低帧率不补发重叠拖影。没有新贴图，不改伤害、速度或碰撞。

单机/主机从 DownloadChargeVisual2D 采样。客机从 NetworkEntityView 的第一渲染层匹配迅雷 Dash 图，在插值后采样；其他敌人与姿态残影不采样。共用同一拖影 Prefab，不增加网络消息或快照层。镜像回收保留短暂尾影，整个网络世界清理则立即清空。协议仍为 11，内容版本为 `20261007-download-trail-1`。

编辑器 `DownloadMotionTrailChecks.Run()`：19 项断言，覆盖装配、贴图/透明度/翻转/变换、世界位置不跟随、采样间隔、暂停、回池自然淡出、固定容量、无碰撞、清理。尚未手机/真实双端视觉验收，不打包。
