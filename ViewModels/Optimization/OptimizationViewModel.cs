// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Optimization/OptimizationViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — module VM (design 2026-09-12)
//
// Tabs: JOBS → SIZES → STOCKS → MARGINS → RESULT → 2D VIEW.
//   • JOBS    — piece list (ref / W×H / qty); a reference job loader
//               ships with the owner's calibration DGU data. The smart
//               Job-Order picker arrives next phase (source lock:
//               confirmed JOs only).
//   • SIZES   — merged thickness+color summary of the piece list.
//   • STOCKS  — editable stock pool ("best sheet size" rule: smallest
//               fitting stock is consumed first).
//   • MARGINS — per-thickness LM/RM/TM/BM, kerf, minimum breakout,
//               rotation (enabled by default for all pieces).
//   • RESULT  — sheets, utilization, unplaced, oracle verdict.
//   • 2D VIEW — scaled sheet canvas with piece labels.
//
// Engine: Services/Optimization/GuillotinePacker (pure, deterministic,
// calibrated against the owner's benchmark: Low-E 17 @ 82.77%).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public class OptimizationViewModel : ViewModels.ViewModelBase
    {
        // ── tabs ────────────────────────────────────────────────────────
        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }

        // ── JOBS tab: confirmed Job-Order picker (source lock) ─────────
        // Production runs COMPLETELY from the confirmed PI (Job Order).
        // The picker lists every non-draft, non-void JO; loading one expands
        // its cutting pieces through the production distribution engine so
        // the W2/H2 size semantics (equal-size vs overlap) match the floor.

        public sealed class JobOptionVm
        {
            public string JobOrderId { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public int PieceRows { get; set; }

            /// <summary>
            /// Step 4/5 pane decomposition of this job: one entry per
            /// spec-group × physical pane (Outer / Inner / Lam …), each
            /// carrying its OWN glass composition and cutting pieces.
            /// </summary>
            public List<PaneChoiceVm> PaneChoices { get; } = new();

            /// <summary>Flat merged cutting list (composition, label, w, h, qty)
            /// across all panes — convenience for "cut the whole job".</summary>
            public List<(string Glass, string Label, int W, int H, int Qty)> Pieces { get; } = new();
        }

        /// <summary>
        /// One physical pane of a Step 5 spec group, decomposed from the
        /// Step 4 spec name. Two specs that share the same outer pane
        /// (e.g. a low-e pane on a clear DGU and on an obscure DGU) each
        /// expose it — the operator multi-selects both and the SIZES merge
        /// folds them into ONE cutting group by composition.
        /// </summary>
        public sealed class PaneChoiceVm : ViewModels.ViewModelBase
        {
            private bool _isSelected;
            public int SpecGroupNumber { get; set; }
            public string SpecName { get; set; } = string.Empty;
            public string BuildType { get; set; } = string.Empty;
            public string Position { get; set; } = string.Empty;
            public string Composition { get; set; } = string.Empty;
            public string Thickness { get; set; } = string.Empty;
            public string Treatment { get; set; } = string.Empty;
            public string SizesSummary { get; set; } = string.Empty;
            public int TotalPcs { get; set; }
            public string AreaLabel { get; set; } = string.Empty;
            public string CountLabel { get; set; } = string.Empty;

            public string SpecLabel => "Spec " + SpecGroupNumber.ToString(CultureInfo.InvariantCulture)
                + " · " + BuildType.ToUpperInvariant();
            public string PaneLabel => string.IsNullOrWhiteSpace(Treatment)
                ? Position : Position + " · " + Treatment;

            public List<(string Glass, string Label, int W, int H, int Qty)> Pieces { get; } = new();

            public bool IsSelected
            {
                get => _isSelected;
                set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
            }
        }

        public ObservableCollection<JobOptionVm> AvailableJobs { get; } = new();

        /// <summary>Job picker filtered by <see cref="JobSearchText"/> (id / customer).</summary>
        public ObservableCollection<JobOptionVm> FilteredJobs { get; } = new();

        private string _jobSearchText = string.Empty;
        public string JobSearchText
        {
            get => _jobSearchText;
            set
            {
                if (_jobSearchText == value) return;
                _jobSearchText = value;
                OnPropertyChanged();
                RefreshJobsFilter();
            }
        }

        private JobOptionVm? _selectedJob;
        public JobOptionVm? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (_selectedJob == value) return;
                _selectedJob = value;
                OnPropertyChanged();
                PopulateJobPanes(value);
            }
        }

        /// <summary>Pane checklist for the currently selected job (multi-select).</summary>
        public ObservableCollection<PaneChoiceVm> JobPaneChoices { get; } = new();

        private bool _hasPaneChoices;
        public bool HasPaneChoices { get => _hasPaneChoices; private set { _hasPaneChoices = value; OnPropertyChanged(); } }

        private int _selectedPaneCount;
        public int SelectedPaneCount
        {
            get => _selectedPaneCount;
            private set
            {
                if (_selectedPaneCount == value) return;
                _selectedPaneCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPaneSummary));
                OnPropertyChanged(nameof(HasSelectedPanes));
                OnPropertyChanged(nameof(AddPanesButtonLabel));
            }
        }
        public string SelectedPaneSummary => SelectedPaneCount == 0
            ? "No panes selected"
            : SelectedPaneCount.ToString(CultureInfo.InvariantCulture) + " pane(s) selected";
        public bool HasSelectedPanes => SelectedPaneCount > 0;
        public string AddPanesButtonLabel => SelectedPaneCount == 0
            ? "Add selected panes → cutting list"
            : "Add " + SelectedPaneCount.ToString(CultureInfo.InvariantCulture) + " selected pane(s) → cutting list";

        private string _jobsStatusText = "Loading confirmed job orders…";
        public string JobsStatusText
        {
            get => _jobsStatusText;
            set { _jobsStatusText = value; OnPropertyChanged(); }
        }

        private bool _hasPieces;
        public bool HasPieces
        {
            get => _hasPieces;
            private set { _hasPieces = value; OnPropertyChanged(); OnPropertyChanged(nameof(PiecesEmptyHint)); }
        }

        /// <summary>Shown on JOBS/SIZES tabs while the cutting list is empty.</summary>
        public string PiecesEmptyHint => HasPieces
            ? string.Empty
            : "No pieces yet — select a confirmed job order on the left and load its cutting list, or add pieces manually below.";

        public ICommand RefreshJobsCommand { get; }
        public ICommand AddSelectedPanesCommand { get; }
        public ICommand SelectAllPanesCommand { get; }
        public ICommand ClearPaneSelectionCommand { get; }


        public sealed class PieceEntryVm : ViewModels.ViewModelBase
        {
            private string _glassRef = "6mm clear ft glass";
            private string _pieceLabel = string.Empty;
            private string _position = string.Empty;
            private string _widthText = string.Empty;
            private string _heightText = string.Empty;
            private string _qtyText = "1";

            /// <summary>Raised when an editable field changes (VM refreshes
            /// the summary, merge groups and cut plan).</summary>
            public Action? OnEdited { get; set; }

            public string GlassRef { get => _glassRef; set { _glassRef = value; OnPropertyChanged(); } }
            public string PieceLabel { get => _pieceLabel; set { _pieceLabel = value; OnPropertyChanged(); } }
            /// <summary>Pane position provenance — "Outer", "Inner", … ("" for ad-hoc pieces).</summary>
            public string Position { get => _position; set { _position = value; OnPropertyChanged(); } }
            public string WidthText { get => _widthText; set { _widthText = value; OnPropertyChanged(); OnEdited?.Invoke(); } }
            public string HeightText { get => _heightText; set { _heightText = value; OnPropertyChanged(); OnEdited?.Invoke(); } }
            public string QtyText { get => _qtyText; set { _qtyText = value; OnPropertyChanged(); OnEdited?.Invoke(); } }

            public bool TryParse(out int width, out int height, out int qty)
            {
                width = height = qty = 0;
                return int.TryParse(WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out width) &&
                       int.TryParse(HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out height) &&
                       int.TryParse(QtyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out qty) &&
                       width > 0 && height > 0 && qty > 0;
            }
        }

        public ObservableCollection<PieceEntryVm> Pieces { get; } = new();

        private PieceEntryVm _newPiece = new();
        public PieceEntryVm NewPiece { get => _newPiece; private set { _newPiece = value; OnPropertyChanged(); } }

        public ICommand AddPieceCommand { get; }
        public ICommand RemovePieceCommand { get; }
        public ICommand ClearPiecesCommand { get; }
        public ICommand LoadReferenceJobCommand { get; }

        private string _pieceSummary = "No pieces yet";
        public string PieceSummary { get => _pieceSummary; private set { _pieceSummary = value; OnPropertyChanged(); } }

        // ── SIZES tab: merged groups ───────────────────────────────────

        public sealed class SizeGroupVm
        {
            public string Composition { get; set; } = string.Empty;
            public string Thickness { get; set; } = string.Empty;
            public string PieceCount { get; set; } = string.Empty;
            public string AreaM2 { get; set; } = string.Empty;
        }

        public ObservableCollection<SizeGroupVm> SizeGroups { get; } = new();

        // ── SIZES tab: expanded cut plan (the pieces as the saw cuts them) ──

        public sealed class CutPlanRowVm
        {
            public string Number { get; set; } = string.Empty;
            public string Ref { get; set; } = string.Empty;
            public string Composition { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
        }

        public ObservableCollection<CutPlanRowVm> CutPlanRows { get; } = new();

        private bool _hasCutPlan;
        public bool HasCutPlan { get => _hasCutPlan; private set { _hasCutPlan = value; OnPropertyChanged(); } }

        private string _cutPlanNote = string.Empty;
        public string CutPlanNote { get => _cutPlanNote; private set { _cutPlanNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCutPlanNote)); } }
        public bool HasCutPlanNote => !string.IsNullOrWhiteSpace(_cutPlanNote);

        /// <summary>Expanded rows are capped for the on-screen plan (the engine still cuts every piece).</summary>
        private const int CutPlanDisplayCap = 500;

        // ── STOCKS tab ─────────────────────────────────────────────────

        public sealed class StockEntryVm : ViewModels.ViewModelBase
        {
            private string _widthText;
            private string _heightText;
            private string _availableText = "0"; // 0 = unlimited
            public StockEntryVm(string w, string h) { _widthText = w; _heightText = h; }

            public string WidthText { get => _widthText; set { _widthText = value; OnPropertyChanged(); } }
            public string HeightText { get => _heightText; set { _heightText = value; OnPropertyChanged(); } }
            public string AvailableText { get => _availableText; set { _availableText = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<StockEntryVm> Stocks { get; } = new();
        public ICommand AddStockCommand { get; }
        public ICommand RemoveStockCommand { get; }

        private StockEntryVm _newStock = new("3210", "2250");
        public StockEntryVm NewStock { get => _newStock; private set { _newStock = value; OnPropertyChanged(); } }

        // ── MARGINS tab ────────────────────────────────────────────────

        private string _thicknessText = "6";
        public string ThicknessText { get => _thicknessText; set { _thicknessText = value; OnPropertyChanged(); } }

        private string _leftMarginText = "15";
        private string _rightMarginText = "15";
        private string _topMarginText = "15";
        private string _bottomMarginText = "15";
        private string _kerfText = "3";
        private string _minBreakoutText = "15";
        private bool _rotationEnabled = true; // floor default: rotation ON for all pieces

        public string LeftMarginText { get => _leftMarginText; set { _leftMarginText = value; OnPropertyChanged(); } }
        public string RightMarginText { get => _rightMarginText; set { _rightMarginText = value; OnPropertyChanged(); } }
        public string TopMarginText { get => _topMarginText; set { _topMarginText = value; OnPropertyChanged(); } }
        public string BottomMarginText { get => _bottomMarginText; set { _bottomMarginText = value; OnPropertyChanged(); } }
        public string KerfText { get => _kerfText; set { _kerfText = value; OnPropertyChanged(); } }
        public string MinBreakoutText { get => _minBreakoutText; set { _minBreakoutText = value; OnPropertyChanged(); } }
        public bool RotationEnabled { get => _rotationEnabled; set { _rotationEnabled = value; OnPropertyChanged(); } }

        public ICommand LoadHouseDefaultsCommand { get; }

        public ObservableCollection<string> EffortLevels { get; } = new() { "1", "2", "3", "4", "5" };
        private string _selectedEffortLevel = "3";
        public string SelectedEffortLevel
        {
            get => _selectedEffortLevel;
            set { _selectedEffortLevel = value; OnPropertyChanged(); }
        }

        // ── RESULT tab ─────────────────────────────────────────────────

        public sealed class SheetRowVm : ViewModels.ViewModelBase
        {
            private bool _isSelected;
            public string SheetNo { get; set; } = string.Empty;
            public string StockLabel { get; set; } = string.Empty;
            public string PieceCount { get; set; } = string.Empty;
            public string Utilization { get; set; } = string.Empty;
            public string WasteLabel { get; set; } = string.Empty;
            /// <summary>Cutting-group (glass composition) this sheet belongs to.</summary>
            public string Composition { get; set; } = string.Empty;
            public int Index { get; set; }

            /// <summary>Highlighted in the 2D filmstrip when this is the
            /// sheet currently on the canvas.</summary>
            public bool IsSelected
            {
                get => _isSelected;
                set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
            }
        }

        public ObservableCollection<SheetRowVm> SheetRows { get; } = new();

        private bool _hasResult;
        public bool HasResult { get => _hasResult; private set { _hasResult = value; OnPropertyChanged(); } }

        private string _resultSheets = "—";
        private string _resultUtilization = "—";
        private string _resultUnplaced = "—";
        private string _resultOracle = "—";
        private string _resultStage = string.Empty;
        private bool _oraclePassed;

        public string ResultSheets { get => _resultSheets; private set { _resultSheets = value; OnPropertyChanged(); } }
        public string ResultUtilization { get => _resultUtilization; private set { _resultUtilization = value; OnPropertyChanged(); } }
        public string ResultUnplaced { get => _resultUnplaced; private set { _resultUnplaced = value; OnPropertyChanged(); } }
        public string ResultOracle { get => _resultOracle; private set { _resultOracle = value; OnPropertyChanged(); } }
        public string ResultStage { get => _resultStage; private set { _resultStage = value; OnPropertyChanged(); } }
        public bool OraclePassed { get => _oraclePassed; private set { _oraclePassed = value; OnPropertyChanged(); } }

        // ── Waste / offcut reporting (utilization + waste = the sheet) ──
        private string _resultWaste = "—";
        /// <summary>Total offcut across the run (stock − used), m².</summary>
        public string ResultWaste { get => _resultWaste; private set { _resultWaste = value; OnPropertyChanged(); } }

        private string _resultWastePct = "—";
        /// <summary>Weighted waste share of the consumed stock, %.</summary>
        public string ResultWastePct { get => _resultWastePct; private set { _resultWastePct = value; OnPropertyChanged(); } }

        /// <summary>Pieces the engine could not place on any stock (labels).</summary>
        public ObservableCollection<string> UnplacedRows { get; } = new();

        private bool _hasUnplaced;
        public bool HasUnplaced { get => _hasUnplaced; private set { _hasUnplaced = value; OnPropertyChanged(); } }

        // ── RESULT tab: per-cutting-group breakdown ─────────────────────
        // One run per glass composition — different glass never mixes on a
        // sheet. Each group reports its own sheets / utilization / waste.

        public sealed class CuttingGroupResultVm
        {
            public string Composition { get; set; } = string.Empty;
            public string Thickness { get; set; } = string.Empty;
            public string PieceCount { get; set; } = string.Empty;
            public string Sheets { get; set; } = string.Empty;
            public string Utilization { get; set; } = string.Empty;
            public string Waste { get; set; } = string.Empty;
            public string Unplaced { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
            public bool HasNote => !string.IsNullOrWhiteSpace(Note);
        }

        public ObservableCollection<CuttingGroupResultVm> GroupResults { get; } = new();

        /// <summary>Private per-composition run record (composition, margins, result).</summary>
        private sealed class GroupRun
        {
            public string Composition = string.Empty;
            public string Thickness = string.Empty;
            public int PieceCount;
            public MarginProfile Margins = null!;
            public OptimizationResult Result = null!;
            public double UtilizationPct;
            public double WasteM2;
            public string UnplacedLabel = "0";
            public string OracleLabel = string.Empty;
            public string SheetsLabel = string.Empty;
            public string MarginNote = string.Empty;
        }

        /// <summary>Flat sheet index → (group run, sheet index in that run).</summary>
        private sealed class FlatSheet
        {
            public GroupRun Run = null!;
            public int SheetIndex;
        }

        private readonly List<GroupRun> _groupRuns = new();
        private readonly List<FlatSheet> _flatSheets = new();

        public ICommand RunQuickCommand { get; }
        public ICommand RunOptimizeCommand { get; }

        private string _runMessage = string.Empty;
        public string RunMessage { get => _runMessage; private set { _runMessage = value; OnPropertyChanged(); } }

        // ── async run state (progress bar + button gating) ─────────────
        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRun)); } }

        /// <summary>Run buttons are enabled only when no run is in flight.</summary>
        public bool CanRun => !_isRunning;

        private string _runStageLabel = string.Empty;
        public string RunStageLabel { get => _runStageLabel; private set { _runStageLabel = value; OnPropertyChanged(); } }

        // ── 2D VIEW tab ────────────────────────────────────────────────

        public sealed class PieceRectVm
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string Label { get; set; } = string.Empty;
            public bool Rotated { get; set; }
        }

        public ObservableCollection<PieceRectVm> SheetRects { get; } = new();

        private double _renderWidth;
        private double _renderHeight;
        private string _previewCaption = string.Empty;
        private int _selectedSheetIndex;
        private double _previewScale = 1.0;

        public double RenderWidth { get => _renderWidth; private set { _renderWidth = value; OnPropertyChanged(); } }
        public double RenderHeight { get => _renderHeight; private set { _renderHeight = value; OnPropertyChanged(); } }
        public string PreviewCaption { get => _previewCaption; private set { _previewCaption = value; OnPropertyChanged(); } }

        /// <summary>Canvas zoom factor (0.4× .. 2.0×) for the 2D layout view.</summary>
        public double PreviewScale
        {
            get => _previewScale;
            private set
            {
                double clamped = System.Math.Clamp(value, 0.4, 2.0);
                if (_previewScale == clamped) return;
                _previewScale = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewScaleLabel));
                RebuildPreview();
            }
        }

        public string PreviewScaleLabel => (_previewScale * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        // ── 2D chrome: scaled margin zone (usable rectangle) ────────────
        private double _usableX;
        private double _usableY;
        private double _usableW;
        private double _usableH;
        public double UsableX { get => _usableX; private set { _usableX = value; OnPropertyChanged(); } }
        public double UsableY { get => _usableY; private set { _usableY = value; OnPropertyChanged(); } }
        public double UsableW { get => _usableW; private set { _usableW = value; OnPropertyChanged(); } }
        public double UsableH { get => _usableH; private set { _usableH = value; OnPropertyChanged(); } }

        private string _previewStockLabel = string.Empty;
        public string PreviewStockLabel { get => _previewStockLabel; private set { _previewStockLabel = value; OnPropertyChanged(); } }

        private string _previewUtilization = "—";
        public string PreviewUtilization { get => _previewUtilization; private set { _previewUtilization = value; OnPropertyChanged(); } }

        private string _previewPiecesCount = "—";
        public string PreviewPiecesCount { get => _previewPiecesCount; private set { _previewPiecesCount = value; OnPropertyChanged(); } }

        private string _marginZoneLabel = string.Empty;
        public string MarginZoneLabel { get => _marginZoneLabel; private set { _marginZoneLabel = value; OnPropertyChanged(); } }

        private bool _hasPreview;
        public bool HasPreview { get => _hasPreview; private set { _hasPreview = value; OnPropertyChanged(); } }

        public int SelectedSheetIndex
        {
            get => _selectedSheetIndex;
            set
            {
                if (_selectedSheetIndex == value) return;
                _selectedSheetIndex = value;
                OnPropertyChanged();
                RebuildPreview();
            }
        }

        public ICommand SelectPreviousSheetCommand { get; }
        public ICommand SelectNextSheetCommand { get; }
        public ICommand SelectSheetCommand { get; }
        public ICommand PreviewZoomInCommand { get; }
        public ICommand PreviewZoomOutCommand { get; }
        public ICommand PreviewZoomResetCommand { get; }

        // ── ctor ───────────────────────────────────────────────────────

        public OptimizationViewModel()
        {
            AddPieceCommand = new RelayCommand(_ => AddPiece());
            RemovePieceCommand = new RelayCommand(p => RemovePiece(p as PieceEntryVm));
            ClearPiecesCommand = new RelayCommand(_ => { Pieces.Clear(); RefreshDerived(); });
            LoadReferenceJobCommand = new RelayCommand(_ => LoadReferenceJob());
            RefreshJobsCommand = new RelayCommand(_ => LoadJobsAsync());
            AddSelectedPanesCommand = new RelayCommand(_ => AddSelectedPanes());
            SelectAllPanesCommand = new RelayCommand(_ => SetAllPanesSelected(true));
            ClearPaneSelectionCommand = new RelayCommand(_ => SetAllPanesSelected(false));
            LoadJobsAsync();

            AddStockCommand = new RelayCommand(_ => AddStock());
            RemoveStockCommand = new RelayCommand(p => RemoveStock(p as StockEntryVm));

            LoadHouseDefaultsCommand = new RelayCommand(_ => LoadHouseDefaults());

            RunQuickCommand = new RelayCommand(_ => Run(stage: "QUICK"));
            RunOptimizeCommand = new RelayCommand(_ => Run(stage: "OPTIMIZE"));

            SelectPreviousSheetCommand = new RelayCommand(_ =>
            {
                if (SelectedSheetIndex > 0) SelectedSheetIndex--;
            });
            SelectNextSheetCommand = new RelayCommand(_ =>
            {
                if (SelectedSheetIndex < _flatSheets.Count - 1)
                    SelectedSheetIndex++;
            });
            SelectSheetCommand = new RelayCommand(p =>
            {
                if (p is int idx && idx >= 0 && idx < _flatSheets.Count)
                {
                    SelectedSheetIndex = idx;
                    SelectedTabIndex = 5; // jump straight to the 2D VIEW
                }
            });
            PreviewZoomInCommand = new RelayCommand(_ => PreviewScale += 0.25);
            PreviewZoomOutCommand = new RelayCommand(_ => PreviewScale -= 0.25);
            PreviewZoomResetCommand = new RelayCommand(_ => PreviewScale = 1.0);

            foreach (var s in DefaultStockPool()) Stocks.Add(s);
            RefreshDerived();
        }

        private static IEnumerable<StockEntryVm> DefaultStockPool()
        {
            yield return new StockEntryVm("3210", "2250");
            yield return new StockEntryVm("2500", "1875");
            yield return new StockEntryVm("2440", "1830");
            yield return new StockEntryVm("3660", "2440");
        }

        // ── piece list ops ─────────────────────────────────────────────

        /// <summary>Creates a list piece with the edit callback wired so the
        /// summary / merge groups / cut plan refresh on inline qty edits.</summary>
        private PieceEntryVm NewPieceRow(string glass, string label, string w, string h, string qty)
        {
            var row = new PieceEntryVm
            {
                GlassRef = glass,
                PieceLabel = label,
                WidthText = w,
                HeightText = h,
                QtyText = qty
            };
            row.OnEdited = () => RefreshDerived();
            return row;
        }

        private void AddPiece()
        {
            if (!NewPiece.TryParse(out _, out _, out _))
            {
                RunMessage = "Piece needs a label, positive W×H (mm) and qty ≥ 1.";
                return;
            }

            Pieces.Add(NewPieceRow(
                NewPiece.GlassRef?.Trim() ?? string.Empty,
                NewPiece.PieceLabel?.Trim() ?? string.Empty,
                NewPiece.WidthText, NewPiece.HeightText, NewPiece.QtyText));
            NewPiece = new PieceEntryVm { GlassRef = NewPiece.GlassRef, PieceLabel = string.Empty };
            RunMessage = string.Empty;
            RefreshDerived();
        }

        private void RemovePiece(PieceEntryVm entry)
        {
            if (entry != null) Pieces.Remove(entry);
            RefreshDerived();
        }

        /// <summary>
        /// Lists every confirmed (non-draft, non-void) Job Order and expands
        /// its cutting pieces via the production distribution engine — the
        /// same code the floor uses, so sizes/semantics never drift.
        /// Never throws: any failure surfaces as a status line.
        /// </summary>
        private async void LoadJobsAsync()
        {
            JobsStatusText = "Loading confirmed job orders…";
            try
            {
                var repo = App.ServiceProvider?.GetService(typeof(IJobOrderRepository)) as IJobOrderRepository;
                if (repo == null)
                {
                    JobsStatusText = "Job store is not available yet — use Refresh.";
                    return;
                }

                var all = await repo.GetAllAsync().ConfigureAwait(false);
                var options = new List<JobOptionVm>();

                foreach (var jo in all
                    .Where(j => j != null && !j.IsVoid && !JobOrderStatusValues.Is(j.Status, JobOrderStatusValues.Draft))
                    .OrderByDescending(j => j.UpdatedAt)
                    .Take(100))
                {
                    var snapshot = JobOrderMapper.DeserializeSnapshot(jo);
                    var invoice = snapshot?.EmbeddedInvoice;
                    var plan = ProductionDistributionEngine.Distribute(jo.JobOrderId, invoice);

                    var cut = plan.Items
                        .Where(i => i.Stage == ProductionStage.Cutting && i.WidthMm > 0 && i.HeightMm > 0)
                        .ToList();

                    var opt = new JobOptionVm
                    {
                        JobOrderId = jo.JobOrderId,
                        CustomerName = jo.CustomerName
                    };

                    // ── Step 4/5 pane decomposition ─────────────────────
                    // Each spec group becomes its physical panes (Outer /
                    // Inner / Lam …) with the pane's OWN glass composition,
                    // using the SAME pane routing the production floor uses
                    // (W1/H1 = outer, W2/H2 = inner, equal-size fallback).
                    // Two specs sharing one outer pane (low-e on a clear DGU
                    // and on an obscure DGU) expose it once per spec — the
                    // operator checks both and the SIZES merge folds them
                    // into ONE cutting group by composition.
                    var groups = (invoice?.SpecGroups ?? new List<SpecGroupSnapshot>())
                        .Where(g => g != null)
                        .OrderBy(g => g.GroupNumber)
                        .ToList();

                    foreach (var group in groups)
                    {
                        string buildType = ProductionDistributionEngine.ResolveBuildType(group, invoice);
                        var occurrences = ProductionDistributionEngine.ResolvePaneOccurrences(buildType, group);

                        foreach (var occ in occurrences)
                        {
                            var paneItems = cut
                                .Where(i => i.SpecGroupNumber == group.GroupNumber
                                    && string.Equals(i.Position, occ.Position, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            if (paneItems.Count == 0) continue;

                            string composition = string.IsNullOrWhiteSpace(occ.Composition)
                                ? group.SelectedSpecName ?? string.Empty
                                : occ.Composition;

                            var pane = new PaneChoiceVm
                            {
                                SpecGroupNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? string.Empty,
                                BuildType = buildType,
                                Position = occ.Position,
                                Composition = composition,
                                Thickness = occ.ThicknessMm > 0
                                    ? occ.ThicknessMm.ToString(CultureInfo.InvariantCulture) + "mm"
                                    : ParseThicknessLabel(composition),
                                Treatment = occ.Treatment
                            };

                            // merge identical sizes; the row refs ride along
                            // as the label so the 2D view and cut plan stay
                            // traceable back to Step 5.
                            foreach (var sizeGroup in paneItems
                                .GroupBy(i => new { i.WidthMm, i.HeightMm })
                                .OrderBy(g => g.Key.WidthMm).ThenBy(g => g.Key.HeightMm))
                            {
                                int qty = sizeGroup.Sum(i => i.Qty);
                                string label = string.Join(", ",
                                    sizeGroup.Select(i => i.GlassRef)
                                             .Where(r => !string.IsNullOrWhiteSpace(r))
                                             .Distinct());
                                pane.Pieces.Add((
                                    composition,
                                    string.IsNullOrWhiteSpace(label) ? "—" : label,
                                    sizeGroup.Key.WidthMm, sizeGroup.Key.HeightMm, qty));
                            }

                            pane.TotalPcs = pane.Pieces.Sum(p => p.Qty);
                            double paneArea = pane.Pieces.Sum(p => p.W / 1000.0 * p.H / 1000.0 * p.Qty);
                            pane.AreaLabel = paneArea.ToString("0.00", CultureInfo.InvariantCulture) + " m²";
                            pane.CountLabel = pane.TotalPcs.ToString(CultureInfo.InvariantCulture) + " pcs";
                            pane.SizesSummary = BuildSizesSummary(pane.Pieces);

                            opt.PaneChoices.Add(pane);
                            foreach (var p in pane.Pieces) opt.Pieces.Add(p);
                        }
                    }

                    int pcs = opt.PaneChoices.Sum(p => p.TotalPcs);
                    double m2 = opt.PaneChoices.Sum(p => p.Pieces.Sum(x => x.W / 1000.0 * x.H / 1000.0 * x.Qty));
                    int specCount = opt.PaneChoices.Select(p => p.SpecGroupNumber).Distinct().Count();
                    opt.PieceRows = opt.PaneChoices.Count;
                    opt.Summary = string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1} pcs · {2:0.0} m² · {3} spec(s)",
                        jo.CustomerName, pcs, m2, specCount);

                    if (opt.PaneChoices.Count > 0) options.Add(opt);
                }

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        AvailableJobs.Clear();
                        foreach (var o in options) AvailableJobs.Add(o);
                        SelectedJob = null;
                        RefreshJobsFilter();
                        JobsStatusText = options.Count == 0
                            ? "No confirmed job orders with cutting pieces yet."
                            : string.Format(CultureInfo.InvariantCulture,
                                "{0} confirmed job order(s) ready.", options.Count);
                    });
                }
            }
            catch (Exception ex)
            {
                JobsStatusText = "Job list unavailable: " + ex.Message;
            }
        }

        /// <summary>
        /// Rebuilds <see cref="FilteredJobs"/> from <see cref="AvailableJobs"/>
        /// using <see cref="JobSearchText"/> (job id / customer / summary).
        /// Runs on the UI thread — callers marshal beforehand.
        /// </summary>
        private void RefreshJobsFilter()
        {
            string q = (_jobSearchText ?? string.Empty).Trim();
            IEnumerable<JobOptionVm> items = string.IsNullOrEmpty(q)
                ? AvailableJobs
                : AvailableJobs.Where(j =>
                    (j.JobOrderId ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (j.CustomerName ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (j.Summary ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));

            FilteredJobs.Clear();
            foreach (var item in items) FilteredJobs.Add(item);
        }

        // ── composition helpers ─────────────────────────────────────────

        /// <summary>Leading thickness of a glass composition, e.g. "6mm clear …" → "6mm".</summary>
        private static string ParseThicknessLabel(string? composition)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                composition ?? string.Empty, @"^\s*(\d+(?:\.\d+)?)\s*mm");
            return m.Success ? m.Groups[1].Value + "mm" : string.Empty;
        }

        /// <summary>Leading thickness in mm (0 when unreadable).</summary>
        private static int ParseThicknessMm(string? composition)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                composition ?? string.Empty, @"^\s*(\d+(?:\.\d+)?)\s*mm");
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int t) ? t : 0;
        }

        /// <summary>Compact "first sizes + count" preview for a pane choice row.</summary>
        private static string BuildSizesSummary(List<(string Glass, string Label, int W, int H, int Qty)> pieces)
        {
            var parts = new List<string>();
            int shown = 0;
            foreach (var p in pieces)
            {
                parts.Add(p.W.ToString(CultureInfo.InvariantCulture) + "×" + p.H.ToString(CultureInfo.InvariantCulture));
                if (++shown >= 3) break;
            }
            if (pieces.Count > shown) parts.Add("+" + (pieces.Count - shown).ToString(CultureInfo.InvariantCulture) + " more");
            return string.Join(", ", parts);
        }

        /// <summary>Shows the selected job's pane checklist for multi-select.</summary>
        private void PopulateJobPanes(JobOptionVm? job)
        {
            foreach (var pane in JobPaneChoices)
                pane.PropertyChanged -= OnPaneSelectionChanged;

            JobPaneChoices.Clear();
            if (job != null)
            {
                foreach (var pane in job.PaneChoices)
                {
                    pane.PropertyChanged += OnPaneSelectionChanged;
                    JobPaneChoices.Add(pane);
                }
            }

            HasPaneChoices = JobPaneChoices.Count > 0;
            RefreshPaneSelectionCount();
        }

        private void OnPaneSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PaneChoiceVm.IsSelected)) return;
            RefreshPaneSelectionCount();
        }

        private void RefreshPaneSelectionCount()
        {
            SelectedPaneCount = JobPaneChoices.Count(p => p.IsSelected);
        }

        private void SetAllPanesSelected(bool selected)
        {
            foreach (var pane in JobPaneChoices) pane.IsSelected = selected;
            RefreshPaneSelectionCount();
        }

        /// <summary>
        /// Adds the checked panes' cutting pieces to the run. Each piece keeps
        /// its per-pane glass composition (so SIZES merges the 76 low-e outer
        /// panes into ONE group, 64 clear and 12 ramly into their own) and its
        /// pane position for the 2D view.
        /// </summary>
        private void AddSelectedPanes()
        {
            var selected = JobPaneChoices.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0)
            {
                RunMessage = "Select at least one pane (Step 4/5 outer or inner) to add.";
                return;
            }

            int addedPieces = 0;
            foreach (var pane in selected)
            {
                foreach (var (glass, label, w, h, qty) in pane.Pieces)
                {
                    var row = NewPieceRow(
                        glass, label,
                        w.ToString(CultureInfo.InvariantCulture),
                        h.ToString(CultureInfo.InvariantCulture),
                        qty.ToString(CultureInfo.InvariantCulture));
                    row.Position = pane.Position;
                    Pieces.Add(row);
                    addedPieces += qty;
                }
            }

            RefreshDerived();

            var compositions = selected
                .Select(p => p.Composition)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "Added {0} pane(s) → {1} pieces in {2} cutting group(s): {3}.",
                selected.Count, addedPieces, compositions.Count,
                string.Join(" · ", compositions));
        }

        /// <summary>
        /// Loads the owner's calibration DGU job — the benchmark the engine
        /// is pinned against (Low-E 17 sheets @ 82.77%). Decomposed exactly
        /// as Step 4/5 records it:
        ///   • Spec 1 — "…low e (outer) + 12mm asp + 6mm clear (inner)": 64 units.
        ///   • Spec 2 — "…low e (outer) + 12mm asp + 6mm obscure ramly (inner)": 12 units.
        ///   • OUTER = 6mm Climaguard low-e on BOTH specs → 76 pieces.
        ///   • INNER = 64 clear + 12 ramly obscure.
        /// Total 152 pieces in three cutting groups.
        /// </summary>
        private void LoadReferenceJob()
        {
            const string lowE = "6mm climaguard hp neutral 70 low e ft glass";
            const string clear = "6mm clear ft glass";
            const string ramly = "6mm obscure clear ramly ft glass";

            // Step 5 rows: ref, W×H (unit size — equal-size DGU, both panes
            // cut the same), qty. Spec 1 first (34 rows), Spec 2 (12 rows).
            (string Ref, int W, int H, int Qty)[] spec1 =
            {
                ("11",1050,2127,2),("9",1054,2130,2),("10",1055,2130,2),("12",800,1798,2),
                ("1",905,2536,2),("2",1054,1986,2),("4",854,1981,2),("7",1003,1857,2),
                ("3",856,1970,2),("8",844,1457,2),("13",650,1382,2),("14",650,1372,2),
                ("2",605,1471,2),("5",553,1448,2),("SA",499,1430,2),("S",509,1421,2),
                ("R17",1815,725,1),("R8",1515,530,1),("R10",2013,828,1),("R10",2513,535,1),
                ("A2",1055,1985,2),("A11",1046,2130,2),("A9",1050,2130,2),("A10",1055,2130,2),
                ("A7",968,1889,2),("A4",850,1970,2),("A3",837,1984,2),("A1",838,2532,2),
                ("A14",657,1350,2),("A8",935,1418,2),("A13",652,1350,2),("A5",654,1479,2),
                ("A6",655,1474,2),("A12",800,1805,2)
            };

            (string Ref, int W, int H)[] spec2 =
            {
                ("A7",525,380),("A11",725,430),("A13",720,430),("A14",720,430),
                ("A18",725,430),("A19",725,430),("8",520,375),("12",725,430),
                ("14",710,430),("15",730,430),("19",720,430),("20",720,430)
            };

            Pieces.Clear();
            void Add(string composition, string position, string label, int w, int h, int qty)
            {
                var row = NewPieceRow(
                    composition, label,
                    w.ToString(CultureInfo.InvariantCulture),
                    h.ToString(CultureInfo.InvariantCulture),
                    qty.ToString(CultureInfo.InvariantCulture));
                row.Position = position;
                Pieces.Add(row);
            }

            // 76 low-e OUTER panes — every unit of both specs.
            foreach (var (r, w, h, q) in spec1) Add(lowE, "Outer", r, w, h, q);
            foreach (var (r, w, h) in spec2) Add(lowE, "Outer", r, w, h, 1);
            // 64 clear INNER panes (Spec 1).
            foreach (var (r, w, h, q) in spec1) Add(clear, "Inner", r, w, h, q);
            // 12 ramly obscure INNER panes (Spec 2).
            foreach (var (r, w, h) in spec2) Add(ramly, "Inner", r, w, h, 1);

            RunMessage = "Reference calibration job loaded — 152 pieces: 76 low-e outer · 64 clear inner · 12 ramly inner.";
            RefreshDerived();
        }

        private void RefreshDerived()
        {
            int count = 0;
            double area = 0;
            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q)) continue;
                count += q;
                area += w / 1000.0 * (h / 1000.0) * q;
            }

            PieceSummary = count == 0
                ? "No pieces yet"
                : string.Format(CultureInfo.InvariantCulture,
                    "{0} pieces · {1:0.00} m² glass", count, area);

            HasPieces = count > 0;

            SizeGroups.Clear();
            foreach (var grp in Pieces
                .Where(p => p.TryParse(out _, out _, out _))
                .GroupBy(p => p.GlassRef ?? string.Empty)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                int gCount = 0;
                double gArea = 0;
                foreach (var p in grp)
                {
                    p.TryParse(out int w, out int h, out int q);
                    gCount += q;
                    gArea += w / 1000.0 * (h / 1000.0) * q;
                }
                SizeGroups.Add(new SizeGroupVm
                {
                    Composition = grp.Key,
                    Thickness = ParseThicknessLabel(grp.Key),
                    PieceCount = gCount.ToString(CultureInfo.InvariantCulture),
                    AreaM2 = gArea.ToString("0.00", CultureInfo.InvariantCulture)
                });
            }

            RebuildCutPlan();
        }

        /// <summary>
        /// Expands the piece list into the on-screen cut plan — one numbered
        /// row per physical piece (qty expands 1..N) with its ref, composition
        /// and size. Capped at <see cref="CutPlanDisplayCap"/> rows for the
        /// screen; the engine itself always cuts every piece.
        /// </summary>
        private void RebuildCutPlan()
        {
            CutPlanRows.Clear();
            int number = 0;
            int total = 0;
            bool truncated = false;

            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q)) continue;
                total += q;
                if (number >= CutPlanDisplayCap) { truncated = true; continue; }

                string composition = string.IsNullOrWhiteSpace(p.GlassRef) ? "—" : p.GlassRef.Trim();
                for (int i = 1; i <= q; i++)
                {
                    if (number >= CutPlanDisplayCap) { truncated = true; break; }
                    number++;
                    string refLabel = string.IsNullOrWhiteSpace(p.PieceLabel)
                        ? "—" : p.PieceLabel.Trim();
                    CutPlanRows.Add(new CutPlanRowVm
                    {
                        Number = number.ToString(CultureInfo.InvariantCulture),
                        Ref = q > 1
                            ? $"{refLabel} {i.ToString(CultureInfo.InvariantCulture)}/{q.ToString(CultureInfo.InvariantCulture)}"
                            : refLabel,
                        Composition = composition,
                        Size = $"{w.ToString(CultureInfo.InvariantCulture)} × {h.ToString(CultureInfo.InvariantCulture)} mm"
                    });
                }
            }

            HasCutPlan = CutPlanRows.Count > 0;
            CutPlanNote = truncated
                ? $"Showing the first {CutPlanDisplayCap.ToString(CultureInfo.InvariantCulture)} of " +
                  $"{total.ToString(CultureInfo.InvariantCulture)} pieces — the engine cuts all of them."
                : string.Empty;
        }

        // ── stock ops ──────────────────────────────────────────────────

        private void AddStock()
        {
            if (!int.TryParse(NewStock.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ||
                !int.TryParse(NewStock.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ||
                w <= 0 || h <= 0)
            {
                RunMessage = "Stock needs positive W×H in mm.";
                return;
            }
            Stocks.Add(new StockEntryVm(
                w.ToString(CultureInfo.InvariantCulture),
                h.ToString(CultureInfo.InvariantCulture))
            { AvailableText = NewStock.AvailableText });
            RunMessage = string.Empty;
        }

        private void RemoveStock(StockEntryVm entry)
        {
            if (entry != null && Stocks.Count > 1) Stocks.Remove(entry);
        }

        // ── margins ────────────────────────────────────────────────────

        private void LoadHouseDefaults()
        {
            int thickness = int.TryParse(ThicknessText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int t) && t > 0 ? t : 6;
            var m = MarginProfile.DefaultFor(thickness);
            ThicknessText = thickness.ToString(CultureInfo.InvariantCulture);
            LeftMarginText = m.LeftMm.ToString(CultureInfo.InvariantCulture);
            RightMarginText = m.RightMm.ToString(CultureInfo.InvariantCulture);
            TopMarginText = m.TopMm.ToString(CultureInfo.InvariantCulture);
            BottomMarginText = m.BottomMm.ToString(CultureInfo.InvariantCulture);
            KerfText = m.KerfMm.ToString(CultureInfo.InvariantCulture);
            MinBreakoutText = m.MinBreakoutMm.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryBuildInputs(
            out List<CutPiece> pieces, out List<StockSize> stocks, out MarginProfile margins)
        {
            pieces = new List<CutPiece>();
            stocks = new List<StockSize>();
            margins = null;

            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q))
                {
                    RunMessage = $"Piece \"{p.PieceLabel}\" is invalid — needs positive W×H and qty.";
                    return false;
                }
                for (int i = 0; i < q; i++)
                {
                    string baseLabel = string.IsNullOrWhiteSpace(p.PieceLabel)
                        ? (p.GlassRef ?? string.Empty).Trim()
                        : p.PieceLabel.Trim();
                    pieces.Add(new CutPiece
                    {
                        GlassRef = p.GlassRef ?? string.Empty,
                        // "G-101 3/24" — piece identity the 2D view / cut
                        // plan carry, matching the design doc's labelling.
                        PieceLabel = q > 1
                            ? $"{baseLabel} {i + 1}/{q}"
                            : baseLabel,
                        Position = p.Position ?? string.Empty,
                        ThicknessMm = ParseThicknessMm(p.GlassRef),
                        WidthMm = w,
                        HeightMm = h,
                        AllowRotate = RotationEnabled
                    });
                }
            }
            if (pieces.Count == 0)
            {
                RunMessage = "Add pieces first (JOBS tab) or load the reference job.";
                return false;
            }

            foreach (var s in Stocks)
            {
                if (!int.TryParse(s.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ||
                    !int.TryParse(s.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ||
                    w <= 0 || h <= 0)
                {
                    RunMessage = "Stock pool has an invalid size (STOCKS tab).";
                    return false;
                }
                int max = int.TryParse(s.AvailableText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int a) ? System.Math.Max(0, a) : 0;
                stocks.Add(new StockSize { WidthMm = w, HeightMm = h, MaxSheets = max });
            }
            if (stocks.Count == 0)
            {
                RunMessage = "Add at least one stock size (STOCKS tab).";
                return false;
            }

            int Parse(string text, string name, out int value)
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
                    return value;
                value = -1;
                RunMessage = $"{name} must be a whole number ≥ 0 (MARGINS tab).";
                return value;
            }

            int left = Parse(LeftMarginText, "Left margin", out int lm);
            int right = Parse(RightMarginText, "Right margin", out int rm);
            int top = Parse(TopMarginText, "Top margin", out int tm);
            int bottom = Parse(BottomMarginText, "Bottom margin", out int bm);
            int kerf = Parse(KerfText, "Kerf", out int k);
            int breakout = Parse(MinBreakoutText, "Minimum breakout", out int mb);
            if (left < 0 || right < 0 || top < 0 || bottom < 0 || kerf < 0 || breakout < 0)
                return false;

            int thickness = int.TryParse(ThicknessText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int th) && th > 0 ? th : 6;

            margins = new MarginProfile
            {
                ThicknessMm = thickness,
                LeftMm = left,
                RightMm = right,
                TopMm = top,
                BottomMm = bottom,
                KerfMm = kerf,
                MinBreakoutMm = breakout
            };
            return true;
        }

        // ── run ────────────────────────────────────────────────────────

        /// <summary>
        /// Validates inputs on the UI thread, then runs the packing engine
        /// per cutting group (glass composition) on a background thread —
        /// different glass never mixes on a sheet. Results are applied back
        /// on the dispatcher by <see cref="SafeFireAndForget"/>.
        /// </summary>
        private void Run(string stage)
        {
            if (IsRunning) return;
            if (!TryBuildInputs(out var pieces, out var stocks, out var margins))
                return;

            int effort = int.TryParse(SelectedEffortLevel, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int lvl) && lvl >= 1 && lvl <= 5 ? lvl : 3;
            var rotation = RotationEnabled ? RotationPolicy.Free90 : RotationPolicy.Locked;
            var groups = BuildCuttingGroups(pieces);

            IsRunning = true;
            RunStageLabel = stage == "OPTIMIZE"
                ? $"Optimizing {groups.Count.ToString(CultureInfo.InvariantCulture)} cutting group(s) · level {effort.ToString(CultureInfo.InvariantCulture)}…"
                : $"Quick packing {groups.Count.ToString(CultureInfo.InvariantCulture)} cutting group(s)…";
            RunMessage = "";

            AsyncUtility.SafeFireAndForget(
                () => RunCoreAsync(stage, groups, stocks, margins, rotation, effort),
                "Optimization run");
        }

        /// <summary>Groups the piece list by glass composition — one pack run each.</summary>
        private static List<(string Composition, List<CutPiece> Pieces)> BuildCuttingGroups(List<CutPiece> pieces)
        {
            return pieces
                .GroupBy(p => string.IsNullOrWhiteSpace(p.GlassRef)
                        ? "(unlabelled glass)" : p.GlassRef,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => (g.Key, g.ToList()))
                .ToList();
        }

        /// <summary>
        /// Thickness-aware margins: when a cutting group's glass thickness
        /// differs from the MARGINS tab, the group is cut with that
        /// thickness's house defaults and the note surfaces on the result.
        /// </summary>
        private static (MarginProfile Margins, string Note) ResolveGroupMargins(
            string composition, MarginProfile userMargins)
        {
            int thickness = ParseThicknessMm(composition);
            if (thickness <= 0 || thickness == userMargins.ThicknessMm)
                return (userMargins, string.Empty);
            return (MarginProfile.DefaultFor(thickness),
                "cut with " + thickness.ToString(CultureInfo.InvariantCulture) + "mm house margins");
        }

        private async Task RunCoreAsync(
            string stage, List<(string Composition, List<CutPiece> Pieces)> groups,
            List<StockSize> stocks, MarginProfile margins, RotationPolicy rotation, int effort)
        {
            try
            {
                var runs = new List<GroupRun>();
                foreach (var (composition, groupPieces) in groups)
                {
                    var (groupMargins, marginNote) = ResolveGroupMargins(composition, margins);

                    var result = await Task.Run(() => stage == "OPTIMIZE"
                        ? GuillotinePacker.PackOptimize(groupPieces, stocks, groupMargins, rotation, effort)
                        : GuillotinePacker.PackQuick(groupPieces, stocks, groupMargins, rotation))
                        .ConfigureAwait(true);

                    double glass = result.Sheets.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm));
                    double stockArea = result.Sheets.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm);
                    int problems = GuillotinePacker.Verify(result, groupMargins).Count;

                    runs.Add(new GroupRun
                    {
                        Composition = composition,
                        Thickness = ParseThicknessLabel(composition),
                        PieceCount = groupPieces.Count,
                        Margins = groupMargins,
                        Result = result,
                        UtilizationPct = stockArea == 0 ? 0 : glass / stockArea * 100.0,
                        WasteM2 = Math.Max(0, (stockArea - glass) / 1_000_000.0),
                        UnplacedLabel = result.Unplaced.Count.ToString(CultureInfo.InvariantCulture),
                        OracleLabel = problems == 0 ? "Verified" : problems.ToString(CultureInfo.InvariantCulture) + " issue(s)",
                        SheetsLabel = result.SheetsUsed.ToString(CultureInfo.InvariantCulture),
                        MarginNote = marginNote
                    });
                }

                ApplyResults(stage, runs, rotation);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void ApplyResults(string stage, List<GroupRun> runs, RotationPolicy rotation)
        {
            _groupRuns.Clear();
            _groupRuns.AddRange(runs);

            double glass = runs.Sum(r => r.Result.Sheets.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm)));
            double stockArea = runs.Sum(r => r.Result.Sheets.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm));
            double util = stockArea == 0 ? 0 : glass / stockArea * 100.0;
            int totalSheets = runs.Sum(r => r.Result.SheetsUsed);
            int totalUnplaced = runs.Sum(r => r.Result.Unplaced.Count);
            int totalProblems = runs.Sum(r => GuillotinePacker.Verify(r.Result, r.Margins).Count);

            string rotationLabel = rotation == RotationPolicy.Free90 ? "rotation ON" : "rotation LOCKED";
            ResultStage = stage == "OPTIMIZE"
                ? $"OPTIMIZE · level {SelectedEffortLevel} · {rotationLabel} · {runs.Count} cutting group(s)"
                : $"QUICK · {rotationLabel} · {runs.Count} cutting group(s)";
            ResultSheets = totalSheets.ToString(CultureInfo.InvariantCulture);
            ResultUtilization = util.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            ResultUnplaced = totalUnplaced.ToString(CultureInfo.InvariantCulture);
            OraclePassed = totalProblems == 0;
            ResultOracle = totalProblems == 0
                ? "Verified — no overlaps, margins & kerf respected"
                : string.Format(CultureInfo.InvariantCulture, "{0} layout issue(s)", totalProblems);

            // Waste = consumed stock − placed glass (the offcut the saw leaves).
            double wasteMm2 = Math.Max(0, stockArea - glass);
            ResultWaste = (wasteMm2 / 1_000_000.0).ToString("0.00", CultureInfo.InvariantCulture) + " m²";
            ResultWastePct = stockArea == 0 ? "0.0%"
                : ((stockArea - glass) / stockArea * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%";

            UnplacedRows.Clear();
            foreach (var r in runs)
            {
                foreach (var up in r.Result.Unplaced)
                {
                    string label = string.IsNullOrWhiteSpace(up.PieceLabel) ? up.GlassRef : up.PieceLabel;
                    UnplacedRows.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1} · {2} × {3} mm",
                        r.Composition, label, up.WidthMm, up.HeightMm));
                }
            }
            HasUnplaced = UnplacedRows.Count > 0;

            // Per-cutting-group breakdown (composition → sheets / util / waste).
            GroupResults.Clear();
            foreach (var r in runs)
            {
                GroupResults.Add(new CuttingGroupResultVm
                {
                    Composition = r.Composition,
                    Thickness = r.Thickness,
                    PieceCount = r.PieceCount.ToString(CultureInfo.InvariantCulture),
                    Sheets = r.SheetsLabel,
                    Utilization = r.UtilizationPct.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                    Waste = r.WasteM2.ToString("0.00", CultureInfo.InvariantCulture) + " m²",
                    Unplaced = r.UnplacedLabel,
                    Note = r.MarginNote
                });
            }

            // Filmstrip: flatten every sheet across all cutting groups.
            _flatSheets.Clear();
            SheetRows.Clear();
            int flatIndex = 0;
            foreach (var r in runs)
            {
                for (int i = 0; i < r.Result.Sheets.Count; i++)
                {
                    var sheet = r.Result.Sheets[i];
                    double sheetGlass = sheet.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
                    double sheetStock = (long)sheet.Stock.WidthMm * sheet.Stock.HeightMm;
                    double sheetWaste = sheetStock - sheetGlass;
                    double sheetUtil = sheetStock == 0 ? 0 : sheetGlass / sheetStock * 100.0;
                    _flatSheets.Add(new FlatSheet { Run = r, SheetIndex = i });
                    SheetRows.Add(new SheetRowVm
                    {
                        SheetNo = (flatIndex + 1).ToString(CultureInfo.InvariantCulture),
                        StockLabel = string.Format(CultureInfo.InvariantCulture,
                            "{0} × {1}", sheet.Stock.WidthMm, sheet.Stock.HeightMm),
                        PieceCount = sheet.Pieces.Count.ToString(CultureInfo.InvariantCulture),
                        Utilization = sheetUtil.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                        WasteLabel = (sheetWaste / 1_000_000.0).ToString("0.00", CultureInfo.InvariantCulture) + " m²",
                        Composition = r.Composition,
                        Index = flatIndex
                    });
                    flatIndex++;
                }
            }

            HasResult = true;
            _selectedSheetIndex = -1;
            SelectedSheetIndex = 0;
            RebuildPreview();

            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} cutting group(s), {2} sheet(s), {3:0.00}% utilization, {4} unplaced.",
                stage, runs.Count, totalSheets, util, totalUnplaced);

            SelectedTabIndex = 4; // RESULT tab
        }

        // ── 2D preview ─────────────────────────────────────────────────

        private const double PreviewRenderWidth = 860.0;

        private void RebuildPreview()
        {
            SheetRects.Clear();
            RefreshSheetSelection();

            if (_flatSheets.Count == 0 ||
                SelectedSheetIndex < 0 || SelectedSheetIndex >= _flatSheets.Count)
            {
                RenderWidth = 0;
                RenderHeight = 0;
                PreviewCaption = string.Empty;
                PreviewStockLabel = string.Empty;
                PreviewUtilization = "—";
                PreviewPiecesCount = "—";
                MarginZoneLabel = string.Empty;
                HasPreview = false;
                return;
            }

            HasPreview = true;
            var flat = _flatSheets[SelectedSheetIndex];
            var result = flat.Run.Result;
            var sheet = result.Sheets[flat.SheetIndex];
            var margins = flat.Run.Margins;

            double scale = PreviewRenderWidth * PreviewScale / sheet.Stock.WidthMm;
            RenderWidth = PreviewRenderWidth * PreviewScale;
            RenderHeight = sheet.Stock.HeightMm * scale;

            // Margin zone = the usable rectangle inside the edge trims.
            UsableX = sheet.UsableX * scale;
            UsableY = sheet.UsableY * scale;
            UsableW = sheet.UsableWidth * scale;
            UsableH = sheet.UsableHeight * scale;

            MarginZoneLabel = string.Format(CultureInfo.InvariantCulture,
                "margins LM {0} · RM {1} · TM {2} · BM {3} · kerf {4} mm",
                margins.LeftMm, margins.RightMm,
                margins.TopMm, margins.BottomMm, margins.KerfMm);

            PreviewStockLabel = string.Format(CultureInfo.InvariantCulture,
                "{0} × {1} mm", sheet.Stock.WidthMm, sheet.Stock.HeightMm);
            PreviewUtilization = sheet.UtilizationLabel;
            PreviewPiecesCount = sheet.Pieces.Count.ToString(CultureInfo.InvariantCulture);

            foreach (var p in sheet.Pieces)
            {
                string refLabel = string.IsNullOrWhiteSpace(p.Piece.PieceLabel)
                    ? p.Piece.GlassRef : p.Piece.PieceLabel;
                string positionTag = string.IsNullOrWhiteSpace(p.Piece.Position)
                    ? string.Empty : p.Piece.Position + " · ";
                SheetRects.Add(new PieceRectVm
                {
                    X = p.X * scale,
                    Y = p.Y * scale,
                    Width = System.Math.Max(1, p.WidthMm * scale),
                    Height = System.Math.Max(1, p.HeightMm * scale),
                    Rotated = p.Rotated,
                    Label = string.Format(CultureInfo.InvariantCulture, "{0}{1} · {2}×{3}{4}",
                        positionTag, refLabel,
                        p.Piece.WidthMm, p.Piece.HeightMm, p.Rotated ? " ⟳" : string.Empty)
                });
            }

            PreviewCaption = string.Format(CultureInfo.InvariantCulture,
                "{0} — sheet {1} of {2} — stock {3} × {4} mm — {5} pieces",
                flat.Run.Composition, flat.SheetIndex + 1, result.Sheets.Count,
                sheet.Stock.WidthMm, sheet.Stock.HeightMm, sheet.Pieces.Count);
        }

        private void RefreshSheetSelection()
        {
            foreach (var row in SheetRows)
                row.IsSelected = row.Index == SelectedSheetIndex;
        }
    }
}
