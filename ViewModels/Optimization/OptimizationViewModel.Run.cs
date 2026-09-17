// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Run.cs
// MODULE: Optimization — SUMMARY — result surface and the pack-engine execution (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
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
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── RESULT tab ─────────────────────────────────────────────────

        public sealed class SheetRowVm : ViewModels.ViewModelBase
        {
            private bool _isSelected;
            public string SheetNo { get; set; } = string.Empty;
            public string StockLabel { get; set; } = string.Empty;
            public string PieceCount { get; set; } = string.Empty;
            public string Utilization { get; set; } = string.Empty;
            public string WasteLabel { get; set; } = string.Empty;
            /// <summary>Utilization band for colour coding: Good ≥85% /
            /// Mid 70–84% / Low &lt;70% (matches the industrial report).</summary>
            public string UtilizationTone { get; set; } = "Good";
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
        public bool HasResult
        {
            get => _hasResult;
            private set
            {
                _hasResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanUpgradeToOptimize));
            }
        }

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

            /// <summary>Oracle violations found for this group. Verified once
            /// when the run finishes and reused by ApplyResults — it used to
            /// re-verify every sheet of every group a second time, and Verify
            /// is O(pieces^2) per sheet.</summary>
            public int ProblemCount;
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

        /// <summary>Cancels an in-flight run. The engine checks the token once
        /// per part, so cancelling takes effect within about a second even on a
        /// group large enough to have taken minutes.</summary>
        public ICommand CancelRunCommand { get; }

        private string _runMessage = string.Empty;
        public string RunMessage
        {
            get => _runMessage;
            private set
            {
                if (_runMessage == value) return;
                _runMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasRunMessage));
            }
        }

        /// <summary>True when a status/validation message should be shown in the header.</summary>
        public bool HasRunMessage => !string.IsNullOrWhiteSpace(_runMessage);

        // ── async run state (progress bar + button gating) ─────────────
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                _isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(CanUpgradeToOptimize));
                OnPropertyChanged(nameof(IsRunProgressVisible));
            }
        }

        /// <summary>Run buttons are enabled only when no run is in flight.</summary>
        public bool CanRun => !_isRunning;

        /// <summary>Cancel source for the in-flight run; null while idle.</summary>
        private CancellationTokenSource _runCts;

        /// <summary>True while a run is in flight and can still be cancelled.</summary>
        public bool CanCancel => _isRunning && _runCts != null;

        /// <summary>Requests cancellation. Safe to call when nothing is running.</summary>
        private void CancelRun()
        {
            var cts = _runCts;
            if (cts == null) return;
            try
            {
                cts.Cancel();
                RunStageLabel = "Cancelling…";
            }
            catch (ObjectDisposedException)
            {
                // The run completed between the null check and Cancel().
            }
        }

        private string _runStageLabel = string.Empty;
        public string RunStageLabel { get => _runStageLabel; private set { _runStageLabel = value; OnPropertyChanged(); } }

        /// <summary>The last successfully applied engine stage ("QUICK" /
        /// "OPTIMIZE") — session save uses it to name and later re-run the
        /// session with the same stage.</summary>
        private string _lastRunStage = "QUICK";
        public string LastRunStage => _lastRunStage;

        // ── run ────────────────────────────────────────────────────────

        /// <summary>
        /// Validates inputs on the UI thread, then runs the packing engine
        /// per cutting group (glass composition) on a background thread —
        /// different glass never mixes on a sheet. Results are applied back
        /// on the dispatcher by <see cref="SafeFireAndForget"/>.
        /// </summary>
        private void Run(string stage)
        {
            // A run that never starts must not leave the "reopen to REPORT"
            // flag dangling — clear it on every early exit.
            if (IsRunning) { _openToReportAfterRun = false; return; }
            if (!TryBuildInputs(out var pieces, out var stockPool, out var margins))
            { _openToReportAfterRun = false; return; }

            int effort = int.TryParse(SelectedEffortLevel, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int lvl) && lvl >= 1 && lvl <= 5 ? lvl : 3;
            var rotation = RotationEnabled ? RotationPolicy.Free90 : RotationPolicy.Locked;
            var groups = BuildCuttingGroups(pieces);

            // SIZES dropdown: optimize ONE pane group at a time when a
            // specific composition (outer / inner / …) is selected.
            if (!string.Equals(_selectedCuttingGroup, AllCuttingGroups, StringComparison.OrdinalIgnoreCase))
            {
                groups = groups
                    .Where(g => string.Equals(g.Composition, _selectedCuttingGroup, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (groups.Count == 0)
                {
                    _openToReportAfterRun = false;
                    RunMessage = "The selected cutting group has no valid pieces.";
                    return;
                }
            }

            IsRunning = true;
            RunStageLabel = stage == "OPTIMIZE"
                ? $"Optimizing {groups.Count.ToString(CultureInfo.InvariantCulture)} cutting group(s) · level {effort.ToString(CultureInfo.InvariantCulture)}…"
                : $"Quick packing {groups.Count.ToString(CultureInfo.InvariantCulture)} cutting group(s)…";
            RunMessage = "";

            int totalParts = groups.Sum(g => g.Pieces.Count);
            RunScaleLabel = string.Format(CultureInfo.InvariantCulture,
                "{0} cutting group(s) · {1} part(s) · SETTINGS level {2}",
                groups.Count, totalParts, SelectedEffortLevel);
            RunProgressPercent = 0;
            RunProgressDetail = stage == "OPTIMIZE"
                ? "Preparing multi-start search…"
                : "Preparing quick pass…";

            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            OnPropertyChanged(nameof(CanCancel));
            StartRunClock();

            AsyncUtility.SafeFireAndForget(
                () => RunCoreAsync(stage, groups, stockPool, margins, rotation, effort),
                "Optimization run");
        }

        /// <summary>Groups the piece list by glass composition — one pack run
        /// each. Keys are trimmed so the run composition exactly matches the
        /// SIZES merge, the STOCKS rack tags and the industrial report.</summary>
        private static List<(string Composition, List<CutPiece> Pieces)> BuildCuttingGroups(List<CutPiece> pieces)
        {
            return pieces
                .GroupBy(p => string.IsNullOrWhiteSpace(p.GlassRef)
                        ? "(unlabelled glass)" : p.GlassRef.Trim(),
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

        /// <summary>
        /// Picks the stock sizes for ONE cutting group. The group's OWN
        /// Step 4 rack (tagged with its glass composition) always wins;
        /// manual "any group" racks (empty composition) are the fallback,
        /// and only when no Step 4 sheet is recorded for the glass do we
        /// drop back to the whole pool so pieces are never silently lost.
        /// A manual rack must never be offered to a group that already has
        /// a dedicated rack — otherwise "smallest stock first" can route a
        /// spec's pieces onto the wrong rack.
        /// </summary>
        private static List<StockSize> ResolveGroupStocks(
            string composition,
            List<(StockSize Stock, string Composition)> pool,
            out string note)
        {
            note = string.Empty;

            // 1. Dedicated Step 4 racks for this glass.
            var dedicated = pool
                .Where(t => !string.IsNullOrWhiteSpace(t.Composition)
                            && string.Equals(t.Composition, composition, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.Stock)
                .ToList();
            dedicated = DistinctBySize(dedicated);
            if (dedicated.Count > 0) return dedicated;

            // 2. Manual "any group" racks (no composition tag).
            var manual = pool
                .Where(t => string.IsNullOrWhiteSpace(t.Composition))
                .Select(t => t.Stock)
                .ToList();
            manual = DistinctBySize(manual);
            if (manual.Count > 0)
            {
                note = "no Step 4 sheet — packed on manual racks";
                return manual;
            }

            // 3. Nothing dedicated and nothing manual: keep the run alive
            //    with every available rack rather than dropping the pieces.
            var all = DistinctBySize(pool.Select(t => t.Stock).ToList());
            if (all.Count > 0) note = "no Step 4 sheet — packed on other specs' racks";
            return all;
        }

        private static List<StockSize> DistinctBySize(List<StockSize> stocks) =>
            stocks.GroupBy(s => (s.WidthMm, s.HeightMm))
                  .Select(g => g.First())
                  .ToList();

        private async Task RunCoreAsync(
            string stage, List<(string Composition, List<CutPiece> Pieces)> groups,
            List<(StockSize Stock, string Composition)> stockPool,
            MarginProfile margins, RotationPolicy rotation, int effort)
        {
            // ── SCALE / ROBUSTNESS ────────────────────────────────────────
            // This method had try/finally with NO catch, and it is invoked
            // through SafeFireAndForget. An OutOfMemoryException from a very
            // large cutting group was therefore swallowed: the spinner cleared,
            // no results appeared, and the operator was told nothing about why.
            // Every failure mode now reports itself in the run header.
            var ct = _runCts?.Token ?? CancellationToken.None;
            int groupCount = groups.Count;
            int groupIndex = 0;
            var scaleNotes = new List<string>();
            var smartWins = new List<string>();
            _lastRunEffort = effort;

            try
            {
                var runs = new List<GroupRun>();
                foreach (var (composition, groupPieces) in groups)
                {
                    groupIndex++;
                    ct.ThrowIfCancellationRequested();

                    var (groupMargins, marginNote) = ResolveGroupMargins(composition, margins);

                    // Each cutting group packs against ITS OWN Step 4 rack
                    // (the sheet its pane was quoted on), falling back to
                    // manual "any group" racks only when no Step 4 sheet is
                    // recorded for that glass.
                    var groupStocks = ResolveGroupStocks(composition, stockPool, out string stockNote);
                    string note = string.IsNullOrWhiteSpace(stockNote)
                        ? marginNote
                        : (string.IsNullOrWhiteSpace(marginNote)
                            ? stockNote
                            : marginNote + " · " + stockNote);

                    // Progress<T> captures the UI synchronization context when
                    // it is constructed, so the engine can report from the
                    // worker thread and the label updates on the dispatcher.
                    int gi = groupIndex;
                    var groupProgress = new Progress<OptimizeProgress>(p =>
                    {
                        RunStageLabel = stage == "OPTIMIZE"
                            ? $"Optimizing {composition} · group {gi} of {groupCount} · {p.Message}"
                            : $"Quick packing {composition} · group {gi} of {groupCount}";
                        RunProgressDetail = p.Message;

                        // Overall completion = finished groups plus this group's
                        // own pass fraction, so a 12-group job moves smoothly
                        // instead of jumping once per group.
                        double done = groupCount <= 1
                            ? p.Fraction
                            : ((gi - 1) + p.Fraction) / groupCount;
                        RunProgressPercent = (int)Math.Round(done * 100.0);
                    });

                    // QUICK reports per-part progress as of 2026-09-17, so this
                    // is a seed rather than the whole story: it puts the bar at
                    // the right place for this group before the first part lands.
                    if (stage != "OPTIMIZE" && groupCount > 0)
                        RunProgressPercent = (int)Math.Round(((gi - 1) * 100.0) / groupCount);

                    var result = await Task.Run(() => stage == "OPTIMIZE"
                        ? GuillotinePacker.PackOptimize(groupPieces, groupStocks, groupMargins,
                              rotation, effort, ct, groupProgress)
                        : GuillotinePacker.PackQuick(groupPieces, groupStocks, groupMargins,
                              rotation, SplitMode.HorizontalFirst, ct, groupProgress))
                        .ConfigureAwait(true);

                    // Size-driven search reductions are surfaced, never silent.
                    foreach (var scaleNote in result.ScaleNotes)
                        if (!scaleNotes.Contains(scaleNote)) scaleNotes.Add(scaleNote);

                    // Which strategy actually won this group. A multi-start run
                    // evaluates up to 147 complete plans and keeps one, so without
                    // this a better sheet count is unexplainable — and an
                    // unexplainable improvement is one nobody trusts.
                    if (string.Equals(result.Strategy, GuillotinePacker.SmartStrategyLabel,
                            StringComparison.Ordinal) && !smartWins.Contains(composition))
                        smartWins.Add(composition);

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
                        ProblemCount = problems,
                        SheetsLabel = result.SheetsUsed.ToString(CultureInfo.InvariantCulture),
                        MarginNote = note
                    });
                }

                ApplyResults(stage, runs, rotation);

                // APPEND, never replace: ApplyResults has just written the
                // sheet-count / utilization summary the operator reads first.
                // Overwriting it would hide the result exactly on the large
                // jobs where the note matters most.
                if (scaleNotes.Count > 0)
                {
                    string notes = string.Join(" ", scaleNotes);
                    RunMessage = string.IsNullOrWhiteSpace(RunMessage)
                        ? notes
                        : RunMessage + " " + notes;
                }

                // Same append-never-replace rule: the summary ApplyResults wrote
                // is what the operator reads first.
                if (smartWins.Count > 0)
                {
                    string smartNote = string.Format(CultureInfo.InvariantCulture,
                        "Smart placement won {0} of {1} cutting group(s): {2}.",
                        smartWins.Count, runs.Count, string.Join(", ", smartWins));
                    RunMessage = string.IsNullOrWhiteSpace(RunMessage)
                        ? smartNote
                        : RunMessage + " " + smartNote;
                }
            }
            catch (OperationCanceledException)
            {
                RunStageLabel = "Cancelled.";
                RunMessage = "Optimization cancelled. No results were applied.";
            }
            catch (OutOfMemoryException)
            {
                RunStageLabel = "Stopped — out of memory.";
                RunMessage = "Optimization stopped: this machine ran out of memory packing a group " +
                    "this large. Split the job into smaller cutting groups or close other " +
                    "applications, then run it again.";
            }
            catch (Exception ex)
            {
                RunStageLabel = "Failed.";
                RunMessage = $"Optimization failed: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
                StopRunClock();
                _runCts?.Dispose();
                _runCts = null;
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanUpgradeToOptimize));
            }
        }

        private void ApplyResults(string stage, List<GroupRun> runs, RotationPolicy rotation)
        {
            _lastRunStage = stage;

            // Result summary first, then the per-group breakdown ordered
            // MAX → lowest utilization (the owner's preferred reading order).
            runs = runs.OrderByDescending(r => r.UtilizationPct).ToList();

            _groupRuns.Clear();
            _groupRuns.AddRange(runs);

            double glass = runs.Sum(r => r.Result.Sheets.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm)));
            double stockArea = runs.Sum(r => r.Result.Sheets.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm));
            double util = stockArea == 0 ? 0 : glass / stockArea * 100.0;
            int totalSheets = runs.Sum(r => r.Result.SheetsUsed);
            int totalUnplaced = runs.Sum(r => r.Result.Unplaced.Count);
            // Reuse the count computed when each group finished. Re-running
            // Verify here doubled the oracle cost of every run.
            int totalProblems = runs.Sum(r => r.ProblemCount);

            string rotationLabel = rotation == RotationPolicy.Free90 ? "rotation ON" : "rotation LOCKED";
            ResultStage = stage == "OPTIMIZE"
                ? $"OPTIMIZE · level {SelectedEffortLevel} · {rotationLabel} · {runs.Count} cutting group(s)"
                : $"QUICK · {rotationLabel} · {runs.Count} cutting group(s)";
            // Stamps the SETTINGS level onto this result and, when this run was
            // an upgrade of a previous QUICK result, reports what it bought.
            UpdateUpgradeSummary(stage, totalSheets);

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
            // SCALE FIX: a job that cannot be placed pushes one string per part
            // into a bound ObservableCollection, on the dispatcher, one
            // CollectionChanged each. At 30,000 parts that is a frozen window
            // rather than a list — and the list is not even the number the
            // operator needs, because ResultUnplaced already carries the true
            // total. So this shows a sample and says that it is a sample.
            const int unplacedRowCap = 500;
            int unplacedListed = 0;
            int unplacedSeen = 0;
            foreach (var r in runs)
            {
                foreach (var up in r.Result.Unplaced)
                {
                    unplacedSeen++;
                    if (unplacedListed >= unplacedRowCap) continue;
                    string label = string.IsNullOrWhiteSpace(up.PieceLabel) ? up.GlassRef : up.PieceLabel;
                    UnplacedRows.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1} · {2} × {3} mm",
                        r.Composition, label, up.WidthMm, up.HeightMm));
                    unplacedListed++;
                }
            }
            if (unplacedSeen > unplacedListed)
            {
                UnplacedRows.Add(string.Format(CultureInfo.InvariantCulture,
                    "…and {0} more. The UNPLACED total above is the full count.",
                    unplacedSeen - unplacedListed));
            }
            HasUnplaced = unplacedSeen > 0;

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
                r.Result.Sheets.Sort((a, b) =>
                {
                    long aa = a.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
                    long ba = b.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
                    return ba.CompareTo(aa);
                });
                for (int i = 0; i < r.Result.Sheets.Count; i++)
                    r.Result.Sheets[i].SheetNo = i + 1;

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
                        UtilizationTone = sheetUtil >= 85.0 ? "Good" : sheetUtil >= 70.0 ? "Mid" : "Low",
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

            RefreshStockUsage(runs);

            // Priced plan from the same runs, keyed the same way as the usage
            // tally above so the two never disagree about which rack is which.
            RebuildCosting(runs);

            // Hand the finished plan to Step 6 / Step 9 without waiting for the
            // operator to press SAVE on a session.
            PublishLiveFeed();

            // ...and keep a durable copy, so a quote reopened in a LATER app
            // session can still be checked against this run. One reserved row,
            // replaced each run — auto-saving named sessions would bury the ones
            // the operator chose to keep.
            AsyncUtility.SafeFireAndForget(
                () => PersistLatestRunAsync(), "Optimization latest-run snapshot");

            RebuildIndustrialReport();

            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} cutting group(s), {2} sheet(s), {3:0.00}% utilization, {4} unplaced.",
                stage, runs.Count, totalSheets, util, totalUnplaced);

            RememberRunForUpgrade(stage, totalSheets);

            // The CUT LIST tab reads this run back per row. Stamping here means
            // its QTY USED and OPT % columns are current the moment the run
            // lands, with no second click — and it is a no-op until that tab has
            // rows of its own, so a proforma-driven run costs nothing here.
            StampCutListResults();

            // A session reopen re-runs the saved stage and lands on the
            // REPORT tab; a manual run lands on RESULT.
            SelectedTabIndex = _openToReportAfterRun ? TabIndexReport : TabIndexSummary;
            _openToReportAfterRun = false;
        }

        /// <summary>
        /// Live-connects the run to the STOCKS tab: each rack shows how many
        /// sheets this run consumed. Availability stays unlimited by default
        /// (99999999) — usage is tracked, not capped.
        /// </summary>
    }
}
