// Parity tests for the WPF money policy (Services/Reporting/MoneyRounding.cs).
// The half-away-from-zero assertions distinguish it from banker's rounding,
// which the WPF app explicitly rejected (Phase 2, 2026-09-09).

using ProGlass.Saas.Domain.Money;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class MoneyRoundingTests
{
    [Fact]
    public void Money_uses_two_decimal_places()
    {
        Assert.Equal(2, MoneyRounding.DecimalPlaces);
    }

    [Theory]
    [InlineData("2.345", "2.35")]   // banker's rounding would give 2.34
    [InlineData("0.125", "0.13")]   // banker's rounding would give 0.12
    [InlineData("0.005", "0.01")]   // a half-fil rounds UP
    [InlineData("2.344", "2.34")]
    [InlineData("2.346", "2.35")]
    [InlineData("1234.567", "1234.57")]
    [InlineData("0", "0")]
    public void Rounds_half_away_from_zero(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected), MoneyRounding.Round(decimal.Parse(input)));
    }

    [Theory]
    [InlineData("-0.005", "-0.01")] // away from zero, not toward even
    [InlineData("-2.345", "-2.35")]
    [InlineData("-1.234", "-1.23")]
    public void Negative_values_round_away_from_zero(string input, string expected)
    {
        Assert.Equal(decimal.Parse(expected), MoneyRounding.Round(decimal.Parse(input)));
    }

    [Fact]
    public void Explicit_precision_rounds_the_same_way()
    {
        Assert.Equal(1.235m, MoneyRounding.Round(1.2345m, 3));
        Assert.Equal(1.234m, MoneyRounding.Round(1.2344m, 3));
    }

    [Fact]
    public void Invoice_pipeline_stays_consistent()
    {
        // The original WPF bug this policy fixed: subtotal + VAT must equal
        // grand when every step rounds with the SAME rule.
        decimal subtotal = 1000.00m;
        decimal vat = MoneyRounding.Round(subtotal * 0.05m);
        decimal grand = MoneyRounding.Round(subtotal + vat);
        Assert.Equal(50.00m, vat);
        Assert.Equal(1050.00m, grand);
        Assert.Equal(grand, MoneyRounding.Round(subtotal) + MoneyRounding.Round(vat));
    }

    [Fact]
    public void Format_is_invariant_and_always_two_decimals()
    {
        Assert.Equal("1050.00", MoneyRounding.Format(1050m));
        Assert.Equal("0.01", MoneyRounding.Format(0.005m));
    }
}
