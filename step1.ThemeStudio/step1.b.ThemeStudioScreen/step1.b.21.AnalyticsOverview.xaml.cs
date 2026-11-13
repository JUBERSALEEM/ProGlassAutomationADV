using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Customization;
using ProGlassAutomation.ThemeBento;

namespace ProGlassAutomation.ThemeStudio;
public partial class AnalyticsOverview : UserControl
{
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(AnalyticsOverview),
        new PropertyMetadata(false));
    private ThemeStudioViewModel? vm;
    public AnalyticsOverview()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLayout();
    }
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == CompactProperty && IsLoaded) ApplyLayout();
    }
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        DataContextChanged += (_, _) => HookVm();
        Loaded += (_, _) => HookVm();
    }
    private void HookVm()
    {
        if (vm != null) vm.PropertyChanged -= VmChanged;
        vm = DataContext as ThemeStudioViewModel;
        if (vm != null) vm.PropertyChanged += VmChanged;
        if (IsLoaded) ApplyLayout();
    }
    private void VmChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ThemeStudioViewModel.SelectedLayoutPreset))
            ApplyLayout();
    }
    private void ApplyLayout()
    {
        if (AnalyticsGrid is null) return;
        AnalyticsGrid.FillAvailable = false;
        AnalyticsGrid.Columns = 4;
        Show(KpiRevenue, 0, 0, 1, 1);
        Show(KpiOrders, 1, 0, 1, 1);
        Show(KpiConversion, 2, 0, 1, 1);
        Show(KpiOnTime, 3, 0, 1, 1);
        Show(TrendsCard, 0, 1, 3, 2);
        Show(ChannelsCard, 3, 2, 1, 1);
        Show(ActivityCard, 0, 3, 1, 1);
        Show(RevenueCard, 1, 3, 1, 1);
        Show(StatusCard, 2, 3, 1, 1);
        Show(TargetCard, 3, 3, 1, 1);
        Hide(TrendCard);
        if (Compact)
        {
            // Studio preview is 4 rows: KPIs, 2×2 trends + type/channels, bottom tiles. Mix lives in the studio page.
            Hide(MixCard);
            Show(SignalsCard, 3, 1, 1, 1);
            Hide(TableCard);
            return;
        }
        Hide(SignalsCard);
        Hide(ChannelsCard);
        Show(MixCard, 3, 1, 1, 2);
        Show(TableCard, 0, 4, 4, 2);
        if (vm is null) return;
        switch (vm.SelectedLayoutPreset)
        {
            case BentoLayoutPreset.Executive:
            case BentoLayoutPreset.AnalyticsFocus:
                Show(TableCard, 0, 4, 4, 2);
                break;
            case BentoLayoutPreset.CompactDense:
                Hide(TableCard);
                break;
        }
    }
    private static void Show(UIElement tile, int column, int row, int columnSpan, int rowSpan)
    {
        tile.Visibility = Visibility.Visible;
        BentoGrid.SetColumn(tile, column);
        BentoGrid.SetRow(tile, row);
        BentoGrid.SetColumnSpan(tile, columnSpan);
        BentoGrid.SetRowSpan(tile, rowSpan);
    }
    private static void Hide(UIElement tile) => tile.Visibility = Visibility.Collapsed;
}
