using System.Collections.Generic;

namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Models dimension-specific sales analytics grouped by customer.
    /// </summary>
    public class CustomerSalesReport
    {
        public string CustomerName { get; set; } = "";
        public decimal TotalSalesAed { get; set; }
        public decimal TotalVatAed { get; set; }
        public decimal TotalOutstandingAed { get; set; }

        public List<SalesReport> Invoices { get; set; } = new();
    }
}