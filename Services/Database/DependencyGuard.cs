// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/DependencyGuard.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — cross-database orphan protection (Phase 14)
//
// The application stores each domain in its own SQLite file with no
// cross-file foreign keys, so a delete can silently orphan dependent
// records in other databases. Before allowing a deletion this guard checks
// the real dependent relationships and reports what would be orphaned.
//
// Relationships covered:
//   Proforma Invoice ──> Job Order(s)  (by SourceProformaId)
//                    ──> Delivery Note(s) (by ProformaId, real DNs only)
//                    ──> Tax Invoice(s) (by ProformaId)
//   Job Order      ──> Delivery Note(s) (by JobOrderId, real DNs only)
//                  ──> Tax Invoice(s)  (by JobOrderId or source ProformaId)
//   Delivery Note  ──> Tax Invoice(s)  (HasTaxInvoice / DeliveryNoteReference)
//
// All queries are read-only. Never throws — returns a result that either
// allows the delete (no dependents) or explains exactly what is blocked.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.TaxInvoice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>Result of a dependency check before deletion.</summary>
    public readonly struct DependencyCheckResult
    {
        public bool HasDependents { get; }
        public string? Message { get; }

        private DependencyCheckResult(bool has, string? message)
        {
            HasDependents = has;
            Message = message;
        }

        public static DependencyCheckResult Ok() => new(false, null);
        public static DependencyCheckResult Blocked(string message) => new(true, message);
    }

    /// <summary>Read-only cross-database dependency checks for deletes.</summary>
    public static class DependencyGuard
    {
        /// <summary>Which document kinds the guard understands.</summary>
        public enum Kind
        {
            ProformaInvoice,
            JobOrder,
            DeliveryNote,
            TaxInvoice,
        }

        /// <summary>
        /// Checks whether deleting the given document would orphan dependent
        /// records in other databases. The result's <c>HasDependents</c> tells
        /// the caller whether to block; the message explains why.
        /// </summary>
        /// <param name="strict">
        /// ERP hardening (Phase 3, G-4). Controls what happens when the
        /// dependency query itself FAILS (a transient DB error), not what it
        /// finds:
        ///   • false (default) — fail OPEN: a failed check returns Ok() so a
        ///     genuinely childless draft is never blocked by an unrelated glitch.
        ///     Correct for pre-posting drafts, and preserves every existing
        ///     caller's behaviour.
        ///   • true — fail CLOSED: a failed check returns Blocked(...) so a
        ///     posted/fiscal parent can NEVER orphan its children just because
        ///     the verification could not be completed. Use for posted parents.
        /// </param>
        public static async Task<DependencyCheckResult> CheckAsync(
            Kind kind, string id, bool strict = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                return DependencyCheckResult.Ok();

            try
            {
                switch (kind)
                {
                    case Kind.ProformaInvoice:
                        return await CheckProformaAsync(id);
                    case Kind.JobOrder:
                        return await CheckJobOrderAsync(id);
                    case Kind.DeliveryNote:
                        return await CheckDeliveryNoteAsync(id);
                    case Kind.TaxInvoice:
                        return DependencyCheckResult.Ok(); // TI is a leaf
                    default:
                        return DependencyCheckResult.Ok();
                }
            }
            catch (Exception ex)
            {
                if (strict)
                {
                    // Posted/fiscal parent: we could not verify dependents, so
                    // we must NOT allow a delete that might orphan them.
                    Logging.AppLogService.Warning(
                        "DependencyGuard strict check failed — blocking to protect a posted parent: "
                        + ex.Message);
                    return DependencyCheckResult.Blocked(
                        "Could not verify related documents right now. "
                        + "For safety this document was not changed — please try again.");
                }

                // Pre-posting draft: a check failure must never block an
                // otherwise valid delete. Fail-open.
                return DependencyCheckResult.Ok();
            }
        }

        private static async Task<DependencyCheckResult> CheckProformaAsync(string piId)
        {
            var parts = new List<string>();

            if (App.Jobs != null)
            {
                var jobs = await App.Jobs.GetBySourceProformaIdAsync(piId);
                // A VOIDED (soft-closed) child no longer blocks the parent.
                var live = (jobs ?? new()).Where(j => !j.IsVoid).ToList();
                if (live.Count > 0)
                    parts.Add("Job Order: " + FormatIds(live.Select(j => j.JobOrderId)));
            }

            if (App.DeliveryNotes != null)
            {
                var notes = await App.DeliveryNotes.GetAllAsync();
                var related = notes
                    .Where(d => !d.IsVoid && string.Equals(d.ProformaId, piId,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.DeliveryNoteId)
                    .ToList();
                if (related.Count > 0)
                    parts.Add("Delivery Note: " + FormatIds(related));
            }

            if (App.TaxInvoices != null)
            {
                var tis = await App.TaxInvoices.GetByProformaIdAsync(piId);
                var live = (tis ?? new()).Where(t => !t.IsVoid).ToList();
                if (live.Count > 0)
                    parts.Add("Tax Invoice: " + FormatIds(live.Select(t => t.InvoiceNumber)));
            }

            return BlockedIfAny(parts);
        }

        private static async Task<DependencyCheckResult> CheckJobOrderAsync(string jobId)
        {
            var parts = new List<string>();
            string? sourcePi = null;

            if (App.Jobs != null)
            {
                var job = await App.Jobs.GetByJobOrderIdAsync(jobId);
                sourcePi = job?.SourceProformaId;
            }

            if (App.DeliveryNotes != null)
            {
                var dns = await App.DeliveryNotes.GetByJobOrderIdAsync(jobId);
                var live = (dns ?? new()).Where(d => !d.IsVoid).ToList();
                if (live.Count > 0)
                    parts.Add("Delivery Note: " + FormatIds(live.Select(d => d.DeliveryNoteId)));
            }

            if (App.TaxInvoices != null)
            {
                var tis = await App.TaxInvoices.GetAllAsync();
                var related = (tis ?? new())
                    .Where(t =>
                        !t.IsVoid &&
                        (string.Equals(t.JobOrderId, jobId,
                            StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(sourcePi) &&
                         string.Equals(t.ProformaId, sourcePi,
                             StringComparison.OrdinalIgnoreCase))))
                    .Select(t => t.InvoiceNumber)
                    .ToList();
                if (related.Count > 0)
                    parts.Add("Tax Invoice: " + FormatIds(related));
            }

            return BlockedIfAny(parts);
        }

        private static async Task<DependencyCheckResult> CheckDeliveryNoteAsync(string dnId)
        {
            var tiIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (App.DeliveryNotes != null)
            {
                var dn = await App.DeliveryNotes.GetByDeliveryNoteIdAsync(dnId);
                if (dn != null &&
                    dn.HasTaxInvoice &&
                    !string.IsNullOrWhiteSpace(dn.TaxInvoiceId))
                    tiIds.Add(dn.TaxInvoiceId);
            }

            if (App.TaxInvoices != null)
            {
                var tis = await App.TaxInvoices.GetAllAsync();
                foreach (var t in tis ?? new())
                {
                    if (t.IsVoid) continue;   // a voided TI no longer blocks its DN
                    var refs = TaxInvoiceDnReconciler.ParseDnRefs(t.DeliveryNoteReference);
                    if (refs.Any(r => string.Equals(r, dnId, StringComparison.OrdinalIgnoreCase)))
                        tiIds.Add(t.InvoiceNumber);
                }
            }

            if (tiIds.Count == 0)
                return DependencyCheckResult.Ok();

            return BlockedIfAny(new List<string>
            {
                "Tax Invoice: " + FormatIds(tiIds)
            });
        }

        private static DependencyCheckResult BlockedIfAny(List<string> parts)
        {
            if (parts.Count == 0)
                return DependencyCheckResult.Ok();

            return DependencyCheckResult.Blocked(
                "Delete the related documents first:\n\n" +
                string.Join("\n", parts));
        }

        private static string FormatIds(IEnumerable<string?> ids)
        {
            return string.Join(", ",
                ids.Where(s => !string.IsNullOrWhiteSpace(s))
                   .Select(s => s!.Trim())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        }
    }
}
