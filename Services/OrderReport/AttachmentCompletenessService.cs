// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/OrderReport/AttachmentCompletenessService.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — attachment completeness (2026-09-06)
//
// PURPOSE:
//   Counts REAL attachments across the three document kinds in one order
//   family and detects the user's headline scenario:
//     "3 delivery notes prepared but only 2 have attachments
//      → 1 documents missing in attachments of delivery note".
//
//   Sources (all real persisted data — never fabricated):
//     • PI   → ProformaAttachmentService manifest.json
//     • DN   → DeliveryNoteRepository.GetAttachmentCountsAsync (disk files)
//     • TI   → TaxInvoiceRepository.GetAttachmentCountsAsync (DB blobs)
//
//   Stateless-ish: holds repository references; safe to reuse.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.OrderReport
{
    public class AttachmentCompletenessService
    {
        private readonly ProformaAttachmentService _proformaAttachments;
        private readonly IDeliveryNoteRepository _deliveryNotes;
        private readonly ITaxInvoiceRepository _taxInvoices;

        public AttachmentCompletenessService(
            ProformaAttachmentService? proformaAttachments = null,
            IDeliveryNoteRepository? deliveryNotes = null,
            ITaxInvoiceRepository? taxInvoices = null)
        {
            _proformaAttachments = proformaAttachments ?? new ProformaAttachmentService();
            _deliveryNotes = deliveryNotes ?? App.DeliveryNotes;
            _taxInvoices = taxInvoices ?? App.TaxInvoices;
        }

        /// <summary>
        /// Computes attachment completeness for one Proforma family.
        /// </summary>
        /// <param name="proformaId">Proforma id (PI attachment folder + manifest).</param>
        /// <param name="preparedDeliveryNoteIds">
        /// Confirmed (non-master, non-dummy) DN ids for this order. These are
        /// the "prepared" documents the user expects to be attached.
        /// </param>
        /// <param name="taxInvoiceNumbers">Tax invoice numbers in the family.</param>
        public async Task<OrderReportAttachmentCompleteness> ComputeAsync(
            string proformaId,
            IReadOnlyList<string> preparedDeliveryNoteIds,
            IReadOnlyList<string> taxInvoiceNumbers)
        {
            var result = new OrderReportAttachmentCompleteness();

            // ── PI documents (Step 8 manifest) ──────────────────────────
            try
            {
                var piRecords = await _proformaAttachments.LoadManifestAsync(proformaId);
                result.ProformaAttachmentCount = piRecords?.Count ?? 0;
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    $"[OrderReport] PI attachment count failed for {proformaId}: {ex.Message}");
            }

            // ── Delivery Notes — prepared vs attached ───────────────────
            var dnIds = (preparedDeliveryNoteIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.DeliveryNotesPrepared = dnIds.Count;

            if (dnIds.Count > 0)
            {
                try
                {
                    var counts = await _deliveryNotes.GetAttachmentCountsAsync(dnIds);
                    foreach (var id in dnIds)
                    {
                        int count = counts.TryGetValue(id, out int c) ? c : 0;
                        if (count > 0) result.DeliveryNotesAttached++;
                        else result.MissingDeliveryNoteIds.Add(id);
                    }
                }
                catch (Exception ex)
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        $"[OrderReport] DN attachment counts failed for {proformaId}: {ex.Message}");
                }
            }

            // ── Tax Invoices ────────────────────────────────────────────
            var tiNumbers = (taxInvoiceNumbers ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tiNumbers.Count > 0)
            {
                try
                {
                    var counts = await _taxInvoices.GetAttachmentCountsAsync(tiNumbers);
                    result.TaxInvoiceAttachmentCount =
                        tiNumbers.Sum(n => counts.TryGetValue(n, out int c) ? c : 0);
                }
                catch (Exception ex)
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        $"[OrderReport] TI attachment counts failed for {proformaId}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
