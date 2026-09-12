// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionStageViewModelBase.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — shared stage view model (piece flow, 2026-09-11)
//
// Base for the eight production stage modules. Production moves piece by
// piece, like a tool chain:
//
//   Cutting → Holes / Polish / Edge Deletion / Mitring → Tempering →
//   Pairing → Doubling / Lamination
//
// Each stage item tracks DONE pieces (QtyDone); only the completed
// quantity arrives in the next module. Every module screen is a
// two-tier master–detail:
//
//   TIER 1 — UNIT LIST (main stage screen): one row per unit with
//            SR · JOB NO · CUSTOMER · TOTAL · DONE · BALANCE plus a
//            status chip. Double-click opens the full-page detail.
//
//   TIER 2 — FULL-PAGE UNIT DETAIL: top completed-KPI strip, a card
//            per pane (Outer / Inner — single glass shows the spec
//            name) with W1×H1 (and W2/H2), per-pane TOTAL/DONE/BALANCE,
//            a Qty Done input + Save, unit totals, and the recent
//            entries log (who marked done, when). Completed pieces flow
//            into the next stage automatically; the main list
//            live-refreshes on every floor entry.
//
// See docs/PRODUCTION_SCREENS_DESIGN_2026-09-11.md for the full design.
// ═══════════════════════════════════════════════════════════════════════

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
    /// <summary>
    /// One pane-level stage item in the detail panel with its piece flow
    /// (arrived / done / balance) and the floor's done-qty editor state.
    /// </summary>
    public sealed class ProductionWorkItemRow : ViewModelBase
    {
        private readonly ProductionRepository.ProductionFlowItem _flow;

        public ProductionWorkItemRow(ProductionRepository.ProductionFlowItem flow)
        {
            _flow = flow;
            _doneInput = Done.ToString(CultureInfo.InvariantCulture);
        }

        public SavedProductionWorkItem Item => _flow.Item;
        public int Id => _flow.Item.Id;

        public string Position => string.IsNullOrWhiteSpace(_flow.Item.Position) ? "—" : _flow.Item.Position;
        public string Treatment => string.IsNullOrWhiteSpace(_flow.Item.Treatment) ? "—" : _flow.Item.Treatment;

        public string SizeLabel => _flow.Item.WidthMm > 0 && _flow.Item.HeightMm > 0
            ? $"{_flow.Item.WidthMm.ToString(CultureInfo.InvariantCulture)} × {_flow.Item.HeightMm.ToString(CultureInfo.InvariantCulture)} mm"
            : "—";

        /// <summary>
        /// Card title: OUTER / INNER for pane builds; the spec name
        /// directly for single-glass units (SGU).
        /// </summary>
        public string PaneTitle
        {
            get
            {
                string p = _flow.Item.Position ?? "";
                if (string.Equals(p, "Unit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p, "Glass", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(p))
                    return string.IsNullOrWhiteSpace(_flow.Item.SpecName) ? "Glass" : _flow.Item.SpecName;
                return p;
            }
        }

        /// <summary>Spec + treatment + glass ref, only where not already the title.</summary>
        public string PaneSubtitle
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(_flow.Item.SpecName) &&
                    !string.Equals(PaneTitle, _flow.Item.SpecName, StringComparison.OrdinalIgnoreCase))
                    parts.Add(_flow.Item.SpecName);
                if (!string.IsNullOrWhiteSpace(_flow.Item.Treatment))
                    parts.Add(_flow.Item.Treatment);
                if (!string.IsNullOrWhiteSpace(_flow.Item.GlassRef))
                    parts.Add("Ref " + _flow.Item.GlassRef);
                return parts.Count == 0 ? "" : string.Join(" · ", parts);
            }
        }

        public string QtyLabel => _flow.Item.Qty > 0
            ? _flow.Item.Qty.ToString(CultureInfo.InvariantCulture) : "—";

        // ── Grid columns (Step-5 cut-list style) ─────────────────────────

        /// <summary>Row number within its grid (1-based, assigned on populate).</summary>
        public int RowIndex { get; set; }

        /// <summary>The unit serial — SR shown in the detail header.</summary>
        public int SerialNumber => _flow.Item.SerialNumber;

        public string GlassRef => string.IsNullOrWhiteSpace(_flow.Item.GlassRef)
            ? "—" : _flow.Item.GlassRef;

        public string WLabel => _flow.Item.WidthMm > 0
            ? _flow.Item.WidthMm.ToString(CultureInfo.InvariantCulture) : "—";

        public string HLabel => _flow.Item.HeightMm > 0
            ? _flow.Item.HeightMm.ToString(CultureInfo.InvariantCulture) : "—";

        /// <summary>Raw pane position for OUTER/INNER grouping.</summary>
        public string PositionKey => _flow.Item.Position ?? "";

        /// <summary>Fully completed at this stage → moves to the COMPLETED section.</summary>
        public bool IsCompleted => Done >= Inflow && Inflow > 0;

        public string ChargeLabel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_flow.Item.ChargeTemplateKey)) return "";
                string label = _flow.Item.ChargeTemplateKey.Replace('_', ' ');
                if (_flow.Item.ChargeMetricQty > 0)
                {
                    label += $" · {_flow.Item.ChargeMetricQty.ToString("0.###", CultureInfo.InvariantCulture)}" +
                        $" {_flow.Item.ChargeMetricUnit}";
                }
                return label;
            }
        }

        // ── Piece flow ───────────────────────────────────────────────────

        public int Inflow => _flow.Inflow;
        public int Done => _flow.Done;
        public int Balance => _flow.Balance;

        public string InflowLabel => Inflow.ToString(CultureInfo.InvariantCulture);
        public string DoneLabel => Done.ToString(CultureInfo.InvariantCulture);
        public string BalanceLabel => Balance.ToString(CultureInfo.InvariantCulture);

        public string FlowSummaryLabel =>
            $"{Done.ToString(CultureInfo.InvariantCulture)}/{Inflow.ToString(CultureInfo.InvariantCulture)} done";

        public string DetailLabel
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(_flow.Item.PolishSubTypeLabel)) parts.Add(_flow.Item.PolishSubTypeLabel);
                if (!string.IsNullOrWhiteSpace(_flow.Item.Notes)) parts.Add(_flow.Item.Notes);
                return parts.Count == 0 ? "—" : string.Join(" · ", parts);
            }
        }

        public WorkItemStatus Status =>
            Done == 0 ? WorkItemStatus.Pending
            : Done >= Inflow && Inflow > 0 ? WorkItemStatus.Completed
            : WorkItemStatus.InProgress;

        public string StatusLabel => Status switch
        {
            WorkItemStatus.Pending => Inflow == 0 ? "WAITING UPSTREAM" : "PENDING",
            WorkItemStatus.InProgress => "IN PROGRESS",
            WorkItemStatus.Completed => "COMPLETED",
            _ => ""
        };

        public bool CanWork => Inflow > 0;

        /// <summary>Operator who last completed this item (empty if never).</summary>
        public string DoneBy => string.IsNullOrWhiteSpace(_flow.Item.CompletedBy)
            ? "—" : _flow.Item.CompletedBy;

        public string CompletedAtLabel => _flow.Item.CompletedAt == null
            ? "—"
            : _flow.Item.CompletedAt.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

        // ── Done-qty editor ──────────────────────────────────────────────

        private string _doneInput;
        /// <summary>Floor input: pieces completed at this stage.</summary>
        public string DoneInput
        {
            get => _doneInput;
            set { if (_doneInput == value) return; _doneInput = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// One master-list row: a unit with its full Step-5 envelope and this
    /// stage's piece flow (IN / DONE / BALANCE).
    /// </summary>
    public sealed class ProductionListRow
    {
        /// <summary>Unit identity — JobRef|Spec|Serial.</summary>
        public string RowKey { get; init; } = "";

        public int SpecGroupNumber { get; init; }
        public int SerialNumber { get; init; }

        // ── Shared columns ───────────────────────────────────────────────
        public string StatusGlyph { get; init; } = "";
        public string StatusLabel { get; init; } = "";
        public string JobRef { get; init; } = "";
        public string PiNo { get; init; } = "";
        public string Customer { get; init; } = "";
        public string Salesman { get; init; } = "";
        public string SpecLabel { get; init; } = "";
        public string SerialLabel { get; init; } = "—";
        public string GlassRef { get; init; } = "";
        public string BuildType { get; init; } = "";

        // ── Unit envelope — full Step 5 shape, per pane, no pricing ─────
        public string W1Label { get; init; } = "—";
        public string H1Label { get; init; } = "—";
        public string W2Label { get; init; } = "—";
        public string H2Label { get; init; } = "—";
        public string QtyLabel { get; init; } = "—";

        /// <summary>Step 4/5 size rule badge for multi-pane builds.</summary>
        public string SizeRuleLabel { get; init; } = "";
        public string Sqm1Label { get; init; } = "—";
        public string TotalSqm1Label { get; init; } = "—";
        public string Lm1Label { get; init; } = "—";
        public string Sqm2Label { get; init; } = "—";
        public string TotalSqm2Label { get; init; } = "—";
        public string Lm2Label { get; init; } = "—";

        // ── This stage's piece flow ──────────────────────────────────────
        public string InflowLabel { get; init; } = "0";
        public string DoneLabel { get; init; } = "0";
        public string BalanceLabel { get; init; } = "0";
        public string WaitingOn { get; init; } = "";

        /// <summary>This stage's per-item flows (detail panel source).</summary>
        public List<ProductionRepository.ProductionFlowItem> FlowItems { get; init; } = new();
    }

    /// <summary>
    /// One chip in the pipeline chain bar. Every stage module renders the
    /// whole eight-stage chain so the floor can follow work from Cutting
    /// through to Doubling/Lamination; the current stage is highlighted
    /// and any chip navigates to its module.
    /// </summary>
    public sealed class ProductionChainStageChip
    {
        public int Ordinal { get; init; }
        public string NavKey { get; init; } = "";
        public string Name { get; init; } = "";
        public string Icon { get; init; } = "";

        /// <summary>Pieces arrived but not yet done (actionable now).</summary>
        public int Open { get; init; }
        public int Completed { get; init; }
        public int Total { get; init; }

        /// <summary>"pcs" for pane stages, "units" for pairing/assembly.</summary>
        public string UnitLabel { get; init; } = "pcs";

        public bool HasWork => Total > 0;
        public bool IsCleared => Total > 0 && Completed == Total;
        public bool IsCurrent { get; init; }

        public string CountLabel => Open > 0
            ? $"{Open.ToString(CultureInfo.InvariantCulture)} open · " +
              $"{Completed.ToString(CultureInfo.InvariantCulture)}/{Total.ToString(CultureInfo.InvariantCulture)} {UnitLabel}"
            : $"{Completed.ToString(CultureInfo.InvariantCulture)}/{Total.ToString(CultureInfo.InvariantCulture)} {UnitLabel} done";
    }

    /// <summary>Shared behaviour for all eight production stage modules.</summary>
    public abstract class ProductionStageViewModelBase : ViewModelBase
    {
        private const string AllJobsFilter = "All jobs";
        private const string AllStatusesFilter = "All statuses";

        public abstract ProductionStage Stage { get; }
        public abstract string ModuleName { get; }
        public abstract string Description { get; }
        public abstract string Icon { get; }

        /// <summary>
        /// One-line "what this station physically does" hero line, shown in
        /// each stage's own detail sidebar (industry focus). Distinct per
        /// stage — this is what makes the eight screens read as separate
        /// factory stations instead of one shared grid.
        /// </summary>
        public abstract string IndustryFocus { get; }

        /// <summary>"pcs" for pane stages, "units" for pairing/assembly.</summary>
        public string WorkUnitLabel =>
            Stage is ProductionStage.Pairing or ProductionStage.DoublingLamination
                ? "units" : "pcs";

        // Cross-navigation hooks — wired by MainViewModel exactly like
        // Sales Report / Statement drill-downs.
        public Action<string>? OnOpenJobOrderByNumber { get; set; }
        public Action<string>? OnOpenProformaByNumber { get; set; }

        /// <summary>Chain-bar navigation: open any stage module by nav key.</summary>
        public Action<string>? OnOpenStage { get; set; }

        /// <summary>
        /// Creates (or reopens) the Dispatching delivery note for a Job
        /// Order and navigates to it. Returns a message to surface —
        /// empty string on success.
        /// </summary>
        public Func<string, Task<string>>? OnCreateDeliveryNote { get; set; }

        protected ProductionStageViewModelBase()
        {
            RefreshCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadAllAsync, "Production stage refresh"));

            LoadSourcesCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadSourcesAsync, "Production sources load"));

            ReleaseCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReleaseSelectedAsync, "Production release"),
                _ => SelectedSource != null && !IsReleasing);

            OpenStageCommand = new RelayCommand(p =>
            {
                if (p is string navKey && !string.IsNullOrWhiteSpace(navKey))
                    OnOpenStage?.Invoke(navKey);
            }, _ => true);

            OpenJobOrderCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(SelectedJobNo))
                    OnOpenJobOrderByNumber?.Invoke(SelectedJobNo);
            }, _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedJobNo));

            OpenProformaCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(SelectedPiNo))
                    OnOpenProformaByNumber?.Invoke(SelectedPiNo);
            }, _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedPiNo));

            SaveQtyCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyDoneQtyAsync(p, null), "Work item qty save"),
                p => p is ProductionWorkItemRow r && r.CanWork);

            CompleteAllCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyDoneQtyAsync(p, CompleteAllMarker), "Work item qty all"),
                p => p is ProductionWorkItemRow r && r.CanWork);

            ResetQtyCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyDoneQtyAsync(p, 0), "Work item qty reset"),
                p => p is ProductionWorkItemRow r && r.Done > 0);

            CompleteUnitCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(CompleteUnitAsync, "Production unit complete"),
                _ => HasSelection && SelectedStageItems.Any(i => i.CanWork && i.Done < i.Inflow));

            // Always clickable for a production-managed job — the click
            // itself explains when there is nothing ready to deliver, so
            // the button never silently "fades out" while the floor works.
            CreateDeliveryNoteCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(CreateDeliveryNoteAsync, "Production delivery note"),
                _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedJobNo));

            OpenUnitCommand = new RelayCommand(p => OpenUnit(p as ProductionListRow));
            CloseDetailCommand = new RelayCommand(_ => IsDetailOpen = false);

            SaveAllDoneCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(SaveAllDoneAsync, "Work item qty save all"),
                _ => SelectedStageItems.Any(i => i.CanWork));

            // Live refresh: the instant any floor entry is saved (by any
            // screen), every stage module re-reads its list, KPIs, chain
            // and open detail page — no manual refresh button needed.
            ProductionRepository.WorkItemsChanged += OnWorkItemsChanged;
        }

        /// <summary>Marker: "complete every piece that arrived" (inflow).</summary>
        private const int CompleteAllMarker = -1;

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand LoadSourcesCommand { get; }
        public ICommand ReleaseCommand { get; }
        public ICommand OpenStageCommand { get; }
        public ICommand OpenJobOrderCommand { get; }
        public ICommand OpenProformaCommand { get; }
        public ICommand SaveQtyCommand { get; }
        public ICommand CompleteAllCommand { get; }
        public ICommand ResetQtyCommand { get; }
        public ICommand CompleteUnitCommand { get; }
        public ICommand CreateDeliveryNoteCommand { get; }

        // ── Release picker ───────────────────────────────────────────────

        public ObservableCollection<ProductionReleaseSource> Sources { get; } = new();

        private ProductionReleaseSource? _selectedSource;
        public ProductionReleaseSource? SelectedSource
        {
            get => _selectedSource;
            set { if (_selectedSource == value) return; _selectedSource = value; OnPropertyChanged(); }
        }

        private bool _isReleasing;
        public bool IsReleasing
        {
            get => _isReleasing;
            private set { if (_isReleasing == value) return; _isReleasing = value; OnPropertyChanged(); }
        }

        private string _releaseMessage = "";
        public string ReleaseMessage
        {
            get => _releaseMessage;
            private set { if (_releaseMessage == value) return; _releaseMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasReleaseMessage)); }
        }

        public bool HasReleaseMessage => !string.IsNullOrWhiteSpace(_releaseMessage);

        private bool _releaseSucceeded;
        public bool ReleaseSucceeded
        {
            get => _releaseSucceeded;
            private set { if (_releaseSucceeded == value) return; _releaseSucceeded = value; OnPropertyChanged(); }
        }

        // ── Filters ──────────────────────────────────────────────────────

        public ObservableCollection<string> JobFilters { get; } = new();

        private string _selectedJobFilter = AllJobsFilter;
        public string SelectedJobFilter
        {
            get => _selectedJobFilter;
            set
            {
                if (_selectedJobFilter == value) return;
                _selectedJobFilter = value;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(LoadListAsync, "Production job filter");
            }
        }

        public IReadOnlyList<string> StatusFilters { get; } =
            new[] { AllStatusesFilter, "Pending", "In progress", "Completed" };

        private string _selectedStatusFilter = AllStatusesFilter;
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(LoadListAsync, "Production status filter");
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(LoadListAsync, "Production search");
            }
        }

        // ── Master list ──────────────────────────────────────────────────

        /// <summary>
        /// Rows render in pages of 100 (virtualized grid + on-scroll
        /// loading) so jobs with very long unit lists never lag.
        /// </summary>
        private const int ListPageSize = 100;

        /// <summary>Full filtered projection behind the current list.</summary>
        private List<ProductionRepository.ProductionUnitProjection> _listCache = new();
        private int _loadedRowCount;
        private bool _isLoadingMore;

        public ObservableCollection<ProductionListRow> ListRows { get; } = new();

        private ProductionListRow? _selectedRow;
        public ProductionListRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                _selectedRow = value;
                OnPropertyChanged();
                RefreshDetailPanel();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isListEmpty;
        public bool IsListEmpty
        {
            get => _isListEmpty;
            private set { if (_isListEmpty == value) return; _isListEmpty = value; OnPropertyChanged(); }
        }

        private string _listFooterLabel = "";
        /// <summary>"Showing X of Y units — scroll for more".</summary>
        public string ListFooterLabel
        {
            get => _listFooterLabel;
            private set { if (_listFooterLabel == value) return; _listFooterLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasListFooter)); }
        }

        public bool HasListFooter => !string.IsNullOrWhiteSpace(_listFooterLabel);

        // ── KPIs ─────────────────────────────────────────────────────────

        private int _totalCount;
        public int TotalCount { get => _totalCount; private set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); } }

        private int _pendingCount;
        public int PendingCount { get => _pendingCount; private set { if (_pendingCount == value) return; _pendingCount = value; OnPropertyChanged(); } }

        private int _inProgressCount;
        public int InProgressCount { get => _inProgressCount; private set { if (_inProgressCount == value) return; _inProgressCount = value; OnPropertyChanged(); } }

        private int _completedCount;
        public int CompletedCount { get => _completedCount; private set { if (_completedCount == value) return; _completedCount = value; OnPropertyChanged(); } }

        private string _pendingPiecesHint = "";
        public string PendingPiecesHint { get => _pendingPiecesHint; private set { if (_pendingPiecesHint == value) return; _pendingPiecesHint = value; OnPropertyChanged(); } }

        private string _inProgressPiecesHint = "";
        public string InProgressPiecesHint { get => _inProgressPiecesHint; private set { if (_inProgressPiecesHint == value) return; _inProgressPiecesHint = value; OnPropertyChanged(); } }

        private string _completedPiecesHint = "";
        public string CompletedPiecesHint { get => _completedPiecesHint; private set { if (_completedPiecesHint == value) return; _completedPiecesHint = value; OnPropertyChanged(); } }

        private string _areaTotalLabel = "0 m²";
        public string AreaTotalLabel { get => _areaTotalLabel; private set { if (_areaTotalLabel == value) return; _areaTotalLabel = value; OnPropertyChanged(); } }

        private string _metricTotalLabel = "—";
        public string MetricTotalLabel { get => _metricTotalLabel; private set { if (_metricTotalLabel == value) return; _metricTotalLabel = value; OnPropertyChanged(); } }

        private string _unitsReadyLabel = "0 / 0";
        public string UnitsReadyLabel { get => _unitsReadyLabel; private set { if (_unitsReadyLabel == value) return; _unitsReadyLabel = value; OnPropertyChanged(); } }

        private string _actionWarning = "";
        /// <summary>Feedback from clamped floor writes ("only N arrived").</summary>
        public string ActionWarning
        {
            get => _actionWarning;
            private set { if (_actionWarning == value) return; _actionWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActionWarning)); }
        }

        public bool HasActionWarning => !string.IsNullOrWhiteSpace(_actionWarning);

        private string _waitingUpstreamLabel = "";
        /// <summary>
        /// Pipeline backlog: units allocated to this stage where no piece
        /// has arrived yet — they flow in as upstream completes pieces.
        /// </summary>
        public string WaitingUpstreamLabel
        {
            get => _waitingUpstreamLabel;
            private set { if (_waitingUpstreamLabel == value) return; _waitingUpstreamLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWaitingUpstream)); }
        }

        public bool HasWaitingUpstream => !string.IsNullOrWhiteSpace(_waitingUpstreamLabel);

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

        /// <summary>Pane-level stage items for the selected unit.</summary>
        public ObservableCollection<ProductionWorkItemRow> SelectedStageItems { get; } = new();

        // ── Detail grids (Step-5 cut-list style: OUTER left / INNER right) ─

        /// <summary>Outer panes still in progress at this stage (left grid).</summary>
        public ObservableCollection<ProductionWorkItemRow> OuterStageItems { get; } = new();

        /// <summary>Inner panes still in progress at this stage (right grid).</summary>
        public ObservableCollection<ProductionWorkItemRow> InnerStageItems { get; } = new();

        /// <summary>Panes fully completed — moved to the COMPLETED section.</summary>
        public ObservableCollection<ProductionWorkItemRow> CompletedStageItems { get; } = new();

        private bool _isSingleGlass;
        /// <summary>True when the unit has a single glass pane (SGU) — one centered grid.</summary>
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

        // ── Full-page unit detail (double-click opens, back returns) ──────

        private bool _isDetailOpen;
        /// <summary>True while the full-page unit detail overlay is showing.</summary>
        public bool IsDetailOpen
        {
            get => _isDetailOpen;
            private set { if (_isDetailOpen == value) return; _isDetailOpen = value; OnPropertyChanged(); }
        }

        private string _selectedSerialLabel = "—";
        public string SelectedSerialLabel { get => _selectedSerialLabel; private set { if (_selectedSerialLabel == value) return; _selectedSerialLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitTotalLabel = "0";
        /// <summary>Unit TOTAL pieces (outer + inner summed) for the KPI panel.</summary>
        public string SelectedUnitTotalLabel { get => _selectedUnitTotalLabel; private set { if (_selectedUnitTotalLabel == value) return; _selectedUnitTotalLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitDoneLabel = "0";
        public string SelectedUnitDoneLabel { get => _selectedUnitDoneLabel; private set { if (_selectedUnitDoneLabel == value) return; _selectedUnitDoneLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitBalanceLabel = "0";
        public string SelectedUnitBalanceLabel { get => _selectedUnitBalanceLabel; private set { if (_selectedUnitBalanceLabel == value) return; _selectedUnitBalanceLabel = value; OnPropertyChanged(); } }

        private string _selectedUnitCompletedPctLabel = "0%";
        public string SelectedUnitCompletedPctLabel { get => _selectedUnitCompletedPctLabel; private set { if (_selectedUnitCompletedPctLabel == value) return; _selectedUnitCompletedPctLabel = value; OnPropertyChanged(); } }

        /// <summary>Floor audit trail for the selected unit at this stage.</summary>
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

        // ── Stage identity ───────────────────────────────────────────────

        /// <summary>"Stage 3 of 8" — position in the factory pipeline.</summary>
        public string StageOrdinalLabel => $"Stage {((int)Stage).ToString(CultureInfo.InvariantCulture)} of 8";

        // ── Loading ──────────────────────────────────────────────────────

        private bool _initialLoadDone;

        /// <summary>Called by the view on first load and via Refresh.</summary>
        public async Task LoadAsync()
        {
            await LoadAllAsync().ConfigureAwait(true);
            if (!_initialLoadDone)
            {
                _initialLoadDone = true;
                await LoadSourcesAsync().ConfigureAwait(true);
            }
        }

        private async Task LoadAllAsync()
        {
            await LoadListAsync().ConfigureAwait(true);
            await LoadKpisAsync().ConfigureAwait(true);
            await LoadChainAsync().ConfigureAwait(true);
        }

        private async Task LoadListAsync()
        {
            IsLoading = true;
            string? previousKey = _selectedRow?.RowKey;
            try
            {
                string? jobRef = _selectedJobFilter == AllJobsFilter ? null : _selectedJobFilter;
                WorkItemStatus? status = _selectedStatusFilter switch
                {
                    "Pending" => WorkItemStatus.Pending,
                    "In progress" => WorkItemStatus.InProgress,
                    "Completed" => WorkItemStatus.Completed,
                    _ => null
                };

                var data = await Task.Run(() =>
                    ProductionRepository.GetStageListAsync(
                        Stage, jobRef, status,
                        string.IsNullOrWhiteSpace(_searchText) ? null : _searchText))
                    .ConfigureAwait(true);

                // Full filtered projection is cached; the grid renders it
                // in pages of 100 (virtualized) so huge jobs stay fluid.
                _listCache = data.Units;
                _loadedRowCount = 0;
                ListRows.Clear();

                // AutoSum footer over the full filtered set (not just the
                // virtualized page) — TOTAL / DONE / BALANCE column ends.
                ListTotalSum = _listCache.Sum(u => u.Inflow);
                ListDoneSum = _listCache.Sum(u => u.DoneQty);
                ListBalanceSum = _listCache.Sum(u => u.BalanceQty);

                // Keep the selection visible across reloads: extend the
                // first page far enough to include the selected row.
                int firstPageSize = Math.Min(ListPageSize, _listCache.Count);
                if (previousKey != null)
                {
                    int idx = _listCache.FindIndex(u =>
                        $"U|{u.JobRef}|{u.SpecGroupNumber}|{u.SerialNumber}" == previousKey);
                    if (idx >= firstPageSize) firstPageSize = idx + 1;
                }
                AppendRows(firstPageSize);

                IsListEmpty = _listCache.Count == 0;

                int waiting = data.WaitingUnits;
                WaitingUpstreamLabel = waiting > 0
                    ? $"{waiting.ToString(CultureInfo.InvariantCulture)} unit(s) allocated to this stage have " +
                      "no pieces arrived yet — they flow in as the previous stage completes pieces."
                    : "";

                // Restore the selection across reloads (floor writes reload).
                if (previousKey != null)
                {
                    var match = ListRows.FirstOrDefault(x => x.RowKey == previousKey);
                    if (match != null) { _selectedRow = match; OnPropertyChanged(nameof(SelectedRow)); RefreshDetailPanel(); }
                    else ClearDetailPanel();
                }
                else if (_selectedRow == null)
                {
                    ClearDetailPanel();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>Appends rows until the list holds <paramref name="targetCount"/> rows.</summary>
        private void AppendRows(int targetCount)
        {
            targetCount = Math.Min(targetCount, _listCache.Count);
            while (_loadedRowCount < targetCount)
            {
                ListRows.Add(BuildUnitRow(_listCache[_loadedRowCount]));
                _loadedRowCount++;
            }

            ListFooterLabel = _listCache.Count == 0 ? "" :
                _loadedRowCount < _listCache.Count
                    ? $"Showing {_loadedRowCount.ToString(CultureInfo.InvariantCulture)} of " +
                      $"{_listCache.Count.ToString(CultureInfo.InvariantCulture)} units — scroll for the next " +
                      $"{ListPageSize.ToString(CultureInfo.InvariantCulture)}"
                    : $"Showing all {_listCache.Count.ToString(CultureInfo.InvariantCulture)} unit(s)";
        }

        /// <summary>
        /// Incremental loading: the (virtualized) grid calls in from its
        /// ScrollChanged event; near the bottom, the next 100 rows render.
        /// </summary>
        public void OnMasterListScrolled(double verticalOffset, double viewportHeight, double extentHeight)
        {
            if (_isLoadingMore || _loadedRowCount >= _listCache.Count) return;
            if (extentHeight <= 0) return;
            if (verticalOffset + viewportHeight < extentHeight - 80) return;

            _isLoadingMore = true;
            try
            {
                AppendRows(_loadedRowCount + ListPageSize);
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        private async Task LoadKpisAsync()
        {
            // KPIs describe what is ACTIONABLE in this stage right now —
            // arrived pieces, done pieces and the load they carry. Job
            // filter applies; search/status filters don't.
            string? jobRef = _selectedJobFilter == AllJobsFilter ? null : _selectedJobFilter;
            var data = await Task.Run(() =>
                ProductionRepository.GetStageListAsync(Stage, jobRef, null, null, includeWaiting: true))
                .ConfigureAwait(true);

            var arrived = data.Units.Where(u => u.Inflow > 0).ToList();

            TotalCount = arrived.Count;
            PendingCount = arrived.Count(u => u.AggregateStatus == 0);
            InProgressCount = arrived.Count(u => u.AggregateStatus == 1);
            CompletedCount = arrived.Count(u => u.AggregateStatus == 2);

            // Piece-flow KPI hints: the boards count rows, the floor reads
            // pieces — both stay visible (units for assembly stages).
            string unitSuffix = Stage is ProductionStage.Pairing or ProductionStage.DoublingLamination
                ? " units" : " pcs";
            PendingPiecesHint = "Σ " +
                arrived.Where(u => u.AggregateStatus == 0).Sum(u => u.Inflow)
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix;
            InProgressPiecesHint = "Σ " +
                arrived.Where(u => u.AggregateStatus == 1).Sum(u => u.DoneQty)
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix + " done";
            CompletedPiecesHint = "Σ " +
                arrived.Where(u => u.AggregateStatus == 2).Sum(u => u.DoneQty)
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix + " done";

            decimal area = arrived.Sum(u => u.ArrivedArea);
            AreaTotalLabel = area.ToString("0.##", CultureInfo.InvariantCulture) + " m²";

            decimal stageMetric = Stage is ProductionStage.Polish or ProductionStage.Mitring
                ? arrived.Sum(u => u.ArrivedLm)
                : arrived.Sum(u => u.Inflow);

            MetricTotalLabel = Stage switch
            {
                ProductionStage.Polish or ProductionStage.Mitring =>
                    stageMetric.ToString("0.##", CultureInfo.InvariantCulture) + " LM",
                ProductionStage.HolesCutouts =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " pcs to drill/cut",
                ProductionStage.Pairing =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " units to pair",
                ProductionStage.DoublingLamination =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " units to assemble",
                _ => stageMetric.ToString("0", CultureInfo.InvariantCulture) + " pcs"
            };

            // Dispatch readiness is a QUANTITY: pieces that finished the
            // chain (SGU off the pane chain, multi-pane units off
            // doubling) — partial quantities count.
            var (totalUnits, readyUnits, totalPieces, readyPieces) = await Task.Run(() =>
                ProductionRepository.GetUnitReadinessAsync(jobRef))
                .ConfigureAwait(true);
            UnitsReadyLabel =
                $"{readyPieces.ToString(CultureInfo.InvariantCulture)} / {totalPieces.ToString(CultureInfo.InvariantCulture)} pcs" +
                (readyUnits > 0 ? $" · {readyUnits.ToString(CultureInfo.InvariantCulture)} unit(s) complete" : "");

            // Job filter picker (preserve current choice when it survives).
            var jobRefs = await Task.Run(async () =>
                (await ProductionRepository.GetReleasesAsync().ConfigureAwait(false))
                    .Select(r => r.JobRef).Distinct().OrderBy(r => r).ToList())
                .ConfigureAwait(true);

            string previous = _selectedJobFilter;
            JobFilters.Clear();
            JobFilters.Add(AllJobsFilter);
            foreach (var jobRefEntry in jobRefs) JobFilters.Add(jobRefEntry);

            _selectedJobFilter = jobRefs.Contains(previous) ? previous : AllJobsFilter;
            OnPropertyChanged(nameof(SelectedJobFilter));

            // If the selected job disappeared (release removed), the list
            // was filtered by it — resync the list to the reset filter.
            if (_selectedJobFilter != previous)
                await LoadListAsync().ConfigureAwait(true);
        }

        // ── Row builder ──────────────────────────────────────────────────

        private static string FmtInt(int v) =>
            v > 0 ? v.ToString(CultureInfo.InvariantCulture) : "—";

        /// <summary>
        /// Step 4/5 size semantics: a second size pair on the row means
        /// OVERLAP glass; an inherited (equal) pair means an EQUAL-SIZE
        /// unit where the blank W2/H2 took W1/H1 at distribution.
        /// </summary>
        private static string BuildSizeRule(ProductionRepository.ProductionUnitProjection unit)
        {
            if (unit.BuildType == "Sgu" || unit.W2 <= 0 || unit.H2 <= 0) return "";

            return unit.W2 == unit.W1 && unit.H2 == unit.H1
                ? $"EQUAL-SIZE UNIT — both lites {unit.W1.ToString(CultureInfo.InvariantCulture)}×{unit.H1.ToString(CultureInfo.InvariantCulture)}"
                : $"OVERLAP GLASS — inner {unit.W2.ToString(CultureInfo.InvariantCulture)}×{unit.H2.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FmtDec(decimal v, string suffix) =>
            v > 0 ? v.ToString("0.###", CultureInfo.InvariantCulture) + suffix : "—";

        private ProductionListRow BuildUnitRow(ProductionRepository.ProductionUnitProjection unit)
        {
            string glyph = unit.AggregateStatus switch
            {
                2 => "✓",
                1 => "◐",
                _ => "●"
            };
            string statusLabel = unit.AggregateStatus switch
            {
                2 => "COMPLETED",
                1 => "IN PROGRESS",
                _ => unit.Inflow == 0 ? "WAITING UPSTREAM" : "PENDING"
            };

            return new ProductionListRow
            {
                RowKey = $"U|{unit.JobRef}|{unit.SpecGroupNumber}|{unit.SerialNumber}",
                SpecGroupNumber = unit.SpecGroupNumber,
                SerialNumber = unit.SerialNumber,
                StatusGlyph = glyph,
                StatusLabel = statusLabel,
                JobRef = unit.JobRef,
                PiNo = unit.SourceProformaId,
                Customer = unit.CustomerName,
                Salesman = unit.Salesman,
                SpecLabel = $"Spec {unit.SpecGroupNumber.ToString(CultureInfo.InvariantCulture)}" +
                    (string.IsNullOrWhiteSpace(unit.SpecName) ? "" : $" · {unit.SpecName}"),
                SerialLabel = unit.SerialNumber > 0
                    ? unit.SerialNumber.ToString(CultureInfo.InvariantCulture) : "—",
                GlassRef = string.IsNullOrWhiteSpace(unit.GlassRef) ? "—" : unit.GlassRef,
                BuildType = unit.BuildType,
                W1Label = FmtInt(unit.W1),
                H1Label = FmtInt(unit.H1),
                W2Label = FmtInt(unit.W2),
                H2Label = FmtInt(unit.H2),
                QtyLabel = FmtInt(unit.Qty),
                SizeRuleLabel = BuildSizeRule(unit),
                Sqm1Label = FmtDec(unit.UnitSqm1, ""),
                TotalSqm1Label = FmtDec(unit.TotalSqm1, ""),
                Lm1Label = FmtDec(unit.Lm1, ""),
                Sqm2Label = FmtDec(unit.UnitSqm2, ""),
                TotalSqm2Label = FmtDec(unit.TotalSqm2, ""),
                Lm2Label = FmtDec(unit.Lm2, ""),
                InflowLabel = unit.Inflow.ToString(CultureInfo.InvariantCulture),
                DoneLabel = unit.DoneQty.ToString(CultureInfo.InvariantCulture),
                BalanceLabel = unit.BalanceQty.ToString(CultureInfo.InvariantCulture),
                WaitingOn = unit.WaitingOn,
                FlowItems = unit.FlowItems
            };
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

            HasSelection = true;
            SelectedJobNo = row.JobRef;
            SelectedPiNo = row.PiNo;
            SelectedCustomer = row.Customer;
            SelectedSalesman = row.Salesman;
            SelectedSpecLabel = row.SpecLabel;
            SelectedRef = row.GlassRef;

            var first = row.FlowItems.FirstOrDefault()?.Item;
            SelectedProject = first?.ProjectName ?? "";

            var sizes = new List<string>();
            if (row.W1Label != "—") sizes.Add($"W1 {row.W1Label} × H1 {row.H1Label}");
            if (row.W2Label != "—") sizes.Add($"W2 {row.W2Label} × H2 {row.H2Label}");
            SelectedSizeSummary = sizes.Count == 0 ? "—"
                : string.Join("  ·  ", sizes) + $"  ·  Qty {row.QtyLabel}";

            // Step 4/5 size rule — overlap glass vs equal-size unit.
            SelectedSizeRuleLabel = row.SizeRuleLabel;

            // Step 4 composition intelligence: pane treatments.
            var treatments = row.FlowItems
                .Select(fi => fi.Item.Treatment)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
            SelectedTreatmentsLabel = treatments.Count == 0 ? "—"
                : string.Join(" · ", treatments);

            SelectedArrivalLabel = first?.CreatedAt == null || first.CreatedAt == default
                ? "—"
                : first.CreatedAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

            SelectedStageItems.Clear();
            foreach (var flowItem in row.FlowItems)
                SelectedStageItems.Add(new ProductionWorkItemRow(flowItem));

            SelectedSpecName = first?.SpecName ?? "";
            IsSingleGlass = row.FlowItems.Count == 1;
            RebuildUnitPanels();

            AsyncUtility.InvalidateCommandsThrottled();

            // Stage totals — total / done / balance in pieces, SQM and LM.
            int pcsTotal = 0, pcsDone = 0;
            decimal sqmTotal = 0m, sqmDone = 0m, lmTotal = 0m, lmDone = 0m;
            foreach (var fi in row.FlowItems)
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

            // Full-page KPI panel: unit TOTAL / DONE / BALANCE / COMPLETED %.
            SelectedSerialLabel = row.SerialLabel;
            SelectedUnitTotalLabel = pcsTotal.ToString(CultureInfo.InvariantCulture);
            SelectedUnitDoneLabel = pcsDone.ToString(CultureInfo.InvariantCulture);
            SelectedUnitBalanceLabel = (pcsTotal - pcsDone).ToString(CultureInfo.InvariantCulture);
            SelectedUnitCompletedPctLabel = pcsTotal == 0 ? "0%"
                : ((double)pcsDone / pcsTotal * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";

            // Floor audit trail for this unit at this stage.
            AsyncUtility.SafeFireAndForget(RefreshDetailEntriesAsync, "Production entry log");

            // Dispatch figures — finished pieces flow into delivery notes.
            AsyncUtility.SafeFireAndForget(
                () => RefreshDispatchAsync(row.JobRef, row.SpecGroupNumber, row.SerialNumber),
                "Production dispatch panel");
        }

        private static bool IsInnerPosition(string position) =>
            string.Equals(position, "Inner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "Lam Inner", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Distributes the selected unit's panes into the OUTER / INNER grids
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

        private async Task RefreshDispatchAsync(string jobRef, int specGroupNumber, int serialNumber)
        {
            var (ready, delivered, balance, managed) = await Task.Run(() =>
                ProductionDeliveryGateway.GetRowDispatchStateAsync(
                    jobRef, specGroupNumber, serialNumber))
                .ConfigureAwait(true);

            // Selection may have moved on while loading.
            if (_selectedRow == null || _selectedRow.JobRef != jobRef ||
                _selectedRow.SpecGroupNumber != specGroupNumber ||
                _selectedRow.SerialNumber != serialNumber) return;

            HasDispatchPanel = managed;
            SelectedDispatchReadyLabel = ready.ToString(CultureInfo.InvariantCulture);
            SelectedDispatchDeliveredLabel = delivered.ToString(CultureInfo.InvariantCulture);
            SelectedDispatchBalanceLabel = balance.ToString(CultureInfo.InvariantCulture);
            DispatchBalance = balance;
            AsyncUtility.InvalidateCommandsThrottled();
        }

        /// <summary>
        /// Creates (or reopens) the Dispatching delivery note for the
        /// selected unit's Job Order — it opens with remaining balance
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

        /// <summary>Opens the full-page unit detail (double-click on a row).</summary>
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
        /// Loads the floor audit trail for the selected unit at this stage:
        /// each "mark done" save — who, when and how many — newest first.
        /// </summary>
        private async Task RefreshDetailEntriesAsync()
        {
            var row = _selectedRow;
            if (row == null) return;

            var entries = await Task.Run(() =>
                ProductionRepository.GetUnitEntriesAsync(
                    row.JobRef, row.SpecGroupNumber, row.SerialNumber, Stage))
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

        // ── Release sources ──────────────────────────────────────────────

        private async Task LoadSourcesAsync()
        {
            var sources = await Task.Run(ProductionReleaseService.GetSourcesAsync)
                .ConfigureAwait(true);

            Sources.Clear();
            foreach (var source in sources) Sources.Add(source);

            if (SelectedSource == null && Sources.Count > 0)
                SelectedSource = Sources[0];
        }

        private async Task ReleaseSelectedAsync()
        {
            var source = SelectedSource;
            if (source == null)
            {
                ReleaseMessage = "Pick a Job Order to release.";
                ReleaseSucceeded = false;
                return;
            }

            IsReleasing = true;
            ReleaseMessage = $"Distributing {source.Reference}…";
            ReleaseSucceeded = false;
            try
            {
                var (success, message, _) = await Task.Run(() =>
                    ProductionReleaseService.ReleaseAsync(source)).ConfigureAwait(true);

                ReleaseMessage = message;
                ReleaseSucceeded = success;

                if (success) await LoadAllAsync().ConfigureAwait(true);
            }
            finally
            {
                IsReleasing = false;
            }
        }

        // ── Floor actions (piece quantities) ─────────────────────────────

        /// <summary>
        /// Saves the done-piece count for one stage item. The repository
        /// clamps to the pieces that actually arrived from upstream — the
        /// tool chain can never be overstated. <paramref name="qtyOverride"/>
        /// supports Complete-all (inflow) and Reset (0); null reads the
        /// row's editor input.
        /// </summary>
        private async Task ApplyDoneQtyAsync(object? parameter, int? qtyOverride)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            int requested;
            if (qtyOverride.HasValue)
            {
                requested = qtyOverride.Value == CompleteAllMarker ? row.Inflow : qtyOverride.Value;
            }
            else
            {
                string raw = (row.DoneInput ?? "").Trim();
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out requested))
                {
                    ActionWarning = "Enter a whole number of pieces.";
                    return;
                }
            }

            var (ok, applied, message) = await Task.Run(() =>
                ProductionRepository.SetDoneQtyAsync(row.Id, requested))
                .ConfigureAwait(true);

            if (ok)
            {
                ActionWarning = message; // clamp notice or empty
                await LoadAllAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Floor convenience: complete every arrived piece of the selected
        /// unit in one action. Each item is clamped to its inflow by the
        /// repository, so a unit with partly-arrived panes still only
        /// completes what actually arrived.
        /// </summary>
        private async Task CompleteUnitAsync()
        {
            var targets = SelectedStageItems
                .Where(i => i.CanWork && i.Done < i.Inflow)
                .ToList();
            if (targets.Count == 0) return;

            foreach (var row in targets)
            {
                var (ok, _, message) = await Task.Run(() =>
                    ProductionRepository.SetDoneQtyAsync(row.Id, row.Inflow))
                    .ConfigureAwait(true);
                if (ok && !string.IsNullOrWhiteSpace(message))
                    ActionWarning = message;
            }

            await LoadAllAsync().ConfigureAwait(true);
        }
    }
}
