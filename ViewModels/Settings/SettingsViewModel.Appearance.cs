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

        // ══════════════════════════════════════════════════════════════
        //  SPLASH SCREEN STYLE / ROTATION SETTINGS
        // ══════════════════════════════════════════════════════════════

        public IReadOnlyList<ProGlassAutomation.Services.Splash.SplashStyleOption> SplashStyleOptions =>
            ProGlassAutomation.Services.Splash.SplashThemeManager.AvailableStyles;

        public int SelectedSplashStyle
        {
            get => _config?.Appearance?.SplashStyle ?? 0;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.SplashStyle == value) return;
                _config.Appearance.SplashStyle = value;
                ScheduleDebouncedSave(auditTheme: true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSplashStyleName));
                OnPropertyChanged(nameof(SelectedSplashStyleDescription));
            }
        }

        public string SelectedSplashStyleName =>
            ProGlassAutomation.Services.Splash.SplashThemeManager.AvailableStyles
                .FirstOrDefault(s => s.Id == SelectedSplashStyle)?.Name ?? "Auto (Cycle on Boot)";

        public string SelectedSplashStyleDescription =>
            ProGlassAutomation.Services.Splash.SplashThemeManager.AvailableStyles
                .FirstOrDefault(s => s.Id == SelectedSplashStyle)?.Description ?? "";
    }
}
