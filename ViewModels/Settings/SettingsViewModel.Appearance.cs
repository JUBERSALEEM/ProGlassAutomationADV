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
                if (!value)
                {
                    // OFF means reset the defined-edge styling, not merely stop
                    // repainting the colour. Keep the card system on defaults.
                    _config.Appearance.CardEdgeColor = string.Empty;
                    _config.Appearance.CardEdgeThickness = 2.0;
                    ThemeManager.ApplyCardEdgeColor(string.Empty);
                    ThemeManager.ApplyCardEdgeThickness(2.0);
                    OnPropertyChanged(nameof(CardEdgeColor));
                    OnPropertyChanged(nameof(CardEdgeThickness));
                    OnPropertyChanged(nameof(CardEdgeSwatch));
                }
                ThemeManager.ApplyOutline(value);
                Services.AI.AIConfig.ConfigService.SaveDebounced(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card outline changed",
                    value ? "Defined edge ON" : "Defined edge OFF");

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Custom card-edge colour (hex) for the "Define Card Edges" picker.
        /// Empty = follow the theme's strong outline. Applied live through
        /// ThemeManager.ApplyCardEdgeColor + ApplyOutline, so the change
        /// reaches every card border app-wide immediately.
        /// </summary>
        public string CardEdgeColor
        {
            get => _config?.Appearance?.CardEdgeColor ?? "";
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.CardEdgeColor == value) return;

                _config.Appearance.CardEdgeColor = value;
                ThemeManager.ApplyCardEdgeColor(value);
                ThemeManager.ApplyOutline(UseDefinedOutline);
                ScheduleDebouncedSave();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CardEdgeSwatch));
            }
        }

        /// <summary>
        /// Live swatch for the card-edge colour picker. Shows the parsed
        /// hex (or the theme's strong outline when the field is empty) so
        /// the chip always renders a real preview.
        /// </summary>
        public System.Windows.Media.Brush CardEdgeSwatch
        {
            get
            {
                var parsed = ProGlassAutomation.Services.Theme.AccentPresets
                    .TryParseHex(_config?.Appearance?.CardEdgeColor);
                if (parsed is System.Windows.Media.Color c)
                {
                    var brush = new SolidColorBrush(c);
                    brush.Freeze();
                    return brush;
                }

                return Application.Current?.TryFindResource("OutlineStrongBrush")
                        as System.Windows.Media.Brush
                    ?? System.Windows.Media.Brushes.SlateGray;
            }
        }

        /// <summary>
        /// Card-edge colour plate (Settings → Appearance → Define Card
        /// Edges). Generated from AccentPresets.All exactly like the
        /// accent palette, so each swatch IS the colour that gets applied
        /// to the card border line.
        /// </summary>
        public ObservableCollection<AccentOption> CardEdgePalette { get; } = new();

        /// <summary>One-tap: apply a preset colour to every card edge.</summary>
        public ICommand ApplyCardEdgePresetCommand { get; private set; }

        private void InitializeCardEdgePalette()
        {
            CardEdgePalette.Clear();
            foreach (var (key, name) in
                ProGlassAutomation.Services.Theme.AccentPresets.All)
            {
                CardEdgePalette.Add(new AccentOption(
                    key, name,
                    ProGlassAutomation.Services.Theme.AccentPresets.SwatchFor(key)));
            }
        }

        /// <summary>
        /// Applies a preset (or "Auto") to the card-edge colour. The hex
        /// is written into Appearance.CardEdgeColor and repainted live
        /// through ThemeManager, the same path the hex field uses.
        /// </summary>
        private void ApplyCardEdgePreset(string key)
        {
            if (_config?.Appearance == null) return;

            if (string.IsNullOrWhiteSpace(key) || key == "Auto")
            {
                CardEdgeColor = "";
                return;
            }

            var color = ProGlassAutomation.Services.Theme.AccentPresets
                .ColorFor(key);
            if (color == null) return;

            CardEdgeColor =
                $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";

            ProGlassAutomation.Services.Audit.AuditService.Log(
                ProGlassAutomation.Services.Audit.AuditSections.Settings,
                "Card edge colour changed",
                CardEdgeColor);
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
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                OnPropertyChanged(nameof(SurfaceSecondaryBorderThickness));
                // Debounced: no disk/audit writes per slider tick.
                ScheduleDebouncedSave(auditBorder: true);

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Card EDGE weight (DIPs, 1–100) — the defined corner-border line
        /// around cards (Settings → Appearance → Define Card Edges). One
        /// slider restyles every card surface through the CardEdgeThickness
        /// token; text boxes and gridlines stay on the subtle
        /// CardBorderThickness hairline.
        /// </summary>
        public double CardEdgeThickness
        {
            get => _config?.Appearance?.CardEdgeThickness ?? 2.0;
            set
            {
                if (_config?.Appearance == null) return;
                double v = value < 1 ? 1 : (value > 100 ? 100 : value);
                if (Math.Abs(_config.Appearance.CardEdgeThickness - v) < 0.01) return;

                _config.Appearance.CardEdgeThickness = v;
                ThemeManager.ApplyCardEdgeThickness(v);
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                OnPropertyChanged(nameof(SurfacePrimaryBorderThickness));
                ScheduleDebouncedSave(auditBorder: true);

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

        public List<string> SurfaceMaterialOptions { get; } = new()
        {
            "Glass", "Solid", "Transparent"
        };

        public string SurfaceMaterialMode
        {
            get => _config?.Appearance?.SurfaceMaterialMode ?? "Glass";
            set
            {
                if (_config?.Appearance == null) return;
                string v = string.IsNullOrWhiteSpace(value) ? "Glass" : value.Trim();
                if (v is not ("Glass" or "Solid" or "Transparent")) v = "Glass";
                if (_config.Appearance.SurfaceMaterialMode == v) return;
                _config.Appearance.SurfaceMaterialMode = v;
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        public bool UseRoundedSurfaces
        {
            get => _config?.Appearance?.UseRoundedSurfaces ?? true;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.UseRoundedSurfaces == value) return;
                _config.Appearance.UseRoundedSurfaces = value;
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        public bool UsePrimarySurfaceElevation
        {
            get => _config?.Appearance?.UsePrimarySurfaceElevation ?? false;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.UsePrimarySurfaceElevation == value) return;
                _config.Appearance.UsePrimarySurfaceElevation = value;
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        public double SurfacePrimaryOpacity
        {
            get => _config?.Appearance?.SurfacePrimaryOpacity ?? 1.0;
            set => SetSurfaceDouble(value, 0.30, 1.0,
                v => _config!.Appearance.SurfacePrimaryOpacity = v,
                () => _config!.Appearance.SurfacePrimaryOpacity,
                nameof(SurfacePrimaryOpacity));
        }

        public double SurfaceSecondaryOpacity
        {
            get => _config?.Appearance?.SurfaceSecondaryOpacity ?? 0.94;
            set => SetSurfaceDouble(value, 0.25, 1.0,
                v => _config!.Appearance.SurfaceSecondaryOpacity = v,
                () => _config!.Appearance.SurfaceSecondaryOpacity,
                nameof(SurfaceSecondaryOpacity));
        }

        public double SurfaceDetailOpacity
        {
            get => _config?.Appearance?.SurfaceDetailOpacity ?? 0.86;
            set => SetSurfaceDouble(value, 0.15, 1.0,
                v => _config!.Appearance.SurfaceDetailOpacity = v,
                () => _config!.Appearance.SurfaceDetailOpacity,
                nameof(SurfaceDetailOpacity));
        }

        public double PrimarySurfaceOverlayOpacity
        {
            get => _config?.Appearance?.PrimarySurfaceOverlayOpacity ?? 0.0;
            set => SetSurfaceDouble(value, 0.0, 0.35,
                v => _config!.Appearance.PrimarySurfaceOverlayOpacity = v,
                () => _config!.Appearance.PrimarySurfaceOverlayOpacity,
                nameof(PrimarySurfaceOverlayOpacity));
        }

        public double SurfacePrimaryRadius
        {
            get => _config?.Appearance?.SurfacePrimaryRadius ?? CardCornerRadius;
            set => SetSurfaceNullableDouble(value, 0, 48,
                v => _config!.Appearance.SurfacePrimaryRadius = v,
                () => _config!.Appearance.SurfacePrimaryRadius ?? CardCornerRadius,
                nameof(SurfacePrimaryRadius));
        }

        public double SurfaceSecondaryRadius
        {
            get => _config?.Appearance?.SurfaceSecondaryRadius ?? CardCornerRadius;
            set => SetSurfaceNullableDouble(value, 0, 48,
                v => _config!.Appearance.SurfaceSecondaryRadius = v,
                () => _config!.Appearance.SurfaceSecondaryRadius ?? CardCornerRadius,
                nameof(SurfaceSecondaryRadius));
        }

        public double SurfaceDetailRadius
        {
            get => _config?.Appearance?.SurfaceDetailRadius ?? CardCornerRadius;
            set => SetSurfaceNullableDouble(value, 0, 48,
                v => _config!.Appearance.SurfaceDetailRadius = v,
                () => _config!.Appearance.SurfaceDetailRadius ?? CardCornerRadius,
                nameof(SurfaceDetailRadius));
        }

        public double SurfacePrimaryBorderThickness
        {
            get => _config?.Appearance?.SurfacePrimaryBorderThickness ?? CardEdgeThickness;
            set => SetSurfaceNullableDouble(value, 0, 24,
                v => _config!.Appearance.SurfacePrimaryBorderThickness = v,
                () => _config!.Appearance.SurfacePrimaryBorderThickness ?? CardEdgeThickness,
                nameof(SurfacePrimaryBorderThickness));
        }

        public double SurfaceSecondaryBorderThickness
        {
            get => _config?.Appearance?.SurfaceSecondaryBorderThickness ?? CardBorderThickness;
            set => SetSurfaceNullableDouble(value, 0, 24,
                v => _config!.Appearance.SurfaceSecondaryBorderThickness = v,
                () => _config!.Appearance.SurfaceSecondaryBorderThickness ?? CardBorderThickness,
                nameof(SurfaceSecondaryBorderThickness));
        }

        public double SurfaceDetailBorderThickness
        {
            get => _config?.Appearance?.SurfaceDetailBorderThickness ?? 1.0;
            set => SetSurfaceNullableDouble(value, 0, 24,
                v => _config!.Appearance.SurfaceDetailBorderThickness = v,
                () => _config!.Appearance.SurfaceDetailBorderThickness ?? 1.0,
                nameof(SurfaceDetailBorderThickness));
        }

        private void SetSurfaceDouble(
            double value, double min, double max,
            Action<double> setter, Func<double> getter, string propertyName)
        {
            if (_config?.Appearance == null) return;
            double v = value < min ? min : (value > max ? max : value);
            if (Math.Abs(getter() - v) < 0.001) return;
            setter(v);
            ThemeManager.ApplySemanticSurfaces(_config.Appearance);
            ScheduleDebouncedSave();
            OnPropertyChanged(propertyName);
        }

        private void SetSurfaceNullableDouble(
            double value, double min, double max,
            Action<double?> setter, Func<double> getter, string propertyName)
        {
            if (_config?.Appearance == null) return;
            double v = value < min ? min : (value > max ? max : value);
            if (Math.Abs(getter() - v) < 0.001) return;
            setter(v);
            ThemeManager.ApplySemanticSurfaces(_config.Appearance);
            ScheduleDebouncedSave();
            OnPropertyChanged(propertyName);
        }


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
                Services.AI.AIConfig.ConfigService.SaveDebounced(_config);

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
