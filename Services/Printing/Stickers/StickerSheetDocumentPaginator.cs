using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>
    /// Paginates a source-label sequence onto A4 sheets. Each sticker remains at
    /// its exact physical size; only the sheet page is paginated, never scaled.
    /// </summary>
    public sealed class StickerSheetDocumentPaginator : DocumentPaginator
    {
        private readonly StickerLayoutTemplate _template;
        private readonly StickerLabelSequence _sequence;
        private readonly StickerSheetLayoutPlan _plan;
        private Size _pageSize;

        public StickerSheetDocumentPaginator(
            IReadOnlyList<GlassStickerLabel> rows,
            StickerPrintPreferences preferences,
            StickerLayoutTemplate template,
            StickerSheetLayoutPlan plan)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            if (plan.LabelsPerSheet <= 0) throw new ArgumentException("The sheet plan has no sticker positions.", nameof(plan));

            _template = template?.DeepClone() ?? StickerTemplatePresets.CreateSgu();
            _sequence = new StickerLabelSequence(rows, preferences.OneLabelPerPiece);
            _plan = plan;
            _pageSize = new Size(
                StickerLabelVisualFactory.MmToDip(plan.PageWidthMm),
                StickerLabelVisualFactory.MmToDip(plan.PageHeightMm));
        }

        public long PageCountLong => _plan.PageCountFor(_sequence.Count);
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

            var page = new Canvas
            {
                Width = PageSize.Width,
                Height = PageSize.Height,
                Background = Brushes.White,
                ClipToBounds = true
            };

            long firstIndex = (long)pageNumber * _plan.LabelsPerSheet + 1;
            long remaining = _sequence.Count - firstIndex + 1;
            int labelsOnThisPage = (int)Math.Min(_plan.LabelsPerSheet, remaining);
            for (int slot = 0; slot < labelsOnThisPage; slot++)
            {
                var instance = _sequence.GetOneBased(firstIndex + slot);
                var label = StickerLabelSequence.CopyWithPrintedQuantity(
                    instance.Source, instance.PrintedQuantity);
                var sticker = StickerLabelVisualFactory.CreateTemplateCanvas(
                    label, _template, instance.PrintedQuantity);
                sticker.Width = StickerLabelVisualFactory.MmToDip(_plan.LabelWidthMm);
                sticker.Height = StickerLabelVisualFactory.MmToDip(_plan.LabelHeightMm);
                var position = _plan.PositionForSlot(slot);
                Canvas.SetLeft(sticker, StickerLabelVisualFactory.MmToDip(position.Xmm));
                Canvas.SetTop(sticker, StickerLabelVisualFactory.MmToDip(position.Ymm));
                page.Children.Add(sticker);
            }

            page.Measure(PageSize);
            page.Arrange(new Rect(new Point(0, 0), PageSize));
            page.UpdateLayout();
            var bounds = new Rect(0, 0, PageSize.Width, PageSize.Height);
            return new DocumentPage(page, PageSize, bounds, bounds);
        }
    }
}
