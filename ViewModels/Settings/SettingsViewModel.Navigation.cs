// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Navigation.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Navigation
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
        //  v2 — SETTINGS PAGE NAVIGATION (shell nav rail)
        //  Same proven pattern as the TI/DN/JO detail tab strips:
        //  one string key + Is{Page}Page flags + switch command.
        // ══════════════════════════════════════════════════════════════

        private string _settingsPage = "Appearance";

        // ══════════════════════════════════════════════════════════════
        //  v3 — DATA-DRIVEN NAV (2026-09-05 Phase 3). The rail used to
        //  be ten hand-copied Button blocks in XAML with an IsXPage flag
        //  each — adding a page meant XAML surgery + a new flag + a new
        //  notify line. Now the ROW LIST below is the single source of
        //  truth: the rail is one ItemsControl, page validation reads
        //  the row keys, and the section header shows a LOCALIZED label
        //  (it used to print the raw English page key).
        // ══════════════════════════════════════════════════════════════

        /// <summary>One rail entry. Labels resolve through the localization
        /// service at display time and refresh on LanguageChanged.</summary>
        public sealed class SettingsNavRow : ViewModelBase
        {
            public string Key { get; }
            public string Glyph { get; }
            public string LabelKey { get; }
            public string Label => Services.Localization
                .LocalizationService.Instance.T(LabelKey);

            private bool _isActive;
            public bool IsActive
            {
                get => _isActive;
                internal set
                {
                    if (_isActive == value) return;
                    _isActive = value;
                    OnPropertyChanged();
                }
            }

            public SettingsNavRow(string key, string glyph, string labelKey)
            {
                Key = key; Glyph = glyph; LabelKey = labelKey;
            }

            internal void RefreshLabel() =>
                OnPropertyChanged(nameof(Label));
        }

        /// <summary>The rail pages (single registry). Glyphs preserved from
        /// the hand-built rail; Agent finally has its own glyph (it used to
        /// share AI's E8F2 — two identical icons side by side).</summary>
        public ObservableCollection<SettingsNavRow> NavRows { get; } =
            CreateNavRows();

        private static ObservableCollection<SettingsNavRow> CreateNavRows()
        {
            var rows = new ObservableCollection<SettingsNavRow>
            {
                new("Appearance", "\uE790", "Settings.SecAppearance"),
                new("AI",         "\uE8F2", "Settings.SecAi"),
                new("Agent",      "\uE9F5", "Settings.SecAgent"),
                new("Voice",      "\uE720", "Settings.SecVoice"),
                new("Updates",    "\uE72C", "Settings.SecUpdates"),
                new("Data",       "\uE74E", "Settings.SecData"),
                new("Demo",       "\uE714", "Settings.SecDemo"),
                new("License",    "\uE762", "Settings.SecLicense"),
                new("Features",   "\uE192", "Settings.SecFeatures"),
                new("About",      "\uE946", "Settings.SecAbout"),
            };
            rows[0].IsActive = true;
            return rows;
        }

        /// <summary>Localized title of the active page (content header).</summary>
        public string ActivePageLabel =>
            NavRows.FirstOrDefault(r => r.IsActive)?.Label ?? _settingsPage;

        /// <summary>True while an industrial (flat) family is active —
        /// the rail then renders matte (no sheen/glare layers), keeping
        /// the settings chrome honest with the family's no-theatre law.</summary>
        public bool IsIndustrialLook =>
            ProGlassAutomation.Services.Theme.VisualThemeEngine
                .IsIndustrial(_config?.Appearance?.VisualTheme ?? "");

        /// <summary>Active settings page key (nav rail selection).</summary>
        public string SettingsPage
        {
            get => _settingsPage;
            private set
            {
                // Guard: the row list IS the page registry — an unknown
                // key cannot collapse the content pane because it simply
                // is not a registered row.
                if (value == null || !NavRows.Any(r => r.Key == value))
                    return;

                if (_settingsPage == value) return;
                _settingsPage = value;
                OnPropertyChanged();
                // 2026-09-05 owner directive: picking a page from the rail
                // CLOSES the rail automatically so the section gets the
                // full module width. Applies to every switch path — rail
                // buttons AND programmatic GoToPage navigation — because it
                // lives here in the single setter.
                IsSettingsRailOpen = false;
                foreach (var row in NavRows)
                    row.IsActive = row.Key == value;
                OnPropertyChanged(nameof(ActivePageLabel));
            }
        }

        /// <summary>Switches the active settings page (nav rail).</summary>
        public ICommand SwitchSettingsPageCommand { get; private set; }

        /// <summary>
        /// Settings navigation rail visibility (2026-09-05 drawer behaviour).
        /// True when entering the module; closes automatically as soon as a
        /// page is picked, and re-opens with a single click on the floating
        /// menu button. Single-click open/close, no double state.
        /// </summary>
        private bool _isSettingsRailOpen = true;
        public bool IsSettingsRailOpen
        {
            get => _isSettingsRailOpen;
            set
            {
                if (_isSettingsRailOpen == value) return;
                _isSettingsRailOpen = value;
                OnPropertyChanged();
            }
        }

        private ICommand? _toggleSettingsRailCommand;
        /// <summary>Single click toggles the rail: open when closed, close
        /// when open.</summary>
        public ICommand ToggleSettingsRailCommand =>
            _toggleSettingsRailCommand ??= new RelayCommand(
                _ => IsSettingsRailOpen = !IsSettingsRailOpen);

        /// <summary>
        /// Command-free way to switch page. SettingsPage has a private
        /// setter, and SwitchSettingsPageCommand is null until
        /// InitializeCore() has run, so callers outside this class need a
        /// path that cannot NullReference (see ERR-BEFC44 / ERR-435233).
        /// Validation still happens in the SettingsPage setter.
        /// </summary>
        public void GoToPage(string? page)
            => SettingsPage = string.IsNullOrWhiteSpace(page) ? "Appearance" : page;

        public string ConfigFilePath { get; private set; }

    }
}
