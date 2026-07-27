// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Aggregation/StatementAggregator.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIX vs PREVIOUS VERSION:
//   • Removed BuildStrongAsync (duplicate entry point)
//   • Removed ApplyIndexedFilters stub (returned empty list — caused
//     BuildAsync to ALWAYS return null — critical bug)
//   • Removed ApplySalesmanFilterAsync stub (dynamic, never called)
//   • Removed TaxInvoiceListItemInternal dead class
//   • BuildAsync now contains ALL real logic directly (no delegation)
//   • Single clean public entry point: BuildAsync()
//
// QUERY STRATEGY:
//   1. Load all TaxInvoiceListItems via GetAllAsync() (lightweight)
//   2. Apply indexed-column filters in memory
//   3. For salesman filter: parse FullSnapshotJson.PreparedBy.Name
//   4. Batch-load attachment counts (single query)
//   5. Per-invoice: load attachment type flags (SignedPI / SignedDN)
//   6. Build StatementInvoiceRows with DaysOverdue
//   7. Compute StatementFinancialSummary
//   8. Compute StatementAgingBuckets (DueDate basis — UAE AR standard)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Aggregation
{
    /// <summary>
    /// Builds a StatementSnapshot from cross-module data sources.
    /// Single public entry point: BuildAsync().
    /// Called by StatementGeneratorViewModel on a background thread.
    /// </summary>
    public class StatementAggregator
    {
        // ── JSON options ──────────────────────────────────────────────────
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT — single method, all real logic here
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a complete StatementSnapshot by applying the given filter
        /// across Tax Invoice data.
        /// Returns null if no invoices match the filter.
        /// Must be called from a background thread (Task.Run).
        /// </summary>
        public async Task<StatementSnapshot?> BuildAsync(
            StatementFilter filter,
            string statementNumber)
        {
            // ── Step 1: Load all invoices (lightweight list items) ─────────
            var allItems = await App.TaxInvoices.GetAllAsync();

            if (allItems == null || allItems.Count == 0)
                return null;

            // ── Step 2: Apply indexed-column filters in memory ────────────
            var filtered = ApplyFilters(allItems, filter);

            // ── Step 3: Salesman filter (requires snapshot JSON parse) ─────
            if (!string.IsNullOrWhiteSpace(filter.SalesmanName))
            {
                filtered = await ApplySalesmanFilterAsync(
                    filtered, filter.SalesmanName);
            }

            if (filtered.Count == 0)
                return null;

            // ── Step 4: Batch-load attachment counts ──────────────────────
            var invoiceNumbers = filtered
                .Select(i => i.InvoiceNumber)
                .ToList();

            var attachmentCounts = await App.TaxInvoices
                .GetAttachmentCountsAsync(invoiceNumbers);

            // ── Step 5: Load attachment type flags ────────────────────────
            var attachmentFlags = await LoadAttachmentFlagsAsync(
                invoiceNumbers);

            // ── Step 6: Build invoice rows ────────────────────────────────
            var today = DateTime.Today;
            var rows = BuildInvoiceRows(
                filtered, attachmentCounts, attachmentFlags, today);

            // ── Step 7: Financial summary ─────────────────────────────────
            var financial = BuildFinancialSummary(rows);

            // ── Step 8: Aging buckets ─────────────────────────────────────
            var aging = BuildAgingBuckets(rows, today);

            // ── Step 9: Customer info block ───────────────────────────────
            var customerInfo = BuildCustomerInfo(filtered, filter);

            // ── Step 10: Assemble snapshot ────────────────────────────────
            return new StatementSnapshot
            {
                Header = new StatementHeader
                {
                    StatementNumber = statementNumber,
                    Title = filter.GetStatementTitle(),
                    FilterSummary = filter.GetFilterSummary(),
                    GeneratedAt = DateTime.Now,
                    PeriodFrom = filter.DateFrom,
                    PeriodTo = filter.DateTo
                },
                CustomerInfo = customerInfo,
                Financial = financial,
                Aging = aging,
                InvoiceRows = rows
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  FILTER APPLICATION
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies all indexed-column filters in memory.
        /// No JSON parsing — uses TaxInvoiceListItem fields only.
        /// Confirmed field names from TaxInvoiceRepository.ToListItem().
        /// </summary>
        private static List<TaxInvoiceListItem> ApplyFilters(
            List<TaxInvoiceListItem> items,
            StatementFilter filter)
        {
            IEnumerable<TaxInvoiceListItem> q = items;

            // Company (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
            {
                string cn = filter.CompanyName.Trim().ToLower();
                q = q.Where(i =>
                    i.CompanyName.ToLower().Contains(cn));
            }

            // Customer (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                string cu = filter.CustomerName.Trim().ToLower();
                q = q.Where(i =>
                    i.CustomerName.ToLower().Contains(cu));
            }

            // Project (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(filter.ProjectName))
            {
                string pr = filter.ProjectName.Trim().ToLower();
                q = q.Where(i =>
                    i.ProjectName.ToLower().Contains(pr));
            }

            // Date from — IssueDate >= DateFrom
            if (filter.DateFrom.HasValue)
            {
                var df = filter.DateFrom.Value.Date;
                q = q.Where(i => i.IssueDate.Date >= df);
            }

            // Date to — IssueDate <= DateTo
            if (filter.DateTo.HasValue)
            {
                var dt = filter.DateTo.Value.Date;
                q = q.Where(i => i.IssueDate.Date <= dt);
            }

            // Status multi-select — empty list = all statuses included
            if (filter.Statuses.Count > 0)
            {
                var statuses = filter.Statuses
                    .Select(s => s.ToLower())
                    .ToHashSet();
                q = q.Where(i =>
                    statuses.Contains(i.Status.ToLower()));
            }

            // Sort oldest first for statement readability
            return q.OrderBy(i => i.IssueDate).ToList();
        }

        // ═════════════════════════════════════════════════════════════════
        //  SALESMAN FILTER
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads full snapshots to check PreparedBy.Name field.
        /// Only invoked when filter.SalesmanName is set.
        /// Processed in chunks of 10 to limit SQLite pressure.
        /// </summary>
        private static async Task<List<TaxInvoiceListItem>>
            ApplySalesmanFilterAsync(
                List<TaxInvoiceListItem> items,
                string salesmanName)
        {
            string lower = salesmanName.Trim().ToLower();
            var result = new List<TaxInvoiceListItem>();
            const int chunk = 10;

            for (int i = 0; i < items.Count; i += chunk)
            {
                var batch = items.Skip(i).Take(chunk).ToList();

                var tasks = batch.Select(async item =>
                {
                    try
                    {
                        var full = await App.TaxInvoices
                            .GetByInvoiceNumberAsync(item.InvoiceNumber);

                        if (full == null)
                            return (item, false);

                        // No snapshot → include (safe fallback)
                        if (string.IsNullOrWhiteSpace(full.FullSnapshotJson)
                            || full.FullSnapshotJson == "{}")
                            return (item, true);

                        // Parse PreparedBy.Name from snapshot JSON
                        using var doc = JsonDocument.Parse(
                            full.FullSnapshotJson,
                            new JsonDocumentOptions
                            {
                                AllowTrailingCommas = true
                            });

                        if (doc.RootElement.TryGetProperty(
                                "PreparedBy", out var pb) &&
                            pb.TryGetProperty("Name", out var nameEl))
                        {
                            string name = nameEl.GetString() ?? "";
                            return (item,
                                name.ToLower().Contains(lower));
                        }

                        return (item, false);
                    }
                    catch
                    {
                        // Parse error → include item (safe fallback)
                        return (item, true);
                    }
                });

                var chunkResults = await Task.WhenAll(tasks);
                result.AddRange(chunkResults
                    .Where(r => r.Item2)
                    .Select(r => r.Item1));
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  ATTACHMENT FLAGS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads HasSignedPi + HasSignedDn flags for each invoice number.
        /// Processed concurrently in chunks of 10.
        /// </summary>
        private static async Task<Dictionary<string, AttachmentFlags>>
            LoadAttachmentFlagsAsync(List<string> invoiceNumbers)
        {
            var result = new Dictionary<string, AttachmentFlags>();
            const int chunk = 10;

            for (int i = 0; i < invoiceNumbers.Count; i += chunk)
            {
                var batch = invoiceNumbers.Skip(i).Take(chunk).ToList();

                var tasks = batch.Select(async invNum =>
                {
                    try
                    {
                        var atts = await App.TaxInvoices
                            .GetAttachmentsAsync(invNum);

                        return (invNum, new AttachmentFlags
                        {
                            HasSignedPi = atts.Any(a =>
                                string.Equals(a.AttachmentType,
                                    "SignedPI",
                                    StringComparison.OrdinalIgnoreCase)),
                            HasSignedDn = atts.Any(a =>
                                string.Equals(a.AttachmentType,
                                    "SignedDN",
                                    StringComparison.OrdinalIgnoreCase))
                        });
                    }
                    catch
                    {
                        return (invNum, new AttachmentFlags());
                    }
                });

                var chunkResults = await Task.WhenAll(tasks);
                foreach (var (invNum, flags) in chunkResults)
                    result[invNum] = flags;
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  ROW BUILDER
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds StatementInvoiceRow list from filtered TaxInvoiceListItems.
        /// Computes DaysOverdue and PaidAmount per row.
        /// PaidAmount: Paid = GrandTotal (fully paid), all others = 0.
        /// </summary>
        private static List<StatementInvoiceRow> BuildInvoiceRows(
            List<TaxInvoiceListItem> items,
            Dictionary<string, int> attachmentCounts,
            Dictionary<string, AttachmentFlags> attachmentFlags,
            DateTime today)
        {
            var rows = new List<StatementInvoiceRow>(items.Count);

            foreach (var item in items)
            {
                // ── Days overdue ──────────────────────────────────────────
                bool isPaidOrClosed =
                    item.Status == "Paid" ||
                    item.Status == "Cancelled" ||
                    item.Status == "Void";

                int daysOverdue = 0;
                if (!isPaidOrClosed &&
                    item.DueDate != default &&
                    item.DueDate.Date < today)
                {
                    daysOverdue = (today - item.DueDate.Date).Days;
                }

                // ── Paid amount ───────────────────────────────────────────
                decimal paidAmount =
                    item.Status == "Paid" ? item.GrandTotal : 0m;

                // ── Attachment flags ──────────────────────────────────────
                attachmentCounts.TryGetValue(
                    item.InvoiceNumber, out int attCount);
                attachmentFlags.TryGetValue(
                    item.InvoiceNumber, out var flags);

                rows.Add(new StatementInvoiceRow
                {
                    InvoiceNumber = item.InvoiceNumber,
                    ProformaId = item.ProformaId,
                    DeliveryNoteRef = item.DeliveryNoteReference,
                    PurchaseOrderNumber = item.PurchaseOrderNumber,
                    InvoiceType = item.InvoiceType,
                    Status = item.Status,
                    IssueDate = item.IssueDate,
                    DueDate = item.DueDate,
                    TotalQty = item.TotalQty,
                    TotalSqm = item.TotalSqm,
                    Subtotal = item.Subtotal,
                    VatAmount = item.VatAmount,
                    GrandTotal = item.GrandTotal,
                    PaidAmount = paidAmount,
                    DaysOverdue = daysOverdue,
                    HasSignedPi = flags?.HasSignedPi ?? false,
                    HasSignedDn = flags?.HasSignedDn ?? false,
                    AttachmentCount = attCount
                });
            }

            return rows;
        }

        // ═════════════════════════════════════════════════════════════════
        //  FINANCIAL SUMMARY
        // ═════════════════════════════════════════════════════════════════

        private static StatementFinancialSummary BuildFinancialSummary(
            List<StatementInvoiceRow> rows)
        {
            decimal overdueAmt = rows
                .Where(r => r.IsOverdue)
                .Sum(r => r.Balance);

            return new StatementFinancialSummary
            {
                TotalInvoiceCount = rows.Count,
                TotalSubtotal = Math.Round(rows.Sum(r => r.Subtotal), 2),
                TotalVat = Math.Round(rows.Sum(r => r.VatAmount), 2),
                TotalInvoiced = Math.Round(rows.Sum(r => r.GrandTotal), 2),
                TotalPaid = Math.Round(rows.Sum(r => r.PaidAmount), 2),
                TotalOutstanding = Math.Round(rows.Sum(r => r.Balance), 2),
                OverdueAmount = Math.Round(overdueAmt, 2),
                TotalQty = rows.Sum(r => r.TotalQty),
                TotalSqm = Math.Round(rows.Sum(r => r.TotalSqm), 3)
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  AGING BUCKETS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Buckets outstanding balance by days past DueDate.
        /// Paid/Cancelled/Void rows have Balance = 0 → contribute nothing.
        /// </summary>
        private static StatementAgingBuckets BuildAgingBuckets(
            List<StatementInvoiceRow> rows,
            DateTime today)
        {
            var b = new StatementAgingBuckets();

            foreach (var row in rows)
            {
                decimal balance = row.Balance;
                if (balance <= 0m) continue;

                // Not yet due
                if (row.DueDate == default ||
                    row.DueDate.Date >= today)
                {
                    b.Current += balance;
                    continue;
                }

                // Past due — bucket by DaysOverdue
                int days = row.DaysOverdue;
                if (days <= 30) b.Days0To30 += balance;
                else if (days <= 60) b.Days31To60 += balance;
                else if (days <= 90) b.Days61To90 += balance;
                else b.Days90Plus += balance;
            }

            b.Current = Math.Round(b.Current, 2);
            b.Days0To30 = Math.Round(b.Days0To30, 2);
            b.Days31To60 = Math.Round(b.Days31To60, 2);
            b.Days61To90 = Math.Round(b.Days61To90, 2);
            b.Days90Plus = Math.Round(b.Days90Plus, 2);

            return b;
        }

        // ═════════════════════════════════════════════════════════════════
        //  CUSTOMER INFO
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds customer info block from filter + first matching invoice.
        /// Filter values take priority (user-specified).
        /// Falls back to first invoice row for unspecified fields.
        /// </summary>
        private static StatementCustomerInfo BuildCustomerInfo(
            List<TaxInvoiceListItem> filtered,
            StatementFilter filter)
        {
            var first = filtered.First();

            return new StatementCustomerInfo
            {
                CompanyName = !string.IsNullOrWhiteSpace(filter.CompanyName)
                                   ? filter.CompanyName
                                   : first.CompanyName,
                CustomerName = !string.IsNullOrWhiteSpace(filter.CustomerName)
                                   ? filter.CustomerName
                                   : first.CustomerName,
                ProjectName = !string.IsNullOrWhiteSpace(filter.ProjectName)
                                   ? filter.ProjectName
                                   : first.ProjectName,
                SalesmanName = filter.SalesmanName ?? "",
                ContactPerson = ""
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE TYPES
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attachment type presence flags for one invoice.
        /// Built from GetAttachmentsAsync result.
        /// </summary>
        private sealed class AttachmentFlags
        {
            public bool HasSignedPi { get; init; }
            public bool HasSignedDn { get; init; }
        }
    }
}