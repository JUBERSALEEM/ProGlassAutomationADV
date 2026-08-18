// ═══════════════════════════════════════════════════════════════════════
// FILE: JobProfitabilityRow.cs
// PROJECT: ProGlassAutomation
//
// JobProfitabilityRow split out of JobProfitabilityViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobProfitability
{
    public class JobProfitabilityRow : ViewModelBase
    {
        public string ProformaId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public DateTime IssueDate { get; set; }

        /// <summary>Revenue excluding VAT, as recorded by Step 9.</summary>
        public decimal NetExVat { get; set; }

        /// <summary>Glass cost + production cost.</summary>
        public decimal TotalCost { get; set; }

        public decimal Profit { get; set; }
        public decimal MarginPercent { get; set; }

        /// <summary>True when this job lost money.</summary>
        public bool IsLoss { get; set; }

        /// <summary>
        /// True when the stored P&amp;L was produced by the pre-fix DGU/LAM
        /// costing, which dropped the inner pane. The profit shown is
        /// OVERSTATED until Step 9 is recalculated and saved.
        /// </summary>
        public bool NeedsRecalculation { get; set; }

        /// <summary>Why it is flagged. Empty when it is not.</summary>
        public string RecalculationWarning { get; set; } = "";

        /// <summary>
        /// False for invoices saved before Step 9 existed (no stored P&amp;L).
        /// The view greys these out instead of showing a fake 100% margin.
        /// </summary>
        public bool IsCosted { get; set; }

        // ── Display helpers ───────────────────────────────────────────────

        public string IssueDateDisplay => IssueDate == default
            ? "—"
            : IssueDate.ToString("dd MMM yyyy");

        public string NetDisplay => $"AED {NetExVat:N2}";
        public string CostDisplay => $"AED {TotalCost:N2}";
        public string ProfitDisplay => $"AED {Math.Abs(Profit):N2}";

        public string MarginDisplay => IsCosted
            ? $"{MarginPercent:0.#}%"
            : "not costed";

        /// <summary>Customer and project on one line for the compact row.</summary>
        public string CustomerProjectDisplay =>
            string.IsNullOrWhiteSpace(ProjectName)
                ? CustomerName
                : $"{CustomerName} · {ProjectName}";

        public string SalesmanDisplay =>
            string.IsNullOrWhiteSpace(Salesman) ? "—" : Salesman;
    }
}
