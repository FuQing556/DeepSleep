using System;
using DeepSleep.Runtime.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.World.Scrolling
{
    /// <summary>显式预装配的云朵槽位；纯本地表现，无物理、无全局随机数、无逐帧分配。</summary>
    public sealed class DecorativeCloudField : MonoBehaviour
    {
        [Serializable]
        public struct DepthBand
        {
            public string SortingLayer;
            public Vector2Int Orders;
            public Vector2 Opacity;
            public Vector2 Width;
            public Vector2 ViewportY;
        }
        public Camera ViewCamera;
        public Sprite[] Sprites;
        public SpriteRenderer[] Slots;
        public DepthBand[] Bands;
        public Color Tint = Color.white;
        public Vector2 RespawnSeconds = new Vector2(2, 7);
        public Vector2 LifetimeSeconds = new Vector2(18, 32);
        public Vector2 Speed = new Vector2(.3f, .9f);
        [Range(0, 1)] public float RightEntryChance = .5f;
        public float EdgePadding = .15f;
        public float FadeSeconds = 2;

        private struct Cloud
        {
            public bool Live;
            public float Wait, Age, Life, Alpha, Velocity, HalfWidth;
            public bool FromRight;
        }
        private Cloud[] _clouds;
        private System.Random _random;
        private int _limit;
        public int VisibleCount { get; private set; }

        private void Awake()
        {
            if (ViewCamera == null || Sprites == null || Sprites.Length != 5 ||
                Slots == null || Slots.Length == 0 || Slots.Length % 3 != 0 || Bands == null || Bands.Length != 3)
            { Debug.LogError("[DecorativeCloudField] 需要相机、五张云图、三个深度档及完整预装配槽位。", this); enabled = false; return; }
            foreach (var sprite in Sprites) if (sprite == null) { enabled = false; return; }
            foreach (var slot in Slots) if (slot == null) { enabled = false; return; }
            _clouds = new Cloud[Slots.Length];
            _random = new System.Random();
            for (int i = 0; i < Slots.Length; i++) { Slots[i].enabled = false; _clouds[i].Wait = Range(new Vector2(0, RespawnSeconds.y)); }
            SetDensity(CloudPresentationPreferences.Density);
            // 开场就有稀疏的场内云；剩余槽位按随机时间从右侧或场内补充。
            for (int i = 0; i < _limit; i += 2) { Spawn(i, true); _clouds[i].Age = FadeSeconds; }
        }

        private void OnEnable()
        {
            CloudPresentationPreferences.Changed += SetDensity;
            SetDensity(CloudPresentationPreferences.Density);
        }
        private void OnDisable()
        {
            CloudPresentationPreferences.Changed -= SetDensity;
            if (Slots != null) foreach (var slot in Slots) if (slot != null) slot.enabled = false;
            VisibleCount = 0;
        }
        private void SetDensity(float value)
        {
            // 每档三个槽位：一个角色后方，两个角色前方。
            _limit = value <= 0 ? 0 : Mathf.CeilToInt(value * (Slots.Length / 3)) * 3;
        }

        private void LateUpdate()
        {
            if (_clouds == null) return;
            float dt = Time.deltaTime;
            VisibleCount = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                ref Cloud cloud = ref _clouds[i];
                var renderer = Slots[i];
                if (i >= _limit)
                {
                    if (cloud.Live) cloud.Wait = Range(RespawnSeconds);
                    cloud.Live = false; renderer.enabled = false; continue;
                }
                if (!cloud.Live)
                {
                    cloud.Wait -= dt;
                    if (cloud.Wait > 0 || dt <= 0) continue;
                    Spawn(i);
                }
                cloud.Age += dt;
                float leftEdge = ViewCamera.transform.position.x - ViewCamera.orthographicSize * ViewCamera.aspect;
                if (cloud.Age >= cloud.Life || renderer.transform.position.x + cloud.HalfWidth < leftEdge - EdgePadding)
                { cloud.Live = false; renderer.enabled = false; cloud.Wait = Range(RespawnSeconds); continue; }
                renderer.transform.position += Vector3.left * (cloud.Velocity * dt);
                float fade = Mathf.Clamp01(Mathf.Min(cloud.Age, cloud.Life - cloud.Age) / FadeSeconds);
                Color color = Tint; color.a *= cloud.Alpha * fade;
                renderer.color = color;
                renderer.enabled = color.a > 0;
                if (renderer.enabled) VisibleCount++;
            }
        }

        private void Spawn(int index, bool initial = false)
        {
            ref Cloud cloud = ref _clouds[index];
            var band = Bands[index % 3];
            var renderer = Slots[index];
            renderer.sprite = Sprites[_random.Next(Sprites.Length)];
            renderer.sortingLayerName = band.SortingLayer;
            renderer.sortingOrder = _random.Next(band.Orders.x, band.Orders.y + 1);
            renderer.flipX = _random.Next(2) == 0;
            float width = Range(band.Width);
            float scale = width / renderer.sprite.bounds.size.x;
            renderer.transform.localScale = Vector3.one * scale;
            float height = ViewCamera.orthographicSize * 2;
            var center = ViewCamera.transform.position;
            float viewWidth = height * ViewCamera.aspect;
            cloud.HalfWidth = width * .5f;
            cloud.FromRight = !initial && _random.NextDouble() < RightEntryChance;
            float x = cloud.FromRight ? center.x + viewWidth * .5f + cloud.HalfWidth + EdgePadding
                : center.x + (Range(new Vector2(.05f, .95f)) - .5f) * viewWidth;
            renderer.transform.position = new Vector3(x,
                center.y + (Range(band.ViewportY) - .5f) * height, 0);
            cloud.Live = true; cloud.Age = 0; cloud.Life = Range(LifetimeSeconds);
            cloud.Alpha = Range(band.Opacity); cloud.Velocity = Range(Speed);
            // 右侧云的寿命至少覆盖完整横穿，超长屏/低速时也不会刚进画面就消失。
            if (cloud.FromRight) cloud.Life = Mathf.Max(cloud.Life,
                (viewWidth + width + EdgePadding * 2) / cloud.Velocity + FadeSeconds);
        }
        private float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)_random.NextDouble());
    }
}
