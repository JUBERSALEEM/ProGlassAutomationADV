// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/InquiryFeaturesViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings ► Features ► Inquiry
//
// Bindable VM behind the pill toggles for the Inquiry editor's
// optional fields. Same design pattern as
// ProformaInvoiceFeaturesViewModel — each toggle writes to
// AppConfig.InquiryFields, persists via ConfigService.Save, and
// calls InquiryFieldVisibility.Instance.Refresh() so the editor
// reacts live without an app restart.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Features;
using ProGlassAutomation.Services.Logging;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed class InquiryFeaturesViewModel : ViewModelBase
    {
        public InquiryFeaturesViewModel()
        {
            ResetToDefaultsCommand      = new RelayCommand(_ => SetAll(true));
            EnableAllCommand            = new RelayCommand(_ => SetAll(true));
            DisableAllOptionalCommand   = new RelayCommand(_ => SetAll(false));
        }

        public ICommand ResetToDefaultsCommand    { get; }
        public ICommand EnableAllCommand          { get; }
        public ICommand DisableAllOptionalCommand { get; }

        private static InquiryFeatureFlags Flags
        {
            get
            {
                if (App.Config == null) return new InquiryFeatureFlags();
                App.Config.InquiryFields ??= new InquiryFeatureFlags();
                return App.Config.InquiryFields;
            }
        }

        private void PersistAndNotify(string propertyName)
        {
            try
            {
                if (App.Config != null) ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist Inquiry feature flag '" + propertyName +
                    "': " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(InquiryFeaturesViewModel),
                        Operation = "PersistAndNotify"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Inquiry field visibility changed",
                    propertyName + " → " + (GetType().GetProperty(propertyName)?.GetValue(this) ?? "?"));
            }
            catch { /* audit must not block settings */ }

            InquiryFieldVisibility.Instance.Refresh();
            OnPropertyChanged(propertyName);
        }

        private void SetAll(bool value)
        {
            var f = Flags;
            f.ShowInquiryReference   = value;
            f.ShowCustomerLocation   = value;
            f.ShowProjectDescription = value;
            f.ShowSalesman           = value;
            f.ShowPreparedBy         = value;
            f.ShowPriority           = value;
            f.ShowInquiryValue       = value;
            f.ShowNotes              = value;

            try
            {
                if (App.Config != null) ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist bulk Inquiry feature-flag change: " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(InquiryFeaturesViewModel),
                        Operation = "SetAll"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Inquiry field visibility bulk change",
                    "value=" + value);
            }
            catch { /* audit must not block settings */ }

            InquiryFieldVisibility.Instance.Refresh();
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(ShowInquiryReference));
            OnPropertyChanged(nameof(ShowCustomerLocation));
            OnPropertyChanged(nameof(ShowProjectDescription));
            OnPropertyChanged(nameof(ShowSalesman));
            OnPropertyChanged(nameof(ShowPreparedBy));
            OnPropertyChanged(nameof(ShowPriority));
            OnPropertyChanged(nameof(ShowInquiryValue));
            OnPropertyChanged(nameof(ShowNotes));
        }

        public bool ShowInquiryReference
        {
            get => Flags.ShowInquiryReference;
            set { if (Flags.ShowInquiryReference == value) return; Flags.ShowInquiryReference = value; PersistAndNotify(nameof(ShowInquiryReference)); }
        }
        public bool ShowCustomerLocation
        {
            get => Flags.ShowCustomerLocation;
            set { if (Flags.ShowCustomerLocation == value) return; Flags.ShowCustomerLocation = value; PersistAndNotify(nameof(ShowCustomerLocation)); }
        }
        public bool ShowProjectDescription
        {
            get => Flags.ShowProjectDescription;
            set { if (Flags.ShowProjectDescription == value) return; Flags.ShowProjectDescription = value; PersistAndNotify(nameof(ShowProjectDescription)); }
        }
        public bool ShowSalesman
        {
            get => Flags.ShowSalesman;
            set { if (Flags.ShowSalesman == value) return; Flags.ShowSalesman = value; PersistAndNotify(nameof(ShowSalesman)); }
        }
        public bool ShowPreparedBy
        {
            get => Flags.ShowPreparedBy;
            set { if (Flags.ShowPreparedBy == value) return; Flags.ShowPreparedBy = value; PersistAndNotify(nameof(ShowPreparedBy)); }
        }
        public bool ShowPriority
        {
            get => Flags.ShowPriority;
            set { if (Flags.ShowPriority == value) return; Flags.ShowPriority = value; PersistAndNotify(nameof(ShowPriority)); }
        }
        public bool ShowInquiryValue
        {
            get => Flags.ShowInquiryValue;
            set { if (Flags.ShowInquiryValue == value) return; Flags.ShowInquiryValue = value; PersistAndNotify(nameof(ShowInquiryValue)); }
        }
        public bool ShowNotes
        {
            get => Flags.ShowNotes;
            set { if (Flags.ShowNotes == value) return; Flags.ShowNotes = value; PersistAndNotify(nameof(ShowNotes)); }
        }
    }
}
