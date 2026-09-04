// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/UI/AgentSettingsViewModel.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Everything behind Settings → AI & Agent, in one VM that lives inside
// Agent/ so the agent subsystem stays self-contained. The only edits
// outside this folder are the nav-rail entry and the section host.
//
// UI PRINCIPLE: the privacy posture must be readable at a glance and
// impossible to change by accident. Local-Only is a single explicit
// toggle, cloud consent is a SECOND explicit toggle, and turning
// Local-Only back on revokes consent.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Agent.Configuration;
using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Models;
using ProGlassAutomation.Agent.Rules;
using ProGlassAutomation.Agent.Security.Network;
using ProGlassAutomation.Agent.Security.SecretProtection;
using ProGlassAutomation.Agent.Storage;
using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.UI
{
    /// <summary>One taught rule as shown in the Settings list.</summary>
    public sealed class TaughtRuleRow : ViewModelBase
    {
        public int Id { get; set; }
        public string Trigger { get; set; } = "";
        public string Action { get; set; } = "";
        public bool IsApproved { get; set; }
        public string ApprovedBy { get; set; } = "";

        public string Display => $"#{Id}  \"{Trigger}\"  →  {Action}";

        public string StatusDisplay => IsApproved
            ? $"Active (approved by {(string.IsNullOrWhiteSpace(ApprovedBy) ? "user" : ApprovedBy)})"
            : "Waiting for approval — does nothing yet";
    }

    /// <summary>Backing VM for the AI &amp; Agent settings page.</summary>
    public sealed class AgentSettingsViewModel : ViewModelBase
    {
        public AgentSettingsViewModel()
        {
            ProviderOptions = new ObservableCollection<string>(
                AgentSecretStore.KnownProviders);

            ToggleLocalOnlyCommand = new RelayCommand(_ => ToggleLocalOnly());
            ToggleCloudConsentCommand = new RelayCommand(_ => ToggleCloudConsent());
            RemoveKeyCommand = new RelayCommand(_ => RemoveKey());
            RefreshRulesCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(RefreshRulesAsync, "AgentSettings RefreshRules"));
            ApproveRuleCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => ApproveAsync(p), "AgentSettings Approve"));
            RevokeRuleCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => RevokeAsync(p), "AgentSettings Revoke"));
            ClearTeachingCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ClearTeachingAsync, "AgentSettings ClearTeaching"));
            ClearMemoryCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ClearMemoryAsync, "AgentSettings ClearMemory"));
            ClearKnowledgeCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ClearKnowledgeAsync, "AgentSettings ClearKnowledge"));
            ExportKnowledgeCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExportAsync, "AgentSettings Export"));
            ImportKnowledgeCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ImportAsync, "AgentSettings Import"));

            var advice = LocalModelAdvisor.Advise();
            HardwareAdvice = advice.Advice;

            AllowedHostsDisplay = NetworkWhitelist.Describe();
            AgentDatabasePath = AgentDbContext.DatabaseFilePath;
            SettingsFilePath = AgentConfigurationService.SettingsFilePath;

            RefreshPrivacyState();

            // ══════════════════════════════════════════════════════
            //  LOCAL MODEL (one-click offline installer)
            // ══════════════════════════════════════════════════════

            var hw = HardwareProbe.Read();
            var recommended = LocalModelCatalog.RecommendFor(hw.TotalMemoryGb);

            ModelRows = new ObservableCollection<ModelRow>(
                LocalModelCatalog.All.Select(e => new ModelRow
                {
                    Id = e.Id,
                    Title = e.Title,
                    Description = e.Description,
                    SizeText = $"{e.SizeMb:0} MB",
                    RamText = $"Needs ~{e.MinRamGb}+ GB RAM free",
                    LicenseName = e.LicenseName,
                    LicenseUrl = e.LicenseUrl,
                    IsRecommended = e.Id == recommended.Id,
                    CatalogEntry = e
                }));

            _selectedModel =
                ModelRows.FirstOrDefault(r => r.Id == recommended.Id)
                ?? ModelRows[0];

            _isLocalModelEnabled =
                AgentConfigurationService.Current.LocalModelEnabled;

            RefreshModelStatus();
            OnPropertyChanged(nameof(SelectedModel));
            OnPropertyChanged(nameof(IsLocalModelEnabled));

            InstallModelCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(InstallSelectedModelAsync, "LocalModel Install"),
                _ => !IsModelBusy);

            CancelInstallCommand = new RelayCommand(
                _ => _installCts?.Cancel(),
                _ => IsModelBusy);

            UninstallModelCommand = new RelayCommand(
                _ => UninstallSelectedModel(),
                _ => !IsModelBusy);

            VerifyModelCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(VerifySelectedModelAsync, "LocalModel Verify"),
                _ => !IsModelBusy);

            ToggleLocalModelCommand = new RelayCommand(
                _ => ToggleLocalModelEnabled(),
                _ => !IsModelBusy);
        }

        // ══════════════════════════════════════════════════════════════
        //  PRIVACY
        // ══════════════════════════════════════════════════════════════

        private bool _localOnly = true;
        /// <summary>True when the agent may not use the network at all.</summary>
        public bool LocalOnly
        {
            get => _localOnly;
            private set
            {
                if (_localOnly == value) return;
                _localOnly = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LocalOnlyLabel));
                OnPropertyChanged(nameof(PrivacySummary));
            }
        }

        public string LocalOnlyLabel => LocalOnly
            ? "LOCAL ONLY — ON"
            : "LOCAL ONLY — OFF";

        private bool _cloudConsent;
        /// <summary>Separate, explicit consent to use a cloud provider.</summary>
        public bool CloudConsent
        {
            get => _cloudConsent;
            private set
            {
                if (_cloudConsent == value) return;
                _cloudConsent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CloudConsentLabel));
                OnPropertyChanged(nameof(PrivacySummary));
            }
        }

        public string CloudConsentLabel => CloudConsent
            ? "CLOUD CONSENT — GIVEN"
            : "CLOUD CONSENT — NOT GIVEN";

        /// <summary>Plain-English statement of what happens to your data.</summary>
        public string PrivacySummary => LocalOnly
            ? "Nothing leaves this computer. The agent answers from its built-in " +
              "knowledge base and from your own data on this machine."
            : CloudConsent
                ? "The agent may send your question to the selected online provider. " +
                  "It never sends stored API keys, and it blocks anything that still " +
                  "looks like a secret."
                : "Local-Only is off, but you have not consented to cloud use, so the " +
                  "agent is still answering locally.";

        /// <summary>Hardware-based local model advice (never downloads).</summary>
        public string HardwareAdvice { get; }

        /// <summary>Hosts the agent is permitted to contact.</summary>
        public string AllowedHostsDisplay { get; }

        public string AgentDatabasePath { get; }
        public string SettingsFilePath { get; }

        // ══════════════════════════════════════════════════════════════
        //  CREDENTIALS
        // ══════════════════════════════════════════════════════════════

        /// <summary>gemini | groq | cohere.</summary>
        public ObservableCollection<string> ProviderOptions { get; }

        private string _selectedProvider = "gemini";
        public string SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (_selectedProvider == value) return;
                _selectedProvider = value ?? "gemini";
                OnPropertyChanged();
                OnPropertyChanged(nameof(KeyStatus));

                AgentConfigurationService.SetCloudProvider(_selectedProvider);
                AgentHost.Invalidate();
            }
        }

        /// <summary>Whether a key is stored for the selected provider.</summary>
        public string KeyStatus => AgentSecretStore.HasSecret(SelectedProvider)
            ? $"A key is stored for '{SelectedProvider}' (encrypted with Windows DPAPI)."
            : $"No key stored for '{SelectedProvider}'.";

        /// <summary>
        /// Called by the code-behind with the raw PasswordBox value —
        /// secrets never live in a bindable VM property.
        /// </summary>
        public void StoreKey(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                Status = "Enter a key first.";
                return;
            }

            bool ok = AgentSecretStore.Store(SelectedProvider, secret);

            Status = ok
                ? $"Key stored for '{SelectedProvider}'."
                : "Could not store that key.";

            OnPropertyChanged(nameof(KeyStatus));
            AgentHost.Invalidate();
        }

        private void RemoveKey()
        {
            AgentSecretStore.Remove(SelectedProvider);
            Status = $"Key removed for '{SelectedProvider}'.";
            OnPropertyChanged(nameof(KeyStatus));
            AgentHost.Invalidate();
        }

        // ══════════════════════════════════════════════════════════════
        //  TAUGHT RULES
        // ══════════════════════════════════════════════════════════════

        /// <summary>Every taught rule, approved or not.</summary>
        public ObservableCollection<TaughtRuleRow> Rules { get; } = new();

        /// <summary>True when at least one rule exists.</summary>
        public bool HasRules => Rules.Count > 0;

        // ══════════════════════════════════════════════════════════════
        //  STATUS
        // ══════════════════════════════════════════════════════════════

        private string _status = "";
        public string Status
        {
            get => _status;
            private set
            {
                if (_status == value) return;
                _status = value ?? "";
                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  LOCAL MODEL PICKER (one-click offline installer)
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<ModelRow> ModelRows { get; }

        private ModelRow? _selectedModel;
        public ModelRow? SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (_selectedModel == value) return;
                _selectedModel = value;
                OnPropertyChanged();
            }
        }

        private bool _isLocalModelEnabled;
        public bool IsLocalModelEnabled
        {
            get => _isLocalModelEnabled;
            private set
            {
                if (_isLocalModelEnabled == value) return;
                _isLocalModelEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isModelBusy;
        public bool IsModelBusy
        {
            get => _isModelBusy;
            private set
            {
                if (_isModelBusy == value) return;
                _isModelBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModelBusyText));
                AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        private double _modelProgress;
        public double ModelProgress
        {
            get => _modelProgress;
            private set
            {
                if (System.Math.Abs(_modelProgress - value) < 0.05) return;
                _modelProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModelBusyText));
            }
        }

        public string ModelBusyText =>
            IsModelBusy ? $"Downloading… {_modelProgress:0.0}%" : "";

        public ICommand InstallModelCommand { get; }
        public ICommand CancelInstallCommand { get; }
        public ICommand UninstallModelCommand { get; }
        public ICommand VerifyModelCommand { get; }
        public ICommand ToggleLocalModelCommand { get; }

        private CancellationTokenSource? _installCts;

        private void RefreshModelStatus()
        {
            foreach (var row in ModelRows)
            {
                row.IsInstalled = LocalModelManager.IsInstalled(row.Id, out _);
                row.StatusText = row.IsInstalled ? "ready to load when the agent answers." : "not yet downloaded.";
            }
        }

        private async Task InstallSelectedModelAsync()
        {
            if (SelectedModel == null) return;

            IsModelBusy = true;
            ModelProgress = 0;
            _installCts?.Dispose();
            _installCts = new CancellationTokenSource();

            try
            {
                bool ok = await LocalModelManager.InstallAsync(
                    SelectedModel.CatalogEntry,
                    new Progress<double>(v => ModelProgress = v),
                    _installCts.Token);

                RefreshModelStatus();
                IsLocalModelEnabled =
                    AgentConfigurationService.Current.LocalModelEnabled;

                Status = ok
                    ? $"Installed {SelectedModel.Title}. The local agent now " +
                      "answers offline from its own model."
                    : $"Could not install {SelectedModel.Title} — see the log " +
                      "for why (disk space, network, or a server hiccup).";
            }
            catch (OperationCanceledException)
            {
                RefreshModelStatus();
                Status = $"Download of {SelectedModel.Title} cancelled — no " +
                         "partial file left behind.";
            }
            finally
            {
                IsModelBusy = false;
                _installCts?.Dispose();
                _installCts = null;
            }
        }

        private void UninstallSelectedModel()
        {
            if (SelectedModel == null) return;

            string? wasEnabled =
                AgentConfigurationService.Current.LocalModelId;

            bool ok = LocalModelManager.Uninstall(SelectedModel.Id);

            if (ok && wasEnabled == SelectedModel.Id)
            {
                // Keep privacy-first: wiping the model flips the chain back
                // to the curated knowledge base immediately.
                AgentConfigurationService.SetLocalModel("", false);
                IsLocalModelEnabled = false;
            }

            RefreshModelStatus();
            Status = ok
                ? $"Removed {SelectedModel.Title}."
                : $"Could not remove {SelectedModel.Title}.";
        }

        private async Task VerifySelectedModelAsync()
        {
            if (SelectedModel == null) return;

            IsModelBusy = true;
            try
            {
                var (ok, message) = await LocalModelManager.VerifyAsync(
                    SelectedModel.CatalogEntry, CancellationToken.None);
                Status = message;
            }
            finally
            {
                IsModelBusy = false;
            }
        }

        private void ToggleLocalModelEnabled()
        {
            bool next = !IsLocalModelEnabled;

            if (next &&
                !LocalModelManager.IsInstalled(
                    AgentConfigurationService.Current.LocalModelId, out _))
            {
                Status = "Install a model first — enabling has nothing to load.";
                return;
            }

            AgentConfigurationService.SetLocalModel(
                AgentConfigurationService.Current.LocalModelId, next);

            IsLocalModelEnabled = next;
            AgentHost.Invalidate();   // rebuild on the next turn

            Status = next
                ? "Local model enabled. The answers stay fully offline."
                : "Local model disabled — answering from the knowledge base.";
        }

        // ══════════════════════════════════════════════════════════════
        //  ORIGINAL COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand ToggleLocalOnlyCommand { get; }
        public ICommand ToggleCloudConsentCommand { get; }
        public ICommand RemoveKeyCommand { get; }
        public ICommand RefreshRulesCommand { get; }
        public ICommand ApproveRuleCommand { get; }
        public ICommand RevokeRuleCommand { get; }
        public ICommand ClearTeachingCommand { get; }
        public ICommand ClearMemoryCommand { get; }
        public ICommand ClearKnowledgeCommand { get; }
        public ICommand ExportKnowledgeCommand { get; }
        public ICommand ImportKnowledgeCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  ACTIONS
        // ══════════════════════════════════════════════════════════════

        private void RefreshPrivacyState()
        {
            var settings = AgentConfigurationService.Current;
            LocalOnly = settings.LocalOnly;
            CloudConsent = settings.CloudConsentGiven;
            _selectedProvider = string.IsNullOrWhiteSpace(settings.CloudProvider)
                ? "gemini" : settings.CloudProvider;
            OnPropertyChanged(nameof(SelectedProvider));
            OnPropertyChanged(nameof(KeyStatus));
        }

        private void ToggleLocalOnly()
        {
            bool next = !LocalOnly;
            AgentConfigurationService.SetLocalOnly(next);

            AgentHost.Ensure();
            AgentHost.Audit?.Security(
                Services.Audit.AuditService.CurrentUsername,
                "Agent local-only changed",
                next ? "Local-Only turned ON (cloud consent cleared)"
                     : "Local-Only turned OFF");

            AgentHost.Invalidate();
            RefreshPrivacyState();

            Status = next
                ? "Local-Only is on. Cloud consent was cleared."
                : "Local-Only is off. You must also give cloud consent.";
        }

        private void ToggleCloudConsent()
        {
            if (LocalOnly)
            {
                Status = "Turn Local-Only off first.";
                return;
            }

            bool next = !CloudConsent;
            AgentConfigurationService.SetCloudConsent(next);

            AgentHost.Ensure();
            AgentHost.Audit?.Security(
                Services.Audit.AuditService.CurrentUsername,
                "Agent cloud consent changed",
                next ? "consent GIVEN" : "consent WITHDRAWN");

            AgentHost.Invalidate();
            RefreshPrivacyState();

            Status = next
                ? "Cloud use consented."
                : "Cloud consent withdrawn — answering locally.";
        }

        

        /// <summary>Reloads the taught-rule list. Never throws.</summary>
        public async Task RefreshRulesAsync()
        {
            try
            {
                AgentHost.Ensure();
                if (AgentHost.Teaching == null) return;

                var rules = await AgentHost.Teaching.AllRulesAsync()
                    .ConfigureAwait(true);

                Rules.Clear();
                foreach (var r in rules)
                {
                    Rules.Add(new TaughtRuleRow
                    {
                        Id = r.Id,
                        Trigger = r.Trigger,
                        Action = r.Action,
                        IsApproved = r.IsApproved,
                        ApprovedBy = r.ApprovedBy
                    });
                }

                OnPropertyChanged(nameof(HasRules));
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentSettings] refresh rules failed: {ex.Message}");
            }
        }

        private async Task ApproveAsync(object? param)
        {
            if (param is not TaughtRuleRow row) return;

            AgentHost.Ensure();
            if (AgentHost.Teaching == null) return;

            bool ok = await AgentHost.Teaching
                .ApproveAsync(row.Id, Services.Audit.AuditService.CurrentUsername)
                .ConfigureAwait(true);

            Status = ok ? $"Rule {row.Id} approved." : $"Could not approve rule {row.Id}.";
            await RefreshRulesAsync();
        }

        private async Task RevokeAsync(object? param)
        {
            if (param is not TaughtRuleRow row) return;

            AgentHost.Ensure();
            if (AgentHost.Teaching == null) return;

            bool ok = await AgentHost.Teaching.RevokeAsync(row.Id)
                .ConfigureAwait(true);

            Status = ok ? $"Rule {row.Id} revoked." : $"Could not revoke rule {row.Id}.";
            await RefreshRulesAsync();
        }

        private async Task ClearTeachingAsync()
        {
            AgentHost.Ensure();
            if (AgentHost.Teaching == null) return;

            await AgentHost.Teaching.ClearTeachingAsync().ConfigureAwait(true);
            Status = "All taught phrases removed.";
            await RefreshRulesAsync();
        }

        private async Task ClearMemoryAsync()
        {
            AgentHost.Ensure();
            if (AgentHost.Memory == null) return;

            AgentHost.Memory.ClearConversation();
            await AgentHost.Memory.ClearPersistedAsync().ConfigureAwait(true);
            Status = "Conversation and saved memory cleared.";
        }

        private async Task ClearKnowledgeAsync()
        {
            AgentHost.Ensure();
            if (AgentHost.Knowledge == null) return;

            await AgentHost.Knowledge.ClearAsync().ConfigureAwait(true);
            Status = "Knowledge base cleared. Reopen this page to reseed the basics.";
        }

        private async Task ExportAsync()
        {
            try
            {
                AgentHost.Ensure();
                if (AgentHost.Knowledge == null) return;

                var dialog = new SaveFileDialog
                {
                    Title = "Export agent knowledge",
                    Filter = "JSON file (*.json)|*.json",
                    FileName = "proglass-agent-knowledge.json"
                };

                if (dialog.ShowDialog() != true) return;

                string json = await AgentHost.Knowledge.ExportJsonAsync()
                    .ConfigureAwait(true);

                File.WriteAllText(dialog.FileName, json);
                Status = $"Knowledge exported to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentSettings] export failed: {ex.Message}");
                Status = "Export failed.";
            }
        }

        private async Task ImportAsync()
        {
            try
            {
                AgentHost.Ensure();
                if (AgentHost.Knowledge == null) return;

                var dialog = new OpenFileDialog
                {
                    Title = "Import agent knowledge",
                    Filter = "JSON file (*.json)|*.json"
                };

                if (dialog.ShowDialog() != true) return;

                string json = File.ReadAllText(dialog.FileName);

                int count = await AgentHost.Knowledge.ImportJsonAsync(json)
                    .ConfigureAwait(true);

                Status = count > 0
                    ? $"Imported {count} knowledge item(s)."
                    : "Nothing valid was found in that file.";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentSettings] import failed: {ex.Message}");
                Status = "Import failed.";
            }
        }
    }
}
