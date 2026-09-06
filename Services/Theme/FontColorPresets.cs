// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/FontColorPresets.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings → Appearance — global FONT COLOUR presets
//
// One global text colour for the whole UI (every section, inside and
// outside cards). The user picks a preset chip or a custom hex in the
// Font Color tile; the VisualThemeEngine turns that single colour into
// the full Primary/Secondary/Muted text hierarchy (secondary and muted
// are derived by mixing the chosen colour toward the theme's neutral
// slate, so headings, captions and hints keep their contrast order
// whatever colour is chosen — text never sits invisible over the
// transparent frosted surfaces.
//
// "Auto" (empty config value) means "no override": every family keeps
// its own palette (Crystal Fluid / Ios-17pm = the About page's
// LiquidSnow/Cerulean/Muted, all others = the Light/Dark theme defaults).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// Preset chips for the global Font Color control. The swatch shown
    /// in Settings IS the exact primary colour that gets applied.
    /// </summary>
    public static class FontColorPresets
    {
        /// <summary>No override — the active theme/family owns the text.</summary>
        public const string Auto = "Auto";

        public static IReadOnlyList<(string Key, string Name, Color Color)> All { get; } =
            new[]
            {
                ("White",    "Pure White",   Color.FromRgb(0xFF, 0xFF, 0xFF)),
                ("Ice",      "Ice White",    Color.FromRgb(0xE2, 0xEC, 0xFB)),
                ("Silver",   "Silver",       Color.FromRgb(0xCB, 0xD5, 0xE1)),
                ("Sky",      "Sky",          Color.FromRgb(0x7D, 0xC8, 0xF8)),
                ("Amber",    "Amber",        Color.FromRgb(0xF5, 0x9E, 0x0B)),
                ("Rose",     "Rose",         Color.FromRgb(0xFB, 0x71, 0x85)),
                ("Graphite", "Graphite",     Color.FromRgb(0x33, 0x3B, 0x4A)),
                ("Black",    "True Black",   Color.FromRgb(0x02, 0x06, 0x17)),
            };

        /// <summary>Resolves a preset key to its colour (null = Auto/unknown).</summary>
        public static Color? ColorFor(string presetKey)
        {
            if (string.IsNullOrWhiteSpace(presetKey)) return null;
            var hit = All.FirstOrDefault(p =>
                string.Equals(p.Key, presetKey,
                    StringComparison.OrdinalIgnoreCase));
            return hit.Color;
        }

        /// <summary>Formats a colour as #RRGGBB for persistence.</summary>
        public static string ToHex(Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
