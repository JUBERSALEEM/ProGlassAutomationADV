// ══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/OptimizationCutListWorkspaceTests.cs
//
//  Covers the round that turned the CUT LIST tab into a workspace:
//    · Step 5's VIEW RESULT button, and the chain that carries a proforma's
//      run into the optimization module's SUMMARY tab.
//    · The cut list's inner panes — SPECIFICATIONS, CUT LIST, RESULT — and
//      the jumps to the module's own tabs.
//    · Saving a specification to the shared catalogue.
//    · The per-row read-back: QTY USED and OPT %, matched by pane size.
//
//  WHAT IS TESTED BEHAVIOURALLY, AND WHY THE REST IS NOT
//  OptimizationViewModel cannot be constructed in a test — its constructor
//  starts live database loads — so anything that lives only on that instance
//  is pinned in source instead. Two things were deliberately shaped to stay
//  testable for real: the size matcher is a static pure function
//  (StampRows), and the VIEW RESULT button lives on DataGridViewModel, which
//  has a parameterless constructor. Those are the parts with actual logic in
//  them, and they are asserted by running them.
//
//  There is no dotnet SDK in this environment, so these tests are the build
//  verdict on the developer's machine, not here.
// ══════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationCutListWorkspaceTests
    {
        // ── helpers ────────────────────────────────────────────────────────
        private const string CutListView = "Views/Optimization/Tabs/CutListTabView.xaml";
        private const string Step5View = "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml";
        private const string CutListVm = "ViewModels/Optimization/OptimizationViewModel.CutList.cs";
        private const string SessionVm = "ViewModels/Optimization/OptimizationViewModel.Session.cs";
        private const string RunVm = "ViewModels/Optimization/OptimizationViewModel.Run.cs";
        private const string GridVm = "ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.Optimization.cs";
        private const string PiVm = "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs";
        private const string ShellVm = "ViewModels/MainViewModel.Part1.cs";

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

        private static OptimizationViewModel.CostingRowVm Result(
            int w, int h, int pieces, decimal glass, decimal stock) =>
            new OptimizationViewModel.CostingRowVm
            {
                WidthMm = w,
                HeightMm = h,
                PieceCount = pieces,
                GlassM2 = glass,
                StockM2 = stock
            };

        // ══ VIEW RESULT: the button on Step 5 ═════════════════════════════

        [Fact]
        public void View_result_needs_an_optimization_module_behind_the_grid()
        {
            var grid = new DataGridViewModel();

            // No runner bound — this grid is hosted somewhere with no
            // optimization module behind it, so there is no result to view.
            Assert.False(grid.ViewOptimizationResultCommand.CanExecute(null));

            grid.BindOptimizationRunner((_, _) => (true, "started"));

            Assert.True(grid.ViewOptimizationResultCommand.CanExecute(null));
        }

        [Fact]
        public void View_result_is_available_before_a_run_has_finished_this_session()
        {
            var grid = new DataGridViewModel();
            grid.BindOptimizationRunner((_, _) => (true, "started"));

            // Deliberately NOT gated on a run having finished since the app
            // started: a document optimised yesterday, or before a restart, has
            // a result worth looking at. When there genuinely is none, the
            // summary says so in words instead of the button being mysteriously
            // grey — a disabled button gives the operator nothing to act on.
            Assert.True(grid.ViewOptimizationResultCommand.CanExecute(null));
        }

        [Fact]
        public void Pressing_view_result_asks_the_composer_rather_than_the_module()
        {
            var grid = new DataGridViewModel();
            grid.BindOptimizationRunner((_, _) => (true, "started"));

            int raised = 0;
            grid.ViewOptimizationResultRequested += (_, _) => raised++;

            grid.ViewOptimizationResultCommand.Execute(null);

            // The grid does not know what a module or a document is. It asks,
            // and the composer decides — the same late binding the runner uses,
            // which keeps this view model free of the optimization module.
            Assert.Equal(1, raised);
        }

        [Fact]
        public void The_step_5_button_is_translated_in_every_language_this_app_ships()
        {
            string step5 = Read(Step5View);
            Assert.Contains("Command=\"{Binding ViewOptimizationResultCommand}\"", step5);
            Assert.Contains("Content=\"{DynamicResource Dg.ViewResult}\"", step5);
            Assert.Contains("ToolTip=\"{DynamicResource Dg.ViewResultTip}\"", step5);

            string[] languages = Directory.GetFiles(
                Path.Combine(Root(), "Themes", "Languages"), "*.xaml");
            Assert.Equal(15, languages.Length);

            foreach (string file in languages)
            {
                string xaml = File.ReadAllText(file);
                string name = Path.GetFileName(file);
                Assert.True(xaml.Contains("x:Key=\"Dg.ViewResult\""), name + " is missing Dg.ViewResult");
                Assert.True(xaml.Contains("x:Key=\"Dg.ViewResultTip\""), name + " is missing Dg.ViewResultTip");
            }
        }

        [Fact]
        public void The_new_button_does_not_repeat_the_clipping_that_broke_step_6()
        {
            string step5 = Read(Step5View);
            string button = Slice(step5,
                "Command=\"{Binding ViewOptimizationResultCommand}\"", "/>");

            // Height matches its two neighbours, which is proven not to clip a
            // pill. MinWidth is what clipped Step 6's ADD button in 14 of 15
            // languages, so it must stay off.
            Assert.Contains("Height=\"32\"", button);
            Assert.DoesNotContain("MinWidth=", button);
        }

        // ══ VIEW RESULT: the chain into the module ════════════════════════

        [Fact]
        public void The_request_carries_the_document_the_run_belongs_to()
        {
            string pi = Read(PiVm);

            // A proforma's run is persisted against its document key. Without
            // the key the module would open the global row, which belongs to
            // whoever last optimized with no invoice open.
            Assert.Contains("NavigateToOptimizationSummaryRequested?.Invoke(CurrentDocumentKey)", pi);
            Assert.Contains("Step5VM.ViewOptimizationResultRequested += (_, _) =>", pi);
        }

        [Fact]
        public void The_shell_navigates_before_it_asks_for_the_run()
        {
            string shell = Read(ShellVm);
            string handler = Slice(shell,
                "ProformaInvoiceVM.NavigateToOptimizationSummaryRequested += documentKey =>", "};");

            int navigate = handler.IndexOf("Navigate(\"Optimization\")", StringComparison.Ordinal);
            int show = handler.IndexOf("ShowLatestRunAsync(documentKey)", StringComparison.Ordinal);

            // Navigate applies the license gate and creates the module VM, so
            // asking that VM for a run first would either be gated out or hit a
            // different instance than the one now on screen.
            Assert.True(navigate >= 0, "the handler must navigate to the module");
            Assert.True(show > navigate, "it must navigate before loading the run");
        }

        [Fact]
        public void Viewing_a_run_lands_on_summary_while_reopening_a_session_lands_on_report()
        {
            string session = Read(SessionVm);

            Assert.Contains("public async Task ShowLatestRunAsync(string? documentKey)", session);
            Assert.Contains("RestoreSessionCore(s, openToReport: false);", session);
            Assert.Contains("RestoreSessionCore(s, openToReport: true);", session);
            Assert.Contains("private void RestoreSessionCore(SavedOptimizationSession s, bool openToReport)", session);

            // One restore path, one landing rule. Run() already sends a re-run
            // to SUMMARY unless the report flag is set, so "view result" needs
            // no navigation of its own beyond the tab it asks for up front.
            string run = Read(RunVm);
            Assert.Contains("SelectedTabIndex = _openToReportAfterRun ? TabIndexReport : TabIndexSummary;", run);
        }

        [Fact]
        public void The_latest_run_is_read_directly_because_the_session_list_hides_it()
        {
            string session = Read(SessionVm);

            Assert.Contains("await _sessions.GetLatestRunAsync(documentKey);", session);

            // The reserved latest-run row is deliberately excluded from
            // SavedSessions — an operator never saved it. So "open the newest
            // listed session" would show an older, unrelated run and read as the
            // button not working. This pins the reason next to the code path
            // that avoids it.
            Assert.Contains("SavedOptimizationSession.IsReservedLatestRunId(s.Id)", session);
        }

        [Fact]
        public void A_run_in_flight_blocks_a_restore_that_would_swap_the_list_under_it()
        {
            string session = Read(SessionVm);
            string method = Slice(session,
                "public async Task ShowLatestRunAsync(string? documentKey)", "Could not open the latest run: ");

            int guard = method.IndexOf("if (IsRunning)", StringComparison.Ordinal);
            int restore = method.IndexOf("RestoreSessionCore(", StringComparison.Ordinal);

            // Restoring replaces Pieces and Stocks. Doing that underneath a live
            // run would swap the list mid-flight.
            Assert.True(guard >= 0, "ShowLatestRunAsync must guard on IsRunning");
            Assert.True(guard < restore, "and it must guard before restoring");
        }

        [Fact]
        public void The_summary_tab_is_shown_even_when_there_is_nothing_to_show()
        {
            string session = Read(SessionVm);
            string method = Slice(session,
                "public async Task ShowLatestRunAsync(string? documentKey)", "if (IsRunning)");

            // Set before the guards: a run that cannot start leaves Run() early
            // without touching the tab, and the operator still asked to see
            // SUMMARY — with the reason written on the status line.
            Assert.Contains("SelectedTabIndex = TabIndexSummary;", method);
        }

        // ══ per-row read-back: QTY USED and OPT % ═════════════════════════

        [Fact]
        public void A_row_is_stamped_from_the_result_that_matches_its_size()
        {
            var rows = new List<GlassItemRow>
            {
                new GlassItemRow { GlassRef = "G1", W1 = 900, H1 = 1900, Qty = 6 }
            };

            int stamped = OptimizationViewModel.StampRows(rows, new[]
            {
                Result(900, 1900, pieces: 12, glass: 20m, stock: 24m)
            });

            Assert.Equal(1, stamped);
            Assert.True(rows[0].HasOptimizationResult);
            Assert.Equal(12, rows[0].QtyUsed);
            Assert.Equal(83.33m, rows[0].OptPercent);
        }

        [Fact]
        public void A_rotated_placement_still_matches_its_row()
        {
            var rows = new List<GlassItemRow>
            {
                new GlassItemRow { GlassRef = "G1", W1 = 900, H1 = 1900, Qty = 6 }
            };

            // The engine rotates pieces when that packs better and reports the
            // result in the orientation it chose, so 900×1900 comes back as
            // 1900×900 and is still the same glass.
            int stamped = OptimizationViewModel.StampRows(rows, new[]
            {
                Result(1900, 900, pieces: 12, glass: 20m, stock: 24m)
            });

            Assert.Equal(1, stamped);
            Assert.Equal(12, rows[0].QtyUsed);
        }

        [Fact]
        public void Both_panes_of_a_row_count_and_the_percentage_is_area_weighted()
        {
            var rows = new List<GlassItemRow>
            {
                new GlassItemRow { GlassRef = "DGU", W1 = 1000, H1 = 1000, W2 = 500, H2 = 500, Qty = 4 }
            };

            OptimizationViewModel.StampRows(rows, new[]
            {
                Result(1000, 1000, pieces: 4, glass: 4m, stock: 5m),   // 80%
                Result(500, 500, pieces: 4, glass: 1m, stock: 4m)      // 25%
            });

            Assert.Equal(8, rows[0].QtyUsed);

            // One number for the row, so it is the row's own yield: glass over
            // stock across both panes = 5/9. Averaging the two percentages would
            // give 52.5, weighting a small inner pane the same as a large outer.
            Assert.Equal(55.56m, rows[0].OptPercent);
        }

        [Fact]
        public void Zero_placed_and_no_run_yet_are_different_answers()
        {
            var placed = new GlassItemRow { GlassRef = "too big", W1 = 3000, H1 = 3000, Qty = 1 };
            var unmatched = new GlassItemRow { GlassRef = "other", W1 = 123, H2 = 456, H1 = 456, Qty = 1 };

            OptimizationViewModel.StampRows(new[] { placed, unmatched }, new[]
            {
                Result(3000, 3000, pieces: 0, glass: 0m, stock: 5m)
            });

            // "0 placed" is a real answer — the size did not fit. "—" means no
            // run has happened. Conflating them would read as a failure before
            // anything was ever optimized, which is why the flag exists.
            Assert.True(placed.HasOptimizationResult);
            Assert.Equal(0, placed.QtyUsed);

            Assert.False(unmatched.HasOptimizationResult);
            Assert.Equal(0, unmatched.QtyUsed);
        }

        [Fact]
        public void Results_for_the_same_size_in_two_groups_are_added_together()
        {
            var rows = new List<GlassItemRow>
            {
                new GlassItemRow { GlassRef = "G1", W1 = 800, H1 = 1200, Qty = 10 }
            };

            OptimizationViewModel.StampRows(rows, new[]
            {
                Result(800, 1200, pieces: 6, glass: 3m, stock: 4m),
                Result(800, 1200, pieces: 4, glass: 2m, stock: 3m)
            });

            Assert.Equal(10, rows[0].QtyUsed);
            Assert.Equal(71.43m, rows[0].OptPercent);   // 5 / 7
        }

        [Fact]
        public void Stamping_with_nothing_to_stamp_is_a_no_op()
        {
            var rows = new List<GlassItemRow> { new GlassItemRow { W1 = 900, H1 = 1900 } };

            Assert.Equal(0, OptimizationViewModel.StampRows(rows, Array.Empty<OptimizationViewModel.CostingRowVm>()));
            Assert.Equal(0, OptimizationViewModel.StampRows(null, null));
            Assert.Equal(0, OptimizationViewModel.StampRows(rows, null));

            // Untouched, not zeroed into looking like an answer.
            Assert.False(rows[0].HasOptimizationResult);
        }

        [Fact]
        public void A_row_with_no_sizes_cannot_match_anything()
        {
            var rows = new List<GlassItemRow> { new GlassItemRow { GlassRef = "blank" } };

            int stamped = OptimizationViewModel.StampRows(rows, new[]
            {
                Result(900, 1900, pieces: 12, glass: 20m, stock: 24m)
            });

            Assert.Equal(0, stamped);
            Assert.False(rows[0].HasOptimizationResult);
        }

        [Fact]
        public void A_stale_stamp_can_be_dropped()
        {
            var row = new GlassItemRow { W1 = 900, H1 = 1900 };
            OptimizationViewModel.StampRows(new[] { row }, new[]
            {
                Result(900, 1900, pieces: 12, glass: 20m, stock: 24m)
            });
            Assert.True(row.HasOptimizationResult);

            row.ClearOptimizationStamp();

            Assert.False(row.HasOptimizationResult);
            Assert.Equal(0, row.QtyUsed);
            Assert.Equal(0m, row.OptPercent);
        }

        [Fact]
        public void Every_run_reads_itself_back_into_the_cut_list()
        {
            string run = Read(RunVm);
            string tail = Slice(run, "RememberRunForUpgrade(stage, totalSheets);", "_openToReportAfterRun = false;");

            // Stamped as part of the run, not on a second click, so the columns
            // are current the moment the layout lands. It is a no-op until the
            // tab has rows of its own.
            Assert.Contains("StampCutListResults();", tail);
        }

        // ══ the workspace: panes, columns, specification ══════════════════

        [Fact]
        public void The_pane_constants_are_what_the_rail_parameters_say()
        {
            Assert.Equal(0, OptimizationViewModel.CutListPaneSpecs);
            Assert.Equal(1, OptimizationViewModel.CutListPaneGrid);
            Assert.Equal(2, OptimizationViewModel.CutListPaneResult);

            string tab = Read(CutListView);
            foreach (int pane in new[] { 0, 1, 2 })
            {
                Assert.Contains(
                    "IsChecked=\"{Binding CutListPaneIndex, Converter={StaticResource IndexEqualsConverter}, ConverterParameter="
                    + pane + "}\"", tab);
                Assert.Contains(
                    "<DataTrigger Binding=\"{Binding CutListPaneIndex}\" Value=\"" + pane + "\">", tab);
            }
        }

        [Fact]
        public void The_module_tabs_are_offered_as_jumps_not_hosted_a_second_time()
        {
            string tab = Read(CutListView);

            foreach (string label in new[] { "STOCKS", "SETTINGS", "2D VIEW", "REPORT" })
            {
                Assert.Contains("Content=\"" + label + "\" Command=\"{Binding JumpToOptimizationTabCommand}\"", tab);
            }

            // Jump parameters are the top rail's own indexes.
            Assert.Contains("CommandParameter=\"2\"", tab);   // STOCKS
            Assert.Contains("CommandParameter=\"3\"", tab);   // SETTINGS
            Assert.Contains("CommandParameter=\"5\"", tab);   // 2D VIEW
            Assert.Contains("CommandParameter=\"6\"", tab);   // REPORT

            // And no existing tab view is instantiated inside this one, which
            // would mean two live copies of it over one view model.
            Assert.DoesNotContain("StocksTabView", tab);
            Assert.DoesNotContain("SettingsTabView", tab);
            Assert.DoesNotContain("SummaryTabView", tab);
        }

        [Fact]
        public void The_jump_command_refuses_an_index_no_tab_answers_to()
        {
            string vm = Read(CutListVm);
            string command = Slice(vm, "public ICommand JumpToOptimizationTabCommand", "});");

            Assert.Contains("if (index < TabIndexJobs || index > TabIndexCutList) return;", command);
        }

        [Fact]
        public void The_grid_reads_glass_then_colour_then_size_then_the_run()
        {
            string tab = Read(CutListView);
            string header = Slice(tab, "<!-- column header -->", "</Border>");

            var labels = Regex.Matches(header, "Text=\"([^\"]+)\"")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            // Shared columns keep Step 5's translated keys; the columns that are
            // new to this module are hardcoded English, which is the convention
            // every other optimization surface already follows.
            Assert.Equal(new[]
            {
                "{DynamicResource Dg.Hash}",
                "{DynamicResource Dg.GlassRef}",
                "COLOUR 1",
                "{DynamicResource Dg.W1}",
                "{DynamicResource Dg.H1}",
                "COLOUR 2",
                "{DynamicResource Dg.W2}",
                "{DynamicResource Dg.H2}",
                "{DynamicResource Dg.Qty}",
                "SQM 1",
                "SQM 2",
                "QTY USED",
                "OPT %"
            }, labels);
        }

        [Fact]
        public void Colour_is_read_from_the_specification_rather_than_typed_per_row()
        {
            string tab = Read(CutListView);
            string row = Slice(tab, "<ListView.ItemTemplate>", "</ListView.ItemTemplate>");

            Assert.Contains(
                "Text=\"{Binding DataContext.OuterGlassColor, RelativeSource={RelativeSource AncestorType=ListView}}\"", row);
            Assert.Contains(
                "Text=\"{Binding DataContext.InnerGlassColor, RelativeSource={RelativeSource AncestorType=ListView}}\"", row);

            // Read-only on purpose. In this data model colour belongs to the
            // specification, and the engine groups pieces by it — a list with a
            // colour per row is a list the engine cannot group.
            Assert.DoesNotContain("TextBox", Slice(row, "DataContext.OuterGlassColor", "/>"));
        }

        [Fact]
        public void The_specification_pane_saves_to_the_shared_catalogue()
        {
            string tab = Read(CutListView);
            Assert.Contains("Command=\"{Binding SaveCutListSpecCommand}\"", tab);
            Assert.Contains("Text=\"{Binding CutListSpecName,", tab);
            Assert.Contains("Text=\"{Binding CutListSpecPriceText,", tab);
            Assert.Contains("Text=\"{Binding CutListSpecThicknessText,", tab);
            Assert.Contains("IsEnabled=\"{Binding CanSaveCutListSpec}\"", tab);

            string vm = Read(CutListVm);
            string save = Slice(vm, "private async Task SaveCutListSpecAsync()", "IsSavingCutListSpec = false;");

            // The rich overload: colour, thickness and sheet size go with it, so
            // a manual job is reusable rather than throwaway.
            Assert.Contains("await repository.SaveOrUpdateAsync(", save);
            Assert.Contains("(ManualCutList.OuterGlassColor ?? string.Empty).Trim()", save);
            Assert.Contains("ManualCutList.OuterSheetWidthMm", save);

            // Saving is gated on proforma edit rights and on read-only mode, so a
            // refusal is an answer the operator must see, not a swallowed click.
            Assert.Contains("CutListStatus = \"Could not save the specification: \" + ex.Message;", save);
        }

        [Fact]
        public void A_saved_specification_is_read_back_into_the_grid()
        {
            string vm = Read(CutListVm);
            string save = Slice(vm, "private async Task SaveCutListSpecAsync()", "IsSavingCutListSpec = false;");

            Assert.Contains("ManualCutList.SelectedSpecName = saved?.SpecName ?? specName;", save);
            Assert.Contains("await ManualCutList.LoadSpecsAsync()", save);
        }

        [Fact]
        public void The_title_shows_the_composition_and_follows_the_groups_own_edits()
        {
            string tab = Read(CutListView);
            Assert.Contains("Text=\"{Binding CutListCompositionDisplay}\"", tab);

            string vm = Read(CutListVm);

            // Colour, glass type and specification are edited on the group, not
            // through this VM, so the summary has to listen to the group.
            Assert.Contains("group.PropertyChanged += (_, _) =>", vm);
            Assert.Contains("OnPropertyChanged(nameof(CutListCompositionDisplay));", vm);
        }

        [Fact]
        public void The_result_pane_says_so_when_nothing_has_run()
        {
            string tab = Read(CutListView);
            string pane = Slice(tab, "PANE 2: RESULT", "RE-READ RUN");

            Assert.Contains("No run yet.", pane);
            Assert.Contains("<DataTrigger Binding=\"{Binding HasResult}\" Value=\"False\">", pane);
            Assert.Contains("Visibility=\"{Binding HasResult, Converter={StaticResource BoolToVisibilityConverter}}\"", pane);
            Assert.Contains("Command=\"{Binding RefreshCutListResultsCommand}\"", pane);
        }

        [Fact]
        public void No_view_reaches_for_a_resource_that_does_not_exist()
        {
            // This view once bound IsEnabled through an "InverseBoolConverter".
            // The app has an inverse-bool-to-VISIBILITY converter and no
            // inverse-bool converter, and a missing StaticResource is a runtime
            // failure with no compile error and no test to catch it. So: every
            // key this view reaches for must be declared somewhere in the repo.
            string tab = Read(CutListView);
            var used = Regex.Matches(tab, @"\{StaticResource ([A-Za-z0-9_.]+)\}")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            Assert.NotEmpty(used);

            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(Root(), "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "x:Key=\"([^\"]+)\""))
                    declared.Add(m.Groups[1].Value);
            }

            var missing = used.Where(k => !declared.Contains(k)).ToList();
            Assert.True(missing.Count == 0,
                "StaticResource keys with no declaration anywhere: " + string.Join(", ", missing));
        }

        [Fact]
        public void The_paste_wiring_survived_the_restructure()
        {
            string tab = Read(CutListView);

            // The behaviour finds an IExcelPasteable by walking the visual tree
            // for a DataContext that is one, so the hosted SpecGroup has to BE
            // the ListView's DataContext. EnableExcelNav on its own does nothing.
            Assert.Contains("DataContext=\"{Binding ManualCutList}\"", tab);
            Assert.Contains("behaviors:DataGridNavigationBehavior.EnableExcelNav=\"True\"", tab);
            Assert.Contains("ItemsSource=\"{Binding ItemRows}\"", tab);

            // And the ListView settings that behaviour depends on.
            Assert.Contains("ScrollViewer.CanContentScroll=\"False\"", tab);
            Assert.Contains("<ItemsPresenter/>", tab);
        }
    }
}
