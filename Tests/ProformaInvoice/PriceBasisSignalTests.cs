// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/PriceBasisSignalTests.cs
// PROJECT: ProGlassAutomation.Tests
// PHASE 4 — regression net for the Phase 3 staleness signal.
//
// What is pinned:
//   1. GlassPriceStamper.Classify is THE single classification home:
//      the whole four-state matrix, plus determinism (same inputs → same
//      verdict — a report must never flicker between refreshes).
//   2. HasMoved stays a strict projection of Classify == Moved (the two
//      consumers cannot drift apart).
//   3. The fail-safe contract: an unreadable LIVE basis always degrades
//      the answer to "we don't know" — never a false alarm, never an
//      exception.
//   4. ProfitLossSnapshot JSON round-trip: the staleness fields AND the
//      Phase 1 production-rate provenance fields survive the same
//      System.Text.Json pipeline the mapper persists through — a property
//      rename that forgets the wire format is caught here, not in payroll.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice;
using System.Text.Json;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class PriceBasisSignalTests
    {
        private const string Stamp = "638512345678901234:42";

        // ── 1. classification matrix ─────────────────────────────────────
        [Fact]
        public void Classify_MatchingStamps_IsCurrent()
        {
            Assert.Equal(GlassPriceStamper.BasisState.Current,
                GlassPriceStamper.Classify(Stamp, Stamp));
        }

        [Fact]
        public void Classify_DifferentStamps_IsMoved()
        {
            Assert.Equal(GlassPriceStamper.BasisState.Moved,
                GlassPriceStamper.Classify(Stamp, "638512345678901235:42"));
            // count-only drift (sheet deleted) counts as moved as well
            Assert.Equal(GlassPriceStamper.BasisState.Moved,
                GlassPriceStamper.Classify(Stamp, "638512345678901234:41"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Classify_UnstampedSnapshot_IsUnconfirmed_NeverFresh(string? saved)
        {
            Assert.Equal(GlassPriceStamper.BasisState.Unconfirmed,
                GlassPriceStamper.Classify(saved, Stamp));
        }

        [Fact]
        public void Classify_UnreadableBasis_IsUnreadable_NeverAnAlarm()
        {
            Assert.Equal(GlassPriceStamper.BasisState.BasisUnreadable,
                GlassPriceStamper.Classify(Stamp, ""));
            Assert.Equal(GlassPriceStamper.BasisState.BasisUnreadable,
                GlassPriceStamper.Classify(null, null));
            // The FAIL-SAFE rule: an unreadable basis plus a stale-looking
            // saved stamp must still produce NO alarm.
            Assert.Equal(GlassPriceStamper.BasisState.BasisUnreadable,
                GlassPriceStamper.Classify("anything", " "));
        }

        // ── determinism: reports refresh constantly — same in, same out ──
        [Fact]
        public void Classify_IsDeterministic()
        {
            Assert.Equal(
                GlassPriceStamper.Classify(Stamp, "1:1"),
                GlassPriceStamper.Classify(Stamp, "1:1"));
        }

        // ── 2. HasMoved stays glued to the classifier ────────────────────
        [Fact]
        public void HasMoved_IsExactly_ClassifyMoved()
        {
            Assert.True(GlassPriceStamper.HasMoved(Stamp, "1:1"));
            Assert.False(GlassPriceStamper.HasMoved(Stamp, Stamp));
            Assert.False(GlassPriceStamper.HasMoved(null, Stamp));
            Assert.False(GlassPriceStamper.HasMoved(Stamp, ""));
        }

        // ── 4. snapshot wire-format round-trip ───────────────────────────
        [Fact]
        public void ProfitLossSnapshot_StalenessFields_SurviveJsonRoundTrip()
        {
            var original = new ProfitLossSnapshot
            {
                GlassPriceStamp = Stamp,
                ComputedAtUtc = new System.DateTime(2026, 9, 4, 10, 30, 0,
                    System.DateTimeKind.Utc),
                NetExVat = 1234.5m
            };

            string json = JsonSerializer.Serialize(original);
            var roundTrip = JsonSerializer.Deserialize<ProfitLossSnapshot>(json)!;

            Assert.Equal(Stamp, roundTrip.GlassPriceStamp);
            Assert.Equal(original.ComputedAtUtc, roundTrip.ComputedAtUtc);
            Assert.Equal(1234.5m, roundTrip.NetExVat);
        }

        [Fact]
        public void ProformaInvoiceSnapshot_ProductionProvenance_SurvivesJsonRoundTrip()
        {
            // Phase 1 provenance fields travel ON THE ROOT snapshot's
            // spec groups — a property rename that forgets the wire
            // format would silently re-pin every group to section 1's
            // production inputs. Caught here.
            var original = new ProformaInvoiceSnapshot
            {
                SpecGroups =
                {
                    new SpecGroupSnapshot
                    {
                        Step4SpacerPricePerSqm = 7.5m,
                        Step4InterlayerCostPerSqm = 3.25m,
                        Step4CuttingCostPerSqm = 2.2m,
                        Step4TemperingCostPerSqm = 1.4m
                    }
                }
            };

            string json = JsonSerializer.Serialize(original);
            var roundTrip = JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(json)!;

            var group = Assert.Single(roundTrip.SpecGroups);
            Assert.Equal(7.5m, group.Step4SpacerPricePerSqm);
            Assert.Equal(3.25m, group.Step4InterlayerCostPerSqm);
            Assert.Equal(2.2m, group.Step4CuttingCostPerSqm);
            Assert.Equal(1.4m, group.Step4TemperingCostPerSqm);
        }

        [Fact]
        public void ProfitLossSnapshot_LegacyJson_WithoutStaleness_DeserializesEmpty()
        {
            // Pre-Phase-3 payloads must load with an EMPTY stamp (unknown),
            // not crash and not synthesize a fake one.
            const string legacy =
                "{\"GrossVatInclusive\":1050,\"NetExVat\":1000,\"Profit\":250}";

            var snap = JsonSerializer.Deserialize<ProfitLossSnapshot>(legacy)!;

            Assert.Equal("", snap.GlassPriceStamp);
            Assert.Null(snap.ComputedAtUtc);
            Assert.Equal(250m, snap.Profit);
        }
    }
}
