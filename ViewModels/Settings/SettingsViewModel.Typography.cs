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

    }
}
