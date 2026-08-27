using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
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
                IsChecked = true
            });

            Assert.NotEqual(22m, row.Rate);

            row.UseOptimizationFormula = false;

            Assert.Equal(22m, row.Rate);
            Assert.Equal(44m, row.ResultAmount);
        }
    }
}
