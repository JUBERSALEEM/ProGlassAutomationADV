// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/OrderReport/OrderReportModuleViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator (standalone module — Dispatch section)
//
// Owns the Report Generator list ↔ wizard toggle exactly like
// StatementModuleViewModel owns the Statement list ↔ generator:
//   ListVM      → OrderReportListView        (saved reports)
//   GeneratorVM → OrderReportGeneratorView   (4-step wizard)
//
// ERR-633EEB pattern: ONE real constructor + parameterless chaining so
// no construction route (DI, Activator fallback, design-time) can ever
// produce a half-built view model.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.OrderReport.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.OrderReport
{
    public class OrderReportModuleViewModel : ViewModelBase
    {
        public OrderReportListViewModel ListVM { get; }
        public OrderReportGeneratorViewModel GeneratorVM { get; }

        private bool _showList = true;
        public bool ShowList
        {
            get => _showList;
            set { if (_showList == value) return; _showList = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowGenerator)); }
        }
        public bool ShowGenerator => !_showList;

        public ICommand NewReportCommand { get; }
        public ICommand BackToListCommand { get; }

        // ── Warning/alert action callbacks (wired by MainViewModel) ─────
        public Action<string>? OnOpenProformaByNumber
        {
            get => GeneratorVM.OnOpenProformaByNumber;
            set => GeneratorVM.OnOpenProformaByNumber = value;
        }

        public Action<string>? OnOpenJobOrderByNumber
        {
            get => GeneratorVM.OnOpenJobOrderByNumber;
            set => GeneratorVM.OnOpenJobOrderByNumber = value;
        }

        public Action<string>? OnOpenTaxInvoiceByNumber
        {
            get => GeneratorVM.OnOpenTaxInvoiceByNumber;
            set => GeneratorVM.OnOpenTaxInvoiceByNumber = value;
        }

        public OrderReportModuleViewModel(
            IDialogService? dialogs,
            IOrderReportRepository? repo,
            IMasterDataRepository? masterData,
            IProformaInvoiceRepository? invoices,
            IDatabaseServices? database)
        {
            var sp = App.ServiceProvider;

            var dlg = dialogs
                ?? TryGet<IDialogService>(sp)
                ?? new DialogService();

            var reports = repo
                ?? TryGet<IOrderReportRepository>(sp)
                ?? new OrderReportRepository();

            var master = masterData
                ?? TryGet<IMasterDataRepository>(sp)
                ?? App.MasterData;

            var piRepo = invoices
                ?? TryGet<IProformaInvoiceRepository>(sp)
                ?? App.Invoices;

            var db = database ?? TryGet<IDatabaseServices>(sp);

            ListVM = new OrderReportListViewModel(dlg, reports, db!);
            GeneratorVM = new OrderReportGeneratorViewModel(dlg, master, piRepo, reports);

            ListVM.OnRequestNewReport = () =>
            {
                GeneratorVM.PrepareNew();
                ShowList = false;
            };
            ListVM.OnRequestOpenReport = item =>
            {
                GeneratorVM.PrepareEdit(item);
                ShowList = false;
            };
            GeneratorVM.OnReportSaved = async () =>
            {
                ShowList = true;
                await ListVM.LoadAsync();
            };
            GeneratorVM.OnCancelled = () => ShowList = true;

            NewReportCommand = new RelayCommand(_ =>
            {
                GeneratorVM.PrepareNew();
                ShowList = false;
            });
            BackToListCommand = new RelayCommand(_ => ShowList = true);

            _ = ListVM.LoadAsync();
        }

        /// <summary>Parameterless ctor for XAML design-time and the
        /// Activator.CreateInstance fallback — chains into the single real ctor.</summary>
        public OrderReportModuleViewModel() : this(null, null, null, null, null) { }

        private static T? TryGet<T>(IServiceProvider? sp) where T : class
        {
            if (sp == null) return null;
            try { return sp.GetService<T>(); }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Debug(
                    $"[OrderReportModule] DI resolve of {typeof(T).Name} failed: {ExceptionMessages.Deep(ex)}");
                return null;
            }
        }
    }
}
