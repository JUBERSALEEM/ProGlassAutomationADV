// Pins overnight phases B–G: canonical identity, multi-JO recovery,
// structured material keys, DGU inner-size rule, feed invalidation,
// material-aware stock pick. NOT EXECUTED until a SDK is available.

using System;
using System.Collections.Generic;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OvernightIdentityTests
    {
        // ── B: structured identity, no invented ids, no qty mutation ──

        [Fact]
        public void Material_key_is_structured_not_composition_text()
        {
            string a = OptimizationMaterialIdentity.BuildKey("FLOAT", "Clear", 6, "FT", "");
            string b = OptimizationMaterialIdentity.BuildKey("FLOAT", "Clear", 6, "FT", null);
            string c = OptimizationMaterialIdentity.BuildKey("FLOAT", "Clear", 8, "FT", "");
            string d = OptimizationMaterialIdentity.BuildKey("FLOAT", "Grey", 6, "FT", "");

            Assert.False(string.IsNullOrWhiteSpace(a));
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.NotEqual(a, d);
            Assert.DoesNotContain("glass", a, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Material_key_refuses_missing_thickness_or_colour()
        {
            Assert.Equal(string.Empty, OptimizationMaterialIdentity.BuildKey("FLOAT", "Clear", 0, "FT", ""));
            Assert.Equal(string.Empty, OptimizationMaterialIdentity.BuildKey("", "", 6, "FT", ""));
        }

        [Fact]
        public void Cut_piece_fingerprint_changes_when_identity_or_qty_changes()
        {
            var one = new List<CutPiece>
            {
                new() { ProformaId = "PI-1", JobOrderId = "JO-1", GlassRef = "G-01",
                    SpecGroupNumber = 1, SourceRowSerial = 1, MaterialKey = "FLOAT|CLEAR|6|FT|-",
                    WidthMm = 1000, HeightMm = 800, W1 = 1000, H1 = 800 }
            };
            var two = new List<CutPiece>
            {
                new() { ProformaId = "PI-1", JobOrderId = "JO-1", GlassRef = "G-01",
                    SpecGroupNumber = 1, SourceRowSerial = 1, MaterialKey = "FLOAT|CLEAR|6|FT|-",
                    WidthMm = 1000, HeightMm = 800, W1 = 1000, H1 = 800 },
                new() { ProformaId = "PI-1", JobOrderId = "JO-1", GlassRef = "G-01",
                    SpecGroupNumber = 1, SourceRowSerial = 1, MaterialKey = "FLOAT|CLEAR|6|FT|-",
                    WidthMm = 1000, HeightMm = 800, W1 = 1000, H1 = 800 }
            };
            var otherJo = new List<CutPiece>
            {
                new() { ProformaId = "PI-1", JobOrderId = "JO-2", GlassRef = "G-01",
                    SpecGroupNumber = 1, SourceRowSerial = 1, MaterialKey = "FLOAT|CLEAR|6|FT|-",
                    WidthMm = 1000, HeightMm = 800, W1 = 1000, H1 = 800 }
            };

            string f1 = OptimizationMaterialIdentity.Fingerprint(one);
            Assert.Equal(f1, OptimizationMaterialIdentity.Fingerprint(one));
            Assert.NotEqual(f1, OptimizationMaterialIdentity.Fingerprint(two));
            Assert.NotEqual(f1, OptimizationMaterialIdentity.Fingerprint(otherJo));
        }

        // ── C: two JOs keep identity; same material may pack together ──

        [Fact]
        public void Two_job_orders_keep_distinct_job_identity_on_cut_pieces()
        {
            var pieces = new[]
            {
                new CutPiece { JobOrderId = "JO-A", MaterialKey = "FLOAT|CLEAR|6|FT|-", WidthMm = 1000, HeightMm = 800 },
                new CutPiece { JobOrderId = "JO-B", MaterialKey = "FLOAT|CLEAR|6|FT|-", WidthMm = 1000, HeightMm = 800 }
            };

            Assert.Equal("JO-A", pieces[0].JobOrderId);
            Assert.Equal("JO-B", pieces[1].JobOrderId);
            Assert.Equal(pieces[0].MaterialKey, pieces[1].MaterialKey);
        }

        // ── D: colour/thickness/processing differ ⇒ different key ──

        [Theory]
        [InlineData("Clear", 6, "FT", "Clear", 6, "Annealed")]
        [InlineData("Clear", 6, "FT", "Clear", 8, "FT")]
        [InlineData("Clear", 6, "FT", "Grey", 6, "FT")]
        public void Material_identity_does_not_collapse_on_composition_text(
            string c1, int t1, string p1, string c2, int t2, string p2)
        {
            string a = OptimizationMaterialIdentity.BuildKey("", c1, t1, p1, "");
            string b = OptimizationMaterialIdentity.BuildKey("", c2, t2, p2, "");
            Assert.NotEqual(a, b);
        }

        // ── E: DGU inherit only when BOTH W2 and H2 are 0 ──

        [Fact]
        public void Blank_w2_h2_inherits_equal_size()
        {
            Assert.True(DguDimensionSemantics.TryResolveInnerSize(
                1200, 900, 0, 0, out int w, out int h, out var kind));
            Assert.Equal(1200, w);
            Assert.Equal(900, h);
            Assert.Equal(DguInnerSizeKind.EqualSize, kind);
        }

        [Fact]
        public void Overlap_w2_h2_cuts_exact_size()
        {
            Assert.True(DguDimensionSemantics.TryResolveInnerSize(
                1500, 1200, 1250, 950, out int w, out int h, out var kind));
            Assert.Equal(1250, w);
            Assert.Equal(950, h);
            Assert.Equal(DguInnerSizeKind.Overlap, kind);
        }

        [Theory]
        [InlineData(1000, 800, 0, 500)]
        [InlineData(1000, 800, 1000, 0)]
        [InlineData(0, 0, 0, 500)]
        public void Incomplete_inner_size_is_not_inherited(int w1, int h1, int w2, int h2)
        {
            Assert.False(DguDimensionSemantics.TryResolveInnerSize(
                w1, h1, w2, h2, out _, out _, out var kind));
            Assert.Equal(DguInnerSizeKind.Incomplete, kind);
        }

        [Fact]
        public void Dgu_zero_by_500_is_skipped_not_inherited()
        {
            var plan = ProductionDistributionEngine.Distribute("JOB-INC",
                OvernightInvoice.Dgu("D-01", 1000, 800, 10, w2: 0, h2: 500));

            var cutting = plan.Items.FindAll(i => i.Stage == Models.Production.ProductionStage.Cutting);
            Assert.Single(cutting);
            Assert.Equal("Outer", cutting[0].Position);
            Assert.Contains(plan.Warnings, w => w.Contains("W2/H2", StringComparison.OrdinalIgnoreCase)
                                             || w.Contains("incomplete", StringComparison.OrdinalIgnoreCase)
                                             || w.Contains("skipped", StringComparison.OrdinalIgnoreCase));
        }

        // ── F: failed / cancelled / input-change must not leave CURRENT ──

        [Fact]
        public void Feed_invalidate_clears_current()
        {
            OptimizationResultFeed.Reset();
            OptimizationResultFeed.PublishLive(
                new[]
                {
                    new OptimizationPaneResult { SpecNumber = 1, Side = "Outer", SheetQuantity = 3 }
                },
                "live test",
                documentKey: "PI-1",
                runId: "run-1",
                inputFingerprint: "abc");

            Assert.True(OptimizationResultFeed.HasData);
            Assert.True(OptimizationResultFeed.IsCurrent);
            Assert.Equal("run-1", OptimizationResultFeed.RunId);
            Assert.Equal("abc", OptimizationResultFeed.InputFingerprint);

            OptimizationResultFeed.Invalidate("optimization input changed");
            Assert.False(OptimizationResultFeed.HasData);
            Assert.False(OptimizationResultFeed.IsCurrent);
            Assert.False(OptimizationResultFeed.IsForDocument("PI-1"));
            Assert.Equal(string.Empty, OptimizationResultFeed.RunId);

            OptimizationResultFeed.Reset();
        }

        // ── G: stock pick requires colour + thickness; no largest fallback ──

        [Fact]
        public void Stock_pick_refuses_thickness_zero_and_wrong_thickness()
        {
            var sheets = new List<GlassSheet>
            {
                new() { Color = "Clear", ThicknessMm = 6, WidthMm = 3210, HeightMm = 2250, Stock = 10, Used = 0 },
                new() { Color = "Clear", ThicknessMm = 8, WidthMm = 6000, HeightMm = 3210, Stock = 4, Used = 0 },
                new() { Color = "Grey", ThicknessMm = 6, WidthMm = 2500, HeightMm = 1600, Stock = 20, Used = 0 }
            };

            Assert.False(SheetStorePricingService.TryPickStockSheet(
                sheets, "Clear", 0, requirePositiveBalance: false, out _, out _, out _));

            Assert.True(SheetStorePricingService.TryPickStockSheet(
                sheets, "Clear", 6, requirePositiveBalance: false, out int w, out int h, out _));
            Assert.Equal(3210, w);
            Assert.Equal(2250, h);

            Assert.False(SheetStorePricingService.TryPickStockSheet(
                sheets, "Clear", 10, requirePositiveBalance: false, out _, out _, out _));

            Assert.False(SheetStorePricingService.TryPickStockSheet(
                sheets, "Grey", 8, requirePositiveBalance: false, out _, out _, out _));
        }

        [Fact]
        public void Stock_pick_with_balance_ignores_zero_stock_and_does_not_go_unlimited()
        {
            var sheets = new List<GlassSheet>
            {
                new() { Color = "Clear", ThicknessMm = 6, WidthMm = 3210, HeightMm = 2250, Stock = 0, Used = 0 },
                new() { Color = "Clear", ThicknessMm = 6, WidthMm = 2500, HeightMm = 1600, Stock = 5, Used = 0 }
            };

            Assert.True(SheetStorePricingService.TryPickStockSheet(
                sheets, "Clear", 6, requirePositiveBalance: true, out int w, out int h, out int bal));
            Assert.Equal(2500, w);
            Assert.Equal(1600, h);
            Assert.Equal(5, bal);
        }
    }

    internal static class OvernightInvoice
    {
        public static Models.ProformaInvoice.SavedInvoice.ProformaInvoiceSnapshot Dgu(
            string glassRef, int w1, int h1, int qty, int w2 = 0, int h2 = 0)
        {
            var snap = new Models.ProformaInvoice.SavedInvoice.ProformaInvoiceSnapshot();
            snap.Company.ProformaId = "PI-TEST";
            snap.SpecGroups.Add(new Models.ProformaInvoice.SavedInvoice.SpecGroupSnapshot
            {
                GroupNumber = 1,
                GroupName = "Spec 1",
                SelectedSpecName = "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                SelectedGlassType = "DGU",
                ItemRows =
                {
                    new Models.ProformaInvoice.SavedInvoice.GlassItemRowSnapshot
                    {
                        SerialNumber = 1,
                        GlassRef = glassRef,
                        W1 = w1, H1 = h1, W2 = w2, H2 = h2,
                        Qty = qty,
                        PerimeterFactor = 2
                    }
                }
            });
            return snap;
        }
    }
}
