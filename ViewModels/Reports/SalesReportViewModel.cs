// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/SalesReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   Shell ViewModel for the Sales Report / Account Statement module.
//   Owns the top-level List ↔ Generator navigation toggle.
//   Mirrors TaxInvoiceViewModel pattern (Chat 14).
//
// STATES:
//   ShowList      = true  → StatementListView visible
//   ShowList      = false → StatementGeneratorView visible
//
// NAVIGATION:
//   List → Generator : NewStatementCommand or EditStatementCommand
//   Generator → List : StatementGeneratorVM fires OnStatementSaved callback
//   Generator → List : CancelCommand on generator
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.ViewModels.Reports.StatementGenerator;
using ProGlassAutomation.ViewModels.Reports.StatementList;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports
{
    public class SalesReportViewModel : ViewModelBase
    {
        // ── Child ViewModels ──────────────────────────────────────────────
        public StatementListViewModel ListVM { get; }
        public StatementGeneratorViewModel GeneratorVM { get; }

        // ── State ─────────────────────────────────────────────────────────
        private bool _showList = true;
        public bool ShowList
        {
            get => _showList;
            set
            {
                if (_showList == value) return;
                _showList = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowGenerator));
            }
        }

        public bool ShowGenerator => !_showList;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand NewStatementCommand { get; }
        public ICommand BackToListCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public SalesReportViewModel()
        {
            var repo = new StatementRepository();

            ListVM = new StatementListViewModel(repo);
            GeneratorVM = new StatementGeneratorViewModel(repo);

            // ── List → Generator navigation ───────────────────────────────
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

            // ── Generator → List navigation ───────────────────────────────
            GeneratorVM.OnStatementSaved = async () =>
            {
                ShowList = true;
                await ListVM.LoadAsync();
            };

            GeneratorVM.OnCancelled = () =>
            {
                ShowList = true;
            };

            // ── Commands ──────────────────────────────────────────────────
            NewStatementCommand = new RelayCommand(_ =>
            {
                GeneratorVM.PrepareNew();
                ShowList = false;
            });

            BackToListCommand = new RelayCommand(_ =>
            {
                ShowList = true;
            });

            // ── Initial load ──────────────────────────────────────────────
            _ = ListVM.LoadAsync();
        }
    }
}