// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Registry/AgentToolRegistry.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The complete, explicit list of everything the agent can do. If a
// capability is not registered here it does not exist for the agent —
// there is no reflection-based discovery and no dynamic registration,
// so the attack surface is exactly this file.
//
// PHASE F shipped READ + NAVIGATION only. PHASE Q adds exactly ONE
// Write tool (update_sheet_stock) after Phase P proved the permission
// and confirmation gates with a fake write tool in the test assembly.
//
// There is still NO Destructive tool. Nothing the agent can reach
// deletes a record.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Navigation;
using ProGlassAutomation.Agent.Tools.Read;
using ProGlassAutomation.Agent.Tools.Write;
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
                new GetProfitabilityTool(),

                // Document chain: PI -> Job Order -> Delivery -> Tax Invoice.
                // Relationships come from the entities' own linkage fields,
                // and every hop reuses the App.* repository the rest of the
                // ERP uses - no second query layer, no DbContext.
                new GetDocumentChainTool(),

                // Screen + theme awareness (§14/§15/§16). Exposes NO ERP
                // data - only the module name already tracked by the app's
                // own AiScreenContext, plus the accent colour. Reading the
                // theme is safe; this tool cannot change it (§17/§44).
                new GetScreenContextTool(),

                // Statements (§9). READS saved statements only - it never
                // calls StatementAggregator.BuildAsync, which would mint a
                // new statement number. Gated on TaxInvoice.View, not
                // Statement.Generate: that one is a WRITE right.
                new GetStatementTool(),

                // ── Write (PHASE Q) ───────────────────────────────────
                //
                // The FIRST write capability the agent has ever had.
                // Reaching it requires, in order: the tool being listed
                // here, the acting user matching the signed-in user, the
                // SheetStore.Edit permission, AND an explicit Yes in a
                // confirmation dialog. Every one of those gates is now
                // covered by Tests/Agent.
                //
                // Scope is deliberately one sheet and one quantity per
                // call, capped, reversible, and audited with before/after
                // values. No deletion, no pricing, no bulk operations.
                new UpdateSheetStockTool()
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
