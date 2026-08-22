using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Workflows discovered from MainViewModel.Navigate — not invented modules.
    /// Each step is a navigation key the sidebar already accepts.
    /// </summary>
    public sealed class DemoWorkflow
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Purpose { get; init; } = "";
        public string NavigateTarget { get; init; } = "";
        public IReadOnlyList<string> NarrationBeats { get; init; } = [];
    }

    public static class DemoWorkflowCatalog
    {
        public static readonly IReadOnlyList<DemoWorkflow> All = new[]
        {
            W("Dashboard", "Dashboard", "Factory pulse and module entry.",
                "Dashboard",
                "Open the dashboard to see the factory pulse.",
                "The tiles are the same figures the Overview module computes."),
            W("SheetStore", "Sheet Store", "Inventory of glass sheets.",
                "SheetStore",
                "Open Sheet Store.",
                "Stock, used and balance are the live sheet fields.",
                "Available quantity is Stock minus Used."),
            W("ProformaInvoice", "Proforma Invoice", "Customer quotation through save and PDF.",
                "ProformaInvoice",
                "Open Proforma Invoice.",
                "A quotation starts with the customer and project.",
                "Glass lines carry width, height, quantity, rate and value.",
                "Saving persists the snapshot the rest of the chain reads."),
            W("JobOrder", "Job Order", "Production sheet from an approved PI.",
                "JobOrder",
                "Open Job Orders.",
                "A job is converted from a proforma — it has no selling price on the floor sheet."),
            W("DeliveryNote", "Delivery Note", "Dispatch, delivered quantity and remaining balance.",
                "DeliveryNote",
                "Open Delivery Notes.",
                "Create starts as Dispatching. Make DN marks Dispatched.",
                "A signed attachment moves the note to Received."),
            W("TaxInvoice", "Tax Invoice", "FTA tax invoice from dispatched quantity.",
                "TaxInvoice",
                "Open Tax Invoices.",
                "A master invoice uses dispatched pieces only.",
                "Rate and value stay as separate columns on the FTA layout."),
            W("ReportsHub", "Reports Hub", "Unified reporting overview and entry to every report.",
                "SalesReport",
                "Open the Reports Hub overview.",
                "Use the report navigation to open proforma, job order, delivery, tax invoice and statement analysis."),
            W("SalesReport", "Statement Generator", "Account statement of invoiced and outstanding.",
                "StatementGenerator",
                "Open the Statement Generator (Dispatch).",
                "The generator filters live tax invoices, previews matches and selects them."),
            W("JobProfitability", "Job Profitability", "Margin from Step 9 snapshots.",
                "JobProfitability",
                "Open Job Profitability.",
                "Figures are the profit Step 9 already persisted — they are not recalculated here."),
            W("CompanyCenter", "Company Center", "Supplier identity used on documents.",
                "CompanyCenter",
                "Open Company Center.",
                "Company name, TRN, bank and account print on invoices."),
            W("UserCenter", "User Center", "People who sign documents and audit entries.",
                "UserCenter", "Open User Center.", "Manage operators and document signers."),
            W("Optimization", "Optimization", "Plan efficient sheet yield and cutting layouts.", "Optimization", "Open Optimization."),
            W("Cutting", "Cutting", "Track production cutting work.", "Cutting", "Open Cutting."),
            W("HolesCutouts", "Holes and Cutouts", "Manage drilling and cutout operations.", "HolesCutouts", "Open Holes and Cutouts."),
            W("Polish", "Polish", "Track edge-polishing production.", "Polish", "Open Polish."),
            W("EdgeDeletion", "Edge Deletion", "Track coating edge deletion.", "EdgeDeletion", "Open Edge Deletion."),
            W("Mitring", "Mitring", "Manage mitred-edge fabrication.", "Mitring", "Open Mitring."),
            W("Tempering", "Tempering", "Track furnace and tempering operations.", "Tempering", "Open Tempering."),
            W("Pairing", "Pairing", "Pair production panes for assembly.", "Pairing", "Open Pairing."),
            W("DoublingLamination", "Doubling and Lamination", "Assemble DGU and laminated units.", "DoublingLamination", "Open Doubling and Lamination."),
            W("PiReport", "Proforma Report", "Analyze quotations and values.", "PiReport", "Open Proforma Report."),
            W("JoReport", "Job Order Report", "Analyze production orders.", "JoReport", "Open Job Order Report."),
            W("DeliveryReport", "Delivery Report", "Analyze dispatch and balances.", "DeliveryReport", "Open Delivery Report."),
            W("TaxInvoiceReport", "Tax Invoice Report", "Analyze invoicing and VAT.", "TaxInvoiceReport", "Open Tax Invoice Report."),
            W("StatementReport", "Statement Report", "Analyze generated customer statements.", "StatementReport", "Open Statement Report."),
            W("SystemCenter", "System Center", "Administer access and system users.", "SystemCenter", "Open System Center."),
            W("Diagnostics", "Diagnostics", "Inspect application health and logs.", "Diagnostics", "Open Diagnostics."),
            W("Settings", "Settings", "Configure application services and appearance.", "Settings", "Open Settings."),
            W("About", "About", "Review platform capabilities and release information.", "About", "Open About."),
        };

        public static DemoWorkflow? Find(string? id)
        {
            // Exact catalog IDs must win over navigation aliases. Reports Hub
            // navigates to "SalesReport", while the statement workflow keeps
            // the historical SalesReport ID; one OR predicate made the first
            // entry shadow the statement generator.
            var exact = All.FirstOrDefault(w =>
                string.Equals(w.Id, id, System.StringComparison.OrdinalIgnoreCase));
            return exact ?? All.FirstOrDefault(w =>
                string.Equals(w.NavigateTarget, id, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(w.Title, id, System.StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<DemoWorkflow> Resolve(string? argument)
        {
            if (string.IsNullOrWhiteSpace(argument)
                || string.Equals(argument, "All", System.StringComparison.OrdinalIgnoreCase))
                return All;

            var one = Find(argument);
            return one == null ? Enumerable.Empty<DemoWorkflow>() : new[] { one };
        }

        private static DemoWorkflow W(
            string id, string title, string purpose, string nav, params string[] beats) =>
            new()
            {
                Id = id,
                Title = title,
                Purpose = purpose,
                NavigateTarget = nav,
                NarrationBeats = beats
            };
    }
}
