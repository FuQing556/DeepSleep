using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    public enum UiArtworkKind { ButtonBase, ButtonFrame, ButtonOrnament, ButtonGlow, PanelFrame, CircleBase, CircleFrame }

    /// <summary>角色主题的显式美术配置；不持有玩法状态或改变控件行为。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/UI/Theme Palette")]
    public sealed class UiThemePalette : ScriptableObject
    {
        public Color Background, Panel, PanelBottom, Raised, RaisedBottom, Border;
        public Color Text, MutedText, Accent, AccentSoft, OnAccent;
        public Sprite Portrait;
        [Min(0)] public float CornerRadius = 16f;
        [Header("生成美术：独立透明层")]
        public Sprite ButtonBase, ButtonFrame, ButtonOrnament, ButtonGlow, PanelFrame, CircleBase, CircleFrame;
        [Header("主菜单生成美术")]
        public Sprite TitleLogo, MenuBackground;

        /// <summary>只读取显式素材引用，不按路径加载，也不以程序图形代替缺失素材。</summary>
        public Sprite GetArtwork(UiArtworkKind kind) => kind switch
        {
            UiArtworkKind.ButtonBase => ButtonBase,
            UiArtworkKind.ButtonFrame => ButtonFrame,
            UiArtworkKind.ButtonOrnament => ButtonOrnament,
            UiArtworkKind.ButtonGlow => ButtonGlow,
            UiArtworkKind.PanelFrame => PanelFrame,
            UiArtworkKind.CircleBase => CircleBase,
            UiArtworkKind.CircleFrame => CircleFrame,
            _ => null
        };
    }
}
