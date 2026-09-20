// Distinguishes missing / corrupt / valid-empty JO snapshots so Production
// never treats corrupt JSON as "a job with no glass". Does not change the
// frozen PI mapper (corrupt PI JSON still deserializes to empty there).

using System.Linq;
using System.Text.Json;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;

namespace ProGlassAutomation.Services.Production
{
    public enum ProductionSnapshotKind
    {
        Missing,
        Corrupt,
        ValidEmpty,
        Valid
    }

    public static class ProductionSnapshotIntegrity
    {
        public static ProductionSnapshotKind ReadJobOrderJson(
            string? json,
            out ProformaInvoiceSnapshot? invoice,
            out string message)
        {
            invoice = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                message = "snapshot is missing";
                return ProductionSnapshotKind.Missing;
            }

            try
            {
                var jo = JsonSerializer.Deserialize<JobOrderSnapshot>(json);
                invoice = jo?.EmbeddedInvoice;
                if (invoice == null)
                {
                    message = "embedded invoice is missing";
                    return ProductionSnapshotKind.Missing;
                }

                bool hasCutRow = invoice.SpecGroups != null
                    && invoice.SpecGroups.Any(g =>
                        g?.ItemRows != null
                        && g.ItemRows.Any(r => r != null && r.W1 > 0 && r.H1 > 0));

                if (!hasCutRow)
                {
                    message = "no specification rows";
                    return ProductionSnapshotKind.ValidEmpty;
                }

                message = string.Empty;
                return ProductionSnapshotKind.Valid;
            }
            catch (JsonException)
            {
                message = "snapshot JSON is corrupt — not a valid empty job";
                invoice = null;
                return ProductionSnapshotKind.Corrupt;
            }
        }
    }
}
