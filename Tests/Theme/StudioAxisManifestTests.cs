// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/StudioAxisManifestTests.cs
// PROJECT: ProGlassAutomation.Tests
// ─────────────────────────────────────────────────────────────────────────
// Phase-4 audit deliverable (2026-09-05 — THEME STUDIO v2):
//
// The studio has 19 axes (sliders). Before Phase 4, three hand-maintained
// lists had to agree: the POCO properties in ThemeStudioSettings, the
// "Studio{Axis}" properties on the SettingsViewModel partial, and the
// per-slider key→property switch in ResetStudioSlider. F-03/F-06 taught
// us what drift between such lists costs (silent dead UI, "reset does
// nothing" class).
//
// Phase 4 made ThemeStudioSettings.AxisNames the SINGLE home of the
// axis set; the VM's ResetStudioSlider now resolves through the
// reflection API (SetAxis / ResetAxisTo) instead of a second switch.
// These tests are the drift lock that keeps the single home honest:
//
//   1. AxisNames ≡ the public double-property set of the POCO
//      (add an axis in ONE of the two → red build, name and all).
//   2. The VM exposes a writable "Studio{Axis}" double per axis —
//      asserted BY TYPE REFLECTION ONLY: the ViewModel needs services
//      and is never constructed on a test runner.
//   3. Every preset (incl. the new industrial pack Ledger / Workshop /
//      HMI Steel) keeps all 19 axes inside the 0..100 percent scale —
//      the service treats them as percentages; a 0..1 value would
//      silently nullify a preset's effect.
//
// No WPF Application is constructed; everything here is static
// catalogue data or POCO manipulation (safe on any runner).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProGlassAutomation.Services.Theme;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class StudioAxisManifestTests
    {
        // The VM partial that owns the slider surface. Direct typeof:
        // a renamed/missing class is a compile error in THIS project —
        // that is exactly the drift lock we want (typeof never
        // constructs, so no services are touched on the runner).
        private static Type StudioVmType() =>
            typeof(ProGlassAutomation.ViewModels.Settings.SettingsViewModel);

        private static HashSet<string> PocoDoubleProperties() =>
            typeof(ThemeStudioSettings)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(double))
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

        // ── 1. the registry IS the property set (both directions) ──────

        [Fact]
        public void AxisNames_MatchEveryPublicDoubleProperty()
        {
            var poco = PocoDoubleProperties();
            var registry = new HashSet<string>(
                ThemeStudioSettings.AxisNames, StringComparer.Ordinal);

            // Every axis in the registry must exist as a writable
            // double on the POCO (SetAxis/ResetAxisTo rely on this).
            foreach (var axis in ThemeStudioSettings.AxisNames)
                Assert.Contains(axis, poco);

            // …and no double property may exist OUTSIDE the registry —
            // that would be a slider-less axis that presets could still
            // tune but operators could never see.
            Assert.Equal(poco.Count, registry.Count);
        }

        [Fact]
        public void AxisNames_AreUnique_AndNineteen()
        {
            // 19 axes: 2 basic + 2 glass + 2 geometry + 3 shadow +
            // 3 border + 2 background + 2 motion + 3 colour system.
            // The count is pinned so dropping/duplicating one is loud.
            Assert.Equal(19, ThemeStudioSettings.AxisNames.Count);
            Assert.Equal(
                ThemeStudioSettings.AxisNames.Count,
                ThemeStudioSettings.AxisNames
                    .Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void AxisNames_UseUiReadingOrder()
        {
            // The studio tile renders sections basic → geometry →
            // shadow → border → background → motion → colour. The
            // registry pins that order so generated surfaces stay in
            // the reading order operators were trained on.
            var expected = new[]
            {
                "Frostness", "Transparency", "BlurIntensity", "GlassTint",
                "CornerRoundness", "CardPadding",
                "ShadowIntensity", "ShadowSoftness", "ShadowDepth",
                "BorderVisibility", "BorderOpacity", "BorderThickness",
                "BackgroundVisibility", "BackgroundGlow",
                "HighlightStrength", "AnimationIntensity",
                "ColorRichness", "TextStrength", "WaterFlow",
            };
            Assert.Equal(expected, ThemeStudioSettings.AxisNames);
        }

        // ── 2. the VM slider surface (type reflection — no services) ──

        [Fact]
        public void ViewModel_ExposesWritableStudioPropertyPerAxis()
        {
            var vm = StudioVmType();

            foreach (var axis in ThemeStudioSettings.AxisNames)
            {
                var prop = vm.GetProperty(
                    "Studio" + axis,
                    BindingFlags.Public | BindingFlags.Instance);

                Assert.True(
                    prop != null,
                    $"SettingsViewModel is missing Studio{axis} — " +
                    "the axis exists in the registry but has no slider.");
                Assert.Equal(typeof(double), prop!.PropertyType);
                Assert.True(prop.CanRead && prop.CanWrite,
                    $"Studio{axis} must be a read/write double.");
            }
        }

        // ── 3. every preset stays inside the percent scale ─────────────

        [Fact]
        public void EveryPreset_KeepsAllAxesWithinZeroToHundred()
        {
            // PresetNames is the catalogue's single truth (parity tests
            // already pin Preset() to it). Industrial pack included.
            Assert.Contains("Ledger", ThemeStudioSettings.PresetNames);
            Assert.Contains("Workshop", ThemeStudioSettings.PresetNames);
            Assert.Contains("HMI Steel", ThemeStudioSettings.PresetNames);

            foreach (var presetName in ThemeStudioSettings.PresetNames)
            {
                var preset = ThemeStudioSettings.Preset(presetName);

                foreach (var axis in ThemeStudioSettings.AxisNames)
                {
                    var prop = typeof(ThemeStudioSettings)
                        .GetProperty(axis)!;
                    var value = (double)prop.GetValue(preset)!;

                    Assert.InRange(
                        value, 0.0, 100.0); // percent scale, inclusive
                }
            }
        }

        // ── 4. the reflection API itself behaves ───────────────────────

        [Fact]
        public void SetAxis_WritesByName_AndResetAxisToRestores()
        {
            var live = ThemeStudioSettings.Preset("Ledger");
            var defaults = ThemeStudioSettings.DefaultsFor("Normal");

            // Unknown keys refuse cleanly instead of throwing (the
            // per-slider chip passes XAML command parameters — a rogue
            // one must not crash the settings page).
            Assert.False(live.SetAxis("Nonsense", 42));
            Assert.False(live.ResetAxisTo("Nonsense", defaults));

            // Every axis round-trips: write a foreign value by name,
            // then restore from the family defaults.
            foreach (var axis in ThemeStudioSettings.AxisNames)
            {
                var prop = typeof(ThemeStudioSettings).GetProperty(axis)!;
                var before = (double)prop.GetValue(defaults)!;

                Assert.True(live.SetAxis(axis, 99.0));
                Assert.Equal(99.0, (double)prop.GetValue(live)!);

                Assert.True(live.ResetAxisTo(axis, defaults));
                Assert.Equal(before, (double)prop.GetValue(live)!);
            }
        }

        [Fact]
        public void ResetAxisTo_NullDefaultsRefusesCleanly()
        {
            var live = ThemeStudioSettings.Preset("Workshop");
            Assert.False(live.ResetAxisTo("Frostness", null!));
        }
    }
}
