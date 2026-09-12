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
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;

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
            /// <summary>Cutting pieces (glass, label, w, h, qty) pre-expanded.</summary>
            public List<(string Glass, string Label, int W, int H, int Qty)> Pieces { get; } = new();
        }

        public ObservableCollection<JobOptionVm> AvailableJobs { get; } = new();

        private JobOptionVm? _selectedJob;
        public JobOptionVm? SelectedJob
        {
            get => _selectedJob;
            set { _selectedJob = value; OnPropertyChanged(); }
        }

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
        public ICommand LoadSelectedJobCommand { get; }


        public sealed class PieceEntryVm : ViewModels.ViewModelBase
        {
            private string _glassRef = "6mm clear ft glass";
            private string _pieceLabel = string.Empty;
            private string _widthText = string.Empty;
            private string _heightText = string.Empty;
            private string _qtyText = "1";

            public string GlassRef { get => _glassRef; set { _glassRef = value; OnPropertyChanged(); } }
            public string PieceLabel { get => _pieceLabel; set { _pieceLabel = value; OnPropertyChanged(); } }
            public string WidthText { get => _widthText; set { _widthText = value; OnPropertyChanged(); } }
            public string HeightText { get => _heightText; set { _heightText = value; OnPropertyChanged(); } }
            public string QtyText { get => _qtyText; set { _qtyText = value; OnPropertyChanged(); } }

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
            public string PieceCount { get; set; } = string.Empty;
            public string AreaM2 { get; set; } = string.Empty;
        }

        public ObservableCollection<SizeGroupVm> SizeGroups { get; } = new();

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

        public sealed class SheetRowVm
        {
            public string SheetNo { get; set; } = string.Empty;
            public string StockLabel { get; set; } = string.Empty;
            public string PieceCount { get; set; } = string.Empty;
            public string Utilization { get; set; } = string.Empty;
            public int Index { get; set; }
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

        public ICommand RunQuickCommand { get; }
        public ICommand RunOptimizeCommand { get; }

        private string _runMessage = string.Empty;
        public string RunMessage { get => _runMessage; private set { _runMessage = value; OnPropertyChanged(); } }

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

        public double RenderWidth { get => _renderWidth; private set { _renderWidth = value; OnPropertyChanged(); } }
        public double RenderHeight { get => _renderHeight; private set { _renderHeight = value; OnPropertyChanged(); } }
        public string PreviewCaption { get => _previewCaption; private set { _previewCaption = value; OnPropertyChanged(); } }

        public int SelectedSheetIndex
        {
            get => _selectedSheetIndex;
            set { _selectedSheetIndex = value; OnPropertyChanged(); RebuildPreview(); }
        }

        public ICommand SelectPreviousSheetCommand { get; }
        public ICommand SelectNextSheetCommand { get; }

        private OptimizationResult _lastResult;
        private MarginProfile _lastMargins;

        // ── ctor ───────────────────────────────────────────────────────

        public OptimizationViewModel()
        {
            AddPieceCommand = new RelayCommand(_ => AddPiece());
            RemovePieceCommand = new RelayCommand(p => RemovePiece(p as PieceEntryVm));
            ClearPiecesCommand = new RelayCommand(_ => { Pieces.Clear(); RefreshDerived(); });
            LoadReferenceJobCommand = new RelayCommand(_ => LoadReferenceJob());
            RefreshJobsCommand = new RelayCommand(_ => LoadJobsAsync());
            LoadSelectedJobCommand = new RelayCommand(_ => LoadSelectedJob());
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
                if (_lastResult != null && SelectedSheetIndex < _lastResult.Sheets.Count - 1)
                    SelectedSheetIndex++;
            });

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

        private void AddPiece()
        {
            if (!NewPiece.TryParse(out _, out _, out _))
            {
                RunMessage = "Piece needs a label, positive W×H (mm) and qty ≥ 1.";
                return;
            }

            Pieces.Add(new PieceEntryVm
            {
                GlassRef = NewPiece.GlassRef?.Trim() ?? string.Empty,
                PieceLabel = NewPiece.PieceLabel?.Trim() ?? string.Empty,
                WidthText = NewPiece.WidthText,
                HeightText = NewPiece.HeightText,
                QtyText = NewPiece.QtyText
            });
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
                    var plan = ProductionDistributionEngine.Distribute(
                        jo.JobOrderId, snapshot?.EmbeddedInvoice);

                    var cut = plan.Items
                        .Where(i => i.Stage == ProductionStage.Cutting && i.WidthMm > 0 && i.HeightMm > 0)
                        .ToList();

                    var opt = new JobOptionVm
                    {
                        JobOrderId = jo.JobOrderId,
                        CustomerName = jo.CustomerName
                    };

                    foreach (var g in cut
                        .GroupBy(i => new { i.GlassRef, i.WidthMm, i.HeightMm, Label = i.SpecName })
                        .OrderBy(g => g.Key.Label))
                    {
                        int qty = g.Sum(i => i.Qty);
                        opt.Pieces.Add((g.Key.GlassRef, g.Key.Label, g.Key.WidthMm, g.Key.HeightMm, qty));
                    }

                    int pcs = opt.Pieces.Sum(p => p.Qty);
                    double m2 = opt.Pieces.Sum(p => p.W / 1000.0 * p.H / 1000.0 * p.Qty);
                    opt.PieceRows = opt.Pieces.Count;
                    opt.Summary = string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1} pcs · {2:0.0} m²", jo.CustomerName, pcs, m2);

                    if (opt.Pieces.Count > 0) options.Add(opt);
                }

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        AvailableJobs.Clear();
                        foreach (var o in options) AvailableJobs.Add(o);
                        SelectedJob = null;
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

        /// <summary>Copies the selected JO's cutting pieces into the run.</summary>
        private void LoadSelectedJob()
        {
            if (SelectedJob == null)
            {
                RunMessage = "Select a confirmed job order first.";
                return;
            }

            Pieces.Clear();
            foreach (var (glass, label, w, h, qty) in SelectedJob.Pieces)
            {
                Pieces.Add(new PieceEntryVm
                {
                    GlassRef = glass,
                    PieceLabel = label,
                    WidthText = w.ToString(CultureInfo.InvariantCulture),
                    HeightText = h.ToString(CultureInfo.InvariantCulture),
                    QtyText = qty.ToString(CultureInfo.InvariantCulture)
                });
            }

            RefreshDerived();
            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "Loaded {0} cutting rows from {1}.", SelectedJob.PieceRows, SelectedJob.JobOrderId);
        }

        /// <summary>
        /// Loads the owner's calibration DGU job (Spec 1 low-e + clear
        /// panes, Spec 2 ramly outers) — the benchmark the engine is
        /// pinned against (Low-E 17 sheets @ 82.77%).
        /// </summary>
        private void LoadReferenceJob()
        {
            const string lowE = "6mm climaguard hp neutral 70 low e ft glass";
            const string clear = "6mm clear ft glass";
            const string ramly = "6mm obscure clear ramly ft glass";

            Pieces.Clear();
            void Add(string composition, string label, int w, int h, int qty) =>
                Pieces.Add(new PieceEntryVm
                {
                    GlassRef = composition,
                    PieceLabel = label,
                    WidthText = w.ToString(CultureInfo.InvariantCulture),
                    HeightText = h.ToString(CultureInfo.InvariantCulture),
                    QtyText = qty.ToString(CultureInfo.InvariantCulture)
                });

            // Spec 1 — BIG group.
            Add(clear, "11", 1050, 2127, 2); Add(clear, "9", 1054, 2130, 2);
            Add(clear, "10", 1055, 2130, 2); Add(clear, "12", 800, 1798, 2);
            Add(lowE, "1", 905, 2536, 2); Add(clear, "2", 1054, 1986, 2);
            Add(clear, "4", 854, 1981, 2); Add(clear, "7", 1003, 1857, 2);
            Add(clear, "3", 856, 1970, 2); Add(clear, "8", 844, 1457, 2);
            Add(clear, "13", 650, 1382, 2); Add(clear, "14", 650, 1372, 2);
            Add(clear, "2S", 605, 1471, 2); Add(clear, "5", 553, 1448, 2);
            Add(clear, "SA", 499, 1430, 2); Add(clear, "S", 509, 1421, 2);
            Add(clear, "R17", 1815, 725, 1); Add(clear, "R8", 1515, 530, 1);
            Add(clear, "R10a", 2013, 828, 1); Add(clear, "R10b", 2513, 535, 1);

            // Spec 1 — A group.
            Add(clear, "A2", 1055, 1985, 2); Add(clear, "A11", 1046, 2130, 2);
            Add(clear, "A9", 1050, 2130, 2); Add(clear, "A10", 1055, 2130, 2);
            Add(clear, "A7", 968, 1889, 2); Add(clear, "A4", 850, 1970, 2);
            Add(clear, "A3", 837, 1984, 2); Add(lowE, "A1", 838, 2532, 2);
            Add(clear, "A14", 657, 1350, 2); Add(clear, "A8", 935, 1418, 2);
            Add(clear, "A13", 652, 1350, 2); Add(clear, "A5", 654, 1479, 2);
            Add(clear, "A6", 655, 1474, 2); Add(clear, "A12", 800, 1805, 2);

            // Spec 2 — ramly outers (clear panes of the obscure DGU).
            Add(ramly, "A7", 525, 380, 1); Add(ramly, "A11", 725, 430, 1);
            Add(ramly, "A13", 720, 430, 1); Add(ramly, "A14", 720, 430, 1);
            Add(ramly, "A18", 725, 430, 1); Add(ramly, "A19", 725, 430, 1);
            Add(ramly, "8", 520, 375, 1); Add(ramly, "12", 725, 430, 1);
            Add(ramly, "14", 710, 430, 1); Add(ramly, "15", 730, 430, 1);
            Add(ramly, "19", 720, 430, 1); Add(ramly, "20", 720, 430, 1);

            RunMessage = "Reference calibration job loaded (80 pieces, 101.6 m²).";
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
                    PieceCount = gCount.ToString(CultureInfo.InvariantCulture),
                    AreaM2 = gArea.ToString("0.00", CultureInfo.InvariantCulture)
                });
            }
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
                    pieces.Add(new CutPiece
                    {
                        GlassRef = p.GlassRef ?? string.Empty,
                        PieceLabel = q > 1
                            ? $"{p.PieceLabel}-{i + 1}"
                            : p.PieceLabel,
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

        private void Run(string stage)
        {
            if (!TryBuildInputs(out var pieces, out var stocks, out var margins))
                return;

            var rotation = RotationEnabled ? RotationPolicy.Free90 : RotationPolicy.Locked;
            var result = stage == "OPTIMIZE"
                ? GuillotinePacker.PackOptimize(pieces, stocks, margins, rotation,
                    int.TryParse(SelectedEffortLevel, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int lvl) && lvl >= 1 && lvl <= 5 ? lvl : 3)
                : GuillotinePacker.PackQuick(pieces, stocks, margins, rotation);

            _lastResult = result;
            _lastMargins = margins;

            var problems = GuillotinePacker.Verify(result, margins);
            double glass = result.Sheets.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm));
            double stockArea = result.Sheets.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm);
            double util = stockArea == 0 ? 0 : glass / stockArea * 100.0;

            ResultStage = stage == "OPTIMIZE"
                ? $"OPTIMIZE · level {SelectedEffortLevel} · {(RotationEnabled ? "rotation ON" : "rotation LOCKED")}"
                : $"QUICK · {(RotationEnabled ? "rotation ON" : "rotation LOCKED")}";
            ResultSheets = result.SheetsUsed.ToString(CultureInfo.InvariantCulture);
            ResultUtilization = util.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            ResultUnplaced = result.Unplaced.Count.ToString(CultureInfo.InvariantCulture);
            OraclePassed = problems.Count == 0;
            ResultOracle = problems.Count == 0
                ? "Verified — no overlaps, margins & kerf respected"
                : string.Format(CultureInfo.InvariantCulture, "{0} layout issue(s)", problems.Count);

            SheetRows.Clear();
            for (int i = 0; i < result.Sheets.Count; i++)
            {
                var sheet = result.Sheets[i];
                double sheetGlass = sheet.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
                double sheetStock = (long)sheet.Stock.WidthMm * sheet.Stock.HeightMm;
                SheetRows.Add(new SheetRowVm
                {
                    SheetNo = (i + 1).ToString(CultureInfo.InvariantCulture),
                    StockLabel = string.Format(CultureInfo.InvariantCulture,
                        "{0} × {1}", sheet.Stock.WidthMm, sheet.Stock.HeightMm),
                    PieceCount = sheet.Pieces.Count.ToString(CultureInfo.InvariantCulture),
                    Utilization = (sheetStock == 0 ? 0 : sheetGlass / sheetStock * 100.0)
                        .ToString("0.0", CultureInfo.InvariantCulture) + "%",
                    Index = i
                });
            }

            HasResult = true;
            SelectedSheetIndex = 0;
            RebuildPreview();

            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} sheet(s), {2:0.00}% utilization, {3} unplaced.",
                stage, result.SheetsUsed, util, result.Unplaced.Count);

            SelectedTabIndex = 4; // RESULT tab
        }

        // ── 2D preview ─────────────────────────────────────────────────

        private const double PreviewRenderWidth = 860.0;

        private void RebuildPreview()
        {
            SheetRects.Clear();
            if (_lastResult == null || _lastResult.Sheets.Count == 0 ||
                SelectedSheetIndex < 0 || SelectedSheetIndex >= _lastResult.Sheets.Count)
            {
                RenderWidth = 0;
                RenderHeight = 0;
                PreviewCaption = string.Empty;
                return;
            }

            var sheet = _lastResult.Sheets[SelectedSheetIndex];
            double scale = PreviewRenderWidth / sheet.Stock.WidthMm;
            RenderWidth = PreviewRenderWidth;
            RenderHeight = sheet.Stock.HeightMm * scale;

            foreach (var p in sheet.Pieces)
            {
                SheetRects.Add(new PieceRectVm
                {
                    X = p.X * scale,
                    Y = p.Y * scale,
                    Width = System.Math.Max(1, p.WidthMm * scale),
                    Height = System.Math.Max(1, p.HeightMm * scale),
                    Rotated = p.Rotated,
                    Label = string.Format(CultureInfo.InvariantCulture, "{0} · {1}×{2}{3}",
                        string.IsNullOrWhiteSpace(p.Piece.PieceLabel) ? p.Piece.GlassRef : p.Piece.PieceLabel,
                        p.Piece.WidthMm, p.Piece.HeightMm, p.Rotated ? " ⟳" : string.Empty)
                });
            }

            PreviewCaption = string.Format(CultureInfo.InvariantCulture,
                "Sheet {0} of {1} — stock {2} × {3} mm — {4} pieces",
                SelectedSheetIndex + 1, _lastResult.Sheets.Count,
                sheet.Stock.WidthMm, sheet.Stock.HeightMm, sheet.Pieces.Count);
        }
    }
}
