// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsSharingPageViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — per-page ViewModel PILOT (phase 3b)
//
// First page promoted out of the SettingsViewModel god class. The Sharing
// page was chosen because its members are referenced by exactly one section
// (SettingsSharingSection.xaml) and no other partial: a clean seam.
//
// Contract: the shell exposes this as SettingsViewModel.SharingPage and the
// page template sets the section's DataContext to it, so every binding inside
// the section keeps its exact property names. Shared state arrives through
// the constructor (AppConfig); writes go through ConfigService.SaveDebounced
// + AuditService exactly as before.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Sharing.Providers;
using ProGlassAutomation.Services.Sharing.Templates;
using ProGlassAutomation.Services.Sharing;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System;

namespace ProGlassAutomation.ViewModels.Settings.Pages
{
    /// <summary>Sharing Center page state: providers, defaults, document
    /// rules and message templates. Owned by the shell, scoped to the page.</summary>
    public sealed partial class SettingsSharingPageViewModel : ViewModelBase
    {
        private readonly AppConfig _config;

        public SettingsSharingPageViewModel(AppConfig config)
        {
            _config = config;
        }
        private SharingSettings SharingCfg => _config.Sharing;

        private void SaveSharing(string action, string details)
        {
            ConfigService.SaveDebounced(_config);
            AuditService.Log(AuditSections.Settings, action, details);
        }

        // ── Providers ──────────────────────────────────────────────

        public bool SharingWhatsAppEnabled
        {
            get => SharingCfg.IsProviderEnabled(SharingProviderIds.WhatsApp);
            set { SetProviderEnabled(SharingProviderIds.WhatsApp, value); }
        }

        public bool SharingEmailEnabled
        {
            get => SharingCfg.IsProviderEnabled(SharingProviderIds.Email);
            set { SetProviderEnabled(SharingProviderIds.Email, value); }
        }

        public bool SharingSystemFileEnabled
        {
            get => SharingCfg.IsProviderEnabled(SharingProviderIds.SystemFile);
            set { SetProviderEnabled(SharingProviderIds.SystemFile, value); }
        }

        private void SetProviderEnabled(string providerId, bool enabled)
        {
            var list = SharingCfg.EnabledProviders ??= new List<string>();
            bool has = list.Any(id =>
                string.Equals(id?.Trim(), providerId, StringComparison.OrdinalIgnoreCase));
            if (enabled && !has)
                list.Add(providerId);
            else if (!enabled && has)
                list.RemoveAll(id =>
                    string.Equals(id?.Trim(), providerId, StringComparison.OrdinalIgnoreCase));

            // The last provider cannot be switched off — an empty enabled
            // list would strand the composer with no way to share.
            if (list.Count == 0)
                list.Add(providerId);

            SaveSharing("Sharing provider toggled",
                $"{providerId} {(enabled ? "enabled" : "disabled")}");
            OnPropertyChanged(nameof(SharingWhatsAppEnabled));
            OnPropertyChanged(nameof(SharingEmailEnabled));
            OnPropertyChanged(nameof(SharingSystemFileEnabled));
        }

        public string[] SharingProviderOptions { get; } =
        {
            SharingProviderIds.WhatsApp,
            SharingProviderIds.Email,
            SharingProviderIds.SystemFile
        };

        public string SharingDefaultProvider
        {
            get => SharingCfg.DefaultProviderId ?? SharingProviderIds.WhatsApp;
            set
            {
                SharingCfg.DefaultProviderId = string.IsNullOrWhiteSpace(value)
                    ? SharingProviderIds.WhatsApp : value.Trim().ToUpperInvariant();
                SaveSharing("Sharing default provider changed",
                    SharingCfg.DefaultProviderId);
                OnPropertyChanged();
            }
        }

        // ── Defaults ───────────────────────────────────────────────

        public string SharingDefaultCountryCode
        {
            get => SharingCfg.DefaultCountryCode ?? "971";
            set
            {
                string cc = (value ?? "").Trim().TrimStart('+');
                var digits = new string(cc.Where(char.IsDigit).ToArray());
                if (digits.Length == 0) digits = "971";
                if (digits.Length > 4) digits = digits.Substring(0, 4);
                SharingCfg.DefaultCountryCode = digits;
                SaveSharing("Sharing country code changed", digits);
                OnPropertyChanged();
            }
        }

        public string SharingTempRetentionHours
        {
            get => SharingCfg.TempRetentionHours.ToString();
            set
            {
                if (int.TryParse((value ?? "").Trim(), out int hours))
                {
                    hours = Math.Clamp(hours, 1, 720);
                    SharingCfg.TempRetentionHours = hours;
                    SaveSharing("Sharing temp retention changed",
                        hours + "h");
                }
                OnPropertyChanged();
            }
        }

        public string SharingEmailLargeMb
        {
            get => SharingCfg.EmailLargeAttachmentMb.ToString();
            set
            {
                if (int.TryParse((value ?? "").Trim(), out int mb))
                {
                    mb = Math.Clamp(mb, 1, 2000);
                    SharingCfg.EmailLargeAttachmentMb = mb;
                    SaveSharing("Sharing email warning threshold changed",
                        mb + " MB");
                }
                OnPropertyChanged();
            }
        }

        // ── Document rules ─────────────────────────────────────────

        public ObservableCollection<ShareDocumentRule> SharingDocumentRules { get; }
            = new();

        public string[] SharingFormatOptions { get; } = { "Pdf", "Excel", "Csv" };

        public string[] SharingRecipientKindOptions { get; } =
        {
            "CustomerAttention", "Customer",
            "CompanyRepresentative", "Salesman"
        };

        private void LoadSharingDocumentRules()
        {
            try
            {
                SharingDocumentRules.Clear();
                foreach (var rule in SharingCfg.DocumentRules)
                    SharingDocumentRules.Add(rule);
                _sharingRulesSignature = SharingRulesSignature();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Settings.Sharing] rules load failed: {ex.Message}");
            }
        }

        private string _sharingRulesSignature = "";

        private string SharingRulesSignature() =>
            string.Join(";", SharingDocumentRules.Select(r =>
                $"{r.DocumentTypeKey}={r.DefaultFormat}," +
                $"{r.DefaultProviderId},{r.DefaultRecipientKind}," +
                $"{r.AllowMessageEdit}"));

        /// <summary>
        /// Called by the section when a rule combo changes (rules are
        /// mutable objects bound directly; this persists + audits).
        /// Signature-guarded: initial binding fires the same events, and
        /// re-saving identical values would spam the audit on page open.
        /// </summary>
        public void SaveSharingDocumentRules()
        {
            string sig = SharingRulesSignature();
            if (sig == _sharingRulesSignature) return;
            _sharingRulesSignature = sig;
            SaveSharing("Sharing document rules changed",
                SharingDocumentRules.Count + " rules");
        }

    }
}
