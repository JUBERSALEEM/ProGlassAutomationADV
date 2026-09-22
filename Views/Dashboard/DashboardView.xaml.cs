using ProGlassAutomation.ViewModels.Dashboard;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Dashboard
{
    public partial class DashboardView : UserControl
    {
        private bool? _compactHeader;

        public DashboardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Fires every time the user navigates to Dashboard.
        /// Delegates to the shell VM which refreshes the active sub-page.
        /// </summary>
        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyHeaderLayout(ActualWidth);
            if (DataContext is DashboardViewModel vm)
                vm.OnViewLoaded();
        }
        private void OnDashboardSizeChanged(object sender, SizeChangedEventArgs e)
            => ApplyHeaderLayout(e.NewSize.Width);

        private void ApplyHeaderLayout(double width)
        {
            if (width <= 0 || BrandIdentity == null || PageSubtitle == null || UserBadge == null) return;
            bool compact = width < 720;
            if (_compactHeader == compact) return;
            _compactHeader = compact;

            // Preserve the user and active-page bindings rather than hiding
            // them to make room for the brand at narrow view widths/high DPI.
            Grid.SetColumnSpan(BrandIdentity, compact ? 3 : 1);
            Grid.SetRow(PageSubtitle, compact ? 1 : 0);
            Grid.SetColumn(PageSubtitle, compact ? 0 : 1);
            Grid.SetColumnSpan(PageSubtitle, compact ? 2 : 1);
            PageSubtitle.Margin = compact ? new Thickness(0, 10, 16, 0) : new Thickness(22, 0, 16, 0);
            Grid.SetRow(UserBadge, compact ? 1 : 0);
            UserBadge.Margin = new Thickness(0, compact ? 10 : 0, 0, 0);
        }
    }
}
