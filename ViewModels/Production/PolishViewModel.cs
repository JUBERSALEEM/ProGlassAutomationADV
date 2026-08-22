using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.Production
{
    public class PolishViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Polish";
        public override string Description => "Track polishing operations for edges and surfaces.";
        public override string Icon => "\uE895"; // ✨ → Segoe MDL2 (matches AboutView Polish stage)
    }
}