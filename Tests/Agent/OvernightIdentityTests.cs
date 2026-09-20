// Pins overnight phases B–G: canonical identity, multi-JO recovery,
// structured material keys, DGU inner-size rule, feed invalidation,
// material-aware stock pick. NOT EXECUTED until a SDK is available.

using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.Equal(string.Empty, OptimizationMaterialIdentity.BuildKey("FLOAT", "", 6, "FT", ""));
            Assert.Equal(string.Empty, OptimizationMaterialIdentity.BuildKey("FLOAT", "   ", 6, "FT", ""));
        }

        [Fact]
        public void Identity_version_zero_or_negative_is_never_current_valid()
        {
            Assert.False(OptimizationMaterialIdentity.IsCurrentIdentityVersion(0));
            Assert.False(OptimizationMaterialIdentity.IsCurrentIdentityVersion(-1));
            Assert.False(OptimizationSessionIdentity.IsCurrentIdentityVersion(0));
            Assert.True(OptimizationSessionIdentity.IsCurrentIdentityVersion(1));
            Assert.False(OptimizationSessionIdentity.IsCurrentValid(new OptimizationSessionPiecesPayload
            {
                IdentityVersion = 0,
                RunId = "run",
                InputFingerprint = "abc"
            }));
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

        // ── Hardening: session identity persist / restore ──

        [Fact]
        public void New_session_save_restore_keeps_identity_unchanged()
        {
            var original = new OptimizationSessionPieceRecord
            {
                GlassRef = "6mm Clear FT Glass",
                PieceLabel = "G-01",
                Position = "Outer",
                W = "1000",
                H = "800",
                Qty = "4",
                ProformaId = "PI-2026-001",
                JobOrderId = "JO-2026-001",
                SpecGroupNumber = 1,
                SourceRowSerial = 3,
                Color = "Clear",
                ThicknessMm = 6,
                Processing = "FT",
                MaterialKey = OptimizationMaterialIdentity.BuildKey("", "Clear", 6, "FT", ""),
                SheetStoreSku = OptimizationMaterialIdentity.StockSku("Clear", 6, 3210, 2250),
                W1 = 1000,
                H1 = 800
            };

            string json = OptimizationSessionIdentity.Serialize(new OptimizationSessionPiecesPayload
            {
                IdentityVersion = OptimizationSessionIdentity.CurrentIdentityVersion,
                RunId = "run-abc",
                DocumentKey = "PI-2026-001",
                Pieces = new List<OptimizationSessionPieceRecord> { original }
            });

            Assert.StartsWith("{", json.Trim());
            var restored = OptimizationSessionIdentity.Parse(json);

            Assert.True(OptimizationSessionIdentity.IsCurrentValid(restored));
            Assert.Equal("run-abc", restored.RunId);
            Assert.Equal(original.ProformaId, restored.Pieces[0].ProformaId);
            Assert.Equal(original.JobOrderId, restored.Pieces[0].JobOrderId);
            Assert.Equal(original.SpecGroupNumber, restored.Pieces[0].SpecGroupNumber);
            Assert.Equal(original.SourceRowSerial, restored.Pieces[0].SourceRowSerial);
            Assert.Equal(original.MaterialKey, restored.Pieces[0].MaterialKey);
            Assert.Equal(original.SheetStoreSku, restored.Pieces[0].SheetStoreSku);
            Assert.Equal(original.Color, restored.Pieces[0].Color);
            Assert.Equal(original.ThicknessMm, restored.Pieces[0].ThicknessMm);
            Assert.True(OptimizationSessionIdentity.MatchesCurrentInput(
                restored, OptimizationSessionIdentity.FingerprintRows(restored.Pieces)));
        }

        [Fact]
        public void Legacy_array_session_is_unknown_and_does_not_match_current_input()
        {
            const string legacy = "[{\"GlassRef\":\"6mm Clear FT Glass\",\"PieceLabel\":\"G-01\"," +
                "\"Position\":\"Outer\",\"W\":\"1000\",\"H\":\"800\",\"Qty\":\"4\"}]";

            var parsed = OptimizationSessionIdentity.Parse(legacy);
            Assert.Equal(0, parsed.IdentityVersion);
            Assert.Equal(string.Empty, parsed.RunId);
            Assert.Equal(string.Empty, parsed.InputFingerprint);
            Assert.False(OptimizationSessionIdentity.IsCurrentValid(parsed));

            var current = new List<OptimizationSessionPieceRecord>
            {
                new()
                {
                    GlassRef = "6mm Clear FT Glass",
                    W = "1000", H = "800", Qty = "4",
                    Color = "Clear", ThicknessMm = 6,
                    MaterialKey = OptimizationMaterialIdentity.BuildKey("", "Clear", 6, "FT", "")
                }
            };
            string now = OptimizationSessionIdentity.FingerprintRows(current);
            Assert.False(OptimizationSessionIdentity.MatchesCurrentInput(parsed, now));
        }

        [Fact]
        public void Changed_input_fingerprint_does_not_match_saved_run()
        {
            var a = new OptimizationSessionPieceRecord
            {
                ProformaId = "PI-1", JobOrderId = "JO-1", MaterialKey = "FLOAT|CLEAR|6|FT|-",
                W = "1000", H = "800", Qty = "1", Color = "Clear", ThicknessMm = 6
            };
            var saved = new OptimizationSessionPiecesPayload
            {
                IdentityVersion = 1,
                RunId = "run-1",
                InputFingerprint = OptimizationSessionIdentity.FingerprintRows(new[] { a }),
                Pieces = new List<OptimizationSessionPieceRecord> { a }
            };
            var b = new OptimizationSessionPieceRecord
            {
                ProformaId = "PI-1", JobOrderId = "JO-1", MaterialKey = "FLOAT|CLEAR|8|FT|-",
                W = "1000", H = "800", Qty = "1", Color = "Clear", ThicknessMm = 8
            };
            Assert.False(OptimizationSessionIdentity.MatchesCurrentInput(
                saved, OptimizationSessionIdentity.FingerprintRows(new[] { b })));
        }

        [Fact]
        public void DguLam_third_rack_thickness_follows_lam_inner_not_settings()
        {
            var group10 = OvernightInvoice.DguLamGroup(
                "8mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52mm Clear PVB + 10mm Clear FT Glass");
            var group12 = OvernightInvoice.DguLamGroup(
                "8mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52mm Clear PVB + 12mm Clear FT Glass");

            var occ10 = ProductionDistributionEngine.ResolvePaneOccurrences("DguLam", group10);
            var occ12 = ProductionDistributionEngine.ResolvePaneOccurrences("DguLam", group12);

            Assert.Equal(3, occ10.Count);
            Assert.Equal("Outer", occ10[0].Position);
            Assert.Equal("Lam Outer", occ10[1].Position);
            Assert.Equal("Lam Inner", occ10[2].Position);
            Assert.Equal(8, occ10[0].ThicknessMm);
            Assert.Equal(6, occ10[1].ThicknessMm);
            Assert.Equal(10, occ10[2].ThicknessMm);
            Assert.Equal(12, occ12[2].ThicknessMm);

            int settingsMm = 6;
            int third10 = OptimizationMaterialIdentity.DguLamThirdRackThicknessMm(occ10[2].ThicknessMm);
            int third12 = OptimizationMaterialIdentity.DguLamThirdRackThicknessMm(occ12[2].ThicknessMm);
            Assert.Equal(10, third10);
            Assert.Equal(12, third12);
            Assert.NotEqual(settingsMm, third10);
            Assert.NotEqual(occ10[0].ThicknessMm, third10);
            Assert.NotEqual(occ10[1].ThicknessMm, third10);
            Assert.NotEqual(third10, third12);
            Assert.Equal(0, OptimizationMaterialIdentity.DguLamThirdRackThicknessMm(0));
        }

        [Fact]
        public void Empty_colour_is_unresolved_and_not_invented_from_glassref()
        {
            string fromBlank = OptimizationMaterialIdentity.BuildKey("", "", 6, "FT", "");
            string fromGlassSentence = OptimizationMaterialIdentity.BuildKey(
                "6mm Clear FT Glass", "", 0, "", "");
            Assert.Equal(string.Empty, fromBlank);
            Assert.Equal(string.Empty, fromGlassSentence);
            Assert.Equal(string.Empty, OptimizationMaterialIdentity.StockSku("", 6, 3210, 2250));
        }

        [Fact]
        public void Live_publish_without_run_id_is_not_current_for_step6()
        {
            OptimizationResultFeed.Reset();
            OptimizationResultFeed.PublishLive(
                new[] { new OptimizationPaneResult { SpecNumber = 1, Side = "Outer", SheetQuantity = 3 } },
                "legacy restore");
            Assert.True(OptimizationResultFeed.HasData);
            Assert.False(OptimizationResultFeed.IsCurrent);
            Assert.False(OptimizationResultFeed.IsForDocument("PI-1"));
            OptimizationResultFeed.Reset();
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

        public static Models.ProformaInvoice.SavedInvoice.SpecGroupSnapshot DguLamGroup(string specName) =>
            new()
            {
                GroupNumber = 1,
                SelectedSpecName = specName,
                SelectedGlassType = "DGU+LAM",
                ThirdSheetWidthMm = 3210,
                ThirdSheetHeightMm = 2250
            };
    }
}
