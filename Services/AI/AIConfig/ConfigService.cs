using System;
using System.IO;
using System.Text.Json;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>
    /// Loads/saves app config to %AppData%\ProGlassAutomation\config.json.
    /// Keeps API keys out of source control.
    /// </summary>
    public static class ConfigService
    {
        private static readonly string ConfigFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ProGlassAutomation");

        private static readonly string ConfigPath =
            Path.Combine(ConfigFolder, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new AppConfig();
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
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
    }
}