using System;
using System.IO;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    // ═══════════════════════════════════════════════════════════════════
    // FILE: Tests/Agent/SheetStoreGridGeometrySourceTests.cs
    // FIX-2026-09-05 geometry pins — Sheet Store list column spacing.
    //
    // The Sheet Store list is a hand-built grid (sticky header Grid +
    // ListBox row template Grid) whose two column tables are duplicated
    // in XAML. Three defects were fixed: a 2px header/row alignment
    // offset, ZERO inter-column gutters (every header text touched its
    // neighbour), and a Color column that clipped without trimming.
    //
    // These pins keep the geometry compositional-faithful:
    //   1. Header and row column tables are IDENTICAL (14 runs verified).
    //   2. Header padding = row composed padding (frame 1 + listbox 10 +
    //      selection frame 2 + row padding 12 = 25 on both edges).
    //   3. Every header cell and every row text cell carries a gutter;
    //      pills use Margin instead of Padding (full-width tiles).
    //   4. Color trims+tooltips like its siblings (no silent clip).
    //   5. The stats strip has no stray one-off gutter width.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class SheetStoreGridGeometrySourceTests
    {
        [Fact]
        public void Header_and_row_column_tables_are_identical()
        {
            string view = View();

            int tables = Count(view, "<ColumnDefinition Width=\"40\"/>");
            Assert.Equal(2, tables);
            int buyCol = Count(view, "<ColumnDefinition Width=\"72\"/>");
            Assert.Equal(2, buyCol);
            int pillCol = Count(view, "<ColumnDefinition Width=\"58\"/>");
            Assert.Equal(6, pillCol);           // 3 pills x 2 tables
            int supplierFlex = Count(view, "<ColumnDefinition Width=\"*\"/>");
            Assert.True(supplierFlex >= 2,
                "Supplier stays the flexible column in both tables.");
        }

        [Fact]
        public void Header_padding_matches_composed_row_padding()
        {
            string view = View();

            // 1 (frame) + 24 (header padding) = 25 = 1 + 10 + 2 + 12
            // (list frame + ListBox padding + selection frame + row pad).
            Assert.Contains("Padding=\"24,10\"", view);
            Assert.Contains("CornerRadius=\"12,12,0,0\" Padding=\"24,10\"", view);
            Assert.Contains("Padding=\"10,6,10,10\"", view);   // ListBox
            Assert.Contains("Padding=\"12,10\"", view);         // row Border style
        }

        [Fact]
        public void Every_column_cell_carries_the_same_gutter_rule()
        {
            string view = View();

            // 14 header cells + 11 row text cells = 25 padded cells. The
            // three pills use Margin so their tint fills the full cell.
            int paddedTextCells = Count(view, "Padding=\"6,0\"", true);
            Assert.True(paddedTextCells >= 25,
                $"Expected >=25 padded text cells, found {paddedTextCells}.");
            Assert.Equal(4, Count(view, "Margin=\"6,0\""));
        }

        [Fact]
        public void Color_column_trims_with_tooltip_like_its_siblings()
        {
            string view = View();

            Assert.Contains("Trimming=\"CharacterEllipsis\" ToolTip=\"{Binding Color}\"", view);
            Assert.Contains("Trimming=\"CharacterEllipsis\" ToolTip=\"{Binding Category}\"", view);
            Assert.Contains("Trimming=\"CharacterEllipsis\" ToolTip=\"{Binding Supplier}\"", view);
        }

        [Fact]
        public void Stats_strip_has_no_one_off_gutter()
        {
            string view = View();

            // The row's first gap used to be 16 while the rest were 20 —
            // visible rhythm break between HEADING and first stat.
            Assert.DoesNotContain("<ColumnDefinition Width=\"16\"/>", view);
        }

        // ══════════════════════════════════════════════════════════════
        //  DELIVERY REPORT — same hand-built list pattern, same defects
        //  pinned (header/row origin was 16 vs 14; zero gutters on
        //  headers; ad-hoc 8px/6px margins on a subset of row cells).
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void DeliveryReport_header_and_row_origins_match()
        {
            string view = DeliveryReportView();

            // Header style pads 16; the row Border must be 16 now, not
            // 14 — any retreat = misaligned columns at every cell.
            Assert.Contains("Padding=\"16,9\" Cursor=\"Hand\"", view);
        }

        [Fact]
        public void DeliveryReport_list_has_no_ad_hoc_cell_margins()
        {
            string view = DeliveryReportView();

            // Between the table header and the row template's end, the
            // one-off 8px/6px margin tails are gone; uniforms come from
            // the per-cell Padding rule. (The ageing-bucket card above
            // the table legitimately keeps its 8px gap — not in scope.)
            int hs = view.IndexOf("<!-- Table header -->", StringComparison.Ordinal);
            int re = view.IndexOf("</ListBox.ItemTemplate>", StringComparison.Ordinal);
            Assert.True(hs > 0 && re > hs);
            string scope = view.Substring(hs, re - hs);

            Assert.DoesNotContain("Margin=\"0,0,8,0\"", scope);
            Assert.DoesNotContain("Margin=\"0,0,6,0\"", scope);
            Assert.Contains("Margin=\"0,0,8,0\"",
                view.Substring(0, hs));   // card spacing preserved
        }

        [Fact]
        public void DeliveryReport_every_table_cell_carries_gutters()
        {
            string view = DeliveryReportView();

            // 15 header + 15 row cells, two width-table runs identical.
            int tables = Count(view, "Padding=\"6,0\" Text=", true);
            Assert.True(tables >= 26,
                $"Expected >=26 mid-column guttered cells, found {tables}.");
        }

        private static string View() => File.ReadAllText(Path.Combine(
            LocateProjectRoot(), "Views/SheetStore/SheetStoreView.xaml"));

        private static string DeliveryReportView() => File.ReadAllText(Path.Combine(
            LocateProjectRoot(), "Views/Reports/DeliveryReport/DeliveryReportView.xaml"));

        private static int Count(string haystack, string needle,
            bool bothDirections = false)
            => haystack.Split(
                   new[] { needle }, StringSplitOptions.None).Length - 1;

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
    }
}
