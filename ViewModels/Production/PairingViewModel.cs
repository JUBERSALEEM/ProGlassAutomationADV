using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class PairingViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Pairing";
        public override string Description => "Pair glass pieces with stands for tracking and identification across production floor.";
        public override string Icon => "\uE71B"; // 🔗 → Segoe MDL2 (matches AboutView Pairing stage)
    }
}