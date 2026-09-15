// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Appearance.AccentsDensity.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): split out of SettingsViewModel.Appearance.cs so no file in the
// module exceeds the ~350-line budget. Same partial class, members moved
// verbatim at member boundaries — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Fonts;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        public string DisplayDensity
        {
            get => _config?.Appearance?.DisplayDensity ?? "Compact";
            set
            {
                if (_config?.Appearance == null) return;
                string v = string.IsNullOrWhiteSpace(value) ? "Compact" : value.Trim();
                if (_config.Appearance.DisplayDensity == v) return;

                _config.Appearance.DisplayDensity = v;
                ThemeManager.ApplyDisplayDensity(v);
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                Services.AI.AIConfig.ConfigService.SaveDebounced(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Display density changed", v);

                OnPropertyChanged();
            }
        }

        /// <summary>Global UI zoom 80–125%. Ctrl+Wheel also drives this.</summary>
        public double UiScale
        {
            get => _config?.Appearance?.UiScale ?? 1.0;
            set
            {
                if (_config?.Appearance == null) return;
                double v = Services.Viewport.ViewportService.ClampUiScale(value);
                if (Math.Abs(_config.Appearance.UiScale - v) < 0.001) return;

                _config.Appearance.UiScale = v;
                ThemeManager.ApplyUiScale(v);
                Services.AI.AIConfig.ConfigService.SaveDebounced(_config);
                OnPropertyChanged();
                OnPropertyChanged(nameof(UiScalePercent));
            }
        }

        public int UiScalePercent => (int)Math.Round(UiScale * 100);

        public ICommand ResetUiScaleCommand { get; private set; } = null!;

        public double CardCornerRadius
        {
            get => _config?.Appearance?.CardCornerRadius ?? 12;
            set
            {
                if (_config?.Appearance == null) return;
                double v = value < 0 ? 0 : value;
                if (Math.Abs(_config.Appearance.CardCornerRadius - v) < 0.05) return;

                _config.Appearance.CardCornerRadius = v;
                ThemeManager.ApplyCardCornerRadius(v);
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                OnPropertyChanged(nameof(SurfacePrimaryRadius));
                OnPropertyChanged(nameof(SurfaceSecondaryRadius));
                OnPropertyChanged(nameof(SurfaceDetailRadius));
                // Debounced: no disk/audit writes per slider tick.
                ScheduleDebouncedSave(auditCorner: true);

                OnPropertyChanged();
            }
        }

        public bool IsDarkTheme
        {
            get => ThemeManager.CurrentTheme == AppTheme.Dark;
            set
            {
                // 2026-09-05 Phase-1: run the CANONICAL full rebuild
                // (Light/Dark dictionary swap → single family+module
                // paint → accent → outline) instead of hand-invoking
                // ThemeManager + a family rebuild — one sequence
                // everywhere, one reason string in the log.
                ProGlassAutomation.Services.Theme.ThemePipeline.Rebuild(
                    value, "mode toggle (settings)");

                // Persist the base colour mode so it restores on the
                // next launch.
                if (_config?.Appearance != null)
                {
                    _config.Appearance.Theme = value ? "Dark" : "Light";
                    SaveConfig();
                }

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Theme changed",
                    value ? "Dark theme" : "Light theme");

                OnPropertyChanged();
                // The Appearance MODE segmented control and the page-header mode
                // chip read the same state through three derived members. Raise
                // them here, in the ONE setter every path funnels through
                // (top-bar toggle, rail, Themes page, this page), so a control
                // can never show a stale mode.
                OnPropertyChanged(nameof(ThemeMode));
                OnPropertyChanged(nameof(ThemeModeChipBackgroundKey));
                OnPropertyChanged(nameof(ThemeModeChipForegroundKey));
            }
        }

        /// <summary>
        /// Base colour mode as the token the config already persists
        /// (Appearance.Theme = "Light" | "Dark"). This is what the Appearance
        /// page's MODE segmented control selects (sheet 02), so Light/Dark has
        /// a real control on the page instead of only the top-bar toggle.
        ///
        /// The setter routes through <see cref="IsDarkTheme"/> and nothing else:
        /// one theme-change path (ThemePipeline.Rebuild → persist → audit),
        /// whichever control asked. Nothing is rebuilt or audited when the
        /// requested mode is already active — a segmented control re-selects its
        /// own item while the binding settles, and that must stay free.
        /// </summary>
        public string ThemeMode
        {
            get => IsDarkTheme ? "Dark" : "Light";
            set
            {
                // The selector clears its value while its items are being read.
                if (string.IsNullOrWhiteSpace(value)) return;
                bool dark = string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase);
                if (dark == IsDarkTheme) return;
                IsDarkTheme = dark;
            }
        }

        /// <summary>
        /// Brush KEY for the page-header mode chip's fill — not a brush. The
        /// chip resolves it through BrushKeyConverter, which looks the key up in
        /// Application.Resources on every evaluation, so a theme switch repaints
        /// the chip without any code, and the grammar keeps owning the palette
        /// (no colour literal ever reaches a view).
        /// </summary>
        public string ThemeModeChipBackgroundKey =>
            IsDarkTheme ? "Ios26AccentSoftBrush" : "Ios26ValuePillBrush";

        /// <summary>Brush key for the mode chip's text — the muted twin.</summary>
        public string ThemeModeChipForegroundKey =>
            IsDarkTheme ? "Ios26AccentSoftTextBrush" : "Ios26TextMutedBrush";


        // ══════════════════════════════════════════════════════════════
        //  ACCENT COLOR (Dynamic Theme Engine)
        //
        //  One-click presets feed the adaptive accent engine
        //  (ThemeManager.ApplyAccent) which derives hover/tint/contrast
        //  brushes automatically. "Glass Blue" resets to the default.
        // ══════════════════════════════════════════════════════════════

        public ICommand ApplyAccentCommand { get; private set; }

        /// <summary>One-tap factory reset for the whole Appearance module.</summary>
        public ICommand ResetAppearanceCommand { get; private set; }

        private bool _isResettingAppearance;
        public bool IsResettingAppearance
        {
            get => _isResettingAppearance;
            private set { _isResettingAppearance = value; OnPropertyChanged(); }
        }

        private string _resetStatusMessage = "";
        public string ResetStatusMessage
        {
            get => _resetStatusMessage;
            private set { _resetStatusMessage = value; OnPropertyChanged(); }
        }

        public ICommand ApplyCustomAccentCommand { get; private set; }

        /// <summary>One option on the Settings accent palette.</summary>
        public sealed class AccentOption
        {
            public AccentOption(string key, string name, Color swatch)
            {
                Key = key;
                DisplayName = name;
                var brush = new SolidColorBrush(swatch);
                brush.Freeze();
                Swatch = brush;
            }

            public string Key { get; }
            public string DisplayName { get; }
            public Brush Swatch { get; }
        }

        /// <summary>
        /// The accent palette shown in Appearance. Generated from
        /// AccentPresets.All, so the swatch the user sees is the exact
        /// colour that ApplyAccent receives — the old hand-copied XAML
        /// hexes could drift from the code values, this cannot.
        /// </summary>
        public ObservableCollection<AccentOption> AccentPalette { get; } = new();

        private void InitializeAccentPalette()
        {
            AccentPalette.Clear();
            foreach (var (key, name) in
                ProGlassAutomation.Services.Theme.AccentPresets.All)
            {
                AccentPalette.Add(new AccentOption(
                    key, name,
                    ProGlassAutomation.Services.Theme.AccentPresets.SwatchFor(key)));
            }

            _customAccentHex = _config?.Appearance?.CustomAccentHex ?? "";
            OnPropertyChanged(nameof(CustomAccentHex));
            OnPropertyChanged(nameof(CustomAccentSwatch));
        }

        // ── Free-pick custom colour ────────────────────────────────────

        private string _customAccentHex = "";

        /// <summary>Hex (or named CSS colour) the user types in.</summary>
        public string CustomAccentHex
        {
            get => _customAccentHex;
            set
            {
                _customAccentHex = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomAccentSwatch));
                OnPropertyChanged(nameof(CustomAccentStatus));
            }
        }

        /// <summary>Live preview dot next to the hex field.</summary>
        public Brush CustomAccentSwatch
        {
            get
            {
                var c = ProGlassAutomation.Services.Theme.AccentPresets
                    .TryParseHex(_customAccentHex);
                if (c == null) return Brushes.Transparent;
                var brush = new SolidColorBrush(c.Value);
                brush.Freeze();
                return brush;
            }
        }

        /// <summary>Validation hint under the hex field.</summary>
        public string CustomAccentStatus
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_customAccentHex))
                    return "Type a colour like #7C3AED and press APPLY.";
                return ProGlassAutomation.Services.Theme.AccentPresets
                    .TryParseHex(_customAccentHex) != null
                    ? "Ready — every surface, tint and Monet glass will adapt to this colour."
                    : "Not a recognised colour — use #RGB or #RRGGBB.";
            }
        }

        private void ApplyCustomAccent()
        {
            var color = ProGlassAutomation.Services.Theme.AccentPresets
                .TryParseHex(_customAccentHex);
            if (color == null)
            {
                OnPropertyChanged(nameof(CustomAccentStatus));
                return;
            }

            ThemeManager.ApplyAccent(color.Value);

            if (_config?.Appearance != null)
            {
                _config.Appearance.AccentPreset =
                    ProGlassAutomation.Services.Theme.AccentPresets.Custom;
                _config.Appearance.CustomAccentHex =
                    $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
                _customAccentHex = _config.Appearance.CustomAccentHex;
                SaveConfig();
            }

            ProGlassAutomation.Services.Audit.AuditService.Log(
                ProGlassAutomation.Services.Audit.AuditSections.Settings,
                "Accent changed",
                "Custom colour " + _config?.Appearance?.CustomAccentHex);

            OnPropertyChanged(nameof(CustomAccentHex));
            OnPropertyChanged(nameof(CustomAccentSwatch));
            OnPropertyChanged(nameof(CustomAccentStatus));
        }

    }
}
