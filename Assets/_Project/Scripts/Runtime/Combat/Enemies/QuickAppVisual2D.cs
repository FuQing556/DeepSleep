using DeepSleep.Runtime.Presentation.Poses;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeepSleep.Runtime.Combat.Enemies
{
    public sealed class QuickAppVisual2D : MonoBehaviour
    {
        public QuickAppMotor2D Motor;
        public EnemyActor2D Actor;
        public SpriteRenderer Renderer;
        public SortingGroup Group;
        public SpriteMotionTrail2D MotionTrailPrefab;
        private SpriteMotionTrail2D _trail;
        private void Awake()
        {
            _trail = Instantiate(MotionTrailPrefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_trail.gameObject, gameObject.scene);
            Actor.PresentationReset += ClearTrail;
        }
        private void LateUpdate()
        {
            // 广告保持可读，只小幅倾斜；物理根与碰撞形状不旋转或缩放。
            Renderer.transform.localRotation = Quaternion.Euler(0, 0, -Motor.TravelDirection.y * Motor.Config.VisualTiltDegrees);
            _trail.Sample(Renderer, Group, Time.deltaTime > 0 && Motor.IsRunning);
        }
        private void ClearTrail() => _trail.Clear();
        private void OnDestroy()
        {
            if (Actor != null) Actor.PresentationReset -= ClearTrail;
            if (_trail != null) Destroy(_trail.gameObject);
        }
    }
}
