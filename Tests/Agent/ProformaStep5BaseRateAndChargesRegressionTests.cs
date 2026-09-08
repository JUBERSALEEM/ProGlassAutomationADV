using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ProformaStep5BaseRateAndChargesRegressionTests
    {
        private static Step4PricingSnapshot DguSnapshot(decimal estimatedRate) => new()
        {
            SelectedGlassTypeCode = "DGU",
            GeneratedSpecName =
                "6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm Clear FT Glass",
            EstimatedRatePerSqm = estimatedRate,
            Dgu = new DguPricingPanelSnapshot
            {
                OuterGlassColor = "HD Grey",
                InnerGlassColor = "Clear",
                OuterSheetWidthMm = 3210,
                OuterSheetHeightMm = 2250,
                InnerSheetWidthMm = 2440,
                InnerSheetHeightMm = 1830,
                OuterBasePrice = 52m,
                InnerBasePrice = 31m,
                SpacerExtra = "U-Insert",
                UInsertPricePerSqm = 9.75m
            }
        };

        [Fact]
        public void Typed_step5_base_rate_survives_a_later_step4_snapshot_refresh()
        {
            var group = new SpecGroup();
            group.ApplyStep4PricingSnapshot(DguSnapshot(137.50m));
            Assert.Equal(137.50m, group.BaseRate);
            Assert.False(group.HasManualStep5BaseRateOverride);

            group.BaseRate = 160m;

            group.ApplyStep4PricingSnapshot(DguSnapshot(142m));

            Assert.Equal(160m, group.BaseRate);
            Assert.True(group.HasManualStep5BaseRateOverride);
            Assert.Equal(52m, group.OuterSheetPricePerSqm);
            Assert.Equal(9.75m, group.UInsertPricePerSqm);
        }

        [Fact]
        public void Unedited_step5_base_rate_still_tracks_step4_estimated_rate()
        {
            var group = new SpecGroup();
            group.ApplyStep4PricingSnapshot(DguSnapshot(137.50m));

            group.ApplyStep4PricingSnapshot(DguSnapshot(142m));

            Assert.Equal(142m, group.BaseRate);
            Assert.False(group.HasManualStep5BaseRateOverride);
        }

        [Fact]
        public void Reapplying_the_same_step4_sheet_prices_does_not_pulse_zero()
        {
            var group = new SpecGroup();
            group.ApplyStep4PricingSnapshot(DguSnapshot(137.50m));

            int outerPriceChanges = 0;
            group.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SpecGroup.OuterSheetPricePerSqm))
                    outerPriceChanges++;
            };

            group.ApplyStep4PricingSnapshot(DguSnapshot(137.50m));

            Assert.Equal(0, outerPriceChanges);
            Assert.Equal(52m, group.OuterSheetPricePerSqm);
        }

        [Fact]
        public void Suggested_charge_rate_is_blocked_after_the_operator_types_one()
        {
            var row = new OtherChargeRow { TemplateKey = "U_INSERT" };

            row.ApplySuggestedRate(9.75m);
            Assert.Equal(9.75m, row.Rate);
            Assert.False(row.HasManualRateOverride);

            row.Rate = 12.50m;
            row.ApplySuggestedRate(9.75m);

            Assert.Equal(12.50m, row.Rate);
            Assert.True(row.HasManualRateOverride);
        }

        [Fact]
        public void Restored_charge_rate_is_not_overwritten_by_advanced_suggestions()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.ApplyStep4PricingSnapshot(DguSnapshot(137.50m));

            var vm = new OtherChargesViewModel(grid);
            var row = vm.ChargeRows.First(r => r.TemplateKey == "U_INSERT");
            Assert.True(row.IsActive);
            Assert.Equal(9.75m, row.Rate);

            row.RestoreRate(15m);
            row.RestoreIsActive(true);

            group.UInsertPricePerSqm = 9.75m;
            vm.RefreshWorkspace();

            Assert.Equal(15m, row.Rate);
            Assert.True(row.IsActive);
        }
    }
}
