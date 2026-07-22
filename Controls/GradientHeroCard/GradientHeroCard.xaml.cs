using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Controls.GradientHeroCard
{
    /// <summary>
    /// Prominent hero card with gradient background, icon badge, big
    /// value text, label, and optional trend badge.
    ///
    /// Base class is inferred from the XAML root element — do NOT
    /// specify one here (causes CS0263 partial class mismatch).
    ///
    /// Usage:
    ///     <local:GradientHeroCard
    ///         Icon="💰"
    ///         Value="AED 500.00"
    ///         Label="Total Earning"
    ///         GradientStartColor="#6D28D9"
    ///         GradientEndColor="#8B5CF6"
    ///         TrendArrow="↗"
    ///         TrendText="12%"
    ///         TrendVisibility="Visible"/>
    /// </summary>
    public partial class GradientHeroCard
    {
        public GradientHeroCard()
        {
            InitializeComponent();
            Loaded += (_, __) => UpdateGradient();
        }

        // ─── Icon ─────────────────────────────────────────────────────────

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon),
                typeof(string),
                typeof(GradientHeroCard),
                new PropertyMetadata("✨"));

        // ─── Value (big text) ─────────────────────────────────────────────

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(GradientHeroCard),
                new PropertyMetadata("—"));

        // ─── Label (small text below value) ───────────────────────────────

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(GradientHeroCard),
                new PropertyMetadata("Total"));

        // ─── Trend badge ──────────────────────────────────────────────────

        public string TrendArrow
        {
            get => (string)GetValue(TrendArrowProperty);
            set => SetValue(TrendArrowProperty, value);
        }

        public static readonly DependencyProperty TrendArrowProperty =
            DependencyProperty.Register(
                nameof(TrendArrow),
                typeof(string),
                typeof(GradientHeroCard),
                new PropertyMetadata("↗"));

        public string TrendText
        {
            get => (string)GetValue(TrendTextProperty);
            set => SetValue(TrendTextProperty, value);
        }

        public static readonly DependencyProperty TrendTextProperty =
            DependencyProperty.Register(
                nameof(TrendText),
                typeof(string),
                typeof(GradientHeroCard),
                new PropertyMetadata(""));

        public Visibility TrendVisibility
        {
            get => (Visibility)GetValue(TrendVisibilityProperty);
            set => SetValue(TrendVisibilityProperty, value);
        }

        public static readonly DependencyProperty TrendVisibilityProperty =
            DependencyProperty.Register(
                nameof(TrendVisibility),
                typeof(Visibility),
                typeof(GradientHeroCard),
                new PropertyMetadata(Visibility.Collapsed));

        // ─── Gradient colors ──────────────────────────────────────────────

        public Color GradientStartColor
        {
            get => (Color)GetValue(GradientStartColorProperty);
            set => SetValue(GradientStartColorProperty, value);
        }

        public static readonly DependencyProperty GradientStartColorProperty =
            DependencyProperty.Register(
                nameof(GradientStartColor),
                typeof(Color),
                typeof(GradientHeroCard),
                new PropertyMetadata(
                    Color.FromRgb(0x0F, 0x4C, 0x3A),
                    OnGradientChanged));

        public Color GradientEndColor
        {
            get => (Color)GetValue(GradientEndColorProperty);
            set => SetValue(GradientEndColorProperty, value);
        }

        public static readonly DependencyProperty GradientEndColorProperty =
            DependencyProperty.Register(
                nameof(GradientEndColor),
                typeof(Color),
                typeof(GradientHeroCard),
                new PropertyMetadata(
                    Color.FromRgb(0x22, 0xC5, 0x5E),
                    OnGradientChanged));

        // ─── Gradient update ──────────────────────────────────────────────

        private static void OnGradientChanged(DependencyObject d,
                                              DependencyPropertyChangedEventArgs e)
        {
            if (d is GradientHeroCard card)
                card.UpdateGradient();
        }

        /// <summary>
        /// Updates the LinearGradientBrush stops via runtime lookup.
        /// x:Name on GradientStop elements does not reliably generate
        /// backing fields in .NET WPF, so we walk the visual/logical
        /// tree via FindName() on the root Border.
        /// </summary>
        private void UpdateGradient()
        {
            var rootBorder = FindName("RootBorder") as Border;
            if (rootBorder?.Background is LinearGradientBrush lgb
                && lgb.GradientStops.Count >= 2)
            {
                // If frozen, clone before mutating
                if (lgb.IsFrozen)
                {
                    lgb = lgb.Clone();
                    rootBorder.Background = lgb;
                }

                lgb.GradientStops[0].Color = GradientStartColor;
                lgb.GradientStops[1].Color = GradientEndColor;
            }
        }
    }
}