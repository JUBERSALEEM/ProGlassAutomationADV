// Golden values from the WPF (read 2026-09-27):
//   • Suggested spacer price — DguParametersViewModel.CalculateSuggestedSpacerPrice
//     L454–486: 37.5 + ((mm − 6)/2) × 2.5 + material surcharge.
//   • SQM rules — Utilities/SqmCalculator.cs (Ship 8c-2, Chat 11).
//   • Wastage optimization — WastageRow.RecalculateOptimizationResults +
//     Tests/Agent/WastageOptimizationFormulaTests.cs.

using ProGlass.Saas.Domain.Money;
using ProGlass.Saas.Domain.Pricing;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class SpacerPricingTests
{
    [Fact]
    public void Twelve_mm_asp_with_normal_mill_aluminum_costs_45()
    {
        // The WPF default: 37.5 + ((12 − 6)/2) × 2.5 = 45, no surcharge.
        Assert.Equal(45m, SpacerPricing.SuggestedPrice("12mm ASP", "Normal Mill Aluminum"));
    }

    [Theory]
    [InlineData("6mm ASP", 37.5)]
    [InlineData("8mm ASP", 40.0)]
    [InlineData("10mm ASP", 42.5)]
    [InlineData("12mm ASP", 45.0)]
    [InlineData("16mm ASP", 50.0)]
    [InlineData("20mm ASP", 55.0)]
    [InlineData("32mm ASP", 70.0)]
    public void Base_price_steps_2_5_per_extra_2mm(string width, decimal expected)
    {
        Assert.Equal(expected, SpacerPricing.SuggestedPrice(width, "Normal Mill Aluminum"));
    }

    [Theory]
    [InlineData("Normal Mill Aluminum", 0m)]
    [InlineData("Black Aluminum", 5m)]
    [InlineData("Warm Edge (Super Spacer)", 15m)]
    [InlineData("Black Warm Edge", 20m)]
    [InlineData("Stainless Steel", 10m)]
    [InlineData("TGI Spacer", 12m)]
    public void Material_surcharges_are_flat_aed(string material, decimal surcharge)
    {
        Assert.Equal(
            45m + surcharge,
            SpacerPricing.SuggestedPrice("12mm ASP", material));
    }

    [Fact]
    public void Unknown_material_adds_nothing()
    {
        Assert.Equal(45m, SpacerPricing.SuggestedPrice("12mm ASP", "Unobtainium"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ASP only")]
    [InlineData("mm leading")]
    [InlineData("not a number mm")]
    public void Unparsable_width_falls_back_to_12mm(string? width)
    {
        Assert.Equal(12m, SpacerPricing.ParseWidthMm(width));
        Assert.Equal(45m, SpacerPricing.SuggestedPrice(width, "Normal Mill Aluminum"));
    }

    [Fact]
    public void Width_parse_is_case_insensitive_on_mm()
    {
        Assert.Equal(12m, SpacerPricing.ParseWidthMm("12MM ASP"));
        Assert.Equal(6m, SpacerPricing.ParseWidthMm("6mm ASP"));
    }
}

public class SqmCalculatorTests
{
    [Fact]
    public void Panel_sqm_is_mm_product_over_a_million()
    {
        // 1000 × 1000 mm = exactly 1 m²
        Assert.Equal(1.000m, SqmCalculator.CalculatePanel(1000, 1000));
        // 1200 × 800 mm = 0.96 m²
        Assert.Equal(0.960m, SqmCalculator.CalculatePanel(1200, 800));
    }

    [Fact]
    public void Panels_under_half_a_square_meter_bill_as_half()
    {
        // 300 × 300 mm = 0.09 m² physically, billed as the 0.5 minimum.
        Assert.Equal(0.500m, SqmCalculator.CalculatePanel(300, 300));
        // 632 × 632 = 0.399424 → also under the floor.
        Assert.Equal(0.500m, SqmCalculator.CalculatePanel(632, 632));
        // 708 × 707 = 0.500556 → above the floor, billed as-is (3 dp)
        Assert.Equal(0.501m, SqmCalculator.CalculatePanel(708, 707));
    }

    [Fact]
    public void Invalid_dimensions_bill_zero()
    {
        Assert.Equal(0m, SqmCalculator.CalculatePanel(0, 1000));
        Assert.Equal(0m, SqmCalculator.CalculatePanel(1000, -5));
    }

    [Fact]
    public void Total_is_panel_sqm_times_quantity_at_3dp()
    {
        // 3 × (900 × 900 = 0.81) = 2.43
        Assert.Equal(2.430m, SqmCalculator.CalculateTotal(900, 900, 3));
        // 7 × 0.5-floor panels = 3.5
        Assert.Equal(3.500m, SqmCalculator.CalculateTotal(300, 300, 7));
        Assert.Equal(0m, SqmCalculator.CalculateTotal(900, 900, 0));
    }

    [Fact]
    public void Sheet_area_has_no_minimum_floor_and_uses_4dp()
    {
        // Stock sheets are raw physical area — a tiny sheet is NOT bumped
        // to 0.5 m², and the precision is the sheet-store 4 dp.
        Assert.Equal(0.0900m, SqmCalculator.CalculateSheetArea(300, 300));
        Assert.Equal(0.4998m, SqmCalculator.CalculateSheetArea(707, 707));
        Assert.Equal(3.2100m, SqmCalculator.CalculateSheetArea(3210, 1000));
        Assert.Equal(0m, SqmCalculator.CalculateSheetArea(0, 500));
    }
}

public class WastageFormulaTests
{
    // The WPF's own golden case (Tests/Agent/WastageOptimizationFormulaTests):
    // 40 AED/SQM at 70% optimization over the 85% baseline, 10 SQM selected.
    [Fact]
    public void Optimization_formula_derives_rate_and_amount_from_selected_sqm()
    {
        var r = WastageFormula.CalculateOptimization(40m, 70m, 10m);

        Assert.Equal(57.142857m, r.Section1Value);
        Assert.Equal(47.058824m, r.Section2Value);
        Assert.Equal(10.084033m, r.BaseResultValue);
        Assert.Equal(100.84m, r.ResultAmount);
    }

    [Fact]
    public void Manual_rate_mode_multiplies_sqm_by_entered_rate()
    {
        // WPF: ResultAmount = Math.Round(TotalSqm × Rate, 2)
        Assert.Equal(120.00m, WastageFormula.CalculateManual(10m, 12m));
        Assert.Equal(100.84m, WastageFormula.CalculateManual(10.084033m, 10m));
    }

    [Theory]
    [InlineData(0, 70, 10)]      // no sheet price
    [InlineData(40, 0, 10)]      // no optimization %
    [InlineData(40, 70, 0)]      // no selected SQM
    [InlineData(-1, 70, 10)]
    [InlineData(40, -5, 10)]
    [InlineData(40, 70, -3)]
    public void Non_positive_inputs_zero_the_row(decimal price, decimal pct, decimal sqm)
    {
        var r = WastageFormula.CalculateOptimization(price, pct, sqm);

        Assert.Equal(0m, r.Section1Value);
        Assert.Equal(0m, r.Section2Value);
        Assert.Equal(0m, r.BaseResultValue);
        Assert.Equal(0m, r.ResultAmount);
    }

    [Fact]
    public void Baseline_is_85_percent()
    {
        // The Step 4 rates assume 0.85 yield; the wastage formula charges
        // the gap between the job's real optimization and that baseline.
        Assert.Equal(85m, WastageFormula.BaselineOptimizationPercent);

        // At exactly the baseline there is nothing to charge.
        var r = WastageFormula.CalculateOptimization(40m, 85m, 10m);
        Assert.Equal(0m, r.BaseResultValue);
        Assert.Equal(0m, r.ResultAmount);
    }
}
