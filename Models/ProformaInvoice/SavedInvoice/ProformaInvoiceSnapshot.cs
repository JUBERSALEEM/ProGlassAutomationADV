using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Models.ProformaInvoice.SavedInvoice
{
    /// <summary>
    /// Pure POCO representation of the entire wizard state, ready for
    /// JSON serialization. Never contains WPF types or ObservableCollections.
    /// Populated by ProformaInvoiceMapper.ToSnapshot(),
    /// consumed by ProformaInvoiceMapper.RestoreFromSnapshot().
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
        public DateTime IssueDate { get; set; } = DateTime.Now;
        public DateTime ValidUntil { get; set; } = DateTime.Now.AddDays(7);
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

        // Polish-specific
        public string PolishSubType { get; set; } = "";
        public string CustomPolishDescription { get; set; } = "";
        public decimal CustomPolishQty { get; set; }
    }

    public class WastageRowSnapshot
    {
        public bool IsActive { get; set; }
        public string ColorName { get; set; } = "";
        public decimal Rate { get; set; }

        /// <summary>Selected source keys formatted as "SpecNumber-SideLabel" (e.g. "1-Outer (W1/H1)").</summary>
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
        public string DiscountType { get; set; } = "Percentage"; // "Percentage" or "FixedAmount"
        public decimal DiscountValue { get; set; }
        public bool IsVatApplicable { get; set; } = true;
        public decimal VatPercent { get; set; } = 5m;
    }

    // ── Phase 7.14: Terms & Conditions ──────────────────────────────────

    public class TermClauseSnapshot
    {
        public string Type { get; set; } = "Custom"; // stores enum name as string
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