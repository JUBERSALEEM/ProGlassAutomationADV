using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
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
        public void PrinterAndTemplateKindOptionsHaveHumanReadableFallbackText()
        {
            Assert.Equal("Zebra ZT411", new StickerPrinterOption<StickerPrinterModel>(
                StickerPrinterModel.ZebraZt411, "Zebra ZT411").ToString());
            Assert.Equal("A4 sheet", new StickerPrinterOption<StickerOutputLayoutMode>(
                StickerOutputLayoutMode.A4Sheet, "A4 sheet").ToString());
            Assert.Equal("300 dpi", new StickerPrinterOption<StickerPrinterResolution>(
                StickerPrinterResolution.Dpi300, "300 dpi").ToString());
            Assert.Equal("Peel-off", new StickerPrinterOption<StickerMediaHandling>(
                StickerMediaHandling.PeelOff, "Peel-off").ToString());
            Assert.Equal("SGU · Single glass", new StickerTemplateKindOption(
                StickerTemplateKind.Sgu, "SGU · Single glass").ToString());
        }

        [Fact]
        public void SavingDifferentTemplatesWithTheSameNameDoesNotOverwriteTheLibraryEntry()
        {
            string uniqueName = "Collision test " + Guid.NewGuid().ToString("N");
            string firstPath = string.Empty;
            string secondPath = string.Empty;
            try
            {
                var first = StickerTemplatePresets.CreateCustom(uniqueName);
                var second = StickerTemplatePresets.CreateCustom(uniqueName);

                Assert.True(StickerTemplateStore.TrySaveToLibrary(first, out firstPath, out string firstError), firstError);
                Assert.True(StickerTemplateStore.TrySaveToLibrary(second, out secondPath, out string secondError), secondError);

                Assert.NotEqual(firstPath, secondPath);
                Assert.Contains("(2)", Path.GetFileName(secondPath));
                Assert.True(StickerTemplateStore.TryLoadFile(firstPath, out var restored, out string loadError), loadError);
                Assert.Equal(first.TemplateId, restored!.TemplateId);

                first.LabelWidthMm = 101;
                Assert.True(StickerTemplateStore.TrySaveToLibrary(first, out string updatedPath, out string updateError), updateError);
                Assert.Equal(firstPath, updatedPath);
                Assert.True(StickerTemplateStore.TryLoadFile(firstPath, out restored, out loadError), loadError);
                Assert.Equal(101, restored!.LabelWidthMm);
            }
            finally
            {
                if (File.Exists(firstPath)) File.Delete(firstPath);
                if (File.Exists(secondPath)) File.Delete(secondPath);
            }
        }

        [Fact]
        public void PrintEnginePreflightCountsOnlyValidRowsAndRejectsBatchesOverTheLimit()
        {
            var labels = new[]
            {
                new GlassStickerLabel { WidthMm = 100, HeightMm = 50, Quantity = 50_000 },
                new GlassStickerLabel { WidthMm = 0, HeightMm = 50, Quantity = 10 },
                new GlassStickerLabel { WidthMm = double.NaN, HeightMm = 50, Quantity = 10 },
                new GlassStickerLabel { WidthMm = 100, HeightMm = double.PositiveInfinity, Quantity = 10 },
                new GlassStickerLabel { WidthMm = 100, HeightMm = 50, Quantity = 1 }
            };

            Assert.False(StickerPrintEngine.TryValidateBatch(labels, true, out long oversizedCount, out string error));
            Assert.Equal(50_001, oversizedCount);
            Assert.Contains("50,000", error);

            Assert.True(StickerPrintEngine.TryValidateBatch(labels, false, out long rowCount, out _));
            Assert.Equal(2, rowCount);

            Assert.False(StickerPrintEngine.TryValidateBatch(
                new[] { new GlassStickerLabel { WidthMm = 0, HeightMm = 50, Quantity = 1 } },
                true, out long emptyCount, out _));
            Assert.Equal(0, emptyCount);
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
        public void StandaloneDesignerUsesTemplateSpecificSampleAndPersistsWithoutPrintRows()
        {
            var viewModel = new StickerPrintDialogViewModel(
                Array.Empty<GlassStickerLabel>(), new StickerPrintPreferences());

            Assert.Empty(viewModel.Items);
            Assert.True(viewModel.IsSamplePreview);
            Assert.Equal(StickerTemplateKind.Sgu, viewModel.TemplateKind);
            Assert.Equal("SGU", viewModel.SelectedLabel!.GlassType);
            Assert.StartsWith("SAMPLE", viewModel.SelectedLabel.GlassName);
            viewModel.SelectedLabel = null;
            Assert.Equal("SGU", viewModel.SelectedLabel!.GlassType);
            viewModel.TemplateKind = StickerTemplateKind.Dgu;
            Assert.Equal("DGU", viewModel.SelectedLabel!.GlassType);
            viewModel.TemplateKind = StickerTemplateKind.Sgu;
            Assert.Equal("SGU", viewModel.SelectedLabel!.GlassType);
            Assert.Equal(0, viewModel.EstimatedLabelCount);
            Assert.False(viewModel.TryCreatePreferences(out _, out _));
            Assert.True(viewModel.TryCreatePreferences(out var preferences, out _, requirePrintableRows: false));
            Assert.NotNull(preferences.LastTemplate);
            Assert.Equal(viewModel.ActiveTemplate.Name, preferences.LastTemplateName);

            viewModel.SelectTemplateChoice(viewModel.TemplateChoices.First(choice =>
                choice.Template.Kind == StickerTemplateKind.Dgu));
            Assert.Equal("DGU", viewModel.SelectedLabel!.GlassType);
            Assert.StartsWith("SAMPLE", viewModel.SelectedLabel.GlassName);

            viewModel.SelectTemplateChoice(viewModel.TemplateChoices.First(choice =>
                choice.Template.Kind == StickerTemplateKind.ProformaInvoice));
            var proformaSample = viewModel.SelectedLabel!;
            Assert.Equal("DGU", proformaSample.GlassType);
            Assert.Equal("SAMPLE-JOB-001", proformaSample.JobOrderNumber);
            Assert.Equal("SAMPLE-PI-001", proformaSample.ProformaNumber);
            Assert.Contains("SAMPLE-JOB-001 / SAMPLE-PI-001", proformaSample.OrderDisplay);
            Assert.Equal("SAMPLE COMPANY TELEPHONE", proformaSample.Company.Telephone);
            Assert.Equal("SAMPLE REPRESENTATIVE MOBILE", proformaSample.Company.RepresentativeMobile);
            Assert.All(viewModel.ActiveTemplate.Elements.Where(element =>
                    element.Kind == StickerTemplateElementKind.Text
                    && !string.IsNullOrWhiteSpace(element.FieldKey)),
                element => Assert.False(string.IsNullOrWhiteSpace(
                    StickerLabelVisualFactory.ResolveFieldValue(proformaSample, element.FieldKey))));
        }

        [Fact]
        public void SampleGlassDimensionsUpdatePreviewFieldsWithoutChangingTemplateOrSourceData()
        {
            var viewModel = new StickerPrintDialogViewModel(
                Array.Empty<GlassStickerLabel>(), new StickerPrintPreferences());
            var sample = viewModel.SelectedLabel!;
            var sizeField = viewModel.ActiveTemplate.Elements.First(element => element.FieldKey == "SizeText");
            double templateWidth = viewModel.ActiveTemplate.LabelWidthMm;
            double templateHeight = viewModel.ActiveTemplate.LabelHeightMm;

            viewModel.SelectedElement = sizeField;
            Assert.True(viewModel.CanEditSampleDimensions);
            Assert.Equal("700", viewModel.SampleWidthMmText);
            Assert.Equal("450", viewModel.SampleHeightMmText);

            viewModel.SampleWidthMmText = "820";
            viewModel.SampleHeightMmText = "515";

            Assert.Equal(820, viewModel.SelectedLabel!.WidthMm);
            Assert.Equal(515, viewModel.SelectedLabel.HeightMm);
            Assert.Equal("820", StickerLabelVisualFactory.ResolveFieldValue(sample, "WidthMm"));
            Assert.Equal("515", StickerLabelVisualFactory.ResolveFieldValue(sample, "HeightMm"));
            Assert.Equal("820 × 515 mm", StickerLabelVisualFactory.ResolveFieldValue(sample, "SizeText"));
            Assert.Equal(templateWidth, viewModel.ActiveTemplate.LabelWidthMm);
            Assert.Equal(templateHeight, viewModel.ActiveTemplate.LabelHeightMm);
            Assert.False(viewModel.IsTemplateDirty);

            viewModel.SelectedElement = viewModel.ActiveTemplate.Elements.First(element =>
                element.FieldKey == "CompanyName");
            Assert.False(viewModel.CanEditSampleDimensions);
        }

        [Fact]
        public void SampleDimensionsRejectInvalidInputAndNeverEditProductionRows()
        {
            var sampleViewModel = new StickerPrintDialogViewModel(
                Array.Empty<GlassStickerLabel>(), new StickerPrintPreferences());
            sampleViewModel.SelectedElement = sampleViewModel.ActiveTemplate.Elements.First(element =>
                element.FieldKey == "SizeText");
            sampleViewModel.SampleWidthMmText = "0";
            Assert.Equal(700, sampleViewModel.SelectedLabel!.WidthMm);
            Assert.Contains("whole number", sampleViewModel.SampleDimensionsWarningText);

            sampleViewModel.SampleWidthMmText = "805";
            Assert.Equal(805, sampleViewModel.SelectedLabel!.WidthMm);
            Assert.Equal(string.Empty, sampleViewModel.SampleDimensionsWarningText);

            var sourceRow = new GlassStickerLabel
            {
                WidthMm = 740,
                HeightMm = 460,
                Quantity = 1
            };
            var productionViewModel = new StickerPrintDialogViewModel(
                new[] { sourceRow }, new StickerPrintPreferences());
            productionViewModel.SelectedElement = productionViewModel.ActiveTemplate.Elements.First(element =>
                element.FieldKey == "SizeText");
            Assert.False(productionViewModel.CanEditSampleDimensions);
            productionViewModel.SampleWidthMmText = "999";
            Assert.Equal(740, sourceRow.WidthMm);
            Assert.Equal(740, productionViewModel.SelectedLabel!.WidthMm);
            Assert.Equal(string.Empty, productionViewModel.SampleWidthMmText);

            var suppliedSample = new GlassStickerLabel { WidthMm = 760, HeightMm = 480, Quantity = 1 };
            var isolatedSampleViewModel = new StickerPrintDialogViewModel(
                Array.Empty<GlassStickerLabel>(), new StickerPrintPreferences(), suppliedSample);
            isolatedSampleViewModel.SelectedElement = isolatedSampleViewModel.ActiveTemplate.Elements.First(element =>
                element.FieldKey == "SizeText");
            isolatedSampleViewModel.SampleWidthMmText = "900";
            Assert.Equal(760, suppliedSample.WidthMm);
            Assert.NotSame(suppliedSample, isolatedSampleViewModel.SelectedLabel);
        }

        [Fact]
        public void PreviewNavigationFollowsTheSelectedRealSourceRowAcrossTemplateChanges()
        {
            var first = new GlassStickerLabel
            {
                OrderNumber = "PI-REAL-001",
                CustomerCompanyName = "REAL CUSTOMER",
                GlassName = "6 mm clear",
                GlassType = "SGU",
                WidthMm = 700,
                HeightMm = 450,
                Quantity = 1
            };
            var second = new GlassStickerLabel
            {
                OrderNumber = "PI-REAL-002",
                CustomerCompanyName = "REAL CUSTOMER",
                GlassName = "6 mm low-e / spacer / 6 mm clear",
                GlassType = "DGU",
                WidthMm = 900,
                HeightMm = 600,
                Quantity = 2
            };
            var viewModel = new StickerPrintDialogViewModel(
                new[] { first, second }, new StickerPrintPreferences());

            Assert.False(viewModel.IsSamplePreview);
            Assert.Same(first, viewModel.SelectedLabel);
            Assert.True(viewModel.CanNavigateSourceRows);
            Assert.False(viewModel.CanSelectPreviousSourceRow);
            Assert.True(viewModel.CanSelectNextSourceRow);

            viewModel.SelectNextSourceRow();
            Assert.Same(second, viewModel.SelectedLabel);
            Assert.Equal("Source row 2 of 2", viewModel.SourceRowPositionText);
            Assert.False(viewModel.CanSelectNextSourceRow);

            viewModel.SelectTemplateChoice(viewModel.TemplateChoices.First(choice =>
                choice.Template.Kind == StickerTemplateKind.Dgu));
            var selectedRealRow = viewModel.SelectedLabel!;
            Assert.Same(second, selectedRealRow);
            Assert.Equal("PI-REAL-002", StickerLabelVisualFactory.ResolveFieldValue(
                selectedRealRow, "OrderNumber"));
            Assert.Equal("DGU", StickerLabelVisualFactory.ResolveFieldValue(
                selectedRealRow, "GlassType"));
            Assert.Equal("900", StickerLabelVisualFactory.ResolveFieldValue(
                selectedRealRow, "WidthMm"));
            Assert.Equal("600", StickerLabelVisualFactory.ResolveFieldValue(
                selectedRealRow, "HeightMm"));
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

        [Fact]
        public void EditorPreviewOmitsMissingDataInsteadOfShowingLiteralFieldTokens()
        {
            var template = StickerTemplatePresets.CreateSgu();
            var customerElement = template.Elements.First(element =>
                element.FieldKey == "CustomerCompanyName");
            var row = new GlassStickerLabel
            {
                GlassType = "SGU",
                GlassName = "6 mm clear",
                WidthMm = 700,
                HeightMm = 450,
                Quantity = 1
            };

            var canvas = StickerLabelVisualFactory.CreateTemplateCanvas(
                row, template, editorMode: true);

            Assert.DoesNotContain(canvas.Children.OfType<Border>(), border =>
                ReferenceEquals(border.Tag, customerElement));
            Assert.DoesNotContain(canvas.Children.OfType<Border>(), border =>
                border.Child is TextBlock text && text.Text == "[CustomerCompanyName]");
        }

        [Fact]
        public void OnlySyntheticSampleRowsShowTheSampleLogoPlaceholder()
        {
            var template = StickerTemplatePresets.CreateDgu();
            var sample = StickerTemplateSampleFactory.Create(StickerTemplateKind.Dgu);
            var sampleCanvas = StickerLabelVisualFactory.CreateTemplateCanvas(sample, template);
            Assert.Contains(sampleCanvas.Children.OfType<Border>(), border =>
                border.Child is TextBlock text && text.Text == "SAMPLE LOGO");

            var realRow = new GlassStickerLabel
            {
                Source = "Proforma Invoice Step 5",
                WidthMm = 700,
                HeightMm = 450,
                Quantity = 1
            };
            var productionCanvas = StickerLabelVisualFactory.CreateTemplateCanvas(
                realRow, template, editorMode: true);
            Assert.DoesNotContain(productionCanvas.Children.OfType<Border>(), border =>
                border.Child is TextBlock text && text.Text == "SAMPLE LOGO");
        }
    }
}
