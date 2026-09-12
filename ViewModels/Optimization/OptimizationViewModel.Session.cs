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

        private void PopulateSessions(List<SavedOptimizationSession> list)
        {
            SavedSessions.Clear();
            foreach (var s in list)
            {
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

        private async Task SaveSessionAsync()
        {
            try
            {
                if (!HasPieces)
                {
                    SessionStatus = "Nothing to save — add pieces on the JOBS tab first.";
                    return;
                }

                // No ConfigureAwait(false) here: every VM mutation below must
                // happen on the captured UI synchronization context.
                await _sessions.EnsureCreatedAsync();

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
                        IsUsedInRun = s.IsUsedInRun
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

                var session = new SavedOptimizationSession
                {
                    Name = string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1:yyyy-MM-dd HH:mm}", LastRunStage, DateTime.Now),
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
                    ResultJson = string.Empty
                };

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

                // Re-run the saved engine stage — deterministic, so the
                // reopened layout is byte-for-byte the saved one — then
                // land on the REPORT tab.
                _openToReportAfterRun = true;
                Run(string.Equals(s.EngineStage, "OPTIMIZE", StringComparison.OrdinalIgnoreCase)
                    ? "OPTIMIZE" : "QUICK");

                SessionStatus = "Opened " + (s.Name ?? "session") + " — re-running its layout…";
            }
            catch (Exception ex)
            {
                SessionStatus = "Could not open session: " + ex.Message;
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
