// ═════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationFeedTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Regression locks for the optimization → Step 6 / Step 9 bridge.
//
// The money in a quote is only as good as the join between a finished
// optimization run and the Step 5 pane it cut. These tests lock the three
// places that join can go wrong silently:
//
//   1. SIDE NORMALIZATION. A DGU+LAM spec's "Lam Outer (W2/H2)" pane and a
//      triple-glazed spec's "Middle (W2/H2)" pane are both cut from the
//      group's INNER sheet. A substring test on the word "Outer" sends
//      Lam Outer to the outer rack, and SpecGroupColorSide.SemanticSideLabel
//      folds "Middle" into "Outer" by fall-through. Either mistake prices a
//      pane against the wrong sheet and nothing on screen looks wrong.
//
//   2. PARSER DEFENSIVENESS. Three writers share the ResultJson column with
//      three different shapes. A foreign shape must yield an empty feed, not
//      an exception and not a row of invented zeros.
//
//   3. THE MANUAL CONTRACT. Step 6 stays manual by default, a feed with no
//      data must never write a zero over what the operator typed, and
//      switching back must restore exactly what they typed.
//
// Plus source locks for the two invariants that cannot be unit-tested
// without a database or a full wizard: the feed must never touch
// SheetQuantityText (that setter consumes stock), and Step 9 must claim each
// rack once per rebuild.
// ═════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OptimizationFeedTests
    {
        private static OptimizationPaneResult Pane(
            int spec, string side,
            int w = 3210, int h = 2250,
            int sheets = 17, decimal util = 82.77m,
            string composition = "6mm clear",
            bool shared = false, int sharedWith = 1) => new()
            {
                SpecNumber = spec,
                Side = side,
                Composition = composition,
                SheetWidthMm = w,
                SheetHeightMm = h,
                SheetQuantity = sheets,
                UtilizationPercent = util,
                StockSqm = 7.2225m * sheets,
                GlassSqm = 7.2225m * sheets * util / 100m,
                IsSharedAcrossSpecs = shared,
                SharedWithSpecs = sharedWith
            };

        // ────────────────────────────────────────────────────────────
        //  1. SIDE NORMALIZATION — the wrong-sheet trap
        // ────────────────────────────────────────────────────────────

        [Fact]
        public void Outer_pane_label_maps_to_the_outer_rack()
        {
            Assert.Equal("Outer", OptimizationResultFeed.NormalizeSide("Outer (W1/H1)"));
        }

        [Fact]
        public void Inner_pane_label_maps_to_the_inner_rack()
        {
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Inner (W2/H2)"));
        }

        [Fact]
        public void LamOuter_is_cut_from_the_inner_sheet_so_it_maps_to_inner()
        {
            // WastagesSectionViewModel assigns group.InnerSheetWidthMm to this
            // pane. Reading the word "Outer" instead would price the laminated
            // pane against the outer rack.
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Lam Outer (W2/H2)"));
        }

        [Fact]
        public void LamInner_maps_to_inner()
        {
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Lam Inner (W2/H2)"));
        }

        [Fact]
        public void Middle_panes_map_to_inner_not_outer()
        {
            // A triple-glazed DGU carries two spacers; every middle pane uses
            // the inner sheet geometry. SemanticSideLabel folds "Middle" into
            // "Outer" by fall-through, so the coordinate key has to win.
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Middle (W2/H2)"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Middle 1 (W2/H2)"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Middle 2 (W2/H2)"));
        }

        [Fact]
        public void Coordinate_key_outranks_a_contradicting_pane_word()
        {
            // If a label ever carries both, W1/H1 vs W2/H2 is the ground truth
            // because that is what the sheet assignment is actually keyed on.
            Assert.Equal("Outer", OptimizationResultFeed.NormalizeSide("Outer (W1/H1)"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Lam Outer (W2/H2)"));
        }

        [Fact]
        public void Bare_semantic_words_still_resolve_when_the_coordinate_key_is_gone()
        {
            Assert.Equal("Outer", OptimizationResultFeed.NormalizeSide("Outer"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Inner"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Lam Outer"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Lam Inner"));
            Assert.Equal("Inner", OptimizationResultFeed.NormalizeSide("Middle"));
        }

        [Fact]
        public void Blank_null_and_unrecognised_sides_resolve_to_nothing()
        {
            // An unresolved side must join to nothing rather than guessing a
            // rack, which is how a pane ends up priced against the wrong glass.
            Assert.Equal(string.Empty, OptimizationResultFeed.NormalizeSide(null));
            Assert.Equal(string.Empty, OptimizationResultFeed.NormalizeSide(""));
            Assert.Equal(string.Empty, OptimizationResultFeed.NormalizeSide("   "));
            Assert.Equal(string.Empty, OptimizationResultFeed.NormalizeSide("Spacer"));
        }

        // ────────────────────────────────────────────────────────────
        //  2. LOOKUP
        // ────────────────────────────────────────────────────────────

        [Fact]
        public void Finds_the_pane_by_spec_number_and_side()
        {
            var feed = new[]
            {
                Pane(1, "Outer", sheets: 17),
                Pane(1, "Inner", sheets: 9),
                Pane(2, "Outer", sheets: 4)
            };

            var hit = OptimizationResultFeed.Find(feed, 1, "Inner (W2/H2)");

            Assert.NotNull(hit);
            Assert.Equal(9, hit!.SheetQuantity);
        }

        [Fact]
        public void Sheet_size_confirms_the_right_rack_when_two_look_alike()
        {
            var feed = new[]
            {
                Pane(1, "Outer", w: 3210, h: 2250, sheets: 17),
                Pane(1, "Outer", w: 2440, h: 1830, sheets: 3)
            };

            var hit = OptimizationResultFeed.Find(feed, 1, "Outer (W1/H1)", 2440, 1830);

            Assert.NotNull(hit);
            Assert.Equal(3, hit!.SheetQuantity);
        }

        [Fact]
        public void Falls_back_to_the_same_pane_when_the_sheet_changed_since_the_run()
        {
            // The operator re-quoted the spec on a different sheet after the
            // run. The pane is still the right pane, so returning nothing would
            // drop the row back to a hand-typed number for no good reason.
            var feed = new[] { Pane(1, "Outer", w: 3210, h: 2250, sheets: 17) };

            var hit = OptimizationResultFeed.Find(feed, 1, "Outer (W1/H1)", 2440, 1830);

            Assert.NotNull(hit);
            Assert.Equal(17, hit!.SheetQuantity);
        }

        [Fact]
        public void Returns_null_when_the_run_has_nothing_for_the_pane()
        {
            var feed = new[] { Pane(1, "Outer") };

            Assert.Null(OptimizationResultFeed.Find(feed, 2, "Outer (W1/H1)"));
            Assert.Null(OptimizationResultFeed.Find(feed, 1, "Inner (W2/H2)"));
            Assert.Null(OptimizationResultFeed.Find(feed, 0, "Outer (W1/H1)"));
            Assert.Null(OptimizationResultFeed.Find(feed, 1, null));
            Assert.Null(OptimizationResultFeed.Find(feed, 1, "Spacer"));
            Assert.Null(OptimizationResultFeed.Find(null, 1, "Outer (W1/H1)"));
            Assert.Null(OptimizationResultFeed.Find(
                Array.Empty<OptimizationPaneResult>(), 1, "Outer (W1/H1)"));
        }

        [Fact]
        public void Side_comparison_is_case_insensitive()
        {
            var feed = new[] { Pane(1, "inner", sheets: 9) };

            Assert.NotNull(OptimizationResultFeed.Find(feed, 1, "INNER (W2/H2)"));
            Assert.Equal(9, OptimizationResultFeed.Find(feed, 1, "Inner (W2/H2)")!.SheetQuantity);
        }

        // ────────────────────────────────────────────────────────────
        //  3. PARSER DEFENSIVENESS
        // ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{}")]                                        // bridge writer
        [InlineData("{\"SheetsUsed\":17,\"Utilization\":82.77}")]  // production writer
        [InlineData("[")]                                          // truncated
        [InlineData("not json at all")]
        [InlineData("[{\"Unrelated\":1}]")]                        // array, wrong shape
        public void Foreign_and_empty_result_shapes_yield_an_empty_feed(string? json)
        {
            // Three writers share the ResultJson column with three shapes. A
            // foreign one must degrade to "no data" so Step 6 keeps the
            // operator's manual numbers — never throw, and never invent zeros.
            var rows = OptimizationResultFeed.Parse(json, "session", DateTime.UtcNow);

            Assert.NotNull(rows);
            Assert.Empty(rows);
        }

        [Fact]
        public void Rack_rows_expand_to_one_result_per_pane()
        {
            const string json = "[{" +
                "\"W\":3210,\"H\":2250,\"Composition\":\"6mm clear\",\"Thickness\":\"6\"," +
                "\"Sheets\":17,\"Pieces\":40,\"UtilizationPct\":82.77," +
                "\"GlassSqm\":59.80,\"StockSqm\":72.22,\"WasteSqm\":12.42," +
                "\"RatePerSqm\":180.5,\"HasPrice\":true," +
                "\"IsSharedAcrossSpecs\":false,\"SharedWithSpecs\":1," +
                "\"Panes\":[{\"SpecNumber\":1,\"Side\":\"Outer\"}," +
                           "{\"SpecNumber\":2,\"Side\":\"Outer\"}]}]";

            var rows = OptimizationResultFeed.Parse(json, "run A", DateTime.UtcNow);

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.Equal(17, r.SheetQuantity);
                Assert.Equal(82.77m, r.UtilizationPercent);
                Assert.Equal(3210, r.SheetWidthMm);
                Assert.Equal("run A", r.SessionName);
            });
            Assert.Equal(1, rows[0].SpecNumber);
            Assert.Equal(2, rows[1].SpecNumber);
        }

        [Fact]
        public void A_rack_with_no_pane_identity_is_skipped_rather_than_guessed()
        {
            const string json = "[{" +
                "\"W\":3210,\"H\":2250,\"Composition\":\"6mm clear\"," +
                "\"Sheets\":17,\"UtilizationPct\":82.77,\"Panes\":[]}]";

            Assert.Empty(OptimizationResultFeed.Parse(json, "run", DateTime.UtcNow));
        }

        [Fact]
        public void Every_pane_on_a_shared_rack_is_flagged_as_shared()
        {
            // Two specs on one rack: the sheet count is the shared total, so
            // every pane that sees it must know not to treat it as its own.
            const string json = "[{" +
                "\"W\":3210,\"H\":2250,\"Composition\":\"6mm clear\"," +
                "\"Sheets\":17,\"UtilizationPct\":82.77," +
                "\"IsSharedAcrossSpecs\":true,\"SharedWithSpecs\":2," +
                "\"Panes\":[{\"SpecNumber\":1,\"Side\":\"Outer\"}," +
                           "{\"SpecNumber\":2,\"Side\":\"Outer\"}]}]";

            var rows = OptimizationResultFeed.Parse(json, "run", DateTime.UtcNow);

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.True(r.IsSharedAcrossSpecs);
                Assert.Equal(2, r.SharedWithSpecs);
                Assert.Contains("shared", r.Provenance, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void A_rack_that_did_not_know_it_was_shared_is_still_flagged_from_its_pane_count()
        {
            const string json = "[{" +
                "\"W\":3210,\"H\":2250,\"Sheets\":17,\"UtilizationPct\":82.77," +
                "\"IsSharedAcrossSpecs\":false,\"SharedWithSpecs\":0," +
                "\"Panes\":[{\"SpecNumber\":1,\"Side\":\"Outer\"}," +
                           "{\"SpecNumber\":2,\"Side\":\"Outer\"}]}]";

            var rows = OptimizationResultFeed.Parse(json, "run", DateTime.UtcNow);

            Assert.All(rows, r =>
            {
                Assert.True(r.IsSharedAcrossSpecs);
                Assert.Equal(2, r.SharedWithSpecs);
            });
        }

        [Fact]
        public void Pane_sides_are_normalized_on_the_way_in()
        {
            const string json = "[{" +
                "\"W\":3210,\"H\":2250,\"Sheets\":9,\"UtilizationPct\":70," +
                "\"Panes\":[{\"SpecNumber\":1,\"Side\":\"Lam Outer (W2/H2)\"}]}]";

            var rows = OptimizationResultFeed.Parse(json, "run", DateTime.UtcNow);

            Assert.Single(rows);
            Assert.Equal("Inner", rows[0].Side);
        }

        // ────────────────────────────────────────────────────────────
        //  4. THE STEP 6 MANUAL CONTRACT
        // ────────────────────────────────────────────────────────────

        [Fact]
        public void Manual_values_are_untouched_while_the_feed_is_off()
        {
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };

            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            Assert.False(row.UseOptimizationFeed);
            Assert.Equal(5, row.SheetQuantity);
            Assert.Equal(12.5m, row.OptimizationPercent);

            // offered, but not applied
            Assert.Equal(17, row.OptimizationSheetQuantity);
            Assert.Equal(82.77m, row.OptimizationUtilizationPercent);
            Assert.True(row.HasOptimizationFeed);
        }

        [Fact]
        public void Turning_the_feed_on_applies_the_run_figures()
        {
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            row.UseOptimizationFeed = true;

            Assert.Equal(17, row.SheetQuantity);
            Assert.Equal(82.77m, row.OptimizationPercent);
            Assert.True(row.IsOptimizationFeedMode);
            Assert.False(row.IsManualSheetSourceMode);
        }

        [Fact]
        public void Turning_the_feed_off_restores_exactly_what_the_operator_typed()
        {
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            row.UseOptimizationFeed = true;
            row.UseOptimizationFeed = false;

            Assert.Equal(5, row.SheetQuantity);
            Assert.Equal(12.5m, row.OptimizationPercent);
            Assert.True(row.IsManualSheetSourceMode);
        }

        [Fact]
        public void A_feed_with_no_data_never_writes_a_zero_over_manual_input()
        {
            // The dangerous case: the run has nothing for this pane. Writing 0
            // would zero the buy side of the Step 9 ledger.
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            row.UseOptimizationFeed = true;

            row.SetOptimizationFeed(0, 0m, false, "the run has no row for this pane");

            Assert.Equal(5, row.SheetQuantity);
            Assert.Equal(12.5m, row.OptimizationPercent);
            Assert.False(row.HasOptimizationFeed);
        }

        [Fact]
        public void A_feed_arriving_after_the_switch_is_on_applies_immediately()
        {
            var row = new WastageRow { SheetQuantity = 5 };
            row.UseOptimizationFeed = true;
            Assert.Equal(5, row.SheetQuantity);

            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            Assert.Equal(17, row.SheetQuantity);
            Assert.Equal(82.77m, row.OptimizationPercent);
        }

        [Fact]
        public void Clearing_the_feed_returns_the_row_to_purely_manual_input()
        {
            var row = new WastageRow { SheetQuantity = 5 };
            row.SetOptimizationFeed(17, 82.77m, false, "from the run");
            row.UseOptimizationFeed = true;

            row.ClearOptimizationFeed();

            Assert.False(row.HasOptimizationFeed);
            Assert.Equal(0, row.OptimizationSheetQuantity);
        }

        [Fact]
        public void A_shared_rack_is_flagged_so_the_total_is_not_reused_per_pane()
        {
            var row = new WastageRow { SheetQuantity = 5 };
            row.SetOptimizationFeed(17, 82.77m, true, "shared by 2 specs");

            Assert.True(row.IsSharedOptimizationRack);
            Assert.Contains("shared", row.OptimizationFeedLabel, StringComparison.OrdinalIgnoreCase);
            Assert.True(row.HasOptimizationFeedNote);
        }

        // ────────────────────────────────────────────────────────────
        //  5. SOURCE LOCKS — invariants that need a database or a full
        //     wizard to exercise, locked structurally instead.
        // ────────────────────────────────────────────────────────────

        [Fact]
        public void The_feed_writes_SheetQuantity_directly_never_the_stock_consuming_text_setter()
        {
            // SheetQuantityText's setter raises SheetQuantityCommittedByUser,
            // which consumes stock. A feed refresh routed through it would buy
            // glass every time the operator ticked a source.
            string row = Read(LocateProjectRoot(),
                "Models/ProformaInvoice/Step6OtherCharges/Wastages/WastageRow.cs");

            int feedStart = row.IndexOf("private void ApplySheetSourceMode()",
                StringComparison.Ordinal);
            Assert.True(feedStart > 0, "ApplySheetSourceMode not found");

            string body = row.Substring(feedStart);
            Assert.Contains("SheetQuantity = _optimizationSheetQuantity;", body);
            Assert.DoesNotContain("SheetQuantityText =", body);
            Assert.DoesNotContain("OptimizationPercentText =", body);
        }

        [Fact]
        public void Step9_claims_each_rack_once_per_rebuild()
        {
            // "Middle (W2/H2)" and "Inner (W2/H2)" both resolve to the one
            // inner rack. Without the claim guard each pane takes the rack's
            // full sheet count and the buy side doubles.
            string vm = Read(LocateProjectRoot(),
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            Assert.Contains("if (!_feedRacksClaimed.Add(rackKey)) return false;", vm);
            Assert.Contains("_feedRacksClaimed.Clear();", vm);

            // the clear has to sit inside the rebuild, ahead of both call sites
            int clear = vm.IndexOf("_feedRacksClaimed.Clear();", StringComparison.Ordinal);
            int refresh = vm.IndexOf("public void RefreshAnalysis()", StringComparison.Ordinal);
            int firstUse = vm.IndexOf("if (TryGetOptimizationSheetFigures(",
                StringComparison.Ordinal);

            Assert.True(refresh > 0 && clear > refresh,
                "rack claim set is not cleared at the top of RefreshAnalysis");
            Assert.True(firstUse > clear,
                "a rack lookup runs before the claim set is cleared");
        }

        [Fact]
        public void Step6_resolves_panes_by_their_own_sheet_size_not_the_pane_name_alone()
        {
            string vm = Read(LocateProjectRoot(),
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs");

            Assert.Contains("src.SideLabel, src.SheetWidthMm, src.SheetHeightMm", vm);

            // distinct racks, so two panes sharing one sheet count once
            Assert.Contains("if (seen.Add(key)) racks.Add(hit);", vm);
        }

        [Fact]
        public void Sessions_persist_the_priced_breakdown_with_pane_identity()
        {
            string session = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.Session.cs");

            // ResultJson used to be written as string.Empty, so a saved session
            // could not answer "what did the run produce for spec 3 inner".
            Assert.Contains("ResultJson = JsonSerializer.Serialize(resultRows)", session);
            Assert.DoesNotContain("ResultJson = string.Empty", session);
            Assert.Contains("public int SpecNumber { get; set; }", session);
            Assert.Contains("public string Side { get; set; }", session);
        }

        [Fact]
        public void A_live_run_outranks_a_saved_one()
        {
            // Sessions are only written on an explicit SAVE, so every run
            // publishes itself. A later disk refresh must not trade those newer
            // live figures for an older saved session.
            string feed = Read(LocateProjectRoot(),
                "Services/Optimization/OptimizationResultFeed.cs");

            Assert.Contains("if (_isLive && _current.Count > 0) return;", feed);

            string run = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.Run.cs");
            Assert.Contains("PublishLiveFeed();", run);
        }

        [Fact]
        public void Optimization_racks_carry_their_spec_and_side_from_step5()
        {
            string jobs = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.Jobs.cs");

            Assert.Contains("group.GroupNumber, \"Outer\"", jobs);
            Assert.Contains("group.GroupNumber, \"Inner\"", jobs);
        }

        // ────────────────────────────────────────────────────────────

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }

        private static string Read(string root, string relative) =>
            File.ReadAllText(Path.Combine(root, relative));
    }
}
