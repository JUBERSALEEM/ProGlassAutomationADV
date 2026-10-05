// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsAiPageViewModel.Tuning.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — per-page ViewModel (AI Tuning & Status partial)
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;

namespace ProGlassAutomation.ViewModels.Settings.Pages
{
    public sealed partial class SettingsAiPageViewModel
    {
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

                if (value && !_config.Ai.UseRealData)
                {
                    var confirm = _dialogs?.Show(
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
                        System.Windows.MessageBoxResult.No) ?? System.Windows.MessageBoxResult.No;

                    if (confirm != System.Windows.MessageBoxResult.Yes)
                    {
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
    }
}
