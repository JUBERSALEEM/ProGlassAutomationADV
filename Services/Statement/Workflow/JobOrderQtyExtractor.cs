// ═══════════════════════════════════════════════════════════════════════
//  Services/Statement/Workflow/JobOrderQtyExtractor.cs
//
//  Pure JSON helpers that sum ordered quantity from a Job Order snapshot.
//  Extracted from StatementWorkflowEngine (Phase 11/12 God-VM split) —
//  behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using System.Text.Json;

namespace ProGlassAutomation.Services.Statement.Workflow
{
    /// <summary>
    /// Extracts ordered quantity from a Job Order snapshot JSON.
    /// </summary>
    public static class JobOrderQtyExtractor
    {
        /// <summary>
        /// Extracts ordered qty from JobOrder snapshot.
        /// Searches 3 JSON paths:
        ///   1. EmbeddedInvoice.SpecGroups (correct for your app)
        ///   2. ProformaInvoice.SpecGroups (fallback)
        ///   3. root.SpecGroups (last resort)
        /// Each group may have ItemRows or Rows (checks both).
        /// </summary>
        public static int ExtractOrderedQtyFromJobOrder(
            Models.JobOrder.SavedJobOrder.SavedJobOrder jo)
        {
            if (string.IsNullOrWhiteSpace(jo.FullSnapshotJson) ||
                jo.FullSnapshotJson == "{}")
                return 0;

            try
            {
                using var doc = JsonDocument.Parse(jo.FullSnapshotJson);
                var root = doc.RootElement;
                int total = 0;

                // Path 1: EmbeddedInvoice.SpecGroups
                if (root.TryGetProperty("EmbeddedInvoice", out var embedded))
                {
                    total = SumQtyFromSpecGroups(embedded);
                    if (total > 0)
                    {
                        Debug.WriteLine(
                            $"[Workflow] Qty from EmbeddedInvoice: {total}");
                        return total;
                    }
                }

                // Path 2: ProformaInvoice.SpecGroups
                if (root.TryGetProperty("ProformaInvoice", out var pi))
                {
                    total = SumQtyFromSpecGroups(pi);
                    if (total > 0)
                    {
                        Debug.WriteLine(
                            $"[Workflow] Qty from ProformaInvoice: {total}");
                        return total;
                    }
                }

                // Path 3: root.SpecGroups
                total = SumQtyFromSpecGroups(root);
                Debug.WriteLine($"[Workflow] Qty from root: {total}");
                return total;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Workflow] Qty extract: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Sums Qty from SpecGroups → (ItemRows or Rows) → Qty
        /// </summary>
        public static int SumQtyFromSpecGroups(JsonElement parent)
        {
            if (!parent.TryGetProperty("SpecGroups", out var groups) ||
                groups.ValueKind != JsonValueKind.Array)
                return 0;

            int total = 0;
            foreach (var group in groups.EnumerateArray())
            {
                JsonElement rows;
                bool hasRows =
                    group.TryGetProperty("ItemRows", out rows) ||
                    group.TryGetProperty("Rows", out rows);

                if (!hasRows || rows.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var row in rows.EnumerateArray())
                {
                    if (row.TryGetProperty("Qty", out var qty) &&
                        qty.TryGetInt32(out int q))
                        total += q;
                }
            }

            return total;
        }
    }
}
