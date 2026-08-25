// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/AccentPresets.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — accent preset catalogue
//
// Single source of truth for the accent colour presets shown on the
// Settings > Appearance page. Consumed by:
//   • SettingsViewModel.ApplyAccentPreset (user selection + persistence)
//   • App startup (re-applies the persisted preset)
//   • the Settings UI (swatch colours are bound from this class — the
//     chips are generated from the All list, so the swatch a user sees
//     IS the colour that gets applied; hex values can no longer drift
//     between XAML and code).
//
// 2026-08-25: palette expanded from 6 presets to 16 + a free custom
// colour, because users had no way to pick their own colour and the
// old hand-copied XAML swatch hexes had drifted from the code values.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>The brand accent presets.</summary>
    public static class AccentPresets
    {
        public const string GlassBlue = "GlassBlue";
        public const string Emerald   = "Emerald";
        public const string Amber     = "Amber";
        public const string Rose      = "Rose";
        public const string Violet    = "Violet";
        public const string Teal      = "Teal";

        /// <summary>
        /// Near-black accent. Uses #111827 rather than pure #000000: on
        /// the dark theme a literal black edge against a near-black card
        /// measures about 1.18:1 and reads as a hole rather than a line,
        /// so the keyline derivation would have nothing to work with.
        /// ApplyAccent lifts this for dark mode automatically.
        /// </summary>
        public const string Black     = "Black";

        // ── 2026-08-25 palette expansion ───────────────────────────────
        public const string Sky       = "Sky";
        public const string RoyalBlue = "RoyalBlue";
        public const string Indigo    = "Indigo";
        public const string Fuchsia   = "Fuchsia";
        public const string Orange    = "Orange";
        public const string Crimson   = "Crimson";
        public const string Gold      = "Gold";
        public const string Lime      = "Lime";
        public const string Cyan      = "Cyan";
        public const string Slate     = "Slate";

        /// <summary>
        /// Free-pick colour. The actual value lives in
        /// AppearanceSettings.CustomAccentHex; ColorFor returns null for
        /// this key and the caller resolves the hex.
        /// </summary>
        public const string Custom    = "Custom";

        /// <summary>
        /// Ordered palette shown in Settings. Order = display order.
        /// The Settings UI binds its chips to this list, so swatch and
        /// applied colour can never disagree.
        /// </summary>
        public static IReadOnlyList<(string Key, string Name)> All { get; } =
            new (string, string)[]
            {
                (GlassBlue, "Glass Blue"),
                (RoyalBlue, "Royal Blue"),
                (Sky,       "Sky"),
                (Cyan,      "Cyan"),
                (Teal,      "Teal"),
                (Emerald,   "Emerald"),
                (Lime,      "Lime"),
                (Gold,      "Gold"),
                (Amber,     "Amber"),
                (Orange,    "Orange"),
                (Crimson,   "Crimson"),
                (Rose,      "Rose"),
                (Fuchsia,   "Fuchsia"),
                (Violet,    "Violet"),
                (Indigo,    "Indigo"),
                (Slate,     "Slate"),
                (Black,     "Black"),
            };

        /// <summary>
        /// Maps a preset key to its colour. GlassBlue, Custom and any
        /// unknown key return null = follow the theme's built-in blue
        /// (GlassBlue) or resolve elsewhere (Custom).
        /// </summary>
        public static Color? ColorFor(string presetKey)
        {
            return presetKey switch
            {
                Emerald   => Color.FromRgb(0x05, 0x96, 0x69),
                Amber     => Color.FromRgb(0xD9, 0x77, 0x06),
                Rose      => Color.FromRgb(0xE1, 0x1D, 0x48),
                Violet    => Color.FromRgb(0x7C, 0x3A, 0xED),
                Teal      => Color.FromRgb(0x0D, 0x94, 0x88),
                Black     => Color.FromRgb(0x11, 0x18, 0x27),
                Sky       => Color.FromRgb(0x02, 0x84, 0xC7),
                RoyalBlue => Color.FromRgb(0x25, 0x63, 0xEB),
                Indigo    => Color.FromRgb(0x4F, 0x46, 0xE5),
                Fuchsia   => Color.FromRgb(0xC0, 0x26, 0xD3),
                Orange    => Color.FromRgb(0xEA, 0x58, 0x0C),
                Crimson   => Color.FromRgb(0xB9, 0x1C, 0x1C),
                Gold      => Color.FromRgb(0xCA, 0x8A, 0x04),
                Lime      => Color.FromRgb(0x65, 0xA3, 0x0D),
                Cyan      => Color.FromRgb(0x08, 0x91, 0xB2),
                Slate     => Color.FromRgb(0x47, 0x55, 0x69),
                _         => (Color?)null   // Glass Blue = theme default
            };
        }

        /// <summary>Swatch colour shown on the Settings chip itself.
        /// GlassBlue maps to the default brand blue so the chip always
        /// shows a real preview dot.</summary>
        public static Color SwatchFor(string presetKey)
        {
            return ColorFor(presetKey) ?? Color.FromRgb(0x03, 0x69, 0xA1);
        }

        /// <summary>
        /// Tries to parse a user-typed colour (#RGB, #RRGGBB or a named
        /// CSS colour). Returns null on anything unparseable — callers
        /// show a validation hint instead of throwing.
        /// </summary>
        public static Color? TryParseHex(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var converted = ColorConverter.ConvertFromString(text.Trim());
                if (converted is Color c) return c;
            }
            catch (System.Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[AccentPresets] Custom colour parse failed for '" +
                    text + "': " + ex.Message);
            }
            return null;
        }
    }
}
