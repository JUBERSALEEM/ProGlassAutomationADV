// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionDeliveryGateway.cs
// PROJECT: ProGlassAutomation
// MODULE: Production ↔ Delivery Notes (piece flow, 2026-09-11)
//
// Production completes PIECES; delivery notes deliver them. This gateway
// is the bridge:
//
//   • GetDeliveryCapsAsync(jobOrderId) — per Step-5 row (Spec|Serial),
//     how many pieces a delivery note may deliver RIGHT NOW:
//     pieces finished in production minus pieces already delivered on
//     CONFIRMED delivery notes. Jobs without a production release are
//     uncapped (null) so DNs keep their classic Job-Order behaviour.
//
//   • GetRowDispatchStateAsync(jobOrderId, spec, serial) — the dispatch
//     card figures for one unit row: ready / delivered / balance.
//
// Confirmed = Dispatched / Delivered / Signed / Received
// (DeliveryNoteWorkflow.IsConfirmedDelivery) — in-progress drafts do not
// consume balance, matching DeliveryNoteCreateService rule 5.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    public static class ProductionDeliveryGateway
    {
        /// <summary>
        /// Deliverable cap per (Spec, Serial) row for one Job Order:
        /// production-finished pieces minus confirmed-delivered pieces.
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

                var caps = new Dictionary<(int, int), int>();
                foreach (var kv in ready)
                {
                    delivered.TryGetValue(kv.Key, out int alreadyOut);
                    caps[kv.Key] = Math.Max(0, kv.Value.Ready - alreadyOut);
                }
                return caps;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ProductionDeliveryGateway] caps failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Dispatch card figures for one unit row.</summary>
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
                delivered = Math.Min(delivered, row.Ready);

                return (row.Ready, delivered, Math.Max(0, row.Ready - delivered), true);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ProductionDeliveryGateway] row state failed: " + ex.Message);
                return (0, 0, 0, false);
            }
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
