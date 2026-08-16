// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Providers/Groq/GroqReasoningProvider.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Thin subclass of CloudProviderAdapter. It exists to pin ONE thing: the
// host this provider is allowed to reach. All transport, security gating
// and error handling stay in the base class, so a new provider cannot
// accidentally introduce a second, unguarded network path.
//
// The host below must also appear in NetworkWhitelist.AllowedHosts, or
// every request is refused before it is prepared.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.AI.AIService;

namespace ProGlassAutomation.Agent.Providers.Groq
{
    /// <summary>Groq reasoning, gated by the agent security boundary.</summary>
    public sealed class GroqReasoningProvider : CloudProviderAdapter
    {
        /// <summary>Settings key identifying this provider.</summary>
        public const string ProviderKey = "groq";

        public GroqReasoningProvider(
            IAiService? ai, IAgentSecurityService security)
            : base(ai, security, "Groq")
        {
        }

        protected override string Host => "api.groq.com";
    }
}
