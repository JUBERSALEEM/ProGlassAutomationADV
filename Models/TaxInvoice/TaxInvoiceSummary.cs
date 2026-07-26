using System;

namespace ProGlassAutomation.Models.TaxInvoice
{
    /// <summary>
    /// Pure POCO representing the financial totals summary of a Tax Invoice.
    /// Can be embedded in snapshot or used for binding/transfer.
    /// </summary>
    public class TaxInvoiceSummary
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Currency { get; set; } = "AED";
        public decimal ExchangeRate { get; set; } = 1.0m;
        public decimal VatPercent { get; set; } = 5.0m;
    }
}