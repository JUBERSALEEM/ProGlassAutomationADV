using ProGlassAutomation.Models.Reports.Sales;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Reports.Mapper
{
    public static class SalesReportMapper
    {
        public static List<CustomerSalesReport> GroupByCustomer(List<SalesReport> records)
        {
            return records.GroupBy(r => r.CustomerName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new CustomerSalesReport
                {
                    CustomerName = g.Key,
                    TotalSalesAed = g.Sum(r => r.GrandTotalAed),
                    TotalVatAed = g.Sum(r => r.VatAmountAed),
                    TotalOutstandingAed = g.Sum(r => r.PaymentStatus == "Paid" ? 0m : r.GrandTotalAed),
                    Invoices = g.OrderByDescending(r => r.InvoiceDate).ToList()
                })
                .OrderByDescending(c => c.TotalSalesAed)
                .ToList();
        }

        public static List<CompanySalesReport> GroupByCompany(List<SalesReport> records)
        {
            return records.GroupBy(r => r.CompanyName, StringComparer.OrdinalIgnoreCase)
                .Select(g => {
                    var projectsInCompany = GroupByProject(g.ToList());
                    return new CompanySalesReport
                    {
                        CompanyName = g.Key,
                        CompanyTrn = g.FirstOrDefault()?.InvoiceNumber ?? "", // dummy or placeholder TRN
                        TotalSalesAed = g.Sum(r => r.GrandTotalAed),
                        InvoiceCount = g.Count(),
                        ProjectCount = projectsInCompany.Count,
                        Projects = projectsInCompany
                    };
                })
                .OrderByDescending(c => c.TotalSalesAed)
                .ToList();
        }

        public static List<SalesmanSalesReport> GroupBySalesman(List<SalesReport> records)
        {
            return records.GroupBy(r => r.Salesman, StringComparer.OrdinalIgnoreCase)
                .Select(g => {
                    var list = g.ToList();
                    decimal totalSales = list.Sum(r => r.GrandTotalAed);
                    decimal outstanding = list.Sum(r => r.PaymentStatus == "Paid" ? 0m : r.GrandTotalAed);
                    int uniqueCust = list.Select(r => r.CustomerName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    int uniqueProj = list.Select(r => r.ProjectName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

                    // Dynamic Salesman Performance Summary
                    string summary = "Developing territory.";
                    if (totalSales > 200000m) summary = "Elite Tier Representative.";
                    else if (totalSales > 100000m) summary = "High Performance Performer.";
                    else if (totalSales > 50000m) summary = "Strong steady growth.";

                    return new SalesmanSalesReport
                    {
                        SalesmanName = g.Key,
                        TotalSalesAed = totalSales,
                        OutstandingPaymentsAed = outstanding,
                        UniqueCustomersCount = uniqueCust,
                        UniqueProjectsCount = uniqueProj,
                        InvoiceCount = list.Count,
                        PerformanceSummary = summary,
                        Invoices = list.OrderByDescending(r => r.InvoiceDate).ToList()
                    };
                })
                .OrderByDescending(s => s.TotalSalesAed)
                .ToList();
        }

        public static List<ProjectSalesReport> GroupByProject(List<SalesReport> records)
        {
            return records.GroupBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ProjectSalesReport
                {
                    ProjectName = g.Key,
                    CustomerName = g.FirstOrDefault()?.CustomerName ?? "General Customer",
                    CompanyName = g.FirstOrDefault()?.CompanyName ?? "ProGlass Automation",
                    TotalQuantity = g.Sum(r => r.TotalQuantity),
                    TotalSqm = g.Sum(r => r.TotalSqm),
                    TotalAmountAed = g.Sum(r => r.GrandTotalAed),
                    Invoices = g.OrderByDescending(r => r.InvoiceDate).ToList()
                })
                .OrderByDescending(p => p.TotalAmountAed)
                .ToList();
        }
    }
}