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

        private async void OpenSession(SessionRowVm row)
        {
            if (row == null) return;
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

                RestoreSessionCore(s, openToReport: false);
                SessionStatus = "Showing the latest run — re-running its layout…";
            }
            catch (Exception ex)
            {
                SessionStatus = "Could not open the latest run: " + ex.Message;
            }
        }

        // ── delete ─────────────────────────────────────────────────────

        private async void DeleteSession(SessionRowVm row)
        {
            if (row == null) return;
            try
            {
                await _sessions.DeleteAsync(row.Id);
                SavedSessions.Remove(row);
                HasSessions = SavedSessions.Count > 0;
                SessionStatus = "Session deleted.";
            }
            catch (Exception ex)
            {
                SessionStatus = "Could not delete session: " + ex.Message;
            }
        }
    }
}
