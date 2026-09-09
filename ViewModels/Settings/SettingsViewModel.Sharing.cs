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

        // ── Templates ──────────────────────────────────────────────

        public string[] SharingTemplateKeyOptions { get; } =
        {
            "GLOBAL",
            "WHATSAPP", "EMAIL", "SYSTEMFILE",
            "PI", "JO", "DN", "TI", "Statement", "OrderReport", "Report",
            "Specifications", "SheetStore", "Inquiry",
            "PI+WHATSAPP", "TI+EMAIL", "Report+EMAIL"
        };

        private string _sharingTemplateKey = "GLOBAL";
        public string SharingTemplateKey
        {
            get => _sharingTemplateKey;
            set
            {
                _sharingTemplateKey = string.IsNullOrWhiteSpace(value)
                    ? "GLOBAL" : value.Trim().ToUpperInvariant();
                LoadSharingTemplateText();
                OnPropertyChanged();
            }
        }

        private string _sharingTemplateText = "";
        public string SharingTemplateText
        {
            get => _sharingTemplateText;
            set { _sharingTemplateText = value ?? ""; OnPropertyChanged(); }
        }

        public ICommand SaveSharingTemplateCommand { get; private set; } = null!;
        public ICommand ResetSharingTemplateCommand { get; private set; } = null!;

        private void LoadSharingTemplateText()
        {
            try
            {
                string? existing = null;
                var overrides = SharingCfg.TemplateOverrides;
                if (overrides != null)
                {
                    foreach (var pair in overrides)
                    {
                        if (string.Equals(pair.Key?.Trim(), _sharingTemplateKey,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            existing = pair.Value;
                            break;
                        }
                    }
                }

                SharingTemplateText = existing
                    ?? EffectiveBuiltInFor(_sharingTemplateKey);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Settings.Sharing] template load failed: {ex.Message}");
                SharingTemplateText = ShareTemplateService.BuiltInGlobal;
            }
        }

        private static string EffectiveBuiltInFor(string key)
        {
            // Shows what renders when no override exists: split DOC+PROV.
            string doc = key, prov = "";
            int plus = key.IndexOf('+');
            if (plus > 0)
            {
                doc = key.Substring(0, plus);
                prov = key.Substring(plus + 1);
            }
            if (string.Equals(doc, "GLOBAL", StringComparison.OrdinalIgnoreCase))
                doc = "";
            if (prov.Length > 0 && doc.Length == 0)
            {
                // Provider-only key with no override: the global built-in
                // is what renders (provider has no built-in of its own).
                return ShareTemplateService.BuiltInGlobal;
            }
            return ShareTemplateService.ResolveTemplateText(doc, prov, null);
        }

        private void InitSharingTemplateCommands()
        {
            SaveSharingTemplateCommand = new RelayCommand(_ =>
            {
                try
                {
                    var overrides = SharingCfg.TemplateOverrides ??= new();
                    string? match = null;
                    foreach (var pair in overrides)
                    {
                        if (string.Equals(pair.Key?.Trim(), _sharingTemplateKey,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            match = pair.Key;
                            break;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(SharingTemplateText))
                    {
                        // Blank = remove the override (built-in renders).
                        if (match != null)
                            overrides.Remove(match);
                    }
                    else if (match != null)
                    {
                        overrides[match] = SharingTemplateText;
                    }
                    else
                    {
                        overrides[_sharingTemplateKey] = SharingTemplateText;
                    }

                    SaveSharing("Sharing template saved", _sharingTemplateKey);
                    LoadSharingTemplateText();
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[Settings.Sharing] template save failed: {ex.Message}");
                }
            });

            ResetSharingTemplateCommand = new RelayCommand(_ =>
            {
                try
                {
                    var overrides = SharingCfg.TemplateOverrides;
                    if (overrides != null)
                    {
                        string? match = null;
                        foreach (var pair in overrides)
                        {
                            if (string.Equals(pair.Key?.Trim(), _sharingTemplateKey,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                match = pair.Key;
                                break;
                            }
                        }
                        if (match != null)
                            overrides.Remove(match);
                    }

                    SaveSharing("Sharing template reset", _sharingTemplateKey);
                    LoadSharingTemplateText();
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[Settings.Sharing] template reset failed: {ex.Message}");
                }
            });
        }

        private bool _sharingInitDone;

        /// <summary>
        /// Lazily initializes the sharing editors (rules list + template
        /// commands + first template text). Called from the section's load.
        /// </summary>
        public void EnsureSharingInit()
        {
            if (_sharingInitDone) return;
            _sharingInitDone = true;
            LoadSharingDocumentRules();
            InitSharingTemplateCommands();
            LoadSharingTemplateText();
            OnPropertyChanged(nameof(SharingDocumentRules));
        }
    }
}
