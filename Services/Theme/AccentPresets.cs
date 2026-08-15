// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/AccentPresets.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — accent preset catalogue
//
// Single source of truth for the accent colour presets shown on the
// Settings > Appearance page. Consumed by:
//   • SettingsViewModel.ApplyAccentPreset (user selection + persistence)
//   • App startup (re-applies the persisted preset)
//   • the Settings UI (swatch colours on the preset chips)
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>The six brand accent presets.</summary>
    public static class AccentPresets
    {
        public const string GlassBlue = "GlassBlue";
        public const string Emerald   = "Emerald";
        public const string Amber     = "Amber";
        public const string Rose      = "Rose";
        public const string Violet    = "Violet";
        public const string Teal      = "Teal";

        /// <summary>
        /// Maps a preset key to its colour. GlassBlue (and any unknown
        /// key) returns null = follow the theme's built-in blue.
        /// </summary>
        public static Color? ColorFor(string presetKey)
        {
            return presetKey switch
            {
                Emerald => Color.FromRgb(0x05, 0x96, 0x69),
                Amber   => Color.FromRgb(0xD9, 0x77, 0x06),
                Rose    => Color.FromRgb(0xE1, 0x1D, 0x48),
                Violet  => Color.FromRgb(0x7C, 0x3A, 0xED),
                Teal    => Color.FromRgb(0x0D, 0x94, 0x88),
                _       => (Color?)null   // Glass Blue = theme default
            };
        }

        /// <summary>Swatch colour shown on the Settings chip itself.
        /// GlassBlue maps to the default brand blue so the chip always
        /// shows a real preview dot.</summary>
        public static Color SwatchFor(string presetKey)
        {
            return ColorFor(presetKey) ?? Color.FromRgb(0x1D, 0x4E, 0xD8);
        }
    }
}
