// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Security.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Security
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
        //  v2 — SECURITY (API key storage)
        //
        //  Keys are DPAPI-encrypted in the SecretVault, never plain
        //  text. Turning "keep keys" off makes them session-only.
        // ══════════════════════════════════════════════════════════════

        public bool KeepApiKeysSaved
        {
            get => _config?.Ai?.RememberApiKeys ?? true;
            set
            {
                if (_config == null ||
                    _config.Ai.RememberApiKeys == value)
                    return;

                _config.Ai.RememberApiKeys = value;
                SaveConfig();   // wipes the vault when value is false
                OnPropertyChanged();
            }
        }

        public ICommand RemoveSavedKeysCommand { get; private set; }
        public ICommand ClearAiMemoryCommand { get; private set; }

        /// <summary>Seeds the first-run demo dataset (all modules).</summary>
        public ICommand LoadSampleDataCommand { get; private set; }

        /// <summary>Wipes every record across all modules (e.g. after
        /// evaluating the sample data) so real data entry starts clean.</summary>
        public ICommand ClearAllDataCommand { get; private set; }

    }
}
