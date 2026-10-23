using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;

namespace ProGlassAutomation.Analytics;

public sealed class BentoKpi : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(BentoKpi), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(BentoKpi), new PropertyMetadata(""));
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(nameof(Detail), typeof(string), typeof(BentoKpi), new PropertyMetadata(""));
    public static readonly DependencyProperty SparkProperty = DependencyProperty.Register(nameof(Spark), typeof(ChartDataset), typeof(BentoKpi));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Detail { get => (string)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public ChartDataset? Spark { get => (ChartDataset?)GetValue(SparkProperty); set => SetValue(SparkProperty, value); }
    protected override AutomationPeer OnCreateAutomationPeer() => new KpiPeer(this);
    private sealed class KpiPeer(BentoKpi owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => $"{owner.Label}: {owner.Value}. {owner.Detail}";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
