// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/TaxInvoiceFeaturesViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings ► Features ► Tax Invoice
//
// Bindable VM behind the pill toggles for the Tax Invoice detail's
// optional fields. Same design pattern as ProformaInvoiceFeaturesViewModel
// — each toggle writes to AppConfig.TaxInvoiceFields, persists via
// ConfigService.Save, and calls TaxInvoiceFieldVisibility.Instance
// .Refresh() so the detail reacts live without an app restart.
//
// Issue Date does NOT appear here — it anchors the invoice.
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
    public sealed class TaxInvoiceFeaturesViewModel : ViewModelBase
    {
        private readonly AppConfig _config = null!;

        public TaxInvoiceFeaturesViewModel(AppConfig? config)
        {
            _config = config!;
        }

        public TaxInvoiceFeaturesViewModel()
         : this(
            App.ServiceProvider?.GetService<AppConfig>()?? App.Config){
            ResetToDefaultsCommand      = new RelayCommand(_ => SetAll(true));
            EnableAllCommand            = new RelayCommand(_ => SetAll(true));
            DisableAllOptionalCommand   = new RelayCommand(_ => SetAll(false));
        }

        public ICommand ResetToDefaultsCommand    { get; }
        public ICommand EnableAllCommand          { get; }
        public ICommand DisableAllOptionalCommand { get; }

        private TaxInvoiceFeatureFlags Flags
        {
            get
            {
                if (_config == null) return new TaxInvoiceFeatureFlags();
                _config.TaxInvoiceFields ??= new TaxInvoiceFeatureFlags();
                return _config.TaxInvoiceFields;
            }
        }

        private void PersistAndNotify(string propertyName)
        {
            try
            {
                if (_config != null) ConfigService.Save(_config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist Tax Invoice feature flag '" + propertyName +
                    "': " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(TaxInvoiceFeaturesViewModel),
                        Operation = "PersistAndNotify"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Tax Invoice field visibility changed",
                    propertyName + " → " + (GetType().GetProperty(propertyName)?.GetValue(this) ?? "?"));
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[TaxInvoiceFeaturesViewModel] PersistAndNotify: audit must not block settings: " + ex.Message); }

            TaxInvoiceFieldVisibility.Instance.Refresh();
            OnPropertyChanged(propertyName);
        }

        private void SetAll(bool value)
        {
            var f = Flags;
            f.ShowDueDate         = value;
            f.ShowPaymentTerms    = value;
            f.ShowDiscountPercent = value;
            f.ShowIban            = value;
            f.ShowSwiftCode       = value;

            try
            {
                if (_config != null) ConfigService.Save(_config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist bulk Tax Invoice feature-flag change: " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(TaxInvoiceFeaturesViewModel),
                        Operation = "SetAll"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Tax Invoice field visibility bulk change",
                    "value=" + value);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[TaxInvoiceFeaturesViewModel] SetAll: audit must not block settings: " + ex.Message); }

            TaxInvoiceFieldVisibility.Instance.Refresh();
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(ShowDueDate));
            OnPropertyChanged(nameof(ShowPaymentTerms));
            OnPropertyChanged(nameof(ShowDiscountPercent));
            OnPropertyChanged(nameof(ShowIban));
            OnPropertyChanged(nameof(ShowSwiftCode));
        }

        public bool ShowDueDate
        {
            get => Flags.ShowDueDate;
            set { if (Flags.ShowDueDate == value) return; Flags.ShowDueDate = value; PersistAndNotify(nameof(ShowDueDate)); }
        }
        public bool ShowPaymentTerms
        {
            get => Flags.ShowPaymentTerms;
            set { if (Flags.ShowPaymentTerms == value) return; Flags.ShowPaymentTerms = value; PersistAndNotify(nameof(ShowPaymentTerms)); }
        }
        public bool ShowDiscountPercent
        {
            get => Flags.ShowDiscountPercent;
            set { if (Flags.ShowDiscountPercent == value) return; Flags.ShowDiscountPercent = value; PersistAndNotify(nameof(ShowDiscountPercent)); }
        }
        public bool ShowIban
        {
            get => Flags.ShowIban;
            set { if (Flags.ShowIban == value) return; Flags.ShowIban = value; PersistAndNotify(nameof(ShowIban)); }
        }
        public bool ShowSwiftCode
        {
            get => Flags.ShowSwiftCode;
            set { if (Flags.ShowSwiftCode == value) return; Flags.ShowSwiftCode = value; PersistAndNotify(nameof(ShowSwiftCode)); }
        }
    }
}
