// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/CrystalFluidSettings.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM P3 (2026-09-05) — the authored CrystalFluidSettings surface
// (incoming file 19) integrated under the D31 rule: SINGLE SOURCE OF
// TRUTH. This façade owns NO parallel state:
//
//   · resource-backed axes (water speed/opacity, caustics, bubbles,
//     reflection, wave amplitude) READ the live application resources —
//     i.e. exactly what is on screen. Writes for those axes stay with
//     the Theme Studio (WaterFlow/HighlightStrength master sliders) and
//     the engine family paint, which is why those properties are
//     get-only here (independent per-channel spinners are a future
//     persisted axis — deliberately NOT invented now).
//   · ReducedMotion is two-way: the persisted truth is
//     App.Config.Appearance.ReducedMotion (the settings §43 toggle).
//   · The Baseline* consts are THE single home for the authored
//     baseline numbers: the engine baseline block and the studio master
//     mapping both consume these constants, and the XAML token defaults
//     are pinned to them by CrystalFluidSettingsBridgeTests (XAML cannot
//     reference consts — the test closes the gap).
//   · Glass/shadow/radius axes keep the authored values parked until a
//     live consumer key exists (P4+); they are immutable by design.
//
// Liveness: the engine and the studio call NotifyRepainted() after
// their writes, so every binding through this façade updates live.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// Centralized Crystal Fluid ("Ios-17pm") design-token surface —
    /// live, read-live façade over the app's persisted/in-memory truth.
    /// Supports Theme Studio binding and the reduced-motion preference.
    /// </summary>
    public sealed class CrystalFluidSettings : INotifyPropertyChanged
    {
        #region ── Authored baselines (single home for the numbers) ──

        public const double BaselineGlassOpacity        = 1.0;
        public const double BaselineGlassBorderOpacity  = 0.72;
        public const double BaselineReflectionOpacity   = 0.80;

        public const double BaselineWaterOpacity        = 1.0;
        public const double BaselineWaterSpeed          = 1.0;
        public const double BaselineWaveAmplitude       = 3.0;
        public const double BaselineCausticIntensity    = 0.70;
        public const double BaselineBubbleIntensity     = 0.70;

        public const double BaselineShadowOpacity       = 0.35;
        public const double BaselineCornerRadiusLarge   = 30;
        public const bool   BaselineReducedMotion       = false;
        public const double BaselineAnimationIntensity  = 1.0;

        // Master-mapping floors (the studio WaterFlow bottom edge).
        public const double FloorWaterOpacity           = 0.35;
        public const double FloorWaterSpeed             = 0.60;
        public const double FloorCausticIntensity       = 0.25;
        public const double FloorBubbleIntensity        = 0.25;
        public const double FloorReflectionOpacity      = 0.15;
        public const double FloorWaveAmplitude          = 1.5;

        #endregion

        #region ── Singleton ──

        private static readonly Lazy<CrystalFluidSettings> _instance =
            new(() => new CrystalFluidSettings());

        public static CrystalFluidSettings Current => _instance.Value;

        private CrystalFluidSettings() { }

        #endregion

        #region ── Glass (parked axes — immutable until a live key) ──

        /// <summary>Authored 1.0. Parked: no live key consumes a global
        /// glass opacity yet (per-surface brushes own their alphas).</summary>
        public double GlassOpacity => BaselineGlassOpacity;

        /// <summary>Authored 0.72 — baked into CrystalBorderColor's alpha
        /// (#B8FFFFFF). Parked for the same reason.</summary>
        public double GlassBorderOpacity => BaselineGlassBorderOpacity;

        /// <summary>§13 specular reflection opacity — LIVE: reads the
        /// CrystalReflectionOpacity override the studio/engine paints.</summary>
        public double ReflectionOpacity =>
            ReadDouble("CrystalReflectionOpacity", BaselineReflectionOpacity);

        #endregion

        #region ── Water (resource-backed live axes) ──

        public double WaterOpacity =>
            ReadDouble("CrystalWaterOpacity", BaselineWaterOpacity);

        public double WaterSpeed =>
            ReadDouble("CrystalWaterSpeed", BaselineWaterSpeed);

        public double WaveAmplitude =>
            ReadDouble("CrystalWaveAmplitude", BaselineWaveAmplitude);

        public double CausticIntensity =>
            ReadDouble("CrystalCausticOpacity", BaselineCausticIntensity);

        public double BubbleIntensity =>
            ReadDouble("CrystalBubbleOpacity", BaselineBubbleIntensity);

        #endregion

        #region ── Shadow / radius (parked) ──

        /// <summary>Authored 0.35 (card) — template-embedded today;
        /// a live shadow axis is a future studio spinner.</summary>
        public double ShadowOpacity => BaselineShadowOpacity;

        /// <summary>Authored 30 = the CrystalRadiusLarge token.</summary>
        public double CornerRadiusLarge => BaselineCornerRadiusLarge;

        #endregion

        #region ── Motion (two-way, config-backed) ──

        /// <summary>§43 reduced motion. Two-way: the persisted truth is
        /// App.Config.Appearance.ReducedMotion (the settings toggle);
        /// the engine also seeds the CrystalReducedMotion key from it on
        /// every family paint. Setting here updates the live key plus
        /// the persisted flag (single truth stays the config).</summary>
        public bool ReducedMotion
        {
            get => App.Config?.Appearance?.ReducedMotion == true;
            set
            {
                if (value == ReducedMotion)
                    return;

                if (App.Config?.Appearance != null)
                    App.Config.Appearance.ReducedMotion = value;

                var res = Application.Current?.Resources;
                if (res != null)
                    res["CrystalReducedMotion"] = value;

                OnPropertyChanged();
            }
        }

        /// <summary>0–2 global motion scale — the studio AnimationIntensity
        /// slider (0–100) mapped onto the authored range.</summary>
        public double AnimationIntensity =>
            Clamp((ThemeStudioService.Active?.AnimationIntensity
                   ?? ThemeStudioSettings.DefaultsFor(
                       VisualThemeEngine.Current ?? VisualThemeEngine.Normal)
                       .AnimationIntensity) / 50.0, 0, 2);

        #endregion

        #region ── Liveness hub ──

        /// <summary>Called by the VisualThemeEngine and ThemeStudioService
        /// after they repaint/write the crystal keys. Raises one blanket
        /// change notification so bindings through this façade refresh —
        /// the underlying values may change without per-property
        /// granularity (paint is already a coarse event).</summary>
        public static void NotifyRepainted()
        {
            if (_instance.IsValueCreated)
                _instance.Value.OnPropertyChanged(string.Empty);
        }

        private static double ReadDouble(string key, double fallback)
        {
            var res = Application.Current?.Resources;
            if (res?.Contains(key) == true &&
                res[key] is double d)
                return d;
            return fallback;
        }

        private static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));

        #endregion

        #region ── INotifyPropertyChanged ──

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(name));

        #endregion
    }
}
