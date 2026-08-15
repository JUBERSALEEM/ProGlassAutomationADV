using ProGlassAutomation.Services.AI.AIService;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>Wallpaper configuration for one module (§9).</summary>
    public class ModuleWallpaperSettings
    {
        public bool Enabled { get; set; }
        public string Path { get; set; } = "";
        public double Opacity { get; set; } = 0.85;
    }

    /// <summary>
    /// Appearance preferences (v2 visual theme engine + wallpaper).
    /// No secrets here — plain settings only.
    /// </summary>
    public class AppearanceSettings
    {
        /// <summary>"Normal" | "Monet" | "Professional".</summary>
        public string VisualTheme { get; set; } = "Normal";

        /// <summary>Global wallpaper image path (empty = none).</summary>
        public string WallpaperPath { get; set; } = "";

        /// <summary>How strongly the wallpaper shows through (0.3–1).</summary>
        public double WallpaperOpacity { get; set; } = 0.85;

        /// <summary>
        /// Per-module wallpapers, keyed by WallpaperModules constants
        /// (e.g. "ProformaInvoice"). Missing entries = no wallpaper.
        /// </summary>
        public Dictionary<string, ModuleWallpaperSettings> ModuleWallpapers
        { get; set; } = new();

        /// <summary>
        /// Accent preset key ("GlassBlue" | "Emerald" | "Amber" |
        /// "Rose" | "Violet" | "Teal"). Re-applied on startup so the
        /// whole UI keeps the chosen colour between sessions.
        /// </summary>
        public string AccentPreset { get; set; } = "GlassBlue";
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
