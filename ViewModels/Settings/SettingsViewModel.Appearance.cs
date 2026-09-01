// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Appearance.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Appearance
// members; the class declaration, fields and constructor live in
// SettingsViewModel.cs.
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
        // ══════════════════════════════════════════════════════════════
        //  APPEARANCE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Appearance toggle for the strong card/pill definition edge.
        /// Persisted so the choice survives a restart, and re-applied by
        /// ThemeManager after every Light/Dark swap.
        /// </summary>
        public bool UseDefinedOutline
        {
            get => _config?.Appearance?.UseDefinedOutline ?? false;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.UseDefinedOutline == value) return;

                _config.Appearance.UseDefinedOutline = value;
                ThemeManager.ApplyOutline(value);
                Services.AI.AIConfig.ConfigService.Save(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card outline changed",
                    value ? "Defined edge ON" : "Defined edge OFF");

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Global card border thickness (DIPs). One setting restyles every
        /// card / panel in the app through the CardBorderThickness theme
        /// resource. Persisted so the choice survives a restart.
        /// </summary>
        public double CardBorderThickness
        {
            get => _config?.Appearance?.CardBorderThickness ?? 1.5;
            set
            {
                if (_config?.Appearance == null) return;
                double v = value < 0 ? 0 : value;
                if (Math.Abs(_config.Appearance.CardBorderThickness - v) < 0.01) return;

                _config.Appearance.CardBorderThickness = v;
                ThemeManager.ApplyCardBorderThickness(v);
                Services.AI.AIConfig.ConfigService.Save(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card border thickness changed",
                    $"{v:0.#} DIP");

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Global card corner radius (DIPs). Default 12. One slider
        /// restyles every card through CardCornerRadius (and the derived
        /// Control / Large tokens). Persisted across restarts.
        /// </summary>
        public List<string> DisplayDensityOptions { get; } = new()
        {
            "Compact", "Normal", "Comfortable"
        };

        public List<string> PdfLayoutOptions { get; } = new()
        {
            "Classic Navy", "Modern Teal", "Editorial Ink",
            "Industrial Steel", "Minimal Ivory"
        };

        public string PdfLayout
        {
            get
            {
                var id = _config?.Appearance?.PdfLayoutId ?? "ClassicNavy";
                return ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                    .Resolve(id).DisplayName;
            }
            set
            {
                if (_config?.Appearance == null) return;
                var theme = ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                    .Resolve(value);
                if (_config.Appearance.PdfLayoutId == theme.Id) return;

                _config.Appearance.PdfLayoutId = theme.Id;
                ProGlassAutomation.Services.Export.Pdf.DocumentStyle.Apply(theme);
                Services.AI.AIConfig.ConfigService.Save(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "PDF layout changed", theme.DisplayName);

                OnPropertyChanged();
                OnPropertyChanged(nameof(PdfLayoutHint));
            }
        }

        public string PdfLayoutHint =>
            ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                .Resolve(_config?.Appearance?.PdfLayoutId).Description;

        /// <summary>
        /// Compact / Normal / Comfortable. One setting shrinks shared
        /// chrome so laptop screens can see the data grid.
        /// </summary>
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
                Services.AI.AIConfig.ConfigService.Save(_config);

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
                Services.AI.AIConfig.ConfigService.Save(_config);
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
                Services.AI.AIConfig.ConfigService.Save(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card corner radius changed",
                    $"{v:0} px");

                OnPropertyChanged();
            }
        }

        public bool IsDarkTheme
        {
            get => ThemeManager.CurrentTheme == AppTheme.Dark;
            set
            {
                ThemeManager.ApplyTheme(value ? AppTheme.Dark : AppTheme.Light);

                // Persist the base colour mode so it restores on the
                // next launch.
                if (_config?.Appearance != null)
                {
                    _config.Appearance.Theme = value ? "Dark" : "Light";
                    SaveConfig();
                }

                // Rebuild the visual family (Monet glass, Professional
                // surfaces, wallpaper layers) against the fresh theme
                // colours — otherwise derived surfaces stay stale.
                ApplyVisualTheme();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Theme changed",
                    value ? "Dark theme" : "Light theme");

                OnPropertyChanged();
            }
        }

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
