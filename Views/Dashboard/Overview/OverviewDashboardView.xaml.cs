using System;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Dashboard.Overview
{
    public partial class OverviewDashboardView : UserControl
    {
        // View-only breakpoints, in device-independent pixels. No DataContext,
        // collections, commands or metrics are replaced when the window resizes.
        public static readonly DependencyProperty KpiColumnsProperty =
            DependencyProperty.Register(nameof(KpiColumns), typeof(int), typeof(OverviewDashboardView), new PropertyMetadata(4));
        public static readonly DependencyProperty StatColumnsProperty =
            DependencyProperty.Register(nameof(StatColumns), typeof(int), typeof(OverviewDashboardView), new PropertyMetadata(3));
        public static readonly DependencyProperty DeliveryColumnsProperty =
            DependencyProperty.Register(nameof(DeliveryColumns), typeof(int), typeof(OverviewDashboardView), new PropertyMetadata(4));
        public static readonly DependencyProperty PipelineColumnsProperty =
            DependencyProperty.Register(nameof(PipelineColumns), typeof(int), typeof(OverviewDashboardView), new PropertyMetadata(5));

        public int KpiColumns { get => (int)GetValue(KpiColumnsProperty); private set => SetValue(KpiColumnsProperty, value); }
        public int StatColumns { get => (int)GetValue(StatColumnsProperty); private set => SetValue(StatColumnsProperty, value); }
        public int DeliveryColumns { get => (int)GetValue(DeliveryColumnsProperty); private set => SetValue(DeliveryColumnsProperty, value); }
        public int PipelineColumns { get => (int)GetValue(PipelineColumnsProperty); private set => SetValue(PipelineColumnsProperty, value); }

        private double _lastLayoutWidth = -1;

        public OverviewDashboardView()
        {
            InitializeComponent();
        }

        private void OnOverviewLoaded(object sender, RoutedEventArgs e)
            => ApplyLayout(ActualWidth);

        private void OnOverviewSizeChanged(object sender, SizeChangedEventArgs e)
            => ApplyLayout(e.NewSize.Width);

        private void ApplyLayout(double width)
        {
            // Loaded also applies the layout if an early SizeChanged arrived
            // before InitializeComponent had populated every named element.
            if (PrimaryGrid == null || AnalyticsGrid == null || HeroActions == null || HeroIdentity == null
                || StatsBand == null || PulseCard == null || DeliveryCard == null || TrendCard == null || ActivityCard == null)
                return;
            if (width <= 0 || Math.Abs(width - _lastLayoutWidth) < 0.5) return;
            _lastLayoutWidth = width;

            KpiColumns = width >= 1180 ? 4 : width >= 680 ? 2 : 1;
            StatColumns = width >= 600 ? 3 : 1;
            PipelineColumns = width >= 950 ? 5 : 1;
            DeliveryColumns = width >= 1180 || (width < 880 && width >= 640) ? 4 : 2;

            ArrangePair(PulseCard, DeliveryCard, width < 880);
            bool stackedAnalytics = width < 1050;
            Grid.SetColumnSpan(StatsBand, stackedAnalytics ? 2 : 1);
            Grid.SetColumnSpan(TrendCard, stackedAnalytics ? 2 : 1);
            Grid.SetRow(ActivityCard, stackedAnalytics ? 2 : 0);
            Grid.SetColumn(ActivityCard, stackedAnalytics ? 0 : 1);
            Grid.SetColumnSpan(ActivityCard, stackedAnalytics ? 2 : 1);
            Grid.SetRowSpan(ActivityCard, stackedAnalytics ? 1 : 2);
            ActivityCard.Margin = stackedAnalytics ? new Thickness(0, 18, 0, 0) : new Thickness(18, 0, 0, 0);

            bool stackedHeader = width < 1100;
            Grid.SetColumnSpan(HeroIdentity, stackedHeader ? 2 : 1);
            Grid.SetRow(HeroActions, stackedHeader ? 1 : 0);
            Grid.SetColumn(HeroActions, stackedHeader ? 0 : 1);
            Grid.SetColumnSpan(HeroActions, stackedHeader ? 2 : 1);
            HeroActions.HorizontalAlignment = stackedHeader ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            HeroActions.Margin = new Thickness(0, stackedHeader ? 18 : 0, 0, 0);
        }

        private static void ArrangePair(FrameworkElement first, FrameworkElement second, bool stacked)
        {
            Grid.SetColumnSpan(first, stacked ? 2 : 1);
            Grid.SetRow(second, stacked ? 1 : 0);
            Grid.SetColumn(second, stacked ? 0 : 1);
            Grid.SetColumnSpan(second, stacked ? 2 : 1);
            second.Margin = stacked ? new Thickness(0, 18, 0, 0) : new Thickness(18, 0, 0, 0);
        }
    }
}
