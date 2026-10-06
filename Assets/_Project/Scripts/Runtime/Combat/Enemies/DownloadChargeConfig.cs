using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/敌人/下载冲撞")]
    public sealed class DownloadChargeConfig : ScriptableObject
    {
        public float ChargeSeconds = 1.1f;
        public float DashSeconds = .65f;
        public float DashSpeed = 30f;
        public float RecoverySeconds = .7f;
        public float DespawnMargin = 3f;
        public float ContactSweepRadius = .35f;
        public LayerMask PlayerLayers;
        public bool IsValid => ChargeSeconds > 0 && DashSeconds > 0 && DashSpeed > 0 &&
            RecoverySeconds > 0 && DespawnMargin > 0 && ContactSweepRadius > 0 && PlayerLayers.value != 0;
    }
}
