using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Nlu;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>
    /// Purchase / use history from the real SheetHistory ledger
    /// (Action = Bought | Used | Added | Edited).
    /// </summary>
    public sealed class GetStockHistoryTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_stock_history",
            Description = "Report sheet purchase and consumption history from the store ledger",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.SheetStoreView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            try
            {
                var repo = new SheetHistoryRepository();
                var all = await repo.GetAllAsync().ConfigureAwait(false);
                if (all == null)
                    return ToolResult.Fail("I could not read sheet history.");

                var q = ErpQueryParser.Parse(argument);
                ConversationSlots.ApplyMissing(q);

                var rows = all.AsEnumerable();
                if (q.ThicknessMm.HasValue)
                {
                    string mm = q.ThicknessMm.Value.ToString();
                    rows = rows.Where(h =>
                        ReadToolHelpers.Hit(h.SheetDescription, mm) ||
                        ReadToolHelpers.Hit(h.Details, mm));
                }
                if (!string.IsNullOrWhiteSpace(q.Color))
                {
                    rows = rows.Where(h =>
                        ReadToolHelpers.Hit(h.SheetDescription, q.Color) ||
                        ReadToolHelpers.Hit(h.Details, q.Color));
                }

                if (q.Metric == ErpMetric.Purchased)
                    rows = rows.Where(h => h.Action is "Bought" or "Added");
                else if (q.Metric == ErpMetric.Consumed)
                    rows = rows.Where(h => h.Action == "Used");

                var list = rows
                    .OrderByDescending(h => h.Timestamp)
                    .ToList();

                if (list.Count == 0)
                    return ToolResult.Ok("No sheet-history rows match that filter.");

                int bought = list.Where(h => h.Action is "Bought" or "Added")
                    .Sum(h => h.QuantityChanged);
                int used = list.Where(h => h.Action == "Used").Sum(h => h.QuantityChanged);

                string label = q.HasMaterialHint ? q.FilterText() : "matching sheets";
                var sb = new StringBuilder();
                sb.AppendLine($"Sheet history for {label} (Bought / Used ledger):");
                sb.AppendLine($"• Bought/Added qty in matching rows: {bought}");
                sb.AppendLine($"• Used qty in matching rows: {used}");
                foreach (var h in list.Take(12))
                {
                    sb.AppendLine(
                        $"• {h.Timestamp:dd MMM yyyy} {h.Action} {h.QuantityChanged} — " +
                        $"{h.SheetDescription} {h.Details}");
                }
                ConversationSlots.Remember(q, Definition.Name);
                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[get_stock_history] " + ex.Message);
                return ToolResult.Fail("I could not read sheet history.");
            }
        }
    }
}
