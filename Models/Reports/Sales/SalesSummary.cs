namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Models aggregated sales statistics for the KPI dashboard or summary cards.
    /// </summary>
    public class SalesSummary
    {
        public decimal TotalGrossSalesAed { get; set; }
        public decimal TotalDiscountAed { get; set; }
        public decimal TotalTaxableSalesAed { get; set; }
        public decimal TotalVatAed { get; set; }
        public decimal TotalNetSalesAed { get; set; } // Grand Total (inclusive of VAT)

        public int TotalInvoiceCount { get; set; }
        public int PaidInvoiceCount { get; set; }
        public int UnpaidInvoiceCount { get; set; }
        public int PartiallyPaidInvoiceCount { get; set; }

        public decimal TotalPaidAmountAed { get; set; }
        public decimal TotalOutstandingAmountAed { get; set; }
    }
}