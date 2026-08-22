using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public class OptimizationViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Optimization";
        public override string Description => "Cutting optimization engine to minimize waste and maximize sheet utilization.";
        public override string Icon => "\uE713"; // ⚙ → Segoe MDL2 (matches AboutView settings/workflow glyph)
    }
}