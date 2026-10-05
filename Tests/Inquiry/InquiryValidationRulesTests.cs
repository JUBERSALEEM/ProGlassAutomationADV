using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public sealed class InquiryValidationRulesTests
    {
        [Fact]
        public void ValidateLineItems_CompleteUnknownBuild_BlocksConversionRisk()
        {
            var errors = InquiryValidationRules.ValidateLineItems(new[]
            {
                new InquiryLineItem
                {
                    SpecName = "6mm Clear FT",
                    GlassType = "Tempered",
                    WidthMm = 1000,
                    HeightMm = 1200,
                    Qty = 3,
                    RatePerSqm = 95m
                }
            });

            Assert.Contains(errors, e => e.Contains("SGU, DGU, LAM or DGU+LAM"));
        }

        [Fact]
        public void ValidateLineItems_PartialRows_AreAllowedAsInquiryNotes()
        {
            var errors = InquiryValidationRules.ValidateLineItems(new[]
            {
                new InquiryLineItem
                {
                    Notes = "Customer mentioned possible canopy; dimensions pending."
                }
            });

            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateForSave_NegativeCommercialValues_AreBlocked()
        {
            var record = new InquiryRecord
            {
                CustomerName = "ABC",
                ProjectName = "Tower",
                InquiryValue = -1m,
                LineItems = new()
                {
                    new InquiryLineItem
                    {
                        SpecName = "6mm Clear FT",
                        GlassType = "SGU",
                        WidthMm = 1000,
                        HeightMm = 1000,
                        Qty = 1,
                        RatePerSqm = -25m
                    }
                }
            };

            var errors = InquiryValidationRules.ValidateForSave(record);

            Assert.Contains(errors, e => e.Contains("Inquiry value cannot be negative"));
            Assert.Contains(errors, e => e.Contains("expected rate cannot be negative"));
        }
    }
}
