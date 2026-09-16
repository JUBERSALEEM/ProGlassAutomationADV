// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsAiPageViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — per-page ViewModel (phase 3b, promotion #4)
//
// AI Assistant page promoted out of the god class after Sharing, Voice and
// Updates. Core state moved verbatim from SettingsViewModel.Ai.cs plus the
// KeepApiKeysSaved property from SettingsViewModel.Security.cs; command
// declarations and wiring live in the .Commands.cs partial.
//
// _ai mirrors the shell's original dead-store field (assigned after key
// saves, never read — same pattern as base commit 3baa39e, kept verbatim).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Fonts;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System;

namespace ProGlassAutomation.ViewModels.Settings.Pages
{
    /// <summary>AI Assistant page state: enable/model/params, per-provider
    /// API keys, test + select commands and API-key storage policy.</summary>
    public sealed partial class SettingsAiPageViewModel : ViewModelBase
    {
        private readonly AppConfig? _config;
        private readonly IDialogService? _dialogs;

        /// <summary>CS0103 fix: bus the promoted ActiveProvider setter raises.</summary>
        private readonly Services.AI.IAiProviderEvents _providerEvents;

        private IAiService? _ai;

        private string _geminiTestStatus = string.Empty;
        private string _groqTestStatus = string.Empty;
        private string _cohereTestStatus = string.Empty;
        private bool _apiKeyJustSaved;

        public SettingsAiPageViewModel(AppConfig? config, IDialogService? dialogs,
            Services.AI.IAiProviderEvents? providerEvents = null)
        {
            _config = config;
            _dialogs = dialogs;
            _providerEvents = providerEvents ?? new Services.AI.AiProviderEvents();
            InitializeAiProviderCommands();
            InitializeAiSecurityCommands();
        }

        private void SaveConfig()
        {
            if (_config != null)
                ConfigService.SaveDebounced(_config);
        }

        /// <summary>Same value the shell exposes (AiProvidersCard shows the
        /// config file location) — computed from the identical static path,
        /// so the card keeps working against the page-scoped DataContext.</summary>
        public string ConfigFilePath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "config.json");

        // ══════════════════════════════════════════════════════════════
        //  AI MASTER TOGGLE
        // ══════════════════════════════════════════════════════════════

        public bool IsAiEnabled
        {
            get => _config?.Ai?.Enabled ?? false;
            set
            {
                if (_config?.Ai != null)
                {
                    _config.Ai.Enabled = value;
                    SaveConfig();
                    OnPropertyChanged();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ACTIVE PROVIDER
        // ══════════════════════════════════════════════════════════════

        public AiProvider ActiveProvider
        {
            get => _config?.Ai?.ActiveProvider ?? AiProvider.Gemini;
            set
            {
                if (_config?.Ai != null &&
                    _config.Ai.ActiveProvider != value)
                {
                    _config.Ai.ActiveProvider = value;
                    SaveConfig();
                    _ai = AiProviderFactory.CreateActive(_config.Ai);
                    _providerEvents.RaiseChanged();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGeminiActive));
                    OnPropertyChanged(nameof(IsGroqActive));
                    OnPropertyChanged(nameof(IsCohereActive));
                    OnPropertyChanged(nameof(AiModel));
                    OnPropertyChanged(nameof(ActiveProviderName));
                }
            }
        }

        public bool IsGeminiActive => ActiveProvider == AiProvider.Gemini;
        public bool IsGroqActive => ActiveProvider == AiProvider.Groq;
        public bool IsCohereActive => ActiveProvider == AiProvider.Cohere;

        public string ActiveProviderName => ActiveProvider switch
        {
            AiProvider.Gemini => "Google Gemini",
            AiProvider.Groq => "Groq",
            AiProvider.Cohere => "Cohere",
            _ => "Unknown"
        };

        // ══════════════════════════════════════════════════════════════
        //  API KEYS
        // ══════════════════════════════════════════════════════════════

        public string GeminiApiKey
        {
            get => _config?.Ai?.GeminiApiKey ?? string.Empty;
            set
            {
                if (_config?.Ai != null &&
                    _config.Ai.GeminiApiKey != value)
                {
                    _config.Ai.GeminiApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGeminiConfigured));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public string GroqApiKey
        {
            get => _config?.Ai?.GroqApiKey ?? string.Empty;
            set
            {
                if (_config?.Ai != null &&
                    _config.Ai.GroqApiKey != value)
                {
                    _config.Ai.GroqApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGroqConfigured));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public string CohereApiKey
        {
            get => _config?.Ai?.CohereApiKey ?? string.Empty;
            set
            {
                if (_config?.Ai != null &&
                    _config.Ai.CohereApiKey != value)
                {
                    _config.Ai.CohereApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCohereConfigured));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public string AiApiKey
        {
            get => _config?.Ai?.ApiKey ?? string.Empty;
            set
            {
                if (_config?.Ai != null)
                {
                    _config.Ai.ApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSavedApiKey));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public bool IsGeminiConfigured =>
            !string.IsNullOrWhiteSpace(_config?.Ai?.GeminiApiKey);
        public bool IsGroqConfigured =>
            !string.IsNullOrWhiteSpace(_config?.Ai?.GroqApiKey);
        public bool IsCohereConfigured =>
            !string.IsNullOrWhiteSpace(_config?.Ai?.CohereApiKey);
        public bool HasSavedApiKey =>
            !string.IsNullOrWhiteSpace(_config?.Ai?.ApiKey);

        /// <summary>
        /// Whether saved API keys persist between sessions. Backs the
        /// "Keep keys saved" switch in Views/Settings/Sections/Cards/
        /// AiSecurityCard.xaml, which binds it TwoWay.
        ///
        /// RESTORED 2026-09-16 - this property was lost in the phase-3b
        /// promotion out of the god class. Both this file's header and
        /// SettingsAiPageViewModel.Commands.cs record that it "moved from
        /// SettingsViewModel.Security.cs", but no declaration survived the
        /// move: repo-wide the name existed only in those two comments and
        /// in a SettingsRedesignTests InlineData string. The switch
        /// therefore read nothing and wrote nowhere - WPF fails a TwoWay
        /// binding to a missing property silently, so it animated, appeared
        /// to work, and persisted no preference. The test did not catch it
        /// because it asserts the XAML CONTAINS the binding text, never that
        /// a member backs it.
        ///
        /// The underlying flag was never missing: AiSettings.RememberApiKeys
        /// (default true), honoured by ConfigService.PersistKeysToVault,
        /// which keeps keys session-only when it is false. Default matches
        /// the model so the first frame reads the same value the config
        /// already holds.
        /// </summary>
        public bool KeepApiKeysSaved
        {
            get => _config?.Ai?.RememberApiKeys ?? true;
            set
            {
                if (_config?.Ai != null &&
                    _config.Ai.RememberApiKeys != value)
                {
                    _config.Ai.RememberApiKeys = value;
                    SaveConfig();
                    OnPropertyChanged();
                }
            }
        }

        public bool ApiKeyJustSaved
        {
            get => _apiKeyJustSaved;
            set
            {
                if (_apiKeyJustSaved == value) return;
                _apiKeyJustSaved = value;
                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  TEST STATUS
        // ══════════════════════════════════════════════════════════════

        public string GeminiTestStatus
        {
            get => _geminiTestStatus;
            set
            {
                if (_geminiTestStatus == value) return;
                _geminiTestStatus = value;
                OnPropertyChanged();
            }
        }

        public string GroqTestStatus
        {
            get => _groqTestStatus;
            set
            {
                if (_groqTestStatus == value) return;
                _groqTestStatus = value;
                OnPropertyChanged();
            }
        }

        public string CohereTestStatus
        {
            get => _cohereTestStatus;
            set
            {
                if (_cohereTestStatus == value) return;
                _cohereTestStatus = value;
                OnPropertyChanged();
            }
        }

        public string AiTestStatus
        {
            get => ActiveProvider switch
            {
                AiProvider.Gemini => _geminiTestStatus,
                AiProvider.Groq => _groqTestStatus,
                AiProvider.Cohere => _cohereTestStatus,
                _ => string.Empty
            };
            set
            {
                switch (ActiveProvider)
                {
                    case AiProvider.Gemini: GeminiTestStatus = value; break;
                    case AiProvider.Groq: GroqTestStatus = value; break;
                    case AiProvider.Cohere: CohereTestStatus = value; break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  RAG TOGGLE
        // ══════════════════════════════════════════════════════════════

        public bool UseRealData
        {
            get => _config?.Ai?.UseRealData ?? false;
            set
            {
                if (_config?.Ai == null) return;

                // Consent gate — P-1. Enabling real-data access is a privacy
                // decision: a real-time summary containing real customer names,
                // project names and AED totals will leave the machine to the
                // configured AI provider (Gemini / Groq / Cohere). It is opt-in,
                // so turning it ON must require an explicit confirmation. The
                // toggle stays off unless the user confirms.
                if (value && !_config.Ai.UseRealData)
                {
                    var confirm = _dialogs.Show(
                        "Turning on \"Use Real Business Data\" sends a live summary " +
                        "of your invoices, job orders, deliveries, tax invoices and " +
                        "balances — including real customer names, project names and " +
                        "AED totals — over the internet to your AI provider " +
                        "(Gemini, Groq or Cohere) as part of every chat message.\n\n" +
                        "Those companies process the data under their own privacy " +
                        "terms. Turn it off to keep the assistant to general " +
                        "questions only.\n\nDo you want to allow this?",
                        "Allow AI to access your business data?",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No);

                    if (confirm != System.Windows.MessageBoxResult.Yes)
                    {
                        // User declined — keep it off and reflect that on the toggle.
                        OnPropertyChanged(nameof(UseRealData));
                        return;
                    }
                }

                _config.Ai.UseRealData = value;
                SaveConfig();
                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  MODEL & TUNING
        // ══════════════════════════════════════════════════════════════

        public string AiModel => _config?.Ai?.GetModelForProvider(ActiveProvider) ?? "—";
        public int AiMaxTokens => _config?.Ai?.MaxTokens ?? 1024;
        public double AiTemperature => _config?.Ai?.Temperature ?? 0.7;

        // ══════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════
    }
}
