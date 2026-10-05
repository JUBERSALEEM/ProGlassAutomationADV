using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ProfitLossChargeLabelTests
    {
        [Fact]
        public void Price_display_uses_the_row_unit_label()
        {
            var row = new ProfitLossRow
            {
                PricePerSqm = 10m,
                UnitLabel = "QTY"
            };

            Assert.Equal("10.00 / QTY", row.PriceDisplay);
        }
    }
}
