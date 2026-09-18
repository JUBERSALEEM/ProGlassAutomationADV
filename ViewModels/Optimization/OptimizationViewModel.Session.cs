// ═══════════════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Session.cs
// MODULE: Optimization — session save / reopen (partial of OptimizationViewModel)
//
// Phase 3 persistence: the current cutting list, Step 4 stock pool and
// margin settings are snapshotted to optimization.db as JSON payloads;
// reopening a session restores those inputs and re-runs the SAME engine
// stage so the saved layout lands back on the REPORT tab (the engine is
// deterministic — same inputs, same sheets — pinned by the benchmark
// tests, so a re-run reproduces the saved layout exactly).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Optimization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Optimization;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── SAVED SESSIONS (REPORT tab) ────────────────────────────────

        public sealed class SessionRowVm
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Detail { get; set; } = string.Empty;
            public string CreatedLabel { get; set; } = string.Empty;
        }

        public ObservableCollection<SessionRowVm> SavedSessions { get; } = new();

        public ICommand SaveSessionCommand { get; }
        public ICommand OpenSessionCommand { get; }
        public ICommand DeleteSessionCommand { get; }
        public ICommand RefreshSessionsCommand { get; }

        private readonly OptimizationSessionRepository _sessions = new();

        private string _sessionStatus = "Saved sessions appear here — save the cutting list to reopen it later.";
        public string SessionStatus
        {
            get => _sessionStatus;
            private set { if (_sessionStatus == value) return; _sessionStatus = value; OnPropertyChanged(); }
        }

        private bool _hasSessions;
        public bool HasSessions { get => _hasSessions; private set { _hasSessions = value; OnPropertyChanged(); } }

        /// <summary>When an opened session re-runs, land on the REPORT tab
        /// (not RESULT) so the reopened layout is reviewed in its reports.</summary>
        private bool _openToReportAfterRun;

        // ── input DTOs (plain get/set — safe System.Text.Json round-trips) ──

        public sealed class PieceDto
        {
            public string GlassRef { get; set; } = string.Empty;
            public string PieceLabel { get; set; } = string.Empty;
            public string Position { get; set; } = string.Empty;
            public string W { get; set; } = string.Empty;
            public string H { get; set; } = string.Empty;
            public string Qty { get; set; } = "1";
        }

        public sealed class StockDto
        {
            public string W { get; set; } = string.Empty;
            public string H { get; set; } = string.Empty;
            public string Composition { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string Available { get; set; } = "99999999";
            public bool IsUsedInRun { get; set; } = true;
            public int SpecNumber { get; set; }
            public string Side { get; set; } = string.Empty;
        }

        /// <summary>
        /// One priced rack row of a finished run. Persisted in ResultJson so
        /// Step 6 and Step 9 can read the sheet quantity, utilization and cost
        /// the engine actually produced for a pane, instead of relying on a
        /// hand-typed optimization percentage.
        /// </summary>
        public sealed class ResultRowDto
        {
            public int W { get; set; }
            public int H { get; set; }
            public string Composition { get; set; } = string.Empty;
            public string Thickness { get; set; } = string.Empty;
            public int Sheets { get; set; }
            public int Pieces { get; set; }
            public decimal UtilizationPct { get; set; }
            public decimal GlassSqm { get; set; }
            public decimal StockSqm { get; set; }
            public decimal WasteSqm { get; set; }
            public decimal RatePerSqm { get; set; }
            public bool HasPrice { get; set; }
            public bool IsSharedAcrossSpecs { get; set; }
            public int SharedWithSpecs { get; set; }
            public List<PaneIdentity> Panes { get; set; } = new();
        }

        public sealed class MarginDto
        {
            public string Thickness { get; set; } = "6";
            public string Left { get; set; } = "15";
            public string Right { get; set; } = "15";
            public string Top { get; set; } = "15";
            public string Bottom { get; set; } = "15";
            public string Kerf { get; set; } = "3";
            public string MinBreakout { get; set; } = "15";
        }

        // ── load ───────────────────────────────────────────────────────

        private async Task LoadSavedSessionsAsync()
        {
            try
            {
                await _sessions.EnsureCreatedAsync().ConfigureAwait(false);
                var list = await _sessions.ListAsync().ConfigureAwait(false);

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() => PopulateSessions(list));
                }
                else
                {
                    PopulateSessions(list);
                }
            }
            catch (Exception ex)
            {
                SessionStatus = "Could not load saved sessions: " + ex.Message;
            }
        }

        /// <summary>Display name stored on the reserved latest-run row.</summary>
        private const string LatestRunSessionName = "Latest run (auto-saved)";

        private void PopulateSessions(List<SavedOptimizationSession> list)
        {
            SavedSessions.Clear();
            foreach (var s in list)
            {
                // The auto-saved latest run is plumbing for the Step 6 / Step 9
                // feed, not something the operator saved and wants to reopen.
                if (SavedOptimizationSession.IsReservedLatestRunId(s.Id)) continue;

                SavedSessions.Add(new SessionRowVm
                {
                    Id = s.Id,
                    Name = s.Name,
                    Detail = string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1} sheets · {2:0.0}% util · {3} pieces",
                        s.EngineStage, s.SheetsUsed, s.UtilizationPct, s.PieceCount),
                    CreatedLabel = s.CreatedUtc.ToLocalTime()
                        .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
                });
            }
            HasSessions = SavedSessions.Count > 0;
            if (HasSessions)
                SessionStatus = string.Format(CultureInfo.InvariantCulture,
                    "{0} saved session(s) — click Open to restore one.", SavedSessions.Count);
        }

        // ── save ───────────────────────────────────────────────────────

        /// <summary>
        /// The per-rack breakdown the REPORT tab already computes, in the shape
        /// that gets persisted. Shared by the named SAVE and by the automatic
        /// latest-run snapshot, so the two can never drift apart — a field added
        /// to CostingRowVm has exactly one projection to update.
        /// </summary>
        private List<ResultRowDto> BuildResultRows() => CostingRows
            .Select(c => new ResultRowDto
            {
                W = c.WidthMm,
                H = c.HeightMm,
                Composition = c.Composition,
                Thickness = c.Thickness,
                Sheets = c.SheetCount,
                Pieces = c.PieceCount,
                UtilizationPct = c.UtilizationPercent,
                GlassSqm = c.GlassM2,
                StockSqm = c.StockM2,
                WasteSqm = c.WasteM2Value,
                RatePerSqm = c.Rate,
                HasPrice = c.HasPrice,
                IsSharedAcrossSpecs = c.IsSharedAcrossSpecs,
                SharedWithSpecs = c.SharedWithSpecs,
                Panes = c.Panes.ToList()
            })
            .ToList();

        /// <summary>
        /// Persists the run that just finished into the one reserved latest-run
        /// row, so a quote reopened in a later app session can still be checked
        /// against real figures instead of falling back to whatever was typed.
        ///
        /// Deliberately not a named session: an operator iterating on a cut would
        /// otherwise accumulate dozens of entries in Saved Sessions.
        /// </summary>
        private async Task PersistLatestRunAsync()
        {
            try
            {
                if (!HasResult) return;

                var resultRows = BuildResultRows();

                // No pane identity means nothing can be joined to a Step 6 row,
                // so there is nothing worth writing.
                if (resultRows.Count == 0) return;

                // Captured synchronously, before the first await. The snapshot
                // reads four UI-bound ObservableCollections — Pieces, Stocks,
                // AvailableJobs and CostingRows — so building it after an await
                // would let an operator edit land mid-write and store results
                // from this run next to stocks that no longer match them.
                // SafeFireAndForget invokes this on the calling (UI) thread and
                // ConfigureAwait(true) keeps it there, so this is the UI thread.
                var snapshot = BuildSessionSnapshot(LatestRunSessionName, resultRows);

                await _sessions.EnsureCreatedAsync();

                await _sessions.UpsertLatestRunAsync(snapshot, DocumentKey);
            }
            catch (Exception ex)
            {
                // Plumbing for a reporting bridge. It must never surface as a
                // failed run, and never block the UI thread.
                AppLogService.Debug("[Optimization] latest-run snapshot not saved: " +
                    ExceptionMessages.Deep(ex));
            }
        }

        /// <summary>
        /// Builds the full session snapshot for the current state: pieces,
        /// stocks, margins, results and the summary figures.
        /// </summary>
        /// <remarks>
        /// One implementation, used by both the manual SAVE button and the
        /// automatic latest-run upsert. That matters for the feed: a consumer
        /// reading the reserved row must get whatever was current when the run
        /// finished, so a half-populated row would silently narrow what can be
        /// rebuilt later.
        /// </remarks>
        /// <param name="name">Session name. The manual save stamps a timestamp.</param>
        /// <param name="resultRows">Per-pane rows, projected by the caller so
        /// that the whole snapshot is captured at one instant. Call this on the
        /// UI thread before the first await — see the callers.</param>
        private SavedOptimizationSession BuildSessionSnapshot(
            string name, List<ResultRowDto> resultRows)
        {
            var pieces = Pieces
                .Where(p => p.TryParse(out _, out _, out _))
                .Select(p => new PieceDto
                {
                    GlassRef = p.GlassRef ?? string.Empty,
                    PieceLabel = p.PieceLabel ?? string.Empty,
                    Position = p.Position ?? string.Empty,
                    W = p.WidthText,
                    H = p.HeightText,
                    Qty = p.QtyText
                })
                .ToList();

            var stocks = Stocks
                .Select(s => new StockDto
                {
                    W = s.WidthText,
                    H = s.HeightText,
                    Composition = s.Composition ?? string.Empty,
                    Source = s.SourceLabel ?? string.Empty,
                    Available = s.AvailableText,
                    IsUsedInRun = s.IsUsedInRun,
                    SpecNumber = s.SpecNumber,
                    Side = s.Side ?? string.Empty
                })
                .ToList();

            var margins = new MarginDto
            {
                Thickness = ThicknessText,
                Left = LeftMarginText,
                Right = RightMarginText,
                Top = TopMarginText,
                Bottom = BottomMarginText,
                Kerf = KerfText,
                MinBreakout = MinBreakoutText
            };

            int totalSheets = _groupRuns.Sum(r => r.Result.Sheets.Count);
            int unplaced = _groupRuns.Sum(r => r.Result.Unplaced.Count);
            long glass = _groupRuns.Sum(r => r.Result.Sheets.Sum(sh => sh.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm)));
            long stockArea = _groupRuns.Sum(r => r.Result.Sheets.Sum(sh => (long)sh.Stock.WidthMm * sh.Stock.HeightMm));
            double util = stockArea == 0 ? 0 : (double)glass / stockArea * 100.0;

            int pieceTotal = 0;
            foreach (var p in Pieces)
                if (p.TryParse(out _, out _, out int q)) pieceTotal += q;

            int effort = int.TryParse(SelectedEffortLevel, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int lvl) && lvl >= 1 && lvl <= 5 ? lvl : 3;

            return new SavedOptimizationSession
            {
                Name = name,
                JobOrderRefs = string.Join(", ",
                    AvailableJobs.Where(j => j.IsSelected).Select(j => j.JobOrderId).Distinct()),
                EngineStage = LastRunStage,
                EffortLevel = effort,
                RotationEnabled = RotationEnabled,
                PieceCount = pieceTotal,
                SheetsUsed = totalSheets,
                UnplacedCount = unplaced,
                UtilizationPct = util,
                MarginsJson = JsonSerializer.Serialize(margins),
                StocksJson = JsonSerializer.Serialize(stocks),
                PiecesJson = JsonSerializer.Serialize(pieces),
                ResultJson = JsonSerializer.Serialize(resultRows)
            };
        }

        private async Task SaveSessionAsync()
        {
            try
            {
                if (!HasPieces)
                {
                    SessionStatus = "Nothing to save — add pieces on the JOBS tab first.";
                    return;
                }

                // Captured before the first await, on the UI thread this command
                // handler runs on, for the same reason as PersistLatestRunAsync:
                // one atomic view of the four UI-bound collections rather than a
                // mix of pre- and post-await state.
                var resultRows = BuildResultRows();

                var session = BuildSessionSnapshot(string.Format(CultureInfo.InvariantCulture,
                    "{0} · {1:yyyy-MM-dd HH:mm}", LastRunStage, DateTime.Now), resultRows);

                // No ConfigureAwait(false) here: every VM mutation below must
                // happen on the captured UI synchronization context.
                await _sessions.EnsureCreatedAsync();

                await _sessions.InsertAsync(session);
                SessionStatus = "Session saved — " + session.Name;

                await LoadSavedSessionsAsync();
            }
            catch (Exception ex)
            {
                SessionStatus = "Could not save session: " + ex.Message;
            }
        }

        // ── open ───────────────────────────────────────────────────────

        private void OpenSession(SessionRowVm row)
        {
            if (row == null) return;
            AsyncUtility.SafeFireAndForget(
                () => OpenSessionInternalAsync(row),
                $"[OptimizationViewModel] OpenSession {row.Id}");
        }

        private async Task OpenSessionInternalAsync(SessionRowVm row)
        {
            try
            {
                // No ConfigureAwait(false): restoring the VM must stay on
                // the UI thread (ObservableCollections + bindings).
                var s = await _sessions.GetAsync(row.Id);
                if (s == null)
                {
                    SessionStatus = "Session not found — it may have been deleted.";
                    return;
                }

                RestoreSessionCore(s, openToReport: true);
                SessionStatus = "Opened " + (s.Name ?? "session") + " — re-running its layout…";
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"[OptimizationViewModel] Could not open session {row.Id}: {ex.Message}",
                    new LogContext { Module = "Optimization", Operation = "OpenSession" });
                SessionStatus = "Could not open session: " + ex.Message;
            }
        }

        /// <summary>
        /// Puts a persisted session back on screen and re-runs its saved stage.
        /// The engine is deterministic, so the reopened layout is byte-for-byte
        /// the one that was saved. <paramref name="openToReport"/> decides where
        /// that run lands: a session an operator deliberately reopened goes to
        /// REPORT, a run they only asked to look at goes to SUMMARY.
        /// </summary>
        private void RestoreSessionCore(SavedOptimizationSession s, bool openToReport)
        {
            var pieces = JsonSerializer.Deserialize<List<PieceDto>>(s.PiecesJson) ?? new List<PieceDto>();
            var stocks = JsonSerializer.Deserialize<List<StockDto>>(s.StocksJson) ?? new List<StockDto>();
            var margins = JsonSerializer.Deserialize<MarginDto>(s.MarginsJson);

            // Restore margins / rotation / effort on the UI thread's
            // field values (the run reads them synchronously next).
            ThicknessText = margins?.Thickness ?? "6";
            LeftMarginText = margins?.Left ?? "15";
            RightMarginText = margins?.Right ?? "15";
            TopMarginText = margins?.Top ?? "15";
            BottomMarginText = margins?.Bottom ?? "15";
            KerfText = margins?.Kerf ?? "3";
            MinBreakoutText = margins?.MinBreakout ?? "15";
            RotationEnabled = s.RotationEnabled;
            if (s.EffortLevel >= 1 && s.EffortLevel <= 5)
                SelectedEffortLevel = s.EffortLevel.ToString(CultureInfo.InvariantCulture);
            SelectedCuttingGroup = AllCuttingGroups;

            // Restore the cutting list.
            Pieces.Clear();
            foreach (var p in pieces)
            {
                var restored = NewPieceRow(
                    string.IsNullOrWhiteSpace(p.GlassRef) ? "6mm clear ft glass" : p.GlassRef,
                    p.PieceLabel ?? string.Empty,
                    p.W, p.H, p.Qty);
                restored.Position = p.Position ?? string.Empty;
                Pieces.Add(restored);
            }

            // Restore the Step 4 stock pool.
            Stocks.Clear();
            foreach (var st in stocks)
            {
                Stocks.Add(new StockEntryVm(st.W, st.H, st.Composition ?? string.Empty, st.Source ?? string.Empty)
                {
                    AvailableText = string.IsNullOrWhiteSpace(st.Available) ? "99999999" : st.Available,
                    IsUsedInRun = st.IsUsedInRun
                });
            }

            RefreshDerived();

            // VIEW RESULT lands on SUMMARY, and everything SUMMARY and COSTING
            // show was persisted with the run — so restore it instead of cutting
            // glass again. Re-running here (the behaviour until 2026-09-17) meant
            // rebuilding every grid row on the UI thread and then repeating the
            // whole search at the stored effort: on a large quote that is a frozen
            // window, and at 30k parts a dead process. OpenSession still re-runs,
            // because it asks for the REPORT tab and that needs real geometry.
            if (!openToReport && TryRestoreStoredResult(s))
            {
                _openToReportAfterRun = false;
                return;
            }

            _openToReportAfterRun = openToReport;
            Run(string.Equals(s.EngineStage, "OPTIMIZE", StringComparison.OrdinalIgnoreCase)
                ? "OPTIMIZE" : "QUICK");
        }

        /// <summary>
        /// Lands on SUMMARY showing the most recent run for one document — the
        /// bridge behind Step 5's VIEW RESULT button.
        /// </summary>
        /// <remarks>
        /// A proforma runs the engine on its own private host view model, so the
        /// run reaches this module the way it reaches Step 6: through the
        /// reserved latest-run row that host persists when it finishes. That row
        /// is deliberately excluded from <see cref="SavedSessions"/> — an
        /// operator never saved it, and it should not appear as something they
        /// can reopen by hand — which is why this reads it directly instead of
        /// opening the newest listed session. Opening the newest session would
        /// show an older, unrelated run and read as the button not working.
        /// </remarks>
        public async Task ShowLatestRunAsync(string? documentKey)
        {
            // Set first: Run() leaves early without touching the tab when it
            // cannot start, and the operator still asked to see SUMMARY.
            SelectedTabIndex = TabIndexSummary;

            if (IsRunning)
            {
                // Restoring replaces Pieces and Stocks. Doing that underneath a
                // live run would swap the list mid-flight.
                SessionStatus = "A run is already in progress — wait for it to finish, then open the result.";
                return;
            }

            try
            {
                // No ConfigureAwait(false): this restores UI-bound collections.
                await _sessions.EnsureCreatedAsync();
                var s = await _sessions.GetLatestRunAsync(documentKey);
                if (s == null)
                {
                    SessionStatus = string.IsNullOrWhiteSpace(documentKey)
                        ? "No optimisation run has been saved yet — run QUICK or OPTIMIZE first."
                        : "No optimisation run has been saved for this document yet — run it from Step 5 first.";
                    return;
                }

                // Stamp BEFORE restoring: PublishLiveFeed reads DocumentKey, and
                // an unstamped publish is one every other quote can read back.
                DocumentKey = documentKey ?? string.Empty;
                RestoreSessionCore(s, openToReport: false);
            }
            catch (Exception ex)
            {
                SessionStatus = "Could not open the latest run: " + ex.Message;
            }
        }

        // ── delete ─────────────────────────────────────────────────────

        private void DeleteSession(SessionRowVm row)
        {
            if (row == null) return;
            AsyncUtility.SafeFireAndForget(
                () => DeleteSessionInternalAsync(row),
                $"[OptimizationViewModel] DeleteSession {row.Id}");
        }

        private async Task DeleteSessionInternalAsync(SessionRowVm row)
        {
            try
            {
                await _sessions.DeleteAsync(row.Id);
                SavedSessions.Remove(row);
                HasSessions = SavedSessions.Count > 0;
                SessionStatus = "Session deleted.";
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"[OptimizationViewModel] Could not delete session {row.Id}: {ex.Message}",
                    new LogContext { Module = "Optimization", Operation = "DeleteSession" });
                SessionStatus = "Could not delete session: " + ex.Message;
            }
        }
        /// <summary>
        /// Rebuilds the SUMMARY and COSTING surface from the result rows a
        /// finished run persisted, so opening a result is a read, not a re-cut.
        /// </summary>
        /// <remarks>
        /// What the stored row carries: every headline number, one priced row per
        /// sheet size × composition, and the Step 5 pane identity Step 6 and
        /// Step 9 join on. What it does not carry is sheet-by-sheet geometry, so
        /// the 2D view and the REPORT are cleared and the status line says the
        /// layout needs a re-cut — in words, rather than rendering an empty
        /// canvas the operator would read as a broken button.
        /// </remarks>
        /// <returns>True when a stored result was restored and no re-run is needed.</returns>
        private bool TryRestoreStoredResult(SavedOptimizationSession s)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.ResultJson)) return false;
            List<ResultRowDto> rows;
            try
            {
                rows = JsonSerializer.Deserialize<List<ResultRowDto>>(s.ResultJson)
                       ?? new List<ResultRowDto>();
            }
            catch (Exception ex)
            {
                SessionStatus = "The saved result could not be read: " + ex.Message;
                return false;
            }
            rows = rows.Where(r => r != null).ToList();
            if (rows.Count == 0) return false;

            string stage = string.Equals(s.EngineStage, "OPTIMIZE", StringComparison.OrdinalIgnoreCase)
                ? "OPTIMIZE"
                : "QUICK";
            _lastRunStage = stage;
            // Clear the previous run's geometry rather than leave sheets on screen
            // behind numbers that did not produce them.
            _groupRuns.Clear();
            _flatSheets.Clear();
            SheetRows.Clear();
            UnplacedRows.Clear();

            decimal totStock = 0m, totGlass = 0m, totWaste = 0m, totCost = 0m;
            int pricedRows = 0;
            CostingRows.Clear();
            foreach (var r in rows)
            {
                bool hasPrice = r.HasPrice && r.RatePerSqm > 0m;
                decimal glassCost = r.GlassSqm * r.RatePerSqm;
                decimal wasteCost = r.WasteSqm * r.RatePerSqm;
                decimal totalCost = r.StockSqm * r.RatePerSqm;
                totStock += r.StockSqm;
                totGlass += r.GlassSqm;
                totWaste += r.WasteSqm;
                if (hasPrice)
                {
                    pricedRows++;
                    totCost += totalCost;
                }
                var identities = r.Panes ?? new List<PaneIdentity>();
                var row = new CostingRowVm
                {
                    Composition = r.Composition,
                    Thickness = r.Thickness,
                    SheetSize = r.W.ToString(CultureInfo.InvariantCulture) + " \u00d7 "
                        + r.H.ToString(CultureInfo.InvariantCulture),
                    SheetQty = r.Sheets.ToString(CultureInfo.InvariantCulture),
                    PieceQty = r.Pieces.ToString(CultureInfo.InvariantCulture),
                    Utilization = r.UtilizationPct.ToString("0.00", CultureInfo.InvariantCulture) + "%",
                    GlassArea = r.GlassSqm.ToString("0.00", CultureInfo.InvariantCulture),
                    StockArea = r.StockSqm.ToString("0.00", CultureInfo.InvariantCulture),
                    WasteArea = r.WasteSqm.ToString("0.00", CultureInfo.InvariantCulture),
                    RateLabel = hasPrice
                        ? string.Format(CultureInfo.InvariantCulture, "AED {0:0.##} / m\u00b2", r.RatePerSqm)
                        : "\u2014",
                    GlassCostLabel = hasPrice
                        ? string.Format(CultureInfo.InvariantCulture, "AED {0:N2}", glassCost) : "\u2014",
                    WasteCostLabel = hasPrice
                        ? string.Format(CultureInfo.InvariantCulture, "AED {0:N2}", wasteCost) : "\u2014",
                    TotalCostLabel = hasPrice
                        ? string.Format(CultureInfo.InvariantCulture, "AED {0:N2}", totalCost) : "\u2014",
                    HasPrice = hasPrice,
                    SheetCount = r.Sheets,
                    Rate = r.RatePerSqm,
                    GlassM2 = r.GlassSqm,
                    StockM2 = r.StockSqm,
                    WasteM2Value = r.WasteSqm,
                    GlassCost = glassCost,
                    WasteCost = wasteCost,
                    TotalCost = totalCost,
                    WidthMm = r.W,
                    HeightMm = r.H,
                    PieceCount = r.Pieces,
                    UtilizationPercent = r.UtilizationPct,
                    SpecNumber = identities.Count == 1 ? identities[0].SpecNumber : 0,
                    Side = identities.Count == 1 ? identities[0].Side : string.Empty,
                    IsSharedAcrossSpecs = r.IsSharedAcrossSpecs || identities.Count > 1,
                    SharedWithSpecs = r.SharedWithSpecs > 0 ? r.SharedWithSpecs : identities.Count
                };
                foreach (var idn in identities) row.Panes.Add(idn);
                row.PaneLabel = DescribePanes(identities);
                CostingRows.Add(row);
            }
            CostingTotalLabel = string.Format(CultureInfo.InvariantCulture,
                "{0} sheet(s) \u00b7 {1:0.00} m\u00b2 stock \u00b7 {2:0.00} m\u00b2 glass \u00b7 {3:0.00} m\u00b2 wastage \u00b7 AED {4:N2}",
                s.SheetsUsed, totStock, totGlass, totWaste, totCost);
            CostingPriceNote = CostingRows.Count == 0 || pricedRows == CostingRows.Count
                ? string.Empty
                : string.Format(CultureInfo.InvariantCulture,
                    "No Step 4 rate for {0} of {1} row(s) \u2014 those amounts are pending, not free.",
                    CostingRows.Count - pricedRows, CostingRows.Count);

            // Same breakdown shape a live run fills, grouped the way the stored
            // rows allow (composition), fullest first.
            GroupResults.Clear();
            foreach (var byComp in rows
                         .GroupBy(r => r.Composition ?? string.Empty)
                         .OrderByDescending(g => g.Sum(x => x.UtilizationPct)))
            {
                decimal stockM2 = byComp.Sum(x => x.StockSqm);
                decimal glassM2 = byComp.Sum(x => x.GlassSqm);
                decimal util = stockM2 == 0m ? 0m : glassM2 / stockM2 * 100m;
                GroupResults.Add(new CuttingGroupResultVm
                {
                    Composition = byComp.Key,
                    Thickness = byComp.First().Thickness,
                    StockSize = string.Join(", ", byComp.Select(x => $"{x.StockWidth}×{x.StockHeight}").Distinct()),
                    PieceCount = byComp.Sum(x => x.Pieces).ToString(CultureInfo.InvariantCulture),
                    Sheets = byComp.Sum(x => x.Sheets).ToString(CultureInfo.InvariantCulture),
                    Utilization = util.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                    Waste = Math.Max(0m, stockM2 - glassM2).ToString("0.00", CultureInfo.InvariantCulture) + " m\u00b2",
                    Unplaced = "\u2014",
                    Note = "Restored from the saved run"
                });
            }

            string rotationLabel = s.RotationEnabled ? "rotation ON" : "rotation LOCKED";
            ResultStage = string.Format(CultureInfo.InvariantCulture,
                stage == "OPTIMIZE"
                    ? "OPTIMIZE \u00b7 level {0} \u00b7 {1} \u00b7 saved {2:dd MMM yyyy HH:mm}"
                    : "QUICK \u00b7 {1} \u00b7 saved {2:dd MMM yyyy HH:mm}",
                s.EffortLevel, rotationLabel, s.UpdatedUtc.ToLocalTime());
            ResultSheets = s.SheetsUsed.ToString(CultureInfo.InvariantCulture);
            ResultUtilization = s.UtilizationPct.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            ResultUnplaced = s.UnplacedCount.ToString(CultureInfo.InvariantCulture);
            ResultWaste = Math.Max(0m, totWaste).ToString("0.00", CultureInfo.InvariantCulture) + " m\u00b2";
            ResultWastePct = totStock == 0m
                ? "0.0%"
                : (Math.Max(0m, totStock - totGlass) / totStock * 100m).ToString("0.0", CultureInfo.InvariantCulture) + "%";
            // The oracle checks live geometry, which is not stored. It says so
            // instead of showing a tick this restore did not earn.
            OraclePassed = false;
            ResultOracle = "Not re-verified \u2014 this is the saved run, not a fresh layout check.";
            HasUnplaced = s.UnplacedCount > 0;
            if (HasUnplaced)
            {
                UnplacedRows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} part(s) were unplaced in the saved run. Which parts is not stored \u2014 re-run to list them.",
                    s.UnplacedCount));
            }
            ResultEffortLabel = stage == "OPTIMIZE"
                ? string.Format(CultureInfo.InvariantCulture,
                    "Optimization level {0} of 5 (from SETTINGS)", s.EffortLevel)
                : string.Format(CultureInfo.InvariantCulture,
                    "Quick single pass \u2014 saved at level {0}", s.EffortLevel);
            UpgradeSummaryLabel = string.Empty;
            HasResult = true;
            _selectedSheetIndex = -1;
            SelectedSheetIndex = 0;
            RebuildPreview();
            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} sheet(s), {2:0.00}% utilization, {3} unplaced \u2014 restored from the saved run ({4} priced row(s)), no re-cut.",
                stage, s.SheetsUsed, s.UtilizationPct, s.UnplacedCount, CostingRows.Count);
            // Step 6 and Step 9 read a run through this feed, so a quote reopened
            // days later gets its real figures back without cutting glass again.
            PublishLiveFeed();
            SelectedTabIndex = TabIndexSummary;
            SessionStatus = string.Format(CultureInfo.InvariantCulture,
                "Showing the saved run \u2014 {0} sheet(s) at {1:0.00}%. The sheet-by-sheet layout is not stored; press OPTIMIZE to re-cut and open the 2D view.",
                s.SheetsUsed, s.UtilizationPct);
            return true;
        }

    }
}
