// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 21:
//   Services/Reporting/CncExport/CncExportFormats.cs  (PHASE 4)
//   Services/Reporting/CncExport/CncDxfWriter.cs      (PHASE 4)
//   Services/Reporting/CncExport/CncCutListWriter.cs  (PHASE 4)
// Verbatim ports over the SaaS Optimization domain model (Phase 11 —
// the model types are the same shapes).
//
// THE HONEST PART (kept verbatim from the WPF): three of the four CNC
// pills are proprietary and undocumented. Formats that are documented
// are implemented; formats that are not are declared, named, and say
// exactly what finishes them (a sample file from the machine). IsReady
// is the switch and it is false for every format that cannot be proven
// correct. Nothing here writes a machine file it cannot stand behind.
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Text;
using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Application.Reporting;

/// <summary>Everything a machine-file writer needs to know about a plan.</summary>
public sealed class CncExportPlan
{
    /// <summary>The plan to export. Sheets carry both pieces and cuts.</summary>
    public OptimizationResult Result { get; init; } = null!;

    /// <summary>Free-text provenance stamped into the file header.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Cut pattern the plan was built under, stamped for traceability.</summary>
    public string CutPattern { get; init; } = string.Empty;

    /// <summary>Blade gap, so a reader knows what the cut lines consumed.</summary>
    public int KerfMm { get; init; }

    /// <summary>Local timestamp stamped into the file header.</summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>
/// One export target. <see cref="IsReady"/> is the contract: a format
/// with a null writer can be listed, explained and requested, but never
/// silently written.
/// </summary>
public sealed class CncExportFormat
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Extension { get; init; } = "";

    /// <summary>True only when the writer produces output this codebase
    /// can prove correct against a published specification.</summary>
    public bool IsReady => Writer != null;

    /// <summary>What is true about this format, or — when it is not ready —
    /// exactly what would finish it. Shown to the operator either way.</summary>
    public string Note { get; init; } = "";

    /// <summary>Produces the file text. Null when the format is not ready.</summary>
    public Func<CncExportPlan, string>? Writer { get; init; }
}

public static class CncExportFormats
{
    public static readonly CncExportFormat GenericDxf = new()
    {
        Id = "dxf",
        DisplayName = "Generic DXF",
        Extension = ".dxf",
        Note = "DXF R12 (AC1009) — sheet outline, every piece, and the cut "
             + "sequence as lines in execution order. Documented format, "
             + "readable by CAM and by the drawing packages the floor already has.",
        Writer = CncDxfWriter.Write
    };

    public static readonly CncExportFormat CutListCsv = new()
    {
        Id = "cutlist",
        DisplayName = "Cut list CSV",
        Extension = ".csv",
        Note = "One row per cut in execution order: sheet, sequence, axis, "
             + "position, the rectangle the cut crosses, stage, and whether it "
             + "is a trim. Opens in Excel; feeds any CAM that accepts a table.",
        Writer = CncCutListWriter.Write
    };

    public static readonly CncExportFormat LisecDat = new()
    {
        Id = "lisec",
        DisplayName = "Lisec .DAT",
        Extension = ".dat",
        Note = "Not implemented. Lisec does not publish this format; vendor "
             + "format lists name Lisec output as OPT101.CNC. Send one sample "
             + "file exported by your Lisec table and this can be written "
             + "against it — until then a file that merely looks right would be "
             + "dangerous to put in front of a machine."
    };

    public static readonly CncExportFormat BotteroOpt = new()
    {
        Id = "bottero",
        DisplayName = "Bottero .OPT",
        Extension = ".opt",
        Note = "Not implemented. Bottero does not publish this format and "
             + "vendor format lists name Bottero output as *.CUT. Send one sample "
             + "file exported by your Bottero table and this can be written "
             + "against it."
    };

    public static readonly CncExportFormat IntermacIso = new()
    {
        Id = "intermac",
        DisplayName = "Intermac .ISO",
        Extension = ".iso",
        Note = "Not implemented. Intermac (Biesse) does not publish this "
             + "format. Send one sample file exported by your Intermac table "
             + "and this can be written against it."
    };

    public static IReadOnlyList<CncExportFormat> All { get; } = new[]
    {
        GenericDxf,
        CutListCsv,
        LisecDat,
        BotteroOpt,
        IntermacIso
    };

    /// <summary>Looks a format up by id. Unknown ids return null rather than
    /// defaulting, so a typo can never export to the wrong machine.</summary>
    public static CncExportFormat? ById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < All.Count; i++)
            if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return All[i];
        return null;
    }
}

/// <summary>
/// WHY R12 / AC1009: the oldest ASCII DXF still understood by everything —
/// cutting tables, CAM packages, AutoCAD, LibreCAD. Entities are LINE only,
/// for the same reason: no reader can misinterpret a LINE.
///
/// LAYERS: SHEET (usable rectangle of every plate), PIECES (every placed
/// pane), CUTS (every guillotine cut in execution order), REMNANTS (what
/// the plate is left holding when it is big enough to rack), WASTE (when
/// it is not, so nobody mistakes a sliver for a stock plate), TEXT (sheet
/// number + pane label).
///
/// COORDINATES: millimetres, sheet origin at 0,0, Y up. Piece coordinates
/// already include the margin offset, so what is drawn is what is cut. A
/// cut line sits on the piece's trailing edge; the kerf is consumed from
/// the remainder side, exactly as the packer accounts for it.
/// </summary>
public static class CncDxfWriter
{
    public static string Write(CncExportPlan plan)
    {
        if (plan?.Result == null)
            throw new ArgumentNullException(nameof(plan));

        var sb = new StringBuilder();

        // One HEADER, one TABLES, one ENTITIES — in that order. R12 readers
        // are entitled to assume a single ENTITIES section.
        Header(sb);
        Tables(sb);
        Entities(sb, plan);
        Footer(sb);

        return sb.ToString();
    }

    // ── header ─────────────────────────────────────────────────────────

    /// <summary>
    /// R12 header. $ACADVER is the only variable written: $INSUNITS and
    /// $MEASUREMENT are R2000 additions and an R12 controller is entitled
    /// to reject them. Units are millimetres by construction and are
    /// stated in the stamped text instead, where no reader can object.
    /// </summary>
    private static void Header(StringBuilder sb)
    {
        sb.Append("0\nSECTION\n2\nHEADER\n");
        sb.Append("9\n$ACADVER\n1\nAC1009\n");
        sb.Append("0\nENDSEC\n");
    }

    private static void Tables(StringBuilder sb)
    {
        sb.Append("0\nSECTION\n2\nTABLES\n");
        sb.Append("0\nTABLE\n2\nLAYER\n70\n4\n");

        Layer(sb, "SHEET", 8);
        Layer(sb, "PIECES", 3);
        Layer(sb, "CUTS", 1);
        Layer(sb, "REMNANTS", 4);
        Layer(sb, "WASTE", 8);
        Layer(sb, "TEXT", 7);

        sb.Append("0\nENDTAB\n0\nENDSEC\n");
    }

    private static void Layer(StringBuilder sb, string name, int color)
    {
        sb.Append("0\nLAYER\n2\n").Append(name).Append('\n');
        sb.Append("70\n0\n");
        sb.Append("62\n").Append(color.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("6\nCONTINUOUS\n");
    }

    // ── entities ───────────────────────────────────────────────────────

    private static void Entities(StringBuilder sb, CncExportPlan plan)
    {
        sb.Append("0\nSECTION\n2\nENTITIES\n");

        // Provenance travels as drawing text, not as a header variable a
        // strict R12 reader could reject.
        string stamp = string.Format(CultureInfo.InvariantCulture,
            "ProGlassAutomation cut plan | {0} | {1} sheet(s) | {2} | kerf {3}mm | millimetres | {4:yyyy-MM-dd HH:mm}",
            string.IsNullOrWhiteSpace(plan.Source) ? "optimization run" : plan.Source,
            plan.Result.Sheets.Count,
            string.IsNullOrWhiteSpace(plan.CutPattern) ? "free guillotine" : plan.CutPattern,
            plan.KerfMm,
            plan.GeneratedAt.LocalDateTime);
        Text(sb, stamp, 0, -60, 24);

        double offsetY = 0;

        for (int si = 0; si < plan.Result.Sheets.Count; si++)
        {
            var sheet = plan.Result.Sheets[si];
            double stockH = sheet.Stock?.HeightMm ?? sheet.UsableHeight;
            double originX = 0;
            double originY = offsetY;

            // Sheet outline — the full stock plate, not just the usable
            // area, so the operator sees the margins they configured.
            Rectangle(sb, "SHEET", originX, originY,
                sheet.Stock?.WidthMm ?? sheet.UsableWidth, stockH);

            // Usable area, so a margin mistake is visible before cutting.
            Rectangle(sb, "SHEET", originX + sheet.UsableX, originY + sheet.UsableY,
                sheet.UsableWidth, sheet.UsableHeight);

            Text(sb, "SHEET " + sheet.SheetNo.ToString(CultureInfo.InvariantCulture),
                originX + sheet.UsableX, originY + sheet.UsableY + sheet.UsableHeight + 20, 24);

            for (int pi = 0; pi < sheet.Pieces.Count; pi++)
            {
                var piece = sheet.Pieces[pi];
                Rectangle(sb, "PIECES", originX + piece.X, originY + piece.Y,
                    piece.WidthMm, piece.HeightMm);

                string label = piece.Piece?.PieceLabel;
                if (!string.IsNullOrWhiteSpace(label))
                    Text(sb, label, originX + piece.X + 10, originY + piece.Y + 10, 12);
            }

            // What the plate is left holding. Usable offcuts go on their
            // own layer with their size written on them, so the question
            // "can I rack this?" is answered on the drawing.
            for (int li = 0; li < sheet.Leftovers.Count; li++)
            {
                var remnant = sheet.Leftovers[li];
                Rectangle(sb, remnant.IsUsable ? "REMNANTS" : "WASTE",
                    originX + remnant.X, originY + remnant.Y,
                    remnant.WidthMm, remnant.HeightMm);

                if (remnant.IsUsable)
                    Text(sb, "OFFCUT " + remnant.SizeLabel,
                        originX + remnant.X + 10, originY + remnant.Y + 10, 14);
            }

            // Cuts, in the order the table executes them.
            for (int ci = 0; ci < sheet.Cuts.Count; ci++)
            {
                var cut = sheet.Cuts[ci];
                double x1, y1, x2, y2;
                if (cut.Axis == CutAxis.Vertical)
                {
                    x1 = x2 = originX + cut.AtMm;
                    y1 = originY + cut.FromY;
                    y2 = originY + cut.FromY + cut.FromH;
                }
                else
                {
                    y1 = y2 = originY + cut.AtMm;
                    x1 = originX + cut.FromX;
                    x2 = originX + cut.FromX + cut.FromW;
                }
                Line(sb, "CUTS", x1, y1, x2, y2);
            }

            offsetY += stockH + 500;   // stack plates vertically, 500mm apart
        }
    }

    private static void Rectangle(StringBuilder sb, string layer,
        double x, double y, double w, double h)
    {
        Line(sb, layer, x, y, x + w, y);
        Line(sb, layer, x + w, y, x + w, y + h);
        Line(sb, layer, x + w, y + h, x, y + h);
        Line(sb, layer, x, y + h, x, y);
    }

    private static void Line(StringBuilder sb, string layer,
        double x1, double y1, double x2, double y2)
    {
        sb.Append("0\nLINE\n8\n").Append(layer).Append('\n');
        sb.Append("10\n").Append(Num(x1)).Append('\n');
        sb.Append("20\n").Append(Num(y1)).Append('\n');
        sb.Append("30\n0.0\n");
        sb.Append("11\n").Append(Num(x2)).Append('\n');
        sb.Append("21\n").Append(Num(y2)).Append('\n');
        sb.Append("31\n0.0\n");
    }

    private static void Text(StringBuilder sb, string value, double x, double y, double height)
    {
        if (value.Length > 200) value = value.Substring(0, 200);
        sb.Append("0\nTEXT\n8\nTEXT\n");
        sb.Append("10\n").Append(Num(x)).Append('\n');
        sb.Append("20\n").Append(Num(y)).Append('\n');
        sb.Append("30\n0.0\n");
        sb.Append("40\n").Append(Num(height)).Append('\n');
        sb.Append("1\n").Append(value).Append('\n');
    }

    private static string Num(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static void Footer(StringBuilder sb)
    {
        sb.Append("0\nENDSEC\n0\nEOF\n");
    }
}

/// <summary>
/// The cut SCHEDULE: one row per cut, in the order the table executes
/// them, with the stage number and the trim flag. RFC 4180: comma
/// separated, CRLF, fields containing a comma, quote or newline are
/// quoted and inner quotes doubled. Millimetres, sheet origin at 0,0,
/// Y up — the same coordinates as the DXF.
/// </summary>
public static class CncCutListWriter
{
    public static string Write(CncExportPlan plan)
    {
        if (plan?.Result == null)
            throw new ArgumentNullException(nameof(plan));

        var sb = new StringBuilder();

        // Provenance as a leading note row. Excel keeps it in column A and
        // every CSV reader treats an unquoted line as one row.
        sb.Append("# ProGlassAutomation cut list | ")
          .Append(Escape(string.IsNullOrWhiteSpace(plan.Source) ? "optimization run" : plan.Source))
          .Append(" | ")
          .Append(plan.Result.Sheets.Count.ToString(CultureInfo.InvariantCulture))
          .Append(" sheet(s) | ")
          .Append(Escape(string.IsNullOrWhiteSpace(plan.CutPattern) ? "free guillotine" : plan.CutPattern))
          .Append(" | kerf ")
          .Append(plan.KerfMm.ToString(CultureInfo.InvariantCulture))
          .Append("mm | millimetres | ")
          .Append(plan.GeneratedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
          .Append("\r\n");

        sb.Append("Sheet,SheetWidthMm,SheetHeightMm,Seq,Axis,PositionMm,"
            + "FromX,FromY,FromW,FromH,Stage,IsTrim,PiecesOnSheet\r\n");

        for (int si = 0; si < plan.Result.Sheets.Count; si++)
        {
            var sheet = plan.Result.Sheets[si];
            string sheetPrefix = Num(sheet.SheetNo) + ","
                + Num(sheet.Stock?.WidthMm ?? sheet.UsableWidth) + ","
                + Num(sheet.Stock?.HeightMm ?? sheet.UsableHeight) + ",";

            for (int ci = 0; ci < sheet.Cuts.Count; ci++)
            {
                var cut = sheet.Cuts[ci];
                sb.Append(sheetPrefix)
                  .Append(Num(cut.Seq)).Append(',')
                  .Append(cut.Axis == CutAxis.Vertical ? "V" : "H").Append(',')
                  .Append(Num(cut.AtMm)).Append(',')
                  .Append(Num(cut.FromX)).Append(',')
                  .Append(Num(cut.FromY)).Append(',')
                  .Append(Num(cut.FromW)).Append(',')
                  .Append(Num(cut.FromH)).Append(',')
                  .Append(Num(cut.Stage)).Append(',')
                  .Append(cut.IsTrim ? "TRIM" : "CUT").Append(',')
                  .Append(Num(sheet.Pieces.Count))
                  .Append("\r\n");
            }
        }

        return sb.ToString();
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
