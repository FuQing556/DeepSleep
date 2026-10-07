using System;
using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Accessories
{
    public enum AccessoryLayer { Front = 0, Back = 1 }
    [Serializable]
    public struct HeadwearPose
    {
        public Sprite Pose;
        public DeepSleep.Runtime.Players.Identity.PlayerRole Role;
        [Tooltip("角色Sprite矩形内的头顶坐标，左下为0，右上为1")]
        public Vector2 Anchor;
        public float Angle;
        [Tooltip("饰品完整Sprite矩形宽度 / 角色Sprite矩形宽度")]
        public float WidthRatio;
    }

    [CreateAssetMenu(menuName="DeepSleep/Presentation/Headwear")]
    public sealed class HeadwearDefinition : ScriptableObject
    {
        public int NetworkId;
        public ShopProductDefinition Product;
        public Sprite Sprite;
        [Tooltip("前饰显示在角色前面，背饰显示在角色后面")]
        public AccessoryLayer Layer;
        public Vector2 BaseAnchor = new(.5f, .35f);
        public HeadwearPose[] Poses;
        public bool TryGetPose(Sprite sprite, out HeadwearPose pose)
        {
            foreach (var entry in Poses)
                if (entry.Pose == sprite) { pose = entry; return true; }
            pose = default; return false;
        }
    }
}
