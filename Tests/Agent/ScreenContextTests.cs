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

        // ══════════════════════════════════════════════════════════════
        //  OPT-IN DOCUMENT DETAIL
        // ══════════════════════════════════════════════════════════════

        private sealed class FakeAwareVm : IAgentContextAware
        {
            public string Detail = "";
            public string AgentContextDetail => Detail;
        }

        private sealed class FakeThrowingVm : IAgentContextAware
        {
            public string AgentContextDetail =>
                throw new System.InvalidOperationException("module is broken");
        }

        [Fact]
        public void Live_view_model_detail_wins_over_the_navigation_snapshot()
        {
            // The snapshot goes stale when the user selects a different
            // record without navigating. The live value must take priority.
            AiScreenContext.Set("Sheet Store", "sheet selected: 6mm Clear");

            var vm = new FakeAwareVm { Detail = "sheet selected: 12mm Grey" };
            AgentScreenContext.CurrentViewModelProvider = () => vm;

            try
            {
                Assert.Equal("sheet selected: 12mm Grey",
                    AgentScreenContext.CurrentDetail);
            }
            finally
            {
                AgentScreenContext.CurrentViewModelProvider = null;
            }
        }

        [Fact]
        public void A_module_that_publishes_nothing_reports_nothing()
        {
            AiScreenContext.Set("Dashboard", "");
            AgentScreenContext.CurrentViewModelProvider = () => new FakeAwareVm();

            try
            {
                Assert.Equal("", AgentScreenContext.CurrentDetail);
            }
            finally
            {
                AgentScreenContext.CurrentViewModelProvider = null;
            }
        }

        [Fact]
        public void A_module_that_throws_cannot_break_the_agent()
        {
            // A defect in one module must not take out screen awareness.
            AiScreenContext.Set("Job Order", "");
            AgentScreenContext.CurrentViewModelProvider = () => new FakeThrowingVm();

            try
            {
                Assert.Equal("", AgentScreenContext.CurrentDetail);
            }
            finally
            {
                AgentScreenContext.CurrentViewModelProvider = null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  LIST vs DETAIL — a module must not name a record it is not on
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Mirrors the shape every document module uses: publish the id
        /// ONLY while the detail view is open.
        /// </summary>
        private sealed class FakeDocumentModule : IAgentContextAware
        {
            public bool IsDetailView;
            public string Id = "";

            public string AgentContextDetail =>
                !IsDetailView || string.IsNullOrWhiteSpace(Id)
                    ? ""
                    : $"editing {Id}";
        }

        [Fact]
        public void List_view_publishes_nothing()
        {
            // On a list there is no single record in focus. Naming the
            // highlighted row would make the agent answer about a document
            // the user is not actually looking at.
            var vm = new FakeDocumentModule { IsDetailView = false, Id = "JO-2201" };
            AgentScreenContext.CurrentViewModelProvider = () => vm;

            try
            {
                Assert.Equal("", AgentScreenContext.CurrentDetail);
            }
            finally { AgentScreenContext.CurrentViewModelProvider = null; }
        }

        [Fact]
        public void Detail_view_publishes_the_identifier()
        {
            var vm = new FakeDocumentModule { IsDetailView = true, Id = "JO-2201" };
            AgentScreenContext.CurrentViewModelProvider = () => vm;

            try
            {
                Assert.Equal("editing JO-2201", AgentScreenContext.CurrentDetail);
            }
            finally { AgentScreenContext.CurrentViewModelProvider = null; }
        }

        [Fact]
        public void A_new_unsaved_document_publishes_nothing()
        {
            // A new record has no number yet. An empty string is honest.
            var vm = new FakeDocumentModule { IsDetailView = true, Id = "" };
            AgentScreenContext.CurrentViewModelProvider = () => vm;

            try
            {
                Assert.Equal("", AgentScreenContext.CurrentDetail);
            }
            finally { AgentScreenContext.CurrentViewModelProvider = null; }
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
