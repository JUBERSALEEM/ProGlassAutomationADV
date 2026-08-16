// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetInventoryTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// "How much glass do we have?" answered from the real Sheet Store.
//
// Available SQM is computed the same way the Sheet Store module does:
//   Σ sheetSqm × (stock − used)
// so the agent can never quote a number that disagrees with the screen.
//
// It also surfaces low stock (balance ≤ 2), because the useful answer to
// "how is stock" is usually "fine, except these".
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Reports stock levels from the Sheet Store.</summary>
    public sealed class GetInventoryTool : IAgentTool
    {
        /// <summary>At or below this balance a sheet counts as low stock.</summary>
        public const int LowStockThreshold = 2;

        /// <summary>Maximum low-stock lines listed.</summary>
        public const int MaxLowStockRows = 10;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_inventory",
            Description = "Report glass stock: sheet types, available SQM and low stock",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.SheetStoreView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            try
            {
                var repo = new SheetStoreRepository();
                var sheets = await repo.GetAllAsync().ConfigureAwait(false);

                if (sheets == null)
                    return ToolResult.Fail(
                        "I could not read the Sheet Store, so I cannot give you " +
                        "stock figures.");

                // Optional filter, e.g. "get_inventory clear".
                string term = (argument ?? "").Trim();
                if (term.Length > 0)
                {
                    sheets = sheets.Where(s =>
                        ReadToolHelpers.Hit(s.Color, term) ||
                        ReadToolHelpers.Hit(s.Category, term) ||
                        ReadToolHelpers.Hit(s.ThicknessDisplay, term)).ToList();
                }

                if (sheets.Count == 0)
                {
                    return ToolResult.Ok(term.Length > 0
                        ? $"No sheet types match '{term}'."
                        : "The Sheet Store has no sheet types yet.");
                }

                int types = sheets.Count;

                // Same maths as the Sheet Store module: balance = stock - used.
                decimal availableSqm = sheets.Sum(s =>
                    s.SqmPerSheet * Math.Max(0, s.Stock - s.Used));

                int totalBalance = sheets.Sum(s => Math.Max(0, s.Stock - s.Used));

                var low = sheets
                    .Where(s => (s.Stock - s.Used) <= LowStockThreshold)
                    .OrderBy(s => s.Stock - s.Used)
                    .Take(MaxLowStockRows)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine(term.Length > 0
                    ? $"Stock matching '{term}':"
                    : "Sheet Store stock:");
                sb.AppendLine($"• {types} sheet type(s)");
                sb.AppendLine($"• {totalBalance} sheet(s) available");
                sb.AppendLine($"• {availableSqm:N2} SQM available");

                if (low.Count > 0)
                {
                    sb.AppendLine($"Low stock (≤ {LowStockThreshold}):");
                    foreach (var s in low)
                    {
                        sb.AppendLine(
                            $"• {s.ThicknessDisplay} {s.Color} {s.SizeDisplay} — " +
                            $"{Math.Max(0, s.Stock - s.Used)} left");
                    }
                }
                else
                {
                    sb.AppendLine("No sheet type is at or below the low-stock level.");
                }

                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[get_inventory] {ex.Message}");
                return ToolResult.Fail(
                    "I could not read the Sheet Store, so I cannot give you stock " +
                    "figures.");
            }
        }
    }
}
