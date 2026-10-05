// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Controls/CrystalProgressBar.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED PROGRESS BAR (P2 2026-09-05) — integrated port of
// the CrystalFluid reference implementation (incoming file 08).
//
// Defect fixes applied:
//   D13 Spurious `new` modifier on CornerRadiusProperty/CornerRadius —
//       ProgressBar declares nothing of the name (CS0109 warning).
//   +   IsIndeterminate now has a template treatment (CrystalFluidControls.xaml):
//       determinate fill swaps for a breathing band — the app does use
//       indeterminate bars (e.g. wallpaper fetch), so silently freezing
//       the fill would have been an adoption regression.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// A liquid-styled progress bar (pill track, liquid fill,
    /// breathing indeterminate state).
    /// </summary>
    public class CrystalProgressBar : ProgressBar
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(CrystalProgressBar),
                new PropertyMetadata(new CornerRadius(99)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        static CrystalProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CrystalProgressBar),
                new FrameworkPropertyMetadata(typeof(CrystalProgressBar)));
        }
    }
}
