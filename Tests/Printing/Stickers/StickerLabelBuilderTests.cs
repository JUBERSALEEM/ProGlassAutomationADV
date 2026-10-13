using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels.Printing.Stickers;
using Xunit;

namespace ProGlassAutomation.Tests.Printing.Stickers
{
    public sealed class StickerLabelBuilderTests
    {
        [Fact]
        public void Step5LabelsKeepInvoiceBrandCustomerReferenceAndMillimetreSize()
        {
            var invoice = CreateInvoice("PI-7629/2", "AL NEEFA");
            invoice.Project.ProjectReference = "REF-17";
            invoice.SpecGroups.Add(new SpecGroupSnapshot
            {
                GroupNumber = 1,
                SelectedGlassType = "SGU",
                SelectedSpecName = "6mm SNLX D GREY CHROMA",
                OuterGlassColor = "SNLX D GREY CHROMA",
                ItemRows =
                {
                    new GlassItemRowSnapshot
                    {
                        SerialNumber = 4,
                        GlassRef = "PANE-REF-4",
                        W1 = 730,
                        H1 = 555,
                        Qty = 1
                    }
                }
            });

            var labels = GlassStickerLabelBuilder.FromStep5(invoice.SpecGroups, invoice);

            var label = Assert.Single(labels);
            Assert.Equal("PI-7629/2", label.OrderNumber);
            Assert.Equal("AL NEEFA", label.CustomerCompanyName);
            Assert.Equal("REF-17", label.Reference);
            Assert.Equal(730, label.WidthMm);
            Assert.Equal(555, label.HeightMm);
            Assert.Equal(1, label.Quantity);
            Assert.Equal("SGU", label.GlassType);
            Assert.Equal("Ocean Glass Industry LLC", label.Company.CompanyName);
            Assert.Equal("+971 6 555 0100", label.Company.Telephone);
            Assert.Equal("+971 50 111 2222", label.Company.RepresentativeMobile);
            Assert.Equal("https://example.test", label.Company.Website);
            Assert.Equal("Ocean Glass Industry LLC", label.Company.LogoPath);
            Assert.Equal("bG9nby1zbmFwc2hvdA==", label.Company.LogoBase64);
            Assert.Contains("SNLX D GREY CHROMA", label.GlassName.ToUpperInvariant());
            Assert.Equal("PANE-REF-4", label.GlassReference);
        }

        [Fact]
        public void Step5PaneExpansionMatchesTheProductionEngineForDgu()
        {
            var invoice = CreateInvoice("PI-100", "Customer");
            invoice.SpecGroups.Add(new SpecGroupSnapshot
            {
                GroupNumber = 2,
                SelectedGlassType = "DGU",
                SelectedSpecName = "6mm grey / 6mm clear",
                OuterGlassColor = "Grey",
                InnerGlassColor = "Clear",
                ItemRows =
                {
                    new GlassItemRowSnapshot
                    {
                        SerialNumber = 1,
                        GlassRef = "ROW-1",
                        W1 = 800,
                        H1 = 600,
                        Qty = 2
                    }
                }
            });

            var labels = GlassStickerLabelBuilder.FromStep5(invoice.SpecGroups, invoice);

            Assert.Equal(2, labels.Count);
            Assert.All(labels, label => Assert.Equal("DGU", label.GlassType));
            Assert.Contains(labels, label => label.PanePosition == "Outer"
                && label.WidthMm == 800 && label.HeightMm == 600 && label.Quantity == 2);
            Assert.Contains(labels, label => label.PanePosition == "Inner"
                && label.WidthMm == 800 && label.HeightMm == 600 && label.Quantity == 2);
        }

        [Fact]
        public void OptimizationSourceRowsAreFilteredAndPreserveJobAndGlassIdentity()
        {
            var labels = GlassStickerLabelBuilder.FromOptimization(new[]
            {
                new OptimizationStickerSourceRow
                {
                    OrderNumber = "JOB-20",
                    JobOrderNumber = "JOB-20",
                    ProformaNumber = "PI-20",
                    CustomerCompanyName = "AL NEEFA",
                    GlassName = "6mm SNLX D GREY CHROMA",
                    GlassType = "DGU",
                    WidthMm = 730,
                    HeightMm = 555,
                    Quantity = 3,
                    Reference = "LPO-42",
                    PanePosition = "Outer"
                },
                new OptimizationStickerSourceRow { WidthMm = 0, HeightMm = 100, Quantity = 1 }
            });

            var label = Assert.Single(labels);
            Assert.Equal("JOB-20 / PI-20", label.OrderDisplay);
            Assert.Equal("AL NEEFA", label.CustomerCompanyName);
            Assert.Equal("DGU", label.GlassType);
            Assert.Equal("730 × 555 mm", label.SizeText);
            Assert.Equal(3, label.Quantity);
            Assert.Equal("Outer", label.PanePosition);
        }

        [Fact]
        public void GlassNameCompositionDoesNotDuplicateSourceThicknessOrColor()
        {
            string result = GlassStickerLabelBuilder.ComposeGlassName(
                "6mm SNLX D GREY CHROMA", "SNLX D GREY CHROMA", 6);

            Assert.Equal("6mm SNLX D GREY CHROMA", result);
        }

        [Fact]
        public void CompanyTelephoneAndWebsiteAreAdditiveSnapshotFields()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    CompanyName = "Ocean Glass Industry LLC",
                    CompanyTelephone = "+971 6 555 0100",
                    CompanyWebsite = "https://example.test"
                }
            });
            var restored = System.Text.Json.JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(json);
            Assert.Equal("+971 6 555 0100", restored!.Company.CompanyTelephone);
            Assert.Equal("https://example.test", restored.Company.CompanyWebsite);

            var legacy = System.Text.Json.JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                "{\"Company\":{\"CompanyName\":\"Legacy Co\"}}");
            Assert.Equal(string.Empty, legacy!.Company.CompanyTelephone);
            Assert.Equal(string.Empty, legacy.Company.CompanyWebsite);
        }

        [Fact]
        public void OneLabelPerPieceModeCountsEveryPaneAndCanSwitchToAggregateRows()
        {
            var rows = new[]
            {
                new GlassStickerLabel { WidthMm = 730, HeightMm = 555, Quantity = 4 },
                new GlassStickerLabel { WidthMm = 800, HeightMm = 600, Quantity = 2 }
            };
            var viewModel = new StickerPrintDialogViewModel(rows, new StickerPrintPreferences());

            Assert.Equal(6, viewModel.EstimatedLabelCount);
            viewModel.OneLabelPerPiece = false;
            Assert.Equal(2, viewModel.EstimatedLabelCount);
            Assert.True(viewModel.TryCreatePreferences(out var settings, out _));
            Assert.False(settings.OneLabelPerPiece);
        }

        private static ProformaInvoiceSnapshot CreateInvoice(string proformaId, string customerName)
        {
            return new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    CompanyName = "Ocean Glass Industry LLC",
                    CompanyLocation = "Ajman Industrial Area Jurf-3",
                    CompanyTelephone = "+971 6 555 0100",
                    RepresentativeMobile = "+971 50 111 2222",
                    CompanyWebsite = "https://example.test",
                    CompanyEmail = "info@example.test",
                    LogoPath = "Ocean Glass Industry LLC",
                    LogoBase64 = "bG9nby1zbmFwc2hvdA==",
                    ProformaId = proformaId
                },
                Client = new ClientSnapshot { CustomerName = customerName },
                Project = new ProjectSnapshot()
            };
        }
    }
}
