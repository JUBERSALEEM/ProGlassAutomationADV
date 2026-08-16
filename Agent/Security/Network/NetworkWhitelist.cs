// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/Network/NetworkWhitelist.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// An ALLOW-list, never a block-list. Anything not named here is refused,
// so adding a new provider is a deliberate, reviewable code change rather
// than something a config file or a taught rule could arrange.
//
// https is mandatory: an http URL is rejected even for a listed host.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.Security.Network
{
    /// <summary>The only hosts the agent may ever contact.</summary>
    public static class NetworkWhitelist
    {
        /// <summary>Allowed hosts (exact match, case-insensitive).</summary>
        public static readonly IReadOnlyList<string> AllowedHosts = new[]
        {
            // Reasoning providers
            "generativelanguage.googleapis.com",   // Gemini
            "api.groq.com",                        // Groq
            "api.cohere.com",                      // Cohere

            // Update + model distribution (already used by the app)
            "github.com",
            "objects.githubusercontent.com",
            "huggingface.co"
        };

        /// <summary>True when the bare host name is allowed.</summary>
        public static bool IsAllowedHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;

            return AllowedHosts.Any(h =>
                string.Equals(h, host.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True when the URL is https AND its host is allowed. Anything
        /// unparseable is refused.
        /// </summary>
        public static bool IsAllowedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
                return false;

            // https only — no plaintext egress, ever.
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return IsAllowedHost(uri.Host);
        }

        /// <summary>Human-readable list for the Settings UI.</summary>
        public static string Describe() => string.Join(", ", AllowedHosts);
    }
}
