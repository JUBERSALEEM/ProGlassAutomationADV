// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/CncExport/CncCutListWriter.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — cut-list CSV (PHASE 4)
//
// WHY THIS EXISTS
//   DXF is the drawing. This is the schedule: one row per cut, in the order
//   the table executes them, with the stage number and the trim flag. It is
//   the format that needs no specification to be useful — a supervisor can
//   read it, Excel can sort it, and any CAM that accepts a table can consume
//   it. It is also the format a machine vendor can be handed to BUILD the
//   proprietary exporter from, because it carries exactly the information
//   those files contain.
//
// FORMAT
//   RFC 4180: comma separated, CRLF, fields containing a comma, quote or
//   newline are quoted and inner quotes doubled. Millimetres, sheet origin
//   at 0,0, Y up — the same coordinates as the DXF.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Optimization;
using System;
using System.Globalization;
using System.Text;

namespace ProGlassAutomation.Services.Reporting.CncExport
{
    public static class CncCutListWriter
    {
        public static string Write(CncExportPlan plan)
        {
            if (plan?.Result == null)
                throw new ArgumentNullException(nameof(plan));

            var sb = new StringBuilder();

            // Provenance as a leading note row. Excel keeps it in column A and
            // every CSV reader treats an unquoted line as one row, so it costs
            // nothing and tells the reader what they are looking at.
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
}
