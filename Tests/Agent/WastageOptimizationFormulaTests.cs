using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class WastageOptimizationFormulaTests
    {
        [Fact]
        public void Optimization_formula_derives_rate_and_amount_from_selected_sqm()
        {
            var row = new WastageRow
            {
                UseOptimizationFormula = true,
                FormulaPricePerSqm = 40m,
                OptimizationPercent = 70m
            };

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 10m,
                PricePerSqm = 40m,
                IsChecked = true
            });

            Assert.Equal(57.142857m, row.Section1Value);
            Assert.Equal(47.058824m, row.Section2Value);
            Assert.Equal(10.084033m, row.BaseResultValue);
            Assert.Equal(10.084033m, row.Rate);
            Assert.Equal(100.84m, row.ResultAmount);
        }

        [Fact]
        public void Manual_rate_mode_preserves_entered_rate()
        {
            var row = new WastageRow
            {
                Rate = 12m
            };

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 3m,
                PricePerSqm = 40m,
                IsChecked = true
            });

            Assert.False(row.UseOptimizationFormula);
            Assert.Equal(12m, row.Rate);
            Assert.Equal(36m, row.ResultAmount);
        }

        [Fact]
        public void Switching_back_to_manual_mode_restores_manual_rate()
        {
            var row = new WastageRow
            {
                Rate = 22m,
                UseOptimizationFormula = true,
                FormulaPricePerSqm = 40m,
                OptimizationPercent = 70m
            };

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 2m,
                PricePerSqm = 40m,
                IsChecked = true
            });

            Assert.NotEqual(22m, row.Rate);

            row.UseOptimizationFormula = false;

            Assert.Equal(22m, row.Rate);
            Assert.Equal(44m, row.ResultAmount);
        }

        [Fact]
        public void Optimization_percent_text_accepts_decimal_input()
        {
            var row = new WastageRow();

            row.OptimizationPercentText = "15.60";

            Assert.Equal(15.60m, row.OptimizationPercent);
        }

        [Fact]
        public void Selected_source_auto_fills_sheet_price_and_size()
        {
            var row = new WastageRow();

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 5m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });

            Assert.Equal(48m, row.FormulaPricePerSqm);
            Assert.Equal("3210", row.SelectedSheetWidthText);
            Assert.Equal("2250", row.SelectedSheetHeightText);
            Assert.Equal(5m, row.ChargeableSqm);
        }

        [Fact]
        public void Utilized_sqm_raises_change_notification_when_formula_result_changes()
        {
            var row = new WastageRow();
            bool raised = false;
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(row.UtilizedSqm))
                    raised = true;
            };

            row.OptimizationPercentText = "70";
            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 5m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });

            Assert.True(raised);
            Assert.Equal(12.10m, decimal.Round(row.UtilizedSqm, 2));
        }

        [Fact]
        public void Matching_sources_across_outer_and_inner_are_aggregated_automatically()
        {
            var row = new WastageRow();

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                ColorName = "Clear",
                SqmValue = 5m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });
            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 2,
                SideLabel = "Inner (W2/H2)",
                ColorName = "Clear",
                SqmValue = 3m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });

            Assert.Equal(8m, row.ChargeableSqm);
            Assert.Equal(48m, row.FormulaPricePerSqm);
            Assert.Equal("3210", row.SelectedSheetWidthText);
            Assert.Equal("2250", row.SelectedSheetHeightText);
        }

        [Fact]
        public void Advanced_wastage_rows_start_on_by_default_for_new_detected_colors()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.SelectedGlassType = "DGU";
            group.SelectedSpecName = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass";
            group.OuterGlassColor = "HD Grey";
            group.InnerGlassColor = "Clear";

            var vm = new WastagesSectionViewModel(grid);
            vm.ApplyAdvancedSuggestions(new[]
            {
                new Services.ProformaInvoice.AutoWastageSuggestion
                {
                    SpecGroupNumber = 1,
                    SourceSideLabel = "Outer (W1/H1)",
                    DisplayName = "HD Grey — outer glass wastage"
                }
            });

            Assert.Single(vm.WastageRows);
            Assert.True(vm.WastageRows[0].IsActive);
        }
    }
}
