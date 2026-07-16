using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    public class SheetStoreViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Sheet Store";
        public override string Description => "Manage raw glass sheet inventory — track stock levels, incoming supplies, and consumption.";
        public override string Icon => "\uD83D\uDCE6"; // 📦
    }
}