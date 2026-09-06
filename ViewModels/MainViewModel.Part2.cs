// ═══════════════════════════════════════════════════════════════════════
// FILE: MainViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.CompanyCenter;
using ProGlassAutomation.ViewModels.UserCenter;
using ProGlassAutomation.ViewModels.AI;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Inquiry;
using ProGlassAutomation.ViewModels.JobProfitability;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
using ProGlassAutomation.ViewModels.Reports.VatSubmission;
using ProGlassAutomation.ViewModels.Settings;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.ViewModels.Diagnostics;
using ProGlassAutomation.ViewModels.SheetStore;
using ProGlassAutomation.ViewModels.SystemCenter;
using ProGlassAutomation.ViewModels.TaxInvoice;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Dialogs;

namespace ProGlassAutomation.ViewModels
{
        public partial class MainViewModel : ViewModelBase, IDisposable
    {
            private void TogglePopup(string? which)
        {
            bool isOpen = which switch
            {
                "Settings" => IsSettingsOpen,
                "Notifications" => IsNotificationsOpen,
                "UserSwitch" => IsUserSwitchOpen,
                "Screenshot" => IsScreenshotOpen,
                "ScreenRecord" => IsScreenRecordOpen,
                _ => false
            };

            if (isOpen)
            {
                switch (which)
                {
                    case "Settings": IsSettingsOpen = false; break;
                    case "Notifications": IsNotificationsOpen = false; break;
                    case "UserSwitch": IsUserSwitchOpen = false; break;
                    case "Screenshot": IsScreenshotOpen = false; break;
                    case "ScreenRecord": IsScreenRecordOpen = false; break;
                }
            }
            else
            {
                IsSettingsOpen = which == "Settings";
                IsNotificationsOpen = which == "Notifications";
                IsUserSwitchOpen = which == "UserSwitch";
                IsScreenshotOpen = which == "Screenshot";
                IsScreenRecordOpen = which == "ScreenRecord";
            }
        }

        private void CloseAllPopups()
        {
            IsSettingsOpen = false;
            IsNotificationsOpen = false;
            IsScreenshotOpen = false;
            IsUserSwitchOpen = false;
            IsScreenRecordOpen = false;
            IsStorageOpen = false;
        }

        private T ResolveOrNew<T>() where T : class
        {
            try
            {
                if (_serviceProvider != null)
                {
                    var svc = _serviceProvider.GetService<T>();
                    if (svc != null) return svc;
                }
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[MainViewModel] DI resolve failed; falling back to Activator: " + ExceptionMessages.Deep(ex)); }
            // Fallback — during migration, allow `new` if DI not ready
            try { return Activator.CreateInstance<T>(); } catch { return null!; }
        }

        private System.Threading.Tasks.Task<bool> AgentConfirmAsync(string title, string message, bool destructive)
        {
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null) return System.Threading.Tasks.Task.FromResult(false);
                bool Ask()
                {
                    var result = _dialogs.Show(message,
                        string.IsNullOrWhiteSpace(title) ? "ProGlass Agent" : title,
                        System.Windows.MessageBoxButton.YesNo,
                        destructive ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Question,
                        System.Windows.MessageBoxResult.No);
                    return result == System.Windows.MessageBoxResult.Yes;
                }
                bool answer = dispatcher.CheckAccess() ? Ask() : dispatcher.Invoke(Ask);
                return System.Threading.Tasks.Task.FromResult(answer);
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"[MainViewModel] agent confirm failed: {ExceptionMessages.Deep(ex)}",
                    new LogContext { Module = "Main", Operation = "AgentConfirm" });
                return System.Threading.Tasks.Task.FromResult(false);
            }
        }

        /// <summary>
        /// 3-way "How should this new Proforma Invoice start?"
        /// dialog. Returns the user's choice. The default result
        /// (when the user hits Escape or closes the dialog) is
        /// <see cref="ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Cancelled"/>
        /// so a stray dismissal never silently creates a blank PI.
        /// </summary>
        private ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice PromptNewPiStart()
        {
            // We use a 3-button MessageBox: Yes / No / Cancel, mapped to
            // FromInquiry / Skip / Cancelled. The Yes button is the
            // default because starting from an Inquiry is the
            // recommended path; Cancel is the cancel button so Esc
            // dismisses safely.
            var message =
                "Proforma Invoices should start from a confirmed Inquiry so the customer, " +
                "project, value, and audit trail are pre-filled automatically.\n\n" +
                "Open the Inquiry list now to pick (or create) a Confirmed inquiry?\n\n" +
                "• Yes — open the Inquiry list (recommended)\n" +
                "• No — create a blank Proforma Invoice without an inquiry\n" +
                "• Cancel — do not start a new Proforma Invoice";
            var result = _dialogs.Show(message,
                LocalizationService.Instance.T("Msg.CapStartProformaInvoiceFromInquiry"),
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.Yes);
            return result switch
            {
                System.Windows.MessageBoxResult.Yes => ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.FromInquiry,
                System.Windows.MessageBoxResult.No => ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Skip,
                _ => ViewModels.ProformaInvoice.ProformaInvoiceViewModel.NewProformaStartChoice.Cancelled
            };
        }

        public void NavigateFromAgent(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(new Action(() => Navigate(target)));
            else
                Navigate(target);
        }

        private void Navigate(object parameter)
        {
            string? targetKey = parameter?.ToString();

            // Phase 3 — feature gating. A Trial user
            // should not be able to open the Cutting
            // Optimisation module. The gate is a pure
            // function of (moduleKey, current license
            // state), so a future test passes trivially.
            var gate = ProGlassAutomation.Services.Licensing.LicenseGate
                .CanNavigate(targetKey ?? "");
            if (!gate.Allowed)
            {
                _dialogs.Show(
                    gate.Reason,
                    LocalizationService.Instance.T("Msg.CapUpgradeRequired"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            CurrentModuleKey = targetKey ?? "";
            switch (targetKey)
            {
                case "Dashboard": CurrentViewModel = DashboardVM; break;
                case "Inquiry": CurrentViewModel = InquiryVM; break;
                case "SheetStore": CurrentViewModel = SheetStoreVM; break;
                case "Specifications": CurrentViewModel = SpecificationsVM; break;
                case "ProformaInvoice": CurrentViewModel = ProformaInvoiceVM; break;
                case "JobOrder": CurrentViewModel = JobOrderVM; break;
                case "Optimization": CurrentViewModel = OptimizationVM; break;
                case "Cutting": CurrentViewModel = CuttingVM; break;
                case "HolesCutouts": CurrentViewModel = HolesCutoutsVM; break;
                case "Polish": CurrentViewModel = PolishVM; break;
                case "EdgeDeletion": CurrentViewModel = EdgeDeletionVM; break;
                case "Mitring": CurrentViewModel = MitringVM; break;
                case "Tempering": CurrentViewModel = TemperingVM; break;
                case "Pairing": CurrentViewModel = PairingVM; break;
                case "DoublingLamination": CurrentViewModel = DoublingLaminationVM; break;
                case "DeliveryNote": CurrentViewModel = DeliveryNoteVM; break;
                case "TaxInvoice": CurrentViewModel = TaxInvoiceVM; break;
                case "SalesReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Overview"; break;
                case "PiReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Pi"; break;
                case "JoReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Jo"; break;
                case "DeliveryReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Delivery"; break;
                case "TaxInvoiceReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "TaxInvoice"; break;
                case "StatementReport": CurrentViewModel = SalesReportVM; SalesReportVM.ActiveKey = "Statement"; _ = SalesReportVM.StatementReport.LoadAsync(); break;
                case "StatementGenerator": CurrentViewModel = StatementModuleVM; break;
                case "OrderReport":
                    WireOrderReportCallbacks();
                    CurrentViewModel = OrderReportModuleVM;
                    break;
                case "About": CurrentViewModel = AboutVM; break;
                case "Settings": CurrentViewModel = SettingsVM; break;
                case "SystemCenter":
                    CurrentViewModel = SystemCenterVM;
                    _ = SystemCenterVM.LoadUsersAsync();
                    break;
                case "CompanyCenter": CurrentViewModel = CompanyCenterVM; break;
                case "UserCenter": CurrentViewModel = UserCenterVM; break;
                case "JobProfitability":
                    CurrentViewModel = JobProfitabilityVM;
                    // Guarded for the same reason as OpenSettingsPage: this VM
                    // comes from ResolveOrNew<T>() and its commands are created
                    // in InitializeCore(), so navigation must not assume either
                    // is non-null (ERR-BEFC44 family).
                    if (JobProfitabilityVM?.RefreshCommand?.CanExecute(null) == true)
                        JobProfitabilityVM.RefreshCommand.Execute(null);
                    break;
                case "VatSubmission":
                    CurrentViewModel = VatSubmissionVM;
                    break;
                case "Diagnostics":
                    CurrentViewModel = DiagnosticsVM;
                    DiagnosticsVM.OnShown();
                    break;
            }
            ApplyModuleWallpaper(parameter?.ToString());
            IsMenuOpen = false;
        }

        private void ApplyModuleWallpaper(string? navKey)
        {
            string moduleKey = navKey switch
            {
                "Dashboard" => ProGlassAutomation.Services.Theme.WallpaperModules.Dashboard,
                "Inquiry" => ProGlassAutomation.Services.Theme.WallpaperModules.ProformaInvoice,
                "SheetStore" => ProGlassAutomation.Services.Theme.WallpaperModules.SheetStore,
                // Specifications sits in the same Inventory group as Sheet
                // Store and reads as part of it, so it shares its wallpaper
                // rather than adding a module key nobody can configure yet.
                "Specifications" => ProGlassAutomation.Services.Theme.WallpaperModules.SheetStore,
                "ProformaInvoice" => ProGlassAutomation.Services.Theme.WallpaperModules.ProformaInvoice,
                "JobOrder" => ProGlassAutomation.Services.Theme.WallpaperModules.JobOrder,
                "DeliveryNote" => ProGlassAutomation.Services.Theme.WallpaperModules.DeliveryNote,
                "TaxInvoice" => ProGlassAutomation.Services.Theme.WallpaperModules.TaxInvoice,
                "SalesReport" => ProGlassAutomation.Services.Theme.WallpaperModules.SalesReport,
                "StatementGenerator" => ProGlassAutomation.Services.Theme.WallpaperModules.StatementGenerator,
                // Report Generator sits next to Statement Generator (Finance
                // group) and shares its wallpaper until a dedicated key exists.
                "OrderReport" => ProGlassAutomation.Services.Theme.WallpaperModules.StatementGenerator,
                "Optimization" or "Cutting" or "HolesCutouts" or "Polish" or "EdgeDeletion" or "Mitring" or "Tempering" or "Pairing" or "DoublingLamination" => ProGlassAutomation.Services.Theme.WallpaperModules.Optimization,
                "Diagnostics" => ProGlassAutomation.Services.Theme.WallpaperModules.Logs,
                "SystemCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.SystemCenter,
                "Settings" => ProGlassAutomation.Services.Theme.WallpaperModules.Settings,
                "About" => ProGlassAutomation.Services.Theme.WallpaperModules.About,
                "CompanyCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.CompanyCenter,
                "UserCenter" => ProGlassAutomation.Services.Theme.WallpaperModules.UserCenter,
                "JobProfitability" => ProGlassAutomation.Services.Theme.WallpaperModules.JobProfitability,
                "VatSubmission" => ProGlassAutomation.Services.Theme.WallpaperModules.TaxInvoice,
                _ => ""
            };
            ProGlassAutomation.Services.Theme.VisualThemeEngine.ApplyModule(moduleKey, _config?.Appearance);
        }

        private bool _orderReportCallbacksWired;

        /// <summary>
        /// Wires the Report Generator's warning-action callbacks exactly once
        /// (the module VM is lazy). Mirrors the Statement Generator wiring in
        /// Part1: "open PI / open JO / open TI" navigate to the real document.
        /// </summary>
        private void WireOrderReportCallbacks()
        {
            if (_orderReportCallbacksWired) return;
            _orderReportCallbacksWired = true;

            OrderReportModuleVM.OnOpenProformaByNumber = async proformaId =>
            {
                if (string.IsNullOrWhiteSpace(proformaId) || _invoices == null) return;
                var pi = await _invoices.GetByProformaIdAsync(proformaId);
                if (pi == null) return;
                Navigate("ProformaInvoice");
                await ProformaInvoiceVM.OpenSavedInvoiceAsync(pi.Id);
            };

            OrderReportModuleVM.OnOpenJobOrderByNumber = async jobOrderId =>
            {
                if (string.IsNullOrWhiteSpace(jobOrderId) || _jobs == null) return;
                var jo = await _jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo == null) return;
                Navigate("JobOrder");
                await JobOrderVM.OpenJobOrderAsync(jo.Id);
            };

            OrderReportModuleVM.OnOpenTaxInvoiceByNumber = async invoiceNumber =>
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber) || _taxInvoices == null) return;
                var inv = await _taxInvoices.GetByInvoiceNumberAsync(invoiceNumber);
                if (inv == null) return;
                Navigate("TaxInvoice");
                await TaxInvoiceVM.OpenInvoiceAsync(inv.Id);
            };
        }

        private async Task LoadVatSubmissionAsync()
        {
            try
            {
                // Load filter options first (companies, salesmen, projects)
                await VatSubmissionVM.LoadFilterOptionsAsync();
                // Then load the data
                await VatSubmissionVM.LoadAsync();
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error($"Failed to load VAT Submission: {ExceptionMessages.Deep(ex)}", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_connectivityHandler != null && _connectivity != null) _connectivity.ConnectivityChanged -= _connectivityHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: connectivity detach failed: " + ExceptionMessages.Deep(ex)); }
            try { Services.Audit.AuditService.CurrentUserChanged -= _currentUserChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: user-changed detach failed: " + ExceptionMessages.Deep(ex)); }
            try { _clockTimer?.Stop(); _clockTimer = null; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: clock timer stop failed: " + ExceptionMessages.Deep(ex)); }
            // Work-A-2: dispose the shell VMs so their child module VMs (ListVM,
            // DetailVM, ActivityVM) detach their static-event subscriptions
            // (DocumentLifecycle, SampleData.DataSeeded,
            // AuditService.ActivityLogged) — without this they leak and keep
            // firing after the module is no longer in use.
            try { InquiryVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: InquiryVM teardown failed: " + ExceptionMessages.Deep(ex)); }
            try { JobOrderVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: JobOrderVM teardown failed: " + ExceptionMessages.Deep(ex)); }
            try { DeliveryNoteVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: DeliveryNoteVM teardown failed: " + ExceptionMessages.Deep(ex)); }
            try { ProformaInvoiceVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: ProformaInvoiceVM teardown failed: " + ExceptionMessages.Deep(ex)); }
            try { TaxInvoiceVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainViewModel] Dispose: TaxInvoiceVM teardown failed: " + ExceptionMessages.Deep(ex)); }
            GC.SuppressFinalize(this);
        }
    }
}
