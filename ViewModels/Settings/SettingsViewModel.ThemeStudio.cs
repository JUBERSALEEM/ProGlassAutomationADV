// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.ThemeStudio.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Theme Studio (2026-09-02) — live 0–100 appearance customization.
//
// LIVE-APPLY PATTERN (same as every other appearance setting):
//   slider setter → ThemeStudioService.Apply (instant, in-memory only)
//                → debounced ConfigService.Save (600 ms after the last
//                  change — no disk writes while dragging)
//                → AuditService.Log once on settle
//
// Presets, per-theme reset and reset-all follow the master prompt:
//   · presets are centralized profiles in ThemeStudioSettings.Preset()
//   · Reset Current Theme restores the ACTIVE family's own defaults
//   · Reset All clears the studio layer entirely (family defaults win)
//     behind a Yes/No confirmation
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Theme;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        // ══════════════════════════════════════════════════════════════
        //  THEME STUDIO
        // ══════════════════════════════════════════════════════════════

        private ThemeStudioSettings? _studioDraft;
        private DispatcherTimer? _studioSaveTimer;
        private bool _studioApplying;

        public ICommand ResetStudioThemeCommand { get; private set; }
        public ICommand ResetStudioAllCommand { get; private set; }

        public List<string> StudioPresetOptions { get; } = new()
        {
            "Crystal",
            "LightGlass",
            "Balanced",
            "Frosted",
            "DenseGlass",
        };

        /// <summary>Current visual family name (displayed on the tile).</summary>
        public string StudioFamilyName =>
            _config?.Appearance?.VisualTheme
            ?? ProGlassAutomation.Services.Theme.VisualThemeEngine.LiquidGlass;

        /// <summary>
        /// True when the draft differs from the persisted profile (used
        /// to show the reset button state).
        /// </summary>
        public bool IsStudioDirty =>
            !(_studioDraft?.Matches(_config?.Appearance?.Studio) ?? true);

        /// <summary>True when the studio layer is active for this family.</summary>
        public bool IsStudioCustomized =>
            _studioDraft?.IsCustomized ?? false;

        /// <summary>
        /// Preset picker — applying a preset replaces the draft values
        /// (family defaults are still one click away via Reset Current).
        /// </summary>
        public string SelectedStudioPreset
        {
            get => _studioActivePreset;
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                var preset = ThemeStudioSettings.Preset(value);
                _studioDraft = preset;
                _studioActivePreset = value;
                StudioApplyAndSettle();
                AuditService.Log(
                    AuditSections.Settings,
                    "Theme Studio preset applied",
                    value + " · " + StudioFamilyName);
                OnPropertyChanged(nameof(SelectedStudioPreset));
            }
        }

        private string _studioActivePreset = "";

        // ── 16 sliders ────────────────────────────────────────────────

        public double StudioFrostness
        {
            get => _studioDraft?.Frostness ?? 50;
            set => StudioSet(v => v.Frostness = value);
        }
        public double StudioTransparency
        {
            get => _studioDraft?.Transparency ?? 35;
            set => StudioSet(v => v.Transparency = value);
        }
        public double StudioBlurIntensity
        {
            get => _studioDraft?.BlurIntensity ?? 50;
            set => StudioSet(v => v.BlurIntensity = value);
        }
        public double StudioGlassTint
        {
            get => _studioDraft?.GlassTint ?? 40;
            set => StudioSet(v => v.GlassTint = value);
        }
        public double StudioCornerRoundness
        {
            get => _studioDraft?.CornerRoundness ?? 50;
            set => StudioSet(v => v.CornerRoundness = value);
        }
        public double StudioCardPadding
        {
            get => _studioDraft?.CardPadding ?? 50;
            set => StudioSet(v => v.CardPadding = value);
        }
        public double StudioShadowIntensity
        {
            get => _studioDraft?.ShadowIntensity ?? 50;
            set => StudioSet(v => v.ShadowIntensity = value);
        }
        public double StudioShadowSoftness
        {
            get => _studioDraft?.ShadowSoftness ?? 50;
            set => StudioSet(v => v.ShadowSoftness = value);
        }
        public double StudioShadowDepth
        {
            get => _studioDraft?.ShadowDepth ?? 50;
            set => StudioSet(v => v.ShadowDepth = value);
        }
        public double StudioBorderVisibility
        {
            get => _studioDraft?.BorderVisibility ?? 50;
            set => StudioSet(v => v.BorderVisibility = value);
        }
        public double StudioBorderOpacity
        {
            get => _studioDraft?.BorderOpacity ?? 60;
            set => StudioSet(v => v.BorderOpacity = value);
        }
        public double StudioBorderThickness
        {
            get => _studioDraft?.BorderThickness ?? 35;
            set => StudioSet(v => v.BorderThickness = value);
        }
        public double StudioBackgroundVisibility
        {
            get => _studioDraft?.BackgroundVisibility ?? 70;
            set => StudioSet(v => v.BackgroundVisibility = value);
        }
        public double StudioBackgroundGlow
        {
            get => _studioDraft?.BackgroundGlow ?? 30;
            set => StudioSet(v => v.BackgroundGlow = value);
        }
        public double StudioHighlightStrength
        {
            get => _studioDraft?.HighlightStrength ?? 40;
            set => StudioSet(v => v.HighlightStrength = value);
        }
        public double StudioAnimationIntensity
        {
            get => _studioDraft?.AnimationIntensity ?? 50;
            set => StudioSet(v => v.AnimationIntensity = value);
        }

        // ── Commands ──────────────────────────────────────────────────

        private void InitializeThemeStudio()
        {
            _studioDraft =
                _config?.Appearance?.Studio?.Clone()
                ?? ThemeStudioSettings.DefaultsFor(StudioFamilyName);

            _studioSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _studioSaveTimer.Tick += OnStudioSaveTick;

            ResetStudioThemeCommand = new RelayCommand(_ => ResetStudioTheme());
            ResetStudioAllCommand = new RelayCommand(_ => ResetStudioAll());

            OnPropertyChanged(nameof(StudioFamilyName));
        }

        private void DisposeThemeStudio()
        {
            if (_studioSaveTimer != null)
            {
                _studioSaveTimer.Stop();
                _studioSaveTimer.Tick -= OnStudioSaveTick;
                _studioSaveTimer = null;
            }
        }

        /// <summary>Applies the draft live and schedules a debounced save.</summary>
        private void StudioApplyAndSettle()
        {
            if (_studioApplying || _studioDraft == null) return;
            _studioApplying = true;
            try
            {
                ThemeStudioService.Apply(_studioDraft);
                _studioSaveTimer?.Stop();
                _studioSaveTimer?.Start();
                OnPropertyChanged(nameof(IsStudioDirty));
                OnPropertyChanged(nameof(IsStudioCustomized));
            }
            finally
            {
                _studioApplying = false;
            }
        }

        /// <summary>Slider setter helper (clamp + live apply + debounce).</summary>
        private void StudioSet(Action<ThemeStudioSettings> mutate)
        {
            if (_studioDraft == null) return;
            mutate(_studioDraft);
            _studioActivePreset = ""; // sliders leave the preset identity
            OnPropertyChanged(nameof(SelectedStudioPreset));
            StudioApplyAndSettle();
        }

        private void OnStudioSaveTick(object? sender, EventArgs e)
        {
            _studioSaveTimer?.Stop();
            if (_studioDraft == null || _config?.Appearance == null) return;

            _studioDraft.IsCustomized = true;
            _config.Appearance.Studio = _studioDraft.Clone();
            SaveConfig();

            AuditService.Log(
                AuditSections.Settings,
                "Theme Studio adjusted",
                StudioFamilyName + " · custom profile saved");

            OnPropertyChanged(nameof(IsStudioDirty));
            OnPropertyChanged(nameof(IsStudioCustomized));
        }

        /// <summary>Reset Current Theme — family defaults, no confirmation
        /// (live + undoable via presets).</summary>
        private void ResetStudioTheme()
        {
            if (_studioDraft == null) return;
            var defaults = ThemeStudioSettings.DefaultsFor(StudioFamilyName);
            _studioDraft = defaults;
            _studioActivePreset = "";

            ThemeStudioService.Apply(defaults);

            if (_config?.Appearance != null)
            {
                _config.Appearance.Studio = null; // family defaults rule
                SaveConfig();
            }

            AuditService.Log(
                AuditSections.Settings,
                "Theme Studio reset",
                StudioFamilyName + " · defaults restored");

            OnPropertyChanged(nameof(IsStudioDirty));
            OnPropertyChanged(nameof(IsStudioCustomized));
            OnPropertyChanged(nameof(SelectedStudioPreset));
            StudioNotifyAllSliders();
        }

        /// <summary>Reset All — clears the studio layer behind a Yes/No.</summary>
        private void ResetStudioAll()
        {
            var ok = _dialogs.Show(
                LocalizationService.Instance.T("Msg.ThemeStudioResetAll"),
                LocalizationService.Instance.T("Settings.ThemeStudio"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
            if (!ok) return;

            _studioDraft = ThemeStudioSettings.DefaultsFor(StudioFamilyName);
            _studioActivePreset = "";
            if (_config?.Appearance != null)
            {
                _config.Appearance.Studio = null;
                SaveConfig();
            }
            ThemeStudioService.ResetAll();

            AuditService.Log(
                AuditSections.Settings,
                "Theme Studio reset all",
                "Studio layer cleared · family defaults everywhere");

            OnPropertyChanged(nameof(IsStudioDirty));
            OnPropertyChanged(nameof(IsStudioCustomized));
            OnPropertyChanged(nameof(SelectedStudioPreset));
            StudioNotifyAllSliders();
        }

        private void StudioNotifyAllSliders()
        {
            OnPropertyChanged(nameof(StudioFrostness));
            OnPropertyChanged(nameof(StudioTransparency));
            OnPropertyChanged(nameof(StudioBlurIntensity));
            OnPropertyChanged(nameof(StudioGlassTint));
            OnPropertyChanged(nameof(StudioCornerRoundness));
            OnPropertyChanged(nameof(StudioCardPadding));
            OnPropertyChanged(nameof(StudioShadowIntensity));
            OnPropertyChanged(nameof(StudioShadowSoftness));
            OnPropertyChanged(nameof(StudioShadowDepth));
            OnPropertyChanged(nameof(StudioBorderVisibility));
            OnPropertyChanged(nameof(StudioBorderOpacity));
            OnPropertyChanged(nameof(StudioBorderThickness));
            OnPropertyChanged(nameof(StudioBackgroundVisibility));
            OnPropertyChanged(nameof(StudioBackgroundGlow));
            OnPropertyChanged(nameof(StudioHighlightStrength));
            OnPropertyChanged(nameof(StudioAnimationIntensity));
            OnPropertyChanged(nameof(StudioFamilyName));
        }
    }
}
