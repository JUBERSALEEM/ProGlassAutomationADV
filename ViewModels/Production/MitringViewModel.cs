using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class MitringViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Mitring";
        public override string Description => "Track mitring operations for angled edge finishing.";
        public override string Icon => "\uE7E8"; // 🔺 → Segoe MDL2 (matches AboutView Mitring stage)
    }
}