using UnityEngine;

namespace DeepSleep.Runtime.Players.Presentation
{
    [CreateAssetMenu(menuName = "DeepSleep/Presentation/Player Action Poses")]
    public sealed class PlayerActionPoseSet : ScriptableObject
    {
        public Sprite Movement, Hurt, Rescue, Celebrate;
        public Sprite Backward;
    }
}
