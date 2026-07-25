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
    ///         to DateTime.Now at property level. Mapper sets them
    ///         explicitly to avoid silent "today's date" on empty snapshots.
    /// BUG-02: TermClauseSnapshot.Type stores enum name as string. This
    ///         is INTENTIONAL for JSON stability — enum reordering will
    ///         not break saved invoices. Rename enum values with care.
    /// BUG-03: PolishSubType documented — valid values match the
    ///         PolishSubType enum in Step6OtherCharges.
    /// </summary>
    public class ProformaInvoiceSnapshot
    {
        public int SchemaVersion { get; set; } = 1;

        // Step 1: Company
        public CompanySnapshot Company { get; set; } = new();

        // Step 2: Project
        public ProjectSnapshot Project { get; set; } = new();

        // Step 3: Client
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

        /// <summary>
        /// BUG-01: Changed default from DateTime.Now to default(DateTime).
        /// Mapper sets this explicitly — no silent today's-date fallback.
        /// </summary>
        public DateTime IssueDate { get; set; } = default;

        /// <summary>
        /// BUG-01: Changed default from DateTime.Now.AddDays(7) to default.
        /// Mapper sets this explicitly based on Company VM.
        /// </summary>
        public DateTime ValidUntil { get; set; } = default;

        public string ProformaStatus { get; set; } = "Draft";
    }

    // ── Step 2 ──────────────────────────────────────────────────────────

    public class ProjectSnapshot
    {
        public string ProjectIdentifierName { get; set; } = "";
        public string ProjectContractNo { get; set; } = "";
        public string SiteDeliveryLocation { get; set; } = "";
        public string LpoNumber { get; set; } = "";
    }

    // ── Step 3 ──────────────────────────────────────────────────────────

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

    public class PredefinedChargeSnapshot
    {
        public string TemplateKey { get; set; } = "";
        public bool IsActive { get; set; }
        public string SelectedSourceKey { get; set; } = "";
        public decimal Rate { get; set; }

        // ── Polish-specific ──
        /// <summary>
        /// BUG-03: Stores PolishSubType enum name as string.
        /// Valid values: "AllSidePolish", "AllSidePolishAsPerDwg",
        ///               "BothHeightSide", "BothWidthSide", "Custom".
        /// Empty string = no sub-type selected.
        /// </summary>
        public string PolishSubType { get; set; } = "";
        public string CustomPolishDescription { get; set; } = "";
        public decimal CustomPolishQty { get; set; }
    }

    public class WastageRowSnapshot
    {
        public bool IsActive { get; set; }
        public string ColorName { get; set; } = "";
        public decimal Rate { get; set; }

        /// <summary>
        /// Selected source keys formatted as "SpecNumber-SideLabel"
        /// (e.g. "1-Outer (W1/H1)").
        /// </summary>
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
        /// BUG-02: Stores TermClauseType enum name as string for JSON
        /// stability. Valid values: "PaymentTerms", "Delivery", "Warranty",
        /// "Validity", "Taxes", "Installation", "Custom".
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