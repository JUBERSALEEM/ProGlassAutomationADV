// DGU / LAM inner-pane size rule.
// Equal-size inherit is ONLY when W2 == 0 AND H2 == 0.
// A single missing axis (0×500, 1000×0) is incomplete — never inherited.

namespace ProGlassAutomation.Services.Production
{
    public enum DguInnerSizeKind
    {
        EqualSize,
        Overlap,
        Incomplete
    }

    public static class DguDimensionSemantics
    {
        /// <summary>
        /// Resolves the inner (pane 2) cutting size from W1/H1 and W2/H2.
        /// </summary>
        /// <returns>
        /// True when the inner pane has a complete size. False for incomplete
        /// pairs — the caller must skip the pane and warn, not invent a size.
        /// </returns>
        public static bool TryResolveInnerSize(
            int w1, int h1, int w2, int h2,
            out int widthMm, out int heightMm,
            out DguInnerSizeKind kind)
        {
            widthMm = 0;
            heightMm = 0;

            if (w2 == 0 && h2 == 0)
            {
                if (w1 > 0 && h1 > 0)
                {
                    widthMm = w1;
                    heightMm = h1;
                    kind = DguInnerSizeKind.EqualSize;
                    return true;
                }

                kind = DguInnerSizeKind.Incomplete;
                return false;
            }

            if (w2 > 0 && h2 > 0)
            {
                widthMm = w2;
                heightMm = h2;
                kind = DguInnerSizeKind.Overlap;
                return true;
            }

            // One axis present, the other zero: 0×500 and 1000×0 are invalid.
            kind = DguInnerSizeKind.Incomplete;
            return false;
        }
    }
}
