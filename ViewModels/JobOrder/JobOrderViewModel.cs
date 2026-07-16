using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.JobOrder
{
    public class JobOrderViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Job Order";
        public override string Description => "Create and manage production job orders converted from approved proforma invoices.";
        public override string Icon => "\uD83D\uDCCB"; // 📋
    }
}