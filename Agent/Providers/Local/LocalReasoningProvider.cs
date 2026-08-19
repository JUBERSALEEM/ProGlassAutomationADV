// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Providers/Local/LocalReasoningProvider.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The DEFAULT provider. No network, no model file, no inference: it
// answers strictly from the curated knowledge base.
//
// It is deliberately honest about its own limits. When the knowledge base
// has nothing relevant it SAYS so and suggests what would help, instead
// of producing a fluent guess. For a system that quotes glass prices and
// VAT, a confident wrong answer is worse than "I don't know".
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Providers.Local
{
    /// <summary>Knowledge-base-backed answering. Always offline.</summary>
    public sealed class LocalReasoningProvider : IAgentReasoningProvider
    {
        private readonly IAgentKnowledge? _knowledge;

        public LocalReasoningProvider(IAgentKnowledge? knowledge)
        {
            _knowledge = knowledge;
        }

        public string ProviderName => "Local knowledge";

        public bool IsLocal => true;

        public async Task<string> ReasonAsync(string prompt)
        {
            string question = (prompt ?? "").Trim();

            if (question.Length == 0)
                return "Ask me something and I will look it up.";

            if (_knowledge == null)
                return "My knowledge base is not available right now.";

            try
            {
                var facts = await _knowledge.RetrieveAsync(question)
                    .ConfigureAwait(false);

                if (facts.Count == 0)
                {
                    return
                        "I do not have anything about that in my local knowledge " +
                        "base, and I will not guess. You can add a note in " +
                        "Settings → AI & Agent, or ask me about stock, a document " +
                        "number, a customer, or recent activity — those I read " +
                        "from the live data.";
                }

                var sb = new StringBuilder();
                sb.AppendLine("From what I know locally:");
                foreach (var fact in facts.Take(4))
                    sb.AppendLine("• " + fact);

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalReasoning] failed: {ex.Message}");
                return "I could not search my knowledge base just now.";
            }
        }
    }
}
