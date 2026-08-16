// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Registry/AgentToolRegistry.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The complete, explicit list of everything the agent can do. If a
// capability is not registered here it does not exist for the agent —
// there is no reflection-based discovery and no dynamic registration,
// so the attack surface is exactly this file.
//
// PHASE F ships READ + NAVIGATION tools only. No Write and no
// Destructive tool exists yet; the gates that would govern them are
// implemented and tested, but nothing can currently mutate ERP data
// through the agent.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Navigation;
using ProGlassAutomation.Agent.Tools.Read;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Agent.Tools.Registry
{
    /// <summary>Builds the agent's tool table.</summary>
    public static class AgentToolRegistry
    {
        /// <summary>
        /// Creates every tool, keyed by name.
        /// </summary>
        /// <param name="navigate">
        /// Shell navigation callback. When null the navigation tool is
        /// still registered but reports truthfully that it cannot move.
        /// </param>
        public static Dictionary<string, IAgentTool> Build(
            Action<string>? navigate = null)
        {
            var tools = new List<IAgentTool>
            {
                // ── Navigation ────────────────────────────────────────
                new NavigateTool(navigate),

                // ── Read-only queries over existing repositories ──────
                new SearchProformaTool(),
                new SearchJobOrderTool(),
                new SearchDeliveryTool(),
                new SearchTaxInvoiceTool(),
                new SearchCustomerTool(),
                new GetInventoryTool(),
                new GetActivityTool(),

                // PHASE N — reads the profit Step 9 already computed and
                // the mapper already persisted, so the agent and the Job
                // Profitability module can never quote different numbers.
                new GetProfitabilityTool()
            };

            var map = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                map[tool.Definition.Name] = tool;

            return map;
        }

        /// <summary>Human-readable capability list for the help response.</summary>
        public static string Describe(Dictionary<string, IAgentTool> tools)
        {
            if (tools == null || tools.Count == 0)
                return "No tools are registered.";

            var lines = new List<string>();
            foreach (var tool in tools.Values)
                lines.Add($"• {tool.Definition.Name} — {tool.Definition.Description}");

            return string.Join("\n", lines);
        }
    }
}
