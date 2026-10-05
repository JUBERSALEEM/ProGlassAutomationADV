// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/AgentSecurityService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The boundary. Three jobs:
//
//  1. SanitizeUntrusted — text that came from OUTSIDE the developer's
//     control (database rows, voice transcripts, imported knowledge) is
//     treated as DATA, never as instructions. Control characters go, and
//     known prompt-injection phrases are replaced with [filtered].
//
//  2. Redact — API keys, bearer tokens and passwords are masked before
//     anything is logged, displayed or sent.
//
//  3. AllowExternalAsync — an ORDERED gate on outbound payloads:
//        LocalOnly → consent → host whitelist → size cap → redaction sanity
//     The redaction-sanity step is the important one: if a payload still
//     contains something that looks like a secret AFTER redaction, the
//     send is BLOCKED rather than cleaned again. Fail closed.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Configuration;
using ProGlassAutomation.Agent.Security.Redaction;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Security.Network;
using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Security
{
    /// <summary>Sanitization, redaction and the outbound-egress gate.</summary>
    public sealed class AgentSecurityService : IAgentSecurityService
    {
        /// <summary>
        /// Maximum length of any sanitized input. Mirrors the Redaction
        /// module so existing callers and tests keep compiling against
        /// this name, while there is still only one real value.
        /// </summary>
        public const int MaxUntrustedLength = AgentRedaction.MaxUntrustedLength;

        private readonly IAgentAuditService? _audit;

        public AgentSecurityService(IAgentAuditService? audit = null)
        {
            _audit = audit;
        }

        /// <summary>Reads the live setting — never cached.</summary>
        public bool LocalOnlyMode => AgentConfigurationService.Current.LocalOnly;

        // ══════════════════════════════════════════════════════════════
        //  1. SANITIZE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// PHASE T — delegates to the Redaction module.
        /// The phrase list and control-character stripping now live in
        /// Agent/Security/Redaction/AgentRedaction.cs so there is exactly
        /// ONE implementation. Two copies of a security rule is how they
        /// drift apart.
        /// </summary>
        public string SanitizeUntrusted(string text) =>
            AgentRedaction.SanitizeUntrusted(text);

        // ══════════════════════════════════════════════════════════════
        //  2. REDACT
        // ══════════════════════════════════════════════════════════════

        /// <summary>PHASE T — delegates to the Redaction module.</summary>
        public string Redact(string text) => AgentRedaction.Redact(text);

        /// <summary>True when the text still looks like it holds a secret.</summary>
        private static bool LooksLikeSecret(string text) =>
            AgentRedaction.ContainsSecret(text);

        // ══════════════════════════════════════════════════════════════
        //  3. EGRESS GATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Ordered gate. The FIRST failing check wins and the reason is
        /// returned verbatim to the user — no silent drops.
        /// </summary>
        public Task<PermissionDecision> AllowExternalAsync(
            string host, string payload)
        {
            var settings = AgentConfigurationService.Current;
            string user = Services.Audit.AuditService.CurrentUsername;

            // 1 ── Local-only mode
            if (settings.LocalOnly)
            {
                _audit?.Security(user, "External blocked",
                    $"local-only mode is on (host {host})");

                return Task.FromResult(PermissionDecision.Deny(
                    "Local-Only mode is on, so I cannot send anything to the " +
                    "internet. You can turn it off in Settings → AI & Agent."));
            }

            // 2 ── Explicit cloud consent
            if (!settings.CloudConsentGiven)
            {
                _audit?.Security(user, "External blocked",
                    $"cloud consent not given (host {host})");

                return Task.FromResult(PermissionDecision.Deny(
                    "Cloud use has not been consented to yet. Enable it in " +
                    "Settings → AI & Agent before I can use an online provider."));
            }

            // 3 ── Host whitelist (https-only enforced inside)
            if (!NetworkWhitelist.IsAllowedHost(host))
            {
                _audit?.Security(user, "External blocked",
                    $"host not on the allow-list: {host}");

                return Task.FromResult(PermissionDecision.Deny(
                    $"'{host}' is not on the allowed-hosts list, so I will not " +
                    "contact it."));
            }

            // 4 ── Payload cap
            int cap = settings.ExternalPayloadCap > 0
                ? settings.ExternalPayloadCap
                : 4000;

            if ((payload ?? "").Length > cap)
            {
                _audit?.Security(user, "External blocked",
                    $"payload {payload?.Length ?? 0} chars exceeds cap {cap}");

                return Task.FromResult(PermissionDecision.Deny(
                    $"That request is {payload?.Length ?? 0} characters, over the " +
                    $"{cap}-character outbound limit, so I did not send it."));
            }

            // 5 ── Redaction sanity: a payload that STILL looks like it
            //      carries a secret is blocked outright, not re-cleaned.
            if (LooksLikeSecret(payload ?? ""))
            {
                _audit?.Security(user, "External blocked",
                    "payload appears to contain a secret after redaction");

                return Task.FromResult(PermissionDecision.Deny(
                    "That request looks like it contains an API key or password, " +
                    "so I blocked it instead of sending it."));
            }

            _audit?.Security(user, "External allowed", $"host {host}");
            return Task.FromResult(PermissionDecision.Allow());
        }
    }
}
