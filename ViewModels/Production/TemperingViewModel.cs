using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class TemperingViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Tempering";
        public override string Description => "Manage tempering furnace operations and batch tracking.";
        public override string Icon => "\uE728"; // 🔥 → Segoe MDL2 (matches AboutView Tempering stage)
    }
}