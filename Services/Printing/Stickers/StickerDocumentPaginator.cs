using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>Creates one custom-media WPF document page per sticker without building a large FixedDocument.</summary>
    public sealed class StickerDocumentPaginator : DocumentPaginator
    {
        private readonly IReadOnlyList<GlassStickerLabel> _rows;
        private readonly StickerPrintPreferences _preferences;
        private readonly StickerLayoutTemplate _template;
        private readonly long[] _pageEnds;
        private Size _pageSize;

        public StickerDocumentPaginator(
            IReadOnlyList<GlassStickerLabel> rows,
            StickerPrintPreferences preferences)
            : this(rows, preferences, preferences?.LastTemplate ?? StickerTemplatePresets.CreateSgu())
        {
        }

        public StickerDocumentPaginator(
            IReadOnlyList<GlassStickerLabel> rows,
            StickerPrintPreferences preferences,
            StickerLayoutTemplate template)
        {
            _rows = rows ?? Array.Empty<GlassStickerLabel>();
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _template = template?.DeepClone() ?? StickerTemplatePresets.CreateSgu();
            _pageSize = new Size(
                StickerLabelVisualFactory.MmToDip(_template.LabelWidthMm),
                StickerLabelVisualFactory.MmToDip(_template.LabelHeightMm));

            _pageEnds = new long[_rows.Count];
            long count = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row != null && row.WidthMm > 0 && row.HeightMm > 0 && row.Quantity > 0)
                    count += preferences.OneLabelPerPiece ? row.Quantity : 1;
                _pageEnds[i] = count;
            }
            PageCountLong = count;
        }

        public long PageCountLong { get; }
        public override int PageCount => checked((int)PageCountLong);
        public override bool IsPageCountValid => true;
        public override IDocumentPaginatorSource Source => null!;

        public override Size PageSize
        {
            get => _pageSize;
            set
            {
                if (value.Width <= 0 || value.Height <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Page dimensions must be positive.");
                _pageSize = value;
            }
        }

        public override DocumentPage GetPage(int pageNumber)
        {
            if (pageNumber < 0 || pageNumber >= PageCount)
                throw new ArgumentOutOfRangeException(nameof(pageNumber));

            var (source, printedQuantity) = FindRow(pageNumber);
            var page = new FixedPage
            {
                Width = PageSize.Width,
                Height = PageSize.Height
            };
            // The preview and physical paginator share the same visual factory;
            // one-per-piece mode only changes the quantity printed per page.
            var label = CloneWithPrintedQuantity(source, printedQuantity);
            page.Children.Add(StickerLabelVisualFactory.CreateTemplateCanvas(
                label, _template, printedQuantity));
            page.Measure(PageSize);
            page.Arrange(new Rect(new Point(0, 0), PageSize));
            page.UpdateLayout();

            var bounds = new Rect(0, 0, PageSize.Width, PageSize.Height);
            return new DocumentPage(page, PageSize, bounds, bounds);
        }

        private (GlassStickerLabel Label, int PrintedQuantity) FindRow(int pageNumber)
        {
            long target = pageNumber + 1L;
            int low = 0;
            int high = _pageEnds.Length - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (_pageEnds[mid] < target) low = mid + 1;
                else high = mid - 1;
            }

            if (low >= _rows.Count)
                throw new InvalidOperationException("The sticker page could not be mapped to a source row.");

            var row = _rows[low];
            int printedQuantity = _preferences.OneLabelPerPiece ? 1 : row.Quantity;
            return (row, printedQuantity);
        }

        private static GlassStickerLabel CloneWithPrintedQuantity(GlassStickerLabel source, int printedQuantity)
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
