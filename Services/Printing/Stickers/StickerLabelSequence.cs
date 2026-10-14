using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    internal readonly record struct StickerPrintLabelInstance(
        GlassStickerLabel Source,
        int PrintedQuantity);

    /// <summary>
    /// Maps a one-based physical-sticker index to its source row without expanding
    /// repeated quantities into a potentially large in-memory list.
    /// </summary>
    internal sealed class StickerLabelSequence
    {
        private readonly IReadOnlyList<GlassStickerLabel> _rows;
        private readonly bool _oneLabelPerPiece;
        private readonly long[] _rowEnds;

        public StickerLabelSequence(
            IReadOnlyList<GlassStickerLabel>? rows,
            bool oneLabelPerPiece)
        {
            _rows = rows ?? Array.Empty<GlassStickerLabel>();
            _oneLabelPerPiece = oneLabelPerPiece;
            _rowEnds = new long[_rows.Count];

            long count = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (IsPrintable(row))
                    count += oneLabelPerPiece ? row.Quantity : 1;
                _rowEnds[i] = count;
            }
            Count = count;
        }

        public long Count { get; }

        public StickerPrintLabelInstance GetOneBased(long index)
        {
            if (index < 1 || index > Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            int low = 0;
            int high = _rowEnds.Length - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (_rowEnds[mid] < index) low = mid + 1;
                else high = mid - 1;
            }

            if (low >= _rows.Count || !IsPrintable(_rows[low]))
                throw new InvalidOperationException("The sticker index could not be mapped to a source row.");

            var source = _rows[low];
            return new StickerPrintLabelInstance(source, _oneLabelPerPiece ? 1 : source.Quantity);
        }

        public static bool IsPrintable(GlassStickerLabel? row)
            => row != null
                && double.IsFinite(row.WidthMm)
                && double.IsFinite(row.HeightMm)
                && row.WidthMm > 0
                && row.HeightMm > 0
                && row.Quantity > 0;

        public static GlassStickerLabel CopyWithPrintedQuantity(
            GlassStickerLabel source,
            int printedQuantity)
        {
            return new GlassStickerLabel
            {
                Company = source.Company,
                OrderNumber = source.OrderNumber,
                JobOrderNumber = source.JobOrderNumber,
                ProformaNumber = source.ProformaNumber,
                CustomerCompanyName = source.CustomerCompanyName,
                GlassName = source.GlassName,
                GlassType = source.GlassType,
                GlassReference = source.GlassReference,
                WidthMm = source.WidthMm,
                HeightMm = source.HeightMm,
                Quantity = source.Quantity,
                PrintedQuantity = printedQuantity,
                Reference = source.Reference,
                PanePosition = source.PanePosition,
                Source = source.Source,
                SpecGroupNumber = source.SpecGroupNumber,
                SerialNumber = source.SerialNumber
            };
        }
    }
}
