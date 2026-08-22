using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class DoublingLaminationViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Doubling / Lamination";
        public override string Description => "Manage double glazing and lamination processes for finished glass units.";
        public override string Icon => "\uE81C"; // 📚 → Segoe MDL2 (matches AboutView Doubling/DGU stage)
    }
}