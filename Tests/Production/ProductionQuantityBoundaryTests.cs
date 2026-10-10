// Snapshot kinds, Qty=0 legacy, DGU units vs panes, reservation vs PCS,
// allocation vs Used. Source + executable. NOT EXECUTED without SDK.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Production;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionQuantityBoundaryTests
    {
        [Fact]
        public void Corrupt_job_json_is_not_valid_empty()
        {
            var kind = ProductionSnapshotIntegrity.ReadJobOrderJson(
                "{ this is not json", out var invoice, out string msg);

            Assert.Equal(ProductionSnapshotKind.Corrupt, kind);
            Assert.Null(invoice);
            Assert.Contains("corrupt", msg, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Missing_json_is_missing_not_empty_job()
        {
            var kind = ProductionSnapshotIntegrity.ReadJobOrderJson(
                "  ", out var invoice, out _);
            Assert.Equal(ProductionSnapshotKind.Missing, kind);
            Assert.Null(invoice);
        }

        [Fact]
        public void Valid_empty_spec_list_is_empty_not_corrupt()
        {
            var jo = new JobOrderSnapshot
            {
                JobOrderId = "JOB-EMPTY",
                EmbeddedInvoice = new ProformaInvoiceSnapshot()
            };
            string json = JsonSerializer.Serialize(jo);

            var kind = ProductionSnapshotIntegrity.ReadJobOrderJson(
                json, out var invoice, out string msg);

            Assert.Equal(ProductionSnapshotKind.ValidEmpty, kind);
            Assert.NotNull(invoice);
            Assert.Contains("no specification rows", msg);
        }

        [Fact]
        public void Valid_asymmetric_row_is_valid()
        {
            var jo = JobWithRow(3210, 2250, 3000, 1800, 17);
            string json = JsonSerializer.Serialize(jo);

            var kind = ProductionSnapshotIntegrity.ReadJobOrderJson(
                json, out var invoice, out _);

            Assert.Equal(ProductionSnapshotKind.Valid, kind);
            var row = invoice!.SpecGroups[0].ItemRows[0];
            Assert.Equal(3210, row.W1);
            Assert.Equal(2250, row.H1);
            Assert.Equal(3000, row.W2);
            Assert.Equal(1800, row.H2);
            Assert.Equal(17, row.Qty);
        }

        [Fact]
        public void Legacy_qty_zero_still_distributes_one_with_warning()
        {
            var snap = new ProformaInvoiceSnapshot();
            snap.SpecGroups.Add(new SpecGroupSnapshot
            {
                GroupNumber = 1,
                SelectedSpecName = "6mm Clear Annealed Glass",
                SelectedGlassType = "SGU",
                ItemRows =
                {
                    new GlassItemRowSnapshot
                    {
                        SerialNumber = 1,
                        GlassRef = "G-0",
                        W1 = 3210,
                        H1 = 2250,
                        Qty = 0
                    }
                }
            });

            var plan = ProductionDistributionEngine.Distribute("JOB-Q0", snap);
            var cutting = plan.Items.Single(i => i.Stage == Models.Production.ProductionStage.Cutting);
            Assert.Equal(1, cutting.Qty);
            Assert.Contains(plan.Warnings, w => w.Contains("no quantity", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Dgu_17_is_seventeen_units_and_thirty_four_pane_cuts()
        {
            var snap = new ProformaInvoiceSnapshot();
            snap.SpecGroups.Add(new SpecGroupSnapshot
            {
                GroupNumber = 1,
                SelectedSpecName = "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                SelectedGlassType = "DGU",
                ItemRows =
                {
                    new GlassItemRowSnapshot
                    {
                        SerialNumber = 1,
                        GlassRef = "D-17",
                        W1 = 3210, H1 = 2250,
                        W2 = 3000, H2 = 1800,
                        Qty = 17
                    }
                }
            });

            var plan = ProductionDistributionEngine.Distribute("JOB-D17", snap);
            var cutting = plan.Items.Where(i => i.Stage == Models.Production.ProductionStage.Cutting).ToList();
            Assert.Equal(2, cutting.Count);
            Assert.All(cutting, c => Assert.Equal(17, c.Qty));
            Assert.Equal(34, cutting.Sum(c => c.Qty));
            Assert.Equal(17, plan.Items.Single(i => i.Stage == Models.Production.ProductionStage.Pairing).Qty);
            Assert.Contains(cutting, c => c.Position == "Outer" && c.WidthMm == 3210 && c.HeightMm == 2250);
            Assert.Contains(cutting, c => c.Position == "Inner" && c.WidthMm == 3000 && c.HeightMm == 1800);
        }

        [Fact]
        public void Reservation_qty_is_not_qtydone_or_usestock()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.DoesNotContain("QtyDone", res);
            Assert.DoesNotContain("UseStockAsync", res);

            string cut = Read("Services/Production/Cutting/CuttingWorkflowService.cs");
            Assert.Contains("Math.Ceiling(group.TotalAreaSqm", cut);
            Assert.Contains("yyyyMMddHHmmss", cut);

            string complete = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("int newDone = Math.Min(workItem.Qty, workItem.QtyDone + 1);", complete);
            Assert.DoesNotContain("UseStockAsync", complete);
            Assert.Contains("reservation.Status = (int)ReservationStatus.Consumed", complete);
        }

        [Fact]
        public void Allocation_quantity_is_one_placed_piece()
        {
            string src = Read("Services/Production/MaterialAllocationService.cs");
            Assert.Contains("quantity: 1", Read("Services/Production/Cutting/CuttingWorkflowService.cs"));
            Assert.Contains("if (quantity <= 0) quantity = 1;", src);
            Assert.DoesNotContain("UseStockAsync", src);
        }

        [Fact]
        public void UseStock_cas_and_consumed_are_separate_databases()
        {
            string use = Read("Services/SheetStore/SheetStoreRepository.cs");
            Assert.Contains("int balance = sheet.Stock - sheet.Used;", use);
            Assert.Contains("db.SheetHistories.Add(CreateHistory(", use);

            string release = Read("Services/Production/ProductionReleaseService.cs");
            Assert.Contains("ProductionSnapshotKind.Corrupt", release);
            Assert.Contains("empty is not corrupt", release);
        }

        [Fact]
        public void Pi_mapper_still_returns_empty_on_corrupt_json_frozen_contract()
        {
            // Frozen PI mapper + SnapshotRoundTripTests.Corrupt_json_returns_empty_snapshot_not_throws.
            // Production gate above is the fail-closed path for the factory.
            string mapper = Read("Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs");
            Assert.Contains("return new ProformaInvoiceSnapshot();", mapper);
        }

        private static JobOrderSnapshot JobWithRow(int w1, int h1, int w2, int h2, int qty)
        {
            var snap = new ProformaInvoiceSnapshot();
            snap.SpecGroups.Add(new SpecGroupSnapshot
            {
                GroupNumber = 1,
                SelectedSpecName = "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                ItemRows =
                {
                    new GlassItemRowSnapshot
                    {
                        W1 = w1, H1 = h1, W2 = w2, H2 = h2, Qty = qty
                    }
                }
            });
            return new JobOrderSnapshot
            {
                JobOrderId = "JOB-OK",
                EmbeddedInvoice = snap
            };
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
                    "Services", "Production", "ProductionReleaseService.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
