using System.Collections.Generic;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    /// <summary>Built-in starting points; each can be freely edited and saved as a custom file.</summary>
    public static class StickerTemplatePresets
    {
        public static IReadOnlyList<StickerLayoutTemplate> All => new[]
        {
            CreateSgu(),
            CreateDgu(),
            CreateProformaInvoice()
        };

        public static StickerLayoutTemplate CreateSgu()
        {
            var template = NewTemplate("SGU — Single Glass", StickerTemplateKind.Sgu, 100, 80);
            AddLogo(template, 2, 2, 16, 11);
            AddText(template, "Company name", "CompanyName", 20, 2, 78, 7, 10, bold: true);
            AddText(template, "Company address", "CompanyAddress", 2, 14, 96, 5, 6.5);
            AddLine(template, "Header divider", 2, 20, 96, 0.5);
            AddText(template, "Order", "OrderDisplay", 2, 22, 96, 7, 10, bold: true, prefix: "ORDER: ");
            AddText(template, "Customer", "CustomerCompanyName", 2, 30, 96, 6, 8.5, bold: true, prefix: "CUSTOMER: ");
            AddText(template, "Glass / colour", "GlassName", 2, 38, 96, 10, 13, bold: true);
            AddText(template, "Size", "SizeText", 2, 49, 96, 13, 18, bold: true, alignment: StickerTextAlignment.Center);
            AddText(template, "Quantity", "Quantity", 2, 63, 26, 6, 8, bold: true, prefix: "QTY: ");
            AddText(template, "Reference", "Reference", 31, 63, 67, 6, 7.5, prefix: "REF: ");
            AddText(template, "Telephone", "CompanyTelephone", 2, 69, 44, 4.5, 6);
            AddText(template, "Website", "CompanyWebsite", 48, 69, 50, 4.5, 6);
            AddText(template, "Email", "CompanyEmail", 2, 74, 96, 4.5, 5.5);
            return template;
        }

        public static StickerLayoutTemplate CreateDgu()
        {
            var template = NewTemplate("DGU — Insulated Glass", StickerTemplateKind.Dgu, 100, 88);
            AddLogo(template, 2, 2, 15, 11);
            AddText(template, "Company name", "CompanyName", 20, 2, 78, 7, 10, bold: true);
            AddText(template, "Company address", "CompanyAddress", 2, 14, 96, 5, 6.5);
            AddLine(template, "Header divider", 2, 20, 96, 0.5);
            AddText(template, "Order", "OrderDisplay", 2, 22, 96, 7, 10, bold: true, prefix: "ORDER: ");
            AddText(template, "Customer", "CustomerCompanyName", 2, 30, 96, 6, 8.5, bold: true, prefix: "CUSTOMER: ");
            AddText(template, "Glass build", "GlassType", 2, 37, 28, 6, 7.5, bold: true, prefix: "BUILD: ");
            AddText(template, "Pane side", "PanePosition", 32, 37, 66, 6, 7.5, prefix: "SIDE: ");
            AddText(template, "Glass / colour", "GlassName", 2, 44, 96, 9, 11.5, bold: true);
            AddText(template, "Glass reference", "GlassReference", 2, 53, 96, 6, 7, prefix: "GLASS REF: ");
            AddText(template, "Size", "SizeText", 2, 59, 96, 12, 17, bold: true, alignment: StickerTextAlignment.Center);
            AddText(template, "Quantity", "Quantity", 2, 72, 26, 6, 8, bold: true, prefix: "QTY: ");
            AddText(template, "Reference", "Reference", 31, 72, 67, 6, 7.5, prefix: "REF: ");
            AddText(template, "Telephone", "CompanyTelephone", 2, 79, 44, 4, 5.5);
            AddText(template, "Website", "CompanyWebsite", 48, 79, 50, 4, 5.5);
            AddText(template, "Email", "CompanyEmail", 2, 83.5, 96, 4, 5);
            return template;
        }

        public static StickerLayoutTemplate CreateProformaInvoice()
        {
            var template = NewTemplate("Proforma / Job", StickerTemplateKind.ProformaInvoice, 100, 90);
            AddLogo(template, 2, 2, 17, 12);
            AddText(template, "Company name", "CompanyName", 21, 2, 77, 8, 10.5, bold: true);
            AddText(template, "Company address", "CompanyAddress", 2, 15, 96, 5, 6.5);
            AddText(template, "Document type", string.Empty, 2, 21, 96, 5, 7, bold: true,
                staticText: "PROFORMA / JOB PRODUCTION LABEL");
            AddLine(template, "Header divider", 2, 27, 96, 0.5);
            AddText(template, "PI / Job number", "OrderDisplay", 2, 29, 96, 8, 11, bold: true, prefix: "PI / JOB: ");
            AddText(template, "Customer", "CustomerCompanyName", 2, 38, 96, 7, 9, bold: true, prefix: "CUSTOMER: ");
            AddText(template, "Reference", "Reference", 2, 46, 96, 6, 7.5, prefix: "REFERENCE: ");
            AddText(template, "Glass build", "GlassType", 2, 53, 34, 6, 7.5, bold: true, prefix: "TYPE: ");
            AddText(template, "Pane side", "PanePosition", 38, 53, 60, 6, 7.5, prefix: "SIDE: ");
            AddText(template, "Glass / colour", "GlassName", 2, 60, 96, 9, 11, bold: true);
            AddText(template, "Size", "SizeText", 2, 69, 66, 12, 17, bold: true);
            AddText(template, "Quantity", "Quantity", 70, 69, 28, 12, 9, bold: true, prefix: "QTY: ");
            AddText(template, "Telephone", "CompanyTelephone", 2, 81, 45, 4, 5.5);
            AddText(template, "Website", "CompanyWebsite", 48, 81, 50, 4, 5.5);
            AddText(template, "Email", "CompanyEmail", 2, 85, 96, 4.5, 5.5);
            return template;
        }

        public static StickerLayoutTemplate CreateCustom(string name = "Custom sticker",
            double widthMm = 100, double heightMm = 75)
        {
            return NewTemplate(name, StickerTemplateKind.Custom, widthMm, heightMm);
        }

        private static StickerLayoutTemplate NewTemplate(string name, StickerTemplateKind kind,
            double widthMm, double heightMm)
        {
            return new StickerLayoutTemplate
            {
                Name = name,
                Kind = kind,
                LabelWidthMm = widthMm,
                LabelHeightMm = heightMm,
                MarginMm = 2,
                DefaultFontSizePt = 8,
                Background = "#FFFFFFFF"
            };
        }

        private static void AddLogo(StickerLayoutTemplate template,
            double x, double y, double width, double height)
        {
            template.Elements.Add(new StickerLayoutElement
            {
                Name = "Company logo",
                Kind = StickerTemplateElementKind.Logo,
                FieldKey = "CompanyLogo",
                Xmm = x,
                Ymm = y,
                WidthMm = width,
                HeightMm = height,
                IsLocked = false
            });
        }

        private static void AddLine(StickerLayoutTemplate template, string name,
            double x, double y, double width, double height)
        {
            template.Elements.Add(new StickerLayoutElement
            {
                Name = name,
                Kind = StickerTemplateElementKind.Line,
                Xmm = x,
                Ymm = y,
                WidthMm = width,
                HeightMm = height,
                Background = "#FF344054",
                BorderColor = "#FF344054",
                BorderWidthMm = 0
            });
        }

        private static void AddText(StickerLayoutTemplate template, string name, string fieldKey,
            double x, double y, double width, double height, double fontSize,
            bool bold = false, StickerTextAlignment alignment = StickerTextAlignment.Left,
            string prefix = "", string staticText = "")
        {
            template.Elements.Add(new StickerLayoutElement
            {
                Name = name,
                Kind = StickerTemplateElementKind.Text,
                FieldKey = fieldKey,
                Text = staticText,
                Prefix = prefix,
                Xmm = x,
                Ymm = y,
                WidthMm = width,
                HeightMm = height,
                FontSizePt = fontSize,
                FontFamily = "Segoe UI",
                Bold = bold,
                PaddingMm = 0.4,
                TextAlignment = alignment,
                ContentAlignment = StickerContentAlignment.Center,
                Foreground = "#FF101828",
                Background = "#00FFFFFF",
                BorderColor = "#00000000",
                HideWhenEmpty = !string.IsNullOrWhiteSpace(fieldKey)
            });
        }
    }
}
