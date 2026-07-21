using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview
{
    /// <summary>
    /// Main ViewModel for Step 7 — Print Preview.
    /// Hosts the Invoice Main Summary (Phase 7.12) financial waterfall,
    /// and the Printable Invoice preview (Phase 7.13) — full A4 layout.
    /// </summary>
    public class PrintPreviewViewModel : ViewModelBase
    {
        public string Title { get; } = "PRINT PREVIEW & REVIEW";
        public string Subtitle { get; } = "Final review of financial totals + printable A4 invoice preview.";

        /// <summary>Phase 7.12 — Invoice financial totals card.</summary>
        public InvoiceMainSummaryViewModel InvoiceMainSummary { get; }

        /// <summary>Phase 7.13 — Full A4 printable invoice preview.</summary>
        public PrintableInvoiceViewModel PrintableInvoice { get; }

        public PrintPreviewViewModel(
            CompanySetupViewModel companyVM,
            ProjectSpecsViewModel projectVM,
            ClientProfileViewModel clientVM,
            DataGridViewModel dataGridVM,
            OtherChargesViewModel otherChargesVM)
        {
            InvoiceMainSummary = new InvoiceMainSummaryViewModel(dataGridVM, otherChargesVM);

            PrintableInvoice = new PrintableInvoiceViewModel(
                companyVM,
                projectVM,
                clientVM,
                dataGridVM,
                otherChargesVM,
                InvoiceMainSummary);
        }
    }
}