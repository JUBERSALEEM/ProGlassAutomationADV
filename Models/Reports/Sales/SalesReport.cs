using System;

namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Pure POCO representing a single row in the Sales Report dataset.
    /// Combines tax invoice records with project and salesman metadata.
    /// </summary>
    public class SalesReport
    {
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string JobOrderId { get; set; } = "";
        public string DeliveryNoteId { get; set; } = "";
        public string ProformaId { get; set; } = "";

        public int TotalQuantity { get; set; }
        public decimal TotalSqm { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Currency { get; set; } = "AED";
        public decimal ExchangeRate { get; set; } = 1.0m;

        public string PaymentStatus { get; set; } = "Unpaid";

        // Amount in Base Currency (AED) for unified reporting
        public decimal GrandTotalAed => GrandTotal * ExchangeRate;
        public decimal VatAmountAed => VatAmount * ExchangeRate;
        public decimal TaxableAmountAed => TaxableAmount * ExchangeRate;
    }
}