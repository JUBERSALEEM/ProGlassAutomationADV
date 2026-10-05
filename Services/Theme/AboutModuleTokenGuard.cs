// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/AboutModuleTokenGuard.cs
// PROJECT: ProGlassAutomation
// MODULE: About — runtime guarantee that the module's tokens exist
//
// WHY THIS EXISTS (owner report, 2026-09-16)
// ─────────────────────────────────────────
// The About page was converted from a locally declared palette to
// theme-owned tokens so it would follow Light / Dark / density. The report
// that came back was "cards not accepting dynamic colors … no border lines
// on cards". Those three symptoms are the signature of ONE failure mode:
//
//     a {DynamicResource} that cannot resolve does NOT throw — it leaves
//     the property at its default. For a Border that means BorderBrush =
//     null and Background = null, so the card renders as a shadow with no
//     edge and no fill; for a TextBlock it means FontSize falls back to
//     the inherited 12 px. Everything looks "frozen" and nothing is logged.
//
// The static analysis was clean: all 45 keys resolve on paper. But the
// About keys now come from three different places (both theme dictionaries,
// Themes/Styles/AppStage.xaml, Themes/Styles/AboutTypeScale.xaml), and any
// of them can be absent at runtime — a dictionary that did not compile into
// the binary, an overlay that skipped a key, a stale build. WPF reports
// none of that.
//
// WHAT THIS DOES
// ──────────────
// After every theme apply it checks all 45 keys and REPAIRS the missing
// ones from tokens that are guaranteed live (the same trick
// VisualThemeEngine.Ios26.SeedNeutralAboutTokens already uses for the About
// glass keys), then writes ONE warning line naming them. So the module can
// never render border-less and colour-less, and the next report of "nothing
// changed" arrives with the exact key list in the log instead of a guess.
//
// The repair deliberately does not invent values: each About key maps to a
// live semantic token (popup surface, hairline, ink, accent, tint). A
// repaired About looks like a themed card, not like the mockup — which is
// the correct failure mode: visible, diagnosable, never silent.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Theme
{
    internal static class AboutModuleTokenGuard
    {
        /// <summary>The five invariant stage colours, in the same order as
        /// <see cref="StageKeys"/>. AppStageAuditTests asserts these strings
        /// equal the declarations in Themes/Styles/AppStage.xaml, so this
        /// table can never drift from the design.</summary>
        internal static readonly string[] StageColors =
        {
            "#F1F5FB", // canvas
            "#BFE8E3", // orb — teal
            "#C5D8F6", // orb — blue
            "#CFD4F5", // orb — lavender
            "#D2EBD6", // orb — green
        };

        internal static readonly string[] StageKeys =
        {
            "Ios26CanvasBrush", "Ios26OrbTealBrush", "Ios26OrbBlueBrush",
            "Ios26OrbLavenderBrush", "Ios26OrbGreenBrush",
        };

        /// <summary>The 25 theme-owned About palette keys. Must match the
        /// declarations in both theme dictionaries (locked by
        /// AppStageAuditTests.About_guard_table_matches_the_theme_columns).</summary>
        internal static readonly string[] PaletteKeys =
        {
            "Ios26CardBrush", "Ios26CardBorderBrush", "Ios26RailBrush",
            "Ios26TileBrush", "Ios26TileBorderBrush", "Ios26HairlineBrush",
            "Ios26TrackBrush", "Ios26HoverBrush", "Ios26TextTitleBrush",
            "Ios26TextBodyBrush", "Ios26TextSecondaryBrush", "Ios26TextMutedBrush",
            "Ios26TextFaintBrush", "Ios26OnAccentBrush", "Ios26OnAccentChipBrush",
            "Ios26AccentBrush", "Ios26AccentDeepBrush", "Ios26AccentSoftBrush",
            "Ios26AccentSoftTextBrush", "Ios26SuccessBrush", "Ios26SuccessSoftBrush",
            "Ios26ValuePillBrush", "Ios26SearchBrush", "Ios26NumberBrush",
            "Ios26DangerDotBrush",
        };

        /// <summary>Live fallback token per palette key, same index. Every one
        /// of these is declared in BOTH theme dictionaries, so the repair can
        /// always resolve even when the About palette itself did not arrive.
        /// Index i of this array belongs to index i of <see cref="PaletteKeys"/>.</summary>
        internal static readonly string[] FallbackTokenKeys =
        {
            "PopupSurfaceBrush",     // Ios26CardBrush
            "BorderBrush",           // Ios26CardBorderBrush  — the card edge
            "PopupSurfaceBrush",     // Ios26RailBrush
            "ElevatedSurfaceBrush",  // Ios26TileBrush
            "BorderBrush",           // Ios26TileBorderBrush
            "DividerBrush",          // Ios26HairlineBrush
            "DividerBrush",          // Ios26TrackBrush
            "MenuHoverBrush",        // Ios26HoverBrush
            "TextPrimaryBrush",      // Ios26TextTitleBrush
            "TextPrimaryBrush",      // Ios26TextBodyBrush
            "TextSecondaryBrush",    // Ios26TextSecondaryBrush
            "MutedForegroundBrush",  // Ios26TextMutedBrush
            "MutedForegroundBrush",  // Ios26TextFaintBrush
            "OnAccentForegroundBrush", // Ios26OnAccentBrush
            "GlassSurfaceElevated",  // Ios26OnAccentChipBrush
            "AccentBrush",           // Ios26AccentBrush
            "LiquidAccentDeep",      // Ios26AccentDeepBrush
            "TintBlueBrush",         // Ios26AccentSoftBrush
            "AccentBrush",           // Ios26AccentSoftTextBrush
            "SuccessBrush",          // Ios26SuccessBrush
            "TintGreenBrush",        // Ios26SuccessSoftBrush
            "PopupSurfaceBrush",     // Ios26ValuePillBrush
            "MenuHoverBrush",        // Ios26SearchBrush
            "AccentBrush",           // Ios26NumberBrush
            "DangerBrush",           // Ios26DangerDotBrush
        };

        /// <summary>
        /// Repairs any absent About token and returns how many were repaired
        /// (0 = the module is fully tokenised at runtime, which is the healthy
        /// state). Called after every theme apply.
        /// </summary>
        internal static int Ensure(ResourceDictionary resources)
        {
            if (resources == null) return 0;

            var repaired = new List<string>();

            for (int i = 0; i < StageKeys.Length && i < StageColors.Length; i++)
            {
                if (resources.Contains(StageKeys[i])) continue;
                resources[StageKeys[i]] = Frozen(StageColors[i]);
                repaired.Add(StageKeys[i]);
            }

            for (int i = 0; i < PaletteKeys.Length && i < FallbackTokenKeys.Length; i++)
            {
                if (resources.Contains(PaletteKeys[i])) continue;

                var token = FallbackTokenKeys[i];
                if (!resources.Contains(token))
                {
                    // The fallback itself is missing: name the key anyway so the
                    // log still says WHICH About token has no live source.
                    repaired.Add(PaletteKeys[i] + " (no fallback: " + token + ")");
                    continue;
                }

                resources[PaletteKeys[i]] = resources[token];
                repaired.Add(PaletteKeys[i]);
            }

            // The 15 sizes come from Themes/Styles/AboutTypeScale.xaml and are
            // republished per density; if that file did not arrive, the About
            // typography would silently fall back to the inherited size.
            bool sizesMissing = false;
            for (int i = 0; i < AboutDensityTypeScale.Keys.Length; i++)
            {
                if (!resources.Contains(AboutDensityTypeScale.Keys[i]))
                {
                    sizesMissing = true;
                    break;
                }
            }

            if (sizesMissing)
            {
                AboutDensityTypeScale.Publish(resources, ThemeManager.DisplayDensity);
                repaired.Add("Ios26FontSize* (all " +
                    AboutDensityTypeScale.Keys.Length + " from AboutTypeScale.xaml)");
            }

            // Surface tokens (2026-09-16): the 5 radii and the card edge.
            // A card with no CornerRadius renders square and a card with a
            // null BorderBrush renders with no outline — both silently, which
            // is the failure this guard exists to make impossible.
            bool surfaceMissing = false;
            for (int i = 0; i < AboutSurfaceTokens.RadiusKeys.Length; i++)
            {
                if (!resources.Contains(AboutSurfaceTokens.RadiusKeys[i]))
                {
                    surfaceMissing = true;
                    break;
                }
            }

            if (!surfaceMissing && !resources.Contains("Ios26CardBorderBrush"))
            {
                surfaceMissing = true;
            }

            if (surfaceMissing)
            {
                AboutSurfaceTokens.Publish(resources, ThemeManager.CardCornerRadius);
                repaired.Add("Ios26Radius*/Ios26CardBorderBrush (About surface tokens)");
            }

            if (repaired.Count > 0)
            {
                AppLogService.Warning(
                    "[ThemeManager] About tokens repaired: " + repaired.Count +
                    " key(s) were missing at apply time — " +
                    string.Join(", ", repaired) +
                    ". The About module fell back to live theme tokens. If this " +
                    "line appears on a normal start, an About dictionary did not " +
                    "reach the running build (check App.xaml merges + a full Rebuild).",
                    new LogContext
                    {
                        Module = "Themes",
                        Operation = "EnsureAboutModuleTokens"
                    });
            }

            return repaired.Count;
        }

        private static SolidColorBrush Frozen(string hex)
        {
            var brush = new SolidColorBrush(ParseHex(hex));
            brush.Freeze();
            return brush;
        }

        /// <summary>Parses "#RRGGBB" without a type converter, so the guard has
        /// no dependency on locale, converter behaviour or nullable annotations.
        /// </summary>
        private static Color ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7 || hex[0] != '#')
                return Colors.Transparent;

            int value = Convert.ToInt32(hex.Substring(1, 6), 16);
            return Color.FromRgb(
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }
    }
}
