using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;

namespace ProGlassAutomation.Services.ProformaInvoice.PdfExport
{
    /// <summary>
    /// Carries all data needed by the PDF service.
    /// Built from live wizard VMs by the caller — the PDF service
    /// never touches ViewModels or WPF types directly.
    /// </summary>
    public class PdfExportContext
    {
        // Step 1
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CompanyLocation { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string IssueDate { get; set; } = "";
        public string ValidUntil { get; set; } = "";
        public string ProformaStatus { get; set; } = "Draft";

        // Step 2
        public string ProjectName { get; set; } = "";
        public string ProjectContractNo { get; set; } = "";
        public string SiteDelivery { get; set; } = "";
        public string LpoNumber { get; set; } = "";

        // Step 3
        public string CustomerName { get; set; } = "";
        public string CustomerTrn { get; set; } = "";
        public string AttentionName { get; set; } = "";
        public string ContactPhone { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public string CustomerReference { get; set; } = "";
        public string SalesRepresentative { get; set; } = "";

        // Step 5: Spec Groups
        public System.Collections.Generic.List<PdfSpecGroup> SpecGroups { get; set; } = new();

        // Step 6: Other Charges
        public System.Collections.Generic.List<PdfChargeLine> OtherCharges { get; set; } = new();

        // Financial Summary
        public decimal GlassMaterialsTotal { get; set; }
        public decimal OtherChargesTotal { get; set; }
        public decimal NetSubtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountLabel { get; set; } = "";
        public bool IsVatApplicable { get; set; } = true;
        public decimal VatPercent { get; set; } = 5m;
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }

        // Terms & Conditions
        public System.Collections.Generic.List<PdfTermClause> TermClauses { get; set; } = new();

        // Footer
        public string FooterDisclaimer { get; set; } = "";
        public string TermsText { get; set; } = "";

        // Prepared By
        public string PreparedByName { get; set; } = "";
        public string PreparedByDept { get; set; } = "";
        public string PreparedByStatus { get; set; } = "";
    }

    public class PdfSpecGroup
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public System.Collections.Generic.List<PdfGlassRow> Rows { get; set; } = new();
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class PdfGlassRow
    {
        public int Serial { get; set; }
        public string GlassRef { get; set; } = "";
        public string SizeW1H1 { get; set; } = "";
        public string SizeW2H2 { get; set; } = "";
        public int Qty { get; set; }
        public decimal Area { get; set; }
        public decimal Rate { get; set; }
        public decimal Total { get; set; }
    }

    public class PdfChargeLine
    {
        public string Name { get; set; } = "";
        public string QtyLabel { get; set; } = "";
        public string RateLabel { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class PdfTermClause
    {
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public System.Collections.Generic.List<string> SubLines { get; set; } = new();
    }
}