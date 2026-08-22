using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class HolesCutoutsViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Holes & Cutouts";
        public override string Description => "Manage drilling and cutout operations for hardware fittings and openings.";
        public override string Icon => "\uE71D"; // 🕳 → Segoe MDL2 (matches AboutView Holes & Cutouts stage)
    }
}