// Source pins for import → snapshot → production → reservation quantity.
// No SDK. Does not modify frozen PI / JO / Sheet Store / reservation lock.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ImportProductionDataIntegrityTests
    {
        [Fact]
        public void Cut_list_header_maps_w1_h1_w2_h2_qty_by_name()
        {
            string src = Read("Services/ProformaInvoice/Step5Grid/CutListImportParser.cs");

            Assert.Contains("\"w1\" or \"w\" or \"width\" or \"width1\"", src);
            Assert.Contains("\"h1\" or \"h\" or \"height\" or \"height1\"", src);
            Assert.Contains("\"w2\" or \"width2\" => \"w2\"", src);
            Assert.Contains("\"h2\" or \"height2\" => \"h2\"", src);
            Assert.Contains("\"qty\" or \"quantity\" or \"pcs\"", src);
            Assert.Contains("W1 = w1", src);
            Assert.Contains("H1 = h1", src);
            Assert.Contains("W2 = w2", src);
            Assert.Contains("H2 = h2", src);
        }

        [Fact]
        public void Import_apply_does_not_swap_axes()
        {
            string src = Read("ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs");

            Assert.Contains("dest.W1 = src.W1;", src);
            Assert.Contains("dest.H1 = src.H1;", src);
            Assert.Contains("dest.W2 = src.W2;", src);
            Assert.Contains("dest.H2 = src.H2;", src);
            Assert.Contains("dest.Qty = src.Qty > 0 ? src.Qty : 1;", src);
            Assert.DoesNotContain("dest.W1 = src.H1", src);
            Assert.DoesNotContain("dest.H1 = src.W1", src);
        }

        [Fact]
        public void Snapshot_persists_w1_h1_w2_h2_qty_and_ignores_computed_area()
        {
            string snap = Read("Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs");
            Assert.Contains("public int W1 { get; set; }", snap);
            Assert.Contains("public int H1 { get; set; }", snap);
            Assert.Contains("public int W2 { get; set; }", snap);
            Assert.Contains("public int H2 { get; set; }", snap);
            Assert.Contains("public int Qty { get; set; }", snap);
            Assert.Contains("[JsonIgnore]", snap);

            string mapper = Read("Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs");
            Assert.Contains("W1 = row.W1,", mapper);
            Assert.Contains("H1 = row.H1,", mapper);
            Assert.Contains("W2 = row.W2,", mapper);
            Assert.Contains("H2 = row.H2,", mapper);
            Assert.Contains("Qty = row.Qty,", mapper);
            Assert.Contains("rSnap.W1, rSnap.H1, rSnap.W2, rSnap.H2,", mapper);
        }

        [Fact]
        public void Production_outer_is_w1_h1_inner_uses_dgu_semantics()
        {
            string src = Read("Services/Production/ProductionDistributionEngine.cs");

            Assert.Contains("w = row.W1;", src);
            Assert.Contains("h = row.H1;", src);
            Assert.Contains("DguDimensionSemantics.TryResolveInnerSize(", src);
            Assert.Contains("Do not invent W1×H1 for", src);
            Assert.Contains("int qty = row.Qty <= 0 ? 1 : row.Qty;", src);
        }

        [Fact]
        public void Reservation_quantity_is_integer_sheets_not_spec_qty()
        {
            string cut = Read("Services/Production/Cutting/CuttingWorkflowService.cs");
            Assert.Contains("quantity: Math.Max(1, (int)Math.Ceiling(group.TotalAreaSqm", cut);

            string bridge = Read("Services/Production/ProductionOptimizationBridge.cs");
            Assert.Contains("sheet.Stock.Color, 1, $\"Bridge allocation sheet", bridge);

            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains("IsolationLevel.Serializable", res);
            Assert.Contains("g.Stock - g.Used", res);
            Assert.DoesNotContain("UseStockAsync", res);
        }

        [Fact]
        public void Consume_does_not_write_sheet_store_used()
        {
            Assert.DoesNotContain("UseStockAsync",
                Read("Services/Production/MaterialAllocationService.cs"));
            Assert.DoesNotContain("UseStockAsync",
                Read("Services/Production/Cutting/CuttingCompletionService.cs"));
            Assert.Contains("await _repo.UseStockAsync(sheet.Id, quantity)",
                Read("Services/ProformaInvoice/SheetStockLinkService.cs"));
        }

        [Fact]
        public void Stock_selection_filter_does_not_subtract_placed_pieces()
        {
            string src = Read("Services/Production/Cutting/StockSelectionService.cs");
            Assert.Contains("int effective = Math.Max(0, available - reserved);", src);
            Assert.DoesNotContain("available - reserved - allocated", src);
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "Production", "MaterialReservationService.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
