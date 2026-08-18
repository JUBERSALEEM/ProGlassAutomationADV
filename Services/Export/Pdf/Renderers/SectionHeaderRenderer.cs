using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Quiet section title: accent rail + small-caps label + hairline.
    /// </summary>
    public static class SectionHeaderRenderer
    {
        public static void Render(IContainer container, string title)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(3).Background(DocumentStyle.ColorAccent);
                    row.RelativeItem()
                        .Background(DocumentStyle.ColorSurface)
                        .PaddingHorizontal(8).PaddingVertical(4)
                        .Text((title ?? "").ToUpperInvariant())
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .Bold()
                        .FontColor(DocumentStyle.ColorAccent);
                });
                col.Item().Height(0.6f).Background(DocumentStyle.ColorBorder);
            });
        }
    }
}
