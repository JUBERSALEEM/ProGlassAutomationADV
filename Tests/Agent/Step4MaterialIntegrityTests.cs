// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/Step4MaterialIntegrityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PHASE A.1 — Step 4 material integrity (behavioral).
// NOT EXECUTED in this environment — .NET SDK is unavailable.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class Step4MaterialIntegrityTests
    {
        [Fact]
        public void Empty_colour_is_invalid()
        {
            var v = Step4MaterialIntegrity.EvaluatePane(
                "SGU", "", "6mm", "FT Glass", 3210, 2250, 46m, true);

            Assert.False(v.IsValid);
            Assert.Contains("colour", v.Reason);
        }

        [Fact]
        public void Unparseable_thickness_is_invalid()
        {
            var v = Step4MaterialIntegrity.EvaluatePane(
                "SGU", "Clear", "SETTINGS", "FT Glass", 3210, 2250, 46m, true);

            Assert.False(v.IsValid);
            Assert.Contains("thickness", v.Reason);
        }

        [Fact]
        public void Leftover_price_46_with_sheet_size_and_null_resolution_is_invalid()
        {
            var v = Step4MaterialIntegrity.EvaluatePane(
                "SGU", "Clear", "6mm", "FT Glass", 3210, 2250, 46m, null);

            Assert.False(v.IsValid);
            Assert.False(Step4MaterialIntegrity.IsStorePriceResolved(null));
            Assert.False(Step4MaterialIntegrity.IsStorePriceResolved(false));
            Assert.True(Step4MaterialIntegrity.IsStorePriceResolved(true));

            var sgu = Step4MaterialIntegrity.EvaluateSgu(new SguPricingPanelSnapshot
            {
                SelectedThickness = "6mm",
                GlassColor = "Clear",
                SelectedTreatment = "FT Glass",
                BasePricePerSqm = 46m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                PriceResolvedFromStore = null
            });
            Assert.False(sgu.IsValid);
        }

        [Fact]
        public void Lookup_miss_does_not_resolve_a_zero_or_default_as_store_price()
        {
            Assert.Null(Step4MaterialIntegrity.ResolveStorePrice(null));
            Assert.Null(Step4MaterialIntegrity.ResolveStorePrice((0m, 0m, "")));
            Assert.Equal(25m, Step4MaterialIntegrity.ResolveStorePrice((20m, 25m, "x")));
            Assert.Equal(20m, Step4MaterialIntegrity.ResolveStorePrice((20m, 0m, "x")));
        }

        [Fact]
        public void DguLam_without_all_three_sheet_sizes_is_invalid()
        {
            var snap = ValidDguLam();
            snap.LamInnerSheetWidthMm = 0;
            snap.LamInnerSheetHeightMm = 0;

            var v = Step4MaterialIntegrity.EvaluateDguLam(snap);
            Assert.False(v.IsValid);
            Assert.Contains("lam inner", v.Reason);
        }

        [Fact]
        public void DguLam_with_three_resolved_panes_is_valid()
        {
            var v = Step4MaterialIntegrity.EvaluateDguLam(ValidDguLam());
            Assert.True(v.IsValid);
            Assert.Equal("", v.Reason);
        }

        [Fact]
        public void MaterialIsValid_null_is_rejected_even_when_colour_size_and_price_look_filled()
        {
            var group = FilledSguGroup();
            group.MaterialIsValid = null;
            group.MaterialInvalidReason = "";

            var v = Step4MaterialIntegrity.EvaluateSpecGroup(group);
            Assert.False(v.IsValid);
            Assert.Contains("unresolved", v.Reason);

            var groups = Step4MaterialIntegrity.EvaluateSpecGroups(new[] { group });
            Assert.False(groups.IsValid);
        }

        [Fact]
        public void MaterialIsValid_false_is_rejected()
        {
            var group = FilledSguGroup();
            group.MaterialIsValid = false;
            group.MaterialInvalidReason = "SGU: no Sheet Store row for Clear 6mm 3210×2250.";

            var v = Step4MaterialIntegrity.EvaluateSpecGroup(group);
            Assert.False(v.IsValid);
            Assert.Contains("Sheet Store", v.Reason);
        }

        [Fact]
        public void ApplyPricingSnapshot_null_resolution_stays_unresolved()
        {
            var vm = new PricingEstimatorViewModel(new DialogService(), null);
            vm.ApplyPricingSnapshot(new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "SGU",
                Sgu = new SguPricingPanelSnapshot
                {
                    SelectedThickness = "6mm",
                    GlassColor = "Clear",
                    SelectedTreatment = "FT Glass",
                    BasePricePerSqm = 46m,
                    SheetWidthMm = 3210,
                    SheetHeightMm = 2250,
                    PriceResolvedFromStore = null
                }
            });

            Assert.False(vm.SguVM.PanePriceResolvedFromStore);
            Assert.Equal(46m, vm.SguVM.BasePricePerSqm);
            Assert.False(Step4MaterialIntegrity.EvaluateSgu(new SguPricingPanelSnapshot
            {
                SelectedThickness = vm.SguVM.SelectedThickness,
                GlassColor = vm.SguVM.GlassColor,
                SelectedTreatment = vm.SguVM.SelectedTreatment,
                BasePricePerSqm = vm.SguVM.BasePricePerSqm,
                SheetWidthMm = vm.SguVM.SheetWidthMm,
                SheetHeightMm = vm.SguVM.SheetHeightMm,
                PriceResolvedFromStore = vm.SguVM.PanePriceResolvedFromStore
            }).IsValid);
        }

        [Fact]
        public void RunFromStep5_with_invalid_material_does_not_enter_the_optimizer()
        {
            var vm = new OptimizationViewModel { IsHeadlessHost = true };
            int piecesBefore = vm.Pieces.Count;
            int stocksBefore = vm.Stocks.Count;

            var group = FilledSguGroup();
            group.MaterialIsValid = false;
            group.ItemRows.Add(new GlassItemRowSnapshot
            {
                GlassRef = "A1", W1 = 1000, H1 = 800, Qty = 1
            });

            bool started = vm.RunFromStep5(new List<SpecGroupSnapshot> { group }, "QUICK", out string message);

            Assert.False(started);
            Assert.False(vm.IsRunning);
            Assert.Equal(piecesBefore, vm.Pieces.Count);
            Assert.Equal(stocksBefore, vm.Stocks.Count);
            Assert.StartsWith("Cannot optimize:", message);
        }

        [Fact]
        public void Job_order_optimization_with_invalid_material_does_not_enter_the_optimizer()
        {
            var invoice = new ProformaInvoiceSnapshot();
            var group = FilledSguGroup();
            group.MaterialIsValid = null;
            invoice.SpecGroups.Add(group);

            var vm = new OptimizationViewModel { IsHeadlessHost = true };
            bool authorized = vm.TryAuthorizeJobInvoiceForOptimization(invoice, out string message);

            Assert.False(authorized);
            Assert.StartsWith("Cannot optimize:", message);
            Assert.False(vm.IsRunning);
            Assert.Empty(vm.Pieces);

            vm.IsHeadlessHost = false;
            var job = new OptimizationViewModel.JobOptionVm
            {
                JobOrderId = "JO-INVALID",
                CanOptimizeMaterial = false,
                MaterialInvalidReason = "Step 4 material is unresolved or invalid — optimization cannot use this spec."
            };
            job.IsSelected = true;
            vm.AttachJobOption(job);
            vm.AvailableJobs.Add(job);

            vm.RunQuickCommand.Execute(null);

            Assert.False(vm.IsRunning);
            Assert.StartsWith("Cannot optimize:", vm.RunMessage);
            Assert.DoesNotContain("sheet(s)", vm.RunMessage);
        }

        [Fact]
        public void Lam_sheet_store_miss_leaves_resolved_false_and_keeps_displayed_price()
        {
            var vm = new LamParametersViewModel();
            Assert.False(vm.OuterPriceResolvedFromStore);
            Assert.False(vm.InnerPriceResolvedFromStore);
            Assert.Equal(46m, vm.OuterBasePrice);
            Assert.Equal(46m, vm.InnerBasePrice);

            vm.SetPanePriceResolvedFromStore(false, false);
            Assert.False(vm.OuterPriceResolvedFromStore);
            Assert.False(vm.InnerPriceResolvedFromStore);
            Assert.Equal(46m, vm.OuterBasePrice);

            var lam = Step4MaterialIntegrity.EvaluateLam(new LamPricingPanelSnapshot
            {
                OuterThickness = "6mm",
                OuterGlassColor = "Clear",
                OuterTreatment = "FT",
                OuterBasePrice = vm.OuterBasePrice,
                OuterSheetWidthMm = 3210,
                OuterSheetHeightMm = 2250,
                OuterPriceResolvedFromStore = vm.OuterPriceResolvedFromStore,
                InnerThickness = "6mm",
                InnerGlassColor = "Clear",
                InnerTreatment = "FT",
                InnerBasePrice = vm.InnerBasePrice,
                InnerSheetWidthMm = 3210,
                InnerSheetHeightMm = 2250,
                InnerPriceResolvedFromStore = vm.InnerPriceResolvedFromStore
            });
            Assert.False(lam.IsValid);
        }

        private static SpecGroupSnapshot FilledSguGroup() => new()
        {
            GroupNumber = 1,
            SelectedGlassType = "SGU",
            SelectedSpecName = "6mm Clear FT Glass",
            OuterGlassColor = "Clear",
            OuterSheetWidthMm = 3210,
            OuterSheetHeightMm = 2250,
            OuterSheetPricePerSqm = 46m,
            MaterialIsValid = true
        };

        private static DguLamPricingPanelSnapshot ValidDguLam() => new()
        {
            OuterThickness = "6mm",
            OuterGlassColor = "HD Grey",
            OuterTreatment = "FT",
            OuterBasePrice = 48m,
            OuterSheetWidthMm = 3210,
            OuterSheetHeightMm = 2250,
            OuterPriceResolvedFromStore = true,
            LamOuterThickness = "6mm",
            LamOuterColor = "Clear",
            LamOuterTreatment = "FT",
            LamOuterBasePrice = 25m,
            LamOuterSheetWidthMm = 3210,
            LamOuterSheetHeightMm = 2250,
            LamOuterPriceResolvedFromStore = true,
            LamInnerThickness = "6mm",
            LamInnerColor = "Clear",
            LamInnerTreatment = "FT",
            LamInnerBasePrice = 25m,
            LamInnerSheetWidthMm = 3210,
            LamInnerSheetHeightMm = 2250,
            LamInnerPriceResolvedFromStore = true
        };
    }
}
