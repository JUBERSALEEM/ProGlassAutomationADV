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

        /// <summary>Active settings page key (nav rail selection).</summary>
        public string SettingsPage
        {
            get => _settingsPage;
            private set
            {
                // Guard: an unknown key would collapse every section and
                // leave the content pane empty — refuse it outright.
                if (value != "Appearance" && value != "AI" &&
                    value != "Agent" && value != "Voice" &&
                    value != "Updates" && value != "Data" &&
                    value != "Demo" && value != "License" &&
                    value != "Features" && value != "About")
                    return;

                if (_settingsPage == value) return;
                _settingsPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAppearancePage));
                OnPropertyChanged(nameof(IsAiPage));
                OnPropertyChanged(nameof(IsAgentPage));   // PHASE F
                OnPropertyChanged(nameof(IsVoicePage));
                OnPropertyChanged(nameof(IsUpdatesPage));
                OnPropertyChanged(nameof(IsDataPage));
                OnPropertyChanged(nameof(IsAboutPage));
                OnPropertyChanged(nameof(IsDemoPage));
                OnPropertyChanged(nameof(IsLicensePage));
                OnPropertyChanged(nameof(IsFeaturesPage));
            }
        }

        public bool IsAppearancePage => SettingsPage == "Appearance";
        public bool IsAiPage         => SettingsPage == "AI";

        /// <summary>PHASE F — ProGlass Agent privacy / teaching page.</summary>
        public bool IsAgentPage      => SettingsPage == "Agent";

        public bool IsVoicePage      => SettingsPage == "Voice";
        public bool IsUpdatesPage    => SettingsPage == "Updates";
        public bool IsDataPage       => SettingsPage == "Data";
        public bool IsAboutPage      => SettingsPage == "About";
        public bool IsDemoPage       => SettingsPage == "Demo";

        /// <summary>
        /// License management page (Settings → License).
        /// Hosts the shared <c>LicenseActivationView</c> so
        /// the user can start a trial, install a .lic file,
        /// or inspect their current activation. This is the
        /// ONLY place in the shell where licensing lives now
        /// — the old startup licence gate is gone.
        /// </summary>
        public bool IsLicensePage    => SettingsPage == "License";

        /// <summary>
        /// Per-module feature-flag page (Settings → Features).
        /// Right now it hosts the Proforma Invoice field-visibility
        /// toggles (Steps 1-3). Future modules with hideable UI can
        /// plug into the same section so non-coders never need XAML
        /// edits for minor layout preferences.
        /// </summary>
        public bool IsFeaturesPage   => SettingsPage == "Features";

        /// <summary>Switches the active settings page (nav rail).</summary>
        public ICommand SwitchSettingsPageCommand { get; private set; }

        public string ConfigFilePath { get; }

    }
}
