// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Planning/AgentPlanner.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Turns the intent DSL produced by IntentDetector into ordered steps.
//
// Deliberately DUMB and total: it does no reasoning, makes no network
// call and never invents a step that was not in the intent. Anything it
// does not recognise becomes a single "answer" step, which routes to the
// reasoning provider — the safe default.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Agent.Planning
{
    /// <summary>Deterministic intent → steps translation.</summary>
    public sealed class AgentPlanner : IAgentPlanner
    {
        public IReadOnlyList<AgentPlanStep> Plan(string intent, string originalText)
        {
            var steps = new List<AgentPlanStep>();
            string value = (intent ?? "").Trim();

            if (value.Length == 0)
            {
                steps.Add(Answer(originalText));
                return steps;
            }

            // help ────────────────────────────────────────────────────────
            if (value.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new AgentPlanStep { Kind = "help" });
                return steps;
            }

            // teach:<trigger>|<action> ────────────────────────────────────
            if (value.StartsWith("teach:", StringComparison.OrdinalIgnoreCase))
            {
                string body = value["teach:".Length..];
                int split = body.IndexOf('|');

                if (split > 0)
                {
                    steps.Add(new AgentPlanStep
                    {
                        Kind = "teach",
                        Target = body[..split].Trim(),
                        Argument = body[(split + 1)..].Trim()
                    });
                    return steps;
                }

                steps.Add(Answer(originalText));
                return steps;
            }

            // approve:<id> ────────────────────────────────────────────────
            if (value.StartsWith("approve:", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new AgentPlanStep
                {
                    Kind = "approve",
                    Target = value["approve:".Length..].Trim()
                });
                return steps;
            }

            // navigate:<target> ───────────────────────────────────────────
            if (value.StartsWith("navigate:", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new AgentPlanStep
                {
                    Kind = "navigate",
                    Target = value["navigate:".Length..].Trim()
                });
                return steps;
            }

            // tool:<name>[:<argument>] ────────────────────────────────────
            if (value.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            {
                string body = value["tool:".Length..];
                int split = body.IndexOf(':');

                steps.Add(new AgentPlanStep
                {
                    Kind = "tool",
                    Target = split > 0 ? body[..split].Trim() : body.Trim(),
                    Argument = split > 0 ? body[(split + 1)..].Trim() : ""
                });
                return steps;
            }

            steps.Add(Answer(originalText));
            return steps;
        }

        private static AgentPlanStep Answer(string text) =>
            new() { Kind = "answer", Argument = text ?? "" };
    }
}
