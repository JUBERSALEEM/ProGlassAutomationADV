// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ThemeRegistryParityTests.cs
// PROJECT: ProGlassAutomation.Tests
// ─────────────────────────────────────────────────────────────────────────
// Phase-1 audit deliverable (2026-09-05 — SETTINGS_MODULE_AUDIT.md):
// every bug in the F-01..F-05 class was a REGISTRY DRIFT — two lists that
// must agree, maintained by hand, silently disagreeing:
//
//   F-01  family painter vs the managed-key contract      (ERR-50472F)
//   F-03  VM preset combo  vs Preset() switch cases        (DenseGlass)
//   F-06  VM mode-coupling switch vs engine family data
//
// These tests pin the registries so a future drift is a red build,
// not a customer crash report. No WPF Application is constructed —
// everything asserted here is static catalogue data (safe on any runner).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProGlassAutomation.Services.Theme;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class ThemeRegistryParityTests
    {
        // The complete family catalogue (the nine shipped families).
        private static readonly string[] AllFamilies =
        {
            VisualThemeEngine.Normal,
            VisualThemeEngine.Monet,
            VisualThemeEngine.Professional,
            VisualThemeEngine.LiquidGlass,
            VisualThemeEngine.CrystalFluid,
            VisualThemeEngine.Ocean,
            VisualThemeEngine.IndustrialWhite,
            VisualThemeEngine.IndustrialGrey,
            VisualThemeEngine.IndustrialDark,
        };

        // ── F-01: the managed-key contract ──────────────────────────────

        private static string[] ManagedKeys()
        {
            var field = typeof(VisualThemeEngine).GetField(
                "ManagedKeys",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            return (string[])field!.GetValue(null)!;
        }

        [Fact]
        public void ManagedKeys_ContainsEveryCrashCriticalBrush()
        {
            // ERR-50472F proved these must ALWAYS be family-managed:
            // ClearOverrides strips them and every family republishes
            // them. If one disappears from this list, a template can
            // inflate against an absent key again — that crash class
            // must never return.
            var critical = new[]
            {
                "WindowBackgroundBrush", "ModuleBackgroundBrush",
                "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
                "MenuHoverBrush",
                "DashLiquidSurfaceBrush", "DashAmbientBrush",
                "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
                "ShellLiquidBrush", "SidebarCardBrush",
                "LiquidGlassBrush", "LiquidGlassSubtleBrush",
                "LiquidGlassStrongBrush",
                "LiquidEdgeBrush", "LiquidInnerHighlightBrush",
                "LiquidReflectionBrush",
                "LiquidControlHoverBrush", "LiquidControlPressedBrush",
                "LiquidControlDisabledBrush",
                "LiquidFocusBrush", "LiquidSelectionBrush",
                "LiquidPrimaryButtonBrush", "LiquidOverlayBrush",
                "LiquidShadowColor",
                "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
                "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
                "TintGrayBrush",
                "CardCornerRadius", "CardCornerRadiusLarge",
                "ControlCornerRadius",
                "PrimaryForegroundBrush", "SecondaryForegroundBrush",
                "MutedForegroundBrush", "TextPrimaryBrush",
                "TextSecondaryBrush",
            };

            var managed = ManagedKeys();
            foreach (var key in critical)
                Assert.Contains(key, managed);
        }

        [Fact]
        public void ManagedKeys_DoesNotContainAccentOwnedOutlines()
        {
            // Documented engine decision (VisualThemeEngine.Part1):
            // OutlineBrush / OutlineDefaultBrush / BorderBrush /
            // DividerBrush are written by ThemeManager.ApplyAccent LAST
            // on every paint. Listing them in ManagedKeys would make
            // ClearOverrides delete the accent-derived keyline every
            // family switch — a known historical bug. Keep them OUT.
            var managed = ManagedKeys();
            Assert.DoesNotContain("OutlineBrush", managed);
            Assert.DoesNotContain("OutlineDefaultBrush", managed);
            Assert.DoesNotContain("BorderBrush", managed);
            Assert.DoesNotContain("DividerBrush", managed);
        }

        // ── F-01 net: the integrity guarantee must cover the crash set ──

        private static string[] CriticalFallbackKeys(bool dark)
        {
            var method = typeof(VisualThemeEngine).GetMethod(
                "CriticalFallbacks",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var sequence = (System.Collections.IEnumerable)method!
                .Invoke(null, new object[] { dark })!;
            var keys = new List<string>();
            foreach (var item in sequence)
            {
                var field = item.GetType().GetField("Item1");
                Assert.NotNull(field);
                keys.Add((string)field!.GetValue(item)!);
            }
            return keys.ToArray();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CriticalFallbacks_CoverTheErpCrashSet(bool dark)
        {
            // The pre-seed (ERR-50472F fix) can only close the never-
            // absent window for keys this list knows. Every key the
            // settings/dashboard chrome cannot live without is pinned.
            var netCritical = new[]
            {
                "WindowBackgroundBrush", "ModuleBackgroundBrush",
                "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
                "MenuHoverBrush", "SidebarCardBrush", "ShellLiquidBrush",
                "DashLiquidSurfaceBrush", "DashAmbientBrush",
                "OutlineBrush", "BorderBrush", "DividerBrush",
                "PrimaryForegroundBrush", "SecondaryForegroundBrush",
                "MutedForegroundBrush", "AccentBrush",
                "LiquidGlassBrush", "LiquidGlassSubtleBrush",
                "LiquidGlassStrongBrush",
                "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
                "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
                "TintGrayBrush",
            };

            var coverage = CriticalFallbackKeys(dark);
            foreach (var key in netCritical)
                Assert.Contains(key, coverage);
        }

        // ── F-03: preset registry parity ────────────────────────────────

        [Fact]
        public void EveryRegisteredPreset_ProducesADistinctProfile()
        {
            // The VM combo lists exactly ThemeStudioSettings.PresetNames
            // (single source). Distinctness per name proves no named
            // preset silently collapsed onto the default arm — the
            // DenseGlass failure shape (F-03).
            var names = ThemeStudioSettings.PresetNames;
            Assert.True(names.Count >= 5,
                "registry must keep the shipped preset set");

            var profiles = names
                .Select(n => (Name: n, Profile: ThemeStudioSettings.Preset(n)))
                .ToList();

            foreach (var (_, profile) in profiles)
                Assert.True(profile.IsCustomized,
                    "a preset is by definition a customization");

            for (int i = 0; i < profiles.Count; i++)
                for (int j = i + 1; j < profiles.Count; j++)
                    Assert.False(
                        profiles[i].Profile.Matches(profiles[j].Profile),
                        $"presets '{profiles[i].Name}' and " +
                        $"'{profiles[j].Name}' render identically — one " +
                        $"of them is a phantom row in the combo");
        }

        [Fact]
        public void UnknownPreset_FallsBackSafelyWithoutLying()
        {
            // Persisted legacy/unknown names must not throw — the
            // default arm is the Dense Glass profile, which the
            // registry now also lists by name.
            var fallback = ThemeStudioSettings.Preset("__no_such_preset__");
            Assert.NotNull(fallback);
        }

        // ── F-06: mode coupling lives in the engine ─────────────────────

        [Theory]
        [InlineData(VisualThemeEngine.IndustrialDark, true)]
        [InlineData(VisualThemeEngine.IndustrialWhite, false)]
        [InlineData(VisualThemeEngine.IndustrialGrey, false)]
        [InlineData(VisualThemeEngine.Normal, null)]
        [InlineData(VisualThemeEngine.Monet, null)]
        [InlineData(VisualThemeEngine.Professional, null)]
        [InlineData(VisualThemeEngine.LiquidGlass, null)]
        // Crystal Fluid is light-only (2026-09-05 product decision).
        [InlineData(VisualThemeEngine.CrystalFluid, false)]
        [InlineData(VisualThemeEngine.Ocean, null)]
        public void ModePairFor_OnlyIndustrialAndCrystalCoupleTheMode(
            string family, bool? expected)
        {
            Assert.Equal(expected, VisualThemeEngine.ModePairFor(family));
        }

        // ── Catalogue coverage: defaults + scenes for every family ──────

        [Fact]
        public void DefaultsFor_EveryFamily_StaysInSliderRange()
        {
            foreach (var family in AllFamilies)
            {
                var profile = ThemeStudioSettings.DefaultsFor(family);
                Assert.NotNull(profile);

                foreach (var prop in typeof(ThemeStudioSettings)
                             .GetProperties(BindingFlags.Public |
                                            BindingFlags.Instance)
                             .Where(p => p.PropertyType == typeof(double)))
                {
                    double v = (double)prop.GetValue(profile)!;
                    Assert.True(v >= 0 && v <= 100,
                        $"{family}.{prop.Name} = {v} — outside the " +
                        $"0..100 slider domain");
                }
            }
        }

        [Fact]
        public void SceneCatalogue_CoversEveryFamily_Exactly()
        {
            var sceneKeys = BuiltinWallpaperScenes.All
                .Select(row => row.Key)
                .ToList();
            Assert.Equal(
                AllFamilies.OrderBy(k => k, StringComparer.Ordinal),
                sceneKeys.OrderBy(k => k, StringComparer.Ordinal));

            // ...and the painter actually renders each one (never throws,
            // never returns null).
            foreach (var family in AllFamilies)
                Assert.NotNull(BuiltinWallpaperScenes.For(family));
        }
    }
}
