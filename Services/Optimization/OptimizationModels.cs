// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/OptimizationModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — glass cutting optimization (design 2026-09-12)
//
// Pure domain models for the sheet-optimization engine. No WPF, no EF —
// the engine is fully unit-testable and pinned by oracle tests
// (no-overlap, margins, kerf, rotation). See
// docs/OPTIMIZATION_MODULE_DESIGN_2026-09-12.md.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProGlassAutomation.Services.Optimization
{
    /// <summary>
    /// How pieces may be turned on the sheet. Free90 is ADAPTIVE: the
    /// piece keeps its original orientation whenever that fits — rotation
    /// is used only when the piece needs it (the floor's preferred 0/90
    /// mix). This is the default for every run.
    /// </summary>
    public enum RotationPolicy
    {
        /// <summary>Pieces stay exactly as entered (W×H).</summary>
        Locked,
        /// <summary>Original orientation first; rotate 90° only if it doesn't fit.</summary>
        Free90
    }

    /// <summary>Guillotine split direction after a placement.</summary>
    public enum SplitMode
    {
        /// <summary>Bottom remainder keeps the full width (calibrated default).</summary>
        HorizontalFirst,
        /// <summary>Right remainder keeps the full height.</summary>
        VerticalFirst,
        /// <summary>Split along the side with the longer leftover.</summary>
        LongerLeftover
    }

    /// <summary>
    /// Cutting allowances for one glass thickness. Margins are the edge
    /// trims cut off the raw stock before piece placement; kerf is the
    /// blade gap between cuts; MinBreakout is the narrowest offcut worth
    /// keeping (thinner strips are reported as trim waste).
    /// </summary>
    public sealed class MarginProfile
    {
        public int ThicknessMm { get; init; }
        public int LeftMm { get; init; }
        public int RightMm { get; init; }
        public int TopMm { get; init; }
        public int BottomMm { get; init; }
        public int KerfMm { get; init; }
        public int MinBreakoutMm { get; init; }

        /// <summary>
        /// House defaults (floor-confirmed for 6mm: LM/RM/TM/BM = 15mm,
        /// minimum breakout 15mm, kerf 3–4mm) — editable in the MARGINS
        /// tab, per thickness.
        /// </summary>
        public static MarginProfile DefaultFor(int thicknessMm) => new()
        {
            ThicknessMm = thicknessMm,
            LeftMm = thicknessMm <= 6 ? 15 : thicknessMm <= 8 ? 20 : 25,
            RightMm = thicknessMm <= 6 ? 15 : thicknessMm <= 8 ? 20 : 25,
            TopMm = thicknessMm <= 6 ? 15 : thicknessMm <= 8 ? 20 : 25,
            BottomMm = thicknessMm <= 6 ? 15 : thicknessMm <= 8 ? 20 : 25,
            KerfMm = thicknessMm >= 10 ? 4 : 3,
            MinBreakoutMm = 15
        };
    }

    /// <summary>One stock sheet size in the run's pool.</summary>
    public sealed class StockSize
    {
        public int WidthMm { get; init; }
        public int HeightMm { get; init; }
        public int ThicknessMm { get; init; }
        public string Color { get; init; } = "";
        public string Label { get; init; } = "";

        /// <summary>Optional cap (Sheet Store balance). 0 = unlimited.</summary>
        public int MaxSheets { get; init; }

        public string SizeLabel => string.IsNullOrWhiteSpace(Label)
            ? $"{WidthMm.ToString(CultureInfo.InvariantCulture)} × {HeightMm.ToString(CultureInfo.InvariantCulture)}"
            : Label;
    }

    /// <summary>One piece to cut (already expanded from row quantities).</summary>
    public sealed class CutPiece
    {
        public string JobRef { get; init; } = "";
        public string GlassRef { get; init; } = "";
        public string SpecLabel { get; init; } = "";
        public string Position { get; init; } = "";
        public string Color { get; init; } = "";
        public int ThicknessMm { get; init; }
        public int WidthMm { get; init; }
        public int HeightMm { get; init; }
        public bool AllowRotate { get; init; } = true;

        /// <summary>Identity for the 2D view, e.g. "G-101 · 3/24".</summary>
        public string PieceLabel { get; init; } = "";
    }

    /// <summary>One placed piece on one sheet (integer mm coordinates).</summary>
    public sealed class PlacedPiece
    {
        public CutPiece Piece { get; init; } = null!;
        public int X { get; init; }
        public int Y { get; init; }
        public int WidthMm { get; init; }
        public int HeightMm { get; init; }
        public bool Rotated { get; init; }
    }

    /// <summary>One optimized sheet with its placements and metrics.</summary>
    public sealed class SheetLayout
    {
        public int SheetNo { get; set; }
        public StockSize Stock { get; init; } = null!;

        /// <summary>Usable rectangle inside the edge trims.</summary>
        public int UsableX { get; init; }
        public int UsableY { get; init; }
        public int UsableWidth { get; init; }
        public int UsableHeight { get; init; }

        public List<PlacedPiece> Pieces { get; } = new();

        public decimal UsedSqm =>
            System.Math.Round(Pieces.Sum(p => (decimal)p.WidthMm * p.HeightMm) / 1_000_000m,
                4, System.MidpointRounding.AwayFromZero);

        public decimal StockSqm =>
            System.Math.Round((decimal)Stock.WidthMm * Stock.HeightMm / 1_000_000m,
                4, System.MidpointRounding.AwayFromZero);

        /// <summary>Utilization % = placed glass area / full sheet area.</summary>
        public decimal UtilizationPct => StockSqm == 0 ? 0m :
            System.Math.Round(UsedSqm / StockSqm * 100m, 2, System.MidpointRounding.AwayFromZero);

        public string UtilizationLabel =>
            UtilizationPct.ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Whole-run result.</summary>
    public sealed class OptimizationResult
    {
        public List<SheetLayout> Sheets { get; } = new();
        public List<CutPiece> Unplaced { get; } = new();
        public RotationPolicy Rotation { get; init; }

        public int SheetsUsed => Sheets.Count;

        public decimal TotalUsedSqm =>
            System.Math.Round(Sheets.Sum(s => s.UsedSqm), 4, System.MidpointRounding.AwayFromZero);

        public decimal TotalStockSqm =>
            System.Math.Round(Sheets.Sum(s => s.StockSqm), 4, System.MidpointRounding.AwayFromZero);

        /// <summary>Weighted utilization across all sheets.</summary>
        public decimal UtilizationPct => TotalStockSqm == 0 ? 0m :
            System.Math.Round(TotalUsedSqm / TotalStockSqm * 100m, 2, System.MidpointRounding.AwayFromZero);

        public bool Success => Unplaced.Count == 0 && Sheets.Count > 0;
    }
}
