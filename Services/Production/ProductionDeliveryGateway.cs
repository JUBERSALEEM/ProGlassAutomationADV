// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionDeliveryGateway.cs
// PROJECT: ProGlassAutomation
// MODULE: Production ↔ Delivery Notes (piece flow, 2026-09-11 + Phase14)
//   Phase 14 extension: Produced − Failed − Rework Pending − Already Delivered = Deliverable
//   Failed = Breakage entries + QC Failed entries
//   Rework Pending = replacements with Pending/InProgress status
//   Delivery remains authoritative via existing DN caps logic.
//   Cross-DB failure: production.db read may fail — fallback to null (classic JO behaviour).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    public static class ProductionDeliveryGateway
    {
        /// <summary>
        /// Deliverable cap per (Spec, Serial) row for one Job Order:
        /// Phase 14: Produced − Failed − Rework Pending − Already Delivered
        /// Returns NULL when the job has no production release — the DN
        /// module then falls back to plain Job-Order balances.
        /// </summary>
        public static async Task<Dictionary<(int Spec, int Serial), int>?> GetDeliveryCapsAsync(
            string jobOrderId)
        {
            if (string.IsNullOrWhiteSpace(jobOrderId)) return null;

            try
            {
                var ready = await ProductionRepository
                    .GetDispatchReadyByRowAsync(jobOrderId).ConfigureAwait(false);
                if (ready == null) return null; // not production-managed

                var delivered = await GetConfirmedDeliveredAsync(jobOrderId)
                    .ConfigureAwait(false);

                // Phase 14: deduct breakage + QC failed + rework pending
                var deductions = await GetDeductionsByRowAsync(jobOrderId).ConfigureAwait(false);

                var caps = new Dictionary<(int, int), int>();
                foreach (var kv in ready)
                {
                    delivered.TryGetValue(kv.Key, out int alreadyOut);
                    deductions.TryGetValue(kv.Key, out int deduction);
                    // Produced - Failed - ReworkPending - AlreadyDelivered
                    int deliverable = kv.Value.Ready - deduction - alreadyOut;
                    caps[kv.Key] = Math.Max(0, deliverable);
                }
                return caps;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ProductionDeliveryGateway] caps failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Dispatch card figures for one unit row — Phase14 includes deductions.</summary>
        public static async Task<(int Ready, int Delivered, int Balance, bool ProductionManaged)>
            GetRowDispatchStateAsync(string jobOrderId, int specGroupNumber, int serialNumber)
        {
            try
            {
                var ready = await ProductionRepository
                    .GetDispatchReadyByRowAsync(jobOrderId).ConfigureAwait(false);

                if (ready == null ||
                    !ready.TryGetValue((specGroupNumber, serialNumber), out var row))
                    return (0, 0, 0, false);

                var deliveredMap = await GetConfirmedDeliveredAsync(jobOrderId)
                    .ConfigureAwait(false);
                deliveredMap.TryGetValue((specGroupNumber, serialNumber), out int delivered);

                var deductions = await GetDeductionsByRowAsync(jobOrderId).ConfigureAwait(false);
                deductions.TryGetValue((specGroupNumber, serialNumber), out int deduction);

                int effectiveReady = Math.Max(0, row.Ready - deduction);
                delivered = Math.Min(delivered, effectiveReady);

                return (effectiveReady, delivered, Math.Max(0, effectiveReady - delivered), true);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ProductionDeliveryGateway] row state failed: " + ex.Message);
                return (0, 0, 0, false);
            }
        }

        /// <summary>Dispatch card figures for a whole Job Order (all rows summed) — Phase14 deductions.</summary>
        public static async Task<(int Ready, int Delivered, int Balance, bool ProductionManaged)>
            GetJobDispatchStateAsync(string jobOrderId)
        {
            try
            {
                var ready = await ProductionRepository
                    .GetDispatchReadyByRowAsync(jobOrderId).ConfigureAwait(false);

                if (ready == null) return (0, 0, 0, false);

                var deliveredMap = await GetConfirmedDeliveredAsync(jobOrderId)
                    .ConfigureAwait(false);

                var deductions = await GetDeductionsByRowAsync(jobOrderId).ConfigureAwait(false);

                int totalReady = 0;
                int totalDeduction = 0;
                foreach (var row in ready.Values) totalReady += row.Ready;
                foreach (var d in deductions.Values) totalDeduction += d;

                int effectiveReady = Math.Max(0, totalReady - totalDeduction);

                int totalDelivered = 0;
                foreach (var kv in deliveredMap)
                {
                    int rowReady = ready.TryGetValue(kv.Key, out var r) ? r.Ready : 0;
                    deductions.TryGetValue(kv.Key, out int rowDeduction);
                    int effRowReady = Math.Max(0, rowReady - rowDeduction);
                    totalDelivered += Math.Min(kv.Value, effRowReady);
                }

                return (effectiveReady, totalDelivered, Math.Max(0, effectiveReady - totalDelivered), true);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ProductionDeliveryGateway] job state failed: " + ex.Message);
                return (0, 0, 0, false);
            }
        }

        /// <summary>
        /// Phase 14: deductions per row = Breakage qty + QC Failed qty + Rework Pending qty
        /// Produced − Failed − Rework Pending = deliverable before delivery deduction.
        /// </summary>
        private static async Task<Dictionary<(int Spec, int Serial), int>> GetDeductionsByRowAsync(string jobOrderId)
        {
            var map = new Dictionary<(int, int), int>();
            if (string.IsNullOrWhiteSpace(jobOrderId)) return map;

            try
            {
                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();

                // Breakage entries
                var breakages = await db.Entries.AsNoTracking()
                    .Where(e => e.JobRef == jobOrderId && e.EntryType == (int)ProductionEntryType.Breakage)
                    .ToListAsync().ConfigureAwait(false);

                foreach (var b in breakages)
                {
                    var key = (b.SpecGroupNumber, b.SerialNumber);
                    map.TryGetValue(key, out int current);
                    map[key] = current + b.Qty;
                }

                // QC Failed entries — only count if not yet reworked (no Passed after Failed)
                var qcFailed = await db.QcEntries.AsNoTracking()
                    .Where(q => q.JobRef == jobOrderId && q.Result == (int)QcResult.Failed)
                    .ToListAsync().ConfigureAwait(false);

                // For each failed QC, check if later Passed exists for same work item
                foreach (var qcf in qcFailed)
                {
                    bool hasLaterPass = await db.QcEntries.AsNoTracking()
                        .AnyAsync(q => q.WorkItemId == qcf.WorkItemId &&
                                       q.QcSequence > qcf.QcSequence &&
                                       q.Result == (int)QcResult.Passed).ConfigureAwait(false);

                    if (!hasLaterPass)
                    {
                        var key = (qcf.SpecGroupNumber, qcf.SerialNumber);
                        map.TryGetValue(key, out int current);
                        // QC failure deducts 1 per failed piece? Use QtyDone of work item or 1
                        // For foundation, deduct 1 per failed QC entry (conservative)
                        map[key] = current + 1;
                    }
                }

                // Rework pending — replacements with Pending/InProgress
                var pendingReplacements = await db.Replacements.AsNoTracking()
                    .Where(r => r.JobRef == jobOrderId && (r.Status == 0 || r.Status == 1))
                    .ToListAsync().ConfigureAwait(false);

                foreach (var pr in pendingReplacements)
                {
                    var key = (pr.SpecGroupNumber, pr.SerialNumber);
                    map.TryGetValue(key, out int current);
                    map[key] = current + pr.Quantity;
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[ProductionDeliveryGateway] deductions failed for {jobOrderId}: {ex.Message}");
            }

            return map;
        }

        /// <summary>
        /// Pieces already out the door per (Spec, Serial), summed across
        /// CONFIRMED delivery notes only.
        /// </summary>
        private static async Task<Dictionary<(int Spec, int Serial), int>> GetConfirmedDeliveredAsync(
            string jobOrderId)
        {
            var map = new Dictionary<(int, int), int>();
            if (App.DeliveryNotes == null) return map;

            var list = await App.DeliveryNotes
                .GetByJobOrderIdAsync(jobOrderId).ConfigureAwait(false);

            foreach (var dn in list)
            {
                if (!DeliveryNote.DeliveryNoteWorkflow.IsConfirmedDelivery(dn.Status))
                    continue;

                var entity = await App.DeliveryNotes.GetByIdAsync(dn.Id)
                    .ConfigureAwait(false);
                if (entity?.Items == null) continue;

                foreach (var item in entity.Items)
                {
                    if (item.DeliveredQty <= 0) continue;
                    var key = (item.SpecGroupNumber, item.SerialNumber);
                    map.TryGetValue(key, out int current);
                    map[key] = current + item.DeliveredQty;
                }
            }

            return map;
        }
    }
}
