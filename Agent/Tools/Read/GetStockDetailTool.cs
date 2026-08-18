using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Nlu;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>
    /// Stock breakdown from the real Sheet Store fields:
    ///   Stock  = received / purchased quantity
    ///   Used   = consumed
    ///   Balance = Stock − Used (same as the module)
    /// There is no separate wastage ledger on sheets — that is stated, not invented.
    /// </summary>
    public sealed class GetStockDetailTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_stock_detail",
            Description = "Report purchased, consumed and available quantity for a glass type",
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
                        "I could not read the Sheet Store, so I cannot give stock figures.");

                var query = ErpQueryParser.Parse(argument);
                ConversationSlots.ApplyMissing(query);

                if (query.ThicknessMm.HasValue)
                    sheets = sheets.Where(s => s.ThicknessMm == query.ThicknessMm.Value).ToList();
                if (!string.IsNullOrWhiteSpace(query.Color))
                {
                    sheets = sheets.Where(s =>
                        ReadToolHelpers.Hit(s.Color, query.Color) ||
                        ReadToolHelpers.Hit(s.Category, query.Color)).ToList();
                }

                string term = (argument ?? "").Trim();
                if (sheets.Count == 0 && term.Length > 0)
                {
                    sheets = (await repo.GetAllAsync().ConfigureAwait(false))
                        .Where(s =>
                            ReadToolHelpers.Hit(s.Color, term) ||
                            ReadToolHelpers.Hit(s.Category, term) ||
                            ReadToolHelpers.Hit(s.ThicknessDisplay, term) ||
                            ReadToolHelpers.Hit(s.Supplier, term))
                        .ToList();
                }

                if (sheets.Count == 0)
                {
                    return ToolResult.Ok(query.HasMaterialHint
                        ? $"No sheet types match {query.FilterText()}."
                        : "The Sheet Store has no matching sheet types.");
                }

                if (query.HasMaterialHint == false
                    && query.Metric != ErpMetric.LowStock
                    && sheets.Select(s => s.Color + s.ThicknessMm).Distinct().Count() > 4
                    && term.Length == 0)
                {
                    return ToolResult.Ok(
                        "Which glass do you mean? Give a thickness and colour — " +
                        "for example 6mm Clear.");
                }

                int purchased = sheets.Sum(s => s.Stock);
                int consumed = sheets.Sum(s => s.Used);
                int available = sheets.Sum(s => Math.Max(0, s.Stock - s.Used));
                decimal availSqm = sheets.Sum(s => s.SqmPerSheet * Math.Max(0, s.Stock - s.Used));

                var sb = new StringBuilder();
                string label = query.HasMaterialHint ? query.FilterText() : "matching sheets";
                sb.AppendLine($"Sheet Store figures for {label} (same fields as the module):");
                sb.AppendLine($"• Purchased / received (Stock): {purchased} sheet(s)");
                sb.AppendLine($"• Consumed (Used): {consumed} sheet(s)");
                sb.AppendLine($"• Available (Stock − Used): {available} sheet(s), {availSqm:N2} SQM");

                if (query.Metric == ErpMetric.Wastage)
                {
                    sb.AppendLine(
                        "There is no separate wastage ledger on sheet stock. " +
                        "Used is production/consumption, not breakage.");
                }

                var suppliers = sheets
                    .Where(s => !string.IsNullOrWhiteSpace(s.Supplier))
                    .Select(s => s.Supplier)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(6)
                    .ToList();
                if (suppliers.Count > 0)
                    sb.AppendLine("• Supplier(s): " + string.Join(", ", suppliers));

                var lastBuy = sheets
                    .Where(s => s.LastPurchaseDate.HasValue)
                    .Select(s => s.LastPurchaseDate!.Value)
                    .DefaultIfEmpty()
                    .Max();
                if (lastBuy != default)
                    sb.AppendLine($"• Last purchase date on file: {lastBuy:dd MMM yyyy}");

                foreach (var s in sheets.Take(8))
                {
                    sb.AppendLine(
                        $"• {s.ThicknessDisplay} {s.Color} {s.SizeDisplay} — " +
                        $"stock {s.Stock}, used {s.Used}, left {Math.Max(0, s.Balance)}");
                }

                ConversationSlots.Remember(query, Definition.Name);
                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[get_stock_detail] {ex.Message}");
                return ToolResult.Fail(
                    "I could not read the Sheet Store, so I cannot give stock figures.");
            }
        }
    }
}
