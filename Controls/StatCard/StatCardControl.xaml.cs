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
