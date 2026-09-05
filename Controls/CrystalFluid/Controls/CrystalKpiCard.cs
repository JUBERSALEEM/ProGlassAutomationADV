// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Controls/CrystalKpiCard.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED KPI CARD (P2 2026-09-05) — integrated port of
// the CrystalFluid reference implementation (incoming file 06).
//
// Defect fixes applied:
//   D13 Spurious `new` modifier on Value — CrystalCard declares nothing
//       of the name (CS0109 warning).
//   D12 TrendText/TrendPositive are no longer dead API: the KPI template
//       (CrystalFluidControls.xaml) consumes them (tinted trend chip —
//       positive/danger brush via trigger).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// A KPI-specific crystal card with liquid fill.
    /// Displays Title, Value, Subtitle, Icon, and trend information.
    /// Water + bobbles + tilt default ON (KPI surfaces are the authored
    /// carriers of the liquid material).
    /// </summary>
    public class CrystalKpiCard : CrystalCard
    {
        #region ── Dependency Properties ──

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(CrystalKpiCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(CrystalKpiCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(
                nameof(Subtitle),
                typeof(string),
                typeof(CrystalKpiCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon),
                typeof(object),
                typeof(CrystalKpiCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TrendTextProperty =
            DependencyProperty.Register(
                nameof(TrendText),
                typeof(string),
                typeof(CrystalKpiCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TrendPositiveProperty =
            DependencyProperty.Register(
                nameof(TrendPositive),
                typeof(bool),
                typeof(CrystalKpiCard),
                new PropertyMetadata(true));

        #endregion

        #region ── CLR Properties ──

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string TrendText
        {
            get => (string)GetValue(TrendTextProperty);
            set => SetValue(TrendTextProperty, value);
        }

        public bool TrendPositive
        {
            get => (bool)GetValue(TrendPositiveProperty);
            set => SetValue(TrendPositiveProperty, value);
        }

        #endregion

        static CrystalKpiCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CrystalKpiCard),
                new FrameworkPropertyMetadata(typeof(CrystalKpiCard)));
        }

        public CrystalKpiCard()
        {
            // KPI surfaces carry the water material by default (§31);
            // every channel remains per-instance overridable.
            WaterEnabled = true;
            TiltEnabled = true;
            BubbleEnabled = true;
            BubbleCount = 3;
        }
    }
}
