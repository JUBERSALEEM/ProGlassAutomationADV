// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Navigation/NavigateTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The agent's only way to change what is on screen. It cannot touch the
// shell directly: navigation is an injected Action<string> that routes
// through MainViewModel.Navigate, the same path the sidebar uses.
//
// Only keys in KnownTargets are accepted. An unknown key is refused with
// a truthful message rather than passed through to the shell.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Navigation
{
    /// <summary>Switches the active module.</summary>
    public sealed class NavigateTool : IAgentTool
    {
        private readonly Action<string>? _navigate;

        /// <summary>
        /// Navigation keys the agent may use. These must exist as cases in
        /// MainViewModel.Navigate().
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> KnownTargets =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Dashboard"] = "Dashboard",
                ["SheetStore"] = "Sheet Store",
                ["ProformaInvoice"] = "Proforma Invoice",
                ["JobOrder"] = "Job Order",
                ["DeliveryNote"] = "Delivery Note",
                ["TaxInvoice"] = "Tax Invoice",
                ["SalesReport"] = "Sales Report",
                ["CompanyCenter"] = "Company Center",
                ["UserCenter"] = "User Center",
                ["JobProfitability"] = "Job Profitability",
                ["Settings"] = "Settings"
            };

        public NavigateTool(Action<string>? navigate)
        {
            _navigate = navigate;
        }

        public ToolDefinition Definition { get; } = new()
        {
            Name = "navigate",
            Description = "Open a module screen",
            Risk = ToolRisk.Navigation,
            RequiredPermission = "",     // changing view touches no data
            RequiresConfirmation = false
        };

        public Task<ToolResult> ExecuteAsync(string argument)
        {
            string target = (argument ?? "").Trim();

            if (target.Length == 0)
                return Task.FromResult(ToolResult.Fail(
                    "You did not say which screen to open."));

            if (!KnownTargets.TryGetValue(target, out var label))
            {
                return Task.FromResult(ToolResult.Fail(
                    $"I do not know a screen called '{target}'. I can open: " +
                    string.Join(", ", KnownTargets.Values) + "."));
            }

            if (_navigate == null)
            {
                return Task.FromResult(ToolResult.Fail(
                    "I am not connected to the window right now, so I cannot " +
                    "change the screen."));
            }

            try
            {
                _navigate(target);
                return Task.FromResult(ToolResult.Ok($"Opened {label}.", target));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NavigateTool] failed: {ex.Message}");
                return Task.FromResult(ToolResult.Fail(
                    $"I could not open {label}."));
            }
        }
    }
}
