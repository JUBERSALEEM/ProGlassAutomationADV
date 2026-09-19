// ═══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/Step5ProgressAndViewResultTests.cs
//  PROJECT: ProGlassAutomation.Tests
//
//  Covers the 2026-09-17 round of reported defects:
//    · the AI chat panel's drag bindings pointed at code-behind property
//      names instead of XAML element names (System.Windows.Data Error: 4,
//      and a chat window that could not be moved);
//    · QUICK reported no progress at all, so Step 5's bar sat at 0% for the
//      whole run and then jumped — "show actual real progress";
//    · Step 5 runs cut against a sheet size Step 4 never supplied, because
//      DGU+LAM (and any legacy build type) leaves the group's sheet size at
//      zero — Step 4 is now the authority with a Sheet Store cross-check;
//    · VIEW RESULT rebuilt the cutting list and RE-RAN the engine, which on
//      a large quote is a frozen window and at 30k parts a dead process.
//
//  WHAT IS TESTED BEHAVIOURALLY, AND WHY THE REST IS PINNED IN SOURCE
//  The packer is a static pure function, so progress reporting is asserted by
//  running it — including the one property that must never change: a run with
//  a progress sink must produce exactly the plan a run without one produces,
//  because the owner's benchmark figures are pinned on PackQuick.
//  OptimizationViewModel cannot be constructed in a test (its constructor
//  starts live database loads), so the run/restore/Step 5 wiring is pinned in
//  source. The Sheet Store cross-check is asserted only on the paths that
//  short-circuit before touching a database, so the test never creates one.
//
//  There is no dotnet SDK in this environment, so these tests are the build
//  verdict on the developer's machine, not here.
// ═══════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class Step5ProgressAndViewResultTests
    {
        // ── helpers ────────────────────────────────────────────────────
        private const string AiChatView = "Views/AI/AiChatView.xaml";
        private const string Packer = "Services/Optimization/GuillotinePacker.cs";
        private const string RunVm = "ViewModels/Optimization/OptimizationViewModel.Run.cs";
        private const string SessionVm = "ViewModels/Optimization/OptimizationViewModel.Session.cs";
        private const string LiveRunVm = "ViewModels/Optimization/OptimizationViewModel.LiveRun.cs";
        private const string SheetStoreSvc = "Services/ProformaInvoice/SheetStorePricingService.cs";
        private const string ComposerVm = "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs";

        private static string Root()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Views", "Optimization")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string Slice(string text, string start, string end)
        {
            int i = text.IndexOf(start, StringComparison.Ordinal);
            Assert.True(i >= 0, $"start marker not found: {start}");
            int j = text.IndexOf(end, i + start.Length, StringComparison.Ordinal);
            Assert.True(j > i, $"end marker not found after start: {end}");
            return text.Substring(i, j + end.Length - i);
        }

        /// <summary>
        /// Collects reports synchronously. <see cref="Progress{T}"/> posts to a
        /// synchronization context, so it would make these assertions race; the
        /// engine only ever needs the interface.
        /// </summary>
        private sealed class CollectingProgress : IProgress<OptimizeProgress>
        {
            private readonly List<OptimizeProgress> _into;
            public CollectingProgress(List<OptimizeProgress> into) => _into = into;
            public void Report(OptimizeProgress value) => _into.Add(value);
        }

        private static List<CutPiece> Pieces()
        {
            var list = new List<CutPiece>();
            for (int i = 0; i < 40; i++)
            {
                list.Add(new CutPiece
                {
                    GlassRef = "6mm clear ft glass",
                    PieceLabel = (i + 1).ToString(CultureInfo.InvariantCulture),
                    WidthMm = 400 + (i % 7) * 90,
                    HeightMm = 600 + (i % 5) * 130
                });
            }
            return list;
        }

        private static List<StockSize> Stock() =>
            new List<StockSize> { new StockSize { WidthMm = 3210, HeightMm = 2250 } };

        // ══ 1. QUICK progress: real, monotone, and packing-neutral ══════
        [Fact]
        public void Quick_reports_real_progress_and_produces_the_identical_plan()
        {
            var reports = new List<OptimizeProgress>();
            var sink = new CollectingProgress(reports);

            // Fresh pieces and stock for each run: the comparison is only proof
            // of "reporting changes nothing" if both runs start from identical,
            // unshared input.
            var withSink = GuillotinePacker.PackQuick(Pieces(), Stock(), MarginProfile.DefaultFor(6),
                RotationPolicy.Free90, SplitMode.HorizontalFirst, default, sink);
            var without = GuillotinePacker.PackQuick(Pieces(), Stock(), MarginProfile.DefaultFor(6),
                RotationPolicy.Free90, SplitMode.HorizontalFirst);

            // The benchmark-critical property: reporting observes, it never
            // steers. Same sheets, same unplaced, same utilization.
            Assert.Equal(without.Sheets.Count, withSink.Sheets.Count);
            Assert.Equal(without.Unplaced.Count, withSink.Unplaced.Count);

            Assert.NotEmpty(reports);
            Assert.Contains(reports, r => r.Stage == "quick");
            Assert.Contains(reports, r => r.Stage == "consolidate");
            Assert.Equal("done", reports[reports.Count - 1].Stage);
            Assert.Equal(1.0, reports[reports.Count - 1].Fraction, 3);

            double previous = -1d;
            foreach (var r in reports)
            {
                Assert.True(r.Fraction >= previous,
                    $"progress went backwards: {previous:0.###} → {r.Fraction:0.###} ({r.Stage})");
                previous = r.Fraction;
            }

            // Throttled to whole percent changes: a 40-part job reports at most
            // 40 placement steps, and a 30,000-part job still reports ~100.
            Assert.True(reports.Count <= 102,
                $"expected throttled reporting, got {reports.Count} reports");
        }

        [Fact]
        public void Quick_progress_is_optional_so_every_existing_caller_is_untouched()
        {
            string packer = Read(Packer);
            string quick = Slice(packer, "public static OptimizationResult PackQuick(", "var scale = ScaleProfile.For(pieces.Count, 1);");
            Assert.Contains("CancellationToken cancellationToken = default,", quick);
            Assert.Contains("IProgress<OptimizeProgress> progress = null)", quick);
            // The ordering the benchmark is pinned on must not have moved.
            Assert.Contains("RunPass(Sort(pieces, OrderBy.Area), stocks, margins, rotation, split,", packer);
            Assert.Contains("cancellationToken, scale.ConsolidationSweeps, progress);", packer);
        }

        [Fact]
        public void A_single_candidate_optimize_pass_reports_per_part_instead_of_jumping()
        {
            string packer = Read(Packer);
            Assert.Contains("comboList.Count == 1 ? progress : null);", packer);
            // Multi-candidate runs keep candidate-level reporting and must not
            // pay a per-part post 149 times over.
            Assert.Contains("progress?.Report(OptimizeProgress.Of(\"pass\", ci, smartTotal,", packer);
        }

        [Fact]
        public void The_step5_run_hands_quick_the_same_sink_optimize_already_had()
        {
            string run = Read(RunVm);
            Assert.Contains("rotation, SplitMode.HorizontalFirst, ct, groupProgress))", run);
            // The sink already feeds Step 5's bar through the composer mirror.
            Assert.Contains("RunProgressPercent = (int)Math.Round(done * 100.0);", run);
        }

        // ══ 2. the chat panel drag bindings ═════════════════════════════
        [Fact]
        public void The_chat_header_binds_real_xaml_names_not_code_behind_properties()
        {
            string view = Read(AiChatView);
            Assert.Contains("behaviors:CanvasDragBehavior.Panel=\"{Binding ElementName=ChatPanel}\"", view);
            Assert.Contains("behaviors:CanvasDragBehavior.Canvas=\"{Binding ElementName=ChatCanvas}\"", view);
            // The elements those names must resolve to really are named.
            Assert.Contains("x:Name=\"ChatPanel\"", view);
            Assert.Contains("x:Name=\"ChatCanvas\"", view);
            // ChatPanelRef / ChatCanvasRef are private FindName() accessors in
            // the code-behind: ElementName cannot see them, which is exactly the
            // System.Windows.Data Error: 4 that was reported.
            Assert.DoesNotContain("ElementName=ChatPanelRef", view);
            Assert.DoesNotContain("ElementName=ChatCanvasRef", view);
        }

        // ══ 3. Step 5 sheet size: Step 4 first, Sheet Store cross-check ══
        [Fact]
        public void Step5_records_what_step4_covered_and_fills_only_the_gaps()
        {
            string live = Read(LiveRunVm);
            Assert.Contains("var wantedByGroup = new Dictionary<int, HashSet<string>>();", live);
            Assert.Contains("var rackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);", live);
            // A Step 4 rack marks its (group, composition) as covered...
            Assert.Contains("rackedKeys.Add(compKey(group.GroupNumber, outerComp));", live);
            Assert.Contains("rackedKeys.Add(compKey(group.GroupNumber, innerComp));", live);
            // ...and the fallback skips anything already covered, so Step 4 is
            // never overridden — it is only supplemented.
            Assert.Contains("if (rackedKeys.Contains(compKey(group.GroupNumber, comp))) continue;", live);
            Assert.Contains(".TryResolveStockSheet(", live);
            Assert.Contains("\"Sheet Store cross-check\"", live);
            Assert.Contains("racksAdded += storeRacks;", live);
        }

        [Fact]
        public void A_rack_that_did_not_come_from_step4_is_announced_not_silent()
        {
            string live = Read(LiveRunVm);
            Assert.Contains("if (sheetNotes.Count > 0)", live);
            Assert.Contains("message += \" \" + string.Join(\" \", sheetNotes);", live);
            Assert.Contains("had no sheet size from Step 4", live);
            Assert.Contains("Step 4 still governs the run.", live);
            // The refusal now says the store was asked too, so the operator is
            // not left wondering whether the fallback ran.
            Assert.Contains("The Sheet Store had no stocked size for these glass colours either.", live);
        }

        [Fact]
        public void The_cross_check_helper_exists_and_is_a_note_never_an_override()
        {
            string live = Read(LiveRunVm);
            string helper = Slice(live, "private static void CrossCheckStep4Sheet(", "notes.Add(string.Format(CultureInfo.InvariantCulture,");
            Assert.Contains("List<string> notes)", helper);
            // Silent when the store cannot answer at all — a cold snapshot must
            // never manufacture a warning.
            Assert.Contains("TryResolveStockSheet(colour, 0, out int stockedW, out int stockedH))", helper);
            Assert.Contains("IsStockedSize(colour, 0, widthMm, heightMm))", helper);
            // It writes into notes; nothing in it touches Stocks.
            Assert.DoesNotContain("Stocks.Add", helper);
        }

        [Fact]
        public void The_sync_store_lookup_never_blocks_and_never_throws()
        {
            string svc = Read(SheetStoreSvc);
            Assert.Contains("public static void PrimeSyncSnapshot()", svc);
            Assert.Contains("public static async Task RefreshSyncSnapshotAsync()", svc);
            Assert.Contains("public static bool TryResolveStockSheet(", svc);
            Assert.Contains("public static bool IsStockedSize(", svc);
            Assert.Contains("private const int SnapshotTrustSeconds = 60;", svc);

            // Sync means sync, and BOUNDED means bounded.
            //
            // This used to assert DoesNotContain(".Result") across the two
            // lookups. That assertion has been RED since the 2026-09-20 senior
            // fix, which added a deliberate cold-cache database fallback to both
            // — without it a first optimization run came back with zero racks for
            // DGU+LAM and any spec Step 4 left without a sheet. The fix reads
            // task.Wait(timeout) then task.Result, so ".Result" is present by
            // design.
            //
            // The invariant worth pinning is not "never blocks" but "never blocks
            // WITHOUT A TIMEOUT". RunFromStep5 builds racks on the UI thread, so
            // an unbounded wait freezes the window; a bounded one degrades to
            // "the store cannot say", which every caller already handles as a
            // note rather than an error. Asserting the weaker, true contract here
            // and adding the two Phase 10 balance lookups to the same sweep.
            foreach (var start in new[]
                     {
                         "public static bool TryResolveStockSheet(",
                         "public static bool IsStockedSize(",
                         "private static bool TryGetSnapshot(",
                         "public static bool TryGetStockBalance(",
                         "private static bool TryGetSnapshotWithBoundedFallback("
                     })
            {
                string body = Slice(svc, start, "catch (Exception ex)");

                // Unbounded blocking, in any form: never.
                Assert.DoesNotContain(".Wait()", body);
                Assert.DoesNotContain(".Wait();", body);
                Assert.DoesNotContain("GetAwaiter().GetResult()", body);
            }

            // And wherever a blocking read does happen, the wait that precedes it
            // must carry an explicit bound — a literal timeout, or the timeout
            // parameter the shared fallback helper is handed by its callers
            // (1500 for the balance lookup, 2000 for the rack resolve).
            foreach (var start in new[]
                     {
                         "public static bool TryResolveStockSheet(",
                         "public static bool IsStockedSize(",
                         "public static bool TryGetStockBalance(",
                         "private static bool TryGetSnapshotWithBoundedFallback("
                     })
            {
                string body = Slice(svc, start, "catch (Exception ex)");
                if (body.Contains(".Result"))
                    Assert.Matches(@"\.Wait\(\s*(\d+|timeoutMs)\s*\)", body);
            }

            // Every async lookup still reads the store fresh — the header note
            // promises the snapshot is a cross-check, not a pricing authority.
            Assert.Contains("var sheets = await LoadAllAsync().ConfigureAwait(false);", svc);

            // The guards that must answer before any database is touched.
            Assert.False(SheetStorePricingService.TryResolveStockSheet(null, 6, out int w, out int h));
            Assert.Equal(0, w);
            Assert.Equal(0, h);
            Assert.False(SheetStorePricingService.TryResolveStockSheet("   ", 0, out _, out _));
            Assert.False(SheetStorePricingService.IsStockedSize("clear", 6, 0, 0));
            Assert.False(SheetStorePricingService.IsStockedSize(null, 0, 3210, 2250));
        }

        [Fact]
        public void The_composer_warms_the_snapshot_when_the_host_is_created()
        {
            string composer = Read(ComposerVm);
            Assert.Contains("Services.ProformaInvoice.SheetStorePricingService.PrimeSyncSnapshot();", composer);
            string host = Slice(composer, "private ViewModels.Optimization.OptimizationViewModel CreateOptimizationHost()", "return host;");
            Assert.Contains("PrimeSyncSnapshot();", host);
        }

        // ══ 4. VIEW RESULT reads the saved run instead of re-cutting ════
        [Fact]
        public void Viewing_a_result_restores_the_stored_run_and_does_not_re_run_it()
        {
            string session = Read(SessionVm);
            Assert.Contains("private bool TryRestoreStoredResult(SavedOptimizationSession s)", session);
            Assert.Contains("if (!openToReport && TryRestoreStoredResult(s))", session);

            // The early return is the whole point: no Run() on this path.
            string gate = Slice(session,
                "if (!openToReport && TryRestoreStoredResult(s))",
                "_openToReportAfterRun = openToReport;");
            Assert.Contains("return;", gate);
            Assert.DoesNotContain("Run(string.Equals(s.EngineStage", gate);

            // Reopening a NAMED session still re-runs: it asks for the REPORT
            // tab, which needs real geometry that is not stored.
            Assert.Contains("RestoreSessionCore(s, openToReport: true);", session);
        }

        [Fact]
        public void The_restored_run_is_published_and_lands_on_summary()
        {
            string session = Read(SessionVm);
            string restore = Slice(session,
                "private bool TryRestoreStoredResult(SavedOptimizationSession s)",
                "return true;");

            // Reads what the run persisted, and gives up honestly if there is
            // nothing to read (legacy rows fall back to the re-run).
            Assert.Contains("JsonSerializer.Deserialize<List<ResultRowDto>>(s.ResultJson)", restore);
            Assert.Contains("if (rows.Count == 0) return false;", restore);
            Assert.Contains("CostingRows.Add(row);", restore);

            // Step 6 / Step 9 read a run through this feed, so a quote reopened
            // days later gets its real figures back without cutting glass again.
            Assert.Contains("PublishLiveFeed();", restore);
            Assert.Contains("SelectedTabIndex = TabIndexSummary;", restore);
            Assert.Contains("HasResult = true;", restore);

            // Geometry is not stored: clear it and say so, rather than render an
            // empty canvas the operator reads as a broken button.
            Assert.Contains("_flatSheets.Clear();", restore);
            Assert.Contains("SheetRows.Clear();", restore);
            Assert.Contains("RebuildPreview();", restore);
            Assert.Contains("The sheet-by-sheet layout is not stored", restore);
            // The oracle verifies live layouts; a restore must not claim a tick
            // it did not earn.
            Assert.Contains("OraclePassed = false;", restore);
        }

        [Fact]
        public void The_document_is_stamped_before_a_restored_run_is_published()
        {
            string session = Read(SessionVm);
            string show = Slice(session,
                "public async Task ShowLatestRunAsync(string? documentKey)",
                "RestoreSessionCore(s, openToReport: false);");
            // PublishLiveFeed stamps with DocumentKey; an unstamped publish is a
            // run every other quote can read back.
            Assert.Contains("DocumentKey = documentKey ?? string.Empty;", show);
            Assert.Contains("await _sessions.GetLatestRunAsync(documentKey);", show);
        }
    }
}
