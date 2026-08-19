// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/SecretProtection/AgentSecretStore.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Provider API keys, kept out of every config file.
//
// This is a THIN WRAPPER over the existing Services/Security/SecretVault
// (Windows DPAPI, per-user encryption). It adds nothing cryptographic —
// deliberately, because re-implementing secret storage would be a new
// attack surface. All it adds is a namespace prefix so agent keys are
// isolated from any other secret the app stores and can be enumerated
// and removed as a group.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Security.SecretProtection
{
    /// <summary>DPAPI-backed storage for agent provider credentials.</summary>
    public static class AgentSecretStore
    {
        /// <summary>Namespace prefix isolating agent secrets.</summary>
        public const string KeyPrefix = "agent.provider.";

        /// <summary>Providers this store knows about.</summary>
        public static readonly IReadOnlyList<string> KnownProviders =
            new[] { "gemini", "groq", "cohere" };

        private static string FullKey(string provider) =>
            KeyPrefix + (provider ?? "").Trim().ToLowerInvariant();

        /// <summary>Stores (or replaces) a provider key. Never throws.</summary>
        public static bool Store(string provider, string secret)
        {
            if (string.IsNullOrWhiteSpace(provider) ||
                string.IsNullOrWhiteSpace(secret))
                return false;

            try
            {
                SecretVault.Store(FullKey(provider), secret);
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentSecretStore] Store failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Retrieves a provider key. False when absent.</summary>
        public static bool TryRetrieve(string provider, out string secret)
        {
            secret = "";

            if (string.IsNullOrWhiteSpace(provider)) return false;

            try
            {
                return SecretVault.TryRetrieve(FullKey(provider), out secret);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentSecretStore] Retrieve failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>True when a key is stored for this provider.</summary>
        public static bool HasSecret(string provider) =>
            TryRetrieve(provider, out var s) && !string.IsNullOrWhiteSpace(s);

        /// <summary>Removes one provider key.</summary>
        public static bool Remove(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return false;

            try
            {
                SecretVault.Remove(FullKey(provider));
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentSecretStore] Remove failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Removes every agent provider key.</summary>
        public static void RemoveAll()
        {
            foreach (var provider in KnownProviders)
                Remove(provider);
        }
    }
}
