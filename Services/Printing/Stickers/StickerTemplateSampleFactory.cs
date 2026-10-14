namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>
    /// Creates conspicuously synthetic, template-specific preview data. These
    /// rows are for layout review and standalone test prints only; they are never
    /// mixed into Step 5 or Optimization source collections.
    /// </summary>
    public static class StickerTemplateSampleFactory
    {
        public static GlassStickerLabel Create(StickerTemplateKind kind)
        {
            var company = new StickerCompanyProfile
            {
                CompanyName = "SAMPLE COMPANY",
                Address = "SAMPLE ADDRESS — TEST ONLY",
                Telephone = "SAMPLE COMPANY TELEPHONE",
                RepresentativeMobile = "SAMPLE REPRESENTATIVE MOBILE",
                Website = "sample.example.invalid",
                Email = "sample@example.invalid"
            };

            return kind switch
            {
                StickerTemplateKind.Sgu => CreateSgu(company),
                StickerTemplateKind.Dgu => CreateDgu(company),
                StickerTemplateKind.ProformaInvoice => CreateProformaJob(company),
                _ => CreateCustom(company)
            };
        }

        private static GlassStickerLabel CreateSgu(StickerCompanyProfile company) => CreateBase(company,
            orderNumber: "SAMPLE-ORDER-001",
            jobOrderNumber: string.Empty,
            proformaNumber: string.Empty,
            glassName: "SAMPLE — 6 mm clear float glass",
            glassType: "SGU",
            panePosition: "Single pane — sample");

        private static GlassStickerLabel CreateDgu(StickerCompanyProfile company) => CreateBase(company,
            orderNumber: "SAMPLE-ORDER-001",
            jobOrderNumber: string.Empty,
            proformaNumber: string.Empty,
            glassName: "SAMPLE — 6 mm clear / 12 mm spacer / 6 mm clear",
            glassType: "DGU",
            panePosition: "Outer pane — sample");

        private static GlassStickerLabel CreateProformaJob(StickerCompanyProfile company) => CreateBase(company,
            orderNumber: "SAMPLE-ORDER-001",
            jobOrderNumber: "SAMPLE-JOB-001",
            proformaNumber: "SAMPLE-PI-001",
            glassName: "SAMPLE — 6 mm clear DGU build",
            glassType: "DGU",
            panePosition: "Inner pane — sample");

        private static GlassStickerLabel CreateCustom(StickerCompanyProfile company) => CreateBase(company,
            orderNumber: "SAMPLE-ORDER-001",
            jobOrderNumber: "SAMPLE-JOB-001",
            proformaNumber: "SAMPLE-PI-001",
            glassName: "SAMPLE — custom glass layout",
            glassType: "SAMPLE",
            panePosition: "Sample pane");

        private static GlassStickerLabel CreateBase(
            StickerCompanyProfile company,
            string orderNumber,
            string jobOrderNumber,
            string proformaNumber,
            string glassName,
            string glassType,
            string panePosition) => new()
        {
            Company = company,
            OrderNumber = orderNumber,
            JobOrderNumber = jobOrderNumber,
            ProformaNumber = proformaNumber,
            CustomerCompanyName = "SAMPLE CUSTOMER — TEST ONLY",
            GlassName = glassName,
            GlassType = glassType,
            GlassReference = "SAMPLE-GLASS-001",
            WidthMm = 700,
            HeightMm = 450,
            Quantity = 1,
            PrintedQuantity = 1,
            Reference = "SAMPLE-REF-001",
            PanePosition = panePosition,
            Source = "DESIGN SAMPLE — NOT PRODUCTION DATA"
        };
    }
}
