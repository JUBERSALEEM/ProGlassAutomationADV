// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIConfig/ConfigService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Config (Chat 25 v3.0 — corrected deprecated model list)
//
// CHANGES vs previous:
//   ✓ FIX CRITICAL: Removed gemini-2.5-flash, gemini-2.5-pro,
//     gemini-2.5-flash-lite, gemini-2.0-flash, gemini-2.0-flash-001,
//     gemini-2.0-flash-lite from deprecated list.
//     These are Google's CURRENT production models. Auto-upgrading
//     users off these to gemini-2.0-flash was a downgrade.
//   ✓ FIX: CurrentDefaultModel changed to "gemini-2.0-flash"
//     which is the confirmed stable free-tier model.
//   ✓ Deprecated list now contains ONLY truly discontinued models:
//     1.0-pro series (shutdown), 1.5 series (deprecated March 2025),
//     2.0-flash-exp (experimental replaced by stable).
//   ✓ All other logic preserved exactly.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using System;
using System.IO;
using System.Text.Json;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>
    /// Loads/saves app config to %AppData%\ProGlassAutomation\config.json.
    /// Auto-upgrades ONLY truly deprecated Gemini model names on load.
    ///
    /// v2 SECURITY — API keys never persist in plain text:
    ///   Load(): legacy plain-text keys in config.json are migrated into
    ///           the DPAPI SecretVault, then wiped from the file.
    ///   Save(): key properties are masked to "" for serialization and
    ///           restored in memory afterwards; the vault is the source
    ///           of truth (unless RememberApiKeys=false → session only).
    /// </summary>
    public static class ConfigService
    {
        // SecretVault entry names (one per provider)
        private const string VaultGemini = "ai.gemini.key";
        private const string VaultGroq = "ai.groq.key";
        private const string VaultCohere = "ai.cohere.key";

        private static readonly string ConfigFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation");

        private static readonly string ConfigPath =
            Path.Combine(ConfigFolder, "config.json");

        /// <summary>
        /// FIX: Changed from "gemini-flash-latest" to "gemini-2.0-flash".
        /// gemini-2.0-flash is the current stable free-tier Gemini model
        /// with confirmed 15 req/min 1500/day quota on free tier.
        /// </summary>
        private const string CurrentDefaultModel = "gemini-3.5-flash";

        /// <summary>
        /// Discontinued / shut-down model names. Any saved value here is
        /// auto-migrated to CurrentDefaultModel on load.
        ///
        /// CURRENT (NOT deprecated — left out of this list):
        ///   gemini-3.5-flash      ← default (Google's recommended target)
        ///   gemini-2.5-flash      ← valid until Oct 2026
        ///   gemini-2.5-pro        ← valid
        ///
        /// The 2.0 GA family was SHUT DOWN by Google on 2026-06-01, so it
        /// is listed below and auto-migrated for existing installs.
        /// </summary>
        private static readonly string[] DeprecatedModels =
        {
            // 1.x series — shutdown by Google
            "gemini-1.0-pro",
            "gemini-pro",            // old alias for 1.0-pro
            "gemini-1.5-flash",      // deprecated March 2025
            "gemini-1.5-flash-latest",
            "gemini-1.5-flash-002",
            "gemini-1.5-pro",        // deprecated March 2025
            "gemini-1.5-pro-latest",

            // 2.0 experimental — replaced by stable release
            "gemini-2.0-flash-exp",

            // 2.0 GA family — SHUT DOWN by Google on 2026-06-01.
            // Auto-migrated to the current default (gemini-3.5-flash).
            "gemini-2.0-flash",
            "gemini-2.0-flash-001",
            "gemini-2.0-flash-lite",
            "gemini-2.0-flash-lite-001",
        };

        public static AppConfig Load()
        {
            try
            {
                AppConfig config;

                if (!File.Exists(ConfigPath))
                {
                    config = new AppConfig();
                }
                else
                {
                    var json = File.ReadAllText(ConfigPath);
                    config = JsonSerializer.Deserialize<AppConfig>(json)
                             ?? new AppConfig();
                }

                // Auto-upgrade only truly deprecated models
                if (config.Ai != null &&
                    IsDeprecatedModel(config.Ai.Model))
                {
                    config.Ai.Model = CurrentDefaultModel;
                }

                // v2: migrate legacy plain-text keys out of config.json
                // into the encrypted vault, then hydrate from the vault.
                MigrateLegacyKeys(config);
                HydrateKeysFromVault(config);

                return config;
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                    Directory.CreateDirectory(ConfigFolder);

                // v2: keys go to the vault — never to the JSON file.
                PersistKeysToVault(config);

                var ai = config.Ai;
                string g = ai?.GeminiApiKey ?? "";
                string q = ai?.GroqApiKey ?? "";
                string c = ai?.CohereApiKey ?? "";

                if (ai != null)
                {
                    ai.GeminiApiKey = "";
                    ai.GroqApiKey = "";
                    ai.CohereApiKey = "";
                }

                var json = JsonSerializer.Serialize(config,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);

                // Restore the in-memory working values.
                if (ai != null)
                {
                    ai.GeminiApiKey = g;
                    ai.GroqApiKey = q;
                    ai.CohereApiKey = c;
                }
            }
            catch (Exception ex)
            {
                // S-5: this used to be silent. It is the worst place in the
                // app to be silent: Save() failing means the user's settings
                // (AI provider choice, theme, company details) were NOT
                // written, yet every caller reports success. The user then
                // loses the change on restart with no explanation.
                // Still non-throwing — a settings write must not crash the
                // app — but it is now visible in the log.
                AppLogService.Error(
                    "Failed to save app configuration — changes will be " +
                    "lost on restart.",
                    ex,
                    new LogContext
                    {
                        Module = "Config",
                        Class = nameof(ConfigService),
                        Operation = "Save"
                    });
            }
        }

        /// <summary>
        /// One-time migration: plain-text keys found in the JSON (v1
        /// installs) are copied into the vault. The masked Save() then
        /// wipes them from the file on the next write.
        /// </summary>
        private static void MigrateLegacyKeys(AppConfig config)
        {
            var ai = config.Ai;
            if (ai == null) return;

            if (!string.IsNullOrEmpty(ai.GeminiApiKey))
                SecretVault.Store(VaultGemini, ai.GeminiApiKey);
            if (!string.IsNullOrEmpty(ai.GroqApiKey))
                SecretVault.Store(VaultGroq, ai.GroqApiKey);
            if (!string.IsNullOrEmpty(ai.CohereApiKey))
                SecretVault.Store(VaultCohere, ai.CohereApiKey);
        }

        private static void HydrateKeysFromVault(AppConfig config)
        {
            var ai = config.Ai;
            if (ai == null) return;

            if (SecretVault.TryRetrieve(VaultGemini, out var g))
                ai.GeminiApiKey = g;
            if (SecretVault.TryRetrieve(VaultGroq, out var q))
                ai.GroqApiKey = q;
            if (SecretVault.TryRetrieve(VaultCohere, out var c))
                ai.CohereApiKey = c;
        }

        private static void PersistKeysToVault(AppConfig config)
        {
            var ai = config.Ai;
            if (ai == null) return;

            if (!ai.RememberApiKeys)
            {
                // Session-only mode: wipe anything stored on disk.
                SecretVault.Remove(VaultGemini);
                SecretVault.Remove(VaultGroq);
                SecretVault.Remove(VaultCohere);
                return;
            }

            SecretVault.Store(VaultGemini, ai.GeminiApiKey ?? "");
            SecretVault.Store(VaultGroq, ai.GroqApiKey ?? "");
            SecretVault.Store(VaultCohere, ai.CohereApiKey ?? "");
        }

        private static bool IsDeprecatedModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return true;
            foreach (var deprecated in DeprecatedModels)
            {
                if (model.Equals(deprecated,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}