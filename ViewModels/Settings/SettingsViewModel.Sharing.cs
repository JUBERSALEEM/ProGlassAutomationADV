// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Sharing.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings — Sharing Center preferences (Stage 2)
//
// Edits AppConfig.Sharing in place (same pattern as the other settings
// partials): every setter writes through + ConfigService.SaveDebounced.
// Every control here drives working code — providers gate the composer,
// defaults seed it, document rules steer it, templates render it.
//
// Document rules bind directly to the SharingSettings.DocumentRules list
// (mutable rule objects); template overrides edit through the selected
// key + text + explicit Save (a dictionary has no per-key binding).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Sharing;
using ProGlassAutomation.Services.Sharing.Providers;
using ProGlassAutomation.Services.Sharing.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
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

    }
}
