using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class EdgeDeletionViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Edge Deletion";
        public override string Description => "Manage edge deletion process for coated glass sheets.";
        public override string Icon => "\uE774"; // 📐 → Segoe MDL2 (matches AboutView Edge Deletion stage)
    }
}