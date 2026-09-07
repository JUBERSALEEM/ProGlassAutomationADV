// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Inquiry/InquiryBoqConversionTests.cs
// PROJECT: ProGlassAutomation.Tests
// PHASE 4 — regression net for the inquiry→PI scope conversion contract.
//
// The pure conversion decisions live in
// Services.ProformaInvoice.InquiryBoqConversion (extracted from the wizard
// in Phase 4 so they are testable). What is pinned here:
//
//   1. Glass-type normalisation: DGU+LAM matched EXACTLY and FIRST —
//      a starts-with "DGU" rule would silently downgrade the most
//      expensive spec type. Unknown labels normalise to "" and complete
//      rows with unknown builds are not imported — never guessed.
//   2. Only IsImportable rows (spec name + W×H + qty) convert; partial
//      rows stay on the inquiry as notes.
//   3. Stable ordering by SortOrder — the PI grid mirrors the BOQ grid.
//   4. A non-positive rate means "not quoted": HasRate=false so the grid
//      group keeps its own pricing; the rate is never forced to zero.
//   5. Straight trimming of names/colours; nulls become "".
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.ProformaInvoice;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public sealed class InquiryBoqConversionTests
    {
        // ── 1. normalisation matrix ──────────────────────────────────────
        [Theory]
        [InlineData("DGU+LAM", "DGU+LAM")]
        [InlineData("dgu+lam", "DGU+LAM")]
        [InlineData(" DGU+LAM ", "DGU+LAM")]
        [InlineData("SGU", "SGU")]
        [InlineData("dgu", "DGU")]
        [InlineData(" LAM ", "LAM")]
        [InlineData("laminated", "")]     // not silently guessed
        [InlineData("TEMPERED", "")]      // tint layers aren't builds
        [InlineData("", "")]
        [InlineData(null, "")]
        public void NormalizeGlassType_MapsOntoPiVocabulary_Only(string? raw, string expected)
        {
            Assert.Equal(expected, InquiryBoqConversion.NormalizeGlassType(raw));
        }

        [Fact]
        public void NormalizeGlassType_CompoundType_IsNeverSwallowedByDgu()
        {
            // Order-of-evaluation pin: "dgu+lam" must reach "DGU+LAM", not "DGU".
            Assert.Equal("DGU+LAM", InquiryBoqConversion.NormalizeGlassType("dgu+lam"));
        }

        // ── 2/3/4/5. import-decision contract ────────────────────────────
        [Fact]
        public void ToImportRows_Null_ReturnsEmptyNotThrow()
        {
            Assert.Empty(InquiryBoqConversion.ToImportRows(null));
            Assert.Empty(InquiryBoqConversion.ToImportRows(
                Array.Empty<InquiryLineItem>()));
        }

        [Fact]
        public void ToImportRows_OnlyImportableRows_StableSortOrder()
        {
            var items = new List<InquiryLineItem>
            {
                MakeRow(sort: 2, spec: "Spec B", w: 500, h: 400, qty: 3),
                MakeRow(sort: 0, spec: "Spec A", w: 1000, h: 1200, qty: 6),
                MakeRow(sort: 1, spec: "",    w: 100,  h: 100,  qty: 1),   // partial — stays as a note
                MakeRow(sort: 3, spec: "Spec C", w: 800, h: 600, qty: 0),  // no qty — stays
            };

            var rows = InquiryBoqConversion.ToImportRows(items);

            Assert.Equal(2, rows.Count);
            Assert.Equal("Spec A", rows[0].SpecName);
            Assert.Equal("Spec B", rows[1].SpecName);
            Assert.Equal(1000, rows[0].WidthMm);
            Assert.Equal(1200, rows[0].HeightMm);
            Assert.Equal(6, rows[0].Qty);
        }

        [Fact]
        public void ToImportRows_UnknownCompleteBuild_IsNotImportedAsDefault()
        {
            var items = new List<InquiryLineItem>
            {
                MakeRow(sort: 0, spec: "Customer typed build", w: 700, h: 900, qty: 2)
            };
            items[0].GlassType = "Tempered";

            Assert.Empty(InquiryBoqConversion.ToImportRows(items));
        }

        [Fact]
        public void ToImportRows_NonPositiveRate_IsNotForcedIntoPricing()
        {
            var items = new List<InquiryLineItem>
            {
                MakeRow(sort: 0, spec: "Unpriced", w: 500, h: 500, qty: 2, rate: 0m),
                MakeRow(sort: 1, spec: "Priced",   w: 500, h: 500, qty: 2, rate: 95.5m),
            };

            var rows = InquiryBoqConversion.ToImportRows(items);

            Assert.False(rows[0].HasRate);
            Assert.True(rows[1].HasRate);
            Assert.Equal(95.5m, rows[1].RatePerSqm);
        }

        [Fact]
        public void ToImportRows_TrimsNamesAndColours_NullsBecomeEmpty()
        {
            var items = new List<InquiryLineItem>
            {
                new InquiryLineItem
                {
                    SpecName = "  Clear 6mm  ",
                    WidthMm = 400, HeightMm = 300, Qty = 1,
                    GlassType = "SGU",
                    OuterGlassColor = " Bronze ",
                    InnerGlassColor = null!,
                    SortOrder = 0
                }
            };

            var row = Assert.Single(InquiryBoqConversion.ToImportRows(items));
            Assert.Equal("Clear 6mm", row.SpecName);
            Assert.Equal("Bronze", row.OuterGlassColor);
            Assert.Equal("", row.InnerGlassColor);
            Assert.Equal("SGU", row.GlassType);
        }

        private static InquiryLineItem MakeRow(
            int sort, string spec, int w, int h, int qty, decimal rate = 85m) => new()
            {
                SpecName = spec,
                GlassType = "SGU",
                WidthMm = w,
                HeightMm = h,
                Qty = qty,
                RatePerSqm = rate,
                SortOrder = sort
            };
    }
}
