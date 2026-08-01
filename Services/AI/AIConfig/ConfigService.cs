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
using System;
using System.IO;
using System.Text.Json;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>
    /// Loads/saves app config to %AppData%\ProGlassAutomation\config.json.
    /// Auto-upgrades ONLY truly deprecated Gemini model names on load.
    /// </summary>
    public static class ConfigService
    {
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
        private const string CurrentDefaultModel = "gemini-2.0-flash";

        /// <summary>
        /// ONLY truly discontinued / removed model names.
        ///
        /// REMOVED FROM LIST (these are CURRENT models — not deprecated):
        ///   gemini-2.5-flash      ← Google's latest flagship (better quality)
        ///   gemini-2.5-pro        ← Google's latest pro model
        ///   gemini-2.5-flash-lite ← Lightweight current model
        ///   gemini-2.0-flash      ← Current stable free-tier model
        ///   gemini-2.0-flash-001  ← Stable pinned version
        ///   gemini-2.0-flash-lite ← Lightweight current model
        ///
        /// These were incorrectly in the deprecated list and caused users
        /// on better models to be silently downgraded.
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
        };

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppConfig();

                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json)
                             ?? new AppConfig();

                // Auto-upgrade only truly deprecated models
                if (config.Ai != null &&
                    IsDeprecatedModel(config.Ai.Model))
                {
                    config.Ai.Model = CurrentDefaultModel;
                    Save(config);
                }

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

                var json = JsonSerializer.Serialize(config,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Config is optional — silent fail
            }
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