// ═══════════════════════════════════════════════════════════════════════
//  Services/Statement/Workflow/StatementSnapshotProjector.cs
//
//  Pure, state-free helpers that project/reduce a StatementSnapshot. Kept
//  out of the (large) StatementGeneratorViewModel so that projection logic
//  lives in one place and is trivially unit-testable.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Models.Statement;

namespace ProGlassAutomation.Services.Statement.Workflow
{
    /// <summary>
    /// Pure projection helpers over <see cref="StatementSnapshot"/>.
    /// </summary>
    public static class StatementSnapshotProjector
    {
        /// <summary>
        /// Rebuilds a statement snapshot restricted to the given invoice
        /// numbers, recomputing the financial summary and the aging buckets
        /// over only the selected rows. Behaviour is identical to the former
        /// private helper on the generator VM.
        /// </summary>
        public static StatementSnapshot FilterByInvoices(
            StatementSnapshot original,
            List<string> selectedInvoiceNumbers)
        {
            var selectedSet = selectedInvoiceNumbers
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var filteredRows = original.InvoiceRows
                .Where(r => selectedSet.Contains(r.InvoiceNumber)).ToList();

            decimal overdue = filteredRows
                .Where(r => r.IsOverdue).Sum(r => r.Balance);

            var financial = new StatementFinancialSummary
            {
                TotalInvoiceCount = filteredRows.Count,
                TotalSubtotal = Math.Round(filteredRows.Sum(r => r.Subtotal), 2),
                TotalVat = Math.Round(filteredRows.Sum(r => r.VatAmount), 2),
                TotalInvoiced = Math.Round(filteredRows.Sum(r => r.GrandTotal), 2),
                TotalPaid = Math.Round(filteredRows.Sum(r => r.PaidAmount), 2),
                TotalOutstanding = Math.Round(filteredRows.Sum(r => r.Balance), 2),
                OverdueAmount = Math.Round(overdue, 2),
                TotalQty = filteredRows.Sum(r => r.TotalQty),
                TotalSqm = Math.Round(filteredRows.Sum(r => r.TotalSqm), 3)
            };

            var today = DateTime.Today;
            var aging = new StatementAgingBuckets();
            foreach (var row in filteredRows)
            {
                decimal balance = row.Balance;
                if (balance <= 0m) continue;

                if (row.DueDate == default || row.DueDate.Date >= today)
                {
                    aging.Current += balance;
                    continue;
                }

                int days = row.DaysOverdue;
                if (days <= 30) aging.Days0To30 += balance;
                else if (days <= 60) aging.Days31To60 += balance;
                else if (days <= 90) aging.Days61To90 += balance;
                else aging.Days90Plus += balance;
            }
            aging.Current = Math.Round(aging.Current, 2);
            aging.Days0To30 = Math.Round(aging.Days0To30, 2);
            aging.Days31To60 = Math.Round(aging.Days31To60, 2);
            aging.Days61To90 = Math.Round(aging.Days61To90, 2);
            aging.Days90Plus = Math.Round(aging.Days90Plus, 2);

            return new StatementSnapshot
            {
                Header = original.Header,
                CustomerInfo = original.CustomerInfo,
                Financial = financial,
                Aging = aging,
                InvoiceRows = filteredRows
            };
        }
    }
}
