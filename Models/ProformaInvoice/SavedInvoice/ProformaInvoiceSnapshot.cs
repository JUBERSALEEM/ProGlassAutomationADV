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

using System;
using System.Collections.Generic;

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
        public int SchemaVersion { get; set; } = 1;

        // Step 1: Company
        public CompanySnapshot Company { get; set; } = new();

        // Step 2 in snapshot = Project data (structure unchanged for JSON stability)
        public ProjectSnapshot Project { get; set; } = new();

        // Step 3 in snapshot = Client data (structure unchanged for JSON stability)
        public ClientSnapshot Client { get; set; } = new();

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
    }

    // ── Step 1 ──────────────────────────────────────────────────────────

    public class CompanySnapshot
    {
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CompanyLocation { get; set; } = "";
        public string ProformaId { get; set; } = "";

        /// <summary>BUG-01: default(DateTime). Mapper sets explicitly.</summary>
        public DateTime IssueDate { get; set; } = default;

        /// <summary>BUG-01: default(DateTime). Mapper sets explicitly.</summary>
        public DateTime ValidUntil { get; set; } = default;

        public string ProformaStatus { get; set; } = "Draft";
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
    }

    // ── Step 6: Other Charges ───────────────────────────────────────────

    public class OtherChargesSnapshot
    {
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
        public string PreparedByStatus { get; set; } = "APPROVED";
        public string TermsText { get; set; } = "";
        public string FooterDisclaimer { get; set; } = "";
    }
}