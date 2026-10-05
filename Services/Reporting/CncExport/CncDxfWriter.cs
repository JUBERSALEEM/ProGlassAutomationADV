// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/CncExport/CncDxfWriter.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — DXF machine file (PHASE 4)
//
// WHY R12 / AC1009
//   The oldest ASCII DXF still understood by everything: cutting tables,
//   CAM packages, AutoCAD, LibreCAD, browsers. Newer DXFs add object types
//   that older machine controllers reject, and everything this file needs
//   to express — straight lines — existed in R12. Entities are LINE only,
//   for the same reason: no reader can misinterpret a LINE.
//
// WHAT IS IN THE FILE
//   Layer SHEET    — the usable rectangle of every plate.
//   Layer PIECES   — every placed pane, as a closed rectangle.
//   Layer CUTS     — every guillotine cut, in execution order, as a single
//                    line spanning the rectangle it crosses.
//   Layer REMNANTS — what the plate is left holding, when the rectangle is
//                    big enough to rack. Labelled with its size.
//   Layer WASTE    — the same rectangles when they are too small to be worth
//                    racking, so nobody mistakes a sliver for a stock plate.
//   Layer TEXT     — the sheet number and the pane label, so the drawing
//                    is readable by a human as well as a machine.
//
// COORDINATES
//   Millimetres, sheet origin at 0,0, Y up. Piece coordinates already
//   include the margin offset, so what is drawn is what is cut. A cut line
//   sits on the piece's trailing edge; the kerf is consumed from the
//   remainder side, exactly as the packer accounts for it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Optimization;
using System;
using System.Globalization;
using System.Text;

namespace ProGlassAutomation.Services.Reporting.CncExport
{
    public static class CncDxfWriter
    {
        public static string Write(CncExportPlan plan)
        {
            if (plan?.Result == null)
                throw new ArgumentNullException(nameof(plan));

            var sb = new StringBuilder();

            // One HEADER, one TABLES, one ENTITIES — in that order. R12 readers
            // are entitled to assume a single ENTITIES section, so the file is
            // built that way rather than relying on later, laxer rules.
            Header(sb);
            Tables(sb);
            Entities(sb, plan);
            Footer(sb);

            return sb.ToString();
        }

        // ── header ─────────────────────────────────────────────────────

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

        // ── entities ───────────────────────────────────────────────────

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
                // "can I rack this?" is answered on the drawing instead of by
                // measuring the waste by hand.
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
}
