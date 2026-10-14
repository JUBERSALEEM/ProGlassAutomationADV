using System;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    public enum StickerSheetOrientation
    {
        Portrait,
        Landscape
    }

    /// <summary>
    /// A calculated, scale-preserving grid for placing one or more physical sticker
    /// labels on a standard printer page. All dimensions and positions are in mm.
    /// </summary>
    public readonly record struct StickerSheetLayoutPlan(
        StickerSheetOrientation Orientation,
        double PageWidthMm,
        double PageHeightMm,
        double GridLeftMm,
        double GridTopMm,
        double LabelWidthMm,
        double LabelHeightMm,
        double HorizontalGapMm,
        double VerticalGapMm,
        int Columns,
        int Rows)
    {
        public int LabelsPerSheet => checked(Columns * Rows);
        public double GridWidthMm => Columns * LabelWidthMm + Math.Max(0, Columns - 1) * HorizontalGapMm;
        public double GridHeightMm => Rows * LabelHeightMm + Math.Max(0, Rows - 1) * VerticalGapMm;

        public long PageCountFor(long labelCount)
        {
            if (labelCount <= 0) return 0;
            if (LabelsPerSheet <= 0) throw new InvalidOperationException("The A4 sheet has no sticker positions.");
            return 1 + ((labelCount - 1) / LabelsPerSheet);
        }

        /// <summary>Returns the top-left position for a zero-based, row-major slot.</summary>
        public (double Xmm, double Ymm) PositionForSlot(int slot)
        {
            if (slot < 0 || slot >= LabelsPerSheet)
                throw new ArgumentOutOfRangeException(nameof(slot));

            int row = slot / Columns;
            int column = slot % Columns;
            return (
                GridLeftMm + column * (LabelWidthMm + HorizontalGapMm),
                GridTopMm + row * (LabelHeightMm + VerticalGapMm));
        }
    }

    /// <summary>
    /// Pure layout math shared by print planning, preview and tests. The printable
    /// rectangle comes from the selected printer's capabilities; requested outer
    /// margins are intersected with it so a sheet never intentionally uses the
    /// printer's non-imageable area.
    /// </summary>
    public static class StickerSheetLayoutPlanner
    {
        public const double A4WidthMm = 210;
        public const double A4HeightMm = 297;
        public const double DefaultSheetMarginMm = 5;
        public const double DefaultStickerGapMm = 2;

        public static bool TryCreatePlan(
            StickerSheetOrientation orientation,
            double pageWidthMm,
            double pageHeightMm,
            double imageableLeftMm,
            double imageableTopMm,
            double imageableWidthMm,
            double imageableHeightMm,
            double labelWidthMm,
            double labelHeightMm,
            double sheetMarginMm,
            double gapMm,
            out StickerSheetLayoutPlan plan,
            out string error)
        {
            plan = default;
            error = string.Empty;
            if (!AllFinite(pageWidthMm, pageHeightMm, imageableLeftMm, imageableTopMm,
                    imageableWidthMm, imageableHeightMm, labelWidthMm, labelHeightMm,
                    sheetMarginMm, gapMm)
                || pageWidthMm <= 0 || pageHeightMm <= 0
                || imageableWidthMm <= 0 || imageableHeightMm <= 0
                || labelWidthMm <= 0 || labelHeightMm <= 0
                || sheetMarginMm < 0 || gapMm < 0)
            {
                error = "A4 page, printable area, sticker size, margin and gap must be valid positive dimensions.";
                return false;
            }

            double usableLeft = Math.Max(sheetMarginMm, Math.Max(0, imageableLeftMm));
            double usableTop = Math.Max(sheetMarginMm, Math.Max(0, imageableTopMm));
            double usableRight = Math.Min(pageWidthMm - sheetMarginMm,
                Math.Min(pageWidthMm, imageableLeftMm + imageableWidthMm));
            double usableBottom = Math.Min(pageHeightMm - sheetMarginMm,
                Math.Min(pageHeightMm, imageableTopMm + imageableHeightMm));
            double usableWidth = usableRight - usableLeft;
            double usableHeight = usableBottom - usableTop;

            int columns = CountFit(usableWidth, labelWidthMm, gapMm);
            int rows = CountFit(usableHeight, labelHeightMm, gapMm);
            if (columns <= 0 || rows <= 0)
            {
                error = $"A {labelWidthMm:0.##} × {labelHeightMm:0.##} mm sticker does not fit in the selected printer's A4 imageable area with the current margins. Reduce the sticker size or use one label per page.";
                return false;
            }

            double gridWidth = columns * labelWidthMm + (columns - 1) * gapMm;
            double gridHeight = rows * labelHeightMm + (rows - 1) * gapMm;
            plan = new StickerSheetLayoutPlan(
                orientation,
                pageWidthMm,
                pageHeightMm,
                usableLeft + (usableWidth - gridWidth) / 2,
                usableTop + (usableHeight - gridHeight) / 2,
                labelWidthMm,
                labelHeightMm,
                gapMm,
                gapMm,
                columns,
                rows);
            return true;
        }

        /// <summary>
        /// Computes an approximate A4 preview using the supplied physical bounds.
        /// Actual printing recalculates this against the selected printer driver.
        /// </summary>
        public static bool TryCreateNominalA4Plan(
            double labelWidthMm,
            double labelHeightMm,
            double sheetMarginMm,
            double gapMm,
            out StickerSheetLayoutPlan plan,
            out string error)
        {
            bool portraitFits = TryCreatePlan(
                StickerSheetOrientation.Portrait,
                A4WidthMm, A4HeightMm,
                0, 0, A4WidthMm, A4HeightMm,
                labelWidthMm, labelHeightMm, sheetMarginMm, gapMm,
                out var portrait, out string portraitError);
            bool landscapeFits = TryCreatePlan(
                StickerSheetOrientation.Landscape,
                A4HeightMm, A4WidthMm,
                0, 0, A4HeightMm, A4WidthMm,
                labelWidthMm, labelHeightMm, sheetMarginMm, gapMm,
                out var landscape, out string landscapeError);

            if (!portraitFits && !landscapeFits)
            {
                plan = default;
                error = portraitError.Length > 0 ? portraitError : landscapeError;
                return false;
            }

            // Prefer portrait for ties: it is the usual A4 feed direction and
            // avoids unnecessary landscape rotation in the printer driver.
            plan = landscapeFits && (!portraitFits || landscape.LabelsPerSheet > portrait.LabelsPerSheet)
                ? landscape : portrait;
            error = string.Empty;
            return true;
        }

        private static int CountFit(double available, double item, double gap)
        {
            if (!double.IsFinite(available) || available <= 0) return 0;
            double count = Math.Floor((available + gap + 1e-9) / (item + gap));
            return count >= int.MaxValue ? int.MaxValue : (int)count;
        }

        private static bool AllFinite(params double[] values)
        {
            foreach (double value in values)
                if (!double.IsFinite(value)) return false;
            return true;
        }
    }
}
