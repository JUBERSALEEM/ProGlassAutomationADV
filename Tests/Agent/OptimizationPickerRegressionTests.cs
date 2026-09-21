// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationPickerRegressionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// REGRESSION: b9820ef skipped every JO that failed
// TryAuthorizeJobInvoiceForOptimization inside LoadJobs, so JOB / SIZES /
// CUT LIST / STOCK cascaded empty for legacy snapshots (MaterialIsValid
// null). Run() already refuses packing — the picker must still list the
// job and import real Step 5 W×H×Qty.
//
// September 14–17 (c37f71f) listed jobs with panes; zero-pane jobs must
// still appear with HasOptimizablePanes=false, never fake sizes.
//
// NOT EXECUTED here — .NET SDK is unavailable.
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows;
using ProGlassAutomation.Converters;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationPickerRegressionTests
    {
        [Fact]
        public void Invalid_material_job_is_listed_and_imports_sizes_but_cannot_run()
        {
            var invoice = new ProformaInvoiceSnapshot();
            invoice.SpecGroups.Add(new SpecGroupSnapshot
            {
                GroupNumber = 1,
                SelectedGlassType = "SGU",
                SelectedSpecName = "6mm Clear FT Glass",
                OuterGlassColor = "Clear",
                OuterSheetWidthMm = 3210,
                OuterSheetHeightMm = 2250,
                OuterSheetPricePerSqm = 46m,
                MaterialIsValid = null
            });

            var vm = new OptimizationViewModel { IsHeadlessHost = false };
            Assert.False(vm.TryAuthorizeJobInvoiceForOptimization(invoice, out string auth));
            Assert.StartsWith("Cannot optimize:", auth);

            var layer = new OptimizationViewModel.BuildUpLayerVm
            {
                Kind = "Pane",
                Position = "Glass",
                IsSelected = true,
                JobOrderId = "JO-REGRESS",
                ProformaId = "PI-REGRESS",
                SpecGroupNumber = 1,
                Color = "Clear",
                ThicknessMm = 6,
                Processing = "FT",
                Title = "6mm Clear FT glass",
                MaterialKey = "UNRESOLVED|JO-REGRESS|1|Glass"
            };
            layer.Pieces.Add(("6mm Clear FT glass", "G-01", 1200, 900, 4));

            var spec = new OptimizationViewModel.SpecOptionVm
            {
                JobOrderId = "JO-REGRESS",
                SpecGroupNumber = 1,
                SpecName = "6mm Clear FT Glass",
                BuildType = "Sgu"
            };
            spec.Layers.Add(layer);

            var job = new OptimizationViewModel.JobOptionVm
            {
                JobOrderId = "JO-REGRESS",
                CustomerName = "Regression Customer",
                CanOptimizeMaterial = false,
                MaterialInvalidReason = auth,
                HasOptimizablePanes = true
            };
            job.Specs.Add(spec);
            job.Step4Sheets.Add((3210, 2250, "6mm Clear FT glass", "Spec 1 outer", 46m, 1, "Outer"));

            vm.AttachJobOption(job);
            vm.AvailableJobs.Add(job);
            job.IsSelected = true;

            Assert.Single(vm.AvailableJobs);
            Assert.True(vm.HasAvailableJobs);
            Assert.True(vm.HasPieces);
            int qty = 0;
            foreach (var p in vm.Pieces)
            {
                if (p.TryParse(out _, out _, out int q)) qty += q;
            }
            Assert.Equal(4, qty);
            Assert.Contains(vm.Pieces, p => p.WidthText == "1200" && p.HeightText == "900");
            Assert.DoesNotContain(vm.Pieces, p => p.WidthText == "1000" && p.HeightText == "1000");

            Assert.True(vm.HasStocks);
            Assert.Contains(vm.Stocks, s => s.WidthText == "3210" && s.HeightText == "2250");

            Assert.Equal("6", vm.ThicknessText);
            Assert.False(vm.HasResult);
            Assert.Equal("—", vm.ResultSheets);

            vm.RunQuickCommand.Execute(null);
            Assert.False(vm.IsRunning);
            Assert.StartsWith("Cannot optimize:", vm.RunMessage);
            Assert.False(vm.HasResult);
        }

        [Fact]
        public void Selecting_job_with_step5_rows_and_no_layers_imports_cutting_list()
        {
            var vm = new OptimizationViewModel { IsHeadlessHost = false };
            var job = new OptimizationViewModel.JobOptionVm
            {
                JobOrderId = "JO-STEP5",
                CustomerName = "Step 5 only",
                CanOptimizeMaterial = false,
                HasOptimizablePanes = true,
                GlassType = "DGU",
                SpecName = "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
                OuterGlassColor = "Clear",
                InnerGlassColor = "Clear",
                OuterSheetWidthMm = 3210,
                OuterSheetHeightMm = 2250
            };
            job.Step5Rows.Add(new GlassItemRowSnapshot
            {
                GlassRef = "G-01",
                W1 = 1100,
                H1 = 900,
                W2 = 1080,
                H2 = 880,
                Qty = 3
            });
            vm.AttachJobOption(job);
            vm.AvailableJobs.Add(job);
            job.IsSelected = true;

            Assert.True(vm.HasPieces);
            Assert.Contains(vm.Pieces, p => p.WidthText == "1100" && p.HeightText == "900");
            Assert.Contains(vm.Pieces, p => p.WidthText == "1080" && p.HeightText == "880");
            int qty = 0;
            foreach (var p in vm.Pieces)
            {
                if (p.TryParse(out _, out _, out int q)) qty += q;
            }
            Assert.Equal(6, qty);
            Assert.True(vm.ManualCutList.ItemRows.Count > 0);
            Assert.Equal("DGU", vm.ManualCutList.SelectedGlassType);
            Assert.Equal("6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
                vm.ManualCutList.SelectedSpecName);
            Assert.Equal("Clear", vm.ManualCutList.OuterGlassColor);
            Assert.Equal(3210, vm.ManualCutList.OuterSheetWidthMm);
            Assert.Equal(2250, vm.ManualCutList.OuterSheetHeightMm);
        }

        [Fact]
        public void Load_reference_keeps_selected_job_and_does_not_replace_with_calibration()
        {
            var vm = new OptimizationViewModel { IsHeadlessHost = false };
            var job = new OptimizationViewModel.JobOptionVm
            {
                JobOrderId = "JO-KEEP",
                CustomerName = "Keep me",
                CanOptimizeMaterial = false,
                HasOptimizablePanes = true,
                GlassType = "SGU",
                SpecName = "6mm Clear FT Glass"
            };
            job.Step5Rows.Add(new GlassItemRowSnapshot
            {
                GlassRef = "A1",
                W1 = 900,
                H1 = 1200,
                Qty = 2
            });
            vm.AttachJobOption(job);
            vm.AvailableJobs.Add(job);
            job.IsSelected = true;

            vm.LoadReferenceJobCommand.Execute(null);

            Assert.True(job.IsSelected);
            Assert.Same(job, vm.SelectedJob ?? job);
            Assert.Contains(vm.Pieces, p => p.WidthText == "900" && p.HeightText == "1200");
            int qty = 0;
            foreach (var p in vm.Pieces)
            {
                if (p.TryParse(out _, out _, out int q)) qty += q;
            }
            Assert.Equal(2, qty);
            Assert.Equal("SGU", vm.ManualCutList.SelectedGlassType);
        }

        [Fact]
        public void Zero_pane_job_is_listed_without_inventing_sizes()
        {
            var vm = new OptimizationViewModel { IsHeadlessHost = false };
            var job = new OptimizationViewModel.JobOptionVm
            {
                JobOrderId = "JO-EMPTY",
                CustomerName = "No panes",
                CanOptimizeMaterial = true,
                HasOptimizablePanes = false
            };
            job.Warnings.Add("NO OPTIMIZABLE PANES");
            vm.AttachJobOption(job);
            vm.AvailableJobs.Add(job);
            job.IsSelected = true;

            Assert.Single(vm.AvailableJobs);
            Assert.False(job.HasOptimizablePanes);
            Assert.Contains("NO OPTIMIZABLE PANES", job.Warnings);
            Assert.False(vm.HasPieces);
            Assert.Contains("NO OPTIMIZABLE PANES", vm.PiecesEmptyHint);
            Assert.False(vm.HasResult);
            Assert.Empty(vm.Pieces);
        }

        [Fact]
        public void Load_reference_job_restores_sept12_152_piece_calibration()
        {
            var vm = new OptimizationViewModel { IsHeadlessHost = false };
            vm.LoadReferenceJobCommand.Execute(null);

            int qty = 0;
            foreach (var p in vm.Pieces)
            {
                if (p.TryParse(out int w, out int h, out int q))
                {
                    qty += q;
                    Assert.False(w == 1000 && h == 1000);
                }
            }
            Assert.Equal(152, qty);
            Assert.True(vm.HasPieces);
            Assert.True(vm.HasStocks);
            Assert.False(vm.HasResult);
            Assert.True(vm.ManualCutList.ItemRows.Count > 0);
            Assert.Contains(vm.ManualCutList.ItemRows,
                r => r.W1 == 1050 && r.H1 == 2127 && r.W2 == 1050 && r.H2 == 2127 && r.Qty == 2);
        }

        [Fact]
        public void Tab_index_converter_shows_jobs_and_hides_other_tabs()
        {
            var c = new IndexEqualsConverter();
            var inv = CultureInfo.InvariantCulture;
            Assert.Equal(Visibility.Visible,
                c.Convert(0, typeof(Visibility), "0", inv));
            Assert.Equal(Visibility.Collapsed,
                c.Convert(0, typeof(Visibility), "1", inv));
            Assert.Equal(Visibility.Visible,
                c.Convert(7, typeof(Visibility), "7", inv));
            Assert.True((bool)c.Convert(0, typeof(bool), "0", inv)!);
            Assert.False((bool)c.Convert(1, typeof(bool), "0", inv)!);
        }
    }
}
