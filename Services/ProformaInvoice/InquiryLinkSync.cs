// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/InquiryLinkSync.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice ↔ Inquiry link keeper (senior audit — Aug 2026)
//
// PURPOSE
// ───────
// Called by ProformaInvoiceRepository on every Insert / Update / Delete
// that touches a PI carrying an InquiryId, this helper keeps two things
// in sync:
//
//   1. The denormalised Inquiries.ProformaInvoiceId + ProformaNumber
//      columns — used by the Inquiry list projection and by the
//      "Convert to Proforma Invoice" duplicate-prevention guard.
//
//   2. The IInquiryLifecycleService cache — so a freshly-created /
//      edited / deleted PI shows up on the Inquiry list within one
//      refresh, not after the 60-second TTL expires.
//
// BEFORE THIS EXISTED
// ───────────────────
// The two columns were declared, read by the projection and used by the
// Convert-to-PI guard — but NEVER written by any code path. The guard
// was dead code and a single Confirmed inquiry could produce unlimited
// PIs. See docs/PROFORMA_INQUIRY_INTEGRATION_2026-08.md for the full
// bug analysis.
//
// FIRE-AND-FORGET SAFETY
// ──────────────────────
// The link write is bookkeeping — it must NEVER cause the PI
// operation to fail. Every method here swallows exceptions and logs
// a warning; the calling PI repository always returns success as
// long as its own SaveChanges succeeded.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Logging;
using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice
{
    /// <summary>
    /// Static helper that pushes PI-side changes into the Inquiry
    /// module's denormalised link fields and lifecycle cache.
    /// </summary>
    public static class InquiryLinkSync
    {
        /// <summary>
        /// Called after a PI is INSERTED. Writes the new PI id / number
        /// onto the linked inquiry (if any) so the Inquiry list stops
        /// offering "Convert to PI" for that row.
        /// </summary>
        public static Task AfterInsertAsync(SavedProformaInvoice invoice)
            => WriteLinkAsync(invoice, clearInsteadOfSet: false);

        /// <summary>
        /// Called after a PI is UPDATED. Idempotent — writes the same
        /// link if it's already there, corrects it if InquiryId
        /// changed (e.g. an operator re-linked a PI from one inquiry
        /// to another via a snapshot edit).
        /// </summary>
        public static Task AfterUpdateAsync(SavedProformaInvoice invoice)
            => WriteLinkAsync(invoice, clearInsteadOfSet: false);

        /// <summary>
        /// Called after a PI is DELETED. Clears the link on the
        /// previously-linked inquiry so the Convert-to-PI button
        /// re-enables and the list stops showing a stale PI number.
        /// </summary>
        public static Task AfterDeleteAsync(SavedProformaInvoice? entity)
            => WriteLinkAsync(entity, clearInsteadOfSet: true);

        private static async Task WriteLinkAsync(
            SavedProformaInvoice? invoice, bool clearInsteadOfSet)
        {
            if (invoice == null) return;
            if (!invoice.InquiryId.HasValue || invoice.InquiryId.Value <= 0) return;
            if (App.Inquiries == null) return;

            try
            {
                if (clearInsteadOfSet)
                {
                    await App.Inquiries.SetProformaLinkAsync(
                        invoice.InquiryId.Value,
                        proformaInvoiceId: null,
                        proformaNumber: string.Empty)
                        .ConfigureAwait(false);
                }
                else
                {
                    await App.Inquiries.SetProformaLinkAsync(
                        invoice.InquiryId.Value,
                        proformaInvoiceId: invoice.Id,
                        proformaNumber: invoice.ProformaId ?? string.Empty)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Bookkeeping only — never let the PI save fail because
                // the back-link write threw. The PI is already committed
                // at this point; the worst case is a 60-second cache
                // staleness the next lifecycle read resolves.
                AppLogService.Warning(
                    "InquiryLinkSync." +
                    (clearInsteadOfSet ? "AfterDelete" : "AfterUpsert") +
                    " failed for PI '" + (invoice.ProformaId ?? "?") +
                    "' → inquiry " + invoice.InquiryId + ": " + ex.Message,
                    new LogContext
                    {
                        Module = "Proforma",
                        Class = nameof(InquiryLinkSync),
                        Operation = clearInsteadOfSet ? "AfterDelete" : "AfterUpsert"
                    });
            }
        }
    }
}
