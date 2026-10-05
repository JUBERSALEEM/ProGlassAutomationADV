// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Providers/CloudProviderAdapter.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Wraps the EXISTING IAiService rather than opening its own sockets. The
// agent therefore inherits the app's proven HTTP handling and adds only
// what it must: the security boundary in front of it.
//
// ORDER IS THE CONTRACT — whitelist, then boundary, then the call:
//   1. Is this host allowed at all?            (NetworkWhitelist)
//   2. Does the boundary permit THIS payload?  (LocalOnly → consent →
//                                               host → cap → redaction)
//   3. Only then AskAsync.
//
// A blocked request returns the boundary's own truthful reason — the user
// is told exactly why nothing was sent, never left with silence.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Security.Network;
using ProGlassAutomation.Services.AI.AIService;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Providers
{
    /// <summary>Security-gated bridge to the app's cloud AI service.</summary>
    public class CloudProviderAdapter : IAgentReasoningProvider
    {
        private readonly IAiService? _ai;
        private readonly IAgentSecurityService _security;

        /// <summary>Host this provider talks to (must be whitelisted).</summary>
        protected virtual string Host => "generativelanguage.googleapis.com";

        public CloudProviderAdapter(
            IAiService? ai,
            IAgentSecurityService security,
            string providerName = "Cloud AI")
        {
            _ai = ai;
            _security = security;
            ProviderName = providerName;
        }

        public string ProviderName { get; }

        public bool IsLocal => false;

        public async Task<string> ReasonAsync(string prompt)
        {
            string question = (prompt ?? "").Trim();
            if (question.Length == 0) return "Ask me something first.";

            // 1 ── Whitelist. Refuse before anything is prepared.
            if (!NetworkWhitelist.IsAllowedHost(Host))
            {
                return $"'{Host}' is not on the allowed-hosts list, so I will not " +
                       "contact it.";
            }

            // Redact BEFORE the boundary sees it, so the sanity check tests
            // what would actually be transmitted.
            string payload = _security.Redact(question);

            // 2 ── Boundary.
            var decision = await _security.AllowExternalAsync(Host, payload)
                .ConfigureAwait(false);

            if (!decision.Allowed)
                return decision.Reason;

            // 3 ── Only now the call.
            if (_ai == null || !_ai.IsConfigured)
            {
                return "No cloud AI provider is configured, so I stayed local. " +
                       "You can add a key in Settings → AI & Agent.";
            }

            try
            {
                var response = await _ai.AskAsync(payload).ConfigureAwait(false);

                if (response == null)
                    return "The cloud provider returned nothing.";

                return response.Success
                    ? (response.Text ?? "")
                    : $"The cloud provider could not answer: {response.ErrorMessage}";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[CloudProvider] failed: {ex.Message}");
                return "I could not reach the cloud provider, so I could not answer.";
            }
        }
    }
}
