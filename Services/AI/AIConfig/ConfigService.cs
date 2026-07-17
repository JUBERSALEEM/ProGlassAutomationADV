using ProGlassAutomation.Services.AI.AIService;
using System;
using System.IO;
using System.Text.Json;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>
    /// Loads/saves app config to %AppData%\ProGlassAutomation\config.json.
    /// Auto-upgrades deprecated Gemini model names on load.
    /// Uses 'latest' model aliases to avoid future deprecation.
    /// </summary>
    public static class ConfigService
    {
        private static readonly string ConfigFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ProGlassAutomation");

        private static readonly string ConfigPath =
            Path.Combine(ConfigFolder, "config.json");

        // 'latest' alias auto-tracks newest Flash model — never deprecates
        private const string CurrentDefaultModel = "gemini-flash-latest";

        // Deprecated / unavailable model names (auto-upgraded on load)
        private static readonly string[] DeprecatedModels = new[]
        {
            // Old 1.x series
            "gemini-1.5-flash",
            "gemini-1.5-flash-latest",
            "gemini-1.5-flash-002",
            "gemini-1.5-pro",
            "gemini-1.5-pro-latest",
            "gemini-1.0-pro",
            "gemini-pro",
            // Old 2.0 experimental
            "gemini-2.0-flash-exp",
            // 2.5 versions (deprecated for new users)
            "gemini-2.5-flash",
            "gemini-2.5-pro",
            "gemini-2.5-flash-lite",
            // 2.0 stable (also being phased)
            "gemini-2.0-flash",
            "gemini-2.0-flash-001",
            "gemini-2.0-flash-lite",
            "gemini-2.0-flash-lite-001"
        };

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppConfig();

                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // Auto-upgrade deprecated model names
                if (config.Ai != null && IsDeprecatedModel(config.Ai.Model))
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
                // Silently fail — config is optional
            }
        }

        private static bool IsDeprecatedModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return true;
            foreach (var deprecated in DeprecatedModels)
            {
                if (model.Equals(deprecated, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}