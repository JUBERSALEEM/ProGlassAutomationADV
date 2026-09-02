// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/DeliveryNoteFeaturesViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings ► Features ► Delivery Note
//
// Bindable VM behind the pill toggles for the Delivery Note detail's
// optional fields. Same design pattern as ProformaInvoiceFeaturesViewModel
// — each toggle writes to AppConfig.DeliveryNoteFields, persists via
// ConfigService.Save, and calls DeliveryNoteFieldVisibility.Instance
// .Refresh() so the detail reacts live without an app restart.
//
// Status does NOT appear here — it drives the dispatch workflow.
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
    public sealed class DeliveryNoteFeaturesViewModel : ViewModelBase
    {
        private readonly AppConfig _config = null!;

        // ── ERR-633EEB CLASS FIX (2026-09-02) ──────────────────────
        // Initialization used to live in the PARAMETERLESS ctor while
        // DI selected this parameterized overload (MS.DI always picks
        // the greediest satisfiable ctor). Every DI-resolved instance
        // was therefore half-built — null child view models / null
        // commands — and threw NullReferenceException on first use.
        // The body was moved HERE so all construction routes (DI,
        // Activator fallback, XAML design-time) initialize fully.
        public DeliveryNoteFeaturesViewModel(AppConfig? config)
        {
            _config = config!;

            ResetToDefaultsCommand      = new RelayCommand(_ => SetAll(true));
            EnableAllCommand            = new RelayCommand(_ => SetAll(true));
            DisableAllOptionalCommand   = new RelayCommand(_ => SetAll(false));
        }

        public DeliveryNoteFeaturesViewModel()
         : this(
            App.ServiceProvider?.GetService<AppConfig>()?? App.Config){
        }

        public ICommand ResetToDefaultsCommand    { get; }
        public ICommand EnableAllCommand          { get; }
        public ICommand DisableAllOptionalCommand { get; }

        private DeliveryNoteFeatureFlags Flags
        {
            get
            {
                if (_config == null) return new DeliveryNoteFeatureFlags();
                _config.DeliveryNoteFields ??= new DeliveryNoteFeatureFlags();
                return _config.DeliveryNoteFields;
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
                    "Could not persist Delivery Note feature flag '" + propertyName +
                    "': " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(DeliveryNoteFeaturesViewModel),
                        Operation = "PersistAndNotify"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Delivery Note field visibility changed",
                    propertyName + " → " + (GetType().GetProperty(propertyName)?.GetValue(this) ?? "?"));
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[DeliveryNoteFeaturesViewModel] PersistAndNotify: audit must not block settings: " + ex.Message); }

            DeliveryNoteFieldVisibility.Instance.Refresh();
            OnPropertyChanged(propertyName);
        }

        private void SetAll(bool value)
        {
            var f = Flags;
            f.ShowDriverName   = value;
            f.ShowVehicleNumber = value;
            f.ShowIsFoc        = value;
            f.ShowReceiverName = value;
            f.ShowNotes        = value;

            try
            {
                if (_config != null) ConfigService.Save(_config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist bulk Delivery Note feature-flag change: " + ex.Message,
                    new LogContext
                    {
                        Module = "Settings",
                        Class = nameof(DeliveryNoteFeaturesViewModel),
                        Operation = "SetAll"
                    });
            }

            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Delivery Note field visibility bulk change",
                    "value=" + value);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[DeliveryNoteFeaturesViewModel] SetAll: audit must not block settings: " + ex.Message); }

            DeliveryNoteFieldVisibility.Instance.Refresh();
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(ShowDriverName));
            OnPropertyChanged(nameof(ShowVehicleNumber));
            OnPropertyChanged(nameof(ShowIsFoc));
            OnPropertyChanged(nameof(ShowReceiverName));
            OnPropertyChanged(nameof(ShowNotes));
        }

        public bool ShowDriverName
        {
            get => Flags.ShowDriverName;
            set { if (Flags.ShowDriverName == value) return; Flags.ShowDriverName = value; PersistAndNotify(nameof(ShowDriverName)); }
        }
        public bool ShowVehicleNumber
        {
            get => Flags.ShowVehicleNumber;
            set { if (Flags.ShowVehicleNumber == value) return; Flags.ShowVehicleNumber = value; PersistAndNotify(nameof(ShowVehicleNumber)); }
        }
        public bool ShowIsFoc
        {
            get => Flags.ShowIsFoc;
            set { if (Flags.ShowIsFoc == value) return; Flags.ShowIsFoc = value; PersistAndNotify(nameof(ShowIsFoc)); }
        }
        public bool ShowReceiverName
        {
            get => Flags.ShowReceiverName;
            set { if (Flags.ShowReceiverName == value) return; Flags.ShowReceiverName = value; PersistAndNotify(nameof(ShowReceiverName)); }
        }
        public bool ShowNotes
        {
            get => Flags.ShowNotes;
            set { if (Flags.ShowNotes == value) return; Flags.ShowNotes = value; PersistAndNotify(nameof(ShowNotes)); }
        }
    }
}
