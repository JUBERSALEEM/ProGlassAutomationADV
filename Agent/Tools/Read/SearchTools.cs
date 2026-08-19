// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/SearchTools.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// READ-ONLY document lookups. Each tool:
//   • declares the AppPermissions key the user must hold,
//   • queries an EXISTING repository through its public API,
//   • caps its result list,
//   • and is TRUTHFUL on failure — "I could not read the job orders"
//     rather than a silent empty answer that reads like "there are none".
//
// That last property is the one that matters: an agent that says "no
// results" when the database was unreachable is worse than useless.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Shared helpers for the read tools.</summary>
    internal static class ReadToolHelpers
    {
        /// <summary>Maximum rows any single tool reports.</summary>
        public const int MaxRows = 10;

        public static bool Hit(string? value, string term) =>
            (value ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        public static string Format(string header, List<string> lines, int total)
        {
            if (lines.Count == 0) return header + " nothing matched.";

            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var line in lines) sb.AppendLine("• " + line);

            if (total > lines.Count)
                sb.AppendLine($"({lines.Count} of {total} shown.)");

            return sb.ToString().TrimEnd();
        }

        public static ToolResult Unavailable(string what) =>
            ToolResult.Fail(
                $"I could not read the {what} right now, so I cannot answer that " +
                "reliably.");
    }

    // ══════════════════════════════════════════════════════════════════
    //  PROFORMA
    // ══════════════════════════════════════════════════════════════════

    public sealed class SearchProformaTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "search_proforma",
            Description = "Search proforma invoices by number, customer or project",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.ProformaView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.Invoices == null)
                    return ReadToolHelpers.Unavailable("proforma invoices");

                var all = await App.Invoices.GetAllAsync().ConfigureAwait(false);

                var matches = term.Length == 0
                    ? all.OrderByDescending(p => p.UpdatedAt).ToList()
                    : all.Where(p =>
                        ReadToolHelpers.Hit(p.ProformaId, term) ||
                        ReadToolHelpers.Hit(p.CustomerName, term) ||
                        ReadToolHelpers.Hit(p.ProjectName, term)).ToList();

                var lines = matches.Take(ReadToolHelpers.MaxRows)
                    .Select(p =>
                        $"{p.ProformaId} — {p.CustomerName} — {p.ProjectName} — " +
                        $"{p.Status} — AED {p.GrandTotalVatInclusive:N2}")
                    .ToList();

                return ToolResult.Ok(ReadToolHelpers.Format(
                    term.Length == 0
                        ? "Recent proforma invoices:"
                        : $"Proforma invoices matching '{term}':",
                    lines, matches.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[search_proforma] {ex.Message}");
                return ReadToolHelpers.Unavailable("proforma invoices");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  JOB ORDER
    // ══════════════════════════════════════════════════════════════════

    public sealed class SearchJobOrderTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "search_joborder",
            Description = "Search job orders by number, customer or project",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.JobOrderView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.Jobs == null)
                    return ReadToolHelpers.Unavailable("job orders");

                var all = await App.Jobs.GetAllAsync().ConfigureAwait(false);

                var matches = term.Length == 0
                    ? all.OrderByDescending(j => j.UpdatedAt).ToList()
                    : all.Where(j =>
                        ReadToolHelpers.Hit(j.JobOrderId, term) ||
                        ReadToolHelpers.Hit(j.CustomerName, term) ||
                        ReadToolHelpers.Hit(j.ProjectName, term)).ToList();

                var lines = matches.Take(ReadToolHelpers.MaxRows)
                    .Select(j =>
                        $"{j.JobOrderId} — {j.CustomerName} — {j.ProjectName} — " +
                        $"{j.Status}")
                    .ToList();

                return ToolResult.Ok(ReadToolHelpers.Format(
                    term.Length == 0
                        ? "Recent job orders:"
                        : $"Job orders matching '{term}':",
                    lines, matches.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[search_joborder] {ex.Message}");
                return ReadToolHelpers.Unavailable("job orders");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DELIVERY NOTE
    // ══════════════════════════════════════════════════════════════════

    public sealed class SearchDeliveryTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "search_delivery",
            Description = "Search delivery notes by number, customer or job order",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.DeliveryNoteView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.DeliveryNotes == null)
                    return ReadToolHelpers.Unavailable("delivery notes");

                var all = await App.DeliveryNotes.GetAllAsync().ConfigureAwait(false);

                var matches = term.Length == 0
                    ? all.OrderByDescending(d => d.UpdatedAt).ToList()
                    : all.Where(d =>
                        ReadToolHelpers.Hit(d.DeliveryNoteId, term) ||
                        ReadToolHelpers.Hit(d.CustomerName, term) ||
                        ReadToolHelpers.Hit(d.JobOrderId, term)).ToList();

                var lines = matches.Take(ReadToolHelpers.MaxRows)
                    .Select(d =>
                        $"{d.DeliveryNoteId} — {d.CustomerName} — {d.Status} — " +
                        $"delivered {d.TotalDeliveredQty}, balance {d.TotalBalanceQty}")
                    .ToList();

                return ToolResult.Ok(ReadToolHelpers.Format(
                    term.Length == 0
                        ? "Recent delivery notes:"
                        : $"Delivery notes matching '{term}':",
                    lines, matches.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[search_delivery] {ex.Message}");
                return ReadToolHelpers.Unavailable("delivery notes");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  TAX INVOICE
    // ══════════════════════════════════════════════════════════════════

    public sealed class SearchTaxInvoiceTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "search_taxinvoice",
            Description = "Search tax invoices by number, customer or proforma",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.TaxInvoiceView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.TaxInvoices == null)
                    return ReadToolHelpers.Unavailable("tax invoices");

                var all = await App.TaxInvoices.GetAllAsync().ConfigureAwait(false);

                var matches = term.Length == 0
                    ? all.OrderByDescending(t => t.UpdatedAt).ToList()
                    : all.Where(t =>
                        ReadToolHelpers.Hit(t.InvoiceNumber, term) ||
                        ReadToolHelpers.Hit(t.CustomerName, term) ||
                        ReadToolHelpers.Hit(t.ProformaId, term)).ToList();

                var lines = matches.Take(ReadToolHelpers.MaxRows)
                    .Select(t =>
                        $"{t.InvoiceNumber} — {t.CustomerName} — {t.Status}")
                    .ToList();

                return ToolResult.Ok(ReadToolHelpers.Format(
                    term.Length == 0
                        ? "Recent tax invoices:"
                        : $"Tax invoices matching '{term}':",
                    lines, matches.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[search_taxinvoice] {ex.Message}");
                return ReadToolHelpers.Unavailable("tax invoices");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  CUSTOMER
    // ══════════════════════════════════════════════════════════════════

    public sealed class SearchCustomerTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "search_customer",
            Description = "Search saved customers by name, TRN or sales rep",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.ProformaView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.MasterData == null)
                    return ReadToolHelpers.Unavailable("customer records");

                var all = await App.MasterData.GetAllClientsAsync()
                    .ConfigureAwait(false);

                var matches = term.Length == 0
                    ? all.OrderByDescending(c => c.LastUsedAt).ToList()
                    : all.Where(c =>
                        ReadToolHelpers.Hit(c.CustomerName, term) ||
                        ReadToolHelpers.Hit(c.CustomerTrn, term) ||
                        ReadToolHelpers.Hit(c.SalesRepresentative, term)).ToList();

                var lines = matches.Take(ReadToolHelpers.MaxRows)
                    .Select(c =>
                        $"{c.CustomerName} — TRN {c.CustomerTrn} — rep " +
                        $"{c.SalesRepresentative}")
                    .ToList();

                return ToolResult.Ok(ReadToolHelpers.Format(
                    $"Customers matching '{term}':", lines, matches.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[search_customer] {ex.Message}");
                return ReadToolHelpers.Unavailable("customer records");
            }
        }
    }
}
