// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/AboutSurfaceTokens.cs
// PROJECT: ProGlassAutomation
// MODULE: About — card geometry + card edge, driven by the live engine
//
// WHY THIS EXISTS (owner report, 2026-09-16)
// ─────────────────────────────────────────
//   "why about page cards still not accepting dynamic card corner border
//    color from monet engine etc."
//
// Because TWO About contracts existed and never met:
//
//   ENGINE SIDE (published live, cleaned up on every family switch):
//     AboutCardCornerRadius, AboutPanelCornerRadius, AboutCardEffect,
//     AboutCardBorderBrush, AboutDividerBrush, AboutInteractive*,
//     AboutGlass* — and this file's own reading of the audit showed the
//     About palettes among those are referenced by ZERO view files.
//
//   VIEW SIDE (read by all nine About XAML files):
//     Ios26RadiusCard / Inner / Tile / Chip / Small / Bar,
//     Ios26CardBorderBrush, Ios26CardShadow — of which the palette is
//     theme-owned (Light/Dark only) and the six radii were LOCAL CONSTANTS
//     inside Views/About/AboutIos26Theme.xaml.
//
// So the engine changed keys nobody read, and About read keys nobody
// changed. The radius authority of the whole application is
// ThemeManager.ApplyCardCornerRadius — the Appearance "card corner radius"
// preference (default 12, clamped 0..28), which families and the theme
// pipeline also write through CardCornerRadius / Radius.* primitives. Every
// other card in the app follows it; the About cards did not follow anything.
//
// WHAT THIS DOES
// ──────────────
// Republish the five live About radii from that ONE authority, using the
// approved mockup's own proportions so the DEFAULT configuration renders
// the approved design exactly:
//
//   About key            ratio to the live card radius   at default 12
//   ─────────────────    ─────────────────────────────   ────────────
//   Ios26RadiusCard      26/12  (hero rounding)                    26
//   Ios26RadiusInner     12/12                                     12
//   Ios26RadiusTile      10/12                                     10
//   Ios26RadiusChip      12/12                                     12
//   Ios26RadiusSmall      6/12                                      6
//
// Every ratio is the mockup value divided by the mockup's 12 px card
// radius, so the design is not "close to" what was approved at the default
// setting — it is exactly what was approved, and it scales from there.
// Ios26RadiusBar is a local About preview token, not part of this scale.
// 2026-09-19 owner correction: it is now 12 px (no 999 full round remains).
//
// The card EDGE follows the engine's own About edge token
// (AboutCardBorderBrush, accent-derived and republished by
// ThemeManager.PublishAboutPalette on every accent/family pass), falling
// back to the theme dictionary's Ios26CardBorderBrush when the engine has
// not published one. That is what makes the card outline track the engine
// and the accent instead of only Light/Dark.
//
// Pure functions first so the locks can test the maths without a WPF
// Application; Publish() is the only WPF-touching member.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;

namespace ProGlassAutomation.Services.Theme
{
    internal static class AboutSurfaceTokens
    {
        /// <summary>The mockup's own card radius — the design's reference
        /// point. Every ratio below is expressed against it.</summary>
        internal const double MockupCardRadius = 12.0;

        /// <summary>The approved mockup corner radii, at the default card
        /// radius. AboutSurfaceAuditTests pins these against the XAML
        /// fallbacks, so this table and the design cannot drift apart.</summary>
        internal static readonly string[] RadiusKeys =
        {
            "Ios26RadiusCard", "Ios26RadiusInner", "Ios26RadiusTile",
            "Ios26RadiusChip", "Ios26RadiusSmall",
        };

        internal static readonly double[] MockupRadii = { 26, 12, 10, 12, 6 };

        /// <summary>Upper bound on every derived radius. A card that is
        /// rounder than a pill stops reading as a card; the Appearance
        /// slider can reach 28, and 28 * 26/12 would be 61.</summary>
        internal const double MaxRadius = 48.0;

        /// <summary>Whole-pixel radius for one scale step at a given live
        /// card radius. Rounded (not truncated) so the design's generous
        /// rounding is preserved as the slider moves.</summary>
        internal static double Radius(int index, double cardRadius)
        {
            if (index < 0 || index >= MockupRadii.Length) return MockupCardRadius;

            if (cardRadius < 0) cardRadius = 0;

            double scaled = cardRadius * (MockupRadii[index] / MockupCardRadius);
            return Math.Round(Math.Min(MaxRadius, Math.Max(0, scaled)));
        }

        /// <summary>The whole scale for a card radius, same order as
        /// <see cref="RadiusKeys"/>.</summary>
        internal static double[] Radii(double cardRadius)
        {
            var radii = new double[RadiusKeys.Length];
            for (int i = 0; i < radii.Length; i++)
            {
                radii[i] = Radius(i, cardRadius);
            }

            return radii;
        }

        /// <summary>Reads the live card radius from the resource scope.
        /// CardCornerRadius is a CornerRadius token (that is what the
        /// Appearance slider, the family engines and ReapplyCardCornerRadius
        /// all write), so it is read as one and reduced to its top-left
        /// value — the same number on every corner.</summary>
        internal static double ReadCardRadius(ResourceDictionary? resources, double fallback)
        {
            if (resources != null && resources.Contains("CardCornerRadius"))
            {
                switch (resources["CardCornerRadius"])
                {
                    case CornerRadius cr:
                        return cr.TopLeft;
                    case double d:
                        return d;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Republishes the About radius scale and the About card edge from
        /// the live engine tokens. Called after every theme/family apply and
        /// after every card-radius change, so the About module moves with
        /// the rest of the application.
        /// </summary>
        internal static void Publish(ResourceDictionary? resources, double fallbackCardRadius)
        {
            if (resources == null) return;

            double cardRadius = ReadCardRadius(resources, fallbackCardRadius);
            var radii = Radii(cardRadius);

            for (int i = 0; i < RadiusKeys.Length && i < radii.Length; i++)
            {
                // Indexer writes (never Add/Remove) so each About view's
                // {DynamicResource Ios26Radius*} re-evaluates in the same pass.
                resources[RadiusKeys[i]] = new CornerRadius(radii[i]);
            }

            // Card edge: the engine's own About edge token when it exists,
            // otherwise the theme dictionary keeps ownership and nothing is
            // written. Registering the alias here is what makes the outline
            // follow the accent / family instead of only Light/Dark.
            if (resources.Contains("AboutCardBorderBrush"))
            {
                resources["Ios26CardBorderBrush"] = resources["AboutCardBorderBrush"];
            }
        }
    }
}
