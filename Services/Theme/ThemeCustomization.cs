// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/ThemeCustomization.cs
// PROJECT: ProGlassAutomation
// MODULE: Theme customization (2026-09-06) — the bounded, family-aware
//         Settings surface that replaces the removed Theme Studio.
//
// The Theme Studio (0–100 playground) was removed 2026-09-06; this class
// restores its ONE legitimate capability — tuning the Crystal Fluid
// (Ios-17pm) material — as a bounded, validated, persisted surface:
//
//   · SIX live axes (the authored set with real DynamicResource
//     consumers): water opacity/speed, wave amplitude, caustic and
//     bubble intensity, reflection opacity. Glass/glass-border/shadow
//     opacity stay parked (CrystalFluidSettings) until a live key
//     exists — no fake controls.
//   · Bounds are consts here (single number home for the Settings
//     sliders too); every read is clamped so a corrupt save can never
//     paint an out-of-range token.
//   · Named presets (Default / Calm / Intense) replace the old random
//     "Quick Looks"; Default == the authored baselines.
//   · Normal/Monet have no material axes — their customization is the
//     existing Appearance fields (CardCornerRadius, CardBorderThickness,
//     UseGradientSurfaces), so this class never touches them.
//
// Single source of truth: the persisted values live in
// AppConfig.Appearance.ThemeCustomization; the authored numbers live in
// CrystalFluidSettings.Baseline*; this class only CLAMPS and APPLIES.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using ProGlassAutomation.Services.AI.AIConfig;

namespace ProGlassAutomation.Services.Theme
{
    public static class ThemeCustomization
    {
        #region ── Axis bounds (single number home for sliders + clamp) ──

        // The minimums are the authored floors already homed in
        // CrystalFluidSettings (ex-studio master-mapping floors) — no
        // second copy of the numbers; the maxima are authored here.
        public const double MinWaterOpacity      = CrystalFluidSettings.FloorWaterOpacity;
        public const double MaxWaterOpacity      = 1.25;

        public const double MinWaterSpeed        = CrystalFluidSettings.FloorWaterSpeed;
        public const double MaxWaterSpeed        = 1.50;

        public const double MinWaveAmplitude     = CrystalFluidSettings.FloorWaveAmplitude;
        public const double MaxWaveAmplitude     = 4.5;

        public const double MinCausticIntensity  = CrystalFluidSettings.FloorCausticIntensity;
        public const double MaxCausticIntensity  = 1.00;

        public const double MinBubbleIntensity   = CrystalFluidSettings.FloorBubbleIntensity;
        public const double MaxBubbleIntensity   = 1.00;

        public const double MinReflectionOpacity = CrystalFluidSettings.FloorReflectionOpacity;
        public const double MaxReflectionOpacity = 1.00;

        #endregion

        #region ── Named presets ──

        /// <summary>One named material preset. Default is byte-identical
        /// to the authored baselines (CrystalFluidSettings.Baseline*).</summary>
        public sealed record Preset(
            string Key,
            double WaterOpacity,
            double WaterSpeed,
            double WaveAmplitude,
            double CausticIntensity,
            double BubbleIntensity,
            double ReflectionOpacity);

        public static readonly Preset[] Presets =
        {
            new("Default",
                CrystalFluidSettings.BaselineWaterOpacity,
                CrystalFluidSettings.BaselineWaterSpeed,
                CrystalFluidSettings.BaselineWaveAmplitude,
                CrystalFluidSettings.BaselineCausticIntensity,
                CrystalFluidSettings.BaselineBubbleIntensity,
                CrystalFluidSettings.BaselineReflectionOpacity),
            new("Calm",
                0.55, 0.75, 2.0, 0.40, 0.40, 0.45),
            new("Intense",
                1.20, 1.35, 3.8, 0.90, 0.90, 0.95),
        };

        public static Preset? FindPreset(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (var p in Presets)
                if (string.Equals(p.Key, key, StringComparison.Ordinal))
                    return p;
            return null;
        }

        #endregion

        #region ── Clamp / effective values ──

        public static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));

        /// <summary>The persisted customization for the active config
        /// (never null — a missing block resolves to a fresh baseline
        /// instance so a corrupt/hand-edited save cannot crash apply).</summary>
        public static ThemeCustomizationSettings Current =>
            App.Config?.Appearance?.ThemeCustomization
            ?? new ThemeCustomizationSettings();

        public static double WaterOpacity(ThemeCustomizationSettings? c) =>
            Clamp(c?.WaterOpacity
                  ?? CrystalFluidSettings.BaselineWaterOpacity,
                  MinWaterOpacity, MaxWaterOpacity);

        public static double WaterSpeed(ThemeCustomizationSettings? c) =>
            Clamp(c?.WaterSpeed
                  ?? CrystalFluidSettings.BaselineWaterSpeed,
                  MinWaterSpeed, MaxWaterSpeed);

        public static double WaveAmplitude(ThemeCustomizationSettings? c) =>
            Clamp(c?.WaveAmplitude
                  ?? CrystalFluidSettings.BaselineWaveAmplitude,
                  MinWaveAmplitude, MaxWaveAmplitude);

        public static double CausticIntensity(ThemeCustomizationSettings? c) =>
            Clamp(c?.CausticIntensity
                  ?? CrystalFluidSettings.BaselineCausticIntensity,
                  MinCausticIntensity, MaxCausticIntensity);

        public static double BubbleIntensity(ThemeCustomizationSettings? c) =>
            Clamp(c?.BubbleIntensity
                  ?? CrystalFluidSettings.BaselineBubbleIntensity,
                  MinBubbleIntensity, MaxBubbleIntensity);

        public static double ReflectionOpacity(ThemeCustomizationSettings? c) =>
            Clamp(c?.ReflectionOpacity
                  ?? CrystalFluidSettings.BaselineReflectionOpacity,
                  MinReflectionOpacity, MaxReflectionOpacity);

        #endregion

        #region ── Apply (single writer into the Crystal* tokens) ──

        /// <summary>
        /// Pushes the clamped, persisted Crystal Fluid axes into the
        /// root-level Crystal* tokens. Called by VisualThemeEngine on
        /// every family paint; the tokens are DynamicResource-bound, so
        /// surfaces repaint live. Never throws past its own guard — a
        /// customization write must never take the theme down.
        /// </summary>
        public static void Apply(ResourceDictionary resources)
        {
            if (resources == null) return;

            var c = App.Config?.Appearance?.ThemeCustomization;

            resources["CrystalWaterOpacity"]     = WaterOpacity(c);
            resources["CrystalWaterSpeed"]       = WaterSpeed(c);
            resources["CrystalWaveAmplitude"]    = WaveAmplitude(c);
            resources["CrystalCausticOpacity"]   = CausticIntensity(c);
            resources["CrystalBubbleOpacity"]    = BubbleIntensity(c);
            resources["CrystalReflectionOpacity"] = ReflectionOpacity(c);
        }

        /// <summary>
        /// Copies a named preset into the persisted customization AND the
        /// live resources, then notifies the settings façade so bindings
        /// refresh. The caller persists the config (SaveConfig) exactly
        /// like every other appearance setter.
        /// </summary>
        public static bool ApplyPreset(string key)
        {
            var preset = FindPreset(key);
            if (preset == null) return false;

            var appearance = App.Config?.Appearance;
            if (appearance == null) return false;

            // Defensive: a hand-edited save can set the block to null in
            // JSON; recover with a fresh baseline instance so a preset
            // click can never throw on a corrupt config.
            var c = appearance.ThemeCustomization;
            if (c == null)
            {
                c = new ThemeCustomizationSettings();
                appearance.ThemeCustomization = c;
            }

            c.WaterOpacity     = preset.WaterOpacity;
            c.WaterSpeed       = preset.WaterSpeed;
            c.WaveAmplitude    = preset.WaveAmplitude;
            c.CausticIntensity = preset.CausticIntensity;
            c.BubbleIntensity  = preset.BubbleIntensity;
            c.ReflectionOpacity = preset.ReflectionOpacity;
            c.CrystalPreset    = preset.Key;

            var res = Application.Current?.Resources;
            if (res != null)
                Apply(res);

            CrystalFluidSettings.NotifyRepainted();
            return true;
        }

        /// <summary>
        /// Resets the Crystal axes to the authored baselines (the
        /// "Default" preset) — the Settings "Reset" action.
        /// </summary>
        public static void Reset()
            => ApplyPreset("Default");

        #endregion
    }
}
