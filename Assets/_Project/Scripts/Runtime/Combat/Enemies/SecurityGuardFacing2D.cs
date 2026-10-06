using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>追逐可斜向，盾只保持正左/正右；跨过死区才换边，避免头顶处抖动。</summary>
    public sealed class SecurityGuardFacing2D : MonoBehaviour, IFixedSimulationStep
    {
        public EnemyMotor2D Motor;
        public Transform VisualRoot, ShieldRoot;
        public SpritePoseTransition2D Transition;
        public SpriteRenderer Renderer;
        public float ShieldOffset = .65f;
        public float TurnDelay = .16f;
        public float DirectionDeadZone = .18f;
        private int _facing = 1;
        private float _turn;
        private void OnEnable() { _turn = 0; _facing = 1; Apply(); }
        public void Simulate(float deltaTime)
        {
            float x = Motor.TravelDirection.x;
            if (Mathf.Abs(x) < DirectionDeadZone || Mathf.Sign(x) == _facing) { _turn = 0; return; }
            _turn += deltaTime;
            if (_turn < TurnDelay) return;
            Transition.CaptureCurrentPose(); _facing = x < 0 ? -1 : 1; _turn = 0; Apply();
        }
        private void Apply()
        {
            if (Renderer != null) Renderer.flipX = _facing < 0;
            if (ShieldRoot != null)
            {
                var p = ShieldRoot.localPosition; p.x = _facing * ShieldOffset; ShieldRoot.localPosition = p;
                var s = ShieldRoot.localScale; s.x = Mathf.Abs(s.x) * _facing; ShieldRoot.localScale = s;
            }
        }
    }
}
