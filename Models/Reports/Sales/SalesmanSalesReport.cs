using System.Collections.Generic;

namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Models dimension-specific sales analytics grouped by Sales Representative.
    /// </summary>
    public class SalesmanSalesReport
    {
        public string SalesmanName { get; set; } = "";
        public decimal TotalSalesAed { get; set; }
        public decimal OutstandingPaymentsAed { get; set; }
        public int UniqueCustomersCount { get; set; }
        public int UniqueProjectsCount { get; set; }
        public int InvoiceCount { get; set; }

        public string PerformanceSummary { get; set; } = "";

        public List<SalesReport> Invoices { get; set; } = new();
    }
}