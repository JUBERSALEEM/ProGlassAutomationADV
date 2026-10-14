using System;
using ProGlassAutomation.Services.Printing.Stickers;

namespace ProGlassAutomation.ViewModels.Printing.Stickers
{
    /// <summary>
    /// Main-screen, design-only instance of the sticker studio. Print dialogs
    /// still use StickerPrintDialogViewModel with their real source rows.
    /// </summary>
    public sealed class StickerPrinterViewModel : StickerPrintDialogViewModel
    {
        public StickerPrinterViewModel()
            : base(Array.Empty<GlassStickerLabel>(),
                StickerPrintPreferencesStore.Load(),
                CreateDesignerPreviewLabel())
        {
        }

        internal static GlassStickerLabel CreateDesignerPreviewLabel() => new()
        {
            Company = new StickerCompanyProfile
            {
                CompanyName = "Your company name",
                Address = "Company address",
                Telephone = "Company telephone",
                RepresentativeMobile = "Representative mobile",
                Website = "company website",
                Email = "company email"
            },
            OrderNumber = "SAMPLE ORDER",
            JobOrderNumber = "SAMPLE JOB",
            ProformaNumber = "SAMPLE PI",
            CustomerCompanyName = "Customer company",
            GlassName = "6MM CLEAR — PREVIEW",
            GlassType = "DGU",
            GlassReference = "Glass reference",
            WidthMm = 730,
            HeightMm = 555,
            Quantity = 1,
            PrintedQuantity = 1,
            Reference = "REF",
            PanePosition = "Outer",
            Source = "Design-only sample"
        };
    }
}
