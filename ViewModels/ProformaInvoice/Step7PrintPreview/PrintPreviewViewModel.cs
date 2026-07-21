using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview
{
    /// <summary>
    /// Main ViewModel for Step 7 — Print Preview.
    /// Hosts:
    ///   - Invoice Main Summary (Phase 7.12) financial waterfall
    ///   - Terms &amp; Conditions editor (Phase 7.14)
    ///   - Printable Invoice preview (Phase 7.13) — full A4 layout
    /// </summary>
    public class PrintPreviewViewModel : ViewModelBase
    {
        public string Title { get; } = "PRINT PREVIEW & REVIEW";
        public string Subtitle { get; } = "Final review of financial totals, editable terms, and printable A4 preview.";

        /// <summary>Phase 7.12 — Invoice financial totals card.</summary>
        public InvoiceMainSummaryViewModel InvoiceMainSummary { get; }

        /// <summary>Phase 7.14 — Editable Terms &amp; Conditions with default clauses.</summary>
        public TermsAndConditionsViewModel Terms { get; }

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

            // Phase 7.14 — instantiate Terms editor with 6 UAE glass industry defaults
            Terms = new TermsAndConditionsViewModel();

            // PrintableInvoice receives Terms as its final parameter so it can render
            // the T&C section on the A4 paper preview live from user edits.
            PrintableInvoice = new PrintableInvoiceViewModel(
                companyVM,
                projectVM,
                clientVM,
                dataGridVM,
                otherChargesVM,
                InvoiceMainSummary,
                Terms);
        }
    }
}