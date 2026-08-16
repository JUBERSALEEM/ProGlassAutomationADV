// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ScreenContextTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// MASTER PROMPT §14/§15 (context), §27 (memory safety),
//                §45/§46 (no screenshots, no hidden recording)
//
// These test the PRIVACY GUARANTEES, not just the happy path. A context
// layer that works but quietly retains more than it claims is worse than
// no context layer, so the assertions below are mostly about what the
// class must NOT do.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Read;
using ProGlassAutomation.Agent.Tools.Registry;
using ProGlassAutomation.Services.AI.AIContext;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    [Collection("AgentSettings")]   // shares static state; must not run parallel
    public class ScreenContextTests
    {
        // ══════════════════════════════════════════════════════════════
        //  IT READS THE APP'S EXISTING TRACKER (§4 — no duplicate hook)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Reads_the_module_from_the_existing_AiScreenContext()
        {
            AgentScreenContext.Clear();
            AiScreenContext.Set("Sheet Store");

            Assert.Equal("Sheet Store", AgentScreenContext.CurrentModule);
        }

        [Fact]
        public void Describe_includes_the_optional_detail_when_a_module_publishes_one()
        {
            AgentScreenContext.Clear();
            AiScreenContext.Set("Proforma Invoice", "editing PI-2026-014");

            string described = AgentScreenContext.Describe();

            Assert.Contains("Proforma Invoice", described);
            Assert.Contains("PI-2026-014", described);
        }

        [Fact]
        public void Describe_is_empty_rather_than_guessing_when_nothing_is_known()
        {
            AgentScreenContext.Clear();
            AiScreenContext.Set("");

            // An invented module name would be a fabricated fact (§30).
            Assert.Equal("", AgentScreenContext.Describe());
        }

        // ══════════════════════════════════════════════════════════════
        //  THE TRAIL IS BOUNDED AND FORGETTABLE
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Trail_is_capped_so_it_cannot_become_a_usage_log()
        {
            AgentScreenContext.Clear();

            for (int i = 0; i < AgentScreenContext.MaxTrail * 3; i++)
            {
                AiScreenContext.Set($"Module {i}");
                AgentScreenContext.Observe();
            }

            Assert.True(
                AgentScreenContext.RecentScreens().Count
                    <= AgentScreenContext.MaxTrail);
        }

        [Fact]
        public void Repeated_observation_of_the_same_screen_records_one_entry()
        {
            AgentScreenContext.Clear();
            AiScreenContext.Set("Dashboard");

            for (int i = 0; i < 5; i++) AgentScreenContext.Observe();

            Assert.Single(AgentScreenContext.RecentScreens());
        }

        [Fact]
        public void Clear_actually_forgets_the_trail()
        {
            AiScreenContext.Set("Tax Invoice");
            AgentScreenContext.Observe();
            AgentScreenContext.Clear();

            Assert.Empty(AgentScreenContext.RecentScreens());
        }

        [Fact]
        public void Recent_screens_returns_a_copy_callers_cannot_mutate()
        {
            AgentScreenContext.Clear();
            AiScreenContext.Set("Job Order");
            AgentScreenContext.Observe();

            var first = AgentScreenContext.RecentScreens();
            var second = AgentScreenContext.RecentScreens();

            Assert.NotSame(first, second);
        }

        // ══════════════════════════════════════════════════════════════
        //  THE TOOL EXPOSES NO ERP DATA
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Screen_tool_is_read_only()
        {
            var def = new GetScreenContextTool().Definition;

            Assert.Equal(ToolRisk.Read, def.Risk);
            Assert.False(def.RequiresConfirmation);
        }

        [Fact]
        public async Task Screen_tool_states_what_it_does_not_collect()
        {
            AiScreenContext.Set("Sheet Store");

            var result = await new GetScreenContextTool().ExecuteAsync("");

            Assert.True(result.Success);

            // The privacy promise must be in the answer itself, not only in
            // a comment the user will never read.
            Assert.Contains("never", result.Message);
            Assert.Contains("screenshot", result.Message);
        }

        [Fact]
        public async Task Screen_tool_admits_when_no_module_is_known()
        {
            AgentScreenContext.Clear();
            AiScreenContext.Set("");

            var result = await new GetScreenContextTool().ExecuteAsync("");

            Assert.Contains("cannot tell", result.Message);
        }

        [Fact]
        public void Registry_still_has_no_destructive_tool()
        {
            var tools = AgentToolRegistry.Build().Values.ToList();

            Assert.Empty(tools.Where(
                t => t.Definition.Risk == ToolRisk.Destructive));

            Assert.Contains(tools,
                t => t.Definition.Name == "get_screen_context");
        }
    }
}
