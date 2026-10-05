// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsAiPageViewModel.Commands.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — AI page VM: commands + key-storage security
//
// Command declarations moved from the tail of SettingsViewModel.Ai.cs;
// provider test/select wiring moved from SettingsViewModel.InitializeCore;
// RemoveSavedKeys/ClearAiMemory wiring moved from
// SettingsViewModel.InitializeSecurityCommands (the data-safety commands
// LoadSampleData/ClearAllData stay on the shell for the Data page).
// KeepApiKeysSaved moved from SettingsViewModel.Security.cs.
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
    public sealed partial class SettingsAiPageViewModel
    {
        public ICommand TestGeminiCommand { get; private set; }
        public ICommand TestGroqCommand { get; private set; }
        public ICommand TestCohereCommand { get; private set; }

        public ICommand TestAiConnectionCommand => ActiveProvider switch
        {
            AiProvider.Gemini => TestGeminiCommand,
            AiProvider.Groq => TestGroqCommand,
            AiProvider.Cohere => TestCohereCommand,
            _ => TestGeminiCommand
        };

        public ICommand SelectGeminiCommand { get; private set; }
        public ICommand SelectGroqCommand { get; private set; }
        public ICommand SelectCohereCommand { get; private set; }

        public ICommand RemoveSavedKeysCommand { get; private set; }
        public ICommand ClearAiMemoryCommand { get; private set; }

        private void InitializeAiProviderCommands()
        {
            // ── Per-provider test commands ─────────────────────────────
            TestGeminiCommand = new RelayCommand(async _ =>
            {
                GeminiTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Gemini, _config.Ai);
                var result = await svc.TestConnectionAsync();
                GeminiTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            TestGroqCommand = new RelayCommand(async _ =>
            {
                GroqTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Groq, _config.Ai);
                var result = await svc.TestConnectionAsync();
                GroqTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            TestCohereCommand = new RelayCommand(async _ =>
            {
                CohereTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Cohere, _config.Ai);
                var result = await svc.TestConnectionAsync();
                CohereTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            // ── Provider selection ─────────────────────────────────────
            SelectGeminiCommand = new RelayCommand(_ =>
                ActiveProvider = AiProvider.Gemini);
            SelectGroqCommand = new RelayCommand(_ =>
                ActiveProvider = AiProvider.Groq);
            SelectCohereCommand = new RelayCommand(_ =>
                ActiveProvider = AiProvider.Cohere);
        }

        private void InitializeAiSecurityCommands()
        {
            RemoveSavedKeysCommand = new RelayCommand(_ =>
            {
                var confirm = _dialogs.Show(
                    LocalizationService.Instance.T("Msg.RemoveAllSavedAPIKeys"),
                    LocalizationService.Instance.T("Settings.RemoveSavedKeys"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                Services.Security.SecretVault.RemoveAll();

                if (_config?.Ai != null)
                {
                    _config.Ai.GeminiApiKey = "";
                    _config.Ai.GroqApiKey = "";
                    _config.Ai.CohereApiKey = "";
                    SaveConfig();
                }

                OnPropertyChanged(nameof(GeminiApiKey));
                OnPropertyChanged(nameof(GroqApiKey));
                OnPropertyChanged(nameof(CohereApiKey));

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "API keys removed",
                    "All stored AI keys deleted from device");
            });

                        ClearAiMemoryCommand = new RelayCommand(async _ =>
            {
                await Services.AI.AIMemory.AiMemoryService.ClearAsync();
                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "AI memory cleared", "");
            });

        }
    }
}
