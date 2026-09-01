// ═══════════════════════════════════════════════════════════════════════
// FILE: StatementAggregator.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Statement.Reconciliation;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Aggregation
{
        public partial class StatementAggregator
    {
            private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        // ── Reconciliation service (Chat 22 v3) ──────────────────────────
        // Stateless — one instance per aggregator is safe and cheap.
        private readonly OrderReconciliationService _reconciliationService = new();

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        // ═════════════════════════════════════════════════════════════════

        public async Task<StatementSnapshot?> BuildAsync(
            StatementFilter filter,
            string statementNumber)
        {
            // ── Step 1: Load all invoices ─────────────────────────────────
            var allItems = await App.TaxInvoices.GetAllAsync();

            AppLogService.Debug($"[Aggregator] Loaded {allItems?.Count ?? 0} " +
                            "invoices from DB");

            if (allItems == null || allItems.Count == 0)
            {
                AppLogService.Debug("[Aggregator] DB is empty");
                return null;
            }

            // ── Step 1.5: HARD VALIDITY FILTER ────────────────────────────
            // A statement may only include invoices that are actually issued
            // AND generated against a real (non-master) delivery note. Draft /
            // Cancelled / Void invoices and invoices without a real delivery
            // are dropped here, before the operator's own filters run.
            allItems = await FilterToValidStatementInvoicesAsync(allItems);

            if (allItems.Count == 0)
            {
                AppLogService.Debug("[Aggregator] No issued invoices with a real delivery note");
                return null;
            }

            // ── Step 2: Indexed-column filters ────────────────────────────
            var filtered = ApplyFilters(allItems, filter);

            AppLogService.Debug($"[Aggregator] After indexed filters: " +
                            $"{filtered.Count} invoices");

            // ── Step 2b: Project filter (JSON search) ─────────────────────
            if (!string.IsNullOrWhiteSpace(filter.ProjectName))
            {
                filtered = await ApplyProjectFilterAsync(
                    filtered, filter.ProjectName);

                AppLogService.Debug($"[Aggregator] After project filter: " +
                                $"{filtered.Count} invoices");
            }

            // ── Step 3: Salesman filter (multi-path JSON search) ──────────
            if (!string.IsNullOrWhiteSpace(filter.SalesmanName))
            {
                filtered = await ApplySalesmanFilterAsync(
                    filtered, filter.SalesmanName);

                AppLogService.Debug($"[Aggregator] After salesman filter: " +
                                $"{filtered.Count} invoices");
            }

            if (filtered.Count == 0)
            {
                AppLogService.Debug("[Aggregator] No invoices matched filters");
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

            // ── Step 5.5: RECONCILIATION (Chat 22 v3) ★ NEW ★ ─────────────
            // Build reconciliation for all unique Proforma IDs in the
            // filtered result. Batch-loaded so each PI is processed once
            // even if multiple TIs belong to the same PI family.
            var reconciliationMap = await BuildReconciliationMapAsync(filtered);

            AppLogService.Debug($"[Aggregator] Reconciliation built for " +
                            $"{reconciliationMap.Count} PI families");

            // ── Step 6-10: Build rows + financial + aging + assemble ──────
            var today = DateTime.Today;
            var rows = BuildInvoiceRows(
                filtered, attachmentCounts, attachmentFlags, today);

            // Attach reconciliation summaries to each row (Chat 22 v3)
            AttachReconciliationToRows(rows, reconciliationMap);

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
        //  RECONCILIATION LOADER (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds reconciliation summaries for every unique Proforma ID
        /// present in the filtered invoice list.
        ///
        /// DEDUPLICATION:
        ///   If 5 TaxInvoices share the same ProformaId (partial invoicing),
        ///   only ONE reconciliation load happens — the same summary is
        ///   attached to all 5 rows later in AttachReconciliationToRows.
        ///
        /// Never throws — errors within reconciliation service are
        /// logged internally and return empty summaries.
        /// </summary>
        private async Task<Dictionary<string, OrderReconciliationSummary>>
            BuildReconciliationMapAsync(List<TaxInvoiceListItem> filtered)
        {
            // Extract unique, non-empty Proforma IDs from filtered invoices
            var proformaIds = filtered
                .Select(i => i.ProformaId ?? "")
                .Where(pid => !string.IsNullOrWhiteSpace(pid))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (proformaIds.Count == 0)
            {
                AppLogService.Debug("[Aggregator] No PIs to reconcile");
                return new Dictionary<string, OrderReconciliationSummary>(
                    StringComparer.OrdinalIgnoreCase);
            }

            AppLogService.Debug($"[Aggregator] Reconciling {proformaIds.Count} " +
                            "unique PI families…");

            try
            {
                return await _reconciliationService.BuildBatchAsync(proformaIds);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[Statement] Reconciliation batch failed.", ex,
                    new LogContext
                    {
                        Module = "Statement",
                        Class = nameof(StatementAggregator),
                        Operation = "GetReconciliationMapAsync"
                    });
                // Return empty map — statement still generates, just no
                // reconciliation data. Preview UI degrades gracefully.
                return new Dictionary<string, OrderReconciliationSummary>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Attaches the reconciliation summary to each StatementInvoiceRow
        /// by looking up its ProformaId in the pre-built map.
        ///
        /// Rows without a ProformaId or with no matching reconciliation
        /// keep row.Reconciliation = null. UI code must null-check.
        /// </summary>
        private static void AttachReconciliationToRows(
            List<StatementInvoiceRow> rows,
            Dictionary<string, OrderReconciliationSummary> reconciliationMap)
        {
            if (rows == null || reconciliationMap == null) return;

            int attachedCount = 0;
            int discrepancyCount = 0;

            foreach (var row in rows)
            {
                string pid = row.ProformaId ?? "";
                if (string.IsNullOrWhiteSpace(pid)) continue;

                if (reconciliationMap.TryGetValue(pid, out var summary))
                {
                    row.Reconciliation = summary;
                    attachedCount++;

                    if (summary.HasDiscrepancies) discrepancyCount++;
                }
            }

            AppLogService.Debug($"[Aggregator] Attached reconciliation to " +
                            $"{attachedCount}/{rows.Count} rows " +
                            $"({discrepancyCount} with discrepancies)");
        }

        // ═════════════════════════════════════════════════════════════════
        //  HARD VALIDITY FILTER — issued + real delivery only
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Statuses that count as "issued" for a statement. A tax invoice can
        /// only become Paid / PartiallyPaid / Overdue after it has been
        /// issued, so those are included; Draft / Cancelled / Void are not.
        /// </summary>
        private static readonly string[] IssuedStatuses =
        {
            "Issued", "Paid", "PartiallyPaid", "Partially Paid", "Overdue"
        };

        /// <summary>
        /// Drops invoices that are not issued, and invoices that do not
        /// reference at least one real (non-master) delivery note. Master
        /// ("Dummy") delivery notes are already excluded by
        /// <c>DeliveryNotes.GetAllAsync()</c>, so the returned set is the
        /// real deliveries an invoice may legitimately point at.
        /// </summary>
        private async Task<List<TaxInvoiceListItem>> FilterToValidStatementInvoicesAsync(
            List<TaxInvoiceListItem> items)
        {
            // Load real delivery-note ids once (master DNs excluded by repo).
            var realDnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool dnLoadSucceeded = false;
            try
            {
                var dns = App.DeliveryNotes == null
                    ? null
                    : await App.DeliveryNotes.GetAllAsync();
                foreach (var dn in dns ?? new List<ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteListItem>())
                {
                    if (!string.IsNullOrWhiteSpace(dn.DeliveryNoteId))
                        realDnIds.Add(dn.DeliveryNoteId.Trim());
                }
                dnLoadSucceeded = true;
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[Statement] Failed to load delivery notes for the validity filter.", ex,
                    new LogContext
                    {
                        Module = "Statement",
                        Class = nameof(StatementAggregator),
                        Operation = "FilterToValidStatementInvoicesAsync"
                    });
            }

            var valid = items.Where(i =>
            {
                // 1. Issued status only.
                string status = i.Status?.Trim() ?? "";
                if (!IssuedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                    return false;

                // 2. Must reference at least one real delivery note. If the
                //    delivery-note set could not be loaded, fall back to the
                //    status filter only (never silently drop everything).
                if (!dnLoadSucceeded)
                    return true;

                var refs = TaxInvoiceDnReconciler.ParseDnRefs(i.DeliveryNoteReference);
                if (refs.Count == 0)
                    return false;
                return refs.Any(r => realDnIds.Contains(r));
            }).ToList();

            int dropped = items.Count - valid.Count;
            AppLogService.Debug(
                $"[Aggregator] Validity filter: {items.Count} -> {valid.Count} " +
                $"({dropped} dropped: non-issued or no real delivery)");

            return valid;
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
            // Moved to Step 2b (post-filter JSON search).

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
                    AppLogService.Debug($"[Aggregator] Project JSON search " +
                                    $"error on {item.InvoiceNumber}: {ex.Message}");
                    // On error, INCLUDE the item (don't silently exclude)
                    result.Add(item);
                }
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SALESMAN FILTER — MULTI-PATH SEARCH (Chat 17)
        // ═════════════════════════════════════════════════════════════════

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
                        // Path A: Search Tax Invoice snapshot
                        bool foundInInvoice = await SearchSalesmanInTaxInvoice(
                            item.InvoiceNumber, needle);

                        if (foundInInvoice)
                            return (item, true);

                        // Path B: Fallback to parent PI snapshot
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
                        AppLogService.Debug($"[Aggregator] Salesman search " +
                                        $"error on {item.InvoiceNumber}: " +
                                        $"{ex.Message}");
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

                string? preparedBy = TryReadString(root, "PreparedBy", "Name");
                if (SafeContains(preparedBy, needle))
                {
                    AppLogService.Debug($"[Aggregator] Salesman match " +
                                    $"{invoiceNumber} → PreparedBy.Name");
                    return true;
                }

                string? contact = TryReadString(root, "Customer", "ContactPerson");
                if (SafeContains(contact, needle))
                {
                    AppLogService.Debug($"[Aggregator] Salesman match " +
                                    $"{invoiceNumber} → Customer.ContactPerson");
                    return true;
                }

                string? notes = TryReadString(root, "Bank", "PaymentNotes");
                if (SafeContains(notes, needle))
                {
                    AppLogService.Debug($"[Aggregator] Salesman match " +
                                    $"{invoiceNumber} → Bank.PaymentNotes");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Aggregator] TaxInvoice JSON parse " +
                                $"error {invoiceNumber}: {ex.Message}");
                return false;
            }
        }

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

                string? salesRep = TryReadString(root, "Client", "SalesRepresentative");
                if (SafeContains(salesRep, needle))
                {
                    AppLogService.Debug($"[Aggregator] Salesman match via PI " +
                                    $"{proformaId} → Client.SalesRepresentative");
                    return true;
                }

                string? preparedBy = TryReadString(root, "PreparedBy", "Name");
                if (SafeContains(preparedBy, needle))
                {
                    AppLogService.Debug($"[Aggregator] Salesman match via PI " +
                                    $"{proformaId} → PreparedBy.Name");
                    return true;
                }

                string? docPrep = TryReadString(root, "Client", "DocumentPreparedBy");
                if (SafeContains(docPrep, needle))
                {
                    AppLogService.Debug($"[Aggregator] Salesman match via PI " +
                                    $"{proformaId} → Client.DocumentPreparedBy");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"[Statement] PI JSON parse error for {proformaId}: {ex.Message}");
                return false;
            }
        }

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
                    // Reconciliation is attached in AttachReconciliationToRows
                    // after all rows are built.
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
            // Defensive: FirstOrDefault instead of First (Chat 22 hardening)
            var first = filtered.FirstOrDefault();

            return new StatementCustomerInfo
            {
                CompanyName = !string.IsNullOrWhiteSpace(filter.CompanyName)
                                   ? filter.CompanyName
                                   : (first?.CompanyName ?? ""),
                CustomerName = !string.IsNullOrWhiteSpace(filter.CustomerName)
                                   ? filter.CustomerName
                                   : (first?.CustomerName ?? ""),
                ProjectName = !string.IsNullOrWhiteSpace(filter.ProjectName)
                                   ? filter.ProjectName
                                   : (first?.ProjectName ?? ""),
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
