using ProGlassAutomation.Services.AI.AIService;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>
    /// Appearance preferences (v2 visual theme engine + wallpaper).
    /// No secrets here — plain settings only.
    /// </summary>
    public class AppearanceSettings
    {
        /// <summary>"Normal" | "Monet" | "Professional".</summary>
        public string VisualTheme { get; set; } = "Normal";

        /// <summary>Wallpaper image path (empty = none).</summary>
        public string WallpaperPath { get; set; } = "";

        /// <summary>How strongly the wallpaper shows through (0.3–1).</summary>
        public double WallpaperOpacity { get; set; } = 0.85;
    }

    /// <summary>
    /// Root config container persisted to disk as JSON.
    ///
    /// SECURITY (v2): API keys never reach the JSON file. They live in
    /// the DPAPI-encrypted SecretVault and are hydrated into AiSettings
    /// in memory by ConfigService. With RememberApiKeys=false keys exist
    /// only for the running session.
    /// </summary>
    public class AppConfig
    {
        public AiSettings Ai { get; set; } = new AiSettings();
        public bool OnlineFeaturesEnabled { get; set; } = true;
        public AppearanceSettings Appearance { get; set; } = new();
    }
}
