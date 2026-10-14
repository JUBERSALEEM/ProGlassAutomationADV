using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>Creates one custom-media WPF document page per physical sticker.</summary>
    public sealed class StickerDocumentPaginator : DocumentPaginator
    {
        private readonly StickerPrintPreferences _preferences;
        private readonly StickerLayoutTemplate _template;
        private readonly StickerLabelSequence _sequence;
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
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _template = template?.DeepClone() ?? StickerTemplatePresets.CreateSgu();
            _sequence = new StickerLabelSequence(rows, preferences.OneLabelPerPiece);
            _pageSize = new Size(
                StickerLabelVisualFactory.MmToDip(_template.LabelWidthMm),
                StickerLabelVisualFactory.MmToDip(_template.LabelHeightMm));
        }

        public long PageCountLong => _sequence.Count;
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

            var instance = _sequence.GetOneBased(pageNumber + 1L);
            var label = StickerLabelSequence.CopyWithPrintedQuantity(
                instance.Source, instance.PrintedQuantity);
            var page = StickerLabelVisualFactory.CreateTemplateCanvas(
                label, _template, instance.PrintedQuantity);
            page.Width = PageSize.Width;
            page.Height = PageSize.Height;
            page.Measure(PageSize);
            page.Arrange(new Rect(new Point(0, 0), PageSize));
            page.UpdateLayout();

            var bounds = new Rect(0, 0, PageSize.Width, PageSize.Height);
            return new DocumentPage(page, PageSize, bounds, bounds);
        }
    }
}
