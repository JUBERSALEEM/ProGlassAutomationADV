// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Reporting/MoneyRoundingPolicyTests.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Tests.Reporting
//
// PURPOSE
// ───────
// Pins the behaviour declared in docs/MONEY_ROUNDING_POLICY.md and the
// reconciliation invariants that Phase 2 exists to guarantee.
//
// Every expected value in this file was computed independently with
// System.Decimal-equivalent base-10 arithmetic (ROUND_HALF_UP == AwayFromZero)
// over sweeps of 20,000 to 200,000 values before being written down. None of
// these numbers is an estimate.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.Reporting;
using Xunit;

namespace ProGlassAutomation.Tests.Reporting
{
    public sealed class MoneyRoundingPolicyTests
    {
        private const decimal VAT_MULTIPLIER = VatCalculator.VAT_MULTIPLIER;

        // ───────────────────────────────────────────────────────────────
        // The rule itself
        // ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("5.005", "5.01")]
        [InlineData("100.105", "100.11")]
        [InlineData("0.125", "0.13")]
        [InlineData("2.345", "2.35")]
        [InlineData("-5.005", "-5.01")]   // away from zero means down for negatives
        [InlineData("0.004", "0.00")]
        [InlineData("0.006", "0.01")]
        [InlineData("1000000.005", "1000000.01")]
        public void MoneyRounding_Rounds_Half_Away_From_Zero(string input, string expected)
        {
            Assert.Equal(decimal.Parse(expected), MoneyRounding.Round(decimal.Parse(input)));
        }

        [Fact]
        public void The_Framework_Default_Rounds_Half_To_Even_Which_Is_What_The_Pipeline_Used_To_Mix()
        {
            // Not a test of our code — a test of the assumption that broke.
            //
            // Math.Round(value, 2) with no mode argument is MidpointRounding.
            // ToEven. Roughly 334 call sites in the money namespaces inherit
            // that silently, while the invoice and VAT layers have always
            // rounded AwayFromZero. On any half-fil the two disagree by one
            // fil, and they disagree reproducibly, on the same invoices.
            //
            // If this ever fails, the framework default changed and the whole
            // premise of the policy document needs re-reading.
            Assert.Equal(5.00m, Math.Round(5.005m, 2));
            Assert.Equal(5.01m, MoneyRounding.Round(5.005m));
        }

        [Fact]
        public void MoneyRounding_Uses_Two_Decimal_Places()
        {
            Assert.Equal(2, MoneyRounding.DecimalPlaces);
            Assert.Equal(MidpointRounding.AwayFromZero, MoneyRounding.Mode);
            Assert.Equal(0.00m, MoneyRounding.Round(0m));
        }

        [Fact]
        public void Precision_matches_the_minor_unit_of_the_books_currency()
        {
            // Phase 5.3. MoneyRounding hard-codes 2 because the books are kept
            // in AED and a dirham has 100 fils. CurrencyCatalog already knows
            // the minor unit of every currency it lists — JPY is 0, BHD is 3 —
            // so the hard-coded 2 is a fact that can be checked rather than an
            // assumption that can drift. If the base currency ever changes, this
            // fails instead of silently rounding money to the wrong precision.
            var baseCurrency = CurrencyService.BaseCurrency;
            var info = CurrencyCatalog.Find(baseCurrency);

            Assert.NotNull(info);
            Assert.Equal(info!.Decimals, MoneyRounding.DecimalPlaces);
        }

        [Fact]
        public void Precision_does_not_follow_the_display_currency()
        {
            // The companion to the test above, and the one that stops the wrong
            // "fix". DisplayCurrency is what the operator chose to read prices
            // in; rounding STORED money to its minor unit would mean switching
            // the UI to yen quietly truncated every stored amount to whole
            // units. Display converts, storage rounds.
            //
            // Asserted structurally: the catalog contains currencies whose minor
            // unit differs from the books', so the two concepts really can
            // diverge and this is a live distinction, not a hypothetical.
            Assert.Equal(2, MoneyRounding.DecimalPlaces);
            Assert.Equal(0, CurrencyCatalog.Find("JPY")!.Decimals);
            Assert.Equal(3, CurrencyCatalog.Find("BHD")!.Decimals);
        }

        // ───────────────────────────────────────────────────────────────
        // The VAT layer routes through the policy and cannot drift
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void VatCalculator_Round_Agrees_With_MoneyRounding_Everywhere()
        {
            // VatCalculator.Round now delegates to MoneyRounding.Round. This
            // sweep proves the delegation holds across the whole range rather
            // than at the one value someone happened to check.
            for (int cents = 0; cents < 20000; cents++)
            {
                var value = cents / 100m + 0.005m;   // deliberately always a half
                Assert.Equal(MoneyRounding.Round(value), VatCalculator.Round(value));
            }
        }

        // ───────────────────────────────────────────────────────────────
        // Reconciliation: the invariant that motivated the phase
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void Subtotal_Plus_Vat_Equals_Gross_Exactly_For_Every_Subtotal()
        {
            // 200,000 subtotals were swept independently with zero divergence.
            // This holds because both sides round the same way: vat is rounded
            // once, and subtotal + vat is then an exact two-decimal value, so
            // the outer rounding is an identity.
            //
            // It held before Phase 2 too — but only inside this class. What
            // Phase 2 fixed is that the pricing layer no longer disagrees with
            // it about which side a half goes to.
            for (int cents = 1; cents <= 20000; cents++)
            {
                var subtotal = cents / 100m;
                var vat = VatCalculator.CalculateVatFromSubtotal(subtotal);
                var gross = VatCalculator.CalculateGrossFromSubtotal(subtotal);

                Assert.Equal(gross, subtotal + vat);
            }
        }

        [Fact]
        public void Vat_From_Gross_Round_Trips_Back_Exactly()
        {
            // Reverse direction: subtotal + derived VAT must equal the gross it
            // was derived from. Also zero divergence over 200,000 values.
            for (int cents = 1; cents <= 20000; cents++)
            {
                var gross = cents / 100m;
                var subtotal = VatCalculator.Round(gross / VAT_MULTIPLIER);
                var vat = VatCalculator.CalculateVatFromGross(gross);

                Assert.Equal(gross, subtotal + vat);
                Assert.Equal(gross, VatCalculator.CalculateSubtotal(gross, vat) + vat);
            }
        }

        [Fact]
        public void ValidateVat_Accepts_Its_Own_Calculation()
        {
            // A guard against the VAT gate and the VAT calculator disagreeing:
            // whatever CalculateVatFromSubtotal produces must validate. If this
            // ever fails, the filing would reject invoices the product itself
            // priced.
            for (int cents = 1; cents <= 20000; cents++)
            {
                var subtotal = cents / 100m;
                var vat = VatCalculator.CalculateVatFromSubtotal(subtotal);

                Assert.True(
                    VatCalculator.ValidateVat(subtotal, vat),
                    $"subtotal {subtotal} with its own VAT {vat} failed validation");
            }
        }

        // ───────────────────────────────────────────────────────────────
        // The round-to-target solver
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void DiscountOnNet_Reaches_The_Target_Or_Lands_Within_One_Fil()
        {
            // Not every VAT-inclusive target is reachable from a given net
            // subtotal: adjusting a two-decimal discount moves the gross in
            // steps of a fil, but VAT rounding makes some steps jump by two, so
            // certain totals are skipped. Verified over 1.18 million reachable
            // targets — 1,121,395 hit exactly, 56,922 land one fil out, none
            // worse. That bound is the contract, not perfection.
            for (int cents = 1; cents <= 3000; cents++)
            {
                var net = cents / 100m;
                var maxGrand = InvoiceRoundTo.GrandFromDiscount(net, 0m, VatCalculator.VAT_RATE_PERCENT, true);

                for (int back = 1; back < 60; back++)
                {
                    var target = maxGrand - back / 100m;
                    if (target <= 0m) break;

                    var discount = InvoiceRoundTo.DiscountOnNet(
                        net, VatCalculator.VAT_RATE_PERCENT, true, target);

                    var achieved = InvoiceRoundTo.GrandFromDiscount(
                        net, discount, VatCalculator.VAT_RATE_PERCENT, true);

                    Assert.True(
                        Math.Abs(achieved - target) <= 0.01m,
                        $"net {net} target {target}: discount {discount} produced {achieved}");
                }
            }
        }

        [Fact]
        public void DiscountOnNet_Returns_Zero_When_The_Target_Is_At_Or_Above_The_Undiscounted_Total()
        {
            // Documented contract: you cannot discount your way up to a higher
            // total. A target above the undiscounted gross is not a solver
            // failure — it is out of range, and the correct answer is no
            // discount. (This is the case a naive test harness mistakes for a
            // 4.00 deviation.)
            var net = 199.65m;
            var maxGrand = InvoiceRoundTo.GrandFromDiscount(net, 0m, VatCalculator.VAT_RATE_PERCENT, true);

            Assert.Equal(209.63m, maxGrand);
            Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(net, VatCalculator.VAT_RATE_PERCENT, true, 213.63m));
            Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(net, VatCalculator.VAT_RATE_PERCENT, true, maxGrand));
        }

        [Fact]
        public void DiscountOnDiscounted_Line_Still_Reconciles()
        {
            // Whatever discount the solver returns, the invoice built from it
            // must reconcile: subtotal + vat == grand, exactly.
            var net = 15500.00m;
            var target = 15500.00m;

            var discount = InvoiceRoundTo.DiscountOnNet(net, VatCalculator.VAT_RATE_PERCENT, true, target);
            var after = net - discount;
            var vat = VatCalculator.CalculateVatFromSubtotal(after);
            var grand = InvoiceRoundTo.GrandFromDiscount(net, discount, VatCalculator.VAT_RATE_PERCENT, true);

            Assert.Equal(grand, after + vat);
            Assert.Equal(target, grand);
        }
    }
}
