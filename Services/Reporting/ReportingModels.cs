// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Shared Models
//
// PURPOSE:
//   All DTOs consumed by report ViewModels, export layouts, and the AI
//   reporting tool. No EF, no WPF — pure POCOs so every layer can
//   project efficiently with AsNoTracking().Select().
//
// TRACEABILITY MODEL:
//   Every glass row can be traced:
//     PI → Row → JO → DN → TI → Profitability
//   ReportingDataService materializes RowTrace objects that carry the
//   full lifecycle and reconciliation for one exact specification row.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Services.Reporting
{
    // ── Base Document Headers ─────────────────────────────────────────

    public class PiReportRow
    {
        public int Id { get; set; }
        public string ProformaId { get; set; } = "";
        public string Status { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string PreparedBy { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public decimal GrandTotal { get; set; }
        public int SpecGroupCount { get; set; }
        public int TotalRows { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public bool HasJobOrder { get; set; }
        public bool HasDelivery { get; set; }
        public bool HasTaxInvoice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // ── Inline expander state ─────────────────────────────────────
        public bool IsExpanded { get; set; }

        /// <summary>True once the spec-row drill-down has loaded (lazy).</summary>
        public bool HasLoadedDetail { get; set; }

        /// <summary>Itemized specification rows — loaded lazily on expand.</summary>
        public ObservableCollection<GlassSpecReportRow> DetailRows { get; set; } = new();

        /// <summary>Ordered / delivered / invoiced reconciliation for this PI.</summary>
        public QuantityReconciliation? Reconciliation { get; set; }
    }

    public class JoReportRow
    {
        public int Id { get; set; }
        public string JobOrderId { get; set; } = "";
        public string SourceProformaId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string PreparedBy { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public DateTime? DeliveryDate { get; set; }
        public decimal GrandTotal { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>Inline expander state (single grid — no side panel).</summary>
        public bool IsExpanded { get; set; }
    }

    public class DeliveryReportRow
    {
        // ── Row identity — every row traces to its exact
        //    PI → Specification → Sr No ──────────────────────────────
        public int Id { get; set; }
        public string JobOrderId { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        // ── Display columns ─────────────────────────────────────────
        public string Specification { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string GlassRef { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        // ── Quantity reconciliation (per exact row) ─────────────────
        public int TotalQty { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }

        // ── Row delivery state ──────────────────────────────────────
        public string DeliveryStatus { get; set; } = "";
        public string DeliveryStatusBrushKey { get; set; } = "NeutralBrush";

        /// <summary>Complete glass description for the inline detail.</summary>
        public string GlassDetails => BuildGlassDetails();

        /// <summary>
        /// Inline expander state — the row is a single vertical grid; the
        /// delivery history is revealed inline (no side panel).
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// True once the related delivery notes have been loaded (loaded
        /// lazily on first expand).
        /// </summary>
        public bool HasLoadedDeliveries { get; set; }

        /// <summary>
        /// Related delivery notes — populated lazily on first expand. An
        /// ObservableCollection so the inline list refreshes in place when
        /// the async load completes.
        /// </summary>
        public ObservableCollection<DeliveryNoteDelivery> Deliveries { get; set; } = new();

        private string BuildGlassDetails()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(GlassRef)) parts.Add(GlassRef);
            parts.Add($"{W1} × {H1}");
            if (W2 > 0 || H2 > 0) parts.Add($"{W2} × {H2}");
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>
    /// One Delivery Note's delivery of a single PI spec row — shown in the
    /// row detail panel (which DN, when, how many pieces).
    /// </summary>
    public class DeliveryNoteDelivery
    {
        public string DeliveryNoteId { get; set; } = "";
        public DateTime DeliveryDate { get; set; }
        public string Status { get; set; } = "";
        public int QtyDelivered { get; set; }
        public string DriverName { get; set; } = "";
        public string VehicleNumber { get; set; } = "";
    }

    public class TaxInvoiceReportRow
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string JobOrderId { get; set; } = "";
        public string DeliveryNoteReference { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string PreparedBy { get; set; } = "";
        public string Status { get; set; } = "";
        public string InvoiceType { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal VatAmount { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Inline expander state (single grid — no side panel).</summary>
        public bool IsExpanded { get; set; }
    }

    public class StatementReportRow
    {
        public int Id { get; set; }
        public string StatementNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public int InvoiceCount { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FilterJson { get; set; } = "";
    }

    // ── Company / Customer aggregates ─────────────────────────────────

    public class CompanyReportRow
    {
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public int PiCount { get; set; }
        public int JobOrderCount { get; set; }
        public int DeliveryCount { get; set; }
        public int TaxInvoiceCount { get; set; }
        public int StatementCount { get; set; }
        public decimal PiValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public int PendingQty { get; set; }
        public int OverdueInvoices { get; set; }
    }

    public class CustomerReportRow
    {
        public string CustomerName { get; set; } = "";
        public string CustomerTrn { get; set; } = "";
        public string Salesman { get; set; } = "";
        public int PiCount { get; set; }
        public int JoCount { get; set; }
        public int DnCount { get; set; }
        public int TiCount { get; set; }
        public int StatementCount { get; set; }
        public decimal PiValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal Outstanding { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public int PendingQty { get; set; }
    }

    public class SalesmanReportRow
    {
        public string SalesmanName { get; set; } = "";
        public int PiCount { get; set; }
        public int ConfirmedPiCount { get; set; }
        public decimal ConfirmedValue { get; set; }
        public int JobOrderCount { get; set; }
        public int DeliveryCount { get; set; }
        public int TaxInvoiceCount { get; set; }
        public int CustomerCount { get; set; }
        public int ProjectCount { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public decimal Margin { get; set; }
    }

    public class ProjectReportRow
    {
        public string ProjectName { get; set; } = "";
        public string ProjectContractNo { get; set; } = "";
        public string ProjectReference { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public int PiCount { get; set; }
        public int JoCount { get; set; }
        public int DnCount { get; set; }
        public int TiCount { get; set; }
        public decimal PiValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Profit { get; set; }
        public decimal Margin { get; set; }
        public int StatementCount { get; set; }
    }

    // ── Row-level intelligence ────────────────────────────────────────

    public class GlassSpecReportRow
    {
        public string RowId { get; set; } = ""; // composite: PI-Grp-Serial
        public string ProformaId { get; set; } = "";
        public string JobOrderId { get; set; } = "";
        public string DeliveryNoteId { get; set; } = "";
        public string TaxInvoiceNumber { get; set; } = "";
        public string GlassRef { get; set; } = "";
        public string SpecName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string OuterColor { get; set; } = "";
        public string InnerColor { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public int Qty { get; set; }
        public decimal Sqm1 { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public decimal BaseRate { get; set; }
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public int SerialNumber { get; set; }

        // Sheet provenance
        public int OuterSheetWidthMm { get; set; }
        public int OuterSheetHeightMm { get; set; }
        public int InnerSheetWidthMm { get; set; }
        public int InnerSheetHeightMm { get; set; }

        // Lifecycle / reconciliation
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public int InvoicedQty { get; set; }
        public string DeliveryStatus { get; set; } = ""; // Pending / Partial / Complete
        public string InvoiceStatus { get; set; } = ""; // Pending / Partial / Complete
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public string PiStatus { get; set; } = "";

        // Specification details for identity
        public string CanonicalKey { get; set; } = "";
        public string DimensionsKey { get; set; } = "";
    }

    // ── Canonical spec identity ───────────────────────────────────────

    public class SpecIdentity
    {
        public string GlassRef { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string SpecName { get; set; } = "";
        public string OuterColor { get; set; } = "";
        public string InnerColor { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public string Dimensions => $"{W1}×{H1}" + (W2 > 0 || H2 > 0 ? $" / {W2}×{H2}" : "");
        public decimal Sqm { get; set; }

        // Canonical key — type-aware, exact match only
        public string CanonicalKey => SpecificationMatcher.BuildCanonicalKey(this);
    }

    public class SpecAggregation
    {
        public string CanonicalKey { get; set; } = "";
        public string SpecName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string GlassRef { get; set; } = "";
        public string DimensionsKey { get; set; } = "";
        public int TotalRows { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AvgRate { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public int DistinctCustomers { get; set; }
        public int DistinctProjects { get; set; }
        public List<string> TopCustomers { get; set; } = new();
    }

    // ── Drill-down trace ──────────────────────────────────────────────

    public class RowTrace
    {
        public GlassSpecReportRow SourceRow { get; set; } = new();
        public List<TraceStep> Steps { get; set; } = new();
        public QuantityReconciliation Reconciliation { get; set; } = new();
    }

    public class TraceStep
    {
        public string Stage { get; set; } = ""; // PI, Job Order, Delivery, Tax Invoice, Profitability
        public string DocumentNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public int Qty { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
        public string Notes { get; set; } = "";
    }

    public class QuantityReconciliation
    {
        public int OrderedQty { get; set; }
        public int JobOrderQty { get; set; }
        public int DeliveredQty { get; set; }
        public int RemainingQty { get; set; }
        public int InvoicedQty { get; set; }
        public string DeliveryState { get; set; } = ""; // NotDelivered / PartiallyDelivered / FullyDelivered
        public string InvoiceState { get; set; } = ""; // Pending / PartiallyInvoiced / FullyInvoiced
        public decimal DeliveryPct => OrderedQty <= 0 ? 0 : Math.Round(DeliveredQty / (decimal)OrderedQty * 100, 1);
        public decimal InvoicePct => OrderedQty <= 0 ? 0 : Math.Round(InvoicedQty / (decimal)OrderedQty * 100, 1);
    }

    // ── Aggregates for KPIs ───────────────────────────────────────────

    public class PiReportSummary
    {
        public int TotalPi { get; set; }
        public int Draft { get; set; }
        public int Released { get; set; }
        public int Confirmed { get; set; }
        public int Rejected { get; set; }
        public int Pending { get; set; }
        public int Converted { get; set; }
        public int NotConverted { get; set; }
        public decimal TotalValue { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
    }

    public class JoReportSummary
    {
        public int TotalJo { get; set; }
        public int FromPi { get; set; }
        public int AwaitingJo { get; set; }
        public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> ByPriority { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class DeliveryReportSummary
    {
        public int TotalRows { get; set; }
        public int NotDelivered { get; set; }
        public int Partial { get; set; }
        public int FullyDelivered { get; set; }
        public int TotalOrderedQty { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalRemainingQty { get; set; }
        public decimal CompletionPct => TotalOrderedQty <= 0 ? 0 : Math.Round(TotalDeliveredQty / (decimal)TotalOrderedQty * 100, 1);
    }

    public class TaxInvoiceReportSummary
    {
        public int TotalInvoices { get; set; }
        public int Issued { get; set; }
        public int Paid { get; set; }
        public int PartiallyPaid { get; set; }
        public int Overdue { get; set; }
        public int Draft { get; set; }
        public int PendingInvoice { get; set; } // confirmed PI without TI
        public decimal TotalAmount { get; set; }
        public decimal TotalVat { get; set; }
        public decimal Outstanding { get; set; }
    }

    public class StatementReportSummary
    {
        public int TotalCount { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal TotalOverdue { get; set; }
    }

    /// <summary>
    /// One financial document line in the Statement report — a single
    /// statement line item (one Tax Invoice inside one saved statement)
    /// rendered as a ledger row with Debit / Credit / Balance.
    /// </summary>
    public class StatementLineRow
    {
        public int Id { get; set; }
        public string StatementNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string DocumentType { get; set; } = "Tax Invoice";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Debit { get; set; }        // amount billed
        public decimal Credit { get; set; }       // amount paid
        public decimal Balance { get; set; }      // Debit − Credit (outstanding)

        /// <summary>Inline expander state (single grid — no side panel).</summary>
        public bool IsExpanded { get; set; }
    }

    /// <summary>
    /// Financial statistics for the Statement report:
    /// Opening Balance (outstanding before the period) + Total Debit −
    /// Total Credit = Closing Balance. All values from real stored data.
    /// </summary>
    public class StatementLineSummary
    {
        public int TotalCount { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal ClosingBalance { get; set; }
    }

    // ── Profitability ─────────────────────────────────────────────────

    public class ProfitabilityRow
    {
        public string Key { get; set; } = ""; // PI / JO / Project / Customer / Salesman / Spec key
        public string Name { get; set; } = "";
        public string Category { get; set; } = ""; // PI, JobOrder, Project, Company, Salesman, Specification
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal GlassCost { get; set; }
        public decimal ProductionCost { get; set; }
        public decimal Profit { get; set; }
        public decimal Margin { get; set; }
        public int Qty { get; set; }
        public decimal Sqm { get; set; }
        public int DocumentCount { get; set; }
        public bool HasCostData { get; set; }
        public string CostSource { get; set; } = "";
    }

    public class ProfitabilitySummary
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AvgMargin { get; set; }
        public int InvoicesWithCost { get; set; }
        public int InvoicesWithoutCost { get; set; }
        public List<string> MissingCostSources { get; set; } = new();
    }
}
