// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Ai.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Ai
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

        public ICommand TestGeminiCommand { get; }
        public ICommand TestGroqCommand { get; }
        public ICommand TestCohereCommand { get; }

        public ICommand TestAiConnectionCommand => ActiveProvider switch
        {
            AiProvider.Gemini => TestGeminiCommand,
            AiProvider.Groq => TestGroqCommand,
            AiProvider.Cohere => TestCohereCommand,
            _ => TestGeminiCommand
        };

        public ICommand SelectGeminiCommand { get; }
        public ICommand SelectGroqCommand { get; }
        public ICommand SelectCohereCommand { get; }

    }
}
