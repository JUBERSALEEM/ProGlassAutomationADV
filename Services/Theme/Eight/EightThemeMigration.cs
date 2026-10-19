// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Eight/EightThemeMigration.cs
// PROJECT: ProGlassAutomation
// MODULE: Eight-theme engine — legacy identity migration
//
// WHY THIS FILE IS DELICATE
// ───────────────────────────────────────────────────────────────────────
// Every user's settings file already contains a VisualTheme string. Those
// strings are PERSISTED KEYS and they name implementation details, so they
// can never simply be deleted — the saved value has to be translated into
// the eight-family contract exactly once, on read.
//
// Three failure modes this file exists to prevent:
//
//   1. SILENT RESET. An unknown or empty value resolving to the FIRST
//      catalogue entry means the user's appearance changes every time the
//      Theme Center is reordered. It must resolve to the shipped DEFAULT.
//
//   2. CHAIN MIGRATION. A value is translated every time it is read, so a
//      profile that is saved back must not be re-translated on the next
//      read. Migration is therefore ONE-SHOT and writes a marker — this is
//      the bug where a theme visibly "drifted" a step on every launch.
//
//   3. LEGACY FAMILY LOSS. Two retired families have a genuinely correct
//      destination in the new contract, and losing them would silently
//      change how somebody's installation looks. The map is written out
//      explicitly rather than falling through to a default.
//
// THE LEGACY MAP, AND WHY EACH ARROW POINTS WHERE IT DOES
// ───────────────────────────────────────────────────────────────────────
//   Normal (Industrial) → Bento Grid      A warm cream canvas with paper
//                                         cards, stone ink and NO
//                                         translucency. Bento Grid is the
//                                         only family in the new contract
//                                         that is structurally solid, so
//                                         it is the honest landing.
//   Normal               → Bento Grid      Same family, older key.
//   Monet                → Holographic     The one legacy family whose
//                                         palette was DERIVED live from the
//                                         user's accent colour. So is
//                                         Holographic's, and it is the
//                                         only family where the accent
//                                         keeps re-driving the surface.
//   Crystal Fluid        → Liquid Glass    The most-developed material
//                                         this codebase has ever shipped
//                                         (Themes/CrystalFluid). Liquid
//                                         Glass is its direct successor,
//                                         so this migration is
//                                         near-invisible to the user.
//   Ios-26 / Ios26       → Glassmorphism   Apple's grouped-grey light
//                                         stage with translucent chrome is
//                                         literally the 2020 glass
//                                         recipe.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Theme.Eight
{
    /// <summary>One-shot translation of retired family identities into the
    /// eight-family contract.</summary>
    public static class EightThemeMigration
    {
        /// <summary>Config key that records the migration has run. Its
        /// presence is what makes the translation one-shot.</summary>
        public const string MarkerKey = "EightThemeContractMigrated";

        /// <summary>
        /// Legacy identity → eight-family identity. Ordinal-ignore-case
        /// matching, so "ios-26" and "Ios-26" both land correctly.
        /// </summary>
        private static readonly Dictionary<string, string> LegacyMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Retired industrial trio and the cream SaaS default.
                ["Normal (Industrial)"] = EightThemeNames.BentoGrid,
                ["Normal"] = EightThemeNames.BentoGrid,
                ["Professional"] = EightThemeNames.BentoGrid,
                ["Industrial White"] = EightThemeNames.BentoGrid,
                ["Industrial Grey"] = EightThemeNames.BentoGrid,
                ["Industrial Dark"] = EightThemeNames.BentoGrid,

                // Accent-derived colour → the family that still derives
                // its surfaces from the live accent.
                ["Monet"] = EightThemeNames.Holographic,

                // The water-glass theatre → its direct successor.
                ["Crystal Fluid"] = EightThemeNames.LiquidGlass,
                ["Crystal"] = EightThemeNames.LiquidGlass,

                // Apple grouped-grey translucent chrome → the original
                // frosted recipe.
                ["Ios-26"] = EightThemeNames.Glassmorphism,
                ["Ios26"] = EightThemeNames.Glassmorphism,
                ["iOS 26"] = EightThemeNames.Glassmorphism,
                ["iPhone 17 Pro Max"] = EightThemeNames.LiquidGlass,
            };

        /// <summary>
        /// True when <paramref name="saved"/> is one of the eight current
        /// identities and needs no translation. This is the check that keeps
        /// the migration one-shot without a marker round-trip: a value that
        /// is already canonical is returned untouched, so re-reading a
        /// migrated config can never drift it again.
        /// </summary>
        public static bool IsCanonical(string? saved) =>
            EightThemeCatalog.IsKnown(saved);

        /// <summary>
        /// Translates any persisted identity into the current contract.
        /// Unknown, empty and null values resolve to the shipped DEFAULT —
        /// never to the first catalogue entry.
        /// </summary>
        public static string Normalize(string? saved)
        {
            if (string.IsNullOrWhiteSpace(saved)) return EightThemeNames.Default;
            string trimmed = saved!.Trim();

            if (EightThemeCatalog.IsKnown(trimmed))
                return EightThemeCatalog.Resolve(trimmed).Identity;

            if (LegacyMap.TryGetValue(trimmed, out var mapped)
                && EightThemeCatalog.IsKnown(mapped))
                return mapped;

            return EightThemeNames.Default;
        }

        /// <summary>
        /// Whether <paramref name="saved"/> was a RETIRED identity, as
        /// opposed to an already-current one or a value that never made
        /// sense. Used to decide whether to write the migration marker and
        /// to surface a one-time "your theme was upgraded" notice.
        /// </summary>
        public static bool WasLegacy(string? saved)
        {
            if (string.IsNullOrWhiteSpace(saved)) return false;
            if (EightThemeCatalog.IsKnown(saved!.Trim())) return false;
            return LegacyMap.ContainsKey(saved.Trim());
        }

        /// <summary>
        /// Every legacy identity this migration knows about. Exposed so a
        /// regression test can assert the map is not silently emptied — a
        /// migration table that loses an entry degrades every user on that
        /// theme to the default with no error anywhere.
        /// </summary>
        public static IReadOnlyCollection<string> KnownLegacyIdentities =>
            LegacyMap.Keys;
    }
}