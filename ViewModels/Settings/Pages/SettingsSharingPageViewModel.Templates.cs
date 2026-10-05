// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsSharingPageViewModel.Templates.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — Sharing page VM: message-template members
//
// Split out of SettingsSharingPageViewModel.cs so both files stay inside
// the 350-line Settings VM budget (see SettingsRedesignAuditTests).
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
    public sealed partial class SettingsSharingPageViewModel
    {
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
