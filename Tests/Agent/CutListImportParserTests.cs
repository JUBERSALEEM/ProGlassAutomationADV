using System.Collections.Generic;
using ProGlassAutomation.Services.ProformaInvoice.Step5Grid;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class CutListImportParserTests
    {
        [Fact]
        public void Headered_csv_maps_ref_w1_h1_qty()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "Glass Ref,W1,H1,Qty\nA1,1000,1200,2\nB2,800,600,1\n");

            Assert.Equal(2, result.Rows.Count);
            Assert.Equal("A1", result.Rows[0].GlassRef);
            Assert.Equal(1000, result.Rows[0].W1);
            Assert.Equal(1200, result.Rows[0].H1);
            Assert.Equal(2, result.Rows[0].Qty);
            Assert.Equal("header", result.Layout);
        }

        [Fact]
        public void Headered_csv_accepts_w2_h2()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "REF,W1,H1,W2,H2,QTY\nX,1100,900,1000,800,3\n");

            Assert.Single(result.Rows);
            Assert.Equal(1100, result.Rows[0].W1);
            Assert.Equal(900, result.Rows[0].H1);
            Assert.Equal(1000, result.Rows[0].W2);
            Assert.Equal(800, result.Rows[0].H2);
            Assert.Equal(3, result.Rows[0].Qty);
        }

        [Fact]
        public void Positional_ref_w_h_qty()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "A1\t1000\t1200\t2\n");

            Assert.Single(result.Rows);
            Assert.Equal("A1", result.Rows[0].GlassRef);
            Assert.Equal(1000, result.Rows[0].W1);
            Assert.Equal(1200, result.Rows[0].H1);
            Assert.Equal(2, result.Rows[0].Qty);
        }

        [Fact]
        public void Dimension_token_1000x1200()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "A1 1000x1200 4\n");

            Assert.Single(result.Rows);
            Assert.Equal("A1", result.Rows[0].GlassRef);
            Assert.Equal(1000, result.Rows[0].W1);
            Assert.Equal(1200, result.Rows[0].H1);
            Assert.Equal(4, result.Rows[0].Qty);
        }

        [Fact]
        public void Size_header_column()
        {
            var table = new List<IReadOnlyList<string>>
            {
                new[] { "Mark", "Size", "Qty" },
                new[] { "P1", "900 x 2100", "6" }
            };

            var result = CutListImportParser.ParseTable(table);
            Assert.Single(result.Rows);
            Assert.Equal("P1", result.Rows[0].GlassRef);
            Assert.Equal(900, result.Rows[0].W1);
            Assert.Equal(2100, result.Rows[0].H1);
            Assert.Equal(6, result.Rows[0].Qty);
        }

        [Fact]
        public void Json_array_from_scanner()
        {
            var result = CutListImportParser.ParseJsonArray(
                "Here you go:\n[{\"ref\":\"Z1\",\"w1\":700,\"h1\":800,\"qty\":2}]\n");

            Assert.Single(result.Rows);
            Assert.Equal("Z1", result.Rows[0].GlassRef);
            Assert.Equal(700, result.Rows[0].W1);
            Assert.Equal(800, result.Rows[0].H1);
            Assert.Equal(2, result.Rows[0].Qty);
        }

        [Fact]
        public void Skips_rows_without_size()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "Glass Ref,W1,H1,Qty\nA1,0,0,1\nB2,500,500,1\n");

            Assert.Single(result.Rows);
            Assert.Equal("B2", result.Rows[0].GlassRef);
            Assert.True(result.Skipped >= 1);
        }
    }
}
