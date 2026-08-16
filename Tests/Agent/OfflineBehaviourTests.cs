// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OfflineBehaviourTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// MASTER PROMPT §48 (testing matrix), §49 (offline test), §24 (financial
//                data protection), §34 (no whole-database payloads),
//                §30 (no fabrication)
//
// ─────────────────────────────────────────────────────────────────────
//  WHAT THIS FILE PROVES, AND WHAT IT CANNOT
// ─────────────────────────────────────────────────────────────────────
//  SecurityBoundaryTests already covers the egress gate: local-only,
//  consent, allow-list, payload cap, redaction. Those are about the ONE
//  path that is allowed to reach the network.
//
//  This file covers the other half of §49: the TOOL layer. Every agent
//  tool must work — or refuse honestly — with no network and no database.
//  These run with App.* repositories unset, which is exactly the state a
//  test host is in, and is a faithful stand-in for "the database is
//  unreachable".
//
//  HONEST LIMIT: a unit test cannot prove no socket was opened. It proves
//  the tools do not REQUIRE the network and do not crash without it. The
//  genuine offline test — pull the cable, launch the app, use it — stays
//  on the manual regression checklist, and no assertion here should be
//  read as a substitute for it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OfflineBehaviourTests
    {
        private static IEnumerable<IAgentTool> ReadTools() =>
            AgentToolRegistry.Build().Values
                .Where(t => t.Definition.Risk == ToolRisk.Read);

        // ══════════════════════════════════════════════════════════════
        //  EVERY READ TOOL SURVIVES WITH NO DATABASE
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public async Task No_read_tool_throws_when_the_database_is_unavailable()
        {
            // A tool that throws takes down the chat turn and produces a
            // stack trace instead of an answer. Every one must degrade to a
            // truthful refusal instead.
            var failures = new List<string>();

            foreach (var tool in ReadTools())
            {
                try
                {
                    var result = await tool.ExecuteAsync("");

                    if (result == null)
                        failures.Add($"{tool.Definition.Name} returned null");
                }
                catch (Exception ex)
                {
                    failures.Add($"{tool.Definition.Name} threw {ex.GetType().Name}");
                }
            }

            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Fact]
        public async Task Every_read_tool_answers_with_words_not_silence()
        {
            // An empty message is indistinguishable from a hang. Whether it
            // succeeds or refuses, a tool must say something.
            var silent = new List<string>();

            foreach (var tool in ReadTools())
            {
                var result = await tool.ExecuteAsync("");

                if (string.IsNullOrWhiteSpace(result?.Message))
                    silent.Add(tool.Definition.Name);
            }

            Assert.True(silent.Count == 0,
                "Tool(s) returned an empty message: " + string.Join(", ", silent));
        }

        [Fact]
        public async Task Read_tools_never_report_success_with_invented_data()
        {
            // The dangerous failure is not an error - it is a confident
            // answer built from nothing. With no database, any tool that
            // claims success must not be quoting figures.
            foreach (var tool in ReadTools())
            {
                var result = await tool.ExecuteAsync("");
                if (result is not { Success: true }) continue;

                // "AED" appearing with no database behind it would mean a
                // fabricated monetary figure.
                Assert.False(
                    result.Message.Contains("AED", StringComparison.Ordinal),
                    $"{tool.Definition.Name} reported a currency figure with no " +
                    "database available");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  NOTHING LEAKS THROUGH A TOOL RESULT
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public async Task Tool_results_are_small_enough_to_never_be_a_bulk_dump()
        {
            // §34: an answer is a summary, not a database export. A tool
            // that can return unbounded text is a tool that can quietly
            // hand a whole table to a cloud provider later.
            foreach (var tool in ReadTools())
            {
                var result = await tool.ExecuteAsync("");

                Assert.True((result?.Message?.Length ?? 0) < 8000,
                    $"{tool.Definition.Name} returned an oversized payload");
            }
        }

        [Fact]
        public void No_tool_declares_itself_free_of_a_permission_when_it_reads_erp_data()
        {
            // get_screen_context is the ONLY tool allowed to have no
            // permission, because it exposes no ERP data - just the module
            // name and theme. Any other permissionless read tool would be
            // a hole.
            var permissionless = AgentToolRegistry.Build().Values
                .Where(t => t.Definition.Risk == ToolRisk.Read)
                .Where(t => string.IsNullOrWhiteSpace(t.Definition.RequiredPermission))
                .Select(t => t.Definition.Name)
                .ToList();

            Assert.Equal(new[] { "get_screen_context" }, permissionless);
        }

        // ══════════════════════════════════════════════════════════════
        //  RISK POSTURE
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void The_registry_is_still_overwhelmingly_read_only()
        {
            var tools = AgentToolRegistry.Build().Values.ToList();

            int reads = tools.Count(t => t.Definition.Risk == ToolRisk.Read);
            int writes = tools.Count(t => t.Definition.Risk == ToolRisk.Write);

            Assert.Empty(tools.Where(
                t => t.Definition.Risk == ToolRisk.Destructive));

            Assert.True(reads > writes * 5,
                $"Risk balance shifted: {reads} read vs {writes} write");
        }

        [Fact]
        public void Every_write_tool_requires_permission_and_confirmation()
        {
            var writes = AgentToolRegistry.Build().Values
                .Where(t => t.Definition.Risk is ToolRisk.Write
                                              or ToolRisk.Destructive);

            Assert.All(writes, t =>
            {
                Assert.False(string.IsNullOrWhiteSpace(
                    t.Definition.RequiredPermission));
                Assert.True(t.Definition.RequiresConfirmation);
            });
        }
    }
}
