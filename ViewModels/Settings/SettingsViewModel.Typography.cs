// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Typography.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Typography
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
using ProGlassAutomation.Utilities;
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
        //  GLOBAL FONT COLOUR — Settings → Appearance → Font Color
        //
        //  One colour drives the whole UI's Primary/Secondary/Muted
        //  foregrounds (the VisualThemeEngine derives the hierarchy so
        //  headings/captions/hints keep their contrast order). "Auto"
        //  hands ownership back to the theme/visual family — in Crystal
        //  Fluid (Ios-17pm) that is the About page's text palette.
        //  Persists like the accent, applies live like the accent, works
        //  in EVERY visual family — the visibility dial for transparent
        //  UIs.
        // ══════════════════════════════════════════════════════════════

        /// <summary>Chip list for the Font Color tile (Auto + presets).</summary>
        public ObservableCollection<FontColorOption> FontColorPalette { get; }
            = new();

        public ICommand ApplyFontColorCommand { get; private set; }

        public ICommand ApplyCustomFontColorCommand { get; private set; }

        /// <summary>One option on the Settings font colour palette.</summary>
        public sealed class FontColorOption
        {
            public FontColorOption(string key, string name, Color swatch)
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

        private void InitializeFontColorPalette()
        {
            FontColorPalette.Clear();

            // Auto = "the active theme/family owns the text" — the
            // swatch shows the current theme's own primary colour.
            Color autoSwatch = ProGlassAutomation.Services.ThemeManager
                .GetThemeColor("PrimaryForegroundColor",
                    System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
            FontColorPalette.Add(new FontColorOption(
                ProGlassAutomation.Services.Theme.FontColorPresets.Auto,
                "Auto (theme)", autoSwatch));

            foreach (var (key, name, color) in
                ProGlassAutomation.Services.Theme.FontColorPresets.All)
            {
                FontColorPalette.Add(new FontColorOption(key, name, color));
            }
        }

        /// <summary>
        /// Persisted global font colour — "" (Auto) or #RRGGBB.
        /// Applies live through the normal visual-theme pipeline.
        /// </summary>
        public string SelectedFontColor
        {
            get => _config?.Appearance?.FontColor ?? "";
            set
            {
                if (_config == null) return;

                _config.Appearance.FontColor = value ?? "";
                SaveConfig();
                ApplyVisualTheme();
                OnPropertyChanged();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Font colour changed",
                    string.IsNullOrEmpty(value) ? "Auto" : value);
            }
        }

        private void ApplyFontColorPreset(string presetKey)
        {
            // FontColorPresets is a static class — qualify with the
            // type name, never an instance reference.
            // Auto → clear the override; preset → its exact hex.
            string hex =
                ProGlassAutomation.Services.Theme.FontColorPresets.Auto
                    == presetKey
                ? ""
                : ProGlassAutomation.Services.Theme.FontColorPresets
                    .ColorFor(presetKey) is Color c
                    ? ProGlassAutomation.Services.Theme.FontColorPresets
                        .ToHex(c)
                    : "";

            SelectedFontColor = hex;
            _customFontColorHex = hex; // keep the hex field in sync
            OnPropertyChanged(nameof(CustomFontColorHex));
            OnPropertyChanged(nameof(CustomFontColorSwatch));
            OnPropertyChanged(nameof(CustomFontColorStatus));
        }

        // ── Free-pick custom font colour ─────────────────────────────

        private string _customFontColorHex = "";

        /// <summary>Hex the user types for a custom font colour.</summary>
        public string CustomFontColorHex
        {
            get => _customFontColorHex;
            set
            {
                _customFontColorHex = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomFontColorSwatch));
                OnPropertyChanged(nameof(CustomFontColorStatus));
            }
        }

        /// <summary>Live preview dot next to the hex field.</summary>
        public Brush CustomFontColorSwatch
        {
            get
            {
                var c = ProGlassAutomation.Services.Theme.AccentPresets
                    .TryParseHex(_customFontColorHex);
                if (c == null) return Brushes.Transparent;
                var brush = new SolidColorBrush(c.Value);
                brush.Freeze();
                return brush;
            }
        }

        /// <summary>Validation hint under the hex field.</summary>
        public string CustomFontColorStatus
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_customFontColorHex))
                    return "Type a colour like #FFFFFF and press APPLY — or pick Auto to follow the theme.";
                return ProGlassAutomation.Services.Theme.AccentPresets
                    .TryParseHex(_customFontColorHex) != null
                    ? "Ready — every heading, caption and hint in every view will use this colour family."
                    : "Not a recognised colour — use #RGB or #RRGGBB.";
            }
        }

        private void ApplyCustomFontColor()
        {
            var color = ProGlassAutomation.Services.Theme.AccentPresets
                .TryParseHex(_customFontColorHex);
            if (color == null)
            {
                OnPropertyChanged(nameof(CustomFontColorStatus));
                return;
            }

            SelectedFontColor = ProGlassAutomation.Services.Theme
                .FontColorPresets.ToHex(color.Value);
            OnPropertyChanged(nameof(CustomFontColorSwatch));
            OnPropertyChanged(nameof(CustomFontColorStatus));
        }

        // ── Factory reset for the whole Appearance module ────────────
        //
        // 2026-08-25: one tap restores every appearance preference to its
        // factory default — visual theme, accent, gradients, card edge /
        // radius, display density, UI zoom, font family / weight / style,
        // the global wallpaper and every per-module wallpaper, and the PDF
        // layout. It goes through the SAME apply-path methods each
        // individual control uses, so the whole app restyles live instead
        // of waiting for a restart, and no setting is missed.
        private async Task ResetAppearanceToDefaultsAsync()
        {
            if (IsResettingAppearance) return;
            
            IsResettingAppearance = true;
            ResetStatusMessage = "Resetting appearance...";

            try
            {
                // Run the heavy work on a background thread to avoid UI freeze
                await Task.Run(() =>
                {
                    var ap = _config?.Appearance;
                    if (ap == null) return;

                    // 1) Reset the persisted values to factory defaults
                    //    (the strict industrial default theme).
                    SetResetStatus("Resetting configuration...");
                    ap.VisualTheme = ProGlassAutomation.Services.Theme.VisualThemeEngine.NormalIndustrial;
                    ap.Theme = "Light";
                    ap.UseDefinedOutline = false;
                    ap.UseGradientSurfaces = true;
                    ap.CardBorderThickness = 1.5;
                    ap.CardCornerRadius = 12;
                    ap.SurfaceMaterialMode = "Glass";
                    ap.CardBlurIntensity = 28;
                    ap.CardFrostTint = "";
                    ap.SurfacePrimaryOpacity = 0.82;
                    ap.SurfaceSecondaryOpacity = 0.94;
                    ap.SurfaceDetailOpacity = 0.86;
                    ap.SurfacePrimaryRadius = null;
                    ap.SurfaceSecondaryRadius = null;
                    ap.SurfaceDetailRadius = null;
                    ap.SurfacePrimaryBorderThickness = null;
                    ap.SurfaceSecondaryBorderThickness = null;
                    ap.SurfaceDetailBorderThickness = null;
                    ap.UseRoundedSurfaces = true;
                    ap.UsePrimarySurfaceElevation = false;
                    ap.PrimarySurfaceOverlayOpacity = 0.0;
                    ap.DisplayDensity = "Compact";
                    ap.UiScale = 1.0;
                    ap.WallpaperPath = "";
                    ap.WallpaperOpacity = 0.85;
                    ap.ModuleWallpapers.Clear();
                    ap.AccentPreset = ProGlassAutomation.Services.Theme.AccentPresets.GlassBlue;
                    ap.CustomAccentHex = "";
                    ap.FontColor = "";
                    ap.PdfLayoutId = "ClassicNavy";
                    ap.FontFamilyName = "Segoe UI";
                    ap.FontWeightName = "Normal";
                    ap.FontStyleName = "Normal";
                    SaveConfig();

                    // 2) Apply theme changes on UI thread
                    SetResetStatus("Applying theme...");
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ThemeManager.ApplyTheme(AppTheme.Light);
                    }));

                    SetResetStatus("Configuring gradients...");
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ThemeManager.ApplyGradientPreference(true);
                    }));

                    SetResetStatus("Setting display density...");
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ThemeManager.ApplyDisplayDensity("Compact");
                        ThemeManager.ApplySemanticSurfaces(_config?.Appearance);
                        ThemeManager.ApplyUiScale(1.0);
                    }));

                    SetResetStatus("Applying visual theme...");
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ApplyVisualTheme();
                    }));

                    SetResetStatus("Resetting fonts...");
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FontService.ResetToDefault();
                    }));
                });

                // 3) Resync the bound controls on UI thread
                SetResetStatus("Updating UI...");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ReloadFontOptions();
                    var defFont = FontOptions.FirstOrDefault(f =>
                        string.Equals(f.Name, FontService.DefaultFontFamily,
                            StringComparison.OrdinalIgnoreCase));
                    if (defFont != null) { _selectedFont = defFont; }
                    _selectedFontWeight = "Regular";
                    _selectedFontStyle = "Normal";
                    _customAccentHex = "";
                    _customFontColorHex = "";
                    foreach (var row in ModuleWallpaperRows) row.Enabled = false;

                    OnPropertyChanged(nameof(SelectedVisualTheme));
                    OnPropertyChanged(nameof(SelectedFontColor));
                    OnPropertyChanged(nameof(CustomFontColorHex));
                    OnPropertyChanged(nameof(CustomFontColorSwatch));
                    OnPropertyChanged(nameof(CustomFontColorStatus));
                    OnPropertyChanged(nameof(UseDefinedOutline));
                    OnPropertyChanged(nameof(UseGradientSurfaces));
                    OnPropertyChanged(nameof(CardBorderThickness));
                    OnPropertyChanged(nameof(CardCornerRadius));
                    OnPropertyChanged(nameof(SurfaceMaterialMode));
                    OnPropertyChanged(nameof(CardBlurIntensity));
                    OnPropertyChanged(nameof(CardFrostTint));
                    OnPropertyChanged(nameof(CardFrostTintSwatch));
                    OnPropertyChanged(nameof(SurfacePrimaryOpacity));
                    OnPropertyChanged(nameof(SurfaceSecondaryOpacity));
                    OnPropertyChanged(nameof(SurfaceDetailOpacity));
                    OnPropertyChanged(nameof(SurfacePrimaryRadius));
                    OnPropertyChanged(nameof(SurfaceSecondaryRadius));
                    OnPropertyChanged(nameof(SurfaceDetailRadius));
                    OnPropertyChanged(nameof(SurfacePrimaryBorderThickness));
                    OnPropertyChanged(nameof(SurfaceSecondaryBorderThickness));
                    OnPropertyChanged(nameof(SurfaceDetailBorderThickness));
                    OnPropertyChanged(nameof(UseRoundedSurfaces));
                    OnPropertyChanged(nameof(UsePrimarySurfaceElevation));
                    OnPropertyChanged(nameof(PrimarySurfaceOverlayOpacity));
                    OnPropertyChanged(nameof(DisplayDensity));
                    OnPropertyChanged(nameof(UiScale));
                    OnPropertyChanged(nameof(UiScalePercent));
                    OnPropertyChanged(nameof(WallpaperOpacity));
                    OnPropertyChanged(nameof(WallpaperStatus));
                    OnPropertyChanged(nameof(SelectedFont));
                    OnPropertyChanged(nameof(SelectedFontWeight));
                    OnPropertyChanged(nameof(SelectedFontStyle));
                    OnPropertyChanged(nameof(FontStatus));
                    OnPropertyChanged(nameof(CustomAccentHex));
                    OnPropertyChanged(nameof(CustomAccentSwatch));
                    OnPropertyChanged(nameof(CustomAccentStatus));
                });

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Appearance reset to factory defaults", "");

                SetResetStatus("Complete!");
                await Task.Delay(500); // Show completion message briefly
            }
            catch (Exception ex)
            {
                SetResetStatus($"Error: {ExceptionMessages.Deep(ex)}");
                await Task.Delay(2000);
            }
            finally
            {
                IsResettingAppearance = false;
                ResetStatusMessage = "";
            }
        }

        private void SetResetStatus(string message)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetStatusMessage = message;
            }));
        }

        // ── Gradient surfaces (adaptive-theme depth option) ───────────

        /// <summary>
        /// Gradient support for Monet/adaptive surfaces. Off = flat
        /// palette, On = accent-derived gradients (richer depth).
        /// </summary>
        public bool UseGradientSurfaces
        {
            get => _config?.Appearance?.UseGradientSurfaces ?? true;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.UseGradientSurfaces == value) return;

                _config.Appearance.UseGradientSurfaces = value;
                ThemeManager.ApplyGradientPreference(value);

                // Rebuild the active visual family so surfaces restyle now.
                ApplyVisualTheme();
                SaveConfig();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Gradient surfaces changed",
                    value ? "Gradients on" : "Gradients off");

                OnPropertyChanged();
            }
        }

        /// <summary>Applies a named accent preset. Parameter = preset key.</summary>
        public void ApplyAccentPreset(string presetKey)
        {
            // The Custom chip resolves through the hex field instead.
            if (presetKey == ProGlassAutomation.Services.Theme.AccentPresets.Custom)
            {
                ApplyCustomAccent();
                return;
            }

            var color = ProGlassAutomation.Services.Theme.AccentPresets
                .ColorFor(presetKey);

            ThemeManager.ApplyAccent(color);

            // Persist so the chosen colour survives app restarts.
            if (_config?.Appearance != null)
            {
                _config.Appearance.AccentPreset =
                    string.IsNullOrEmpty(presetKey) ? "GlassBlue" : presetKey;
                SaveConfig();
            }

            ProGlassAutomation.Services.Audit.AuditService.Log(
                ProGlassAutomation.Services.Audit.AuditSections.Settings,
                "Accent changed",
                presetKey == "GlassBlue" ? "Glass Blue (default)" : presetKey);
        }

        // ══════════════════════════════════════════════════════════════
        //  GLOBAL FONT (FontService) — system-wide UI typography
        //
        //  Lets the user pick any installed or imported font, weight
        //  (Regular/Bold) and style (Normal/Italic). The choice is applied
        //  app-wide through the AppFontFamily / AppFontWeight / AppFontStyle
        //  resources and persisted in the appearance config.
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<FontOption> FontOptions { get; } = new();

        public IReadOnlyList<string> FontWeightChoices { get; } =
            new[] { "Regular", "Bold" };

        public IReadOnlyList<string> FontStyleChoices { get; } =
            new[] { "Normal", "Italic" };

        private FontOption? _selectedFont;
        public FontOption? SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (value == null || ReferenceEquals(_selectedFont, value)) return;
                _selectedFont = value;
                OnPropertyChanged();
                ApplyFontSelection();
            }
        }

        private string _selectedFontWeight = "Regular";
        public string SelectedFontWeight
        {
            get => _selectedFontWeight;
            set
            {
                if (_selectedFontWeight == value || string.IsNullOrWhiteSpace(value)) return;
                _selectedFontWeight = value;
                OnPropertyChanged();
                ApplyFontSelection();
            }
        }

        private string _selectedFontStyle = "Normal";
        public string SelectedFontStyle
        {
            get => _selectedFontStyle;
            set
            {
                if (_selectedFontStyle == value || string.IsNullOrWhiteSpace(value)) return;
                _selectedFontStyle = value;
                OnPropertyChanged();
                ApplyFontSelection();
            }
        }

        public ICommand ImportFontCommand { get; private set; } = null!;
        public ICommand ResetFontCommand { get; private set; } = null!;

        public string FontStatus =>
            string.IsNullOrWhiteSpace(FontService.CurrentFamily)
                ? "Segoe UI (default)"
                : FontService.CurrentFamily +
                  (FontService.CurrentWeight == FontWeights.Bold ? " · Bold" : "") +
                  (FontService.CurrentStyle == FontStyles.Italic ? " · Italic" : "");

        private void InitializeFontSettings()
        {
            ReloadFontOptions();

            var current = _config?.Appearance;
            _selectedFontWeight = string.Equals(current?.FontWeightName, "Bold", StringComparison.OrdinalIgnoreCase)
                ? "Bold" : "Regular";
            _selectedFontStyle = string.Equals(current?.FontStyleName, "Italic", StringComparison.OrdinalIgnoreCase)
                ? "Italic" : "Normal";

            string family = current?.FontFamilyName ?? FontService.DefaultFontFamily;
            _selectedFont = FontOptions.FirstOrDefault(f =>
                string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase))
                ?? FontOptions.FirstOrDefault(f =>
                    string.Equals(f.Name, FontService.DefaultFontFamily, StringComparison.OrdinalIgnoreCase));

            ImportFontCommand = new RelayCommand(_ => ImportFontDialog());
            ResetFontCommand = new RelayCommand(_ =>
            {
                FontService.ResetToDefault();
                ReloadFontOptions();
                var d = FontOptions.FirstOrDefault(f =>
                    string.Equals(f.Name, FontService.DefaultFontFamily, StringComparison.OrdinalIgnoreCase));
                if (d != null) { _selectedFont = d; OnPropertyChanged(nameof(SelectedFont)); }
                _selectedFontWeight = "Regular"; OnPropertyChanged(nameof(SelectedFontWeight));
                _selectedFontStyle = "Normal"; OnPropertyChanged(nameof(SelectedFontStyle));
                OnPropertyChanged(nameof(FontStatus));
            });
        }

        private void ReloadFontOptions()
        {
            FontOptions.Clear();
            foreach (var f in FontService.GetAvailableFonts())
                FontOptions.Add(f);
        }

        private void ApplyFontSelection()
        {
            if (_selectedFont == null) return;

            var weight = string.Equals(_selectedFontWeight, "Bold", StringComparison.OrdinalIgnoreCase)
                ? FontWeights.Bold : FontWeights.Normal;
            var style = string.Equals(_selectedFontStyle, "Italic", StringComparison.OrdinalIgnoreCase)
                ? FontStyles.Italic : FontStyles.Normal;

            FontService.ApplyFont(_selectedFont.Name, weight, style);
            OnPropertyChanged(nameof(FontStatus));
        }

        private void ImportFontDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import font",
                Filter = "Font files (*.ttf;*.otf;*.ttc;*.fon)|*.ttf;*.otf;*.ttc;*.fon|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            var result = FontService.ImportFont(dialog.FileName);
            if (!result.IsSuccess)
            {
                try
                {
                    _dialogs.Show(
                        result.Error, LocalizationService.Instance.T("Msg.CapImportFont"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { /* status still updated */ }
                return;
            }

            ReloadFontOptions();
            var imported = FontOptions.FirstOrDefault(f =>
                string.Equals(f.Name, result.Value, StringComparison.OrdinalIgnoreCase));
            if (imported != null)
            {
                _selectedFont = imported;
                OnPropertyChanged(nameof(SelectedFont));
                ApplyFontSelection();
            }

            try
            {
                _dialogs.Show(
                    "Font imported: " + result.Value + "\nIt is now applied app-wide.",
                    LocalizationService.Instance.T("Msg.CapImportFont"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { /* status still updated */ }
        }

        /// <summary>Detected display tier for the Performance section.</summary>
        public string PerformanceTierDisplay =>
            $"{ProGlassAutomation.Services.Performance.PerformanceProfileService.TargetFps} fps profile" +
            (ProGlassAutomation.Services.Performance.PerformanceProfileService.RichEffectsEnabled
                ? "  •  rich effects on"
                : "  •  effects optimized");

    }
}
