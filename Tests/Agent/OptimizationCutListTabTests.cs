// ═══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/OptimizationCutListTabTests.cs
//  PROJECT: ProGlassAutomation.Tests
//
//  Covers the round that (a) gave Step 5 a visible run state, (b) flattened
//  the Step 5 rows and un-clipped three buttons, (c) aligned the Step 6
//  tables, and (d) added the standalone CUT LIST tab to the optimization
//  module.
//
//  Two bug classes here are worth locking down harder than the features
//  themselves, because both are invisible to a compiler and both had already
//  shipped:
//
//    CLIPPING. A button style's natural height/min-width is designed around
//    its template padding. Overriding Height or MinWidth locally shrinks the
//    box but not the padding, so the label is clipped — and only in the
//    languages whose word is longer than English. "Add" fits in 40px;
//    "Добавить" does not.
//
//    DRIFT. These tables are hand-built Grids, so a column header stays over
//    its own column only while two separate ColumnDefinition lists agree.
//    Nothing enforces that, which is exactly how Step 6's headers came to sit
//    off their numbers. The structural tests below parse the XAML and compare
//    the lists instead of trusting anyone to keep them in step.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationCutListTabTests
    {
        private const string Step5View = "Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml";
        private const string CutListView = "Views/Optimization/Tabs/CutListTabView.xaml";
        private const string SizesView = "Views/Optimization/Tabs/SizesTabView.xaml";
        private const string OptShell = "Views/Optimization/OptimizationView.xaml";
        private const string ChargesView = "Views/ProformaInvoice/Step6OtherCharges/OtherChargesView.xaml";
        private const string WastagesView = "Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml";
        private const string CustomChargesView = "Views/ProformaInvoice/Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml";

        // ── 1. Step 5 can now show that a run is happening ─────────────────

        [Fact]
        public void The_mirrored_progress_is_clamped_to_a_percentage()
        {
            var grid = new DataGridViewModel();

            grid.ReportOptimizationProgress(true, 137, "packing");
            Assert.Equal(100, grid.OptimizationProgressPercent);

            grid.ReportOptimizationProgress(true, -4, "packing");
            Assert.Equal(0, grid.OptimizationProgressPercent);

            grid.ReportOptimizationProgress(true, 42, "packing");
            Assert.Equal(42, grid.OptimizationProgressPercent);
        }

        [Fact]
        public void A_missing_progress_detail_becomes_empty_rather_than_null()
        {
            var grid = new DataGridViewModel();

            grid.ReportOptimizationProgress(true, 10, null);

            Assert.Equal(string.Empty, grid.OptimizationProgressDetail);
            Assert.True(grid.IsOptimizationRunning);
        }

        [Fact]
        public void The_run_buttons_are_disabled_while_a_run_is_in_flight()
        {
            var grid = new DataGridViewModel();
            grid.BindOptimizationRunner((_, _) => (true, "started"));

            Assert.True(grid.CanRunOptimization);

            // A second run stacked on the first would rebuild the piece list
            // underneath it, so the buttons have to go dead, not just report.
            grid.ReportOptimizationProgress(true, 5, "group 1 of 4");
            Assert.False(grid.CanRunOptimization);

            grid.ReportOptimizationProgress(false, 100, "done");
            Assert.True(grid.CanRunOptimization);
        }

        [Fact]
        public void A_grid_with_no_optimization_behind_it_never_offers_a_run()
        {
            var grid = new DataGridViewModel();

            Assert.False(grid.CanRunOptimization);
            Assert.False(grid.RunQuickOptimizationCommand.CanExecute(null));
            Assert.False(grid.RunOptimizeOptimizationCommand.CanExecute(null));
        }

        [Fact]
        public void The_loading_bar_sits_before_the_quick_button()
        {
            string xaml = Read(Step5View);

            int bar = xaml.IndexOf("OptimizationProgressPercent", StringComparison.Ordinal);
            int quick = xaml.IndexOf("RunQuickOptimizationCommand", StringComparison.Ordinal);

            Assert.True(bar >= 0, "the progress bar binding is gone");
            Assert.True(bar < quick, "the bar belongs before QUICK, as asked for");

            // It must collapse when idle, or the top panel grows a permanent
            // hole where the bar was.
            Assert.Contains("Visibility=\"{Binding IsOptimizationRunning, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
            Assert.Contains("{Binding OptimizationProgressDetail, Mode=OneWay}", xaml);
        }

        // ── 2. Step 5 rows are a grid, not a strip of boxes ────────────────

        [Fact]
        public void Step5_row_cells_are_flat_spreadsheet_cells()
        {
            string xaml = Read(Step5View);
            string row = SliceBetween(xaml, "<ListView.ItemTemplate>", "</ListView.ItemTemplate>");

            // The flat cell styles are transparent and borderless until hovered
            // or focused; Input.TextBox paints a filled, bordered box per cell,
            // which is what made the rows look crowded.
            Assert.Contains("LineItemsTextCellStyle", row);
            Assert.Contains("LineItemsIntCellStyle", row);
            Assert.Contains("LineItemsDecimalCellStyle", row);
            Assert.DoesNotContain("Input.TextBox", row);
        }

        [Fact]
        public void The_flat_cell_styles_really_are_borderless_and_themed()
        {
            // Guarding the assumption the swap rests on. If someone later gives
            // LineItemsIntCellStyle a background and a border, Step 5 goes back
            // to boxes without anyone touching Step 5.
            string styles = Read("Themes/Styles/DataGridStyles.xaml");
            string cell = SliceBetween(styles,
                "x:Key=\"LineItemsIntCellStyle\"", "</Style>");

            Assert.Contains("Property=\"Background\" Value=\"Transparent\"", cell);
            Assert.Contains("Property=\"BorderThickness\" Value=\"0\"", cell);
            // No Template would mean WPF's default chrome and system colours —
            // the exact defect documented in GlobalDesignSystem.xaml.
            Assert.Contains("Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\"", cell);
            Assert.Contains("<ControlTemplate TargetType=\"TextBox\">", cell);
            Assert.Contains("PART_ContentHost", cell);
        }

        // ── 3. buttons no longer clip their own labels ─────────────────────

        [Fact]
        public void No_button_overrides_a_style_height_small_enough_to_clip_its_label()
        {
            // PiGhostChipButton is 34 tall with template padding 10,4 and
            // PiFooterPrimaryButton is 40 tall with padding 16,0. Shrinking
            // either locally leaves the label nowhere to go.
            string step5 = Read(Step5View);
            Assert.DoesNotContain("Height=\"24\"", step5);

            string custom = Read(CustomChargesView);
            Assert.DoesNotContain("MinWidth=\"72\"", custom);

            // The ADD button now sizes the way PrintPreviewView sizes the same
            // style — no local height or width at all.
            string addButton = SliceBetween(custom,
                "Command=\"{Binding AddChargeCommand}\"", "/>");
            Assert.DoesNotContain("Height=", addButton);
            Assert.DoesNotContain("MinWidth=", addButton);
        }

        [Fact]
        public void The_clipped_label_is_long_in_most_of_the_languages_this_app_ships()
        {
            // The reason the 72px minimum was a real bug and not a nitpick:
            // "Add" fits the ~40px left after template padding, "Добавить"
            // does not. If a translation ever shortens, this test says so.
            string russian = Read("Themes/Languages/ru-RU.xaml");
            string add = Value(russian, "Action.Add");

            Assert.True(add.Length > 3,
                $"expected a long Russian label for Action.Add, got '{add}'");
        }

        // ── 4. Step 6 headers sit over their own columns ───────────────────

        [Fact]
        public void Step6_rows_share_their_headers_horizontal_inset()
        {
            // PiTableHeaderStyle pads 10,6. The data rows padded 8, so every
            // column sat 2px off its label.
            Assert.Contains("Padding=\"10,8\">", Read(ChargesView));
            Assert.Contains("Padding=\"10,6\">", Read(WastagesView));

            Assert.DoesNotContain("Padding=\"8,8\">", Read(ChargesView));
            Assert.DoesNotContain("Padding=\"8,6\">", Read(WastagesView));
        }

        [Fact]
        public void Step6_header_labels_carry_the_alignment_of_the_data_under_them()
        {
            string charges = Read(ChargesView);
            Assert.Contains("Text=\"{DynamicResource ProformaInvoice.ON}\" HorizontalAlignment=\"Center\"", charges);
            Assert.Contains("Text=\"QTY\" HorizontalAlignment=\"Center\"", charges);
            Assert.Contains("Text=\"{DynamicResource Common.Rate}\" HorizontalAlignment=\"Center\"", charges);

            string wastages = Read(WastagesView);
            Assert.Contains("Text=\"{DynamicResource ProformaInvoice.ON}\" HorizontalAlignment=\"Center\"", wastages);
            Assert.Contains("Text=\"SQM\" HorizontalAlignment=\"Center\"", wastages);
            Assert.Contains("Text=\"{DynamicResource Common.Rate}\" HorizontalAlignment=\"Center\"", wastages);
        }

        [Fact]
        public void The_result_column_is_right_aligned_on_both_sides_and_off_the_edge()
        {
            string charges = Read(ChargesView);

            // Header and value must agree, and both need the same inset or the
            // label ends flush against the table edge.
            Assert.Contains(
                "Text=\"{DynamicResource ProformaInvoice.Result}\" HorizontalAlignment=\"Right\" Margin=\"0,0,6,0\"",
                charges);
            string resultCell = SliceBetween(charges,
                "Text=\"{Binding FormulaDisplay}\"", "VerticalAlignment=\"Center\">");
            Assert.Contains("Margin=\"0,0,6,0\"", resultCell);
            Assert.Contains("TextAlignment=\"Right\"", resultCell);
        }

        [Fact]
        public void The_wastage_rate_box_centres_its_digits_under_its_label()
        {
            string wastages = Read(WastagesView);
            string rateCell = SliceBetween(wastages,
                "<TextBox Grid.Column=\"4\"", "/>");

            Assert.Contains("HorizontalContentAlignment=\"Center\"", rateCell);
        }

        // ── 5. the standalone CUT LIST tab ─────────────────────────────────

        [Fact]
        public void The_cut_list_index_is_last_so_no_existing_tab_had_to_move()
        {
            // Shown second on the rail, indexed last. Inserting it at 2 would
            // have renumbered five tabs across seven XAML triggers and three C#
            // jump sites — including "open the report after a run".
            Assert.Equal(7, OptimizationViewModel.TabIndexCutList);

            Assert.Equal(0, OptimizationViewModel.TabIndexJobs);
            Assert.Equal(1, OptimizationViewModel.TabIndexSizes);
            Assert.Equal(2, OptimizationViewModel.TabIndexStocks);
            Assert.Equal(3, OptimizationViewModel.TabIndexSettings);
            Assert.Equal(4, OptimizationViewModel.TabIndexSummary);
            Assert.Equal(5, OptimizationViewModel.TabIndexTwoDView);
            Assert.Equal(6, OptimizationViewModel.TabIndexReport);
        }

        [Fact]
        public void No_tab_jump_in_the_view_model_still_hardcodes_a_number()
        {
            foreach (string file in Directory.EnumerateFiles(
                Path.Combine(Root(), "ViewModels", "Optimization"), "*.cs"))
            {
                string text = File.ReadAllText(file);
                MatchCollection hits = Regex.Matches(text, @"SelectedTabIndex\s*=\s*\d");
                Assert.True(hits.Count == 0,
                    $"{Path.GetFileName(file)} still jumps to a tab by magic number");
            }
        }

        [Fact]
        public void The_cut_list_tab_is_on_the_rail_and_in_the_tab_host()
        {
            string shell = Read(OptShell);

            Assert.Contains("Content=\"CUT LIST\"", shell);
            Assert.Contains("ConverterParameter=7", shell);
            Assert.Contains("<tabs:CutListTabView/>", shell);

            string tab = Read(CutListView);
            Assert.Contains("<DataTrigger Binding=\"{Binding SelectedTabIndex}\" Value=\"7\">", tab);
        }

        [Fact]
        public void The_cut_list_tab_is_displayed_straight_after_sizes()
        {
            string shell = Read(OptShell);

            int sizes = shell.IndexOf("Content=\"SIZES\"", StringComparison.Ordinal);
            int cutList = shell.IndexOf("Content=\"CUT LIST\"", StringComparison.Ordinal);
            int stocks = shell.IndexOf("Content=\"STOCKS\"", StringComparison.Ordinal);

            Assert.True(sizes >= 0 && cutList > sizes && stocks > cutList,
                "CUT LIST belongs on the rail between SIZES and STOCKS");
        }

        [Fact]
        public void The_sizes_tab_offers_a_way_straight_to_manual_entry()
        {
            string sizes = Read(SizesView);

            Assert.Contains("Command=\"{Binding OpenCutListTabCommand}\"", sizes);
            Assert.Contains("Content=\"MANUAL CUT LIST\"", sizes);
        }

        [Fact]
        public void The_cut_list_grid_declares_its_columns_identically_three_times()
        {
            // Header, row template and autosum footer are separate Grids. They
            // stay aligned only while all three lists agree — nothing else
            // enforces it, and this is precisely how Step 6 drifted.
            string tab = Read(CutListView);
            // The tab has other Grids too (title bar, toolbar); the three that
            // must agree are the 10-column header, row template and footer.
            List<List<string>> tables = ColumnBlocks(tab)
                .Where(b => b.Count == 10)
                .ToList();

            Assert.Equal(3, tables.Count);
            Assert.All(tables, b => Assert.Equal(tables[0], b));
        }

        [Fact]
        public void The_cut_list_grid_is_paste_enabled_through_the_hosted_spec_group()
        {
            string tab = Read(CutListView);

            // DataGridNavigationBehavior walks up looking for a DataContext that
            // is an IExcelPasteable. The hosted SpecGroup is one; the
            // OptimizationViewModel is not — so this assignment is what makes
            // Excel paste work here at all.
            Assert.Contains("DataContext=\"{Binding ManualCutList}\"", tab);
            Assert.Contains("behaviors:DataGridNavigationBehavior.EnableExcelNav=\"True\"", tab);
            Assert.Contains("ItemsSource=\"{Binding ItemRows}\"", tab);
        }

        [Fact]
        public void The_cut_list_shows_the_requested_columns_and_their_autosums()
        {
            string tab = Read(CutListView);

            foreach (string column in new[] { "Dg.Hash", "Dg.GlassRef", "Dg.W1", "Dg.H1", "Dg.W2", "Dg.H2", "Dg.Qty" })
            {
                Assert.Contains("Text=\"{DynamicResource " + column + "}\"", tab);
            }

            Assert.Contains("Text=\"SQM 1\"", tab);
            Assert.Contains("Text=\"SQM 2\"", tab);

            Assert.Contains("{Binding ManualCutList.FooterTotalQty, Mode=OneWay}", tab);
            Assert.Contains("{Binding ManualCutList.FooterTotalSqm,", tab);
            Assert.Contains("{Binding ManualCutList.FooterTotalSqm2,", tab);
        }

        // ── 6. the grid the tab hosts really does the arithmetic ───────────

        [Fact]
        public void The_manual_cut_list_starts_on_a_real_build_type()
        {
            // OptimizationViewModel cannot be built in a test — its constructor
            // starts live database loads — so this pins the creation policy in
            // source. The hosted field begins empty, and on a tab whose whole
            // purpose is "start here with nothing" a blank GLASS TYPE dropdown
            // reads as broken rather than as a choice not yet made. The type
            // also drives the perimeter factor GENERATE applies to new rows.
            string vm = Read("ViewModels/Optimization/OptimizationViewModel.CutList.cs");

            Assert.Contains("string.IsNullOrWhiteSpace(group.SelectedGlassType)", vm);
            Assert.Contains("group.SelectedGlassType = CutListGlassTypeOptions[0];", vm);
            Assert.Contains("\"SGU\"", vm);
        }

        [Fact]
        public void A_manual_row_computes_both_pane_areas_and_the_autosums()
        {
            var group = new SpecGroup();

            group.ItemRows.Add(new GlassItemRow
            {
                GlassRef = "G1", W1 = 1000, H1 = 2000, W2 = 900, H2 = 1900, Qty = 3
            });
            group.ItemRows.Add(new GlassItemRow
            {
                GlassRef = "G2", W1 = 500, H1 = 500, Qty = 2
            });

            // Per-row: pane 1 area × qty, and pane 2 area × qty.
            decimal row1Sqm1 = Math.Round(SqmCalculator.CalculatePanel(1000, 2000) * 3, 3);
            decimal row1Sqm2 = Math.Round(SqmCalculator.CalculatePanel(900, 1900) * 3, 3);
            Assert.Equal(row1Sqm1, group.ItemRows[0].TotalSqm);
            Assert.Equal(row1Sqm2, group.ItemRows[0].TotalSqm2);

            // A single-glazed row has no second pane at all.
            Assert.Equal(0m, group.ItemRows[1].TotalSqm2);

            // The autosums the tab footer displays.
            Assert.Equal(5, group.FooterTotalQty);
            Assert.Equal(
                Math.Round(row1Sqm1 + Math.Round(SqmCalculator.CalculatePanel(500, 500) * 2, 3), 3),
                group.FooterTotalSqm);
            Assert.Equal(Math.Round(row1Sqm2, 3), group.FooterTotalSqm2);
        }

        [Fact]
        public void A_row_with_no_sizes_adds_no_glass_to_the_autosums()
        {
            var group = new SpecGroup();

            group.ItemRows.Add(new GlassItemRow { GlassRef = "G1", W1 = 1000, H1 = 1000, Qty = 4 });
            group.ItemRows.Add(new GlassItemRow());   // the blank row GENERATE leaves behind

            // A blank row carries Step 5's default quantity of 1 — that is the
            // existing behaviour of the grid this tab hosts, and it is what
            // makes a freshly generated row ready to type into. What must not
            // happen is it inventing area: no sizes means no glass.
            Assert.Equal(1, group.ItemRows[1].Qty);
            Assert.Equal(0m, group.ItemRows[1].TotalSqm);
            Assert.Equal(0m, group.ItemRows[1].TotalSqm2);

            Assert.Equal(5, group.FooterTotalQty);
            Assert.Equal(Math.Round(SqmCalculator.CalculatePanel(1000, 1000) * 4, 3), group.FooterTotalSqm);
            Assert.Equal(0m, group.FooterTotalSqm2);
        }

        [Fact]
        public void Rows_added_in_bulk_are_numbered_for_the_sr_no_column()
        {
            var group = new SpecGroup();

            group.AddRows(3);

            Assert.Equal(3, group.ItemRows.Count);

            // AddRows creates every row with SerialNumber 0 and renumbers in a
            // finally block. What the SR NO column needs is: no duplicates, in
            // order, and the renumbering actually ran.
            int[] serials = group.ItemRows.Select(r => r.SerialNumber).ToArray();
            Assert.Equal(3, serials.Distinct().Count());
            Assert.Equal(serials.OrderBy(s => s).ToArray(), serials);
            Assert.DoesNotContain(0, serials);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Views", "Optimization")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)))
                .Replace("\r\n", "\n");

        private static string SliceBetween(string text, string startMarker, string endMarker)
        {
            int start = text.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"marker not found: {startMarker}");
            int end = text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert.True(end > start, $"end marker not found after: {startMarker}");
            return text.Substring(start, end - start + endMarker.Length);
        }

        private static string Value(string xml, string key)
        {
            Match m = Regex.Match(xml,
                "x:Key=\"" + Regex.Escape(key) + "\"[^>]*>([^<]*)<");
            Assert.True(m.Success, $"key not found: {key}");
            return m.Groups[1].Value;
        }

        private static List<List<string>> ColumnBlocks(string xaml) =>
            Regex.Matches(xaml,
                    "<Grid\\.ColumnDefinitions>(.*?)</Grid\\.ColumnDefinitions>",
                    RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m => Regex.Matches(m.Groups[1].Value, "Width=\"([^\"]+)\"")
                    .Cast<Match>()
                    .Select(w => w.Groups[1].Value)
                    .ToList())
                .Where(list => list.Count > 0)
                .ToList();
    }
}
