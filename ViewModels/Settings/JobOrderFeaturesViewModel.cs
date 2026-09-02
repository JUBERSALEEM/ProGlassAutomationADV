// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/JobOrderFeaturesViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings ► Features ► Job Order
//
// Bindable VM behind the pill toggles for the Job Order production
// panel's optional fields. Same design pattern as
// ProformaInvoiceFeaturesViewModel — each toggle writes to
// AppConfig.JobOrderFields, persists via ConfigService.Save, and
// calls JobOrderFieldVisibility.Instance.Refresh() so the production
// panel reacts live without an app restart.
//
// DB-required / workflow controls (Status, Priority, Delivery Date)
// do NOT appear here — they cannot be hidden.
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
    public sealed class JobOrderFeaturesViewModel : ViewModelBase
    {
        private readonly AppConfig _config = null!;

        public JobOrderFeaturesViewModel(AppConfig? config)
        {
            _config = config!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        public JobOrderFeaturesViewModel()
         : this(
            App.ServiceProvider?.GetService<AppConfig>()?? App.Config){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            ResetToDefaultsCommand      = new RelayCommand(_ => SetAll(true));
            EnableAllCommand            = new RelayCommand(_ => SetAll(true));
            DisableAllOptionalCommand   = new RelayCommand(_ => SetAll(false));
        }

        public ICommand ResetToDefaultsCommand    { get; private set; }
        public ICommand EnableAllCommand          { get; private set; }
        public ICommand DisableAllOptionalCommand { get; private set; }

        private JobOrderFeatureFlags Flags
        {
            get
            {
                if (_config == null) return new JobOrderFeatureFlags();
                _config.JobOrderFields ??= new JobOrderFeatureFlags();
                return _config.JobOrderFields;
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
                    "Could not persist Job Order feature flag '" + propertyName +
                    "': " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(JobOrderFeaturesViewModel),
                        Operation = "PersistAndNotify"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Job Order field visibility changed",
                    propertyName + " → " + (GetType().GetProperty(propertyName)?.GetValue(this) ?? "?"));
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[JobOrderFeaturesViewModel] PersistAndNotify: audit must not block settings: " + ex.Message); }

            JobOrderFieldVisibility.Instance.Refresh();
            OnPropertyChanged(propertyName);
        }

        private void SetAll(bool value)
        {
            var f = Flags;
            f.ShowAssignedTo          = value;
            f.ShowSiteDeliveryContact = value;
            f.ShowSiteDeliveryPhone   = value;
            f.ShowSiteDeliveryAddress = value;
            f.ShowDrawingReference    = value;
            f.ShowCuttingInstructions = value;
            f.ShowInternalNotes       = value;

            try
            {
                if (_config != null) ConfigService.Save(_config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist bulk Job Order feature-flag change: " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(JobOrderFeaturesViewModel),
                        Operation = "SetAll"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Job Order field visibility bulk change",
                    "value=" + value);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[JobOrderFeaturesViewModel] SetAll: audit must not block settings: " + ex.Message); }

            JobOrderFieldVisibility.Instance.Refresh();
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(ShowAssignedTo));
            OnPropertyChanged(nameof(ShowSiteDeliveryContact));
            OnPropertyChanged(nameof(ShowSiteDeliveryPhone));
            OnPropertyChanged(nameof(ShowSiteDeliveryAddress));
            OnPropertyChanged(nameof(ShowDrawingReference));
            OnPropertyChanged(nameof(ShowCuttingInstructions));
            OnPropertyChanged(nameof(ShowInternalNotes));
        }

        public bool ShowAssignedTo
        {
            get => Flags.ShowAssignedTo;
            set { if (Flags.ShowAssignedTo == value) return; Flags.ShowAssignedTo = value; PersistAndNotify(nameof(ShowAssignedTo)); }
        }
        public bool ShowSiteDeliveryContact
        {
            get => Flags.ShowSiteDeliveryContact;
            set { if (Flags.ShowSiteDeliveryContact == value) return; Flags.ShowSiteDeliveryContact = value; PersistAndNotify(nameof(ShowSiteDeliveryContact)); }
        }
        public bool ShowSiteDeliveryPhone
        {
            get => Flags.ShowSiteDeliveryPhone;
            set { if (Flags.ShowSiteDeliveryPhone == value) return; Flags.ShowSiteDeliveryPhone = value; PersistAndNotify(nameof(ShowSiteDeliveryPhone)); }
        }
        public bool ShowSiteDeliveryAddress
        {
            get => Flags.ShowSiteDeliveryAddress;
            set { if (Flags.ShowSiteDeliveryAddress == value) return; Flags.ShowSiteDeliveryAddress = value; PersistAndNotify(nameof(ShowSiteDeliveryAddress)); }
        }
        public bool ShowDrawingReference
        {
            get => Flags.ShowDrawingReference;
            set { if (Flags.ShowDrawingReference == value) return; Flags.ShowDrawingReference = value; PersistAndNotify(nameof(ShowDrawingReference)); }
        }
        public bool ShowCuttingInstructions
        {
            get => Flags.ShowCuttingInstructions;
            set { if (Flags.ShowCuttingInstructions == value) return; Flags.ShowCuttingInstructions = value; PersistAndNotify(nameof(ShowCuttingInstructions)); }
        }
        public bool ShowInternalNotes
        {
            get => Flags.ShowInternalNotes;
            set { if (Flags.ShowInternalNotes == value) return; Flags.ShowInternalNotes = value; PersistAndNotify(nameof(ShowInternalNotes)); }
        }
    }
}
