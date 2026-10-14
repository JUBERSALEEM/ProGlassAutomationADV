using System;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels.Printing.Stickers;
using Xunit;

namespace ProGlassAutomation.Tests.Printing.Stickers
{
    public sealed class StickerSheetLayoutTests
    {
        [Fact]
        public void SmallStickersAreArrangedAtTrueSizeAndTwelveFitOnOneA4Sheet()
        {
            Assert.True(StickerSheetLayoutPlanner.TryCreateNominalA4Plan(
                labelWidthMm: 50,
                labelHeightMm: 30,
                sheetMarginMm: 5,
                gapMm: 2,
                out var plan,
                out string error), error);

            Assert.Equal(StickerSheetOrientation.Landscape, plan.Orientation);
            Assert.Equal(5, plan.Columns);
            Assert.Equal(6, plan.Rows);
            Assert.Equal(30, plan.LabelsPerSheet);
            Assert.Equal(1, plan.PageCountFor(12));
            Assert.Equal(1, plan.PageCountFor(30));
            Assert.Equal(2, plan.PageCountFor(31));

            var first = plan.PositionForSlot(0);
            var second = plan.PositionForSlot(1);
            var nextRow = plan.PositionForSlot(plan.Columns);
            Assert.Equal(plan.LabelWidthMm + plan.HorizontalGapMm, second.Xmm - first.Xmm, 6);
            Assert.Equal(plan.LabelHeightMm + plan.VerticalGapMm, nextRow.Ymm - first.Ymm, 6);
            Assert.Equal(50, plan.LabelWidthMm);
            Assert.Equal(30, plan.LabelHeightMm);
        }

        [Fact]
        public void PlannerHonoursThePrintersImageableAreaAndConfiguredInset()
        {
            Assert.True(StickerSheetLayoutPlanner.TryCreatePlan(
                StickerSheetOrientation.Portrait,
                pageWidthMm: 210,
                pageHeightMm: 297,
                imageableLeftMm: 8,
                imageableTopMm: 6,
                imageableWidthMm: 194,
                imageableHeightMm: 285,
                labelWidthMm: 50,
                labelHeightMm: 40,
                sheetMarginMm: 5,
                gapMm: 2,
                out var plan,
                out string error), error);

            Assert.True(plan.GridLeftMm >= 8);
            Assert.True(plan.GridTopMm >= 6);
            Assert.True(plan.GridLeftMm + plan.GridWidthMm <= 202 + 1e-6);
            Assert.True(plan.GridTopMm + plan.GridHeightMm <= 291 + 1e-6);
        }

        [Fact]
        public void PlannerRejectsStickersThatCannotFitAndInvalidDimensions()
        {
            Assert.False(StickerSheetLayoutPlanner.TryCreateNominalA4Plan(
                205, 290, 5, 2, out _, out string tooLargeError));
            Assert.Contains("does not fit", tooLargeError);

            Assert.False(StickerSheetLayoutPlanner.TryCreateNominalA4Plan(
                double.NaN, 30, 5, 2, out _, out string invalidError));
            Assert.Contains("valid", invalidError);
        }

        [Fact]
        public void SheetPaginatorGroupsTwelvePhysicalStickerCopiesOntoOneA4Page()
        {
            var labels = new[]
            {
                new GlassStickerLabel { WidthMm = 700, HeightMm = 500, Quantity = 12 }
            };
            var preferences = new StickerPrintPreferences { OneLabelPerPiece = true };
            var template = StickerTemplatePresets.CreateCustom("Small A4 sticker", 50, 30);
            Assert.True(StickerSheetLayoutPlanner.TryCreateNominalA4Plan(
                template.LabelWidthMm, template.LabelHeightMm, 5, 2, out var plan, out string error), error);

            var paginator = new StickerSheetDocumentPaginator(labels, preferences, template, plan);

            Assert.Equal(12, paginator.PageCountLong);
            Assert.Equal(1, paginator.PageCount);
            Assert.Equal(StickerLabelVisualFactory.MmToDip(StickerSheetLayoutPlanner.A4HeightMm), paginator.PageSize.Width, 4);
            Assert.Equal(StickerLabelVisualFactory.MmToDip(StickerSheetLayoutPlanner.A4WidthMm), paginator.PageSize.Height, 4);
        }

        [Fact]
        public void StickerSequencePreservesSourceOrderForRepeatedAndAggregateRows()
        {
            var first = new GlassStickerLabel
            {
                Reference = "A",
                WidthMm = 50,
                HeightMm = 30,
                Quantity = 3
            };
            var second = new GlassStickerLabel
            {
                Reference = "B",
                WidthMm = 60,
                HeightMm = 40,
                Quantity = 2
            };

            var repeated = new StickerLabelSequence(new[] { first, second }, oneLabelPerPiece: true);
            Assert.Equal(5, repeated.Count);
            Assert.Equal("A", repeated.GetOneBased(3).Source.Reference);
            Assert.Equal(1, repeated.GetOneBased(3).PrintedQuantity);
            Assert.Equal("B", repeated.GetOneBased(4).Source.Reference);
            Assert.Equal(1, repeated.GetOneBased(5).PrintedQuantity);

            var aggregate = new StickerLabelSequence(new[] { first, second }, oneLabelPerPiece: false);
            Assert.Equal(2, aggregate.Count);
            Assert.Equal(3, aggregate.GetOneBased(1).PrintedQuantity);
            Assert.Equal(2, aggregate.GetOneBased(2).PrintedQuantity);
        }

        [Fact]
        public void PrinterDefaultUsesA4ForStandardPrintersAndKeepsZt411OnIndividualMedia()
        {
            var row = new GlassStickerLabel { WidthMm = 700, HeightMm = 500, Quantity = 12 };
            var viewModel = new StickerPrintDialogViewModel(
                new[] { row }, new StickerPrintPreferences());

            Assert.True(viewModel.IsA4SheetLayout);
            Assert.Equal(StickerOutputLayoutMode.A4Sheet,
                StickerPrintEngine.ResolveOutputLayout(new StickerPrintPreferences()));
            Assert.True(viewModel.TryCreatePreferences(out var customPreferences, out string customError), customError);
            Assert.Equal(StickerOutputLayoutMode.PrinterDefault, customPreferences.OutputLayoutMode);

            viewModel.ApplyZt411FourBySixStock();
            Assert.False(viewModel.IsA4SheetLayout);
            Assert.Equal(StickerOutputLayoutMode.OneLabelPerPage,
                StickerPrintEngine.ResolveOutputLayout(new StickerPrintPreferences
                {
                    PrinterModel = StickerPrinterModel.ZebraZt411,
                    OutputLayoutMode = StickerOutputLayoutMode.PrinterDefault
                }));
            Assert.True(viewModel.TryCreatePreferences(out var ztPreferences, out string ztError), ztError);
            Assert.Equal(101.6, ztPreferences.LabelWidthMm);
            Assert.Equal(152.4, ztPreferences.LabelHeightMm);

            viewModel.OutputLayoutMode = StickerOutputLayoutMode.A4Sheet;
            Assert.Equal(StickerOutputLayoutMode.PrinterDefault, viewModel.OutputLayoutMode);
            Assert.False(viewModel.IsA4SheetLayout);
        }
    }
}
