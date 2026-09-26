// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/GuillotinePacker.Remnants.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — remnant candidate registration (PHASE 2, part 1)
//
// WHY THIS FILE
//   Every optimizer in this market reports the offcut it leaves, because an
//   offcut is money: LiSEC's dynopt builds its case on remnant merging, and
//   the trim cuts GlasSave offers exist to turn a condemned area into a
//   reusable plate. This app threw that away. It counted the waste in
//   square metres and never said which rectangles it was, so the cutter
//   could not tell a 900 x 600 plate from a 40mm sliver.
//
// HOW IT WORKS
//   No new packing logic. The cut sequence recorded in Phase 0 is replayed:
//   start with the usable rectangle, split it at every recorded cut, and
//   whatever rectangle no piece occupies at the end is leftover. That makes
//   this a READ of the plan, so it cannot change a single placement — the
//   DGU benchmark is untouched.
//
// WHAT IS DELIBERATELY NOT HERE
//   Feeding those rectangles back in as stock for later sheets. That is the
//   second half of phase 2 and it does change packing, so it needs its own
//   gate, its own oracle and its own benchmark run. Registering the
//   rectangles is safe by itself and is a prerequisite for it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Optimization
{
    /// <summary>
    /// One rectangle a sheet is left holding after every cut is made.
    /// </summary>
    public sealed class RemnantRegion
    {
        public int SheetNo { get; set; }

        /// <summary>Position on the plate, in millimetres, plate origin.</summary>
        public double X { get; set; }
        public double Y { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }

        /// <summary>True when both sides are at least the minimum the run was
        /// derived with — i.e. worth racking rather than binning.</summary>
        public bool IsUsable { get; set; }

        /// <summary>The shorter side: what decides whether it can be racked
        /// and whether a later job can fit anything on it.</summary>
        public double MinSideMm => Math.Min(WidthMm, HeightMm);

        public double AreaM2 => (WidthMm / 1000.0) * (HeightMm / 1000.0);

        /// <summary>Rotated, so an offcut can be compared against a piece
        /// without caring which way up it lives on the rack.</summary>
        public string SizeLabel => string.Format(
            System.Globalization.CultureInfo.InvariantCulture, "{0:0} x {1:0}",
            WidthMm, HeightMm);
    }

    public static partial class GuillotinePacker
    {
        /// <summary>
        /// Rebuilds the rectangles a sheet is cut into and records the ones
        /// no piece occupies. Idempotent: the list is cleared first, so it can
        /// be re-run after a plan is edited without double-counting.
        /// </summary>
        /// <param name="minSideMm">Shorter side a leftover must have to be
        /// marked usable. Below this it is recorded as waste — still counted,
        /// still drawn, but not offered.</param>
        internal static void DeriveLeftovers(SheetLayout sheet, int minSideMm, int kerfMm)
        {
            sheet.Leftovers.Clear();

            if (sheet.UsableWidth <= 0 || sheet.UsableHeight <= 0)
                return;
            if (sheet.Cuts.Count == 0 && sheet.Pieces.Count == 0)
                return;

            var regions = new List<RemnantRegion>
            {
                new RemnantRegion
                {
                    SheetNo = sheet.SheetNo,
                    X = sheet.UsableX,
                    Y = sheet.UsableY,
                    WidthMm = sheet.UsableWidth,
                    HeightMm = sheet.UsableHeight
                }
            };

            foreach (var cut in sheet.Cuts)
            {
                var next = new List<RemnantRegion>(regions.Count + 1);
                bool splitAny = false;

                for (int i = 0; i < regions.Count; i++)
                {
                    var r = regions[i];
                    if (!Crosses(cut, r))
                    {
                        next.Add(r);
                        continue;
                    }

                    splitAny = true;
                    double kerf = kerfMm > 0 ? kerfMm : 0;

                    if (cut.Axis == CutAxis.Vertical)
                    {
                        double leftWidth = cut.AtMm - r.X;
                        next.Add(new RemnantRegion
                        {
                            SheetNo = sheet.SheetNo, X = r.X, Y = r.Y,
                            WidthMm = leftWidth, HeightMm = r.HeightMm
                        });
                        // The blade eats kerf from the remainder side, so the
                        // usable remainder starts after it. Without this every
                        // offcut would be measured a kerf too generous.
                        next.Add(new RemnantRegion
                        {
                            SheetNo = sheet.SheetNo, X = cut.AtMm + kerf, Y = r.Y,
                            WidthMm = r.WidthMm - leftWidth - kerf, HeightMm = r.HeightMm
                        });
                    }
                    else
                    {
                        double bottomHeight = cut.AtMm - r.Y;
                        next.Add(new RemnantRegion
                        {
                            SheetNo = sheet.SheetNo, X = r.X, Y = r.Y,
                            WidthMm = r.WidthMm, HeightMm = bottomHeight
                        });
                        next.Add(new RemnantRegion
                        {
                            SheetNo = sheet.SheetNo, X = r.X, Y = cut.AtMm + kerf,
                            WidthMm = r.WidthMm, HeightMm = r.HeightMm - bottomHeight - kerf
                        });
                    }
                }

                if (splitAny) regions = next;
            }

            // A region holding a piece is not a leftover. The tolerance covers
            // the kerf that sits between a piece and the next cut line.
            double tolerance = kerfMm > 0 ? kerfMm + 0.5 : 0.5;

            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                if (r.WidthMm <= 0.5 || r.HeightMm <= 0.5) continue;
                if (HoldsPiece(sheet, r, tolerance)) continue;

                r.IsUsable = r.MinSideMm >= minSideMm;
                sheet.Leftovers.Add(r);
            }
        }

        /// <summary>Derives leftovers for every sheet in a plan.</summary>
        internal static void DeriveLeftovers(OptimizationResult result, int minSideMm, int kerfMm)
        {
            for (int i = 0; i < result.Sheets.Count; i++)
                DeriveLeftovers(result.Sheets[i], minSideMm, kerfMm);
        }

        /// <summary>
        /// Every leftover across the plan big enough to rack, ordered by area
        /// so the most useful offcut is first. Waste-thin ones are excluded:
        /// they are still recorded on their sheet, just not offered.
        /// </summary>
        public static List<RemnantRegion> UsableRemnants(OptimizationResult result)
        {
            var list = new List<RemnantRegion>();
            if (result == null) return list;

            for (int i = 0; i < result.Sheets.Count; i++)
            {
                var sheet = result.Sheets[i];
                for (int j = 0; j < sheet.Leftovers.Count; j++)
                    if (sheet.Leftovers[j].IsUsable)
                        list.Add(sheet.Leftovers[j]);
            }

            list.Sort((a, b) => b.AreaM2.CompareTo(a.AreaM2));
            return list;
        }

        // ── geometry ────────────────────────────────────────────────────

        /// <summary>True when the cut line passes fully through the region.
        /// A cut that only nicks a corner or stops short does not split
        /// it — that is a partial cut and the packer does not emit one.</summary>
        private static bool Crosses(CutStep cut, RemnantRegion r)
        {
            if (cut.Axis == CutAxis.Vertical)
            {
                if (cut.AtMm <= r.X + 0.5 || cut.AtMm >= r.X + r.WidthMm - 0.5) return false;
                return cut.FromY <= r.Y + 0.5
                    && cut.FromY + cut.FromH >= r.Y + r.HeightMm - 0.5;
            }

            if (cut.AtMm <= r.Y + 0.5 || cut.AtMm >= r.Y + r.HeightMm - 0.5) return false;
            return cut.FromX <= r.X + 0.5
                && cut.FromX + cut.FromW >= r.X + r.WidthMm - 0.5;
        }

        private static bool HoldsPiece(SheetLayout sheet, RemnantRegion r, double tolerance)
        {
            for (int i = 0; i < sheet.Pieces.Count; i++)
            {
                var p = sheet.Pieces[i];
                if (p.X >= r.X - tolerance
                    && p.Y >= r.Y - tolerance
                    && p.X + p.WidthMm <= r.X + r.WidthMm + tolerance
                    && p.Y + p.HeightMm <= r.Y + r.HeightMm + tolerance)
                    return true;
            }
            return false;
        }
    }
}
