// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/ReconciliationSpecGroup.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator — Review step reconciliation grouping
//
// Groups the flattened reconciliation line items by specification so the
// Review step can render a DataGrid-like layout:
//   specification header → row data (single line) → summary (total qty + sqm).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Reconciliation;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    /// <summary>
    /// One specification group (e.g. "6mm Clear Tempered") with its line rows
    /// and rollup totals for the summary footer.
    /// </summary>
    public sealed class ReconciliationSpecGroup
    {
        public string SpecGroupName { get; init; } = "";
        public int SpecGroupNumber { get; init; }

        public ObservableCollection<OrderReconciliationRow> Rows { get; } = new();

        // ── Rollups ────────────────────────────────────────────────────────
        public int TotalOrderedQty => Rows.Sum(r => r.OrderedQty);
        public int TotalDeliveredQty => Rows.Sum(r => r.DeliveredQty);
        public int TotalInvoicedQty => Rows.Sum(r => r.InvoicedQty);
        public int TotalBalanceQty => Rows.Sum(r => r.BalanceQty);
        public decimal TotalSqm => Rows.Sum(r => r.TotalSqm);

        public static List<ReconciliationSpecGroup> Build(
            IEnumerable<OrderReconciliationRow> rows)
        {
            return rows
                .GroupBy(r => r.SpecGroupName, System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.First().SpecGroupNumber)
                .Select(g =>
                {
                    var group = new ReconciliationSpecGroup
                    {
                        SpecGroupName = string.IsNullOrWhiteSpace(g.Key)
                            ? "Unspecified"
                            : g.Key,
                        SpecGroupNumber = g.First().SpecGroupNumber
                    };
                    foreach (var row in g.OrderBy(r => r.SerialNumber))
                        group.Rows.Add(row);
                    return group;
                })
                .ToList();
        }
    }
}
