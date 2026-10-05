using System;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>对显式绑定的 uGUI 图形应用本机主题；场景和动态卡片使用同一组件。</summary>
    public sealed class UiThemeView : MonoBehaviour
    {
        public enum SurfaceTone { Panel, Raised, Primary, Quiet, Backdrop }
        public enum GraphicTone { Text, Muted, Accent, OnAccent, Backdrop }

        [Serializable]
        public struct SurfaceBinding
        {
            public UiSurfaceGraphic Target;
            public SurfaceTone Tone;
            [Range(0, 1)] public float Opacity;
            [Tooltip("大于零覆盖主题圆角；触控圆盘可设 999。")]
            public float RadiusOverride;
        }

        [Serializable]
        public struct GraphicBinding
        {
            public Graphic Target;
            public GraphicTone Tone;
        }

        [Serializable]
        public struct PortraitBinding { public Image Target; }

        [Serializable]
        public struct ArtworkBinding
        {
            public Image Target;
            public UiArtworkKind Kind;
        }

        [Serializable]
        public struct BackgroundBinding
        {
            public Image Target;
            public AspectRatioFitter Fitter;
        }

        public UiThemePalette DeepSeek, Harness;
        // 仅供显式 Editor 迁移读取；运行时不再绘制程序几何作为美术后备。
        [HideInInspector]
        public SurfaceBinding[] Surfaces = Array.Empty<SurfaceBinding>();
        public ArtworkBinding[] Artwork = Array.Empty<ArtworkBinding>();
        public GraphicBinding[] Graphics = Array.Empty<GraphicBinding>();
        public PortraitBinding[] Portraits = Array.Empty<PortraitBinding>();
        public Image[] TitleLogos = Array.Empty<Image>();
        public BackgroundBinding[] MenuBackgrounds = Array.Empty<BackgroundBinding>();

        private void OnEnable()
        {
            UiThemePreferences.Changed += Apply;
            Apply(UiThemePreferences.Current);
        }

        private void OnDisable() => UiThemePreferences.Changed -= Apply;

        /// <summary>即时换肤，不改输入、控件显隐、按钮事件或玩法反馈的状态。</summary>
        public void Apply(PlayerRole role)
        {
            if (role != PlayerRole.DeepSeek && role != PlayerRole.Harness)
            {
                ConfigurationError("role 不是有效角色。");
                return;
            }
            UiThemePalette palette = role == PlayerRole.Harness ? Harness : DeepSeek;
            if (!ValidateBindings(palette)) return;
            foreach (ArtworkBinding binding in Artwork)
            {
                binding.Target.sprite = palette.GetArtwork(binding.Kind);
                // 透明度属于各层/动效，不因主题切换覆盖呼吸、悬停或面板淡入状态。
            }
            foreach (GraphicBinding binding in Graphics)
            {
                Color color = binding.Tone switch
                {
                    GraphicTone.Muted => palette.MutedText,
                    GraphicTone.Accent => palette.Accent,
                    GraphicTone.OnAccent => palette.OnAccent,
                    GraphicTone.Backdrop => palette.Background,
                    _ => palette.Text
                };
                if (binding.Tone == GraphicTone.Backdrop) color.a = binding.Target.color.a;
                binding.Target.color = color;
            }
            foreach (PortraitBinding binding in Portraits)
            {
                binding.Target.sprite = palette.Portrait;
                binding.Target.preserveAspect = true;
            }
            foreach (Image logo in TitleLogos)
            {
                logo.sprite = palette.TitleLogo;
                logo.preserveAspect = true;
            }
            foreach (BackgroundBinding binding in MenuBackgrounds)
            {
                binding.Target.sprite = palette.MenuBackground;
                binding.Target.preserveAspect = true;
                // 全屏父级负责覆盖范围，Fitter 只等比裁切，不将插画拉伸到屏幕比例。
                binding.Fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                binding.Fitter.aspectRatio = palette.MenuBackground.rect.width / palette.MenuBackground.rect.height;
            }
        }

        private bool ValidateBindings(UiThemePalette palette)
        {
            if (DeepSeek == null || Harness == null) return ConfigurationError("DeepSeek / Harness 配色资产未完整绑定。");
            if (Surfaces == null || Artwork == null || Graphics == null || Portraits == null ||
                TitleLogos == null || MenuBackgrounds == null)
                return ConfigurationError("绑定数组不能为空引用；不用的分类请保留空数组。");
            if (Surfaces.Length != 0)
                return ConfigurationError("旧程序几何美术尚未迁移；请显式执行 UiLayeredArtworkInstaller，禁止以旧几何回退显示。");
            for (int i = 0; i < Artwork.Length; i++)
            {
                if (Artwork[i].Target == null) return ConfigurationError($"Artwork[{i}].Target 缺失。");
                if (palette.GetArtwork(Artwork[i].Kind) == null)
                    return ConfigurationError($"配色资产 {palette.name} 缺少 {Artwork[i].Kind} 生成素材。");
            }
            for (int i = 0; i < Graphics.Length; i++)
                if (Graphics[i].Target == null) return ConfigurationError($"Graphics[{i}].Target 缺失。");
            for (int i = 0; i < Portraits.Length; i++)
                if (Portraits[i].Target == null) return ConfigurationError($"Portraits[{i}].Target 缺失。");
            if (Portraits.Length > 0 && palette.Portrait == null)
                return ConfigurationError($"配色资产 {palette.name} 缺少 Portrait。");
            for (int i = 0; i < TitleLogos.Length; i++)
                if (TitleLogos[i] == null) return ConfigurationError($"TitleLogos[{i}] 缺失。");
            if (TitleLogos.Length > 0 && palette.TitleLogo == null)
                return ConfigurationError($"配色资产 {palette.name} 缺少 TitleLogo 生成素材。");
            for (int i = 0; i < MenuBackgrounds.Length; i++)
                if (MenuBackgrounds[i].Target == null || MenuBackgrounds[i].Fitter == null ||
                    MenuBackgrounds[i].Target.gameObject != MenuBackgrounds[i].Fitter.gameObject)
                    return ConfigurationError($"MenuBackgrounds[{i}] 需要同物体的 Image / AspectRatioFitter。");
            if (MenuBackgrounds.Length > 0 && palette.MenuBackground == null)
                return ConfigurationError($"配色资产 {palette.name} 缺少 MenuBackground 生成素材。");
            return true;
        }

        private bool ConfigurationError(string message)
        {
            string path = name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            Debug.LogError($"[UiThemeView] {path}: {message}", this);
            enabled = false;
            return false;
        }
    }
}
