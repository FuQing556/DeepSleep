using DeepSleep.Runtime.Presentation.Poses;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>只旋转视觉根；物理根不跟随瞄准倾斜。姿态切换复用单残影。</summary>
    public sealed class DownloadChargeVisual2D : MonoBehaviour
    {
        public DownloadChargeMotor2D Motor;
        public Transform VisualRoot;
        public SpriteRenderer Renderer;
        public SpritePoseTransition2D Transition;
        public Sprite Idle, Charge, Dash;
        private uint _generation;
        public EnemyActor2D Actor;
        private void LateUpdate()
        {
            if (Actor.SpawnGeneration != _generation)
            { _generation = Actor.SpawnGeneration; Transition.ResetTo(Idle); }
            var sprite = Motor.State == DownloadChargeState.Charging ? Charge :
                Motor.State == DownloadChargeState.Dashing ? Dash : Idle;
            Transition.TransitionTo(sprite);
            Vector2 direction = Motor.TravelDirection;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Renderer.flipY = direction.x < 0;
            VisualRoot.localRotation = Quaternion.Euler(0, 0, angle);
            Renderer.color = Motor.State == DownloadChargeState.Charging
                ? Color.Lerp(new Color(.65f, .65f, .65f, 1), Color.white, Motor.ChargeProgress) : Color.white;
        }
    }
}
