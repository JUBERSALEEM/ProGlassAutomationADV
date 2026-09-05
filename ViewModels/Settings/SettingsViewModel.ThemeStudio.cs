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

        /// <summary>Combo rows come from the ONE authoritative registry
        /// (ThemeStudioSettings.PresetNames) — 2026-09-05 Phase-1 audit:
        /// the previous hand-maintained list advertised "DenseGlass"
        /// while Preset() had no case for it (phantom preset, F-03).
        /// Drift is now structurally impossible + locked by tests.</summary>
        public List<string> StudioPresetOptions { get; } =
            new(ThemeStudioSettings.PresetNames);

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
                PushStudioUndo();
                _studioDraft = preset;
                _studioActivePreset = value;
                ClearQuickLookUnlessApplying();
                StudioApplyAndSettle();
                AuditService.Log(
                    AuditSections.Settings,
                    "Theme Studio preset applied",
                    value + " · " + StudioFamilyName);
                OnPropertyChanged(nameof(SelectedStudioPreset));
            }
        }

        private string _studioActivePreset = "";

        // ── Phase 2: quick looks + per-slider reset + glass relevance ──

        /// <summary>Quick-look keys (persisted in Appearance.QuickLook).</summary>
        public const string QuickLookFactoryFloor = "FactoryFloor";
        public const string QuickLookOffice = "Office";
        public const string QuickLookShowroom = "Showroom";

        public ICommand ApplyQuickLookCommand { get; private set; }
        public ICommand ResetStudioSliderCommand { get; private set; }

        // ── Phase 4: one-step studio undo ───────────────────────────

        public ICommand UndoStudioCommand { get; private set; }

        /// <summary>True while the single-slot undo holds a revertible
        /// state. Studio changes mutate immediately (live preview), so
        /// the slot is refreshed on every push and flips to false again
        /// after Undo consumes it.</summary>
        public bool CanStudioUndo => _studioUndo != null;

        /// <summary>Single-slot snapshot of the last revertible studio
        /// state: the draft profile, the preset identity shown in the
        /// picker and the PERSISTED copy (undo must restore what was
        /// saved, not only what is previewing). Null = nothing to undo.
        /// One step, deliberately — a depth stack invites half-applied
        /// audit trails; the operator's mental model is "take back the
        /// last thing I touched".</summary>
        private (ThemeStudioSettings? Draft, string Preset,
            ThemeStudioSettings? ConfigStudio)? _studioUndo;

        private bool _applyingQuickLook;

        /// <summary>False while an industrial (flat) family is active:
        /// the glass-theatre sliders (blur, tint, glow, highlight,
        /// water) scale values the solid-slab language never renders,
        /// so the control dims and locks them instead of inviting a
        /// regression the operator cannot see.</summary>
        public bool IsGlassStudioRelevant =>
            !ProGlassAutomation.Services.Theme.VisualThemeEngine
                .IsIndustrial(StudioFamilyName);

        /// <summary>Localized label of the persisted one-click look
        /// (or the Custom marker once the operator deviates).</summary>
        public string ActiveQuickLookText =>
            LocalizationService.Instance.T(
                QuickLookLabelKey(_config?.Appearance?.QuickLook ?? ""));

        private static string QuickLookLabelKey(string? look) => look switch
        {
            QuickLookFactoryFloor => "Settings.LookFactoryFloor",
            QuickLookOffice => "Settings.LookOffice",
            QuickLookShowroom => "Settings.LookShowroom",
            _ => "Settings.LookCustom",
        };

        /// <summary>Captures the pre-mutation state into the single
        /// undo slot. Call sites push BEFORE touching anything (slider
        /// tick, preset pick, resets, quick-look entry). Quick looks
        /// push exactly once at entry — the guard here stops the nested
        /// flows (family + reset/preset) from overwriting the slot with
        /// mid-look snapshots.</summary>
        private void PushStudioUndo()
        {
            if (_applyingQuickLook) return; // entry pushed already
            _studioUndo = (
                _studioDraft?.Clone(),
                _studioActivePreset,
                _config?.Appearance?.Studio?.Clone());
            OnPropertyChanged(nameof(CanStudioUndo));
        }

        /// <summary>Reverts the most recent studio change (slider drag,
        /// preset, per-slider chip, Reset Current / Reset All, quick
        /// look). Restores LIVE preview and the PERSISTED copy together
        /// — undoing only the preview would resurrect the change on the
        /// next app start. Consumes the slot (one step only). Note: a
        /// quick look's FAMILY switch is intentionally not reverted —
        /// family undo belongs to the theme picker, not the studio.</summary>
        private void UndoStudio()
        {
            var undo = _studioUndo;
            if (undo == null) return;
            _studioUndo = null;
            _studioSaveTimer?.Stop(); // a pending debounced save must not
                                      // re-persist post-undo state as
                                      // "Theme Studio adjusted"

            _studioDraft = undo.Value.Draft?.Clone();
            _studioActivePreset = undo.Value.Preset;

            if (_config?.Appearance != null)
            {
                _config.Appearance.Studio = undo.Value.ConfigStudio?.Clone();
                _config.Appearance.QuickLook = ""; // undone = custom state
                SaveConfig();
            }

            // Bypass the public setters everywhere above (no re-push),
            // then repaint through the proven service paths.
            if (_studioDraft != null)
                ThemeStudioService.Apply(_studioDraft);
            else
                ThemeStudioService.ResetToFamilyDefaults();

            AuditService.Log(
                AuditSections.Settings,
                "Theme Studio undo",
                StudioFamilyName + " · last studio change reverted");

            OnPropertyChanged(nameof(IsStudioDirty));
            OnPropertyChanged(nameof(IsStudioCustomized));
            OnPropertyChanged(nameof(SelectedStudioPreset));
            OnPropertyChanged(nameof(ActiveQuickLookText));
            OnPropertyChanged(nameof(CanStudioUndo));
            StudioNotifyAllSliders();
        }

        /// <summary>Any manual deviation (family/preset/slider/reset)
        /// downgrades the persisted look to Custom — unless the change
        /// came from inside ApplyQuickLook itself.</summary>
        private void ClearQuickLookUnlessApplying()
        {
            if (_applyingQuickLook) return;
            if (_config?.Appearance == null) return;
            if (_config.Appearance.QuickLook.Length == 0) return;

            _config.Appearance.QuickLook = "";
            OnPropertyChanged(nameof(ActiveQuickLookText));
        }

        /// <summary>One-click coherent looks (Phase 2 — "factory user
        /// configures in ≤2 clicks"). Each look composes family + studio
        /// profile + (where promised) colour mode through the PROVEN
        /// existing flows — quick looks reuse SelectedVisualTheme /
        /// ResetStudioTheme / SelectedStudioPreset instead of inventing
        /// a third, subtly different, apply path.</summary>
        private void ApplyQuickLook(object? parameter)
        {
            if (_config?.Appearance == null || parameter is not string look)
                return;

            string family;
            string preset;
            switch (look)
            {
                case QuickLookFactoryFloor:
                    family = ProGlassAutomation.Services.Theme
                        .VisualThemeEngine.IndustrialGrey;
                    preset = "";
                    break;
                case QuickLookOffice:
                    family = ProGlassAutomation.Services.Theme
                        .VisualThemeEngine.Professional;
                    preset = "";
                    break;
                case QuickLookShowroom:
                    family = ProGlassAutomation.Services.Theme
                        .VisualThemeEngine.LiquidGlass;
                    preset = "Balanced";
                    break;
                default:
                    return;
            }

            // One push for the whole composite look: the guard inside
            // PushStudioUndo keeps the nested reset/preset from
            // overwriting the slot with mid-look snapshots.
            PushStudioUndo();
            _applyingQuickLook = true;
            try
            {
                // Office promises the bright corporate pairing; the
                // industrial look self-couples via ModePairFor, and the
                // showroom keeps the user's own mode (glass reads both).
                if (look == QuickLookOffice &&
                    ThemeManager.CurrentTheme != AppTheme.Light)
                {
                    Services.Settings.AppearanceService.ApplyTheme(
                        isDark: false);
                }

                SelectedVisualTheme = family;

                if (preset.Length == 0)
                    ResetStudioTheme();       // family defaults rule
                else
                    SelectedStudioPreset = preset;

                _config.Appearance.QuickLook = look;
                SaveConfig();
            }
            finally
            {
                _applyingQuickLook = false;
            }

            AuditService.Log(
                AuditSections.Settings,
                "Quick look applied",
                LocalizationService.Instance.T(QuickLookLabelKey(look)));

            OnPropertyChanged(nameof(ActiveQuickLookText));
            OnPropertyChanged(nameof(IsGlassStudioRelevant));
            OnPropertyChanged(nameof(IsIndustrialLook));
        }

        /// <summary>Reset ONE slider back to the active family's default
        /// (the per-slider chip — operators fix their own experiments
        /// without hunting Reset-All). Phase 4: the key→property mapping
        /// lives ONCE in ThemeStudioSettings (the axis registry) instead
        /// of a second hand-maintained switch here.</summary>
        private void ResetStudioSlider(object? parameter)
        {
            if (_studioDraft == null || parameter is not string key) return;

            PushStudioUndo();
            if (!_studioDraft.ResetAxisTo(
                    key, ThemeStudioSettings.DefaultsFor(StudioFamilyName)))
                return;

            OnPropertyChanged("Studio" + key);
            ClearQuickLookUnlessApplying();
            StudioApplyAndSettle();
        }

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

        // ── 2026-09-05 rich colour system ─────────────────────────────

        /// <summary>0 = muted pastel, 50 = family hue, 100 = deep rich
        /// saturated colour across cards/glass/tints/wallpaper scene.</summary>
        public double StudioColorRichness
        {
            get => _studioDraft?.ColorRichness ?? 50;
            set => StudioSet(v => v.ColorRichness = value);
        }

        /// <summary>55 = family ink, 100 = stage-aware maximum-contrast
        /// text (auto-forced when cards go near-invisible).</summary>
        public double StudioTextStrength
        {
            get => _studioDraft?.TextStrength ?? 55;
            set => StudioSet(v => v.TextStrength = value);
        }

        /// <summary>0 = flat frost … 100 = strong water-fill: cards turn
        /// into liquid crystal (wet top sheen, waterline glint, colour
        /// settled at the bottom like filled water).</summary>
        public double StudioWaterFlow
        {
            get => _studioDraft?.WaterFlow ?? 0;
            set => StudioSet(v => v.WaterFlow = value);
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

            // Phase 2: quick looks + per-slider reset chips.
            ApplyQuickLookCommand = new RelayCommand(ApplyQuickLook);
            ResetStudioSliderCommand = new RelayCommand(ResetStudioSlider);

            // Phase 4: one-step undo (enabled only while a slot exists).
            UndoStudioCommand = new RelayCommand(
                _ => UndoStudio(), _ => CanStudioUndo);

            OnPropertyChanged(nameof(StudioFamilyName));
            OnPropertyChanged(nameof(IsGlassStudioRelevant));
            OnPropertyChanged(nameof(ActiveQuickLookText));
        }

        private void DisposeThemeStudio()
        {
            if (_studioSaveTimer != null)
            {
                // Flush a pending debounced save so a close right after
                // the last slider move never loses the final values.
                if (_studioSaveTimer.IsEnabled)
                    OnStudioSaveTick(null, EventArgs.Empty);
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
            PushStudioUndo();
            mutate(_studioDraft);
            _studioActivePreset = ""; // sliders leave the preset identity
            OnPropertyChanged(nameof(SelectedStudioPreset));
            ClearQuickLookUnlessApplying();
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
            PushStudioUndo();
            // 2026-09-05: route through the SERVICE reset (forced engine
            // repaint + fresh family snapshot). The old direct Apply()
            // scaled defaults onto whatever stale/possibly studio-written
            // base snapshot was current — the "reset does nothing" cause.
            var defaults = ThemeStudioService.ResetToFamilyDefaults();
            _studioDraft = defaults;
            _studioActivePreset = "";

            ClearQuickLookUnlessApplying();
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

            PushStudioUndo();
            _studioDraft = ThemeStudioSettings.DefaultsFor(StudioFamilyName);
            _studioActivePreset = "";
            ClearQuickLookUnlessApplying();
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
            // 2026-09-05: the three rich-colour sliders were MISSING here —
            // after a reset they kept showing the old positions, which the
            // owner read as "reset is not working" for Text Strength.
            OnPropertyChanged(nameof(StudioColorRichness));
            OnPropertyChanged(nameof(StudioTextStrength));
            OnPropertyChanged(nameof(StudioWaterFlow));
            OnPropertyChanged(nameof(StudioFamilyName));
            OnPropertyChanged(nameof(IsGlassStudioRelevant));
            OnPropertyChanged(nameof(ActiveQuickLookText));
        }
    }
}
