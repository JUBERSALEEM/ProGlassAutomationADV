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
        public void Asymmetric_header_preserves_w1_h1_w2_h2_qty()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "REF,W1,H1,W2,H2,PCS\nD17,3210,2250,3000,1800,17\n");

            Assert.Single(result.Rows);
            Assert.Equal(3210, result.Rows[0].W1);
            Assert.Equal(2250, result.Rows[0].H1);
            Assert.Equal(3000, result.Rows[0].W2);
            Assert.Equal(1800, result.Rows[0].H2);
            Assert.Equal(17, result.Rows[0].Qty);
        }

        [Fact]
        public void Qty_aliases_and_numeric_forms()
        {
            var thousand = CutListImportParser.ParseDelimitedText(
                "W1,H1,Qty\n1000,800,1,000\n");
            Assert.Single(thousand.Rows);
            Assert.Equal(1000, thousand.Rows[0].Qty);

            var decimalQty = CutListImportParser.ParseDelimitedText(
                "W1,H1,Quantity\n1000,800,17.0\n");
            Assert.Single(decimalQty.Rows);
            Assert.Equal(17, decimalQty.Rows[0].Qty);

            var emptyQty = CutListImportParser.ParseDelimitedText(
                "W1,H1,Qty\n1000,800,\n");
            Assert.Single(emptyQty.Rows);
            Assert.Equal(1, emptyQty.Rows[0].Qty);
        }

        [Fact]
        public void Pcs_suffix_in_cell_is_not_parsed_as_seventeen()
        {
            // Documented: ParseMm cannot read "17 pcs". Empty/invalid qty
            // becomes 1. Frozen PI importer — do not silently invent 17.
            var result = CutListImportParser.ParseDelimitedText(
                "W1,H1,PCS\n1000,800,17 pcs\n");
            Assert.Single(result.Rows);
            Assert.Equal(1, result.Rows[0].Qty);
        }

        [Fact]
        public void Empty_w2_h2_is_zero_not_copied_from_w1_h1()
        {
            var result = CutListImportParser.ParseDelimitedText(
                "W1,H1,W2,H2,Qty\n3210,2250,,,5\n");
            Assert.Single(result.Rows);
            Assert.Equal(3210, result.Rows[0].W1);
            Assert.Equal(2250, result.Rows[0].H1);
            Assert.Equal(0, result.Rows[0].W2);
            Assert.Equal(0, result.Rows[0].H2);
            Assert.Equal(5, result.Rows[0].Qty);
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
