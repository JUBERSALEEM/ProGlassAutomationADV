// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementModuleViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator (standalone module — Dispatch section)
//
// The interactive Statement Generator was previously buried inside the
// Reports hub (SalesReportViewModel) and only reachable as an overlay on
// the read-only "Statement" report. It is now its own module so it can sit
// in the Dispatch section alongside Delivery Note and Tax Invoice.
//
// Owns the Statement List ↔ Generator wizard toggle:
//   ListVM      → StatementListView   (saved statements)
//   GeneratorVM → StatementGeneratorView (4-step wizard)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.ViewModels.Reports.StatementGenerator;
using ProGlassAutomation.ViewModels.Reports.StatementList;
using System;
using ProGlassAutomation.Utilities;
using System.Windows.Input;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Services.Database;
namespace ProGlassAutomation.ViewModels.Reports
{
    /// <summary>
    /// Standalone Statement Generator module (list ↔ wizard), previously
    /// merged into the Reports hub.
    /// </summary>
    public class StatementModuleViewModel : ViewModelBase
    {
        // ── Statement List ↔ Generator ─────────────────────────────────
        public StatementListViewModel ListVM { get; }
        public StatementGeneratorViewModel GeneratorVM { get; }

        private bool _showList = true;
        public bool ShowList
        {
            get => _showList;
            set { if (_showList == value) return; _showList = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowGenerator)); }
        }
        public bool ShowGenerator => !_showList;

        public ICommand NewStatementCommand { get; }
        public ICommand BackToListCommand { get; }

        // ── Warning/alert action callbacks (delegate to GeneratorVM) ─────
        // MainViewModel wires these to navigate + open/create the related
        // document. Targets are document references (number / id).
        public Action<string>? OnOpenTaxInvoiceByNumber
        {
            get => GeneratorVM.OnOpenTaxInvoiceByNumber;
            set => GeneratorVM.OnOpenTaxInvoiceByNumber = value;
        }

        public Action<string>? OnCreateInvoiceFromDeliveryNote
        {
            get => GeneratorVM.OnCreateInvoiceFromDeliveryNote;
            set => GeneratorVM.OnCreateInvoiceFromDeliveryNote = value;
        }

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

        public Action<string>? OnPrepareDeliveryNote
        {
            get => GeneratorVM.OnPrepareDeliveryNote;
            set => GeneratorVM.OnPrepareDeliveryNote = value;
        }

        private readonly IDialogService _dialogs;

        // ═══════════════════════════════════════════════════════════════
        // SINGLE real constructor.
        //
        // ERR-633EEB (2026-09-02): the DI container resolved the
        // `StatementModuleViewModel(IDialogService)` overload, which set
        // only `_dialogs` and left ListVM / GeneratorVM / the commands
        // NULL. MainViewModel then assigned
        // `StatementModuleVM.OnOpenTaxInvoiceByNumber = ...`, whose setter
        // forwards to `GeneratorVM.OnOpenTaxInvoiceByNumber` → NRE →
        // MainWindow's XAML object graph failed to construct →
        // TargetInvocationException → fatal startup crash.
        //
        // There is now exactly ONE initialization path, so every
        // construction route (DI, Activator fallback, XAML design-time)
        // produces a fully-built view model. Optional dependencies fall
        // back to the App-level singletons.
        // ═══════════════════════════════════════════════════════════════
        public StatementModuleViewModel(
            IDialogService? dialogs,
            IStatementRepository? repo,
            IMasterDataRepository? masterData,
            IDatabaseServices? database)
        {
            var sp = App.ServiceProvider;

            _dialogs = dialogs
                ?? TryGet<IDialogService>(sp)
                ?? new DialogService();

            var statements = repo
                ?? TryGet<IStatementRepository>(sp)
                ?? new StatementRepository();

            var master = masterData
                ?? TryGet<IMasterDataRepository>(sp)
                ?? App.MasterData;

            var db = database ?? TryGet<IDatabaseServices>(sp);

            ListVM = new StatementListViewModel(_dialogs, statements, db!);
            GeneratorVM = new StatementGeneratorViewModel(_dialogs, master, statements, db!);

            ListVM.OnRequestNewStatement = () =>
            {
                GeneratorVM.PrepareNew();
                ShowList = false;
            };
            ListVM.OnRequestEditStatement = item =>
            {
                GeneratorVM.PrepareEdit(item);
                ShowList = false;
            };
            GeneratorVM.OnStatementSaved = async () =>
            {
                ShowList = true;
                await ListVM.LoadAsync();
            };
            GeneratorVM.OnCancelled = () => ShowList = true;

            NewStatementCommand = new RelayCommand(_ =>
            {
                GeneratorVM.PrepareNew();
                ShowList = false;
            });
            BackToListCommand = new RelayCommand(_ => ShowList = true);

            _ = ListVM.LoadAsync();
        }

        /// <summary>Parameterless ctor for XAML design-time and for
        /// MainViewModel's <c>Activator.CreateInstance&lt;T&gt;()</c> fallback.
        /// Chains into the single real ctor, so no construction route can
        /// ever produce a half-initialized instance (ERR-633EEB). The four
        /// parameters above are deliberately NOT optional: overlapping
        /// optional parameters would make <c>new StatementModuleViewModel()</c>
        /// resolve against two candidates.</summary>
        public StatementModuleViewModel() : this(null, null, null, null) { }

        /// <summary>DI lookup that never throws (the provider may be
        /// mid-build during startup, or the service simply unregistered).</summary>
        private static T? TryGet<T>(IServiceProvider? sp) where T : class
        {
            if (sp == null) return null;
            try { return sp.GetService<T>(); }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Debug(
                    $"[StatementModule] DI resolve of {typeof(T).Name} failed: {ExceptionMessages.Deep(ex)}");
                return null;
            }
        }
    }
}
