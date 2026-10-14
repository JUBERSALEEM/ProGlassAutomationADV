using System;
using System.IO;
using System.Linq;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels.Printing.Stickers;
using Xunit;

namespace ProGlassAutomation.Tests.Printing.Stickers
{
    public sealed class StickerTemplateStoreTests
    {
        [Fact]
        public void BuiltInTemplatesCoverSguDguAndProformaJobFormats()
        {
            var templates = StickerTemplatePresets.All;

            Assert.Contains(templates, t => t.Kind == StickerTemplateKind.Sgu);
            Assert.Contains(templates, t => t.Kind == StickerTemplateKind.Dgu);
            Assert.Contains(templates, t => t.Kind == StickerTemplateKind.ProformaInvoice);
            Assert.All(templates, template => Assert.True(template.Elements.Count > 0));
            Assert.Contains(templates.SelectMany(t => t.Elements), e => e.Kind == StickerTemplateElementKind.Logo);
            Assert.Contains(templates.SelectMany(t => t.Elements), e => e.FieldKey == "GlassType");
            Assert.Contains(templates.SelectMany(t => t.Elements), e => e.FieldKey == "CompanyTelephone");
            Assert.Contains(templates.SelectMany(t => t.Elements), e => e.FieldKey == "CompanyWebsite");
            Assert.Contains(templates.SelectMany(t => t.Elements), e => e.FieldKey == "CompanyEmail");
        }

        [Fact]
        public void TemplateChoicesExposeReadableNamesAndHumanFriendlyStyleFamilies()
        {
            var template = StickerTemplatePresets.CreateSgu();
            var choice = new StickerTemplateChoice(template.Name, template, isBuiltIn: true);

            Assert.Equal(template.Name, choice.TemplateName);
            Assert.Equal("SGU · Single glass", choice.KindDisplayName);
            Assert.Equal("PRESET", choice.SourceLabel);
            Assert.Equal(template.Name, choice.DisplayName);
            Assert.Equal("LAST USED", new StickerTemplateChoice(
                template.Name + " · last used", template, isBuiltIn: false).SourceLabel);
            Assert.Equal("EXPORTED", new StickerTemplateChoice(
                template.Name + " · saved", template, isBuiltIn: false,
                filePath: Path.Combine(Path.GetTempPath(), "exported.pgsticker")).SourceLabel);
            Assert.Equal("LIBRARY", new StickerTemplateChoice(
                template.Name + " · saved", template, isBuiltIn: false,
                filePath: Path.Combine(StickerTemplateStore.LibraryDirectory, "saved.pgsticker")).SourceLabel);

            choice.UpdateTemplateName("Workshop SGU label");
            Assert.Equal("Workshop SGU label", choice.TemplateName);
            Assert.Equal("Workshop SGU label", choice.DisplayName);
        }

        [Fact]
        public void CanvasResizeKeepsTheOppositeEdgesAnchoredAndClampsToLabel()
        {
            var template = StickerTemplatePresets.CreateCustom("Resize test", 100, 50);
            var element = new StickerLayoutElement
            {
                Xmm = 20,
                Ymm = 10,
                WidthMm = 30,
                HeightMm = 15
            };

            Assert.True(StickerElementResize.TryApply(element, template,
                StickerResizeEdges.Left | StickerResizeEdges.Top, 5, 4));
            Assert.Equal(25, element.Xmm);
            Assert.Equal(14, element.Ymm);
            Assert.Equal(25, element.WidthMm);
            Assert.Equal(11, element.HeightMm);
            Assert.Equal(50, element.Xmm + element.WidthMm);
            Assert.Equal(25, element.Ymm + element.HeightMm);

            Assert.True(StickerElementResize.TryApply(element, template,
                StickerResizeEdges.Right | StickerResizeEdges.Bottom, 100, 100));
            Assert.Equal(100, element.Xmm + element.WidthMm);
            Assert.Equal(50, element.Ymm + element.HeightMm);
        }

        [Fact]
        public void CanvasResizeHonorsMinimumSizeAndLockedElements()
        {
            var template = StickerTemplatePresets.CreateCustom("Resize test", 100, 50);
            var element = new StickerLayoutElement
            {
                Xmm = 10,
                Ymm = 10,
                WidthMm = 20,
                HeightMm = 12
            };

            Assert.True(StickerElementResize.TryApply(element, template,
                StickerResizeEdges.Left, 100, 0));
            Assert.Equal(29.5, element.Xmm);
            Assert.Equal(StickerElementResize.MinimumElementSizeMm, element.WidthMm);
            double widthBeforeLock = element.WidthMm;
            element.IsLocked = true;
            Assert.False(StickerElementResize.TryApply(element, template,
                StickerResizeEdges.Right, 5, 0));
            Assert.Equal(widthBeforeLock, element.WidthMm);
        }

        [Fact]
        public void ZebraZt411ProfileUsesItsPrintableWidthAndResolutionSpecificLengthRanges()
        {
            var standard = StickerPrinterSpecifications.GetLimits(
                StickerPrinterModel.ZebraZt411,
                StickerPrinterResolution.Dpi203,
                StickerMediaHandling.TearOff);
            Assert.Equal(25.4, standard.MinimumLabelWidthMm);
            Assert.Equal(104, standard.MaximumPrintableWidthMm);
            Assert.Equal(12.7, standard.MinimumLabelLengthMm);
            Assert.Equal(991, standard.MaximumLabelLengthMm);
            Assert.Equal(114, standard.MaximumMediaWebWidthMm);
            Assert.True(StickerPrinterSpecifications.TryValidateDimensions(
                StickerPrinterModel.ZebraZt411,
                StickerPrinterResolution.Dpi203,
                StickerMediaHandling.TearOff,
                101.6, 152.4, out _));
            Assert.False(StickerPrinterSpecifications.TryValidateDimensions(
                StickerPrinterModel.ZebraZt411,
                StickerPrinterResolution.Dpi203,
                StickerMediaHandling.TearOff,
                104.1, 152.4, out _));

            var highResolution = StickerPrinterSpecifications.GetLimits(
                StickerPrinterModel.ZebraZt411,
                StickerPrinterResolution.Dpi600,
                StickerMediaHandling.PeelOff);
            Assert.Equal(108, highResolution.MaximumMediaWebWidthMm);
            Assert.Equal(508, highResolution.MaximumLabelLengthMm);

            var cutter = StickerPrinterSpecifications.GetLimits(
                StickerPrinterModel.ZebraZt411,
                StickerPrinterResolution.Dpi300,
                StickerMediaHandling.Cutter);
            Assert.Equal(25.4, cutter.MinimumLabelLengthMm);
            Assert.Equal(152.4, cutter.MaximumLabelLengthMm);
        }

        [Fact]
        public void PrinterProfileKeepsFourBySixPresetAndManualDimensionsEditable()
        {
            var viewModel = new StickerPrintDialogViewModel(
                new[] { new GlassStickerLabel { WidthMm = 100, HeightMm = 50, Quantity = 1 } },
                new StickerPrintPreferences());

            viewModel.ApplyZt411FourBySixStock();
            Assert.Equal(StickerPrinterModel.ZebraZt411, viewModel.PrinterModel);
            Assert.Equal(101.6, viewModel.ActiveTemplate.LabelWidthMm);
            Assert.Equal(152.4, viewModel.ActiveTemplate.LabelHeightMm);
            Assert.True(viewModel.TryCreatePreferences(out var preferences, out _, requirePrintableRows: false));
            Assert.Equal(StickerPrinterModel.ZebraZt411, preferences.PrinterModel);
            Assert.Equal(101.6, preferences.LabelWidthMm);
            Assert.Equal(152.4, preferences.LabelHeightMm);

            viewModel.WidthMmText = "90";
            viewModel.HeightMmText = "120";
            Assert.Equal(90, viewModel.ActiveTemplate.LabelWidthMm);
            Assert.Equal(120, viewModel.ActiveTemplate.LabelHeightMm);
            Assert.True(viewModel.TryCreatePreferences(out preferences, out _, requirePrintableRows: false));
            Assert.Equal(90, preferences.LabelWidthMm);
            Assert.Equal(120, preferences.LabelHeightMm);

            viewModel.WidthMmText = "110";
            Assert.False(viewModel.TryCreatePreferences(out _, out string error, requirePrintableRows: false));
            Assert.Contains("104 mm", error);
        }

        [Fact]
        public void EditorSupportsDuplicateLockNudgeAndDeleteActions()
        {
            var viewModel = new StickerPrintDialogViewModel(
                new[] { new GlassStickerLabel { WidthMm = 100, HeightMm = 50, Quantity = 1 } },
                new StickerPrintPreferences());
            viewModel.CreateNewTemplate();
            viewModel.AddTextElement();
            var original = Assert.IsType<StickerLayoutElement>(viewModel.SelectedElement);

            viewModel.DuplicateSelectedElement();
            Assert.Equal(2, viewModel.TemplateElements.Count);
            var copy = Assert.IsType<StickerLayoutElement>(viewModel.SelectedElement);
            double lockedX = copy.Xmm;
            double lockedY = copy.Ymm;
            copy.IsLocked = true;
            viewModel.NudgeSelectedElement(5, 5);
            viewModel.SetSelectedElementPosition(30, 30);
            Assert.Equal(lockedX, copy.Xmm);
            Assert.Equal(lockedY, copy.Ymm);
            Assert.False(viewModel.CanMoveSelectedElement);

            copy.IsLocked = false;
            viewModel.SetSelectedElementPosition(3, 5);
            Assert.Equal(3, copy.Xmm);
            Assert.Equal(5, copy.Ymm);
            viewModel.MoveSelectedElementLayer(-1);
            Assert.Same(copy, viewModel.TemplateElements[0]);
            viewModel.DeleteSelectedElement();
            Assert.Single(viewModel.TemplateElements);
            Assert.Same(original, viewModel.SelectedElement);
        }

        [Fact]
        public void StandaloneDesignerCanPersistAReusableLayoutWithoutPrintRows()
        {
            var previewLabel = new GlassStickerLabel
            {
                OrderNumber = "SAMPLE ONLY",
                GlassName = "PREVIEW",
                WidthMm = 100,
                HeightMm = 50,
                Quantity = 1
            };
            var viewModel = new StickerPrintDialogViewModel(
                Array.Empty<GlassStickerLabel>(), new StickerPrintPreferences(), previewLabel);

            Assert.Empty(viewModel.Items);
            Assert.Same(previewLabel, viewModel.SelectedLabel);
            viewModel.SelectedLabel = null;
            Assert.Same(previewLabel, viewModel.SelectedLabel);
            Assert.Equal(0, viewModel.EstimatedLabelCount);
            Assert.False(viewModel.TryCreatePreferences(out _, out _));
            Assert.True(viewModel.TryCreatePreferences(out var preferences, out _, requirePrintableRows: false));
            Assert.NotNull(preferences.LastTemplate);
            Assert.Equal(viewModel.ActiveTemplate.Name, preferences.LastTemplateName);
        }

        [Fact]
        public void PortablePgStickerFileRoundTripsGeometryStylesAndBindings()
        {
            string path = Path.Combine(Path.GetTempPath(), "pg-sticker-" + Guid.NewGuid().ToString("N")
                + StickerTemplateStore.FileExtension);
            try
            {
                var template = StickerTemplatePresets.CreateDgu();
                var editable = Assert.Single(template.Elements.Where(e => e.FieldKey == "GlassName"));
                editable.Xmm = 11.5;
                editable.Ymm = 28.25;
                editable.FontFamily = "Arial";
                editable.FontSizePt = 14;
                editable.Bold = true;
                editable.Italic = true;
                editable.IsLocked = true;
                editable.TextAlignment = StickerTextAlignment.Center;
                template.Name = "Custom DGU Shop Label";

                Assert.True(StickerTemplateStore.TrySaveFile(path, template, out string saveError), saveError);
                Assert.True(StickerTemplateStore.TryLoadFile(path, out var restored, out string loadError), loadError);

                Assert.NotNull(restored);
                Assert.Equal("Custom DGU Shop Label", restored!.Name);
                Assert.Equal(StickerTemplateKind.Dgu, restored.Kind);
                var restoredElement = Assert.Single(restored.Elements.Where(e => e.FieldKey == "GlassName"));
                Assert.Equal(11.5, restoredElement.Xmm);
                Assert.Equal(28.25, restoredElement.Ymm);
                Assert.Equal("Arial", restoredElement.FontFamily);
                Assert.Equal(14, restoredElement.FontSizePt);
                Assert.True(restoredElement.Bold);
                Assert.True(restoredElement.Italic);
                Assert.True(restoredElement.IsLocked);
                Assert.Equal(StickerTextAlignment.Center, restoredElement.TextAlignment);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void TemplateValidationRejectsUnknownFieldsAndUnsafeDimensions()
        {
            var template = StickerTemplatePresets.CreateSgu();
            template.Elements.First(e => e.Kind == StickerTemplateElementKind.Text).FieldKey = "ArbitrarySqlOrCode";
            Assert.False(StickerTemplateStore.TryValidate(template, out string fieldError));
            Assert.Contains("UNKNOWN DATA FIELD", fieldError.ToUpperInvariant());

            template = StickerTemplatePresets.CreateSgu();
            template.LabelWidthMm = double.NaN;
            Assert.False(StickerTemplateStore.TryValidate(template, out string dimensionError));
            Assert.Contains("WIDTH MUST", dimensionError.ToUpperInvariant());

            template = StickerTemplatePresets.CreateCustom("ZT411 long stock", 104, 991);
            Assert.True(StickerTemplateStore.TryValidate(template, out string longStockError), longStockError);
            template.LabelHeightMm = 12.6;
            Assert.False(StickerTemplateStore.TryValidate(template, out _));
        }

        [Fact]
        public void TemplateFieldResolverUsesTheCurrentPaneAndPrintedQuantity()
        {
            var label = new GlassStickerLabel
            {
                Company = new StickerCompanyProfile
                {
                    Telephone = "+971 6 555 0100",
                    RepresentativeMobile = "+971 50 111 2222"
                },
                GlassType = "DGU",
                PanePosition = "Inner",
                Quantity = 4,
                PrintedQuantity = 1,
                WidthMm = 730,
                HeightMm = 555
            };

            Assert.Equal("DGU", StickerLabelVisualFactory.ResolveFieldValue(label, "GlassType"));
            Assert.Equal("+971 6 555 0100", StickerLabelVisualFactory.ResolveFieldValue(label, "CompanyTelephone"));
            Assert.Equal("+971 50 111 2222", StickerLabelVisualFactory.ResolveFieldValue(label, "CompanyMobile"));
            Assert.Equal("Inner", StickerLabelVisualFactory.ResolveFieldValue(label, "PanePosition"));
            Assert.Equal("1", StickerLabelVisualFactory.ResolveFieldValue(label, "Quantity"));
            Assert.Equal("4", StickerLabelVisualFactory.ResolveFieldValue(label, "Quantity", 4));
        }
    }
}
