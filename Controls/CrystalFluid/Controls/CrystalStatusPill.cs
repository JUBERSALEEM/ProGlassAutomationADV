// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Controls/CrystalStatusPill.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED STATUS PILL (P2 2026-09-05) — integrated port of
// the CrystalFluid reference implementation (incoming file 07).
//
// Defect fixes applied:
//   D11 StatusColor default was Brushes.LimeGreen (off-palette neon).
//       The DP default is now null and the pill STYLE supplies the
//       authored CrystalPositiveBrush via DynamicResource — on-palette,
//       theme-live, per-instance overridable. The template also tints
//       the dot glow from StatusColor instead of hardcoding positive.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// A translucent status pill with optional colored indicator dot.
    /// </summary>
    public class CrystalStatusPill : ContentControl
    {
        public static readonly DependencyProperty StatusColorProperty =
            DependencyProperty.Register(
                nameof(StatusColor),
                typeof(Brush),
                typeof(CrystalStatusPill),
                new PropertyMetadata(null));   // D11: style supplies the token

        public static readonly DependencyProperty ShowDotProperty =
            DependencyProperty.Register(
                nameof(ShowDot),
                typeof(bool),
                typeof(CrystalStatusPill),
                new PropertyMetadata(true));

        public Brush StatusColor
        {
            get => (Brush)GetValue(StatusColorProperty);
            set => SetValue(StatusColorProperty, value);
        }

        public bool ShowDot
        {
            get => (bool)GetValue(ShowDotProperty);
            set => SetValue(ShowDotProperty, value);
        }

        static CrystalStatusPill()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CrystalStatusPill),
                new FrameworkPropertyMetadata(typeof(CrystalStatusPill)));
        }
    }
}
