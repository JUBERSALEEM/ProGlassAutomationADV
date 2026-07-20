using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview
{
    /// <summary>
    /// Main ViewModel for Step 7 — Print Preview.
    /// Hosts the Invoice Main Summary (Phase 7.12) and, in future phases,
    /// terms & conditions, ERP conversion, and print-to-PDF actions.
    /// </summary>
    public class PrintPreviewViewModel : ViewModelBase
    {
        public string Title { get; } = "PRINT PREVIEW & REVIEW";
        public string Subtitle { get; } = "Final review of financial totals, terms, and export options.";

        /// <summary>Phase 7.12 — Invoice financial totals card.</summary>
        public InvoiceMainSummaryViewModel InvoiceMainSummary { get; }

        public PrintPreviewViewModel(DataGridViewModel dataGridVM, OtherChargesViewModel otherChargesVM)
        {
            InvoiceMainSummary = new InvoiceMainSummaryViewModel(dataGridVM, otherChargesVM);
        }
    }
}