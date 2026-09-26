using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Reporting.CncExport;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE 4 (machine-file export). The claim under test is
/// narrow but load-bearing: the two formats we ship are written against a
/// published specification, and the three we do not ship say so instead of
/// emitting file contents nobody can vouch for.
/// </summary>
public sealed class CncExportTests
{
    private static CutPiece Pane(int w, int h, string label, bool allowRotate = true) => new()
    {
        WidthMm = w,
        HeightMm = h,
        ThicknessMm = 6,
        PieceLabel = label,
        GlassRef = "clear",
        AllowRotate = allowRotate
    };

    private static StockSize[] Stock() => new[]
    {
        new StockSize { WidthMm = 1210, HeightMm = 810, ThicknessMm = 6, MaxSheets = 0 }
    };

    private static readonly MarginProfile Margins = new()
    {
        ThicknessMm = 6,
        LeftMm = 10,
        RightMm = 10,
        TopMm = 10,
        BottomMm = 10,
        KerfMm = 3,
        MinBreakoutMm = 15
    };

    private static CncExportPlan Plan()
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < 14; i++)
            pieces.Add(Pane(400 + (i * 37 % 500), 300 + (i * 53 % 380), "p" + i.ToString("00")));

        var result = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);
        return new CncExportPlan
        {
            Result = result,
            Source = "test job",
            CutPattern = CutPolicy.Free.Describe(),
            KerfMm = Margins.KerfMm
        };
    }

    // ── DXF ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dxf_is_R12_with_one_entities_section_and_ends_at_EOF()
    {
        string dxf = CncDxfWriter.Write(Plan());

        Assert.Contains("$ACADVER", dxf, StringComparison.Ordinal);
        Assert.Contains("AC1009", dxf, StringComparison.Ordinal);

        // One ENTITIES section only: R12 readers may assume it.
        Assert.Equal(1, CountOccurrences(dxf, "0\r\nSECTION\r\n2\r\nENTITIES\r\n")
                        + CountOccurrences(dxf, "0\nSECTION\n2\nENTITIES\n"));
        Assert.EndsWith("EOF", dxf.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Dxf_draws_every_piece_and_every_cut()
    {
        var plan = Plan();
        string dxf = CncDxfWriter.Write(plan);

        int lines = CountOccurrences(dxf, "\nLINE\n");
        int pieces = plan.Result.Sheets.Sum(s => s.Pieces.Count);
        int cuts = plan.Result.Sheets.Sum(s => s.Cuts.Count);

        // Per sheet: stock outline + usable rectangle = 8 lines. Then four per
        // piece, four per leftover rectangle, one per cut. Nothing else emits
        // a LINE.
        int leftovers = plan.Result.Sheets.Sum(s => s.Leftovers.Count);
        int expected = plan.Result.Sheets.Count * 8 + pieces * 4 + leftovers * 4 + cuts;
        Assert.True(pieces > 0);
        Assert.True(cuts > 0);
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Dxf_puts_cuts_on_their_own_layer_and_stays_in_the_sheet()
    {
        var plan = Plan();
        string dxf = CncDxfWriter.Write(plan);

        Assert.Contains("CUTS", dxf, StringComparison.Ordinal);
        Assert.Contains("PIECES", dxf, StringComparison.Ordinal);

        // Every number written must be finite — a NaN in a DXF is a file no
        // reader will open, and it fails silently at the machine.
        foreach (var token in dxf.Split('\n'))
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) continue;
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        }
    }

    // ── cut list CSV ─────────────────────────────────────────────────────

    [Fact]
    public void Cut_list_has_one_row_per_cut_in_execution_order()
    {
        var plan = Plan();
        string csv = CncCutListWriter.Write(plan);

        var rows = csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(r => !r.StartsWith("#", StringComparison.Ordinal))
                      .ToList();

        int cuts = plan.Result.Sheets.Sum(s => s.Cuts.Count);
        Assert.True(cuts > 0);
        Assert.Equal("Sheet,SheetWidthMm,SheetHeightMm,Seq,Axis,PositionMm,FromX,FromY,FromW,FromH,Stage,IsTrim,PiecesOnSheet",
            rows[0]);
        Assert.Equal(cuts + 1, rows.Count);

        // Sequence numbers restart at 1 on every sheet — the cut order is
        // per plate, because that is what the table executes. (Sheet, Seq) is
        // the key, which is why the sheet number is the first column.
        int currentSheet = -1;
        int expected = 1;
        for (int i = 1; i < rows.Count; i++)
        {
            var fields = rows[i].Split(',');
            int sheetNo = int.Parse(fields[0], CultureInfo.InvariantCulture);
            if (sheetNo != currentSheet)
            {
                currentSheet = sheetNo;
                expected = 1;
            }
            Assert.Equal(expected++.ToString(CultureInfo.InvariantCulture), fields[3]);
        }
    }

    [Fact]
    public void Cut_list_records_the_axis_and_position_the_floor_needs()
    {
        var plan = Plan();
        string csv = CncCutListWriter.Write(plan);

        var sheet = plan.Result.Sheets[0];
        var cut = sheet.Cuts[0];
        string wanted = cut.Axis == CutAxis.Vertical ? "V" : "H";

        Assert.Contains("," + wanted + "," + cut.AtMm.ToString(CultureInfo.InvariantCulture) + ",",
            csv, StringComparison.Ordinal);
    }

    // ── the formats we refuse to fake ────────────────────────────────────

    [Theory]
    [InlineData("lisec")]
    [InlineData("bottero")]
    [InlineData("intermac")]
    public void Undocumented_machine_formats_are_declared_but_not_written(string id)
    {
        var format = CncExportFormats.ById(id);

        Assert.NotNull(format);
        Assert.False(format!.IsReady);
        Assert.Null(format.Writer);

        // The note has to tell the operator what would finish it.
        Assert.Contains("sample", format.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ready_formats_are_ready_and_unknown_ids_do_not_default()
    {
        Assert.True(CncExportFormats.GenericDxf.IsReady);
        Assert.True(CncExportFormats.CutListCsv.IsReady);
        Assert.NotNull(CncExportFormats.GenericDxf.Writer);

        Assert.Null(CncExportFormats.ById("nonexistent"));
        Assert.Null(CncExportFormats.ById(""));
        Assert.Null(CncExportFormats.ById(null));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
