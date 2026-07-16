using ProGlassAutomation.ViewModels.Shared;

namespace ProGlassAutomation.ViewModels.TaxInvoice
{
    public class TaxInvoiceViewModel : ComingSoonViewModelBase
    {
        public override string ModuleName => "Tax Invoice";
        public override string Description => "Generate GST-compliant tax invoices from completed delivery notes.";
        public override string Icon => "\uD83E\uDDFE";
    }
}