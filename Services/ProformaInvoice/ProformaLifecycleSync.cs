// After a Job Order, Delivery Note or Tax Invoice is removed, the source
// Proforma Invoice must match what is still on the lists.
//
// Display status (Fully invoiced / delivered / Full job created) is derived
// from remaining related documents. When NONE remain, a confirmed PI is
// stored as Rejected so the list shows Cancelled.
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice
{
    public static class ProformaLifecycleSync
    {
        public static async Task AfterDownstreamChangeAsync(string? proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId) || App.Invoices == null)
                return;

            try
            {
                bool anyJob = false;
                bool anyDelivery = false;
                bool anyTaxInvoice = false;

                if (App.Jobs != null)
                {
                    var jobs = await App.Jobs.GetBySourceProformaIdAsync(proformaId);
                    anyJob = jobs != null && jobs.Count > 0;
                }

                if (App.DeliveryNotes != null)
                {
                    var notes = await App.DeliveryNotes.GetAllAsync();
                    anyDelivery = notes.Any(d =>
                        string.Equals(d.ProformaId, proformaId,
                            StringComparison.OrdinalIgnoreCase));
                }

                if (App.TaxInvoices != null)
                {
                    var invoices = await App.TaxInvoices.GetByProformaIdAsync(proformaId);
                    anyTaxInvoice = invoices != null && invoices.Count > 0;
                }

                if (anyJob || anyDelivery || anyTaxInvoice)
                    return;

                var pi = await App.Invoices.GetByProformaIdAsync(proformaId);
                if (pi == null) return;

                if (IsCancelled(pi.Status))
                    return;

                // Only a confirmed / in-workflow PI becomes Cancelled.
                // A draft that never entered the chain stays Draft.
                if (!IsConfirmed(pi.Status))
                    return;

                pi.Status = "Rejected";
                pi.UpdatedAt = DateTime.Now;

                var snap = ProformaInvoiceMapper.DeserializeSnapshot(pi);
                if (snap.Company != null)
                    snap.Company.ProformaStatus = "Rejected";
                pi.FullSnapshotJson = ProformaInvoiceMapper.SerializeSnapshot(snap);

                await App.Invoices.UpdateAsync(pi);

                AuditService.Log(
                    AuditSections.Proforma, "Invoice cancelled",
                    $"{proformaId} — related job order, delivery note and tax invoice removed",
                    "PI", proformaId);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Proforma lifecycle sync failed.", ex,
                    new LogContext
                    {
                        Module = "Proforma",
                        Class = nameof(ProformaLifecycleSync),
                        Operation = "AfterDownstreamChange"
                    });
            }
        }

        private static bool IsCancelled(string? status) =>
            string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool IsConfirmed(string? status) =>
            string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);
    }
}
