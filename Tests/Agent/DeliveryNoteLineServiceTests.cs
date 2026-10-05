// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DeliveryNoteLineServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 6b + Phase 7 — DeliveryNoteLineService
// VERSION: Phase 7 — 2026-08-21
//
// Tests pure domain logic extracted from DeliveryNoteDetailViewModel 2186 LOC
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DeliveryNoteLineServiceTests
    {
        [Fact]
        public void SpecKey_builds_from_group_and_serial()
        {
            var item = new DeliveryNoteItem { SpecGroupNumber = 2, SerialNumber = 5 };
            var key = DeliveryNoteLineService.SpecKey(item);
            Assert.Equal("2|5", key);
        }

        [Fact]
        public void GlassKey_builds_from_dimensions()
        {
            var item = new DeliveryNoteItem { GlassRef = "G-001", W1 = 1000, H1 = 2000, W2 = 0, H2 = 0 };
            var key = DeliveryNoteLineService.GlassKey(item);
            Assert.Equal("G|G-001|1000|2000|0|0", key);
        }

        [Fact]
        public void BuildDeliveredLookup_accumulates_qty()
        {
            var items = new[]
            {
                new DeliveryNoteItem { SpecGroupNumber = 1, SerialNumber = 1, GlassRef = "A", W1 = 100, H1 = 100, DeliveredQty = 5 },
                new DeliveryNoteItem { SpecGroupNumber = 1, SerialNumber = 1, GlassRef = "A", W1 = 100, H1 = 100, DeliveredQty = 3 },
                new DeliveryNoteItem { SpecGroupNumber = 1, SerialNumber = 2, GlassRef = "B", W1 = 200, H1 = 200, DeliveredQty = 2 },
            };

            var map = DeliveryNoteLineService.BuildDeliveredLookup(items);

            Assert.Equal(8, map["1|1"]);
            Assert.Equal(2, map["1|2"]);
        }

        [Fact]
        public void LookupDelivered_prefers_spec_key_over_glass_key()
        {
            var map = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["1|1"] = 10,
                ["G|REF|100|200|0|0"] = 5
            };

            var item = new DeliveryNoteItem { SpecGroupNumber = 1, SerialNumber = 1, GlassRef = "REF", W1 = 100, H1 = 200 };
            var qty = DeliveryNoteLineService.LookupDelivered(map, item);

            Assert.Equal(10, qty); // spec key wins
        }

        [Fact]
        public void ShouldDisplayRow_master_shows_only_balance()
        {
            var row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                SpecGroupNumber = 1, SerialNumber = 1,
                GlassRef = "G-001", W1 = 1000, H1 = 1000,
                OrderedQty = 10, PreviouslyDeliveredQty = 10, DeliveredQty = 0, BalanceQty = 0
            });

            // Master DN, ShowFullMaster=false, Balance=0 → hidden
            Assert.False(DeliveryNoteLineService.ShouldDisplayRow(row, isMasterDn: true, showFullMaster: false));

            // Same row but Balance>0 → visible
            row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                SpecGroupNumber = 1, SerialNumber = 1,
                GlassRef = "G-001", W1 = 1000, H1 = 1000,
                OrderedQty = 10, PreviouslyDeliveredQty = 5, DeliveredQty = 0, BalanceQty = 5
            });
            Assert.True(DeliveryNoteLineService.ShouldDisplayRow(row, isMasterDn: true, showFullMaster: false));

            // ShowFullMaster=true → always visible even if balance 0
            Assert.True(DeliveryNoteLineService.ShouldDisplayRow(row, isMasterDn: true, showFullMaster: true));
        }

        [Fact]
        public void ShouldDisplayRow_real_hides_phantom()
        {
            var phantom = new DnItemRowViewModel(new DeliveryNoteItem
            {
                SpecGroupNumber = 1, SerialNumber = 1,
                GlassRef = "", W1 = 0, H1 = 0,
                OrderedQty = 0, DeliveredQty = 0, BalanceQty = 0
            });

            // Real DN, phantom row (0,0) → hidden
            Assert.False(DeliveryNoteLineService.ShouldDisplayRow(phantom, isMasterDn: false, showFullMaster: false));
        }

        [Fact]
        public void CalculateTotals_sums_correctly()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { OrderedQty = 10, PreviouslyDeliveredQty = 2, DeliveredQty = 3, BalanceQty = 5 }),
                new DnItemRowViewModel(new DeliveryNoteItem { OrderedQty = 5, PreviouslyDeliveredQty = 1, DeliveredQty = 1, BalanceQty = 3 }),
            };

            var totals = DeliveryNoteLineService.CalculateTotals(rows);

            Assert.Equal(15, totals.Ordered);
            Assert.Equal(3, totals.PrevDelivered);
            Assert.Equal(4, totals.Delivered);
            Assert.Equal(8, totals.Balance);
        }

        [Fact]
        public void CalculateProgressPercent_clamps_to_100()
        {
            // Ordered 10, Prev 5, Delivered 3 → 80%
            var pct = DeliveryNoteLineService.CalculateProgressPercent(10, 5, 3);
            Assert.Equal(80.0, pct);

            // Over-delivered → clamp to 100
            pct = DeliveryNoteLineService.CalculateProgressPercent(10, 8, 5);
            Assert.Equal(100.0, pct);

            // Zero ordered → 0
            pct = DeliveryNoteLineService.CalculateProgressPercent(0, 0, 0);
            Assert.Equal(0, pct);
        }

        [Fact]
        public void FillAllRemaining_sets_delivered_to_balance()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { OrderedQty = 10, PreviouslyDeliveredQty = 0, DeliveredQty = 0, BalanceQty = 5 }),
                new DnItemRowViewModel(new DeliveryNoteItem { OrderedQty = 10, PreviouslyDeliveredQty = 0, DeliveredQty = 0, BalanceQty = 0 }),
            };

            DeliveryNoteLineService.FillAllRemaining(rows);

            Assert.Equal(5, rows[0].DeliveredQty);
            Assert.Equal(0, rows[1].DeliveredQty);
        }

        [Fact]
        public void ResetAllQty_sets_all_to_zero()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 5 }),
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 3 }),
            };

            DeliveryNoteLineService.ResetAllQty(rows);

            Assert.All(rows, r => Assert.Equal(0, r.DeliveredQty));
        }

        [Fact]
        public void HasAnyDelivery_detects_delivered()
        {
            var empty = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 0 }),
            };
            var withDelivery = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 0 }),
                new DnItemRowViewModel(new DeliveryNoteItem { DeliveredQty = 1 }),
            };

            Assert.False(DeliveryNoteLineService.HasAnyDelivery(empty));
            Assert.True(DeliveryNoteLineService.HasAnyDelivery(withDelivery));
        }

        // ── Regression: KPI totals must track LIVE row edits ──────────────
        // Bug: the detail VM recalculated the "This DN" / Balance / Progress
        // KPIs from the stored entity items instead of the editable row VMs,
        // so typing a delivered qty left the strip frozen. These lock in the
        // row VM being a live source of truth and CalculateTotals(rows)
        // reflecting an edit immediately.

        [Fact]
        public void EditingDeliveredQty_updates_row_balance_live()
        {
            var row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                OrderedQty = 31, PreviouslyDeliveredQty = 0, DeliveredQty = 0
            });

            Assert.Equal(31, row.BalanceQty);   // nothing delivered yet

            row.DeliveredQty = 12;              // user marks 12 ready on this DN
            Assert.Equal(12, row.DeliveredQty);
            Assert.Equal(19, row.BalanceQty);   // 31 - 0 - 12
        }

        [Fact]
        public void CalculateTotals_over_rows_reflects_edits()
        {
            var rows = new[]
            {
                new DnItemRowViewModel(new DeliveryNoteItem { OrderedQty = 31, PreviouslyDeliveredQty = 0, DeliveredQty = 0 }),
                new DnItemRowViewModel(new DeliveryNoteItem { OrderedQty = 10, PreviouslyDeliveredQty = 4, DeliveredQty = 0 }),
            };

            var before = DeliveryNoteLineService.CalculateTotals(rows);
            Assert.Equal(41, before.Ordered);
            Assert.Equal(4, before.PrevDelivered);
            Assert.Equal(0, before.Delivered);
            Assert.Equal(37, before.Balance);   // (31-0)+(10-4)

            // Mark ready for dispatch on this DN
            rows[0].DeliveredQty = 31;
            rows[1].DeliveredQty = 6;

            var after = DeliveryNoteLineService.CalculateTotals(rows);
            Assert.Equal(41, after.Ordered);        // ordered never changes
            Assert.Equal(4, after.PrevDelivered);   // prev never changes on this DN
            Assert.Equal(37, after.Delivered);      // this DN = 31 + 6
            Assert.Equal(0, after.Balance);         // ordered - prev - thisDn
        }

        [Fact]
        public void DeliveredQty_is_clamped_to_remaining()
        {
            // Ordered 31, already 20 delivered on prior DNs → at most 11 here.
            var row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                OrderedQty = 31, PreviouslyDeliveredQty = 20, DeliveredQty = 0
            });

            row.DeliveredQty = 999;             // over-delivery attempt
            Assert.Equal(11, row.DeliveredQty); // clamped to remaining
            Assert.Equal(0, row.BalanceQty);

            row.DeliveredQty = -5;              // negative attempt
            Assert.Equal(0, row.DeliveredQty);
            Assert.Equal(11, row.BalanceQty);
        }

        // ── Production gate (2026-09-11) ────────────────────────────────
        // A production-managed row can only deliver pieces the floor has
        // finished — completed production flows into delivery notes.

        [Fact]
        public void Production_cap_limits_delivered_qty()
        {
            var row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                SpecGroupNumber = 1, SerialNumber = 1,
                GlassRef = "G-101", W1 = 1200, H1 = 900,
                OrderedQty = 24, PreviouslyDeliveredQty = 0, DeliveredQty = 0
            }, productionCap: 5);

            row.DeliveredQty = 99; // floor tries to deliver everything
            Assert.Equal(5, row.DeliveredQty); // only 5 pieces tempered
            Assert.True(row.IsProductionLimited);
        }

        [Fact]
        public void Without_production_cap_full_job_balance_remains_deliverable()
        {
            var row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                SpecGroupNumber = 1, SerialNumber = 1,
                GlassRef = "G-101", W1 = 1200, H1 = 900,
                OrderedQty = 24, PreviouslyDeliveredQty = 0, DeliveredQty = 0
            });

            row.DeliveredQty = 99;
            Assert.Equal(24, row.DeliveredQty);
            Assert.False(row.IsProductionLimited);
        }

        [Fact]
        public void Production_cap_never_exceeds_job_balance()
        {
            // 20 ordered, 18 already delivered, production says 10 ready —
            // only the 2 remaining ordered pieces can ever go out.
            var row = new DnItemRowViewModel(new DeliveryNoteItem
            {
                SpecGroupNumber = 1, SerialNumber = 1,
                GlassRef = "G-101", W1 = 1200, H1 = 900,
                OrderedQty = 20, PreviouslyDeliveredQty = 18, DeliveredQty = 0
            }, productionCap: 10);

            row.DeliveredQty = 99;
            Assert.Equal(2, row.DeliveredQty);
        }
    }
}
