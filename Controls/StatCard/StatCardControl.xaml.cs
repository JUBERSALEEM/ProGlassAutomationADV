using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Controls.StatCard
{
    /// <summary>
    /// Reusable statistic card (§11). Presentation only — modules own
    /// the data. States: Normal, Loading, Empty, Updating, Success,
    /// Warning, Error, Transparent. Hideable via IsHidden.
    /// </summary>
    public partial class StatCardControl : UserControl, INotifyPropertyChanged
    {
        public StatCardControl()
        {
            InitializeComponent();
            Loaded += OnStatCardLoaded;
        }

        // ── Crystal Fluid (2026-09-05) — KPI cards are liquid crystal
        // reservoirs (spec §30): the water layer is CAPABLE in every
        // StatCard and turns on wherever the active family enables it.
        private void OnStatCardLoaded(object sender, RoutedEventArgs e)
        {
            // Local value wins (a page may opt a card in or out
            // explicitly); unset cards follow the family gate.
            if (ReadLocalValue(WaterEnabledProperty) ==
                DependencyProperty.UnsetValue)
            {
                WaterEnabled =
                    TryFindResource("CrystalWaterDefaultEnabled") is bool on
                    && on;
            }
        }

        public static readonly DependencyProperty WaterEnabledProperty =
            DependencyProperty.Register(
                nameof(WaterEnabled), typeof(bool), typeof(StatCardControl),
                new PropertyMetadata(false));

        /// <summary>False = card looks exactly as before (theme-neutral);
        /// True = Crystal Water renders behind the content (§30).</summary>
        public bool WaterEnabled
        {
            get => (bool)GetValue(WaterEnabledProperty);
            set => SetValue(WaterEnabledProperty, value);
        }

        public static readonly DependencyProperty WaterLevelProperty =
            DependencyProperty.Register(
                nameof(WaterLevel), typeof(double), typeof(StatCardControl),
                // Ios-17pm P1: default mirrors the authored
                // CrystalDefaultWaterLevel token (0.30).
                new PropertyMetadata(0.30, null,
                    (d, v) => Math.Max(0.0, Math.Min(1.0, (double)v))));

        /// <summary>Normalized water fill 0..1 (§15). Defaults to the
        /// authored ambience (CrystalDefaultWaterLevel = 0.30); modules
        /// may bind ALREADY-AVAILABLE semantic ratios (§46 — no business
        /// math may be invented for this).</summary>
        public double WaterLevel
        {
            get => (double)GetValue(WaterLevelProperty);
            set => SetValue(WaterLevelProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon), typeof(string), typeof(StatCardControl),
                new PropertyMetadata("📊"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(StatCardControl),
                new PropertyMetadata(""));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value), typeof(string), typeof(StatCardControl),
                new PropertyMetadata("", OnDisplayChanged));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit), typeof(string), typeof(StatCardControl),
                new PropertyMetadata(""));

        /// <summary>Optional secondary line (trend, hint…).</summary>
        public string TrendText
        {
            get => (string)GetValue(TrendTextProperty);
            set => SetValue(TrendTextProperty, value);
        }

        public static readonly DependencyProperty TrendTextProperty =
            DependencyProperty.Register(
                nameof(TrendText), typeof(string), typeof(StatCardControl),
                new PropertyMetadata(""));

        /// <summary>
        /// Normal | Loading | Empty | Updating | Success | Warning |
        /// Error | Transparent.
        /// </summary>
        public string State
        {
            get => (string)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(
                nameof(State), typeof(string), typeof(StatCardControl),
                new PropertyMetadata("Normal", OnDisplayChanged));

        /// <summary>Collapsed cards never obstruct the working area.</summary>
        public bool IsHidden
        {
            get => (bool)GetValue(IsHiddenProperty);
            set => SetValue(IsHiddenProperty, value);
        }

        public static readonly DependencyProperty IsHiddenProperty =
            DependencyProperty.Register(
                nameof(IsHidden), typeof(bool), typeof(StatCardControl),
                new PropertyMetadata(false));

        /// <summary>Value shown after Empty-state resolution.</summary>
        public string DisplayValue =>
            State == "Empty" ? "—" : Value ?? "";

        private static void OnDisplayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatCardControl card)
            {
                card.RaisePropertyChanged(nameof(DisplayValue));
            }
        }

        private void RaisePropertyChanged(string name) =>
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(name));

        /// <summary>Raised when DisplayValue derives a new value.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
