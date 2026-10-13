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
            viewModel.DeleteSelectedElement();
            Assert.Single(viewModel.TemplateElements);
            Assert.Same(original, viewModel.SelectedElement);
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
            Assert.Contains("WIDTH AND HEIGHT", dimensionError.ToUpperInvariant());
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
