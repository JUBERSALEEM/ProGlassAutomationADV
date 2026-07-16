using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.DeliveryNote
{
    public class DeliveryNoteViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Delivery Note";
        public override string Description => "Generate and manage delivery challans for dispatched goods.";
        public override string Icon => "\uD83D\uDE9A";
    }
}