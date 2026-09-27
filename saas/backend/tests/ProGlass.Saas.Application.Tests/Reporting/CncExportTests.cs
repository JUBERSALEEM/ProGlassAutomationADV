// ═══════════════════════════════════════════════════════════════════════
//  Phase 21 — the CNC export writers (WPF CncDxfWriter / CncCutListWriter
//  / CncExportFormats ports) golden tests: the R12 DXF structure, the
//  RFC 4180 cut list, and the honest readiness contract. Pure — no
//  database, no filesystem.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Reporting;
using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Application.Tests.Reporting;

public sealed class CncExportTests
{
    private static CncExportPlan OneSheetPlan()
    {
        var result = new OptimizationResult();

        var sheet = new SheetLayout
        {
            SheetNo = 1,
            Stock = new StockSize(3210, 2250, 6, "Clear"),
            UsableX = 15,
            UsableY = 15,
            UsableWidth = 3180,
            UsableHeight = 2220
        };
        sheet.Pieces.Add(new PlacedPiece(
            new CutPiece("JOB-1", "A", "6mm Clear Annealed Glass", "Glass",
                "Clear", 6, 1000, 2000, true, "A1"),
            15, 15, 1000, 2000, false));
        sheet.Cuts.Add(new CutStep(
            1, CutAxis.Vertical, 1015, 15, 15, 1000, 2000, 1, IsTrim: false));
        sheet.Cuts.Add(new CutStep(
            2, CutAxis.Horizontal, 2015, 1015, 15, 2165, 2000, 2, IsTrim: false));
        sheet.Leftovers.Add(new RemnantRegion(1, 1015, 15, 2165, 2000)
        {
            IsUsable = true
        });
        result.Sheets.Add(sheet);

        return new CncExportPlan
        {
            Result = result,
            Source = "unit test",
            CutPattern = "free guillotine",
            KerfMm = 3,
            GeneratedAt = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(4))
        };
    }

    // ── the honest format contract ────────────────────────────────────

    [Fact]
    public void Formats_TwoReady_ThreeDeclaredNotImplemented()
    {
        Assert.Equal(5, CncExportFormats.All.Count);
        Assert.Equal(2, CncExportFormats.All.Count(f => f.IsReady));

        Assert.True(CncExportFormats.GenericDxf.IsReady);
        Assert.True(CncExportFormats.CutListCsv.IsReady);
        Assert.False(CncExportFormats.LisecDat.IsReady);
        Assert.False(CncExportFormats.BotteroOpt.IsReady);
        Assert.False(CncExportFormats.IntermacIso.IsReady);

        // Every not-ready format says exactly what finishes it.
        Assert.All(CncExportFormats.All.Where(f => !f.IsReady),
            f => Assert.Contains("Not implemented", f.Note));
    }

    [Fact]
    public void ById_Unknown_ReturnsNull_Typo_Cannot_Reach_The_Wrong_Machine()
    {
        Assert.Null(CncExportFormats.ById(null));
        Assert.Null(CncExportFormats.ById(""));
        Assert.Null(CncExportFormats.ById("lisec-dat-typo"));
        // Case-insensitive lookup.
        Assert.Same(CncExportFormats.GenericDxf, CncExportFormats.ById("DXF"));
    }

    // ── DXF R12 ───────────────────────────────────────────────────────

    [Fact]
    public void Dxf_Is_R12_With_Six_Layers_Line_Entities_Only()
    {
        var dxf = CncDxfWriter.Write(OneSheetPlan());

        // R12 header, $ACADVER only.
        Assert.StartsWith("0\nSECTION\n2\nHEADER\n", dxf);
        Assert.Contains("9\n$ACADVER\n1\nAC1009\n", dxf);
        Assert.DoesNotContain("$INSUNITS", dxf);
        Assert.DoesNotContain("$MEASUREMENT", dxf);

        // The six layers.
        foreach (var layer in new[] { "SHEET", "PIECES", "CUTS", "REMNANTS", "WASTE", "TEXT" })
            Assert.Contains("0\nLAYER\n2\n" + layer + "\n", dxf);

        // One HEADER + one TABLES + one ENTITIES, closed in order.
        Assert.Equal(3, Count(dxf, "0\nSECTION\n"));
        Assert.Equal(1, Count(dxf, "2\nENTITIES\n"));
        Assert.EndsWith("0\nENDSEC\n0\nEOF\n", dxf);

        // LINE entities: stock rect 4 + usable rect 4 + piece 4 + leftover 4
        // + 2 cut lines = 18. Everything else is TEXT.
        Assert.Equal(18, Count(dxf, "0\nLINE\n"));
        Assert.Equal(4, Count(dxf, "0\nTEXT\n"));   // stamp + sheet no + label + offcut
    }

    [Fact]
    public void Dxf_Cut_Line_Spans_The_Rectangle_It_Crosses()
    {
        var dxf = CncDxfWriter.Write(OneSheetPlan());

        // The vertical cut at x=1015 crosses the piece's full height
        // (y 15 → 2015): the trailing-edge rule, verbatim.
        Assert.Contains("10\n1015\n20\n15\n30\n0.0\n11\n1015\n21\n2015\n", dxf);

        // The horizontal cut at y=2015 spans the remainder width.
        Assert.Contains("10\n1015\n20\n2015\n30\n0.0\n11\n3180\n21\n2015\n", dxf);

        // The provenance stamp travels as TEXT, not a header variable.
        Assert.Contains("1\nProGlassAutomation cut plan | unit test | 1 sheet(s) | free guillotine | kerf 3mm | millimetres | 2026-09-27 10:00\n", dxf);
    }

    [Fact]
    public void Dxf_Usable_Offcut_Is_Labelled_Waste_Is_Not()
    {
        var dxf = CncDxfWriter.Write(OneSheetPlan());
        Assert.Contains("OFFCUT 2165 x 2000", dxf);

        var plan = OneSheetPlan();
        plan.Result.Sheets[0].Leftovers[0] =
            new RemnantRegion(1, 1015, 15, 100, 100) { IsUsable = false };
        var dxf2 = CncDxfWriter.Write(plan);
        Assert.DoesNotContain("OFFCUT", dxf2);
    }

    // ── cut list CSV ──────────────────────────────────────────────────

    [Fact]
    public void CutList_Rfc4180_Header_And_Rows()
    {
        var csv = CncCutListWriter.Write(OneSheetPlan());

        var lines = csv.Split("\r\n");
        // Note row + header row + 2 cuts + trailing empty from the final CRLF.
        Assert.StartsWith("# ProGlassAutomation cut list | unit test | 1 sheet(s) | free guillotine | kerf 3mm | millimetres | 2026-09-27 10:00", lines[0]);
        Assert.Equal("Sheet,SheetWidthMm,SheetHeightMm,Seq,Axis,PositionMm,FromX,FromY,FromW,FromH,Stage,IsTrim,PiecesOnSheet", lines[1]);
        Assert.Equal("1,3210,2250,1,V,1015,15,15,1000,2000,1,CUT,1", lines[2]);
        Assert.Equal("1,3210,2250,2,H,2015,1015,15,2165,2000,2,CUT,1", lines[3]);
        Assert.EndsWith("\r\n", csv);
    }

    [Fact]
    public void CutList_Escapes_Commas_In_Provenance()
    {
        var plan = OneSheetPlan();
        plan.Source = "job, with commas";
        var csv = CncCutListWriter.Write(plan);
        Assert.StartsWith("# ProGlassAutomation cut list | \"job, with commas\" |", csv);
    }

    [Fact]
    public void CutList_No_Cuts_Still_Has_The_Schedule_Header()
    {
        var plan = OneSheetPlan();
        plan.Result.Sheets[0].Cuts.Clear();
        var csv = CncCutListWriter.Write(plan);
        var lines = csv.Split("\r\n");
        Assert.StartsWith("#", lines[0]);
        Assert.StartsWith("Sheet,SheetWidthMm", lines[1]);
        Assert.Equal("", lines[2]);   // nothing after the header
    }

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += needle.Length;
        }
        return n;
    }
}
