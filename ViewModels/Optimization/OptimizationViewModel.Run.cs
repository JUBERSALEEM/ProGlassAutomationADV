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

        // ── Million-scale UI guards ──────────────────────────────────
        // SheetRows / UnplacedRows bind to NON-virtualized ItemsControls:
        // at 10k+ rows they hang the UI thread and burn memory per row
        // object. The DATA always stays complete (_flatSheets drives the
        // 2D preview + navigation, the report export lists every sheet)
        // — only the on-screen rows cap, with an honest overflow label.
        private const int MaxSheetRows = 2000;
        private const int MaxUnplacedRows = 500;

        private string _sheetRowsOverflowLabel = string.Empty;
        /// <summary>"Showing first 2,000 of 17,175 sheets…" (empty when all fit).</summary>
        public string SheetRowsOverflowLabel
        {
            get => _sheetRowsOverflowLabel;
            private set { _sheetRowsOverflowLabel = value; OnPropertyChanged(); }
        }

        private bool _hasSheetRowsOverflow;
        public bool HasSheetRowsOverflow
        {
            get => _hasSheetRowsOverflow;
            private set { _hasSheetRowsOverflow = value; OnPropertyChanged(); }
        }

        private string _unplacedOverflowLabel = string.Empty;
        /// <summary>"…and N more unplaced pieces" (empty when all fit).</summary>
        public string UnplacedOverflowLabel
        {
            get => _unplacedOverflowLabel;
            private set { _unplacedOverflowLabel = value; OnPropertyChanged(); }
        }

        private bool _hasUnplacedOverflow;
        public bool HasUnplacedOverflow
        {
            get => _hasUnplacedOverflow;
            private set { _hasUnplacedOverflow = value; OnPropertyChanged(); }
        }

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
            public string StockSize { get; set; } = string.Empty;
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

            /// <summary>
            /// PHASE 4 / finding 4c: true when an oracle reported a problem
            /// against this group's plan. Such a group stays on screen for
            /// inspection but is excluded from the cut-list export and from the
            /// Step 6 feed — a plan that fails geometry or conservation must
            /// never become production work or drive a quote's charges.
            /// </summary>
            public bool ValidationFailed;

            public string SheetsLabel = string.Empty;
            public string MarginNote = string.Empty;
        }

        /// <summary>Flat sheet index → (group run, sheet index in that run).</summary>
        private sealed class FlatSheet
        {
            public GroupRun Run = null!;
            public int SheetIndex;
        }

        /// <summary>
        /// Names what the oracles ACTUALLY checked, so a green label can never
        /// claim a guarantee that was not made.
        ///
        /// PHASE 4 / finding 4e: "Verified" used to mean "GuillotinePacker.Verify
        /// found nothing", and Verify checks geometry only. On the QUICK path
        /// conservation had never been checked at all, so a plan that had lost
        /// pieces could still display "Verified" — the exact failure class the
        /// owner has been chasing, wearing a green label.
        /// </summary>
        private static string BuildOracleLabel(OptimizationResult result, int problems)
        {
            if (problems > 0)
                return problems.ToString(CultureInfo.InvariantCulture) +
                       " issue(s) — not exportable";

            if (result.ConservationChecked && result.GeometryChecked)
                return "Verified";

            return result.GeometryChecked
                ? "Geometry checked — conservation NOT verified"
                : "Not verified";
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
            ClearInputWarnings();
            if (!TryBuildInputs(out var pieces, out var stockPool, out var margins))
            { _openToReportAfterRun = false; return; }

            int effort = int.TryParse(SelectedEffortLevel, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int lvl) && lvl >= 1 && lvl <= 5 ? lvl : 3;
            var rotation = RotationEnabled ? RotationPolicy.Free90 : RotationPolicy.Locked;
            var groups = BuildCuttingGroups(pieces);

            // Phase 2: stamp the run with the single selected job order, so
            // the Step 6 / Step 9 feed can refuse to apply these figures to
            // an unrelated quote (the structural join — spec number + side +
            // sheet size — matches across documents that share a rack size).
            // Multi-job runs have no single identity and stay shared, as
            // before; a Step 5 host sets its own DocumentKey before calling
            // here and is never overwritten (no jobs are selected there).
            var selectedJoIds = AvailableJobs
                .Where(j => j.IsSelected)
                .Select(j => j.JobOrderId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (selectedJoIds.Count == 1)
                DocumentKey = selectedJoIds[0];

            // SIZES dropdown: optimize ONE pane group at a time when a
            // specific composition (outer / inner / …) is selected.
            if (!string.Equals(_selectedCuttingGroup, AllCuttingGroups, StringComparison.OrdinalIgnoreCase))
            {
                // RC-012 (2026-09-20): compare on the SAME canonical key the
                // groups were built with. The dropdown carries the piece row's
                // raw GlassRef while BuildCuttingGroups groups by
                // CompositionKey.Normalize(GlassRef) and labels the group with
                // the FIRST row's raw ref — so a hand-typed list holding both
                // "6mm clear ft" and "6mm Clear FT Glass" produced ONE group
                // labelled with whichever row came first, and picking the
                // other spelling in the dropdown refused the run with "The
                // selected cutting group has no valid pieces." Normalising both
                // sides removes the whole mismatch class.
                string wantedGroup = CompositionKey.Normalize(_selectedCuttingGroup);
                groups = groups
                    .Where(g => string.Equals(
                        CompositionKey.Normalize(g.Composition), wantedGroup,
                        StringComparison.OrdinalIgnoreCase))
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
            // The engine tiers per GROUP (a group over 2,000 parts goes to
            // the partitioned path even inside a mostly-small run), so the
            // architecture line is keyed on the largest group, not the total.
            RunArchitectureLabel = BuildRunArchitectureLabel(
                stage, groups.Max(g => g.Pieces.Count), effort);
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
        /// each. Grouping uses the CANONICAL composition key (Phase 2), so
        /// "6mm clear ft glass" and "6mm clear ft" are one group again; the
        /// displayed label keeps the first original spelling. This is the
        /// defensive net for rows that bypass the import-time normalisation
        /// (hand-typed pieces on the PARTS list).</summary>
        private static List<(string Composition, List<CutPiece> Pieces)> BuildCuttingGroups(List<CutPiece> pieces)
        {
            return pieces
                .GroupBy(p =>
                {
                    string key = CompositionKey.Normalize(p.GlassRef);
                    return key.Length == 0 ? UnlabelledGlass : key;
                })
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    string label = g.Key == UnlabelledGlass
                        ? UnlabelledGlass
                        : g.First().GlassRef.Trim();
                    return (label, g.ToList());
                })
                .ToList();
        }

        /// <summary>
        /// Thickness-aware margins: when a cutting group's glass thickness
        /// differs from the MARGINS tab, the group is cut with that
        /// thickness's house defaults and the note surfaces on the result.
        /// When the user explicitly configures margins, those user settings
        /// take authoritative precedence.
        /// </summary>
        private static (MarginProfile Margins, string Note) ResolveGroupMargins(
            string composition, MarginProfile userMargins)
        {
            // PHASE 1 / finding 3 (2026-09-20): this used to read
            //     if (userMargins != null && (userMargins.LeftMm != 0 || userMargins.TopMm != 0))
            // i.e. "did the operator customize?" was INFERRED from two of the
            // four margin fields being non-zero. An operator who deliberately
            // set Left = 0 and Top = 0 — a normal trim pattern, and exactly
            // what the house thick-glass profile does for its bottom edge —
            // was read as having customized nothing, and house defaults for
            // the parsed thickness were substituted with Note = string.Empty.
            // Setting Right = 15 / Bottom = 15 while Left and Top stayed 0 was
            // invisible too. The result was a wrong usable area, a wrong sheet
            // count, and nothing on screen to explain either.
            //
            // Customization is now an explicit fact set where the operator's
            // input is read (TryBuildInputs), so a deliberate zero is honoured.
            if (userMargins != null && userMargins.IsCustomized)
                return (userMargins, string.Empty);

            int thickness = ParseThicknessMm(composition);
            if (thickness <= 0 || (userMargins != null && thickness == userMargins.ThicknessMm))
                return (userMargins ?? MarginProfile.DefaultFor(6), string.Empty);

            // A substituted profile is named WITH ITS VALUES. The old note said
            // only "cut with 10mm house margins", which told the operator a
            // substitution happened but not what it did — and the thick bucket
            // is asymmetric (BM = 0), so the numbers are the part that matters.
            var house = MarginProfile.DefaultFor(thickness);
            return (house, string.Format(CultureInfo.InvariantCulture,
                "cut with {0}mm house margins (LM {1} · RM {2} · TM {3} · BM {4} · breakout {5})",
                thickness, house.LeftMm, house.RightMm, house.TopMm, house.BottomMm,
                house.MinBreakoutMm));
        }

        /// <summary>
        /// Picks the stock sizes for ONE cutting group. The group's OWN
        /// Step 4 rack (tagged with its glass composition) always wins;
        /// manual "any group" racks (empty composition) are the fallback,
        /// and only when no Step 4 sheet is recorded for the glass do we
        /// drop back to the whole pool so pieces are never silently lost.
        /// When multiple racks fit, prioritize the largest primary stock
        /// envelope so huge architectural panels are not fragmented across
        /// smaller remnant plates.
        /// </summary>
        private static List<StockSize> ResolveGroupStocks(
            string composition,
            List<(StockSize Stock, string Composition)> pool,
            bool allowCrossComposition,
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
            if (dedicated.Count > 0) return SortStocksLargestFirstForTieBreak(dedicated);

            // 2. Manual "any group" racks (no composition tag).
            var manual = pool
                .Where(t => string.IsNullOrWhiteSpace(t.Composition))
                .Select(t => t.Stock)
                .ToList();
            manual = DistinctBySize(manual);
            if (manual.Count > 0)
            {
                note = "no Step 4 sheet — packed on manual racks";
                return SortStocksLargestFirstForTieBreak(manual);
            }

            // 3. Nothing dedicated and nothing manual.
            //
            // PHASE 5 / finding 5a (2026-09-20): this used to hand the group
            // EVERY rack in the pool — "keep the run alive … rather than
            // dropping the pieces" — so 6mm clear was packed onto a 12mm
            // laminated rack. OpenNewSheet then picks the SMALLEST rack that
            // fits, so it would happily choose a small offcut of the wrong
            // glass. The note did surface, but the sheet count, utilisation and
            // cost that reached Step 6 and Step 9 were for material that cannot
            // physically be cut, and keeping the run alive is not worth
            // inventing a plan.
            //
            // Now refused by default. The opt-in stays available in SETTINGS for
            // an operator who genuinely wants a ballpark across mixed racks, and
            // when it is used the note says so in terms nobody can mistake for a
            // real plan.
            var all = DistinctBySize(pool.Select(t => t.Stock).ToList());
            if (all.Count == 0) return all;

            if (!allowCrossComposition)
            {
                note = "NO RACK FOR THIS GLASS — nothing was cut. Every rack on STOCKS belongs to a " +
                       "different composition. Add this glass's sheet size, or a manual rack with no " +
                       "composition, or enable \"allow packing on other specs' racks\" in SETTINGS " +
                       "for a ballpark plan only.";
                return new List<StockSize>();
            }

            note = "no Step 4 sheet — packed on OTHER SPECS' RACKS (glass differs, so this is a " +
                   "ballpark plan, not a cut list)";
            return SortStocksLargestFirstForTieBreak(all);
        }

        /// <summary>
        /// Orders a group's racks largest-first.
        ///
        /// PHASE 5 / finding 5b (2026-09-20): the name claimed this decides
        /// which rack the group is cut on. It does not — OpenNewSheet re-sorts
        /// the whole pool ASCENDING by area and takes the smallest rack that
        /// fits, which is the owner's "save the large racks for the glass that
        /// needs them" rule and correctly overrides this. What survives is the
        /// tie-break: OpenNewSheet's ordering ends in ThenBy(i => i), so among
        /// racks of EQUAL area the one this put first wins. Renaming the intent
        /// rather than deleting it, because a tie-break that looks like a
        /// preference is how the next person "fixes" it in the wrong direction.
        /// </summary>
        private static List<StockSize> SortStocksLargestFirstForTieBreak(List<StockSize> stocks) =>
            stocks.OrderByDescending(s => (long)s.WidthMm * s.HeightMm).ToList();

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
                    var groupStocks = ResolveGroupStocks(composition, stockPool,
                        AllowCrossCompositionStock, out string stockNote);
                    string note = string.IsNullOrWhiteSpace(stockNote)
                        ? marginNote
                        : (string.IsNullOrWhiteSpace(marginNote)
                            ? stockNote
                            : marginNote + " · " + stockNote);

                    // PHASE 5 / finding 5a: no rack for this glass means no plan.
                    // Short-circuit rather than handing the packer an empty pool,
                    // so the group reports "not cut" with every piece accounted
                    // for as unplaced instead of producing 0 sheets and a
                    // confusing silence. Conservation still holds: input count
                    // equals placed (0) plus unplaced (all).
                    if (groupStocks.Count == 0)
                    {
                        var refused = new OptimizationResult { Rotation = rotation };
                        foreach (var refusedPiece in groupPieces)
                            refused.Unplaced.Add(refusedPiece);
                        if (!string.IsNullOrWhiteSpace(stockNote))
                            refused.ScaleNotes.Add(stockNote);
                        refused.RecordValidation(Array.Empty<string>(),
                            conservationChecked: true, geometryChecked: true);

                        runs.Add(new GroupRun
                        {
                            Composition = composition,
                            Thickness = ParseThicknessLabel(composition),
                            PieceCount = groupPieces.Count,
                            Margins = groupMargins,
                            Result = refused,
                            UtilizationPct = 0,
                            WasteM2 = 0,
                            UnplacedLabel = groupPieces.Count.ToString(CultureInfo.InvariantCulture),
                            OracleLabel = "NOT CUT — no rack for this glass",
                            ProblemCount = 0,
                            ValidationFailed = false,
                            SheetsLabel = "0",
                            MarginNote = note
                        });

                        RunProgressPercent = groupCount <= 0
                            ? 0
                            : (int)Math.Round(groupIndex * 100.0 / groupCount);
                        continue;
                    }

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
                        ? (groupPieces.Count > 2000
                            ? GuillotinePacker.PackOptimizePartitioned(groupPieces, groupStocks, groupMargins,
                                  rotation, effort, ct, groupProgress)
                            : GuillotinePacker.PackOptimize(groupPieces, groupStocks, groupMargins,
                                  rotation, effort, ct, groupProgress))
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
                    string winningStrat = !string.IsNullOrWhiteSpace(result.Strategy)
                        ? result.Strategy
                        : (stage == "OPTIMIZE" ? "MULTI-START" : "QUICK SHELF");
                    if (!smartWins.Contains($"{composition} [{winningStrat}]"))
                        smartWins.Add($"{composition} [{winningStrat}]");

                    double glass = result.Sheets.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm));
                    double stockArea = result.Sheets.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm);
                    // PHASE 4 / findings 4b, 4c, 4e (2026-09-20): this line used
                    // to be
                    //     int problems = GuillotinePacker.Verify(result, groupMargins).Count;
                    // and the group was then labelled "Verified" when it was 0.
                    // Verify checks GEOMETRY only — margins, rotation policy,
                    // dimension integrity, overlap, kerf. It never checks that
                    // every input piece survived. So the label claimed a
                    // conservation guarantee that had not been made, and on the
                    // QUICK path conservation had not been checked at all until
                    // Phase 4a. The plan's own verdict is now folded in and the
                    // label says what was actually checked.
                    var geometryProblems = GuillotinePacker.Verify(result, groupMargins);
                    result.RecordValidation(
                        geometryProblems, conservationChecked: false, geometryChecked: true);

                    int problems = result.ValidationProblems.Count;

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
                        OracleLabel = BuildOracleLabel(result, problems),
                        ProblemCount = problems,
                        ValidationFailed = result.HasValidationFailure,
                        SheetsLabel = result.SheetsUsed.ToString(CultureInfo.InvariantCulture),
                        MarginNote = note
                    });
                }

                    ApplyResults(stage, runs, rotation);

                    // PHASE 2 / finding 19: input warnings (excluded invalid
                    // rows, racks left out at a zero balance) are appended here
                    // rather than set in TryBuildInputs, because ApplyResults has
                    // just written the summary the operator reads first. Same
                    // append-never-replace rule as the scale notes below — and
                    // these matter most on exactly the large jobs where a
                    // replaced message would hide an exclusion.
                    ApplyInputWarningsToRunMessage();

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
                        "Strategy breakdown: {0}.", string.Join(", ", smartWins));
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
            // Phase 3: when the run is scoped to exactly ONE confirmed job
            // order, say which — Step 6 and the operator need to know the
            // result belongs to JO-2026-004 and not to whatever job was
            // ticked before. Multi-job and Step-5 runs stay unattributed.
            var singleJob = SelectedJobs
                .Select(j => j.JobOrderId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            string jobScope = singleJob.Count == 1
                ? " · " + singleJob[0]
                : string.Empty;
            ResultStage = stage == "OPTIMIZE"
                ? $"OPTIMIZE · level {SelectedEffortLevel} · {rotationLabel} · {runs.Count} cutting group(s){jobScope}"
                : $"QUICK · {rotationLabel} · {runs.Count} cutting group(s){jobScope}";
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
            HasUnplacedOverflow = unplacedSeen > unplacedListed;
            UnplacedOverflowLabel = HasUnplacedOverflow
                ? string.Format(CultureInfo.InvariantCulture,
                    "…and {0:N0} more unplaced pieces (the full list is in the REPORT export).",
                    unplacedSeen - unplacedListed)
                : string.Empty;

            // Per-cutting-group breakdown (composition → sheets / util / waste / stock size).
            GroupResults.Clear();
            foreach (var r in runs)
            {
                string stockSummary = string.Join(", ", r.Result.Sheets
                    .Select(s => string.Format(CultureInfo.InvariantCulture, "{0}×{1}", s.Stock.WidthMm, s.Stock.HeightMm))
                    .Distinct());

                GroupResults.Add(new CuttingGroupResultVm
                {
                    Composition = r.Composition,
                    Thickness = r.Thickness,
                    StockSize = string.IsNullOrWhiteSpace(stockSummary) ? "—" : stockSummary,
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
                    if (SheetRows.Count < MaxSheetRows)
                    {
                        // On-screen rows cap (the ItemsControls don't
                        // virtualize); _flatSheets stays complete so the 2D
                        // preview, navigation and export see every sheet.
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
                    }
                    flatIndex++;
                }
            }

            HasSheetRowsOverflow = flatIndex > SheetRows.Count;
            SheetRowsOverflowLabel = HasSheetRowsOverflow
                ? string.Format(CultureInfo.InvariantCulture,
                    "Showing first {0:N0} of {1:N0} sheets — the full list is in the REPORT export.",
                    SheetRows.Count, flatIndex)
                : string.Empty;

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
