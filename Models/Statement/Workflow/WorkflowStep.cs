// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/WorkflowStep.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine (Chat 17)
//
// PURPOSE:
//   Defines the 8 discrete steps of statement generation.
//   Each step reports progress to the WorkflowProgressDialog
//   so the user sees exactly what is happening at every moment.
//
// STEP ORDER (must run in this sequence):
//   1. ReadProformaInvoice     — verify PI confirmed + signed attachment
//   2. ReadJobOrder            — verify JO released + get ordered qty
//   3. ReadDeliveryNotes       — load all DNs + attachments
//   4. ValidateQuantities      — sum delivered qty across DNs
//   5. ReadTaxInvoices         — load all invoices for this PI
//   6. CrossMatchCoverage      — detect missing invoices
//   7. GenerateStatementPdf    — build statement summary pages
//   8. MergeIntoFinalPdf       — combine everything into one PDF
//
// DESIGN NOTES:
//   • Each step has a fixed order value (used for UI display order)
//   • Icons chosen for clarity in progress dialog
//   • Names are user-facing — shown directly in the dialog
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>
    /// The 8 sequential steps of statement generation.
    /// Order value used for UI display and progress tracking.
    /// </summary>
    public enum WorkflowStep
    {
        /// <summary>Step 1: Load PI and verify confirmed at Step 8.</summary>
        ReadProformaInvoice = 1,

        /// <summary>Step 2: Load Job Order and verify released.</summary>
        ReadJobOrder = 2,

        /// <summary>Step 3: Load all Delivery Notes + their attachments.</summary>
        ReadDeliveryNotes = 3,

        /// <summary>Step 4: Calculate delivered qty from DN totals.</summary>
        ValidateQuantities = 4,

        /// <summary>Step 5: Load all Tax Invoices for this PI.</summary>
        ReadTaxInvoices = 5,

        /// <summary>Step 6: Detect missing invoices vs delivered qty.</summary>
        CrossMatchCoverage = 6,

        /// <summary>Step 7: Generate the statement summary PDF.</summary>
        GenerateStatementPdf = 7,

        /// <summary>Step 8: Merge statement + invoices + attachments.</summary>
        MergeIntoFinalPdf = 8
    }

    /// <summary>
    /// Extension helpers for WorkflowStep.
    /// Keeps UI concerns (title, icon, description) out of the enum
    /// itself while still centralizing display metadata.
    /// </summary>
    public static class WorkflowStepExtensions
    {
        /// <summary>
        /// User-facing step title shown in the progress dialog.
        /// </summary>
        public static string GetTitle(this WorkflowStep step) => step switch
        {
            WorkflowStep.ReadProformaInvoice => "Reading Proforma Invoice",
            WorkflowStep.ReadJobOrder => "Reading Job Order",
            WorkflowStep.ReadDeliveryNotes => "Reading Delivery Notes",
            WorkflowStep.ValidateQuantities => "Validating Quantities",
            WorkflowStep.ReadTaxInvoices => "Reading Tax Invoices",
            WorkflowStep.CrossMatchCoverage => "Checking Invoice Coverage",
            WorkflowStep.GenerateStatementPdf => "Generating Statement PDF",
            WorkflowStep.MergeIntoFinalPdf => "Merging Final PDF",
            _ => "Unknown Step"
        };

        /// <summary>
        /// Short description shown as subtitle in the progress dialog.
        /// Explains what the step does in plain English.
        /// </summary>
        public static string GetDescription(this WorkflowStep step) => step switch
        {
            WorkflowStep.ReadProformaInvoice =>
                "Verifying PI confirmation and signed attachment",
            WorkflowStep.ReadJobOrder =>
                "Verifying job order release status and ordered qty",
            WorkflowStep.ReadDeliveryNotes =>
                "Loading all delivery notes and checking attachments",
            WorkflowStep.ValidateQuantities =>
                "Comparing ordered vs delivered quantities",
            WorkflowStep.ReadTaxInvoices =>
                "Loading all tax invoices for this proforma",
            WorkflowStep.CrossMatchCoverage =>
                "Detecting missing invoices for delivered items",
            WorkflowStep.GenerateStatementPdf =>
                "Building account statement summary pages",
            WorkflowStep.MergeIntoFinalPdf =>
                "Combining statement + invoices + attachments",
            _ => ""
        };

        /// <summary>
        /// Icon emoji shown in the progress dialog for visual scanning.
        /// </summary>
        public static string GetIcon(this WorkflowStep step) => step switch
        {
            WorkflowStep.ReadProformaInvoice => "📝",
            WorkflowStep.ReadJobOrder => "📋",
            WorkflowStep.ReadDeliveryNotes => "🚚",
            WorkflowStep.ValidateQuantities => "🔢",
            WorkflowStep.ReadTaxInvoices => "🧾",
            WorkflowStep.CrossMatchCoverage => "🔍",
            WorkflowStep.GenerateStatementPdf => "📊",
            WorkflowStep.MergeIntoFinalPdf => "📄",
            _ => "•"
        };

        /// <summary>
        /// Zero-based index (0-7) for progress bar calculation.
        /// </summary>
        public static int GetOrder(this WorkflowStep step) => (int)step - 1;
    }
}