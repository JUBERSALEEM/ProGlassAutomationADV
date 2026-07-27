using System.Collections.Generic;

namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Models dimension-specific sales analytics grouped by invoicing company.
    /// </summary>
    public class CompanySalesReport
    {
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public decimal TotalSalesAed { get; set; }
        public int ProjectCount { get; set; }
        public int InvoiceCount { get; set; }

        public List<ProjectSalesReport> Projects { get; set; } = new();
    }
}