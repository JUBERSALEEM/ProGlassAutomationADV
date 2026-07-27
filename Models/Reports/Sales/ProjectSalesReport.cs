using System.Collections.Generic;

namespace ProGlassAutomation.Models.Reports.Sales
{
    /// <summary>
    /// Models dimension-specific sales analytics grouped by project identifier.
    /// </summary>
    public class ProjectSalesReport
    {
        public string ProjectName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CompanyName { get; set; } = "";

        public int TotalQuantity { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalAmountAed { get; set; }

        public List<SalesReport> Invoices { get; set; } = new();
    }
}