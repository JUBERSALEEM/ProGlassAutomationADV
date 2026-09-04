// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Providers/AgentProviderSelector.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Chooses which brain answers a question.
//
// THE RULE, in order:
//   LOCAL, unless   (a) Local-Only mode is OFF, and
//                   (b) cloud consent has been given, and
//                   (c) a provider key is actually stored.
//   Any failure, any exception, any missing piece → LOCAL.
//
// There is no path where an error or a misconfiguration results in MORE
// data leaving the machine: the failure direction is always inward.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Configuration;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Providers.Cohere;
using ProGlassAutomation.Agent.Providers.Gemini;
using ProGlassAutomation.Agent.Providers.Groq;
using ProGlassAutomation.Agent.Providers.Local;
using ProGlassAutomation.Agent.Security.SecretProtection;
using ProGlassAutomation.Services.AI.AIService;
using System;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Providers
{
    /// <summary>Picks the reasoning provider for the current settings.</summary>
    public static class AgentProviderSelector
    {
        /// <summary>
        /// Returns the provider to use right now. Never throws and never
        /// returns null — the local provider is the guaranteed fallback.
        /// </summary>
        public static IAgentReasoningProvider Select(
            IAgentKnowledge? knowledge,
            IAgentSecurityService security,
            IAiService? ai)
        {
            var local = new LocalReasoningProvider(knowledge);

            try
            {
                var settings = AgentConfigurationService.Current;

                // (a) Local-only wins outright.
                if (settings.LocalOnly) return local;

                // (a2) NO WIRE → NO CLOUD. Consent and a stored key do not
                // create connectivity: with the network down the cloud
                // provider would answer every question with a failure, and
                // the user would read that as "the agent is dead". Failing
                // inward keeps the privacy rule (data never leaves when it
                // cannot) and keeps the agent ALIVE: the local knowledge
                // base determines answer quality, not the last-seen wifi.
                if (App.Connectivity is { IsOnline: false })
                {
                    AppLogService.Debug(
                        "[AgentProviderSelector] offline — using local provider");
                    return local;
                }

                // (b) Consent must be explicit.
                if (!settings.CloudConsentGiven) return local;

                // (c) A key must actually be stored for the chosen provider.
                string key = (settings.CloudProvider ?? "").Trim().ToLowerInvariant();

                if (!AgentSecretStore.HasSecret(key)) return local;

                return key switch
                {
                    GroqReasoningProvider.ProviderKey =>
                        new GroqReasoningProvider(ai, security),
                    CohereReasoningProvider.ProviderKey =>
                        new CohereReasoningProvider(ai, security),
                    GeminiReasoningProvider.ProviderKey =>
                        new GeminiReasoningProvider(ai, security),
                    _ => local
                };
            }
            catch (Exception ex)
            {
                // Selection must never break the agent — fall back inward.
                AppLogService.Debug($"[AgentProviderSelector] falling back to local: {ex.Message}");
                return local;
            }
        }

        /// <summary>One-line description for the Settings status row.</summary>
        public static string DescribeCurrent()
        {
            try
            {
                var settings = AgentConfigurationService.Current;

                if (settings.LocalOnly)
                    return "Local-Only mode is ON — nothing leaves this computer.";

                if (App.Connectivity is { IsOnline: false })
                    return "OFFLINE — answering from the local knowledge base. " +
                           "Cloud providers resume automatically when the network " +
                           "returns.";

                if (!settings.CloudConsentGiven)
                    return "Local-Only is off, but cloud use has not been consented " +
                           "to — still answering locally.";

                string key = (settings.CloudProvider ?? "").Trim().ToLowerInvariant();

                return AgentSecretStore.HasSecret(key)
                    ? $"Cloud provider '{key}' is configured and consented."
                    : $"Cloud provider '{key}' has no stored key — still answering " +
                      "locally.";
            }
            catch
            {
                return "Answering locally.";
            }
        }
    }
}
