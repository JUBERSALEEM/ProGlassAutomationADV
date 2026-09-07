using ProGlassAutomation.Services.ProformaInvoice.Validation;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class CrossStepValidatorFinancialWaterfallTests
    {
        [Fact]
        public void Validate_CorrectFinancialWaterfall_IsValid()
        {
            var result = CrossStepValidator.Validate(BaseSnapshot());

            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        }

        [Fact]
        public void Validate_GlassSubtotalMismatch_BlocksSave()
        {
            var result = CrossStepValidator.Validate(BaseSnapshot(
                summaryGlass: 900m,
                net: 1100m,
                beforeVat: 1000m,
                vat: 50m,
                grand: 1050m));

            Assert.Contains(result.Errors, e => e.Contains("does not match Step 5 total"));
        }

        [Fact]
        public void Validate_WrongVatAmount_BlocksSave()
        {
            var result = CrossStepValidator.Validate(BaseSnapshot(
                vat: 60m,
                grand: 1010m));

            Assert.Contains(result.Errors, e => e.Contains("VAT amount mismatch"));
        }

        private static InvoiceValidationSnapshot BaseSnapshot(
            decimal summaryGlass = 1000m,
            decimal net = 1200m,
            decimal beforeVat = 950m,
            decimal vat = 47.50m,
            decimal grand = 997.50m) => new()
        {
            ProformaId = "PI-2026-001",
            CompanyName = "Pro Glass",
            IssueDate = DateTime.Today,
            ValidUntil = DateTime.Today.AddDays(7),
            CustomerName = "ABC",
            SpecGroupCount = 1,
            TotalSqm = 10m,
            TotalPieces = 4,
            TotalAmount = 1000m,
            TotalOtherCharges = 200m,
            ActiveChargeRows = 1,
            HasWastageRows = true,
            SummaryGlassMaterialsSubtotal = summaryGlass,
            SummaryOtherChargesSubtotal = 200m,
            NetSubtotal = net,
            DiscountAmount = 250m,
            DiscountPercent = 20.83m,
            SubtotalBeforeVat = beforeVat,
            VatPercent = 5m,
            IsVatApplicable = true,
            VatAmount = vat,
            GrandTotal = grand,
            ProfitMarginPercent = 15m,
            TotalCost = 750m,
            TotalRevenue = 997.50m
        };
    }
}
