// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Reconciliation/OrderReconciliationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Order Fulfillment Reconciliation (Chat 22 — Phase 1 Backend)
//
// PURPOSE:
//   Computes line-item reconciliation across PI, DN, and TI for a
//   given ProformaId. Answers the question:
//     "For each glass item ordered, how many were delivered vs invoiced?"
//
// ALGORITHM:
//   1. Load PI snapshot → authoritative ordered qty per line
//   2. Load JO(s) for this PI → get JobOrderIds for DN queries
//   3. Load cumulative delivered qty from all confirmed DNs
//   4. Load cumulative invoiced qty from all non-cancelled TIs
//   5. Compare per line: Ordered vs Delivered vs Invoiced
//   6. Assign status (FullyMatched/OverInvoiced/UnderInvoiced/etc.)
//
// DATA ACCESS:
//   App.Invoices      → PI snapshot (authoritative ordered qty)
//   App.Jobs           → JO lookup by SourceProformaId
//   App.DeliveryNotes  → GetPreviouslyDeliveredMapAsync (cumulative DN qty)
//   App.TaxInvoices    → GetByProformaIdAsync + GetByInvoiceNumberAsync
//
// KEY FORMAT:
//   Line items matched by "specGroupNumber|serialNumber" composite key.
//   Same format used by DeliveryNoteRepository.GetPreviouslyDeliveredMapAsync.
//
// PERFORMANCE:
//   • BuildBatchAsync deduplicates PI IDs (each computed once)
//   • TI line-item loading sequential per PI (typically 1-5 TIs per PI)
//   • DN delivered map loaded in one call per JO
//   • PI snapshot deserialized once per PI
//
// THREAD SAFETY:
//   Stateless service. Each method creates fresh state.
//   Safe to call from any thread — all App.* repos are thread-safe.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.Statement.Reconciliation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Reconciliation
{
    /// <summary>
    /// Computes order fulfillment reconciliation for Proforma Invoice families.
    /// Stateless — create a new instance per use or reuse freely.
    /// </summary>
    public class OrderReconciliationService
    {
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC — Single PI reconciliation
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds line-item reconciliation for one Proforma Invoice.
        /// Cross-references PI ordered qty vs DN delivered qty vs TI invoiced qty.
        ///
        /// Returns a summary with per-line rows and rollup flags.
        /// Never throws — returns degraded summary on errors.
        /// </summary>
        public async Task<OrderReconciliationSummary> BuildForProformaAsync(
            string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId))
                return EmptySummary(proformaId);

            try
            {
                AppLogService.Debug($"[Reconciliation] Building for {proformaId}");

                // ── 1. Load PI snapshot for authoritative line items ─────
                var piLines = await LoadPiLineItemsAsync(proformaId);

                if (piLines == null || piLines.Count == 0)
                {
                    AppLogService.Debug($"[Reconciliation] No PI data for {proformaId}");
                    return new OrderReconciliationSummary
                    {
                        ProformaId = proformaId,
                        HasProformaData = false,
                        HasDeliveryTracking = false
                    };
                }

                // ── 2. Load JO(s) for this PI ───────────────────────────
                var jobOrders = await App.Jobs.GetBySourceProformaIdAsync(proformaId);
                var jobOrderIds = jobOrders
                    .Select(jo => jo.JobOrderId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();

                bool hasDeliveryTracking = jobOrderIds.Count > 0;

                AppLogService.Debug($"[Reconciliation] {proformaId}: " +
                                $"{jobOrderIds.Count} JO(s), " +
                                $"{piLines.Count} line items");

                // ── 3. Load cumulative delivered qty from all DNs ────────
                var deliveredMap = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

                if (hasDeliveryTracking)
                {
                    foreach (var joId in jobOrderIds)
                    {
                        var joMap = await App.DeliveryNotes
                            .GetPreviouslyDeliveredMapAsync(joId, "");

                        foreach (var kvp in joMap)
                        {
                            if (deliveredMap.ContainsKey(kvp.Key))
                                deliveredMap[kvp.Key] += kvp.Value;
                            else
                                deliveredMap[kvp.Key] = kvp.Value;
                        }
                    }
                }

                AppLogService.Debug($"[Reconciliation] {proformaId}: " +
                                $"delivered map has {deliveredMap.Count} entries");

                // ── 4. Load cumulative invoiced qty from all TIs ────────
                var invoicedMap = await LoadInvoicedMapAsync(proformaId);

                AppLogService.Debug($"[Reconciliation] {proformaId}: " +
                                $"invoiced map has {invoicedMap.Count} entries");

                // ── 5. Build per-line reconciliation rows ───────────────
                var rows = new List<OrderReconciliationRow>(piLines.Count);

                foreach (var piLine in piLines)
                {
                    string key = piLine.CompositeKey;

                    int delivered = deliveredMap.TryGetValue(key, out int d) ? d : 0;
                    int invoiced = invoicedMap.TryGetValue(key, out int inv) ? inv : 0;
                    int balance = Math.Max(0, piLine.OrderedQty - delivered);
                    int diff = invoiced - delivered;

                    var status = DetermineStatus(
                        piLine.OrderedQty, delivered, invoiced);

                    rows.Add(new OrderReconciliationRow
                    {
                        ProformaId = proformaId,
                        SpecGroupNumber = piLine.SpecGroupNumber,
                        SpecGroupName = piLine.SpecGroupName,
                        SerialNumber = piLine.SerialNumber,
                        GlassRef = piLine.GlassRef,
                        DimensionDisplay = piLine.DimensionDisplay,
                        OrderedQty = piLine.OrderedQty,
                        DeliveredQty = delivered,
                        BalanceQty = balance,
                        InvoicedQty = invoiced,
                        InvoiceVsDeliveryDiff = diff,
                        Status = status
                    });
                }

                // ── 6. Build and return summary ─────────────────────────
                var summary = new OrderReconciliationSummary
                {
                    ProformaId = proformaId,
                    JobOrderIds = string.Join(",", jobOrderIds),
                    HasProformaData = true,
                    HasDeliveryTracking = hasDeliveryTracking,
                    Rows = rows
                };

                AppLogService.Debug($"[Reconciliation] {proformaId}: " +
                                $"{summary.SummaryDisplay}");

                return summary;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Reconciliation] ERROR for {proformaId}: " +
                                $"{ex.Message}");
                return EmptySummary(proformaId);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC — Batch reconciliation (deduplicates PI IDs)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds reconciliation for multiple Proforma IDs.
        /// Deduplicates — each PI is processed exactly once.
        /// Returns a dictionary keyed by ProformaId.
        /// Used by StatementAggregator to enrich all invoice rows
        /// in one pass during statement generation.
        /// </summary>
        public async Task<Dictionary<string, OrderReconciliationSummary>>
            BuildBatchAsync(IEnumerable<string> proformaIds)
        {
            var uniqueIds = proformaIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new Dictionary<string, OrderReconciliationSummary>(
                StringComparer.OrdinalIgnoreCase);

            if (uniqueIds.Count == 0) return result;

            AppLogService.Debug($"[Reconciliation] Batch: {uniqueIds.Count} unique PIs");

            foreach (var piId in uniqueIds)
            {
                result[piId] = await BuildForProformaAsync(piId);
            }

            // ── Log batch summary ────────────────────────────────────────
            int totalRows = result.Values.Sum(s => s.Rows.Count);
            int overInvoiced = result.Values.Sum(s => s.OverInvoicedCount);
            int underInvoiced = result.Values.Sum(s => s.UnderInvoicedCount);

            AppLogService.Debug($"[Reconciliation] Batch complete: " +
                            $"{totalRows} items, " +
                            $"{overInvoiced} over-invoiced, " +
                            $"{underInvoiced} under-invoiced");

            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE — Load PI line items from snapshot
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads and parses the PI snapshot to extract authoritative
        /// line items with their ordered quantities.
        /// Returns null if PI not found or snapshot is invalid.
        /// </summary>
        private static async Task<List<PiLineInfo>?> LoadPiLineItemsAsync(
            string proformaId)
        {
            try
            {
                var pi = await App.Invoices.GetByProformaIdAsync(proformaId);

                if (pi == null)
                {
                    AppLogService.Debug($"[Reconciliation] PI not found: {proformaId}");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(pi.FullSnapshotJson) ||
                    pi.FullSnapshotJson == "{}")
                {
                    AppLogService.Debug($"[Reconciliation] PI has empty snapshot: {proformaId}");
                    return null;
                }

                var snapshot = JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                    pi.FullSnapshotJson, _jsonOpts);

                if (snapshot?.SpecGroups == null || snapshot.SpecGroups.Count == 0)
                {
                    AppLogService.Debug($"[Reconciliation] PI has no spec groups: {proformaId}");
                    return null;
                }

                var lines = new List<PiLineInfo>();

                foreach (var spec in snapshot.SpecGroups)
                {
                    if (spec.ItemRows == null) continue;

                    foreach (var row in spec.ItemRows)
                    {
                        lines.Add(new PiLineInfo
                        {
                            SpecGroupNumber = spec.GroupNumber,
                            SpecGroupName = spec.GroupName ?? "",
                            SerialNumber = row.SerialNumber,
                            GlassRef = row.GlassRef ?? "",
                            OrderedQty = row.Qty,
                            DimensionDisplay = FormatDimensions(
                                row.W1, row.H1, row.W2, row.H2),
                            CompositeKey = $"{spec.GroupNumber}|{row.SerialNumber}"
                        });
                    }
                }

                return lines;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Reconciliation] PI load error {proformaId}: " +
                                $"{ex.Message}");
                return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE — Load invoiced quantities from TI items
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads all TaxInvoiceItems for a ProformaId family,
        /// sums Qty per (SpecGroupNumber, SerialNumber) composite key.
        /// Excludes Cancelled, Void, and Draft invoices.
        /// </summary>
        private static async Task<Dictionary<string, int>> LoadInvoicedMapAsync(
            string proformaId)
        {
            var map = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                // Get list items first (lightweight — no JSON parse)
                var tiListItems = await App.TaxInvoices
                    .GetByProformaIdAsync(proformaId);

                if (tiListItems == null || tiListItems.Count == 0)
                    return map;

                // Filter to issued/active invoices only
                var activeStatuses = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                { "Issued", "Paid", "PartiallyPaid", "Overdue" };

                var activeTis = tiListItems
                    .Where(ti => activeStatuses.Contains(ti.Status ?? ""))
                    .ToList();

                if (activeTis.Count == 0) return map;

                // Load full TIs to get line items
                foreach (var tiListItem in activeTis)
                {
                    try
                    {
                        var fullTi = await App.TaxInvoices
                            .GetByInvoiceNumberAsync(tiListItem.InvoiceNumber);

                        if (fullTi?.Items == null) continue;

                        foreach (var item in fullTi.Items)
                        {
                            var key = $"{item.SpecGroupNumber}|{item.SerialNumber}";

                            if (map.ContainsKey(key))
                                map[key] += item.Qty;
                            else
                                map[key] = item.Qty;
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[Reconciliation] TI load error " +
                                        $"{tiListItem.InvoiceNumber}: {ex.Message}");
                        // Skip this TI, continue with others
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Reconciliation] Invoiced map error " +
                                $"{proformaId}: {ex.Message}");
            }

            return map;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE — Status determination logic
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Determines the reconciliation status for one line item
        /// based on the three quantity values.
        ///
        /// PRIORITY ORDER (first matching rule wins):
        ///   1. Nothing happened yet → NotYetStarted
        ///   2. Invoiced > Delivered → OverInvoiced (★ THE BUG)
        ///   3. Delivered > Invoiced → UnderInvoiced
        ///   4. Delivered > Ordered  → OverDelivered (rare)
        ///   5. Delivered less than Ordered → PartialDelivery
        ///   6. All equal → FullyMatched
        /// </summary>
        private static ReconciliationStatus DetermineStatus(
            int ordered, int delivered, int invoiced)
        {
            // Nothing happened — no deliveries, no invoices
            if (delivered == 0 && invoiced == 0)
                return ReconciliationStatus.NotYetStarted;

            // ★ THE CRITICAL CHECK: invoiced more than delivered
            // This is the user's exact bug scenario:
            //   Ordered=2, Delivered=1, Invoiced=2 → OverInvoiced!
            if (invoiced > delivered)
                return ReconciliationStatus.OverInvoiced;

            // Delivered but not yet fully invoiced (revenue leakage)
            if (delivered > invoiced)
                return ReconciliationStatus.UnderInvoiced;

            // From here: invoiced == delivered

            // Rare: delivered more than originally ordered
            if (delivered > ordered)
                return ReconciliationStatus.OverDelivered;

            // Delivered less than ordered but billing matches delivery
            if (delivered < ordered)
                return ReconciliationStatus.PartialDelivery;

            // Perfect match: delivered == invoiced == ordered
            return ReconciliationStatus.FullyMatched;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE — Helpers
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Formats glass panel dimensions for display.
        /// Handles both rectangular (W1×H1) and trapezoid (W1×H1/W2×H2) shapes.
        /// </summary>
        private static string FormatDimensions(int w1, int h1, int w2, int h2)
        {
            if (w1 <= 0 && h1 <= 0) return "—";

            // Simple rectangle
            if ((w2 <= 0 || w2 == w1) && (h2 <= 0 || h2 == h1))
                return $"{w1} × {h1} mm";

            // Trapezoid / irregular shape
            return $"{w1}×{h1} / {w2}×{h2} mm";
        }

        /// <summary>Returns an empty summary with no data flags set.</summary>
        private static OrderReconciliationSummary EmptySummary(string proformaId) =>
            new()
            {
                ProformaId = proformaId ?? "",
                HasProformaData = false,
                HasDeliveryTracking = false
            };

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE — Internal DTO for PI line items
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lightweight struct for PI line-item data extracted from snapshot.
        /// Used only within this service — not exposed publicly.
        /// </summary>
        private sealed class PiLineInfo
        {
            public int SpecGroupNumber { get; init; }
            public string SpecGroupName { get; init; } = "";
            public int SerialNumber { get; init; }
            public string GlassRef { get; init; } = "";
            public int OrderedQty { get; init; }
            public string DimensionDisplay { get; init; } = "";
            public string CompositeKey { get; init; } = "";
        }
    }
}