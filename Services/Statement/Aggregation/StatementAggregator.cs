// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Aggregation/StatementAggregator.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16 + Chat 17 fixes)
//
// CHAT 17 FIXES:
//   1. FILTER NULL SAFETY (SafeContains helper)
//      Root cause of "No invoices match" bug on Company/Customer/Project.
//      NULL fields no longer crash the LINQ Where clause.
//
//   2. SALESMAN FILTER MULTI-PATH SEARCH
//      Root cause of "No invoices match" when salesman selected.
//      Tax Invoice snapshot may store salesman at DIFFERENT paths:
//        Path 1: snapshot.PreparedBy.Name (if invoice creator set it)
//        Path 2: parent PI's Client.SalesRepresentative
//                (falls back to Proforma Invoice snapshot)
//      Both paths are searched — matches EITHER = include invoice.
//
//   3. NAMESPACE FIX
//      TaxInvoiceListItem lives in Models.TaxInvoice.SavedTaxInvoice.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        // ═════════════════════════════════════════════════════════════════

        public async Task<StatementSnapshot?> BuildAsync(
            StatementFilter filter,
            string statementNumber)
        {
            // ── Step 1: Load all invoices ─────────────────────────────────
            var allItems = await App.TaxInvoices.GetAllAsync();

            Debug.WriteLine($"[Aggregator] Loaded {allItems?.Count ?? 0} " +
                            "invoices from DB");

            if (allItems == null || allItems.Count == 0)
            {
                Debug.WriteLine("[Aggregator] DB is empty");
                return null;
            }

            // ── Step 2: Indexed-column filters ────────────────────────────
            var filtered = ApplyFilters(allItems, filter);

            Debug.WriteLine($"[Aggregator] After indexed filters: " +
                            $"{filtered.Count} invoices");

            // ── Step 2b: Project filter (JSON search — handles old + new field names) ──
            if (!string.IsNullOrWhiteSpace(filter.ProjectName))
            {
                filtered = await ApplyProjectFilterAsync(
                    filtered, filter.ProjectName);

                Debug.WriteLine($"[Aggregator] After project filter: " +
                                $"{filtered.Count} invoices");
            }



            // ── Step 3: Salesman filter (multi-path JSON search) ──────────
            if (!string.IsNullOrWhiteSpace(filter.SalesmanName))
            {
                filtered = await ApplySalesmanFilterAsync(
                    filtered, filter.SalesmanName);

                Debug.WriteLine($"[Aggregator] After salesman filter: " +
                                $"{filtered.Count} invoices");
            }

            if (filtered.Count == 0)
            {
                Debug.WriteLine("[Aggregator] No invoices matched filters");
                return null;
            }



            // ── Step 4: Attachment counts ─────────────────────────────────
            var invoiceNumbers = filtered
                .Select(i => i.InvoiceNumber ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            var attachmentCounts = await App.TaxInvoices
                .GetAttachmentCountsAsync(invoiceNumbers);

            // ── Step 5: Attachment type flags ─────────────────────────────
            var attachmentFlags = await LoadAttachmentFlagsAsync(
                invoiceNumbers);

            // ── Step 6-10: Build rows + financial + aging + assemble ──────
            var today = DateTime.Today;
            var rows = BuildInvoiceRows(
                filtered, attachmentCounts, attachmentFlags, today);
            var financial = BuildFinancialSummary(rows);
            var aging = BuildAgingBuckets(rows, today);
            var customerInfo = BuildCustomerInfo(filtered, filter);

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
        //  FILTER APPLICATION — NULL-SAFE
        // ═════════════════════════════════════════════════════════════════

        private static List<TaxInvoiceListItem> ApplyFilters(
            List<TaxInvoiceListItem> items,
            StatementFilter filter)
        {
            IEnumerable<TaxInvoiceListItem> q = items;

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
            {
                string needle = filter.CompanyName;
                q = q.Where(i => SafeContains(i.CompanyName, needle));
            }

            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                string needle = filter.CustomerName;
                q = q.Where(i => SafeContains(i.CustomerName, needle));
            }

            // CHAT 20: Project filter REMOVED from indexed filters.
            // Moved to Step 2b (post-filter JSON search) because:
            //   - Old TIs store ProjectIdentifierName in entity.ProjectName
            //   - New TIs store ProjectContractNo in entity.ProjectName
            //   - Dropdown shows ProjectContractNo from masterdata
            //   - The two values may be completely different strings
            //   - Only reliable match is searching the full snapshot JSON
            // See ApplyProjectFilterAsync below.

            if (filter.DateFrom.HasValue)
            {
                var df = filter.DateFrom.Value.Date;
                q = q.Where(i => i.IssueDate.Date >= df);
            }

            if (filter.DateTo.HasValue)
            {
                var dt = filter.DateTo.Value.Date;
                q = q.Where(i => i.IssueDate.Date <= dt);
            }

            if (filter.Statuses != null && filter.Statuses.Count > 0)
            {
                var statuses = filter.Statuses
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToHashSet();

                if (statuses.Count > 0)
                {
                    q = q.Where(i =>
                        statuses.Contains(
                            (i.Status ?? "").Trim().ToLowerInvariant()));
                }
            }

            return q.OrderBy(i => i.IssueDate).ToList();
        }

        private static bool SafeContains(string? haystack, string? needle)
        {
            if (string.IsNullOrWhiteSpace(needle)) return true;
            if (string.IsNullOrEmpty(haystack)) return false;

            return haystack
                .Trim()
                .Contains(needle.Trim(),
                          StringComparison.OrdinalIgnoreCase);
        }

        // ═════════════════════════════════════════════════════════════════
        //  PROJECT FILTER — JSON SEARCH (Chat 20)
        //
        //  Searches for project name across:
        //    1. Indexed entity.ProjectName column (fast — bidirectional)
        //    2. Full snapshot JSON (fallback — catches all field names:
        //       ProjectIdentifierName, ProjectContractNo, ProjectReference)
        //
        //  This handles the mismatch between old TIs (entity stores
        //  ProjectIdentifierName) and new TIs (entity stores ProjectContractNo)
        //  where the dropdown shows ProjectContractNo from masterdata.
        // ═════════════════════════════════════════════════════════════════

        private static async Task<List<TaxInvoiceListItem>>
    ApplyProjectFilterAsync(
        List<TaxInvoiceListItem> items,
        string projectName)
        {
            string needle = projectName.Trim();
            var result = new List<TaxInvoiceListItem>();

            foreach (var item in items)
            {
                // Fast path: check indexed column first (bidirectional)
                if (SafeContains(item.ProjectName, needle) ||
                    SafeContains(needle, item.ProjectName))
                {
                    result.Add(item);
                    continue;
                }

                // Slow path: search snapshot JSON for any project field match
                try
                {
                    // Use GetByIdAsync which we KNOW exists (used by LoadAsync)
                    var full = await App.TaxInvoices.GetByIdAsync(item.Id);

                    if (full != null &&
                        !string.IsNullOrWhiteSpace(full.FullSnapshotJson) &&
                        full.FullSnapshotJson != "{}")
                    {
                        if (full.FullSnapshotJson.Contains(
                                needle, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(item);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Aggregator] Project JSON search " +
                                    $"error on {item.InvoiceNumber}: {ex.Message}");
                    // On error, INCLUDE the item (don't silently exclude)
                    result.Add(item);
                }
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SALESMAN FILTER — MULTI-PATH SEARCH (Chat 17 FIX)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Searches for salesman name across MULTIPLE JSON paths in
        /// BOTH the Tax Invoice snapshot AND its parent Proforma Invoice.
        ///
        /// SEARCH PATHS (in order):
        ///   1. TaxInvoice.PreparedBy.Name         (invoice creator)
        ///   2. TaxInvoice.Customer.ContactPerson  (customer contact)
        ///   3. TaxInvoice.Bank.PaymentNotes       (fallback)
        ///   4. PI.Client.SalesRepresentative      (original salesman)
        ///   5. PI.PreparedBy.Name                 (PI preparer)
        ///
        /// If ANY path contains the salesman name → include the invoice.
        /// This handles legacy invoices where PreparedBy was empty but
        /// the salesman was set on the PI.
        /// </summary>
        private static async Task<List<TaxInvoiceListItem>>
            ApplySalesmanFilterAsync(
                List<TaxInvoiceListItem> items,
                string salesmanName)
        {
            string needle = salesmanName.Trim();
            var result = new List<TaxInvoiceListItem>();
            const int chunk = 10;

            for (int i = 0; i < items.Count; i += chunk)
            {
                var batch = items.Skip(i).Take(chunk).ToList();

                var tasks = batch.Select(async item =>
                {
                    try
                    {
                        // ── Path A: Search Tax Invoice snapshot ──────────
                        bool foundInInvoice = await SearchSalesmanInTaxInvoice(
                            item.InvoiceNumber, needle);

                        if (foundInInvoice)
                            return (item, true);

                        // ── Path B: Fallback to parent PI snapshot ───────
                        if (!string.IsNullOrWhiteSpace(item.ProformaId))
                        {
                            bool foundInPi = await SearchSalesmanInProformaInvoice(
                                item.ProformaId, needle);

                            if (foundInPi)
                                return (item, true);
                        }

                        return (item, false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Aggregator] Salesman search " +
                                        $"error on {item.InvoiceNumber}: " +
                                        $"{ex.Message}");
                        // Safe fallback: exclude on error to prevent
                        // false positives (senior ERP behavior)
                        return (item, false);
                    }
                });

                var chunkResults = await Task.WhenAll(tasks);
                result.AddRange(chunkResults
                    .Where(r => r.Item2)
                    .Select(r => r.Item1));
            }

            return result;
        }

        /// <summary>
        /// Searches Tax Invoice's own snapshot JSON for the salesman name.
        /// Checks: PreparedBy.Name, Customer.ContactPerson, Bank.PaymentNotes.
        /// Returns TRUE if found in ANY of these paths.
        /// </summary>
        private static async Task<bool> SearchSalesmanInTaxInvoice(
            string invoiceNumber,
            string needle)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return false;

            try
            {
                var full = await App.TaxInvoices
                    .GetByInvoiceNumberAsync(invoiceNumber);

                if (full == null) return false;

                if (string.IsNullOrWhiteSpace(full.FullSnapshotJson)
                    || full.FullSnapshotJson == "{}")
                    return false;

                using var doc = JsonDocument.Parse(
                    full.FullSnapshotJson,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true
                    });

                var root = doc.RootElement;

                // Path 1: PreparedBy.Name
                string? preparedBy = TryReadString(root, "PreparedBy", "Name");
                if (SafeContains(preparedBy, needle))
                {
                    Debug.WriteLine($"[Aggregator] Salesman match " +
                                    $"{invoiceNumber} → PreparedBy.Name");
                    return true;
                }

                // Path 2: Customer.ContactPerson
                string? contact = TryReadString(root, "Customer", "ContactPerson");
                if (SafeContains(contact, needle))
                {
                    Debug.WriteLine($"[Aggregator] Salesman match " +
                                    $"{invoiceNumber} → Customer.ContactPerson");
                    return true;
                }

                // Path 3: Bank.PaymentNotes (some invoices store here)
                string? notes = TryReadString(root, "Bank", "PaymentNotes");
                if (SafeContains(notes, needle))
                {
                    Debug.WriteLine($"[Aggregator] Salesman match " +
                                    $"{invoiceNumber} → Bank.PaymentNotes");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Aggregator] TaxInvoice JSON parse " +
                                $"error {invoiceNumber}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Searches parent Proforma Invoice snapshot for the salesman name.
        /// PI is the ORIGINAL source of salesman assignment — most invoices
        /// have this populated even if the Tax Invoice's PreparedBy is empty.
        /// Checks: Client.SalesRepresentative, PreparedBy.Name.
        /// </summary>
        private static async Task<bool> SearchSalesmanInProformaInvoice(
            string proformaId,
            string needle)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return false;

            try
            {
                var pi = await App.Invoices.GetByProformaIdAsync(proformaId);

                if (pi == null) return false;

                if (string.IsNullOrWhiteSpace(pi.FullSnapshotJson)
                    || pi.FullSnapshotJson == "{}")
                    return false;

                using var doc = JsonDocument.Parse(
                    pi.FullSnapshotJson,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true
                    });

                var root = doc.RootElement;

                // Path 1: Client.SalesRepresentative (most common)
                string? salesRep = TryReadString(root, "Client", "SalesRepresentative");
                if (SafeContains(salesRep, needle))
                {
                    Debug.WriteLine($"[Aggregator] Salesman match via PI " +
                                    $"{proformaId} → Client.SalesRepresentative");
                    return true;
                }

                // Path 2: PI's own PreparedBy.Name
                string? preparedBy = TryReadString(root, "PreparedBy", "Name");
                if (SafeContains(preparedBy, needle))
                {
                    Debug.WriteLine($"[Aggregator] Salesman match via PI " +
                                    $"{proformaId} → PreparedBy.Name");
                    return true;
                }

                // Path 3: Client.DocumentPreparedBy
                string? docPrep = TryReadString(root, "Client", "DocumentPreparedBy");
                if (SafeContains(docPrep, needle))
                {
                    Debug.WriteLine($"[Aggregator] Salesman match via PI " +
                                    $"{proformaId} → Client.DocumentPreparedBy");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Aggregator] PI JSON parse error " +
                                $"{proformaId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely reads a nested JSON string property.
        /// Returns null if any part of the path is missing or wrong type.
        /// Never throws — always returns null on any error.
        /// </summary>
        private static string? TryReadString(
            JsonElement root,
            string parent,
            string child)
        {
            try
            {
                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                if (!root.TryGetProperty(parent, out var parentEl))
                    return null;

                if (parentEl.ValueKind != JsonValueKind.Object)
                    return null;

                if (!parentEl.TryGetProperty(child, out var childEl))
                    return null;

                if (childEl.ValueKind != JsonValueKind.String)
                    return null;

                return childEl.GetString();
            }
            catch
            {
                return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  ATTACHMENT FLAGS
        // ═════════════════════════════════════════════════════════════════

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

        private static List<StatementInvoiceRow> BuildInvoiceRows(
            List<TaxInvoiceListItem> items,
            Dictionary<string, int> attachmentCounts,
            Dictionary<string, AttachmentFlags> attachmentFlags,
            DateTime today)
        {
            var rows = new List<StatementInvoiceRow>(items.Count);

            foreach (var item in items)
            {
                string status = item.Status ?? "";

                bool isPaidOrClosed =
                    status == "Paid" ||
                    status == "Cancelled" ||
                    status == "Void";

                int daysOverdue = 0;
                if (!isPaidOrClosed &&
                    item.DueDate != default &&
                    item.DueDate.Date < today)
                {
                    daysOverdue = (today - item.DueDate.Date).Days;
                }

                decimal paidAmount =
                    status == "Paid" ? item.GrandTotal : 0m;

                string invNum = item.InvoiceNumber ?? "";
                attachmentCounts.TryGetValue(invNum, out int attCount);
                attachmentFlags.TryGetValue(invNum, out var flags);

                rows.Add(new StatementInvoiceRow
                {
                    InvoiceNumber = invNum,
                    ProformaId = item.ProformaId ?? "",
                    DeliveryNoteRef = item.DeliveryNoteReference ?? "",
                    PurchaseOrderNumber = item.PurchaseOrderNumber ?? "",
                    InvoiceType = item.InvoiceType ?? "",
                    Status = status,
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

        private static StatementAgingBuckets BuildAgingBuckets(
            List<StatementInvoiceRow> rows,
            DateTime today)
        {
            var b = new StatementAgingBuckets();

            foreach (var row in rows)
            {
                decimal balance = row.Balance;
                if (balance <= 0m) continue;

                if (row.DueDate == default ||
                    row.DueDate.Date >= today)
                {
                    b.Current += balance;
                    continue;
                }

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

        private static StatementCustomerInfo BuildCustomerInfo(
            List<TaxInvoiceListItem> filtered,
            StatementFilter filter)
        {
            var first = filtered.First();

            return new StatementCustomerInfo
            {
                CompanyName = !string.IsNullOrWhiteSpace(filter.CompanyName)
                                   ? filter.CompanyName
                                   : (first.CompanyName ?? ""),
                CustomerName = !string.IsNullOrWhiteSpace(filter.CustomerName)
                                   ? filter.CustomerName
                                   : (first.CustomerName ?? ""),
                ProjectName = !string.IsNullOrWhiteSpace(filter.ProjectName)
                                   ? filter.ProjectName
                                   : (first.ProjectName ?? ""),
                SalesmanName = filter.SalesmanName ?? "",
                ContactPerson = ""
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE TYPES
        // ═════════════════════════════════════════════════════════════════

        private sealed class AttachmentFlags
        {
            public bool HasSignedPi { get; init; }
            public bool HasSignedDn { get; init; }
        }
    }
}