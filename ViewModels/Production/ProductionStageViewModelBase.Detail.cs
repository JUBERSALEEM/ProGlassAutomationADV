// ═══════════════════════════════════════════════════════════════
// FILE: ProductionStageViewModelBase.Detail.cs
// MODULE: Production — full-page job detail: OUTER/INNER grids,
//         AutoSum footers, dispatch and delivery note (partial of
//         ProductionStageViewModelBase)
// ═══════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public partial class ProductionStageViewModelBase
    {
        // ── Detail panel (tier 2) ────────────────────────────────────────

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            private set { if (_hasSelection == value) return; _hasSelection = value; OnPropertyChanged(); }
        }

        private string _selectedJobNo = "";
        public string SelectedJobNo { get => _selectedJobNo; private set { if (_selectedJobNo == value) return; _selectedJobNo = value; OnPropertyChanged(); } }

        private string _selectedPiNo = "";
        public string SelectedPiNo { get => _selectedPiNo; private set { if (_selectedPiNo == value) return; _selectedPiNo = value; OnPropertyChanged(); } }

        private string _selectedCustomer = "";
        public string SelectedCustomer { get => _selectedCustomer; private set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnPropertyChanged(); } }

        private string _selectedProject = "";
        public string SelectedProject { get => _selectedProject; private set { if (_selectedProject == value) return; _selectedProject = value; OnPropertyChanged(); } }

        private string _selectedSalesman = "";
        public string SelectedSalesman { get => _selectedSalesman; private set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); } }

        private string _selectedSpecLabel = "";
        public string SelectedSpecLabel { get => _selectedSpecLabel; private set { if (_selectedSpecLabel == value) return; _selectedSpecLabel = value; OnPropertyChanged(); } }

        private string _selectedRef = "";
        public string SelectedRef { get => _selectedRef; private set { if (_selectedRef == value) return; _selectedRef = value; OnPropertyChanged(); } }

        private string _selectedSizeSummary = "";
        public string SelectedSizeSummary { get => _selectedSizeSummary; private set { if (_selectedSizeSummary == value) return; _selectedSizeSummary = value; OnPropertyChanged(); } }

        // ── Sidebar: row detail (Step 4/5 spec intelligence) ─────────────

        private string _selectedSizeRuleLabel = "";
        /// <summary>Overlap glass vs equal-size unit (multi-pane builds).</summary>
        public string SelectedSizeRuleLabel { get => _selectedSizeRuleLabel; private set { if (_selectedSizeRuleLabel == value) return; _selectedSizeRuleLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSizeRule)); } }

        public bool HasSizeRule => !string.IsNullOrWhiteSpace(_selectedSizeRuleLabel);

        private string _selectedTreatmentsLabel = "";
        /// <summary>Pane treatments from the Step 4 composition.</summary>
        public string SelectedTreatmentsLabel { get => _selectedTreatmentsLabel; private set { if (_selectedTreatmentsLabel == value) return; _selectedTreatmentsLabel = value; OnPropertyChanged(); } }

        private string _selectedArrivalLabel = "";
        /// <summary>When this unit was released to the floor.</summary>
        public string SelectedArrivalLabel { get => _selectedArrivalLabel; private set { if (_selectedArrivalLabel == value) return; _selectedArrivalLabel = value; OnPropertyChanged(); } }

        /// <summary>Pane-level stage items for the selected job (every unit's rows).</summary>
        public ObservableCollection<ProductionWorkItemRow> SelectedStageItems { get; } = new();

        // ── Detail grids (Step-5 cut-list style: OUTER left / INNER right) ─

        /// <summary>Outer panes still in progress at this stage (left grid).</summary>
        public ObservableCollection<ProductionWorkItemRow> OuterStageItems { get; } = new();

        /// <summary>Inner panes still in progress at this stage (right grid).</summary>
        public ObservableCollection<ProductionWorkItemRow> InnerStageItems { get; } = new();

        /// <summary>Panes fully completed — moved to the COMPLETED section.</summary>
        public ObservableCollection<ProductionWorkItemRow> CompletedStageItems { get; } = new();

        private bool _isSingleGlass;
        /// <summary>True when the job has a single glass pane (SGU) — one centered grid.</summary>
        public bool IsSingleGlass
        {
            get => _isSingleGlass;
            private set
            {
                if (_isSingleGlass == value) return;
                _isSingleGlass = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OuterGridTitle));
                OnPropertyChanged(nameof(OuterWHeader));
                OnPropertyChanged(nameof(OuterHHeader));
            }
        }

        public bool HasInnerGrid => InnerStageItems.Count > 0;
        public bool HasCompletedSection => CompletedStageItems.Count > 0;

        private string _selectedSpecName = "";
        public string SelectedSpecName
        {
            get => _selectedSpecName;
            private set { if (_selectedSpecName == value) return; _selectedSpecName = value; OnPropertyChanged(); OnPropertyChanged(nameof(OuterGridTitle)); }
        }

        /// <summary>Left grid title: "OUTER" for pane builds, the spec name for SGU.</summary>
        public string OuterGridTitle => IsSingleGlass
            ? (string.IsNullOrWhiteSpace(SelectedSpecName) ? "GLASS" : SelectedSpecName)
            : "OUTER";

        public string OuterWHeader => IsSingleGlass ? "W" : "W1";
        public string OuterHHeader => IsSingleGlass ? "H" : "H1";

        // ── AutoSum footers (column-end totals per grid) ────────────────

        private int _outerTotalSum, _outerQtySum, _outerDoneSum, _outerBalanceSum;
        public int OuterTotalSum { get => _outerTotalSum; private set { if (_outerTotalSum == value) return; _outerTotalSum = value; OnPropertyChanged(); } }
        public int OuterQtySum { get => _outerQtySum; private set { if (_outerQtySum == value) return; _outerQtySum = value; OnPropertyChanged(); } }
        public int OuterDoneSum { get => _outerDoneSum; private set { if (_outerDoneSum == value) return; _outerDoneSum = value; OnPropertyChanged(); } }
        public int OuterBalanceSum { get => _outerBalanceSum; private set { if (_outerBalanceSum == value) return; _outerBalanceSum = value; OnPropertyChanged(); } }

        private int _innerTotalSum, _innerQtySum, _innerDoneSum, _innerBalanceSum;
        public int InnerTotalSum { get => _innerTotalSum; private set { if (_innerTotalSum == value) return; _innerTotalSum = value; OnPropertyChanged(); } }
        public int InnerQtySum { get => _innerQtySum; private set { if (_innerQtySum == value) return; _innerQtySum = value; OnPropertyChanged(); } }
        public int InnerDoneSum { get => _innerDoneSum; private set { if (_innerDoneSum == value) return; _innerDoneSum = value; OnPropertyChanged(); } }
        public int InnerBalanceSum { get => _innerBalanceSum; private set { if (_innerBalanceSum == value) return; _innerBalanceSum = value; OnPropertyChanged(); } }

        private int _completedTotalSum, _completedDoneSum;
        public int CompletedTotalSum { get => _completedTotalSum; private set { if (_completedTotalSum == value) return; _completedTotalSum = value; OnPropertyChanged(); } }
        public int CompletedDoneSum { get => _completedDoneSum; private set { if (_completedDoneSum == value) return; _completedDoneSum = value; OnPropertyChanged(); } }

        // ── Main-list AutoSum footer ──────────────────────────────────────

        private int _listTotalSum, _listDoneSum, _listBalanceSum;
        public int ListTotalSum { get => _listTotalSum; private set { if (_listTotalSum == value) return; _listTotalSum = value; OnPropertyChanged(); } }
        public int ListDoneSum { get => _listDoneSum; private set { if (_listDoneSum == value) return; _listDoneSum = value; OnPropertyChanged(); } }
        public int ListBalanceSum { get => _listBalanceSum; private set { if (_listBalanceSum == value) return; _listBalanceSum = value; OnPropertyChanged(); } }

        // ── Full-page job detail (double-click opens, back returns) ──────

        private bool _isDetailOpen;
        /// <summary>True while the full-page job detail overlay is showing.</summary>
        public bool IsDetailOpen
        {
            get => _isDetailOpen;
            private set { if (_isDetailOpen == value) return; _isDetailOpen = value; OnPropertyChanged(); }
        }

        private string _selectedSerialLabel = "—";
        public string SelectedSerialLabel { get => _selectedSerialLabel; private set { if (_selectedSerialLabel == value) return; _selectedSerialLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitTotalLabel = "0";
        /// <summary>Job TOTAL pieces (outer + inner summed) for the KPI panel.</summary>
        public string SelectedUnitTotalLabel { get => _selectedUnitTotalLabel; private set { if (_selectedUnitTotalLabel == value) return; _selectedUnitTotalLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitDoneLabel = "0";
        public string SelectedUnitDoneLabel { get => _selectedUnitDoneLabel; private set { if (_selectedUnitDoneLabel == value) return; _selectedUnitDoneLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitBalanceLabel = "0";
        public string SelectedUnitBalanceLabel { get => _selectedUnitBalanceLabel; private set { if (_selectedUnitBalanceLabel == value) return; _selectedUnitBalanceLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitCompletedPctLabel = "0%";
        public string SelectedUnitCompletedPctLabel { get => _selectedUnitCompletedPctLabel; private set { if (_selectedUnitCompletedPctLabel == value) return; _selectedUnitCompletedPctLabel = value; OnPropertyChanged(); } }

        /// <summary>Floor audit trail for the selected job at this stage.</summary>
        public ObservableCollection<string> DetailEntries { get; } = new();

        private bool _hasDetailEntries;
        public bool HasDetailEntries { get => _hasDetailEntries; private set { if (_hasDetailEntries == value) return; _hasDetailEntries = value; OnPropertyChanged(); } }

        public ICommand OpenUnitCommand { get; }
        public ICommand CloseDetailCommand { get; }

        /// <summary>Saves every editable DONE cell in the detail grid in one batch.</summary>
        public ICommand SaveAllDoneCommand { get; }

        // ── Sidebar: stage totals (total / done / balance in the stage's
        //    metrics — pieces, SQM and LM, every module) ─────────────────

        private string _selectedPcsSummary = "—";
        public string SelectedPcsSummary { get => _selectedPcsSummary; private set { if (_selectedPcsSummary == value) return; _selectedPcsSummary = value; OnPropertyChanged(); } }

        private string _selectedSqmSummary = "—";
        public string SelectedSqmSummary { get => _selectedSqmSummary; private set { if (_selectedSqmSummary == value) return; _selectedSqmSummary = value; OnPropertyChanged(); } }

        private string _selectedLmSummary = "—";
        public string SelectedLmSummary { get => _selectedLmSummary; private set { if (_selectedLmSummary == value) return; _selectedLmSummary = value; OnPropertyChanged(); } }

        // ── Sidebar: dispatch panel (finished pieces → delivery notes) ──

        private string _selectedDispatchReadyLabel = "0";
        public string SelectedDispatchReadyLabel { get => _selectedDispatchReadyLabel; private set { if (_selectedDispatchReadyLabel == value) return; _selectedDispatchReadyLabel = value; OnPropertyChanged(); } }

        private string _selectedDispatchDeliveredLabel = "0";
        public string SelectedDispatchDeliveredLabel { get => _selectedDispatchDeliveredLabel; private set { if (_selectedDispatchDeliveredLabel == value) return; _selectedDispatchDeliveredLabel = value; OnPropertyChanged(); } }

        private string _selectedDispatchBalanceLabel = "0";
        public string SelectedDispatchBalanceLabel { get => _selectedDispatchBalanceLabel; private set { if (_selectedDispatchBalanceLabel == value) return; _selectedDispatchBalanceLabel = value; OnPropertyChanged(); } }

        private bool _hasDispatchPanel;
        public bool HasDispatchPanel { get => _hasDispatchPanel; private set { if (_hasDispatchPanel == value) return; _hasDispatchPanel = value; OnPropertyChanged(); } }

        private int _dispatchBalance;
        /// <summary>Ready-but-undelivered pieces (drives Create DN).</summary>
        public int DispatchBalance { get => _dispatchBalance; private set { if (_dispatchBalance == value) return; _dispatchBalance = value; OnPropertyChanged(); } }

        // ── Pipeline chain bar ───────────────────────────────────────────

        /// <summary>
        /// The eight-stage chain, in floor order. Shared metadata with the
        /// stage VMs (same names, same Segoe MDL2 glyphs).
        /// </summary>
        private static readonly (ProductionStage Stage, string NavKey, string Name, string Icon)[] ChainMetadata =
        {
            (ProductionStage.Cutting,             "Cutting",             "Cutting",         "\uE8C6"),
            (ProductionStage.HolesCutouts,        "HolesCutouts",        "Holes & Cutouts", "\uE71D"),
            (ProductionStage.Polish,              "Polish",              "Polish",          "\uE895"),
            (ProductionStage.EdgeDeletion,        "EdgeDeletion",        "Edge Deletion",   "\uE774"),
            (ProductionStage.Mitring,             "Mitring",             "Mitring",         "\uE7E8"),
            (ProductionStage.Tempering,           "Tempering",           "Tempering",       "\uE728"),
            (ProductionStage.Pairing,             "Pairing",             "Pairing",         "\uE71B"),
            (ProductionStage.DoublingLamination,  "DoublingLamination",  "Double / Lam",    "\uE81C")
        };

        /// <summary>Whole-chain chips, refreshed with every list/KPI load.</summary>
        public ObservableCollection<ProductionChainStageChip> ChainStages { get; } = new();

        private string _chainProgressLabel = "";
        /// <summary>Factory-wide aggregate under the chain bar:
        /// "Floor · 12 open · 45 / 57 done".</summary>
        public string ChainProgressLabel
        {
            get => _chainProgressLabel;
            private set { if (_chainProgressLabel == value) return; _chainProgressLabel = value; OnPropertyChanged(); }
        }

        private async Task LoadChainAsync()
        {
            var rows = await Task.Run(ProductionRepository.GetChainSummaryAsync)
                .ConfigureAwait(true);

            var byStage = rows.ToDictionary(r => r.Stage);

            ChainStages.Clear();
            int ordinal = 0;
            int totalOpen = 0, totalDone = 0, totalAll = 0;
            foreach (var (stage, navKey, name, icon) in ChainMetadata)
            {
                ordinal++;
                byStage.TryGetValue((int)stage, out var counts);
                int open = counts?.Open ?? 0;
                int done = counts?.Completed ?? 0;
                int all = counts?.Total ?? 0;
                totalOpen += open;
                totalDone += done;
                totalAll += all;
                ChainStages.Add(new ProductionChainStageChip
                {
                    Ordinal = ordinal,
                    NavKey = navKey,
                    Name = name,
                    Icon = icon,
                    Open = open,
                    Completed = done,
                    Total = all,
                    UnitLabel = stage is ProductionStage.Pairing or ProductionStage.DoublingLamination
                        ? "units" : "pcs",
                    IsCurrent = stage == Stage
                });
            }

            ChainProgressLabel = totalAll == 0
                ? "No work on the floor yet"
                : $"{totalOpen.ToString(CultureInfo.InvariantCulture)} open · " +
                  $"{totalDone.ToString(CultureInfo.InvariantCulture)} / " +
                  $"{totalAll.ToString(CultureInfo.InvariantCulture)} done";
        }

        // ── Detail panel ─────────────────────────────────────────────────

        private void RefreshDetailPanel()
        {
            var row = _selectedRow;
            if (row == null)
            {
                ClearDetailPanel();
                return;
            }

            // The detail carries the WHOLE job: every arrived unit's pane
            // rows are flattened into the OUTER / INNER cut-list grids —
            // this is the real per-piece data the floor edits and saves.
            var units = row.Units;
            HasSelection = true;
            SelectedJobNo = row.JobRef;
            SelectedPiNo = row.PiNo;
            SelectedCustomer = row.Customer;
            SelectedSalesman = row.Salesman;

            int unitCount = units.Count;
            int specCount = units.Select(u => u.SpecGroupNumber).Distinct().Count();
            SelectedSpecLabel = unitCount == 0
                ? ""
                : $"{unitCount.ToString(CultureInfo.InvariantCulture)} unit(s) · " +
                  $"{specCount.ToString(CultureInfo.InvariantCulture)} spec(s)";
            SelectedSerialLabel = row.JobRef;   // header badge shows the job number

            var items = units.SelectMany(u => u.FlowItems).ToList();
            var first = items.FirstOrDefault()?.Item;
            SelectedProject = first?.ProjectName ?? "";
            SelectedRef = "";

            // Single-glass job → one centered grid; pane builds → OUTER/INNER.
            bool hasInner = items.Any(f => IsInnerPosition(f.Item.Position));
            IsSingleGlass = !hasInner;
            SelectedSpecName = specCount == 1 ? (first?.SpecName ?? "") : "";

            SelectedStageItems.Clear();
            foreach (var flowItem in items)
                SelectedStageItems.Add(new ProductionWorkItemRow(flowItem));

            RebuildUnitPanels();
            AsyncUtility.InvalidateCommandsThrottled();

            // Job totals — total / done / balance in pieces, SQM and LM.
            int pcsTotal = 0, pcsDone = 0;
            decimal sqmTotal = 0m, sqmDone = 0m, lmTotal = 0m, lmDone = 0m;
            foreach (var fi in items)
            {
                int inflow = fi.Inflow;
                int done = fi.Done;
                decimal sqmPerPiece = fi.Item.UnitSqm;
                decimal lmPerPiece = fi.Item.Qty > 0 ? fi.Item.TotalLm / fi.Item.Qty : 0m;

                pcsTotal += inflow;
                pcsDone += done;
                sqmTotal += sqmPerPiece * inflow;
                sqmDone += sqmPerPiece * done;
                lmTotal += lmPerPiece * inflow;
                lmDone += lmPerPiece * done;
            }

            SelectedPcsSummary = Summary(pcsTotal, pcsDone, "");
            SelectedSqmSummary = Summary(Math.Round(sqmTotal, 2, MidpointRounding.AwayFromZero),
                Math.Round(sqmDone, 2, MidpointRounding.AwayFromZero), " m²");
            SelectedLmSummary = Summary(Math.Round(lmTotal, 2, MidpointRounding.AwayFromZero),
                Math.Round(lmDone, 2, MidpointRounding.AwayFromZero), " LM");

            // Full-page KPI panel: job TOTAL / DONE / BALANCE / COMPLETED %.
            SelectedUnitTotalLabel = pcsTotal.ToString(CultureInfo.InvariantCulture);
            SelectedUnitDoneLabel = pcsDone.ToString(CultureInfo.InvariantCulture);
            SelectedUnitBalanceLabel = (pcsTotal - pcsDone).ToString(CultureInfo.InvariantCulture);
            SelectedUnitCompletedPctLabel = pcsTotal == 0 ? "0%"
                : ((double)pcsDone / pcsTotal * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";

            // Floor audit trail for the whole job at this stage.
            AsyncUtility.SafeFireAndForget(RefreshDetailEntriesAsync, "Production entry log");

            // Dispatch figures — finished pieces flow into delivery notes.
            AsyncUtility.SafeFireAndForget(
                () => RefreshDispatchAsync(row.JobRef),
                "Production dispatch panel");
        }

        private static bool IsInnerPosition(string position) =>
            string.Equals(position, "Inner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "Lam Inner", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Distributes the selected job's panes into the OUTER / INNER grids
        /// (fully completed panes move to the COMPLETED section) and computes
        /// the AutoSum footers for each grid.
        /// </summary>
        private void RebuildUnitPanels()
        {
            OuterStageItems.Clear();
            InnerStageItems.Clear();
            CompletedStageItems.Clear();

            int outerIdx = 0, innerIdx = 0, doneIdx = 0;
            int ot = 0, oq = 0, od = 0, ob = 0;
            int it = 0, iq = 0, id = 0, ib = 0;
            int ct = 0, cd = 0;

            foreach (var row in SelectedStageItems)
            {
                if (row.IsCompleted)
                {
                    row.RowIndex = ++doneIdx;
                    CompletedStageItems.Add(row);
                    ct += row.Inflow;
                    cd += row.Done;
                }
                else if (IsInnerPosition(row.PositionKey))
                {
                    row.RowIndex = ++innerIdx;
                    InnerStageItems.Add(row);
                    it += row.Inflow;
                    iq += row.Item.Qty;
                    id += row.Done;
                    ib += row.Balance;
                }
                else
                {
                    row.RowIndex = ++outerIdx;
                    OuterStageItems.Add(row);
                    ot += row.Inflow;
                    oq += row.Item.Qty;
                    od += row.Done;
                    ob += row.Balance;
                }
            }

            OuterTotalSum = ot; OuterQtySum = oq; OuterDoneSum = od; OuterBalanceSum = ob;
            InnerTotalSum = it; InnerQtySum = iq; InnerDoneSum = id; InnerBalanceSum = ib;
            CompletedTotalSum = ct; CompletedDoneSum = cd;

            OnPropertyChanged(nameof(HasInnerGrid));
            OnPropertyChanged(nameof(HasCompletedSection));
        }

        /// <summary>
        /// Saves every editable DONE cell in the detail grid as one batch —
        /// the AutoSum footers, KPI cards and main list update on reload.
        /// </summary>
        private async Task SaveAllDoneAsync()
        {
            var rows = SelectedStageItems.Where(i => i.CanWork).ToList();
            if (rows.Count == 0) return;

            var updates = new List<(int ItemId, int RequestedQty)>();
            foreach (var row in rows)
            {
                string raw = (row.DoneInput ?? "").Trim();
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested))
                {
                    ActionWarning = $"Enter a whole number for {row.PaneTitle}.";
                    return;
                }
                if (requested == row.Done) continue;
                updates.Add((row.Id, requested));
            }

            if (updates.Count == 0) return;

            var (ok, messages) = await Task.Run(() =>
                ProductionRepository.SetDoneQtyBatchAsync(updates))
                .ConfigureAwait(true);

            if (!ok)
            {
                ActionWarning = messages.Count > 0 ? messages[0] : "Could not save done quantities.";
                return;
            }
            if (messages.Count > 0)
                ActionWarning = messages[messages.Count - 1];

            // Reload: KPI strip, AutoSum footers, COMPLETED section and the
            // main list all refresh from the database.
            await LoadAllAsync().ConfigureAwait(true);
        }

        private static string Summary(int total, int done, string suffix) =>
            $"{total.ToString(CultureInfo.InvariantCulture)}{suffix} · " +
            $"{done.ToString(CultureInfo.InvariantCulture)}{suffix} done · " +
            $"{(total - done).ToString(CultureInfo.InvariantCulture)}{suffix} left";

        private static string Summary(decimal total, decimal done, string suffix) =>
            $"{total.ToString("0.##", CultureInfo.InvariantCulture)}{suffix} · " +
            $"{done.ToString("0.##", CultureInfo.InvariantCulture)}{suffix} done · " +
            $"{(total - done).ToString("0.##", CultureInfo.InvariantCulture)}{suffix} left";

        private async Task RefreshDispatchAsync(string jobRef)
        {
            var (ready, delivered, balance, managed) = await Task.Run(() =>
                ProductionDeliveryGateway.GetJobDispatchStateAsync(jobRef))
                .ConfigureAwait(true);

            // Selection may have moved on while loading.
            if (_selectedRow == null || _selectedRow.JobRef != jobRef) return;

            HasDispatchPanel = managed;
            SelectedDispatchReadyLabel = ready.ToString(CultureInfo.InvariantCulture);
            SelectedDispatchDeliveredLabel = delivered.ToString(CultureInfo.InvariantCulture);
            SelectedDispatchBalanceLabel = balance.ToString(CultureInfo.InvariantCulture);
            DispatchBalance = balance;
            AsyncUtility.InvalidateCommandsThrottled();
        }

        /// <summary>
        /// Creates (or reopens) the Dispatching delivery note for the
        /// selected Job Order — it opens with remaining balance
        /// only, capped to the pieces production has finished.
        /// </summary>
        private async Task CreateDeliveryNoteAsync()
        {
            string jobRef = SelectedJobNo;
            if (string.IsNullOrWhiteSpace(jobRef) || OnCreateDeliveryNote == null) return;

            // DONE ≠ DELIVERED: finished pieces only leave on a delivery
            // note. With nothing ready, explain instead of doing nothing.
            if (DispatchBalance <= 0)
            {
                ActionWarning = "Nothing ready to deliver — pieces finished in production (DONE) are only marked DELIVERED once a delivery note ships them.";
                return;
            }

            string message = await OnCreateDeliveryNote(jobRef).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(message))
                ActionWarning = message;
        }

        private void ClearDetailPanel()
        {
            IsDetailOpen = false;   // selection is gone — close the full-page detail
            HasSelection = false;
            OuterStageItems.Clear();
            InnerStageItems.Clear();
            CompletedStageItems.Clear();
            OuterTotalSum = OuterQtySum = OuterDoneSum = OuterBalanceSum = 0;
            InnerTotalSum = InnerQtySum = InnerDoneSum = InnerBalanceSum = 0;
            CompletedTotalSum = CompletedDoneSum = 0;
            OnPropertyChanged(nameof(HasInnerGrid));
            OnPropertyChanged(nameof(HasCompletedSection));
            SelectedSpecName = "";
            IsSingleGlass = false;
            SelectedJobNo = "";
            SelectedPiNo = "";
            SelectedCustomer = "";
            SelectedProject = "";
            SelectedSalesman = "";
            SelectedSpecLabel = "";
            SelectedRef = "";
            SelectedSizeSummary = "";
            SelectedSizeRuleLabel = "";
            SelectedTreatmentsLabel = "";
            SelectedArrivalLabel = "";
            SelectedStageItems.Clear();
            SelectedPcsSummary = "—";
            SelectedSqmSummary = "—";
            SelectedLmSummary = "—";
            SelectedSerialLabel = "—";
            SelectedUnitTotalLabel = "0";
            SelectedUnitDoneLabel = "0";
            SelectedUnitBalanceLabel = "0";
            SelectedUnitCompletedPctLabel = "0%";
            DetailEntries.Clear();
            HasDetailEntries = false;
            HasDispatchPanel = false;
            SelectedDispatchReadyLabel = "0";
            SelectedDispatchDeliveredLabel = "0";
            SelectedDispatchBalanceLabel = "0";
            DispatchBalance = 0;
            AsyncUtility.InvalidateCommandsThrottled();
        }

        /// <summary>Opens the full-page job detail (double-click on a job row).</summary>
        private void OpenUnit(ProductionListRow? row)
        {
            if (row == null) return;
            SelectedRow = row;   // refreshes the detail panel state
            IsDetailOpen = true;
        }

        /// <summary>Live-refresh hook: re-reads everything after a floor write.</summary>
        private void OnWorkItemsChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            void Reload() => AsyncUtility.SafeFireAndForget(LoadAllAsync, "Production live refresh");
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(new Action(Reload));
            else
                Reload();
        }

        /// <summary>
        /// Loads the floor audit trail for the selected job at this stage:
        /// each "mark done" save — who, when and how many — newest first.
        /// </summary>
        private async Task RefreshDetailEntriesAsync()
        {
            var row = _selectedRow;
            if (row == null) return;

            var entries = await Task.Run(() =>
                ProductionRepository.GetJobEntriesAsync(row.JobRef, Stage))
                .ConfigureAwait(true);

            // Selection may have moved on while loading.
            if (_selectedRow == null || _selectedRow.RowKey != row.RowKey) return;

            DetailEntries.Clear();
            foreach (var e in entries)
            {
                string position = string.IsNullOrWhiteSpace(e.Position) ? "" : e.Position + " · ";
                string who = string.IsNullOrWhiteSpace(e.DoneBy) ? "operator" : e.DoneBy;
                DetailEntries.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} pcs marked done · {1}{2} · {3}",
                    e.Qty, position, who,
                    e.DoneAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)));
            }
            HasDetailEntries = DetailEntries.Count > 0;
        }
    }
}
