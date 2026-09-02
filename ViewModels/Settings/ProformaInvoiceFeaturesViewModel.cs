// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/ProformaInvoiceFeaturesViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings ‣ Features ‣ Proforma Invoice
//
// PURPOSE
// ───────
// Bindable view-model behind the iPhone-style pill toggles on the
// Settings ‣ Features page. Each toggle mirrors a boolean on
// AppConfig.ProformaInvoiceFields; setting a toggle:
//   1. Writes the new value onto AppConfig,
//   2. Persists the whole config file (ConfigService.Save),
//   3. Calls ProformaFieldVisibility.Instance.Refresh() so the
//      Proforma Invoice wizard hides / shows the field LIVE
//      without an app restart.
//
// DESIGN NOTES
// ────────────
//   • No dependency injection, no async: writes are microseconds,
//     ConfigService.Save is a small file. Debouncing is deliberately
//     not added — the user clicks a toggle then reads the wizard;
//     a synchronous save keeps the visible state honest.
//   • RESET-TO-DEFAULTS command flips every flag back to TRUE and
//     saves once. Matches the "opt-out" contract in the flags class.
//   • The DB-required fields (Company Name, Customer Name, Proforma
//     ID) do NOT appear here — they cannot be hidden.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Features;
using ProGlassAutomation.Services.Logging;
using System;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed class ProformaInvoiceFeaturesViewModel : ViewModelBase
    {
        private readonly AppConfig _config = null!;

        public ProformaInvoiceFeaturesViewModel(AppConfig? config)
        {
            _config = config!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        public ProformaInvoiceFeaturesViewModel()
         : this(
            App.ServiceProvider?.GetService<AppConfig>()?? App.Config){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            ResetToDefaultsCommand = new RelayCommand(_ => ResetToDefaults());
            EnableAllCommand = new RelayCommand(_ => SetAll(true));
            DisableAllOptionalCommand = new RelayCommand(_ => SetAll(false));
        }

        // ── Convenience helpers ────────────────────────────────────

        /// <summary>Reset every field to its default (all visible).</summary>
        public ICommand ResetToDefaultsCommand { get; }

        /// <summary>Turn every optional field ON in one click.</summary>
        public ICommand EnableAllCommand { get; }

        /// <summary>Turn every optional field OFF in one click.</summary>
        public ICommand DisableAllOptionalCommand { get; }

        // ── Backing helpers ────────────────────────────────────────

        private ProformaInvoiceFeatureFlags Flags
        {
            get
            {
                if (_config == null)
                {
                    // Defensive: should never happen because Settings
                    // opens after startup, but a fallback beats an NRE
                    // if a designer / test loads this VM without a
                    // real config.
                    return new ProformaInvoiceFeatureFlags();
                }
                _config.ProformaInvoiceFields ??= new ProformaInvoiceFeatureFlags();
                return _config.ProformaInvoiceFields;
            }
        }

        private void PersistAndNotify(string propertyName)
        {
            try
            {
                if (_config != null)
                    ConfigService.Save(_config);
            }
            catch (Exception ex)
            {
                // Settings must never crash the app. Log and keep the
                // in-memory value; the next successful save will
                // resync the file.
                AppLogService.Warning(
                    "Could not persist Proforma feature flag '" + propertyName +
                    "': " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(ProformaInvoiceFeaturesViewModel),
                        Operation = "PersistAndNotify"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Proforma field visibility changed",
                    propertyName + " → " + (GetType().GetProperty(propertyName)?.GetValue(this) ?? "?"));
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[ProformaInvoiceFeaturesViewModel] PersistAndNotify: audit must not block settings: " + ex.Message); }

            // Push the change into the wizard immediately.
            ProformaFieldVisibility.Instance.Refresh();
            OnPropertyChanged(propertyName);
        }

        private void SetAll(bool value)
        {
            var f = Flags;
            f.ShowCompanyTrn = value;
            f.ShowCompanyLocation = value;
            f.ShowRepresentative = value;
            f.ShowRepresentativeMobile = value;
            f.ShowCompanyEmail = value;
            f.ShowBankName = value;
            f.ShowAccountNumber = value;
            f.ShowIssueDate = value;
            f.ShowValidUntil = value;
            f.ShowProformaStatus = value;

            f.ShowCustomerTrn = value;
            f.ShowCustomerReferenceCode = value;
            f.ShowSalesRepresentative = value;
            f.ShowDocumentPreparedBy = value;
            f.ShowAttentionExecutive = value;
            f.ShowClientContactPhone = value;
            f.ShowBillingAddress = value;

            f.ShowProjectName = value;
            f.ShowProjectReference = value;
            f.ShowContractNo = value;
            f.ShowLpoNumber = value;
            f.ShowSiteDeliveryLocation = value;

            try
            {
                if (_config != null)
                    ConfigService.Save(_config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist bulk Proforma feature-flag change: " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(ProformaInvoiceFeaturesViewModel),
                        Operation = "SetAll"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Proforma field visibility bulk change",
                    "value=" + value);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[ProformaInvoiceFeaturesViewModel] SetAll: audit must not block settings: " + ex.Message); }

            ProformaFieldVisibility.Instance.Refresh();
            RaiseAll();
        }

        private void ResetToDefaults() => SetAll(true);

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(ShowCompanyTrn));
            OnPropertyChanged(nameof(ShowCompanyLocation));
            OnPropertyChanged(nameof(ShowRepresentative));
            OnPropertyChanged(nameof(ShowRepresentativeMobile));
            OnPropertyChanged(nameof(ShowCompanyEmail));
            OnPropertyChanged(nameof(ShowBankName));
            OnPropertyChanged(nameof(ShowAccountNumber));
            OnPropertyChanged(nameof(ShowIssueDate));
            OnPropertyChanged(nameof(ShowValidUntil));
            OnPropertyChanged(nameof(ShowProformaStatus));
            OnPropertyChanged(nameof(ShowCustomerTrn));
            OnPropertyChanged(nameof(ShowCustomerReferenceCode));
            OnPropertyChanged(nameof(ShowSalesRepresentative));
            OnPropertyChanged(nameof(ShowDocumentPreparedBy));
            OnPropertyChanged(nameof(ShowAttentionExecutive));
            OnPropertyChanged(nameof(ShowClientContactPhone));
            OnPropertyChanged(nameof(ShowBillingAddress));
            OnPropertyChanged(nameof(ShowProjectName));
            OnPropertyChanged(nameof(ShowProjectReference));
            OnPropertyChanged(nameof(ShowContractNo));
            OnPropertyChanged(nameof(ShowLpoNumber));
            OnPropertyChanged(nameof(ShowSiteDeliveryLocation));
        }

        // ── Toggles — Step 1: Company ──────────────────────────────

        public bool ShowCompanyTrn
        {
            get => Flags.ShowCompanyTrn;
            set { if (Flags.ShowCompanyTrn == value) return; Flags.ShowCompanyTrn = value; PersistAndNotify(nameof(ShowCompanyTrn)); }
        }
        public bool ShowCompanyLocation
        {
            get => Flags.ShowCompanyLocation;
            set { if (Flags.ShowCompanyLocation == value) return; Flags.ShowCompanyLocation = value; PersistAndNotify(nameof(ShowCompanyLocation)); }
        }
        public bool ShowRepresentative
        {
            get => Flags.ShowRepresentative;
            set { if (Flags.ShowRepresentative == value) return; Flags.ShowRepresentative = value; PersistAndNotify(nameof(ShowRepresentative)); }
        }
        public bool ShowRepresentativeMobile
        {
            get => Flags.ShowRepresentativeMobile;
            set { if (Flags.ShowRepresentativeMobile == value) return; Flags.ShowRepresentativeMobile = value; PersistAndNotify(nameof(ShowRepresentativeMobile)); }
        }
        public bool ShowCompanyEmail
        {
            get => Flags.ShowCompanyEmail;
            set { if (Flags.ShowCompanyEmail == value) return; Flags.ShowCompanyEmail = value; PersistAndNotify(nameof(ShowCompanyEmail)); }
        }
        public bool ShowBankName
        {
            get => Flags.ShowBankName;
            set { if (Flags.ShowBankName == value) return; Flags.ShowBankName = value; PersistAndNotify(nameof(ShowBankName)); }
        }
        public bool ShowAccountNumber
        {
            get => Flags.ShowAccountNumber;
            set { if (Flags.ShowAccountNumber == value) return; Flags.ShowAccountNumber = value; PersistAndNotify(nameof(ShowAccountNumber)); }
        }
        public bool ShowIssueDate
        {
            get => Flags.ShowIssueDate;
            set { if (Flags.ShowIssueDate == value) return; Flags.ShowIssueDate = value; PersistAndNotify(nameof(ShowIssueDate)); }
        }
        public bool ShowValidUntil
        {
            get => Flags.ShowValidUntil;
            set { if (Flags.ShowValidUntil == value) return; Flags.ShowValidUntil = value; PersistAndNotify(nameof(ShowValidUntil)); }
        }
        public bool ShowProformaStatus
        {
            get => Flags.ShowProformaStatus;
            set { if (Flags.ShowProformaStatus == value) return; Flags.ShowProformaStatus = value; PersistAndNotify(nameof(ShowProformaStatus)); }
        }

        // ── Toggles — Step 2: Client ───────────────────────────────

        public bool ShowCustomerTrn
        {
            get => Flags.ShowCustomerTrn;
            set { if (Flags.ShowCustomerTrn == value) return; Flags.ShowCustomerTrn = value; PersistAndNotify(nameof(ShowCustomerTrn)); }
        }
        public bool ShowCustomerReferenceCode
        {
            get => Flags.ShowCustomerReferenceCode;
            set { if (Flags.ShowCustomerReferenceCode == value) return; Flags.ShowCustomerReferenceCode = value; PersistAndNotify(nameof(ShowCustomerReferenceCode)); }
        }
        public bool ShowSalesRepresentative
        {
            get => Flags.ShowSalesRepresentative;
            set { if (Flags.ShowSalesRepresentative == value) return; Flags.ShowSalesRepresentative = value; PersistAndNotify(nameof(ShowSalesRepresentative)); }
        }
        public bool ShowDocumentPreparedBy
        {
            get => Flags.ShowDocumentPreparedBy;
            set { if (Flags.ShowDocumentPreparedBy == value) return; Flags.ShowDocumentPreparedBy = value; PersistAndNotify(nameof(ShowDocumentPreparedBy)); }
        }
        public bool ShowAttentionExecutive
        {
            get => Flags.ShowAttentionExecutive;
            set { if (Flags.ShowAttentionExecutive == value) return; Flags.ShowAttentionExecutive = value; PersistAndNotify(nameof(ShowAttentionExecutive)); }
        }
        public bool ShowClientContactPhone
        {
            get => Flags.ShowClientContactPhone;
            set { if (Flags.ShowClientContactPhone == value) return; Flags.ShowClientContactPhone = value; PersistAndNotify(nameof(ShowClientContactPhone)); }
        }
        public bool ShowBillingAddress
        {
            get => Flags.ShowBillingAddress;
            set { if (Flags.ShowBillingAddress == value) return; Flags.ShowBillingAddress = value; PersistAndNotify(nameof(ShowBillingAddress)); }
        }

        // ── Toggles — Step 3: Project ──────────────────────────────

        public bool ShowProjectName
        {
            get => Flags.ShowProjectName;
            set { if (Flags.ShowProjectName == value) return; Flags.ShowProjectName = value; PersistAndNotify(nameof(ShowProjectName)); }
        }
        public bool ShowProjectReference
        {
            get => Flags.ShowProjectReference;
            set { if (Flags.ShowProjectReference == value) return; Flags.ShowProjectReference = value; PersistAndNotify(nameof(ShowProjectReference)); }
        }
        public bool ShowContractNo
        {
            get => Flags.ShowContractNo;
            set { if (Flags.ShowContractNo == value) return; Flags.ShowContractNo = value; PersistAndNotify(nameof(ShowContractNo)); }
        }
        public bool ShowLpoNumber
        {
            get => Flags.ShowLpoNumber;
            set { if (Flags.ShowLpoNumber == value) return; Flags.ShowLpoNumber = value; PersistAndNotify(nameof(ShowLpoNumber)); }
        }
        public bool ShowSiteDeliveryLocation
        {
            get => Flags.ShowSiteDeliveryLocation;
            set { if (Flags.ShowSiteDeliveryLocation == value) return; Flags.ShowSiteDeliveryLocation = value; PersistAndNotify(nameof(ShowSiteDeliveryLocation)); }
        }
    }
}
