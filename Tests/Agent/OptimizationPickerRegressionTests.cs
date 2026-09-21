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
// NOT EXECUTED here — .NET SDK is unavailable.
// ═══════════════════════════════════════════════════════════════════════

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
                MaterialInvalidReason = auth
            };
            job.Specs.Add(spec);

            vm.AttachJobOption(job);
            vm.AvailableJobs.Add(job);
            job.IsSelected = true;

            Assert.Single(vm.AvailableJobs);
            Assert.True(vm.HasPieces);
            int qty = 0;
            foreach (var p in vm.Pieces)
            {
                if (p.TryParse(out _, out _, out int q)) qty += q;
            }
            Assert.Equal(4, qty);
            Assert.Contains(vm.Pieces, p => p.WidthText == "1200" && p.HeightText == "900");

            vm.RunQuickCommand.Execute(null);
            Assert.False(vm.IsRunning);
            Assert.StartsWith("Cannot optimize:", vm.RunMessage);
            Assert.False(vm.HasResult);
        }
    }
}
