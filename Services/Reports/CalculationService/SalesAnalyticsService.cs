using ProGlassAutomation.Models.Reports.Sales;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Reports.CalculationService
{
    public class SalesAnalyticsService
    {
        /// <summary>
        /// Computes comprehensive KPI summary aggregates for the active dataset.
        /// </summary>
        public SalesSummary ComputeSummary(List<SalesReport> records)
        {
            var summary = new SalesSummary();
            if (records == null || records.Count == 0) return summary;

            summary.TotalInvoiceCount = records.Count;
            summary.TotalGrossSalesAed = records.Sum(r => r.Subtotal * r.ExchangeRate);
            summary.TotalDiscountAed = records.Sum(r => r.Discount * r.ExchangeRate);
            summary.TotalTaxableSalesAed = records.Sum(r => r.TaxableAmountAed);
            summary.TotalVatAed = records.Sum(r => r.VatAmountAed);
            summary.TotalNetSalesAed = records.Sum(r => r.GrandTotalAed);

            foreach (var r in records)
            {
                if (string.Equals(r.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                {
                    summary.PaidInvoiceCount++;
                    summary.TotalPaidAmountAed += r.GrandTotalAed;
                }
                else if (string.Equals(r.PaymentStatus, "Partially Paid", StringComparison.OrdinalIgnoreCase))
                {
                    summary.PartiallyPaidInvoiceCount++;
                    // Assume 50% for partial paid as database representation placeholder
                    decimal paidPart = r.GrandTotalAed * 0.5m;
                    summary.TotalPaidAmountAed += paidPart;
                    summary.TotalOutstandingAmountAed += (r.GrandTotalAed - paidPart);
                }
                else
                {
                    summary.UnpaidInvoiceCount++;
                    summary.TotalOutstandingAmountAed += r.GrandTotalAed;
                }
            }

            return summary;
        }

        /// <summary>
        /// Groups billing values by calendar month for the last 6 months.
        /// Returns a dictionary with keys like "Jan 2026", "Feb 2026" and values of sales amount.
        /// </summary>
        public Dictionary<string, decimal> GetMonthlySalesTrend(List<SalesReport> records)
        {
            var result = new Dictionary<string, decimal>();
            var today = DateTime.Today;

            // Initialize last 6 months in order
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = today.AddMonths(-i);
                string key = targetMonth.ToString("MMM yyyy");
                result[key] = 0m;
            }

            if (records == null) return result;

            foreach (var r in records)
            {
                string key = r.InvoiceDate.ToString("MMM yyyy");
                if (result.ContainsKey(key))
                {
                    result[key] += r.GrandTotalAed;
                }
            }

            return result;
        }
    }
}