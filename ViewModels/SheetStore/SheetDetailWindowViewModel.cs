// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetDetailWindowViewModel.cs
// MODULE: Inventory — full-screen detail workspace
// ADDED: 2026-09-06
//
// Opened by DOUBLE-CLICKING a row. This is the full window the owner
// asked for, not a dialog: maximized, its own chrome, tab switching
// across Overview / Edit / Movements / Analysis.
//
// TAB MODEL — one string, ActiveKey, is the entire selection state.
// Each tab button binds TabState.IsActive to "my key == ActiveKey", so
// two tabs cannot both be active: the invalid state is unrepresentable
// rather than merely avoided. Same shape as the shell nav's
// CurrentModuleKey and SalesReportViewModel.ActiveKey.
//
// FIELDS ARE ALWAYS EDITABLE — the owner's "all boxes by default".
// There is no separate edit mode to enter, so a single click switches
// tab and nothing else. Save is gated on validity, never advisory.
//
// DELETE IS NOT HERE as a peer of Save. Sitting a destructive,
// irreversible action beside the primary confirm on a form the operator
// tabs through is how rows get deleted by muscle memory. It stays in
// the list, where it is a considered choice against a named row.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    public class SheetDetailWindowViewModel : ViewModelBase
    {
        public const string TabOverview  = "Overview";
        public const string TabEdit      = "Edit";
        public const string TabMovements = "Movements";
        public const string TabAnalysis  = "Analysis";

        private readonly ISheetHistoryRepository _historyRepo;

        /// <param name="buyCommand">
        /// The LIST's buy/use commands, passed in rather than rebuilt. They
        /// already carry the permission check, the history write, the audit
        /// entry and the reload; duplicating that here would mean two code
        /// paths recording stock movements differently.
        /// </param>
        public SheetDetailWindowViewModel(GlassSheet? sheet,
                                          ISheetHistoryRepository? historyRepo = null,
                                          ICommand? buyCommand = null,
                                          ICommand? useCommand = null)
        {
            // A NULL sheet is ADD NEW, not an error: the window opens on the
            // Edit tab with an empty form. Overview / Movements / Analysis
            // are hidden, because a sheet that does not exist yet has no
            // stock, no history and no margin to report.
            IsNew = sheet == null;
            Sheet = sheet ?? new GlassSheet();
            _historyRepo = historyRepo ?? new SheetHistoryRepository();
            if (IsNew) _activeKey = TabEdit;
            BuyCommand = buyCommand;
            UseCommand = useCommand;

            // The Edit tab reuses the DIALOG's view-model. AddEditSheetView
            // is a Window and cannot be hosted in a ContentControl, but the
            // view-model has no such constraint — so the validation rules
            // stay in exactly one place and cannot drift between the two
            // surfaces.
            Editor = new AddEditSheetViewModel(sheet);

            SaveCommand = new RelayCommand(_ =>
            {
                // Materialise + re-validate inside the form. CanExecute is an
                // affordance, not a boundary — accelerators and automation
                // peers can still reach this.
                Editor.SaveCommand.Execute(null);
                if (Editor.DialogResult != true || Editor.Result == null) return;

                // The window does not write to the database itself. It raises
                // this and the list persists through the shared path, so the
                // ledger keeps exactly one writer.
                SaveRequested?.Invoke(this, Editor.Result);
                CloseAction?.Invoke();
            },
            _ => Editor.IsValid);

            SelectTabCommand = new RelayCommand(p =>
            {
                var key = p as string;
                if (string.IsNullOrEmpty(key) || key == _activeKey) return;
                ActiveKey = key!;
            });

            CloseCommand = new RelayCommand(_ => CloseAction?.Invoke());
        }

        public GlassSheet Sheet { get; }

        /// <summary>True when this is ADD NEW rather than an existing row.</summary>
        public bool IsNew { get; }

        public string SaveButtonText => IsNew ? "ADD SHEET" : "SAVE CHANGES";

        /// <summary>Only an existing sheet has stock, history and margin.</summary>
        public bool IsExisting => !IsNew;

        /// <summary>Raised with the validated sheet; the list persists it.</summary>
        public event EventHandler<GlassSheet>? SaveRequested;

        public ICommand SaveCommand { get; }

        /// <summary>The always-editable form behind the Edit tab.</summary>
        public AddEditSheetViewModel Editor { get; }

        /// <summary>Row actions, delegated to the list. May be null in design mode.</summary>
        public ICommand? BuyCommand { get; }
        public ICommand? UseCommand { get; }

        // ── Identity ───────────────────────────────────────────────────

        public string HeaderTitle => IsNew
            ? "New glass sheet"
            : $"{Sheet.Category} {Sheet.ThicknessDisplay} {Sheet.Color}".Trim();

        public string HeaderSubtitle => IsNew
            ? "Fill in the specification and save to add it to inventory."
            : $"{Sheet.SizeDisplay} mm  ·  {Sheet.SqmPerSheet:N4} SQM per sheet";

        // ── Tabs ───────────────────────────────────────────────────────

        private string _activeKey = TabOverview;
        public string ActiveKey
        {
            get => _activeKey;
            set
            {
                if (_activeKey == value) return;
                _activeKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverview));
                OnPropertyChanged(nameof(IsEdit));
                OnPropertyChanged(nameof(IsMovements));
                OnPropertyChanged(nameof(IsAnalysis));

                // Movements are fetched on first reveal, not on open: the
                // history table grows without bound and most visits to
                // this window never leave the Overview tab.
                if (_activeKey == TabMovements && !_historyLoaded)
                    AsyncUtility.SafeFireAndForget(LoadHistoryAsync, "SheetDetail LoadHistory");
            }
        }

        public bool IsOverview  => _activeKey == TabOverview;
        public bool IsEdit      => _activeKey == TabEdit;
        public bool IsMovements => _activeKey == TabMovements;
        public bool IsAnalysis  => _activeKey == TabAnalysis;

        public ICommand SelectTabCommand { get; }
        public ICommand CloseCommand { get; }

        // ── Movements ──────────────────────────────────────────────────

        private bool _historyLoaded;

        public ObservableCollection<SheetHistory> Movements { get; } = new();

        private bool _isLoadingHistory;
        public bool IsLoadingHistory
        {
            get => _isLoadingHistory;
            private set { _isLoadingHistory = value; OnPropertyChanged(); }
        }

        public bool HasNoMovements => _historyLoaded && Movements.Count == 0;

        private async Task LoadHistoryAsync()
        {
            IsLoadingHistory = true;
            try
            {
                var rows = await _historyRepo.GetBySheetIdAsync(Sheet.Id);

                UiDispatch.Run(() =>
                {
                    Movements.Clear();
                    foreach (var r in rows) Movements.Add(r);
                    _historyLoaded = true;
                    OnPropertyChanged(nameof(HasNoMovements));
                });
            }
            catch
            {
                // A failed history fetch must not take the window down —
                // Overview and Edit are still fully usable without it.
                UiDispatch.Run(() =>
                {
                    _historyLoaded = true;
                    OnPropertyChanged(nameof(HasNoMovements));
                });
            }
            finally { IsLoadingHistory = false; }
        }

        // ── Analysis ───────────────────────────────────────────────────

        public decimal StockValueAtCost => Sheet.TotalPurchaseValue;
        public decimal BalanceValueAtCost => Sheet.BalanceCostValue;
        public decimal BalanceValueAtSell => Sheet.BalanceValue;
        public decimal UsedValueAtCost => Sheet.UsedCostValue;

        public decimal PotentialProfit =>
            Sheet.BalanceValue - Sheet.BalanceCostValue;

        /// <summary>
        /// Margin on the remaining stock. Guarded against a zero cost
        /// basis, which is real: sheets are sometimes entered before a
        /// purchase price is known.
        /// </summary>
        public decimal MarginPercent => Sheet.BalanceCostValue > 0
            ? (PotentialProfit / Sheet.BalanceCostValue) * 100m
            : 0m;

        public bool HasMarginBasis => Sheet.BalanceCostValue > 0;

        public string MarginDisplay => HasMarginBasis
            ? $"{MarginPercent:N1}%"
            : "No cost basis";

        public decimal ConsumptionPercent => Sheet.Stock > 0
            ? ((decimal)Sheet.Used / Sheet.Stock) * 100m
            : 0m;

        public Action? CloseAction { get; set; }
    }
}
