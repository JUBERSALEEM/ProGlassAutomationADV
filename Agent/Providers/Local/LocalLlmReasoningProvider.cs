// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Providers/Local/LocalLlmReasoningProvider.cs
// PROJECT: ProGlassAutomation — Local LLM program (2026-09-04)
//
// Local-LLM-backed answering. Runs the user-installed GGUF entirely
// OFFLINE (see LocalLlmService). It is a *brain*, not a source of truth:
//
//   • FACTS still come from the knowledge base / live-data tools before
//     generation — the model answers IN THE CONTEXT of real data rather
//     than inventing numbers.
//   • On ANY failure the call said so — the selector chain then falls
//     back to the curated knowledge base provider unchanged.
//   • IsLocal = true: the security service's Local-Only posture
//     automatically accepts it, and the cloud whitelist never sees it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.LocalAi;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Agent.Providers.Local
{
    /// <summary>GGUF-backed local reasoning. IsLocal = true, forever.</summary>
    public sealed class LocalLlmReasoningProvider : IAgentReasoningProvider
    {
        private readonly LocalLlmService _llm;
        private readonly IAgentKnowledge? _knowledge;
        private readonly string _modelTitle;

        public LocalLlmReasoningProvider(
            LocalLlmService llm, IAgentKnowledge? knowledge, string modelTitle)
        {
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
            _knowledge = knowledge;
            _modelTitle = modelTitle;
        }

        /// <summary>e.g. "Local LLM (Qwen 2.5 · 1.5B)".</summary>
        public string ProviderName => $"Local LLM ({_modelTitle})";

        public bool IsLocal => true;

        public async Task<string> ReasonAsync(string prompt)
        {
            string question = (prompt ?? "").Trim();
            if (question.Length == 0)
                return "Ask me anything and I will answer it on this machine.";

            try
            {
                // Ground the model: the knowledge base is seeded with the
                // app's real facts and the user's taught notes — passing
                // them as SYSTEM context turns the LLM into a presenter of
                // facts, never a fabricator of them.
                string kbContext = "";
                if (_knowledge != null)
                {
                    try
                    {
                        var facts = await _knowledge.RetrieveAsync(question)
                            .ConfigureAwait(false);
                        if (facts is { Count: > 0 })
                        {
                            var sb = new StringBuilder(
                                "FACTS from the ProGlass knowledge base (use these, " +
                                "never invent numbers):\n");
                            int n = 0;
                            foreach (var f in facts)
                            {
                                if (++n > 6) break;
                                sb.Append("- ").AppendLine(f);
                            }
                            kbContext = sb.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[LocalLlmProvider] kb grounding skipped: {ex.Message}");
                    }
                }

                string system =
                    "You are the ProGlass assistant. Answer briefly and " +
                    "practically for a glass-fabrication workshop. Be honest: " +
                    "when you do not know, say so instead of guessing." +
                    (kbContext.Length > 0 ? "\n\n" + kbContext : "");

                string? answer = await _llm.GenerateAsync(
                    system, question, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(answer))
                    return answer.Trim();

                // Honest degradation: model could not answer → tell the
                // truth and hand the turn back to the knowledge base text.
                if (_knowledge != null)
                {
                    var facts2 = await _knowledge.RetrieveAsync(question)
                        .ConfigureAwait(false);
                    if (facts2 is { Count: > 0 })
                        return
                            "The local model could not phrase that, but my " +
                            "knowledge base says:\n• " +
                            string.Join("\n• ", facts2.Take(4));
                }

                return
                    "The local model could not answer that one — my curated " +
                    "knowledge base suggests asking about stock, a document " +
                    "number, a customer, or recent activity.";
            }
            catch (Exception ex)
            {
                // Never throw from a reasoning provider; the outer chain
                // reads this as "fall back", not "crash the chat".
                AppLogService.Debug($"[LocalLlmProvider] failed: {ex.Message}");
                return "The local model hit an internal problem on that turn.";
            }
        }
    }
}
