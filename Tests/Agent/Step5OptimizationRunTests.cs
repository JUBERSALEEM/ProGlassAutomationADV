// ═══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/Step5OptimizationRunTests.cs
//  PROJECT: ProGlassAutomation.Tests
//
//  Covers the Step 5 QUICK / OPTIMIZE run path and the two guards that make
//  "Step 6 reads the run by default" safe:
//
//    1. DOCUMENT SCOPING. A run stamped for one proforma must not be applied
//       to another. The Step 6 join is structural (spec number + side + sheet
//       size), so two quotes that both have a "Spec 1 outer" on the same rack
//       size match each other. Without the stamp, quote A silently reads
//       quote B's sheet count.
//
//    2. THE OPERATOR'S EXPLICIT CHOICE. A row now enables itself when the run
//       actually resolves it, so the switch is no longer the only way in. That
//       makes it essential that a deliberate OFF survives every later refresh
//       and every Step 5 rebuild — and that enabling parks the operator's own
//       numbers, because parking happens only on the false→true transition.
//
//  Behavioural tests exercise the real types. The source locks exist for the
//  wiring that cannot be reached without a running WPF application: the XAML
//  bindings, the composer's wiring, and the ORDER of two assignments in an
//  object initializer, which is load-bearing and invisible at runtime.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using ProGlassAutomation.Models.Optimization;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class Step5OptimizationRunTests : IDisposable
    {
        // The feed is static, so every test starts from a known-empty state and
        // leaves it that way for the next one.
        public Step5OptimizationRunTests() => OptimizationResultFeed.Reset();

        public void Dispose() => OptimizationResultFeed.Reset();

        private static OptimizationPaneResult Pane(int spec, string side, int sheets = 17) =>
            new OptimizationPaneResult
            {
                SpecNumber = spec,
                Side = side,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                Composition = "6mm clear",
                SheetQuantity = sheets,
                UtilizationPercent = 82.77m,
                SessionName = "test run"
            };

        // ── 1. document scoping ────────────────────────────────────────────

        [Fact]
        public void A_run_stamped_for_another_proforma_is_refused()
        {
            OptimizationResultFeed.PublishLive(new[] { Pane(1, "Outer") }, "run", "PI-2");

            Assert.True(OptimizationResultFeed.HasData);
            Assert.False(OptimizationResultFeed.IsForDocument("PI-1"));
        }

        [Fact]
        public void A_run_stamped_for_this_proforma_is_accepted()
        {
            OptimizationResultFeed.PublishLive(new[] { Pane(1, "Outer") }, "run", "PI-1");

            Assert.True(OptimizationResultFeed.IsForDocument("PI-1"));
            // Case and padding must not matter: the id travels through a text
            // box in Step 1 and a database primary key on the way back.
            Assert.True(OptimizationResultFeed.IsForDocument("  pi-1 "));
        }

        [Fact]
        public void An_unstamped_run_stays_usable_everywhere()
        {
            // A run driven from the OPTIMIZATION tab works off confirmed Job
            // Orders and has no single proforma identity. Refusing those would
            // have broken the existing flow, so "no stamp" means "no evidence of
            // a mismatch" and is accepted exactly as before.
            OptimizationResultFeed.PublishLive(new[] { Pane(1, "Outer") }, "run");

            Assert.Equal(string.Empty, OptimizationResultFeed.DocumentKey);
            Assert.True(OptimizationResultFeed.IsForDocument("PI-1"));
            Assert.True(OptimizationResultFeed.IsForDocument("anything-else"));
        }

        [Fact]
        public void A_draft_with_no_identity_yet_still_sees_the_run()
        {
            // The proforma id is published as the operator moves through the
            // wizard, so a run can be requested before Step 1 has an id. Refusing
            // there would read as "the button does nothing".
            OptimizationResultFeed.PublishLive(new[] { Pane(1, "Outer") }, "run", "PI-9");

            Assert.True(OptimizationResultFeed.IsForDocument(null));
            Assert.True(OptimizationResultFeed.IsForDocument(""));
        }

        [Fact]
        public void An_empty_feed_belongs_to_no_document()
        {
            Assert.False(OptimizationResultFeed.HasData);
            Assert.False(OptimizationResultFeed.IsForDocument("PI-1"));
            Assert.False(OptimizationResultFeed.IsForDocument(null));
        }

        [Fact]
        public void Reset_clears_the_document_stamp()
        {
            OptimizationResultFeed.PublishLive(new[] { Pane(1, "Outer") }, "run", "PI-1");
            Assert.Equal("PI-1", OptimizationResultFeed.DocumentKey);

            OptimizationResultFeed.Reset();

            Assert.Equal(string.Empty, OptimizationResultFeed.DocumentKey);
            Assert.False(OptimizationResultFeed.IsForDocument("PI-1"));
        }

        // ── 2. per-document reserved row id ────────────────────────────────

        [Fact]
        public void Reserved_row_ids_are_per_document()
        {
            Assert.Equal("__latest_run__:PI-7",
                SavedOptimizationSession.LatestRunIdFor("PI-7"));
            Assert.Equal("__latest_run__:PI-7",
                SavedOptimizationSession.LatestRunIdFor("  PI-7  "));
        }

        [Fact]
        public void A_run_with_no_document_falls_back_to_the_legacy_global_row()
        {
            Assert.Equal(SavedOptimizationSession.LatestRunId,
                SavedOptimizationSession.LatestRunIdFor(null));
            Assert.Equal(SavedOptimizationSession.LatestRunId,
                SavedOptimizationSession.LatestRunIdFor("   "));
        }

        [Fact]
        public void Every_reserved_row_shape_is_hidden_from_saved_sessions()
        {
            // Both forms must be filtered out of the Saved Sessions list: they
            // are feed plumbing, not something the operator chose to keep.
            Assert.True(SavedOptimizationSession.IsReservedLatestRunId(
                SavedOptimizationSession.LatestRunId));
            Assert.True(SavedOptimizationSession.IsReservedLatestRunId(
                SavedOptimizationSession.LatestRunIdFor("PI-7")));
        }

        [Fact]
        public void An_ordinary_session_id_is_not_a_reserved_row()
        {
            Assert.False(SavedOptimizationSession.IsReservedLatestRunId(
                Guid.NewGuid().ToString("N")));
            Assert.False(SavedOptimizationSession.IsReservedLatestRunId(null));
            Assert.False(SavedOptimizationSession.IsReservedLatestRunId(""));
            // A name that merely starts with the words must not be swept up.
            Assert.False(SavedOptimizationSession.IsReservedLatestRunId("__latest_run"));
        }

        // ── 3. Step 6 enabling itself, without losing the operator's numbers ─

        [Fact]
        public void A_row_the_run_resolves_enables_itself()
        {
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            Assert.False(row.UseOptimizationFeed);

            Assert.True(row.TryEnableFeedAutomatically());

            Assert.True(row.IsOptimizationFeedMode);
            Assert.Equal(17, row.SheetQuantity);
            Assert.Equal(82.77m, row.OptimizationPercent);
        }

        [Fact]
        public void Automatic_enable_parks_the_operators_numbers_not_the_initializer_default()
        {
            // THE DATA-LOSS GUARD. Manual values are parked on the false→true
            // transition only. If the default were applied by writing the field
            // instead of the property, _manualSheetQuantity would stay at its
            // initializer (1) and the first switch back to manual would replace
            // the operator's 5 with 1.
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            Assert.True(row.TryEnableFeedAutomatically());
            Assert.Equal(17, row.SheetQuantity);

            row.UseOptimizationFeed = false;

            Assert.Equal(5, row.SheetQuantity);
            Assert.Equal(12.5m, row.OptimizationPercent);
        }

        [Fact]
        public void An_explicit_off_survives_every_later_refresh()
        {
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            row.SetOptimizationFeed(17, 82.77m, false, "from the run");

            Assert.True(row.TryEnableFeedAutomatically());

            // The operator decides they want their own number after all.
            row.UseOptimizationFeed = false;
            Assert.True(row.FeedSwitchTouchedByUser);

            // Feed refreshes run on every Step 5 edit and every ticked pane, so
            // the default must not keep putting the switch back.
            row.SetOptimizationFeed(19, 85m, false, "a newer run");
            Assert.False(row.TryEnableFeedAutomatically());

            Assert.False(row.UseOptimizationFeed);
            Assert.Equal(5, row.SheetQuantity);
            Assert.Equal(12.5m, row.OptimizationPercent);
        }

        [Fact]
        public void An_automatic_enable_is_not_recorded_as_the_operators_choice()
        {
            var row = new WastageRow();

            Assert.False(row.FeedSwitchTouchedByUser);
            row.TryEnableFeedAutomatically();

            // Otherwise a row that merely defaulted on could never be refreshed
            // again after the operator turned it off and back on by hand.
            Assert.False(row.FeedSwitchTouchedByUser);
        }

        [Fact]
        public void Enabling_with_no_run_behind_it_keeps_the_manual_values()
        {
            var row = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };

            Assert.True(row.TryEnableFeedAutomatically());

            // ApplySheetSourceMode hands the operator's own numbers back rather
            // than showing a stale run figure with nothing behind it.
            Assert.Equal(5, row.SheetQuantity);
            Assert.Equal(12.5m, row.OptimizationPercent);
        }

        [Fact]
        public void A_carried_operator_choice_survives_a_rebuild()
        {
            // Step 6 rebuilds its rows on every Step 5 edit. The explicit choice
            // is carried across that rebuild in the row initializer, so it is
            // copied onto a fresh row exactly as the rebuild would.
            var previous = new WastageRow { SheetQuantity = 5 };
            previous.SetOptimizationFeed(17, 82.77m, false, "from the run");
            previous.TryEnableFeedAutomatically();
            previous.UseOptimizationFeed = false;

            var rebuilt = new WastageRow
            {
                SheetQuantity = previous.SheetQuantity,
                FeedSwitchTouchedByUser = previous.FeedSwitchTouchedByUser,
                UseOptimizationFeed = previous.UseOptimizationFeed
            };

            Assert.True(rebuilt.FeedSwitchTouchedByUser);
            Assert.False(rebuilt.UseOptimizationFeed);
            Assert.False(rebuilt.TryEnableFeedAutomatically());
            Assert.Equal(5, rebuilt.SheetQuantity);
        }

        [Fact]
        public void A_rebuild_does_not_promote_the_runs_number_to_the_operators_own()
        {
            // Operator types 5; the run resolves 17 and the row goes onto the feed.
            var previous = new WastageRow { SheetQuantity = 5, OptimizationPercent = 12.5m };
            previous.SetOptimizationFeed(17, 82.77m, false, "from the run");
            previous.TryEnableFeedAutomatically();
            Assert.Equal(17, previous.SheetQuantity);

            // Step 5 edit → Step 6 rebuilds its rows. SheetQuantity and
            // OptimizationPercent now hold the RUN's figures, so copying those
            // properties alone parks the run's number as the operator's own.
            var rebuilt = new WastageRow
            {
                SheetQuantity = previous.SheetQuantity,
                OptimizationPercent = previous.OptimizationPercent,
                FeedSwitchTouchedByUser = previous.FeedSwitchTouchedByUser,
                UseOptimizationFeed = previous.UseOptimizationFeed
            };
            rebuilt.CarryManualSnapshotFrom(previous);

            rebuilt.UseOptimizationFeed = false;

            Assert.Equal(5, rebuilt.SheetQuantity);
            Assert.Equal(12.5m, rebuilt.OptimizationPercent);
        }

        [Fact]
        public void Carrying_the_parked_pair_from_nothing_is_harmless()
        {
            // The first build of a row has no predecessor to carry from.
            var row = new WastageRow { SheetQuantity = 9 };

            row.CarryManualSnapshotFrom(null);

            Assert.Equal(9, row.SheetQuantity);
            Assert.True(row.IsManualSheetSourceMode);
        }

        // ── 4. wiring that needs a running application ─────────────────────

        [Fact]
        public void Step5_top_panel_binds_both_run_buttons()
        {
            string xaml = Read(LocateProjectRoot(),
                "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml");

            Assert.Contains("Command=\"{Binding RunQuickOptimizationCommand}\"", xaml);
            Assert.Contains("Command=\"{Binding RunOptimizeOptimizationCommand}\"", xaml);

            // Localized like everything else in that panel — the module's own
            // buttons are hardcoded English, but this panel is not.
            Assert.Contains("Content=\"{DynamicResource Dg.RunQuick}\"", xaml);
            Assert.Contains("Content=\"{DynamicResource Dg.RunOptimize}\"", xaml);
        }

        [Fact]
        public void The_buttons_sit_before_the_spec_group_in_the_top_panel()
        {
            string xaml = Read(LocateProjectRoot(),
                "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml");

            int quick = xaml.IndexOf("RunQuickOptimizationCommand", StringComparison.Ordinal);
            int specs = xaml.IndexOf("Dg.Specs", StringComparison.Ordinal);

            Assert.True(quick >= 0 && specs > quick,
                "QUICK / OPTIMIZE belong on the right of the top panel, before the Specs group");
        }

        [Fact]
        public void The_composer_binds_the_runner_and_scopes_the_feed_to_this_document()
        {
            string composer = Read(LocateProjectRoot(),
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs");

            Assert.Contains("Step5VM.BindOptimizationRunner(RunOptimizationFromStep5);", composer);

            // Step 6 must see an empty feed rather than another quote's run.
            Assert.Contains("OptimizationResultFeed.IsForDocument(CurrentDocumentKey)", composer);

            // ...and the run read back from disk must be this quote's own.
            Assert.Contains("OptimizationResultFeed.RefreshAsync(CurrentDocumentKey)", composer);

            // The stamp has to be set before the run, not after: both the live
            // publish and the persisted row read it when the run finishes.
            Assert.Contains("host.DocumentKey = CurrentDocumentKey;", composer);
        }

        [Fact]
        public void Step6_enables_a_row_only_after_the_run_has_resolved_it()
        {
            string vm = Read(LocateProjectRoot(),
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs");

            int resolve = vm.IndexOf("row.SetOptimizationFeed(sheets, utilization, shared, label);",
                StringComparison.Ordinal);
            int enable = vm.IndexOf("row.TryEnableFeedAutomatically();", StringComparison.Ordinal);

            Assert.True(resolve >= 0, "the resolve call moved or was renamed");
            Assert.True(enable > resolve,
                "enable must come after the row's figures are set, because enabling parks them");
        }

        [Fact]
        public void The_rebuild_restores_the_sheet_quantity_before_it_restores_the_switch()
        {
            // ORDER IS LOAD-BEARING. Turning the feed on parks whatever
            // SheetQuantity currently holds, so a rebuilt row must have its saved
            // quantity in place first — otherwise the first switch back to manual
            // restores the initializer's 1 over a number the operator saved.
            string vm = Read(LocateProjectRoot(),
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs");

            int qty = vm.IndexOf("SheetQuantity = previousRow?.SheetQuantity ?? 1,",
                StringComparison.Ordinal);
            int carried = vm.IndexOf("FeedSwitchTouchedByUser = previousRow?.FeedSwitchTouchedByUser ?? false,",
                StringComparison.Ordinal);
            int sw = vm.IndexOf("UseOptimizationFeed = previousRow?.UseOptimizationFeed ?? false",
                StringComparison.Ordinal);

            Assert.True(qty >= 0 && carried > qty && sw > carried,
                "expected SheetQuantity, then FeedSwitchTouchedByUser, then UseOptimizationFeed");

            // ...and the parked pair must be carried last, because the
            // UseOptimizationFeed assignment above re-parks whatever the
            // initializer handed it.
            int carry = vm.IndexOf("row.CarryManualSnapshotFrom(previousRow);",
                StringComparison.Ordinal);

            Assert.True(carry > sw,
                "CarryManualSnapshotFrom must run after the row initializer");
        }

        [Fact]
        public void A_Step5_run_tags_its_racks_with_the_pane_identity_Step6_joins_on()
        {
            string live = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.LiveRun.cs");

            // The join is structural: spec number + side. A rack without them
            // can never be matched, which is the original "feed does nothing"
            // failure mode.
            Assert.Contains("SpecNumber = specNumber", live);
            Assert.Contains("Side = side", live);
            Assert.Contains("group.GroupNumber, \"Outer\")", live);
            Assert.Contains("group.GroupNumber, \"Inner\")", live);

            // W1/H1 → outer rack, W2/H2 → inner rack, matching the routing the
            // rest of the chain already agrees on.
            Assert.Contains("ComposeForSlot(PaneSlot.Pane1)", live);
            Assert.Contains("ComposeForSlot(PaneSlot.Pane2)", live);

            // Racks are usage-tracked, never capped: an empty AvailableText is
            // what makes the pool behave like the Job Order path's.
            Assert.Contains("IsUsedInRun = true", live);
        }

        [Fact]
        public void The_two_Step5_buttons_run_at_fixed_effort_levels()
        {
            string live = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.LiveRun.cs");

            // Step 5 has no SETTINGS tab, so the depth rides on the button.
            Assert.Contains("private const string Step5QuickEffort = \"1\";", live);
            Assert.Contains("private const string Step5OptimizeEffort = \"5\";", live);
        }

        [Fact]
        public void A_Step5_run_is_published_with_the_document_it_belongs_to()
        {
            string feed = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.Feed.cs");
            string session = Read(LocateProjectRoot(),
                "ViewModels/Optimization/OptimizationViewModel.Session.cs");

            Assert.Contains("DocumentKey);", feed);
            Assert.Contains("await _sessions.UpsertLatestRunAsync(snapshot, DocumentKey);", session);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null &&
                !File.Exists(Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static string Read(string root, string relative) =>
            File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
    }
}
