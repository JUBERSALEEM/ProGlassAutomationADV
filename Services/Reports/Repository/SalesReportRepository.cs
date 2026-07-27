using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reports.Repository
{
    public class SalesReportRepository : ISalesReportRepository
    {
        public async Task<List<SalesReport>> GetSalesReportAsync(SalesFilterModel? filter = null)
        {
            if (App.TaxInvoices == null) return new List<SalesReport>();

            // Fetch all tax invoices from database
            var invoices = await App.TaxInvoices.GetAllAsync();

            // Load all master companies to match company names with company TRNs
            var companyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (App.MasterData != null)
            {
                try
                {
                    var masterCompanies = await App.MasterData.GetAllCompaniesAsync();
                    foreach (var mc in masterCompanies)
                    {
                        if (!string.IsNullOrWhiteSpace(mc.CompanyTrn) && !companyMap.ContainsKey(mc.CompanyTrn))
                        {
                            companyMap[mc.CompanyTrn] = mc.CompanyName;
                        }
                    }
                }
                catch { /* Silent fallback */ }
            }

            var results = new List<SalesReport>();

            foreach (var inv in invoices)
            {
                // Retrieve friendly company name from map or default to standard
                string companyName = "ProGlass Automation";
                if (!string.IsNullOrWhiteSpace(inv.CompanyTrn) && companyMap.TryGetValue(inv.CompanyTrn, out var mappedName))
                {
                    companyName = mappedName;
                }

                // Gather total quantities and SQM from the snapshot
                int totalQty = 0;
                decimal totalSqm = 0m;
                if (inv.Items != null)
                {
                    totalQty = inv.Items.Sum(i => i.InvoicedQty);
                    // Standard panel size calculator
                    totalSqm = inv.Items.Sum(i => (decimal)(i.W1 * i.H1) / 1000000m * i.InvoicedQty);
                }

                var report = new SalesReport
                {
                    InvoiceNumber = inv.InvoiceNumber,
                    InvoiceDate = inv.InvoiceDate,
                    CustomerName = inv.CustomerName,
                    CompanyName = companyName,
                    Salesman = string.IsNullOrWhiteSpace(inv.Salesman) ? "Unassigned" : inv.Salesman,
                    ProjectName = string.IsNullOrWhiteSpace(inv.ProjectName) ? "General Project" : inv.ProjectName,
                    JobOrderId = inv.JobOrderId,
                    DeliveryNoteId = inv.DeliveryNoteId,
                    ProformaId = inv.ProformaId,
                    TotalQuantity = totalQty,
                    TotalSqm = totalSqm,
                    Subtotal = inv.Subtotal,
                    Discount = inv.Discount,
                    TaxableAmount = inv.TaxableAmount,
                    VatAmount = inv.VatAmount,
                    GrandTotal = inv.GrandTotal,
                    Currency = inv.Currency,
                    ExchangeRate = inv.ExchangeRate,
                    PaymentStatus = inv.PaymentStatus
                };

                // Filter Evaluation
                if (filter != null)
                {
                    if (filter.StartDate.HasValue && report.InvoiceDate.Date < filter.StartDate.Value.Date)
                        continue;
                    if (filter.EndDate.HasValue && report.InvoiceDate.Date > filter.EndDate.Value.Date)
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.CustomerName) && !string.Equals(filter.CustomerName, "All Customers", StringComparison.OrdinalIgnoreCase) && !string.Equals(report.CustomerName, filter.CustomerName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.CompanyName) && !string.Equals(filter.CompanyName, "All Companies", StringComparison.OrdinalIgnoreCase) && !string.Equals(report.CompanyName, filter.CompanyName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.Salesman) && !string.Equals(filter.Salesman, "All Salesmen", StringComparison.OrdinalIgnoreCase) && !string.Equals(report.Salesman, filter.Salesman, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.ProjectName) && !string.Equals(filter.ProjectName, "All Projects", StringComparison.OrdinalIgnoreCase) && !string.Equals(report.ProjectName, filter.ProjectName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber) && !report.InvoiceNumber.Contains(filter.InvoiceNumber, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.PaymentStatus) && !string.Equals(filter.PaymentStatus, "All Statuses", StringComparison.OrdinalIgnoreCase) && !string.Equals(report.PaymentStatus, filter.PaymentStatus, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                results.Add(report);
            }

            return results.OrderByDescending(r => r.InvoiceDate).ToList();
        }

        public async Task<List<string>> GetUniqueCustomersAsync()
        {
            if (App.TaxInvoices == null) return new List<string>();
            var all = await App.TaxInvoices.GetAllAsync();
            return all.Select(x => x.CustomerName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        public async Task<List<string>> GetUniqueProjectsAsync()
        {
            if (App.TaxInvoices == null) return new List<string>();
            var all = await App.TaxInvoices.GetAllAsync();
            return all.Select(x => x.ProjectName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        public async Task<List<string>> GetUniqueSalesmenAsync()
        {
            if (App.TaxInvoices == null) return new List<string>();
            var all = await App.TaxInvoices.GetAllAsync();
            return all.Select(x => x.Salesman)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        public async Task<List<string>> GetUniqueCompaniesAsync()
        {
            var result = new List<string>();
            if (App.MasterData != null)
            {
                try
                {
                    var companies = await App.MasterData.GetAllCompaniesAsync();
                    result.AddRange(companies.Select(c => c.CompanyName));
                }
                catch { }
            }
            if (result.Count == 0)
            {
                result.Add("ProGlass Automation");
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        }

        public async Task<(string ProformaId, string JobOrderId, string DeliveryNoteId, string InvoiceNumber)?> GetDocumentChainAsync(string invoiceNumber)
        {
            if (App.TaxInvoices == null) return null;
            var inv = await App.TaxInvoices.GetByInvoiceNumberAsync(invoiceNumber);
            if (inv == null) return null;

            return (inv.ProformaId, inv.JobOrderId, inv.DeliveryNoteId, inv.InvoiceNumber);
        }
    }
}