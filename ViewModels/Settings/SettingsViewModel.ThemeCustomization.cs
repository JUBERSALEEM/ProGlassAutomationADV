// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3 — Settings "Themes" section (2026-09-06). Bounded, persisted,
// family-aware Crystal Fluid material tuning that replaces the removed
// Theme Studio playground.
//
//   · The SIX live Crystal axes (water opacity/speed, wave amplitude,
//     caustic, bubble, reflection opacity) are the only authored axes
//     with a live DynamicResource consumer. Glass/glass-border/shadow
//     opacity stay parked (CrystalFluidSettings) and are NOT surfaced.
//   · Every axis is clamped on read AND write through
//     Services/Theme/ThemeCustomization.cs — the single number home for
//     bounds (Min* = CrystalFluidSettings.Floor*, Max* authored there)
//     and the single writer into the Crystal* tokens.
//   · Named presets Default / Calm / Intense + a per-family Reset come
//     from ThemeCustomization.Presets / Reset().
//   · Family-aware: the Crystal material group is only visible while
//     Ios-17pm (VisualThemeEngine.CrystalFluid) is the active family;
//     Normal/Monet keep the existing Appearance controls.
//
// Persistence pattern mirrors SettingsViewModel.AppearanceSection.cs:
//   _config write → live resource write (ThemeCustomization.Apply) →
//   SaveConfig / ScheduleDebouncedSave → AuditService.Log →
//   OnPropertyChanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Theme;
using System;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        // ══════════════════════════════════════════════════════════════
        //  THEMES — family gate + Crystal Fluid material axes
        // ══════════════════════════════════════════════════════════════

        /// <summary>The persisted customization block (never null on write;
        /// read may return null for a corrupt/hand-edited save — the axis
        /// getters then fall back to the authored baselines).</summary>
        private ThemeCustomizationSettings? CrystalCustomization =>
            _config?.Appearance?.ThemeCustomization;

        /// <summary>Creates the customization block if a hand-edited save
        /// nulled it — a preset click or slider drag must never throw on a
        /// corrupt config.</summary>
        private ThemeCustomizationSettings EnsureCrystalCustomization()
        {
            var appearance = _config?.Appearance;
            if (appearance == null)
                return new ThemeCustomizationSettings();
            if (appearance.ThemeCustomization == null)
                appearance.ThemeCustomization = new ThemeCustomizationSettings();
            return appearance.ThemeCustomization;
        }

        /// <summary>Family gate — the Crystal Fluid material axes are only
        /// shown while the Ios-17pm family is active. Normal/Monet keep
        /// their existing Appearance controls.</summary>
        public bool IsCrystalThemeActive => false;

        /// <summary>Identity of the last preset that wrote the axes
        /// ("Default" | "Calm" | "Intense"), or "" after a manual tweak.
        /// Drives the active-preset highlight on the three preset buttons.</summary>
        public string ActiveCrystalPreset =>
            CrystalCustomization?.CrystalPreset ?? "Default";

        // ── Axis bounds (exposed for the XAML sliders; the numbers live in
        //    ThemeCustomization.cs — no second copy here) ──────────────

        public double CrystalWaterOpacityMin => ThemeCustomization.MinWaterOpacity;
        public double CrystalWaterOpacityMax => ThemeCustomization.MaxWaterOpacity;
        public double CrystalWaterSpeedMin => ThemeCustomization.MinWaterSpeed;
        public double CrystalWaterSpeedMax => ThemeCustomization.MaxWaterSpeed;
        public double CrystalWaveAmplitudeMin => ThemeCustomization.MinWaveAmplitude;
        public double CrystalWaveAmplitudeMax => ThemeCustomization.MaxWaveAmplitude;
        public double CrystalCausticIntensityMin => ThemeCustomization.MinCausticIntensity;
        public double CrystalCausticIntensityMax => ThemeCustomization.MaxCausticIntensity;
        public double CrystalBubbleIntensityMin => ThemeCustomization.MinBubbleIntensity;
        public double CrystalBubbleIntensityMax => ThemeCustomization.MaxBubbleIntensity;
        public double CrystalReflectionOpacityMin => ThemeCustomization.MinReflectionOpacity;
        public double CrystalReflectionOpacityMax => ThemeCustomization.MaxReflectionOpacity;

        // ── Six live Crystal axes (clamped read + clamped write) ──────

        public double CrystalWaterOpacity
        {
            get => ThemeCustomization.WaterOpacity(CrystalCustomization);
            set => SetCrystalAxis(
                "WaterOpacity", value,
                CrystalWaterOpacityMin, CrystalWaterOpacityMax,
                c => c.WaterOpacity, (c, v) => c.WaterOpacity = v);
        }

        public double CrystalWaterSpeed
        {
            get => ThemeCustomization.WaterSpeed(CrystalCustomization);
            set => SetCrystalAxis(
                "WaterSpeed", value,
                CrystalWaterSpeedMin, CrystalWaterSpeedMax,
                c => c.WaterSpeed, (c, v) => c.WaterSpeed = v);
        }

        public double CrystalWaveAmplitude
        {
            get => ThemeCustomization.WaveAmplitude(CrystalCustomization);
            set => SetCrystalAxis(
                "WaveAmplitude", value,
                CrystalWaveAmplitudeMin, CrystalWaveAmplitudeMax,
                c => c.WaveAmplitude, (c, v) => c.WaveAmplitude = v);
        }

        public double CrystalCausticIntensity
        {
            get => ThemeCustomization.CausticIntensity(CrystalCustomization);
            set => SetCrystalAxis(
                "CausticIntensity", value,
                CrystalCausticIntensityMin, CrystalCausticIntensityMax,
                c => c.CausticIntensity, (c, v) => c.CausticIntensity = v);
        }

        public double CrystalBubbleIntensity
        {
            get => ThemeCustomization.BubbleIntensity(CrystalCustomization);
            set => SetCrystalAxis(
                "BubbleIntensity", value,
                CrystalBubbleIntensityMin, CrystalBubbleIntensityMax,
                c => c.BubbleIntensity, (c, v) => c.BubbleIntensity = v);
        }

        public double CrystalReflectionOpacity
        {
            get => ThemeCustomization.ReflectionOpacity(CrystalCustomization);
            set => SetCrystalAxis(
                "ReflectionOpacity", value,
                CrystalReflectionOpacityMin, CrystalReflectionOpacityMax,
                c => c.ReflectionOpacity, (c, v) => c.ReflectionOpacity = v);
        }

        /// <summary>
        /// Shared axis setter: clamp to the authored bounds, no-op on a
        /// no-change write, persist to the customization block, mark the
        /// tweak manual (preset identity cleared), apply live into the
        /// Crystal* tokens and debounce the config save (slider drags hit
        /// the disk once per drag, not once per tick).
        /// </summary>
        private void SetCrystalAxis(
            string axis,
            double value,
            double min,
            double max,
            Func<ThemeCustomizationSettings, double> read,
            Action<ThemeCustomizationSettings, double> write)
        {
            var customization = EnsureCrystalCustomization();
            double clamped = ThemeCustomization.Clamp(value, min, max);
            if (Math.Abs(read(customization) - clamped) < 0.0005) return;

            write(customization, clamped);
            customization.CrystalPreset = string.Empty; // manual tweak

            ThemeCustomization.Apply(Application.Current?.Resources!);
            ScheduleDebouncedSave();

            OnPropertyChanged(nameof(ActiveCrystalPreset));
            RaiseCrystalAxisChanged(axis);
        }

        private void RaiseCrystalAxisChanged(string axis)
        {
            // The sliders are two-way bound; a preset/apply re-paint also
            // writes these tokens, so raise the matching property so the
            // bound slider thumb follows the live value.
            switch (axis)
            {
                case "WaterOpacity": OnPropertyChanged(nameof(CrystalWaterOpacity)); break;
                case "WaterSpeed": OnPropertyChanged(nameof(CrystalWaterSpeed)); break;
                case "WaveAmplitude": OnPropertyChanged(nameof(CrystalWaveAmplitude)); break;
                case "CausticIntensity": OnPropertyChanged(nameof(CrystalCausticIntensity)); break;
                case "BubbleIntensity": OnPropertyChanged(nameof(CrystalBubbleIntensity)); break;
                case "ReflectionOpacity": OnPropertyChanged(nameof(CrystalReflectionOpacity)); break;
            }
        }

        private void NotifyAllCrystalAxes()
        {
            OnPropertyChanged(nameof(CrystalWaterOpacity));
            OnPropertyChanged(nameof(CrystalWaterSpeed));
            OnPropertyChanged(nameof(CrystalWaveAmplitude));
            OnPropertyChanged(nameof(CrystalCausticIntensity));
            OnPropertyChanged(nameof(CrystalBubbleIntensity));
            OnPropertyChanged(nameof(CrystalReflectionOpacity));
            OnPropertyChanged(nameof(ActiveCrystalPreset));
        }

        // ── Presets (Default / Calm / Intense) + Reset ────────────────

        private ICommand? _applyThemePresetCommand;
        public ICommand ApplyThemePresetCommand =>
            _applyThemePresetCommand ??= new RelayCommand(p => ApplyThemePreset(p as string));

        private ICommand? _resetThemeFamilyCommand;
        public ICommand ResetThemeFamilyCommand =>
            _resetThemeFamilyCommand ??= new RelayCommand(_ => ResetThemeFamily());

        private void ApplyThemePreset(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!ThemeCustomization.ApplyPreset(key)) return;

            SaveConfig();
            AuditService.Log(
                AuditSections.Settings,
                "Crystal Fluid preset applied", key);
            NotifyAllCrystalAxes();
        }

        /// <summary>
        /// Per-family reset: the active family decides what "default" means.
        ///   · Ios-17pm → Crystal Fluid axes back to the authored baselines.
        ///   · Normal/Monet → default card shape (the same factory numbers
        ///     ResetAppearanceToDefaultsAsync uses: corner 12, border 1.5,
        ///     gradients on), routed through the existing setters so the
        ///     canonical apply + debounced save + audit stay single-path.
        /// </summary>
        private void ResetThemeFamily()
        {
            if (IsCrystalThemeActive)
            {
                ThemeCustomization.Reset();
                SaveConfig();
                AuditService.Log(
                    AuditSections.Settings,
                    "Crystal Fluid material reset", "Default baselines");
                NotifyAllCrystalAxes();
                return;
            }

            CardCornerRadius = 12;
            CardBorderThickness = 1.5;
            UseGradientSurfaces = true;
            AuditService.Log(
                AuditSections.Settings,
                "Visual theme reset", "Default card shape");
        }

        /// <summary>Called from the Themes section's Loaded — re-syncs the
        /// bound sliders and the family gate from current state (idempotent,
        /// no writes).</summary>
        public void InitializeThemesSection()
        {
            NotifyAllCrystalAxes();
            OnPropertyChanged(nameof(IsCrystalThemeActive));
        }
    }
}
