// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Typography.FontsSurfaces.cs
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
                catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
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
            catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
        }

        /// <summary>Detected display tier for the Performance section.</summary>
        public string PerformanceTierDisplay =>
            $"{ProGlassAutomation.Services.Performance.PerformanceProfileService.TargetFps} fps profile" +
            (ProGlassAutomation.Services.Performance.PerformanceProfileService.RichEffectsEnabled
                ? "  •  rich effects on"
                : "  •  effects optimized");

    }
}
