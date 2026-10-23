using System;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    [Flags]
    public enum StickerResizeEdges
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8
    }

    /// <summary>Clamps drag-resize edits to the label while keeping the opposite edge anchored.</summary>
    public static class StickerElementResize
    {
        public const double MinimumElementSizeMm = 0.5;

        public static bool TryApply(
            StickerLayoutElement element,
            StickerLayoutTemplate template,
            StickerResizeEdges edges,
            double deltaXmm,
            double deltaYmm)
        {
            if (element == null || template == null || element.IsLocked || edges == StickerResizeEdges.None)
                return false;
            if (!double.IsFinite(template.LabelWidthMm) || !double.IsFinite(template.LabelHeightMm)
                || template.LabelWidthMm < MinimumElementSizeMm
                || template.LabelHeightMm < MinimumElementSizeMm)
                return false;

            double labelWidth = template.LabelWidthMm;
            double labelHeight = template.LabelHeightMm;
            double minimum = MinimumElementSizeMm;
            double x = Math.Clamp(double.IsFinite(element.Xmm) ? element.Xmm : 0,
                0, Math.Max(0, labelWidth - minimum));
            double y = Math.Clamp(double.IsFinite(element.Ymm) ? element.Ymm : 0,
                0, Math.Max(0, labelHeight - minimum));
            double width = Math.Clamp(double.IsFinite(element.WidthMm) ? element.WidthMm : minimum,
                minimum, Math.Max(minimum, labelWidth - x));
            double height = Math.Clamp(double.IsFinite(element.HeightMm) ? element.HeightMm : minimum,
                minimum, Math.Max(minimum, labelHeight - y));
            deltaXmm = double.IsFinite(deltaXmm) ? deltaXmm : 0;
            deltaYmm = double.IsFinite(deltaYmm) ? deltaYmm : 0;

            if ((edges & StickerResizeEdges.Left) != 0)
            {
                double anchoredRight = x + width;
                x = Math.Clamp(x + deltaXmm, 0, Math.Max(0, anchoredRight - minimum));
                width = anchoredRight - x;
            }
            else if ((edges & StickerResizeEdges.Right) != 0)
            {
                width = Math.Clamp(width + deltaXmm, minimum, Math.Max(minimum, labelWidth - x));
            }

            if ((edges & StickerResizeEdges.Top) != 0)
            {
                double anchoredBottom = y + height;
                y = Math.Clamp(y + deltaYmm, 0, Math.Max(0, anchoredBottom - minimum));
                height = anchoredBottom - y;
            }
            else if ((edges & StickerResizeEdges.Bottom) != 0)
            {
                height = Math.Clamp(height + deltaYmm, minimum, Math.Max(minimum, labelHeight - y));
            }

            element.Xmm = x;
            element.Ymm = y;
            element.WidthMm = width;
            element.HeightMm = height;
            return true;
        }
    }
}
