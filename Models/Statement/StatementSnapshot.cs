// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/StatementSnapshot.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   Complete computed state of a generated statement, serialized to JSON
//   and stored in SavedStatement.FullSnapshotJson.
//
//   Mirrors the JSON-blob strategy used by PI, DN, and Tax Invoice modules.
//   Allows re-rendering the PDF at any time without re-querying the DB.
//
// STRUCTURE:
//   StatementSnapshot
//     ├── StatementHeader       — title, period, generated date
//     ├── StatementCustomerInfo — company/customer/project/salesman
//     ├── List<StatementInvoiceRow> — one per Tax Invoice
//     ├── StatementFinancialSummary — totals + aging
//     └── StatementAgingBuckets    — 5 aging buckets
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement.Reconciliation;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Models.Statement
{
    /// <summary>
    /// Root snapshot — full computed statement data.
    /// Serialized to JSON in SavedStatement.FullSnapshotJson.
    /// </summary>
    public class StatementSnapshot
    {
        public StatementHeader Header { get; set; } = new();
        public StatementCustomerInfo CustomerInfo { get; set; } = new();
        public StatementFinancialSummary Financial { get; set; } = new();
        public StatementAgingBuckets Aging { get; set; } = new();

        /// <summary>One row per Tax Invoice included in this statement.</summary>
        public List<StatementInvoiceRow> InvoiceRows { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Statement header metadata.</summary>
    public class StatementHeader
    {
        public string StatementNumber { get; set; } = ""; // STMT-YYYY-NNN
        public string Title { get; set; } = "";
        public string FilterSummary { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }

        /// <summary>Display string for the period shown on the PDF header.</summary>
        public string PeriodDisplay =>
            PeriodFrom.HasValue || PeriodTo.HasValue
                ? $"{PeriodFrom?.ToString("dd MMM yyyy") ?? "—"} " +
                  $"to {PeriodTo?.ToString("dd MMM yyyy") ?? "—"}"
                : "All Dates";
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Customer / project / company info shown on PDF page 1.</summary>
    public class StatementCustomerInfo
    {
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string SalesmanName { get; set; } = "";
        public string ContactPerson { get; set; } = "";

        // v3 — issuing company branding (resolved from master data at
        // generation time) so the statement letterhead carries the logo.
        public string LogoPath { get; set; } = "";
        public CompanyLogoLayout LogoLayout { get; set; } = CompanyLogoLayout.None;
        public double LogoWidthMm { get; set; } = 22;
        public double LogoHeightMm { get; set; } = 26;
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One row in the statement invoice table.
    /// Maps 1:1 to a Tax Invoice (or PI if no invoice exists yet).
    /// </summary>
    public class StatementInvoiceRow
    {
        // ── Invoice identity ──────────────────────────────────────────────
        public string InvoiceNumber { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string DeliveryNoteRef { get; set; } = "";
        public string PurchaseOrderNumber { get; set; } = "";
        public string InvoiceType { get; set; } = ""; // Full | Partial
        public string Status { get; set; } = "";

        // ── Dates ─────────────────────────────────────────────────────────
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        // ── Quantities ────────────────────────────────────────────────────
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }

        // ── Financials ────────────────────────────────────────────────────
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }

        /// <summary>GrandTotal − PaidAmount. Never negative.</summary>
        public decimal Balance => Math.Max(0m, GrandTotal - PaidAmount);

        // ── Aging ─────────────────────────────────────────────────────────

        /// <summary>
        /// Days overdue based on DueDate. 0 if not yet due or paid.
        /// Used to bucket into aging analysis.
        /// </summary>
        public int DaysOverdue { get; set; }

        // ── Attachment flags (for PDF merge decisions) ────────────────────

        /// <summary>True if this invoice has a SignedPI attachment.</summary>
        public bool HasSignedPi { get; set; }

        /// <summary>True if this invoice has a SignedDN attachment.</summary>
        public bool HasSignedDn { get; set; }

        /// <summary>Total attachment count for this invoice.</summary>
        public int AttachmentCount { get; set; }

        // ── Display helpers ───────────────────────────────────────────────
        public string IssueDateDisplay =>
            IssueDate == default ? "—" : IssueDate.ToString("dd MMM yyyy");
        public string DueDateDisplay =>
            DueDate == default ? "—" : DueDate.ToString("dd MMM yyyy");
        public bool IsOverdue =>
            DueDate != default && DueDate < DateTime.Today &&
            Status != "Paid" && Status != "Cancelled" && Status != "Void";

        // ── Order Reconciliation (Chat 22 v3) ─────────────────────────────

        /// <summary>
        /// Cross-referenced PI/DN/TI quantities for this invoice's PI family.
        /// Null if not yet computed or if ProformaId is empty/missing.
        /// Populated by StatementAggregator.AttachReconciliationToRows during
        /// statement generation. Persisted in FullSnapshotJson automatically.
        /// Old saved statements (pre-Chat 22) will have this as null.
        /// </summary>
        public OrderReconciliationSummary? Reconciliation { get; set; }

        /// <summary>
        /// Convenience flag — true when reconciliation data is available.
        /// UI can use this to show/hide the reconciliation section.
        /// </summary>
        public bool HasReconciliation => Reconciliation != null;

        /// <summary>
        /// Convenience flag — true when this invoice's PI family has
        /// any billing discrepancy (over-invoiced, under-invoiced, etc.).
        /// UI can use this to show a red dot or warning indicator.
        /// </summary>
        public bool HasReconciliationIssue =>
            Reconciliation != null && Reconciliation.HasDiscrepancies;

        /// <summary>
        /// Convenience flag — true when this invoice is OVER-INVOICED.
        /// This is the critical bug scenario: billed more than delivered.
        /// UI should highlight these rows in RED.
        /// </summary>
        public bool IsOverInvoiced =>
            Reconciliation != null && Reconciliation.HasOverInvoicing;
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aggregated financial summary across all invoices in the statement.
    /// Shown in the financial summary section of the PDF.
    /// </summary>
    public class StatementFinancialSummary
    {
        public int TotalInvoiceCount { get; set; }
        public decimal TotalSubtotal { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalInvoiced { get; set; } // SUM GrandTotal
        public decimal TotalPaid { get; set; }
        public decimal TotalOutstanding { get; set; } // SUM Balance
        public decimal OverdueAmount { get; set; } // Outstanding on overdue invoices
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AR aging buckets based on DueDate.
    /// Current = not yet due.
    /// 0-30, 31-60, 61-90, 90+ = days past due date.
    /// </summary>
    public class StatementAgingBuckets
    {
        /// <summary>Outstanding amount not yet past due date.</summary>
        public decimal Current { get; set; }

        /// <summary>Outstanding amount 1-30 days past due.</summary>
        public decimal Days0To30 { get; set; }

        /// <summary>Outstanding amount 31-60 days past due.</summary>
        public decimal Days31To60 { get; set; }

        /// <summary>Outstanding amount 61-90 days past due.</summary>
        public decimal Days61To90 { get; set; }

        /// <summary>Outstanding amount more than 90 days past due.</summary>
        public decimal Days90Plus { get; set; }

        /// <summary>Sum of all buckets. Should equal TotalOutstanding.</summary>
        public decimal Total =>
            Current + Days0To30 + Days31To60 + Days61To90 + Days90Plus;
    }
}