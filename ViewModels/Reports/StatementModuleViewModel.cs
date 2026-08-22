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
using ProGlassAutomation.ViewModels.Reports.StatementGenerator;
using ProGlassAutomation.ViewModels.Reports.StatementList;
using System;
using System.Windows.Input;

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

        public StatementModuleViewModel()
        {
            var repo = new StatementRepository();
            ListVM = new StatementListViewModel(repo);
            GeneratorVM = new StatementGeneratorViewModel(repo);

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
    }
}
