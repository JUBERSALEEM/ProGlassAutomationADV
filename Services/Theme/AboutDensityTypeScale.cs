// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/AboutDensityTypeScale.cs
// PROJECT: ProGlassAutomation
// MODULE: About — the module type scale, re-derived per density profile
//
// WHY THIS EXISTS
// ───────────────
// 2026-09-15 owner directive: "convert all cards and fonts safely move to
// dynamic so this about module can adapt the theme style … Keeping the
// cards as it is design."
//
// The 15 Ios26FontSize* tokens used to be declared inside
// Views/About/AboutIos26Theme.xaml — a dictionary merged LOCALLY by every
// About view. A locally declared key shadows Application.Resources, so
// nothing at application level could rewrite it and the About type scale
// was frozen: changing the density profile reflowed the whole app and left
// About on one size ladder.
//
// The tokens now live in Themes/Styles/AboutTypeScale.xaml (application
// level) and THIS class rewrites them, in place, every time the density
// profile changes (ThemeManager.ApplyDisplayDensity → Publish).
//
// THE RULES
// ─────────
//  • Compact is the default profile AND the approved mockup scale, so its
//    lift is 0 and the default rendering does not move by one pixel.
//  • Only sizes >= GrowthFloor (13 px) grow. Micro labels (10 / 11 / 12)
//    stay put at every density, so the label hierarchy never collides with
//    the value hierarchy in a label/value row.
//  • Every result is a whole number. That is not cosmetic: under
//    TextOptions.TextFormattingMode="Display" a fractional size is hinted
//    at a half-pixel grid, stems land between pixels, and the Bold /
//    SemiBold cuts read muddy — the exact defect the 2026-09-15 typography
//    pass removed. The audit lock
//    About_type_scale_stays_whole_pixel_at_every_density asserts it for
//    every profile.
//  • The lift is clamped at MaxLift so the hero display size never passes
//    the global FontSize.Display ceiling (32 px).
//
// This class is deliberately pure (no WPF beyond the dictionary write) so
// the lock can call Sizes() directly instead of instantiating an
// Application.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;

namespace ProGlassAutomation.Services.Theme
{
    internal static class AboutDensityTypeScale
    {
        /// <summary>The 15 About size tokens, smallest first. Order matters:
        /// the audit lock asserts the sequence stays strictly increasing.</summary>
        internal static readonly string[] Keys =
        {
            "Ios26FontSizeCaption",
            "Ios26FontSizeLabel",
            "Ios26FontSizeSmall",
            "Ios26FontSizeBody",
            "Ios26FontSizeValue",
            "Ios26FontSizeSubtitle",
            "Ios26FontSizeCardTitleSm",
            "Ios26FontSizeCardTitle",
            "Ios26FontSizeSectionTitle",
            "Ios26FontSizeGlyph",
            "Ios26FontSizeH2",
            "Ios26FontSizeH1",
            "Ios26FontSizeMetric",
            "Ios26FontSizeStat",
            "Ios26FontSizeDisplay",
        };

        /// <summary>The approved Compact (mockup) ladder, matching
        /// Themes/Styles/AboutTypeScale.xaml value for value.</summary>
        private static readonly int[] CompactSizes =
        {
            10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 22, 24, 28, 30,
        };

        /// <summary>Sizes below this never grow — micro labels keep their
        /// distance from the values they label.</summary>
        internal const int GrowthFloor = 13;

        /// <summary>Ceiling on the per-profile lift (see the header).</summary>
        internal const int MaxLift = 2;

        /// <summary>Whole-pixel lift for a density profile. Unknown or missing
        /// profiles fall through to Compact (0), which is the safe default:
        /// it renders the approved scale rather than a guessed one.</summary>
        internal static int Lift(string density)
        {
            switch (density)
            {
                case "Normal": return 1;
                case "Comfortable": return 2;
                case "Spacious": return 2; // clamped by MaxLift
                default: return 0;         // Compact and anything unrecognised
            }
        }

        /// <summary>The whole-pixel ladder for a density profile.</summary>
        internal static double[] Sizes(string density)
        {
            int lift = Math.Min(MaxLift, Math.Max(0, Lift(density)));
            var sizes = new double[CompactSizes.Length];

            for (int i = 0; i < CompactSizes.Length; i++)
            {
                int size = CompactSizes[i];
                sizes[i] = size >= GrowthFloor ? size + lift : size;
            }

            return sizes;
        }

        /// <summary>Writes the ladder into the application resource scope.
        /// Indexer writes (not Add/Remove) so every About view's
        /// {DynamicResource Ios26FontSize*} re-evaluates in the same pass.</summary>
        internal static void Publish(ResourceDictionary resources, string density)
        {
            if (resources == null) return;

            var sizes = Sizes(density);
            for (int i = 0; i < Keys.Length && i < sizes.Length; i++)
            {
                // Boxed as double on purpose: the XAML fallback is a
                // <sys:Double>, and FontSize is a double property — handing
                // the resource system an int would lean on an implicit
                // conversion at binding time.
                resources[Keys[i]] = sizes[i];
            }
        }
    }
}
