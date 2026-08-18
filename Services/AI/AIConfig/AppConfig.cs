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

        /// <summary>
        /// When true, rounded pills and cards draw a strong near-black
        /// definition edge so they stay visually separate from tinted
        /// (Monet) surfaces instead of blending into them.
        /// </summary>
        public bool UseDefinedOutline { get; set; }

        /// <summary>
        /// Uniform border thickness (in DIPs) for every card / panel in the
        /// app. Defaults to 1.5. Applied globally through the
        /// CardBorderThickness theme resource so one setting controls all
        /// cards instead of editing them individually.
        /// </summary>
        public double CardBorderThickness { get; set; } = 1.5;

        /// <summary>
        /// Uniform corner radius (DIPs) for every card / panel. Defaults
        /// to 12 — the tighter industrial look. Applied globally through
        /// CardCornerRadius so one slider restyles the whole app.
        /// ControlCornerRadius and CardCornerRadiusLarge are derived from
        /// this value (radius − 2 and radius + 4).
        /// </summary>
        public double CardCornerRadius { get; set; } = 12;

        /// <summary>
        /// Display density: "Compact" | "Normal" | "Comfortable".
        /// One setting shrinks inputs, buttons, titles and card padding
        /// app-wide so laptop screens can see the data grid.
        /// Default Compact — factory laptops are typically 1366×768.
        /// </summary>
        public string DisplayDensity { get; set; } = "Compact";

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

        /// <summary>
        /// Print layout id: ClassicNavy | ModernTeal | EditorialInk |
        /// IndustrialSteel | MinimalIvory. Applied to every PDF.
        /// </summary>
        public string PdfLayoutId { get; set; } = "ClassicNavy";
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
