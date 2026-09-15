// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Typography.CustomColor.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): split out of SettingsViewModel.Typography.cs so no file in the
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

    }
}
