// Executable (no SDK here — NOT EXECUTED) quantity + snapshot contracts.

using System.Text.Json;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.Step5Grid;
using ProGlassAutomation.Utilities;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class QtyParseAndSnapshotTests
    {
        [Theory]
        [InlineData("17", 17)]
        [InlineData("17.0", 17)]
        [InlineData("17.00", 17)]
        [InlineData("1,000", 1000)]
        [InlineData("1000", 1000)]
        [InlineData("17 pcs", 17)]
        [InlineData("17 PCS", 17)]
        [InlineData(" 17 pcs ", 17)]
        [InlineData("1,000 pcs", 1000)]
        [InlineData("1,000.0", 1000)]
        public void TryParsePieceQty_accepts_whole_formatted_counts(string raw, int expected)
        {
            Assert.True(TextParsing.TryParsePieceQty(raw, out int qty));
            Assert.Equal(expected, qty);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("abc")]
        [InlineData("17.5")]
        [InlineData("17.9")]
        [InlineData("1.5")]
        [InlineData("1,000.5")]
        public void TryParsePieceQty_rejects_blank_invalid_and_fractional(string? raw)
        {
            Assert.False(TextParsing.TryParsePieceQty(raw, out _));
        }

        [Fact]
        public void Snapshot_round_trip_keeps_asymmetric_panes_and_qty_17()
        {
            var row = new GlassItemRowSnapshot
            {
                SerialNumber = 1,
                GlassRef = "D-17",
                W1 = 3210,
                H1 = 2250,
                W2 = 3000,
                H2 = 1800,
                Qty = 17,
                PerimeterFactor = 2
            };

            string json = JsonSerializer.Serialize(row);
            var back = JsonSerializer.Deserialize<GlassItemRowSnapshot>(json);

            Assert.NotNull(back);
            Assert.Equal(3210, back!.W1);
            Assert.Equal(2250, back.H1);
            Assert.Equal(3000, back.W2);
            Assert.Equal(1800, back.H2);
            Assert.Equal(17, back.Qty);
            Assert.DoesNotContain("Sqm1", json);
        }

        [Fact]
        public void Corrupt_pi_json_deserializes_to_empty_not_null()
        {
            var entity = new SavedProformaInvoice { FullSnapshotJson = "{ this is not json" };
            var snap = ProformaInvoiceMapper.DeserializeSnapshot(entity);
            Assert.NotNull(snap);
            Assert.Empty(snap.SpecGroups);
        }

        [Fact]
        public void Import_then_snapshot_shape_preserves_dgu_row()
        {
            var imported = CutListImportParser.ParseDelimitedText(
                "REF,W1,H1,W2,H2,PCS\nD17,3210,2250,3000,1800,17 pcs\n");
            Assert.Single(imported.Rows);

            var snap = new GlassItemRowSnapshot
            {
                GlassRef = imported.Rows[0].GlassRef,
                W1 = imported.Rows[0].W1,
                H1 = imported.Rows[0].H1,
                W2 = imported.Rows[0].W2,
                H2 = imported.Rows[0].H2,
                Qty = imported.Rows[0].Qty
            };

            Assert.Equal("D17", snap.GlassRef);
            Assert.Equal(3210, snap.W1);
            Assert.Equal(2250, snap.H1);
            Assert.Equal(3000, snap.W2);
            Assert.Equal(1800, snap.H2);
            Assert.Equal(17, snap.Qty);
        }
    }
}
