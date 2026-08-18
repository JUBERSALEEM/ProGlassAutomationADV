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
//                    ──> Tax Invoice(s) (by ProformaId)
//   Job Order      ──> Delivery Note(s) (by JobOrderId)
//   Delivery Note  ──> Tax Invoice(s)  (via its ProformaId; DN may be invoiced)
//
// All queries are read-only. Never throws — returns a result that either
// allows the delete (no dependents) or explains exactly what is blocked.
// ═══════════════════════════════════════════════════════════════════════

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
        /// records in other databases. When <paramref name="blockOnDependents"/>
        /// is true the caller should block; the message explains why.
        /// </summary>
        public static async Task<DependencyCheckResult> CheckAsync(
            Kind kind, string id)
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
                        return DependencyCheckResult.Ok(); // TI is a leaf for now
                    default:
                        return DependencyCheckResult.Ok();
                }
            }
            catch
            {
                // A dependency check must never block an otherwise valid
                // delete because the check itself failed. Fail-open here.
                return DependencyCheckResult.Ok();
            }
        }

        private static async Task<DependencyCheckResult> CheckProformaAsync(string piId)
        {
            if (App.Jobs != null)
            {
                var jobs = await App.Jobs.GetBySourceProformaIdAsync(piId);
                if (jobs != null && jobs.Count > 0)
                    return DependencyCheckResult.Blocked(
                        $"This invoice has {jobs.Count} job order(s). " +
                        "Delete or reject them first to preserve the workflow history.");
            }

            if (App.TaxInvoices != null)
            {
                var tis = await App.TaxInvoices.GetByProformaIdAsync(piId);
                if (tis != null && tis.Count > 0)
                    return DependencyCheckResult.Blocked(
                        $"This invoice has {tis.Count} tax invoice(s). " +
                        "Cancelling them is required before deletion.");
            }

            return DependencyCheckResult.Ok();
        }

        private static async Task<DependencyCheckResult> CheckJobOrderAsync(string jobId)
        {
            if (App.DeliveryNotes != null)
            {
                var dns = await App.DeliveryNotes.GetByJobOrderIdAsync(jobId);
                if (dns != null && dns.Count > 0)
                    return DependencyCheckResult.Blocked(
                        $"This job order has {dns.Count} delivery note(s). " +
                        "Delete or cancel them first.");
            }

            return DependencyCheckResult.Ok();
        }

        private static async Task<DependencyCheckResult> CheckDeliveryNoteAsync(string dnId)
        {
            // A DN is tied to a tax invoice if it carries an invoice number.
            if (App.DeliveryNotes != null)
            {
                var dn = await App.DeliveryNotes.GetByDeliveryNoteIdAsync(dnId);
                if (dn != null && dn.HasTaxInvoice && !string.IsNullOrWhiteSpace(dn.TaxInvoiceId))
                    return DependencyCheckResult.Blocked(
                        $"This delivery note is invoiced as '{dn.TaxInvoiceId}'. " +
                        "Cancel the tax invoice before deleting the delivery note.");
            }

            return DependencyCheckResult.Ok();
        }
    }
}
