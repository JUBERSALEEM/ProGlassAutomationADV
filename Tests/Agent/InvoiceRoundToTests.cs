using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class InvoiceRoundToTests
    {
        [Fact]
        public void Discount_on_net_hits_a_round_grand_total_with_5pct_vat()
        {
            // Net 10,000 + 5% VAT = 10,500. Customer wants 10,000 even.
            decimal discount = InvoiceRoundTo.DiscountOnNet(10000m, 5m, true, 10000m);
            decimal grand = InvoiceRoundTo.GrandFromDiscount(10000m, discount, 5m, true);

            Assert.Equal(10000m, grand);
            Assert.True(discount > 0m);
        }

        [Fact]
        public void Sixteen_thousand_rounds_to_fifteen_five_hundred()
        {
            // Net that Step 7 would show as AED 16,000.00 with 5% VAT.
            const decimal net = 15238.09m;
            Assert.Equal(16000m, InvoiceRoundTo.GrandFromDiscount(net, 0m, 5m, true));

            decimal discount = InvoiceRoundTo.DiscountOnNet(net, 5m, true, 15500m);
            decimal grand = InvoiceRoundTo.GrandFromDiscount(net, discount, 5m, true);
            Assert.Equal(15500m, grand);
        }

        [Fact]
        public void Without_vat_discount_is_the_difference()
        {
            decimal discount = InvoiceRoundTo.DiscountOnNet(16000m, 5m, false, 15500m);
            Assert.Equal(500m, discount);
        }

        [Fact]
        public void Target_at_or_above_undiscounted_grand_is_zero_discount()
        {
            Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(10000m, 5m, true, 10500m));
            Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(10000m, 5m, true, 20000m));
        }

        [Fact]
        public void Fils_amount_is_accepted()
        {
            decimal discount = InvoiceRoundTo.DiscountOnNet(1000m, 5m, true, 999.99m);
            decimal grand = InvoiceRoundTo.GrandFromDiscount(1000m, discount, 5m, true);
            Assert.Equal(999.99m, grand);
        }
    }
}
