// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/ProformaInvoice/Step6OtherCharges/Wastages/WastageRow.cs
//     RecalculateOptimizationResults (L1099–1127) + amount rules L999/1005
//   Golden values: Tests/Agent/WastageOptimizationFormulaTests.cs
// The WPF file is the source of truth; behaviour preserved. Only the shape
// changed: a pure function instead of an INotifyPropertyChanged row.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;

namespace ProGlass.Saas.Domain.Pricing;

/// <summary>The result of one wastage row's calculation.</summary>
public sealed record WastageResult(
    /// <summary>Price ÷ optimization% (Section 1 in the WPF display).</summary>
    decimal Section1Value,
    /// <summary>Price ÷ 85% baseline (Section 2 in the WPF display).</summary>
    decimal Section2Value,
    /// <summary>Section1 − Section2; also the row's rate per SQM.</summary>
    decimal BaseResultValue,
    /// <summary>Final amount (money-rounded); 0 when inactive.</summary>
    decimal ResultAmount);

/// <summary>
/// Step 6 "Other Charges → Wastage" calculation.
///
/// Optimization mode compares the job's sheet optimization % against the
/// 85% baseline the rest of the estimator assumes: the gap between the two
/// yields is exactly the glass that was paid for but could not be used, so
/// the price of that glass is charged back as wastage:
///
///   Section1 = FormulaPricePerSqm / (OptimizationPercent / 100)   [6 dp]
///   Section2 = FormulaPricePerSqm / (Baseline / 100)              [6 dp]
///   Rate     = Section1 − Section2                                [6 dp]
///   Amount   = Rate × SelectedSourceSqm
///
/// Manual mode is the plain "selected SQM × entered rate".
/// </summary>
public static class WastageFormula
{
    /// <summary>
    /// Baseline optimization % — the yield the Step 4 rates already assume
    /// (0.85). WPF: private const in WastageRow, L15.
    /// </summary>
    public const decimal BaselineOptimizationPercent = 85m;

    /// <summary>Optimization formula (WPF RecalculateOptimizationResults).</summary>
    public static WastageResult CalculateOptimization(
        decimal formulaPricePerSqm, decimal optimizationPercent, decimal selectedSourceSqm)
    {
        // Any non-positive input zeroes the row instead of dividing by
        // zero or inventing a charge.
        if (formulaPricePerSqm <= 0m || optimizationPercent <= 0m || selectedSourceSqm <= 0m)
        {
            return new WastageResult(0m, 0m, 0m, 0m);
        }

        decimal optimizationFactor = optimizationPercent / 100m;
        if (optimizationFactor == 0m)
        {
            return new WastageResult(0m, 0m, 0m, 0m);
        }

        decimal section1 = Math.Round(formulaPricePerSqm / optimizationFactor, 6);
        decimal section2 = Math.Round(
            formulaPricePerSqm / (BaselineOptimizationPercent / 100m), 6);
        decimal baseResult = Math.Round(section1 - section2, 6);
        decimal finalResult = Math.Round(baseResult * selectedSourceSqm, 6);

        // Rate = BaseResultValue; amount is money-rounded.
        return new WastageResult(section1, section2, baseResult, MoneyRounding.Round(finalResult));
    }

    /// <summary>Manual rate mode: selected SQM × entered rate (WPF L1005).</summary>
    public static decimal CalculateManual(decimal totalSqm, decimal rate) =>
        Math.Round(totalSqm * rate, 2);
}
