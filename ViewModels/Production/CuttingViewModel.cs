using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class CuttingViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Cutting";
        public override string Description => "Track glass cutting operations, machine assignments, and cut piece output.";
        public override string Icon => "\u2702"; // ✂
    }
}