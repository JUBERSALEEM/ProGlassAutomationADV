// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs
// ═══════════════════════════════════════════════════════════════════════
// CHAT 20 — PER-SPEC TARGETING PERSISTENCE
//
//   PredefinedChargeSnapshot gains 2 new fields:
//     • SelectedMetricKey        — which metric radio was selected
//     • CheckedSpecGroupNumbers  — which specs were checked
//
//   Old JSON without these fields deserialize to defaults:
//     SelectedMetricKey        → "" (System.Text.Json string default)
//     CheckedSpecGroupNumbers  → null (mapper handles null gracefully)
//
//   Backward compatibility: 100% — old invoices load unchanged.
//
// PREVIOUS: CHAT 15 — PROJECT REFERENCE FIELD ADDED
//   ProjectSnapshot.ProjectReference field
//   All other snapshot classes preserved EXACTLY.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Models.ProformaInvoice.SavedInvoice
{
    /// <summary>
    /// Pure POCO representation of the entire wizard state, ready for
    /// JSON serialization. Never contains WPF types or ObservableCollections.
    /// Populated by ProformaInvoiceMapper.ToSnapshot(),
    /// consumed by ProformaInvoiceMapper.RestoreFromSnapshot().
    ///
    /// BUG-01: CompanySnapshot.IssueDate / ValidUntil no longer default
    ///         to DateTime.Now at property level.
    /// BUG-02: TermClauseSnapshot.Type stores enum name as string.
    /// BUG-03: PolishSubType documented.
    ///
    /// CHAT 15: ProjectSnapshot.ProjectReference added.
    /// CHAT 20: PredefinedChargeSnapshot per-spec targeting fields added.
    /// </summary>
    public class ProformaInvoiceSnapshot
    {
        public int SchemaVersion { get; set; } = 2;

        /// <summary>
        /// Furthest wizard step the operator reached before the last save.
        /// Restored on load so the step-completion indicators (and the
        /// operator's ability to jump directly to a previously visited
        /// step) survive an app close or a back-to-list navigation.
        /// Additive: old snapshots deserialise to 0 (step 1 only).
        /// </summary>
        public int FurthestStepReached { get; set; }

        // Step 1: Company
        public CompanySnapshot Company { get; set; } = new();

        // Step 2 in snapshot = Project data (structure unchanged for JSON stability)
        public ProjectSnapshot Project { get; set; } = new();

        // Step 3 in snapshot = Client data (structure unchanged for JSON stability)
        public ClientSnapshot Client { get; set; } = new();

        // Step 4: pricing estimator panel state
        public Step4PricingSnapshot Step4Pricing { get; set; } = new();
        public List<Step4PricingSnapshot> Step4PricingSections { get; set; } = new();
        public int Step4SelectedSectionNumber { get; set; } = 1;

        // Step 5: DataGrid spec groups
        public List<SpecGroupSnapshot> SpecGroups { get; set; } = new();

        // Step 6: Other Charges (predefined + wastages + custom)
        public OtherChargesSnapshot OtherCharges { get; set; } = new();

        // Step 7 / Phase 7.12: Financial summary settings
        public InvoiceSummarySnapshot Summary { get; set; } = new();

        // Phase 7.14: Terms & Conditions
        public List<TermClauseSnapshot> TermClauses { get; set; } = new();

        // Phase 7.13: Prepared By fields
        public PreparedBySnapshot PreparedBy { get; set; } = new();

        // ── PHASE A/B: Step 9 Profit & Loss ─────────────────────────────
        // ADDITIVE — invoices saved before this field existed deserialize
        // with the property initializer intact (System.Text.Json only
        // overwrites members present in the JSON), so every historical
        // snapshot still loads with an empty, harmless P&L block.
        public ProfitLossSnapshot ProfitLoss { get; set; } = new();
    }

    // ── Step 1 ──────────────────────────────────────────────────────────

    public class CompanySnapshot
    {
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CompanyLocation { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public int? InquiryId { get; set; }

        /// <summary>BUG-01: default(DateTime). Mapper sets explicitly.</summary>
        public DateTime IssueDate { get; set; } = default;

        /// <summary>BUG-01: default(DateTime). Mapper sets explicitly.</summary>
        public DateTime ValidUntil { get; set; } = default;

        public string ProformaStatus { get; set; } = "Draft";

        // v2 — richer company identity (Company Center module).
        // Additive: older invoice JSON deserializes with empty values.
        public string Representative { get; set; } = "";
        public string RepresentativeMobile { get; set; } = "";
        public string CompanyEmail { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string BankName { get; set; } = "";
        public string LogoPath { get; set; } = "";
        /// <summary>Id of the Company Center record this invoice uses.</summary>
        public int CompanyRecordId { get; set; }
    }

    // ── Project data ────────────────────────────────────────────────────

    /// <summary>
    /// CHAT 15: ProjectReference field added.
    /// Old JSON snapshots without this field deserialize to "" — safe.
    /// </summary>
    public class ProjectSnapshot
    {
        public string ProjectIdentifierName { get; set; } = "";
        public string ProjectContractNo { get; set; } = "";

        /// <summary>
        /// CHAT 15: NEW — Descriptive project reference.
        /// e.g. "HOUSE FOR IMAD HABIB" — full descriptive text.
        /// Separate from ProjectContractNo which is a short code.
        /// </summary>
        public string ProjectReference { get; set; } = "";

        public string SiteDeliveryLocation { get; set; } = "";
        public string LpoNumber { get; set; } = "";
    }

    // ── Client data ─────────────────────────────────────────────────────

    public class ClientSnapshot
    {
        public string CustomerName { get; set; } = "";
        public string CustomerTrn { get; set; } = "";
        public string CustomerReferenceCode { get; set; } = "";
        public string SalesRepresentative { get; set; } = "";
        public string DocumentPreparedBy { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public string AttentionExecutiveName { get; set; } = "";
        public string ClientContactPhone { get; set; } = "";
    }

    // ── Step 4: Pricing estimator ───────────────────────────────────────

    public class Step4PricingSnapshot
    {
        public string SelectedGlassTypeCode { get; set; } = "";
        public string GeneratedSpecName { get; set; } = "";
        public decimal EstimatedRatePerSqm { get; set; }
        public SguPricingPanelSnapshot Sgu { get; set; } = new();
        public DguPricingPanelSnapshot Dgu { get; set; } = new();
        public LamPricingPanelSnapshot Lam { get; set; } = new();
        public DguLamPricingPanelSnapshot DguLam { get; set; } = new();
    }

    public class SguPricingPanelSnapshot
    {
        public string SelectedThickness { get; set; } = "";
        public string GlassColor { get; set; } = "";
        public string SelectedTreatment { get; set; } = "";
        public decimal BasePricePerSqm { get; set; }
        public decimal CuttingCostPerSqm { get; set; }
        public decimal TemperingCostPerSqm { get; set; }
        public decimal YieldFactor { get; set; }
        public decimal TargetMarginPercent { get; set; }
        public string SelectedPolishOption { get; set; } = "";
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }
    }

    public class DguPricingPanelSnapshot
    {
        public string OuterThickness { get; set; } = "";
        public string OuterGlassColor { get; set; } = "";
        public string OuterTreatment { get; set; } = "";
        public decimal OuterBasePrice { get; set; }
        public int OuterSheetWidthMm { get; set; }
        public int OuterSheetHeightMm { get; set; }
        public string SpacerWidth { get; set; } = "";
        public string SpacerMaterial { get; set; } = "";
        public decimal SpacerPrice { get; set; }
        public string SpacerExtra { get; set; } = "";
        public decimal UInsertPricePerSqm { get; set; }
        public string PanelExtra { get; set; } = "";
        public string InnerThickness { get; set; } = "";
        public string InnerGlassColor { get; set; } = "";
        public string InnerTreatment { get; set; } = "";
        public decimal InnerBasePrice { get; set; }
        public int InnerSheetWidthMm { get; set; }
        public int InnerSheetHeightMm { get; set; }
        public decimal YieldFactor { get; set; }
        public decimal MarginProfitPercent { get; set; }
    }

    public class LamPricingPanelSnapshot
    {
        public string OuterThickness { get; set; } = "";
        public string OuterGlassColor { get; set; } = "";
        public string OuterTreatment { get; set; } = "";
        public decimal OuterBasePrice { get; set; }
        public int OuterSheetWidthMm { get; set; }
        public int OuterSheetHeightMm { get; set; }
        public string InnerThickness { get; set; } = "";
        public string InnerGlassColor { get; set; } = "";
        public string InnerTreatment { get; set; } = "";
        public decimal InnerBasePrice { get; set; }
        public int InnerSheetWidthMm { get; set; }
        public int InnerSheetHeightMm { get; set; }
        public string PvbThickness { get; set; } = "";
        public string PvbColor { get; set; } = "";
        public decimal PvbMaterialRate { get; set; }
        public string LaminationPolish { get; set; } = "";
        public decimal OuterCuttingCost { get; set; }
        public decimal InnerCuttingCost { get; set; }
        public decimal OuterTemperingCost { get; set; }
        public decimal InnerTemperingCost { get; set; }
        public decimal YieldFactor { get; set; }
        public decimal MarginProfitPercent { get; set; }
    }

    public class DguLamPricingPanelSnapshot
    {
        public string OuterThickness { get; set; } = "";
        public string OuterGlassColor { get; set; } = "";
        public string OuterTreatment { get; set; } = "";
        public decimal OuterBasePrice { get; set; }
        public string SpacerThickness { get; set; } = "";
        public decimal SpacerPrice { get; set; }
        public string SpacerExtra { get; set; } = "";
        public string LamOuterThickness { get; set; } = "";
        public string LamOuterColor { get; set; } = "";
        public string LamOuterTreatment { get; set; } = "";
        public decimal LamOuterBasePrice { get; set; }
        public string InterlayerType { get; set; } = "";
        public string InterlayerThickness { get; set; } = "";
        public string InterlayerColor { get; set; } = "";
        public decimal InterlayerBuyPrice { get; set; }
        public decimal InterlayerSellPrice { get; set; }
        public string LamInnerThickness { get; set; } = "";
        public string LamInnerColor { get; set; } = "";
        public string LamInnerTreatment { get; set; } = "";
        public decimal LamInnerBasePrice { get; set; }
        public string PanelExtra { get; set; } = "";
        public string EdgePolish { get; set; } = "";
        public decimal OuterCuttingCost { get; set; }
        public decimal OuterTemperingCost { get; set; }
        public decimal LamOuterCuttingCost { get; set; }
        public decimal LamOuterTemperingCost { get; set; }
        public decimal LamInnerCuttingCost { get; set; }
        public decimal LamInnerTemperingCost { get; set; }
        public decimal YieldFactor { get; set; }
        public decimal MarginProfitPercent { get; set; }
    }

    // ── Step 5: DataGrid spec groups ────────────────────────────────────

    public class SpecGroupSnapshot
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public string SelectedGlassType { get; set; } = "";
        public string SelectedSpecName { get; set; } = "";
        public string OuterGlassColor { get; set; } = "";
        public string InnerGlassColor { get; set; } = "";
        public decimal BaseRate { get; set; }
        public decimal SurchargePercent { get; set; }

        // ── PHASE G: stock sheet chosen in Step 4 ───────────────────────
        // ADDITIVE. Persisting the sheet means a saved invoice always
        // re-costs against the sheet it was QUOTED on, even months later
        // after Sheet Store sizes or prices have changed. Zero on invoices
        // saved before this field existed — Step 9 then falls back and
        // says so, rather than silently using a different sheet.
        public int OuterSheetWidthMm { get; set; }
        public int OuterSheetHeightMm { get; set; }
        public int InnerSheetWidthMm { get; set; }
        public int InnerSheetHeightMm { get; set; }
        public decimal OuterSheetPricePerSqm { get; set; }
        public decimal InnerSheetPricePerSqm { get; set; }

        /// <summary>
        /// Step 4 seeded U-Insert rate for DGU-type specs. Additive: older
        /// snapshots deserialize to 0 and Step 6 falls back to its template.
        /// </summary>
        public decimal UInsertPricePerSqm { get; set; }

        public List<GlassItemRowSnapshot> ItemRows { get; set; } = new();
    }

    public class GlassItemRowSnapshot
    {
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public int PerimeterFactor { get; set; }

        /// <summary>
        /// Unit panel area (W1 × H1). Computed — not persisted.
        /// Old snapshots without this field still bind correctly.
        /// </summary>
        [JsonIgnore]
        public decimal Sqm1 => SqmCalculator.CalculatePanel(W1, H1);

        /// <summary>
        /// Total area (unit SQM × Qty). Computed — not persisted.
        /// </summary>
        [JsonIgnore]
        public decimal TotalSqm => SqmCalculator.CalculateTotal(W1, H1, Qty);
    }

    // ── Step 6: Other Charges ───────────────────────────────────────────

    public class OtherChargesSnapshot
    {
        /// <summary>
        /// True = Step 6 advanced mode auto-detects charges from Step 4 /
        /// Step 5. False = manual mode.
        /// </summary>
        public bool UseAdvancedAutomation { get; set; } = true;

        public List<PredefinedChargeSnapshot> PredefinedCharges { get; set; } = new();
        public List<WastageRowSnapshot> Wastages { get; set; } = new();
        public List<CustomChargeSnapshot> CustomCharges { get; set; } = new();
    }

    /// <summary>
    /// Predefined charge snapshot.
    ///
    /// CHAT 20 additions for per-spec targeting persistence:
    ///   • SelectedMetricKey        — which metric radio was selected
    ///   • CheckedSpecGroupNumbers  — which specs were ticked
    ///
    /// Old JSON files without these fields will deserialize to:
    ///   • SelectedMetricKey       → "" (skip metric restore)
    ///   • CheckedSpecGroupNumbers → null (skip checkbox restore — defaults apply)
    /// </summary>
    public class PredefinedChargeSnapshot
    {
        public string TemplateKey { get; set; } = "";
        public bool IsActive { get; set; }

        /// <summary>
        /// Legacy field from pre-Chat 17 single-source system.
        /// Preserved for backward compatibility. New saves may leave empty.
        /// </summary>
        public string SelectedSourceKey { get; set; } = "";

        public decimal Rate { get; set; }

        /// <summary>
        /// BUG-03: Valid values: "AllSidePolish", "AllSidePolishAsPerDwg",
        ///                       "BothHeightSide", "BothWidthSide", "Custom".
        /// </summary>
        public string PolishSubType { get; set; } = "";
        public string CustomPolishDescription { get; set; } = "";
        public decimal CustomPolishQty { get; set; }

        /// <summary>
        /// CHAT 20: The metric key selected in the row's spec-target popup.
        /// Values match SpecTargetSelection.AvailableMetrics[].Key
        /// (e.g. "LM1", "SQM1", "QTY", "LM_COMBINED", etc.)
        /// Empty on legacy invoices — restore skips metric selection.
        /// </summary>
        public string SelectedMetricKey { get; set; } = "";

        /// <summary>
        /// CHAT 20: SpecGroupNumbers of specs that were CHECKED in the
        /// row's spec-target popup. Others were unchecked.
        /// Null on legacy invoices — restore skips checkbox state
        /// and TargetSelection retains its default all-checked state.
        /// </summary>
        public List<int> CheckedSpecGroupNumbers { get; set; } = new();
    }

    public class WastageRowSnapshot
    {
        public bool IsActive { get; set; }
        public string ColorName { get; set; } = "";
        public decimal Rate { get; set; }

        /// <summary>
        /// True when the wastage row derives its effective AED/SQM rate from
        /// the optimization formula instead of using the manual Rate field.
        /// </summary>
        public bool UseOptimizationFormula { get; set; }

        /// <summary>Glass price fed into the optimization formula.</summary>
        public decimal FormulaPricePerSqm { get; set; }

        /// <summary>Third-party optimization percentage, e.g. 70 means 70%.</summary>
        public decimal OptimizationPercent { get; set; }

        /// <summary>
        /// Sheet quantity from the wastage setup box. Step 9 reads this for
        /// the matching pane's QTY (outer AND inner) even when the wastage
        /// charge row itself is disabled. Old snapshots default to 1.
        /// </summary>
        public int SheetQuantity { get; set; } = 1;

        /// <summary>Format: "SpecNumber-SideLabel" (e.g. "1-Outer (W1/H1)").</summary>
        public List<string> CheckedSourceKeys { get; set; } = new();
    }

    public class CustomChargeSnapshot
    {
        public bool IsActive { get; set; }
        public string ChargeName { get; set; } = "";
        public decimal Qty { get; set; }
        public string Unit { get; set; } = "";
        public decimal Rate { get; set; }
    }

    // ── Phase 7.12: Invoice Summary ─────────────────────────────────────

    public class InvoiceSummarySnapshot
    {
        /// <summary>"Percentage" or "FixedAmount"</summary>
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public bool IsVatApplicable { get; set; } = true;
        public decimal VatPercent { get; set; } = 5m;
    }

    // ── Phase 7.14: Terms & Conditions ──────────────────────────────────

    public class TermClauseSnapshot
    {
        /// <summary>
        /// BUG-02: Valid values: "PaymentTerms", "Delivery", "Warranty",
        ///                       "Validity", "Taxes", "Installation", "Custom".
        /// </summary>
        public string Type { get; set; } = "Custom";

        public int Order { get; set; }
        public bool IsActive { get; set; } = true;
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public List<string> SubLines { get; set; } = new();
        public int ValidityDays { get; set; } = 7;
    }

    // ── Phase 7.13: Prepared By ─────────────────────────────────────────

    public class PreparedBySnapshot
    {
        public string PreparedByName { get; set; } = "";
        public string PreparedByDept { get; set; } = "";
        // Default empty so the printable VM falls back to the workflow
        // status from Company.ProformaStatus. A hard-coded APPROVED here
        // promoted old/blank snapshots into a fake confirmed state.
        public string PreparedByStatus { get; set; } = "";
        public string TermsText { get; set; } = "";
        public string FooterDisclaimer { get; set; } = "";
    }

    // ── PHASE A/B: Step 9 Profit & Loss ─────────────────────────────────

    /// <summary>
    /// Persisted result of the Step 9 Profit &amp; Loss calculation.
    /// Pure POCO, JSON friendly. Every field is a plain value so old and
    /// new snapshots round-trip without custom converters.
    /// </summary>
    public class ProfitLossSnapshot
    {
        /// <summary>Line items — one per glass pane / spacer / production charge.</summary>
        public List<ProfitLossRowSnapshot> Rows { get; set; } = new();

        /// <summary>Invoice grand total as quoted to the customer (VAT inclusive).</summary>
        public decimal GrossVatInclusive { get; set; }

        /// <summary>Revenue excluding VAT (gross ÷ 1.05).</summary>
        public decimal NetExVat { get; set; }

        /// <summary>Sum of the glass (sheet purchase) rows.</summary>
        public decimal TotalGlassCost { get; set; }

        /// <summary>Sum of the production rows (ASP spacer, cutting, tempering).</summary>
        public decimal TotalProductionCost { get; set; }

        /// <summary>NetExVat − TotalGlassCost − TotalProductionCost. Negative = loss.</summary>
        public decimal Profit { get; set; }

        /// <summary>Profit as a percentage of NetExVat, one decimal.</summary>
        public decimal MarginPercent { get; set; }

        /// <summary>
        /// Overall margin including U-insert cost:
        /// (Gross Revenue − Total Cost with U-insert) / Total Cost × 100.
        /// This is the REAL job profitability for decision-making.
        /// Defaults to 0 for snapshots saved before this field existed;
        /// consumers should treat 0 as "unknown" for old snapshots.
        /// </summary>
        public decimal OverallMarginPercent { get; set; }

        /// <summary>
        /// Net margin (accounting perspective):
        /// (Gross Revenue − Total Cost) / Gross Revenue × 100.
        /// Defaults to 0 for snapshots saved before this field existed.
        /// </summary>
        public decimal NetMarginPercent { get; set; }

        /// <summary>Wastage allowance used when sheet counts were computed.</summary>
        public decimal WastagePercent { get; set; }

        /// <summary>
        /// Which Sheet Store column priced the glass when this snapshot was
        /// taken: true = SELLING, false = PURCHASE.
        ///
        /// Recorded because the two produce materially different profit for
        /// the same job — around 20% versus 5% on a typical DGU — and the
        /// default changed from purchase to selling. Without this flag,
        /// invoices saved either side of that change would be indistinguishable
        /// and no one could tell which question a stored figure answered.
        ///
        /// Defaults to false so snapshots written before this field existed
        /// deserialise as purchase-costed, which is what they were.
        /// </summary>
        public bool CostAtSellingPrice { get; set; }
    }

    /// <summary>One line of the Profit &amp; Loss breakdown.</summary>
    public class ProfitLossRowSnapshot
    {
        /// <summary>"Glass", "Spacer" or "Production".</summary>
        public string Kind { get; set; } = "";

        /// <summary>Originating Step 5 spec group number.</summary>
        public int SpecNumber { get; set; }

        /// <summary>Human-readable line description.</summary>
        public string Description { get; set; } = "";

        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }

        /// <summary>Area of one stock sheet in SQM.</summary>
        public decimal SheetSqm { get; set; }

        /// <summary>Sheets consumed (glass) or units of the charge.</summary>
        public decimal Quantity { get; set; }

        /// <summary>Applied unit price.</summary>
        public decimal PricePerSqm { get; set; }

        /// <summary>Display unit for the price (SQM / LM / QTY / TRIP).</summary>
        public string UnitLabel { get; set; } = "SQM";

        /// <summary>Extended line cost in AED.</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// PHASE G — provenance of the sheet size used for this line
        /// ("chosen in Step 4" / "from Sheet Store" / default fallback).
        /// </summary>
        public string SheetSource { get; set; } = "";

        /// <summary>
        /// Full specification the row was costed against, so a reopened
        /// invoice still shows what was quoted rather than "SPEC 2".
        /// Additive — invoices saved before this field load with "".
        /// </summary>
        public string SpecName { get; set; } = "";
    }
}
