// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/LiquidGlassCard.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass design system — layered crystal card surface
//
// WPF Borders cannot host a ControlTemplate, so a true layered glass
// surface (crystal edge + inner top reflection + nearly transparent
// center + soft shadow) needs a dedicated ContentControl. The default
// style lives in Themes/Styles/LiquidGlassStyles.xaml and resolves
// every colour through DynamicResource, so Light/Dark swaps and the
// iOS Liquid Glass family re-tints all cards live.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Controls
{
    /// <summary>Surface density of a liquid glass card.</summary>
    public enum LiquidSurface
    {
        /// <summary>Nearly transparent center — clear water glass.</summary>
        Subtle = 0,

        /// <summary>Balanced translucency (default).</summary>
        Standard = 1,

        /// <summary>Denser frost — dialogs and headers.</summary>
        Strong = 2
    }

    /// <summary>
    /// A liquid-glass surface: layered crystal edge, internal top
    /// reflection, translucent center and a soft shadow — one shared
    /// template instead of duplicated Border/Gradient/Shadow stacks.
    /// </summary>
    public class LiquidGlassCard : ContentControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(LiquidGlassCard),
                new FrameworkPropertyMetadata(
                    new CornerRadius(16),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LiquidIntensityProperty =
            DependencyProperty.Register(
                nameof(LiquidIntensity),
                typeof(LiquidSurface),
                typeof(LiquidGlassCard),
                new FrameworkPropertyMetadata(
                    LiquidSurface.Standard,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Roundedness of the crystal edge (default 16).</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>Center transparency: Subtle / Standard / Strong.</summary>
        public LiquidSurface LiquidIntensity
        {
            get => (LiquidSurface)GetValue(LiquidIntensityProperty);
            set => SetValue(LiquidIntensityProperty, value);
        }

        static LiquidGlassCard()
        {
            // One template instance shared by every card (frozen brushes
            // inside), no per-instance cost beyond the visual tree.
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(LiquidGlassCard),
                new FrameworkPropertyMetadata(typeof(LiquidGlassCard)));
        }
    }
}
