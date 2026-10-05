using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.DeliveryNote
{
    /// <summary>
    /// Regression coverage for the OrderedQty self-heal (the "Ordered shows the
    /// balance / Prev shows 0 on the next DN" bug). OrderedQty is denormalized
    /// onto every DN line and copied master -> DN -> DN; the only durable fix is
    /// to re-derive it from the authoritative Job Order snapshot at load. These
    /// tests pin the map builder that drives that heal.
    /// </summary>
    public class DeliveryNoteOrderedQtyHealTests
    {
        private static JobOrderSnapshot BuildJo(params (int group, int[] qtys)[] groups)
        {
            var pi = new ProformaInvoiceSnapshot();
            foreach (var (group, qtys) in groups)
            {
                var sg = new SpecGroupSnapshot { GroupNumber = group };
                int serialInGroup = 1;
                foreach (var q in qtys)
                {
                    sg.ItemRows.Add(new GlassItemRowSnapshot
                    {
                        SerialNumber = serialInGroup++,
                        GlassRef = "G",
                        W1 = 1000,
                        H1 = 1000,
                        Qty = q
                    });
                }
                pi.SpecGroups.Add(sg);
            }
            return new JobOrderSnapshot { EmbeddedInvoice = pi };
        }

        [Fact]
        public void Map_uses_continuous_serial_across_groups_like_FromJobOrder()
        {
            // Group 1 has 2 lines, group 2 has 1 line. FromJobOrder assigns a
            // CONTINUOUS global serial (1,2,3) — the map must key the same way,
            // otherwise the heal lookup misses and OrderedQty is never fixed.
            var jo = BuildJo((1, new[] { 76, 40 }), (2, new[] { 12 }));

            var map = DeliveryNoteMapper.BuildOrderedQtyMap(jo);

            Assert.Equal(76, map[DeliveryNoteWorkflow.LineKey(1, 1)]);
            Assert.Equal(40, map[DeliveryNoteWorkflow.LineKey(1, 2)]);
            Assert.Equal(12, map[DeliveryNoteWorkflow.LineKey(2, 3)]);
            Assert.Equal(3, map.Count);
        }

        [Fact]
        public void Map_returns_true_ordered_not_balance()
        {
            // The whole point: even if a stored DN line said OrderedQty = 64
            // (the balance), the authoritative map returns 76 (the order).
            var jo = BuildJo((1, new[] { 76 }));

            var map = DeliveryNoteMapper.BuildOrderedQtyMap(jo);

            Assert.Equal(76, map[DeliveryNoteWorkflow.LineKey(1, 1)]);
            Assert.NotEqual(64, map[DeliveryNoteWorkflow.LineKey(1, 1)]);
        }

        [Fact]
        public void Map_dedups_split_rows_by_max_qty()
        {
            // A serial duplicated across an outer/inner split must collapse to
            // one line whose OrderedQty is the MAX (not the SUM) — same rule as
            // FromJobOrder, so the healed value matches the stored line.
            var pi = new ProformaInvoiceSnapshot();
            var sg = new SpecGroupSnapshot { GroupNumber = 1 };
            sg.ItemRows.Add(new GlassItemRowSnapshot
            { SerialNumber = 1, GlassRef = "outer", W1 = 900, H1 = 900, Qty = 30 });
            sg.ItemRows.Add(new GlassItemRowSnapshot
            { SerialNumber = 1, GlassRef = "inner", W1 = 900, H1 = 900, Qty = 30 });
            pi.SpecGroups.Add(sg);
            var jo = new JobOrderSnapshot { EmbeddedInvoice = pi };

            var map = DeliveryNoteMapper.BuildOrderedQtyMap(jo);

            Assert.Single(map);
            Assert.Equal(30, map[DeliveryNoteWorkflow.LineKey(1, 1)]);
        }

        [Fact]
        public void Map_skips_blank_and_zero_qty_rows()
        {
            var pi = new ProformaInvoiceSnapshot();
            var sg = new SpecGroupSnapshot { GroupNumber = 1 };
            // blank placeholder row (no ref, no dims) — skipped
            sg.ItemRows.Add(new GlassItemRowSnapshot
            { SerialNumber = 1, GlassRef = "", W1 = 0, H1 = 0, Qty = 5 });
            // zero-qty row — skipped
            sg.ItemRows.Add(new GlassItemRowSnapshot
            { SerialNumber = 2, GlassRef = "G", W1 = 1000, H1 = 1000, Qty = 0 });
            // real row — kept, first real serial => 1 (continuous counter only
            // advances on kept rows, matching FromJobOrder)
            sg.ItemRows.Add(new GlassItemRowSnapshot
            { SerialNumber = 3, GlassRef = "G", W1 = 1000, H1 = 1000, Qty = 76 });
            pi.SpecGroups.Add(sg);
            var jo = new JobOrderSnapshot { EmbeddedInvoice = pi };

            var map = DeliveryNoteMapper.BuildOrderedQtyMap(jo);

            Assert.Single(map);
            Assert.Equal(76, map[DeliveryNoteWorkflow.LineKey(1, 1)]);
        }

        [Fact]
        public void Map_is_empty_for_null_snapshot()
        {
            Assert.Empty(DeliveryNoteMapper.BuildOrderedQtyMap(null));
        }
    }
}
