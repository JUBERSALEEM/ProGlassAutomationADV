// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/OrderReport/OrderReportModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — Order Fulfilment Report (2026-09-06)
//
// PURPOSE:
//   POCO models for the consolidated "Overall Report" that walks one
//   order through its full lifecycle:
//     Proforma Invoice  →  Job Order (release)  →  Delivery Notes
//     →  Tax Invoices   →  reconciliation  →  attachment completeness.
//
//   All classes are pure POCOs — JSON-serializable by System.Text.Json
//   (mirrors Models/Statement/* which persist into FullSnapshotJson).
//   Computed get-only properties are ignored on deserialize, exactly
//   like StatementSnapshot / ReconciliationModels.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Reconciliation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Models.OrderReport
{
    /// <summary>
    /// Root snapshot for one order (Proforma family). Serialized to JSON
    /// in SavedOrderReport.FullSnapshotJson so a report can be re-rendered
    /// and re-exported without re-querying the databases.
    /// </summary>
    public class OrderReportSnapshot
    {
        public OrderReportHeader Header { get; set; } = new();

        /// <summary>PI specification rows — same data the Proforma Invoice shows.</summary>
        public List<OrderReportLineItem> SpecRows { get; set; } = new();

        /// <summary>One section per confirmed Delivery Note (excludes Master/Dummy).</summary>
        public List<OrderReportDeliveryNote> DeliveryNotes { get; set; } = new();

        /// <summary>One section per Tax Invoice in the family.</summary>
        public List<OrderReportTaxInvoice> TaxInvoices { get; set; } = new();

        /// <summary>Real attachment counts + missing-document detection.</summary>
        public OrderReportAttachmentCompleteness Attachments { get; set; } = new();

        /// <summary>Step 9 Profit & Loss / target-price analysis captured from the saved PI snapshot.</summary>
        public OrderReportStep9Analysis Step9 { get; set; } = new();

        /// <summary>Cross-PI/DN/TI line reconciliation (reuses existing engine).</summary>
        public OrderReconciliationSummary? Reconciliation { get; set; }

        /// <summary>Rolled-up quantities / weights / values.</summary>
        public OrderReportTotals Totals { get; set; } = new();

        /// <summary>Plain warnings raised while building (missing docs, over-invoicing…).</summary>
        public List<OrderReportWarning> Warnings { get; set; } = new();

        public bool HasDeliveryNotes => DeliveryNotes.Count > 0;
        public bool HasTaxInvoices => TaxInvoices.Count > 0;
        public bool HasStep9 => Step9?.HasData == true;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  BUNDLE — a generated report (single or multi-PI). Persisted as the
    //  FullSnapshotJson of SavedOrderReport so Review + PDF re-render from it.
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportBundle
    {
        public string ReportNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime GeneratedAt { get; set; }

        /// <summary>One section per selected Proforma Invoice.</summary>
        public List<OrderReportSnapshot> Sections { get; set; } = new();

        public int SectionCount => Sections.Count;
        public int TotalOrderedQty => Sections.Sum(s => s.Totals.OrderedQty);
        public int TotalDeliveredQty => Sections.Sum(s => s.Totals.DeliveredQty);
        public int TotalInvoicedQty => Sections.Sum(s => s.Totals.InvoicedQty);
        public decimal TotalInvoicedValue => Sections.Sum(s => s.Totals.InvoicedValue);
        public int TotalMissingDocuments =>
            Sections.Sum(s => s.Attachments.DeliveryNotesMissing);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  HEADER — PI + job-release data
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportHeader
    {
        // ── Proforma Invoice ──────────────────────────────────────────────
        public string ProformaId { get; set; } = "";
        public string PiStatus { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string PreparedBy { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public decimal GrandTotal { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }

        // ── Job release data ─────────────────────────────────────────────
        /// <summary>Comma-separated Job Order ids for this PI ("" = not converted).</summary>
        public string JobOrderIds { get; set; } = "";

        /// <summary>Most relevant JO status (first JO if several).</summary>
        public string JobOrderStatus { get; set; } = "";

        /// <summary>Production priority of the JO (High/Medium/Low).</summary>
        public string JobOrderPriority { get; set; } = "";

        /// <summary>
        /// Release / target delivery date. SavedJobOrder persists
        /// DeliveryDate (the scheduled release) — there is no separate
        /// ReleaseDate column, so this is the release figure we can show
        /// from real stored data.
        /// </summary>
        public DateTime? ReleaseDate { get; set; }

        /// <summary>When the PI was converted into this Job Order.</summary>
        public DateTime? ConvertedAt { get; set; }

        public bool HasJobOrder => !string.IsNullOrWhiteSpace(JobOrderIds);

        public string IssueDateDisplay =>
            IssueDate == default ? "—" : IssueDate.ToString("dd MMM yyyy");
        public string ValidUntilDisplay =>
            ValidUntil == default ? "—" : ValidUntil.ToString("dd MMM yyyy");
        public string ReleaseDateDisplay =>
            ReleaseDate.HasValue ? ReleaseDate.Value.ToString("dd MMM yyyy") : "—";
        public string ConvertedAtDisplay =>
            ConvertedAt.HasValue ? ConvertedAt.Value.ToString("dd MMM yyyy HH:mm") : "—";
    }

    // ═════════════════════════════════════════════════════════════════════
    //  SPEC ROWS — the PI rows exactly as the Proforma Invoice shows them
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportLineItem
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public int SerialNumber { get; set; }
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

        // ── Lifecycle / reconciliation (per exact row) ────────────────────
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public int InvoicedQty { get; set; }
        public string DeliveryStatus { get; set; } = "";
        public string InvoiceStatus { get; set; } = "";

        // ── Weight (from DN items; kg) ────────────────────────────────────
        public decimal WeightPerPiece { get; set; }
        public decimal TotalWeight => WeightPerPiece * DeliveredQty;
        public decimal OrderWeight => WeightPerPiece * Qty;

        public string DimensionsDisplay
        {
            get
            {
                if (W1 <= 0 && H1 <= 0) return "—";
                if ((W2 <= 0 || W2 == W1) && (H2 <= 0 || H2 == H1))
                    return $"{W1}×{H1}";
                return $"{W1}×{H1} / {W2}×{H2}";
            }
        }

        public string CompositeKey => $"{GroupNumber}|{SerialNumber}";
    }


    // ═════════════════════════════════════════════════════════════════════
    //  STEP 9 — P&L / target profitability carried into Report Generator
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportStep9Analysis
    {
        public bool HasData => CostRows.Count > 0 || TargetRows.Count > 0;

        public decimal NetExVat { get; set; }
        public decimal TotalGlassCost { get; set; }
        public decimal TotalProductionCost { get; set; }
        public decimal Profit { get; set; }
        public decimal MarginPercent { get; set; }
        public decimal OverallMarginPercent { get; set; }
        public decimal WastagePercent { get; set; }
        public bool CostAtSellingPrice { get; set; }
        public DateTime? ComputedAtUtc { get; set; }
        public string GlassPriceStamp { get; set; } = "";

        public decimal TargetTotalGivenValue { get; set; }
        public decimal TargetTotalRequiredValue { get; set; }
        public decimal TargetTotalDifferenceValue { get; set; }
        public decimal TargetAverageBasePricePerSqm { get; set; }
        public decimal? TargetExtraProfitMarginPercent { get; set; }
        public decimal? TargetMarginAgainstTargetPercent { get; set; }
        public string TargetStatus { get; set; } = "";
        public string TargetUInsertTreatmentSummary { get; set; } = "";
        public string TargetCalculationVersion { get; set; } = "";

        public List<OrderReportStep9CostRow> CostRows { get; set; } = new();
        public List<OrderReportStep9TargetRow> TargetRows { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public decimal TotalCost => TotalGlassCost + TotalProductionCost;
    }

    public class OrderReportStep9CostRow
    {
        public string Section { get; set; } = "";
        public int SpecNumber { get; set; }
        public string SpecName { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Description { get; set; } = "";
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }
        public decimal SheetSqm { get; set; }
        public decimal Quantity { get; set; }
        public decimal PricePerSqm { get; set; }
        public string UnitLabel { get; set; } = "SQM";
        public decimal Amount { get; set; }
        public string SheetSource { get; set; } = "";

        public string SheetDisplay => SheetWidthMm > 0 && SheetHeightMm > 0
            ? $"{SheetWidthMm} × {SheetHeightMm} · {SheetSqm:0.####} SQM"
            : "—";
    }

    /// <summary>
    /// Persisted, report-generator projection of one canonical Step 9 target
        /// result. Canonical rates, targets, margins and status are copied from
        /// <c>SpecTargetSnapshot</c>; only the display-only BASE balance value
        /// is derived from its saved per-SQM balance and saved area. This class
        /// must not become a second target-price calculator.
    /// </summary>
    public class OrderReportStep9TargetRow
    {
        public int SpecNumber { get; set; }
        public string SpecName { get; set; } = "";
        public string GlassTypeCode { get; set; } = "";
        public string BuildType { get; set; } = "";
        public decimal AreaSqm { get; set; }

        // ── Step 5 quote (not the normal formula base) ───────────────────
        public decimal GivenPricePerSqm { get; set; }
        public decimal GivenValue { get; set; }

        // ── Formula inputs / normal pre-profit base ──────────────────────
        public decimal BasePricePerSqm { get; set; }
        public decimal OuterGlassRatePerSqm { get; set; }
        public decimal InnerGlassRatePerSqm { get; set; }
        public decimal ThirdGlassRatePerSqm { get; set; }
        public decimal AspRatePerSqm { get; set; }
        public decimal ProcessingRatePerSqm { get; set; }
        public decimal YieldFactor { get; set; }
        public decimal TargetUpliftPercent { get; set; }
        public decimal SpecificationTargetPerSqm { get; set; }

        // ── U-insert / given judged for the two margins ──────────────────
        public string UInsertTreatment { get; set; } = "";
        public decimal UInsertRatePerSqm { get; set; }
        public string UInsertSourceLabel { get; set; } = "";
        public decimal AppliedUInsertPerSqm { get; set; }
        public decimal GivenPriceJudgedPerSqm { get; set; }

        // ── BASE and TARGET result values ────────────────────────────────
        public decimal? ExtraProfitPerSqm { get; set; }
        public decimal? BaseDifferenceValue { get; set; }
        public decimal? ExtraProfitMarginPercent { get; set; }
        public decimal FinalRequiredTargetPerSqm { get; set; }
        public decimal DifferencePerSqm { get; set; }
        public decimal RequiredTargetValue { get; set; }
        public decimal DifferenceValue { get; set; }
        public decimal? MarginAgainstTargetPercent { get; set; }
        public string Status { get; set; } = "";
        public string FormulaText { get; set; } = "";
        public List<string> Warnings { get; set; } = new();

        [JsonIgnore]
        public bool HasBaseResult => ExtraProfitPerSqm.HasValue
                                      && ExtraProfitMarginPercent.HasValue;
        [JsonIgnore]
        public bool HasTargetResult => MarginAgainstTargetPercent.HasValue;

        /// <summary>Input audit text; values are from the saved Step 9 result.</summary>
        [JsonIgnore]
        public string FormulaInputsSummary
        {
            get
            {
                var parts = new List<string>();
                if (IsBuild("Sgu"))
                {
                    parts.Add($"Glass {FormulaRate(OuterGlassRatePerSqm)}");
                }
                else
                {
                    parts.Add($"Outer {FormulaRate(OuterGlassRatePerSqm)}");
                    if (IsBuild("Dgu", "Lam", "DguLam") || InnerGlassRatePerSqm != 0m)
                        parts.Add($"Inner {FormulaRate(InnerGlassRatePerSqm)}");
                    if (IsBuild("DguLam") || ThirdGlassRatePerSqm != 0m)
                        parts.Add($"Third {FormulaRate(ThirdGlassRatePerSqm)}");
                }

                if (IsBuild("Dgu", "DguLam") || AspRatePerSqm != 0m)
                    parts.Add($"ASP {FormulaRate(AspRatePerSqm)}");
                if (ProcessingRatePerSqm != 0m)
                    parts.Add($"Processing {FormulaRate(ProcessingRatePerSqm)}");

                parts.Add(YieldFactor > 0m
                    ? $"Yield {Compact(YieldFactor)}"
                    : "Yield missing");
                parts.Add(HasSavedTargetDecision
                    ? $"Profit uplift {Compact(TargetUpliftPercent)}%"
                    : "Profit uplift unavailable in saved report");
                return string.Join(" · ", parts);
            }
        }

        /// <summary>
        /// Formula text for the normal pre-profit base. It does not use the
        /// Step 5 quote and does not calculate a replacement price.
        /// </summary>
        [JsonIgnore]
        public string NormalFormulaBaseExpression
        {
            get
            {
                string panes = IsBuild("Sgu")
                    ? Number(OuterGlassRatePerSqm)
                    : IsBuild("Dgu", "Lam")
                        ? $"({Number(OuterGlassRatePerSqm)} + {Number(InnerGlassRatePerSqm)})"
                        : $"({Number(OuterGlassRatePerSqm)} + {Number(InnerGlassRatePerSqm)} + {Number(ThirdGlassRatePerSqm)})";

                var terms = new List<string>
                {
                    YieldFactor > 0m
                        ? $"{panes} ÷ {Compact(YieldFactor)}"
                        : $"{panes} ÷ missing yield"
                };
                if (IsBuild("Dgu", "DguLam") || AspRatePerSqm != 0m)
                    terms.Add(Number(AspRatePerSqm));
                if (ProcessingRatePerSqm != 0m)
                    terms.Add(Number(ProcessingRatePerSqm));
                return string.Join(" + ", terms);
            }
        }

        [JsonIgnore]
        public string NormalFormulaBaseSummary => BasePricePerSqm > 0m
            ? $"{NormalFormulaBaseExpression} = {Rate(BasePricePerSqm)} (normal formula base; no profit)"
            : $"Normal formula base unavailable · {NormalFormulaBaseExpression}";

        [JsonIgnore]
        public string Step5GivenSummary
        {
            get
            {
                if (GivenPricePerSqm <= 0m) return "— (Step 5 quote missing)";
                string value = $"{Rate(GivenPricePerSqm)} · Step 5 value {GivenValueText}";
                return HasBaseResult
                    ? $"{value} · judged {Rate(GivenPriceJudgedPerSqm)}"
                    : value;
            }
        }

        [JsonIgnore]
        public string BaseResultSummary => !HasBaseResult
            ? "— (normal formula base or Step 5 quote incomplete)"
            : $"Balance {SignedRate(ExtraProfitPerSqm)} · value {SignedMoney(BaseDifferenceValue)} · margin {SignedPercent(ExtraProfitMarginPercent)}";

        [JsonIgnore]
        public string RequiredTargetSummary
        {
            get
            {
                if (FinalRequiredTargetPerSqm <= 0m) return "— (target formula incomplete)";
                decimal upliftFactor = 1m + TargetUpliftPercent / 100m;
                string calculation = BasePricePerSqm > 0m && HasSavedTargetDecision
                    ? $"Normal formula base {Rate(BasePricePerSqm)} × {Compact(upliftFactor)} = required target {Rate(FinalRequiredTargetPerSqm)}"
                    : $"Required target {Rate(FinalRequiredTargetPerSqm)}";
                return $"{calculation} · uplift {TargetUpliftText} · value {RequiredTargetValueText}";
            }
        }

        [JsonIgnore]
        public string TargetResultSummary => !HasTargetResult
            ? $"— · {SafeStatus}"
            : $"Quoted balance {SignedRate(DifferencePerSqm)} · value {DifferenceValueText} · margin {SignedPercent(MarginAgainstTargetPercent)} · {SafeStatus}";

        [JsonIgnore]
        public string UInsertSummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UInsertTreatment)) return "Not applicable";
                string rate = UInsertRatePerSqm > 0m ? $"; rate {Rate(UInsertRatePerSqm)}" : string.Empty;
                string source = string.IsNullOrWhiteSpace(UInsertSourceLabel)
                    ? string.Empty
                    : "; source " + UInsertSourceLabel;
                return UInsertTreatment + rate + source;
            }
        }

        [JsonIgnore]
        public string AuditSummary
        {
            get
            {
                string warning = Warnings == null || Warnings.Count == 0
                    ? string.Empty
                    : " Warning: " + string.Join(" | ", Warnings.Where(w => !string.IsNullOrWhiteSpace(w)));
                string formula = string.IsNullOrWhiteSpace(FormulaText) ? "Formula unavailable" : FormulaText;
                return $"{SafeStatus}. Formula: {formula}. U-insert: {UInsertSummary}; " +
                       "required target treatment: U-insert is not added to the normal specification target." + warning;
            }
        }

        private string SafeStatus => string.IsNullOrWhiteSpace(Status)
            ? "CALCULATION INCOMPLETE"
            : Status;

        // These fields were added after some Order Report snapshots had
        // already been saved. A zero uplift is valid (it means no requested
        // profit), so detect the presence of the additive decision payload
        // rather than treating every zero as an old-snapshot omission.
        private bool HasSavedTargetDecision => YieldFactor > 0m
            || SpecificationTargetPerSqm > 0m
            || GivenValue != 0m
            || RequiredTargetValue != 0m
            || !string.IsNullOrWhiteSpace(UInsertTreatment)
            || !string.IsNullOrWhiteSpace(UInsertSourceLabel)
            || UInsertRatePerSqm != 0m
            || AppliedUInsertPerSqm != 0m
            || GivenPriceJudgedPerSqm != 0m;

        // Older saved report-generator snapshots predate these additive
        // value fields. Do not turn their default zero into a fake money
        // result; show that a fresh Step 9/report generation is required.
        private bool HasSavedGivenValue => HasSavedTargetDecision
            || !(AreaSqm > 0m && GivenPricePerSqm > 0m && GivenValue == 0m);
        private bool HasSavedRequiredTargetValue => HasSavedTargetDecision
            || !(AreaSqm > 0m && FinalRequiredTargetPerSqm > 0m && RequiredTargetValue == 0m);
        [JsonIgnore]
        public decimal? GivenValueForExport => HasSavedGivenValue ? GivenValue : null;
        [JsonIgnore]
        public decimal? RequiredTargetValueForExport => HasSavedRequiredTargetValue ? RequiredTargetValue : null;
        [JsonIgnore]
        public decimal? DifferenceValueForExport => HasSavedRequiredTargetValue ? DifferenceValue : null;

        private string GivenValueText => HasSavedGivenValue ? Money(GivenValue) : "— (not saved)";
        private string RequiredTargetValueText => HasSavedRequiredTargetValue ? Money(RequiredTargetValue) : "— (not saved)";
        private string DifferenceValueText => HasSavedRequiredTargetValue ? SignedMoney(DifferenceValue) : "— (not saved)";
        private string TargetUpliftText => HasSavedTargetDecision
            ? Compact(TargetUpliftPercent) + "%"
            : "— (not saved)";

        private bool IsBuild(params string[] expected) =>
            expected.Any(value => string.Equals(BuildType, value, StringComparison.OrdinalIgnoreCase));

        private static string Rate(decimal value) => $"AED {Number(value)}/SQM";
        private static string FormulaRate(decimal value) => $"AED {Number(value)}/SQM";
        private static string Money(decimal value) => $"AED {Number(value)}";
        private static string SignedRate(decimal? value) => !value.HasValue
            ? "—"
            : value.Value < 0m
                ? $"-AED {Number(Math.Abs(value.Value))}/SQM"
                : $"AED {Number(value.Value)}/SQM";
        private static string SignedMoney(decimal? value) => !value.HasValue
            ? "—"
            : value.Value < 0m
                ? $"-AED {Number(Math.Abs(value.Value))}"
                : $"AED {Number(value.Value)}";
        private static string SignedPercent(decimal? value) => !value.HasValue
            ? "—"
            : value.Value.ToString("+0.##;-0.##;0.##", CultureInfo.InvariantCulture) + "%";
        private static string Number(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
        private static string Compact(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  DELIVERY NOTES
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportDeliveryNote
    {
        public string DeliveryNoteId { get; set; } = "";
        public int SequenceNumber { get; set; } = 1;
        public string Status { get; set; } = "Draft";
        public string DeliveryType { get; set; } = "Original";
        public bool IsFoc { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string DeliveryTime { get; set; } = "";
        public string DriverName { get; set; } = "";
        public string VehicleNumber { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string Notes { get; set; } = "";

        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        /// <summary>Sum of item TotalWeight (delivered kg) on this DN.</summary>
        public decimal TotalWeightKg { get; set; }

        /// <summary>Real attachment count for this DN (from repository).</summary>
        public int AttachmentCount { get; set; }
        public bool HasAttachments => AttachmentCount > 0;

        public List<OrderReportDeliveryNoteItem> Items { get; set; } = new();

        public string DeliveryDateDisplay =>
            DeliveryDate == default ? "—" : DeliveryDate.ToString("dd MMM yyyy");
        public string DeliveredWhenDisplay =>
            DeliveryDate == default
                ? "—"
                : $"{DeliveryDate:dd MMM yyyy}" +
                  (string.IsNullOrWhiteSpace(DeliveryTime) ? "" : $"  ·  {DeliveryTime}");
    }

    public class OrderReportDeliveryNoteItem
    {
        public int SpecGroupNumber { get; set; }
        public string SpecGroupName { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        public int OrderedQty { get; set; }
        public int PreviouslyDeliveredQty { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }

        public decimal WeightPerPiece { get; set; }
        public decimal TotalWeight => WeightPerPiece * DeliveredQty;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  TAX INVOICES
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportTaxInvoice
    {
        public string InvoiceNumber { get; set; } = "";
        public string InvoiceType { get; set; } = "Full";   // Full | Partial
        public int SequenceNumber { get; set; } = 1;
        public string Status { get; set; } = "Draft";
        public string DeliveryNoteReference { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }

        public int AttachmentCount { get; set; }
        public bool HasAttachments => AttachmentCount > 0;

        public List<OrderReportTaxInvoiceItem> Items { get; set; } = new();

        public string IssueDateDisplay =>
            IssueDate == default ? "—" : IssueDate.ToString("dd MMM yyyy");
        public string DueDateDisplay =>
            DueDate == default ? "—" : DueDate.ToString("dd MMM yyyy");
    }

    public class OrderReportTaxInvoiceItem
    {
        public int SpecGroupNumber { get; set; }
        public string SpecGroupName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int Qty { get; set; }
        public decimal Sqm { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal Value { get; set; }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ATTACHMENT COMPLETENESS — the user's headline scenario
    //  ("3 delivery notes prepared, only 2 attached → 1 missing")
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportAttachmentCompleteness
    {
        /// <summary>Documents attached to the Proforma Invoice (Step 8 manifest).</summary>
        public int ProformaAttachmentCount { get; set; }
        public bool HasProformaAttachments => ProformaAttachmentCount > 0;

        /// <summary>Confirmed (non-master, non-dummy) delivery notes prepared.</summary>
        public int DeliveryNotesPrepared { get; set; }

        /// <summary>Of the prepared DNs, how many have ≥ 1 attachment.</summary>
        public int DeliveryNotesAttached { get; set; }

        /// <summary>DeliveryNotesPrepared − DeliveryNotesAttached.</summary>
        public int DeliveryNotesMissing =>
            Math.Max(0, DeliveryNotesPrepared - DeliveryNotesAttached);

        /// <summary>Delivery-note ids that have zero attachments.</summary>
        public List<string> MissingDeliveryNoteIds { get; set; } = new();

        /// <summary>Total attachments across all Tax Invoices in the family.</summary>
        public int TaxInvoiceAttachmentCount { get; set; }

        public bool HasMissingDocuments => DeliveryNotesMissing > 0;

        /// <summary>
        /// Example: "1 documents missing in attachments of delivery note DN-…".
        /// Pluralized like the user's wording.
        /// </summary>
        public string MissingDocumentsDisplay
        {
            get
            {
                if (DeliveryNotesMissing <= 0) return "";
                if (MissingDeliveryNoteIds.Count == 0)
                    return $"{DeliveryNotesMissing} documents missing in attachments of delivery note";
                return $"{DeliveryNotesMissing} documents missing in attachments of delivery note " +
                       string.Join(", ", MissingDeliveryNoteIds);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  TOTALS
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportTotals
    {
        public int OrderedQty { get; set; }
        public decimal OrderedSqm { get; set; }
        public decimal OrderedValue { get; set; }
        public decimal OrderedWeightKg { get; set; }

        public int DeliveredQty { get; set; }
        public decimal DeliveredSqm { get; set; }
        public decimal DeliveredWeightKg { get; set; }

        public int BalanceQty { get; set; }
        public decimal BalanceSqm { get; set; }
        public decimal BalanceWeightKg { get; set; }

        public int InvoicedQty { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal InvoicedVat { get; set; }

        /// <summary>InvoicedQty − DeliveredQty (positive = over-invoiced).</summary>
        public int InvoicedMinusDeliveredQty => InvoicedQty - DeliveredQty;

        public decimal DeliveryPct =>
            OrderedQty <= 0 ? 0 : Math.Round(DeliveredQty / (decimal)OrderedQty * 100, 1);
        public decimal InvoicePct =>
            OrderedQty <= 0 ? 0 : Math.Round(InvoicedQty / (decimal)OrderedQty * 100, 1);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  WARNING (plain, JSON-safe)
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportWarning
    {
        /// <summary>Error | Warning | Info (matches LogSeverity names).</summary>
        public string Severity { get; set; } = "Info";

        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";

        /// <summary>Action kind name (OpenProforma / OpenJobOrder / OpenInvoice / OpenPdf).</summary>
        public string ActionKind { get; set; } = "";

        /// <summary>Document reference for the action (number / id).</summary>
        public string TargetId { get; set; } = "";
    }
}
