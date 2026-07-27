using System;

namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Models filter parameters applied to sales reports.
    /// All filters are optional and operate as an intersection (AND).
    /// </summary>
    public class SalesFilterModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string? CustomerName { get; set; }
        public string? CompanyName { get; set; }
        public string? Salesman { get; set; }
        public string? ProjectName { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? PaymentStatus { get; set; }
    }
}