using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Renders a section title bar.
    /// Accent color background strip with white text.
    /// Modules call this before each major content section for
    /// visual consistency.
    /// </summary>
    public static class SectionHeaderRenderer
    {
        public static void Render(IContainer container, string title)
        {
            container
                .Background(DocumentStyle.ColorAccent)
                .PaddingHorizontal(8)
                .PaddingVertical(4)
                .Text(title)
                .FontSize(DocumentStyle.FontSizeH2)
                .SemiBold()
                .FontColor(DocumentStyle.ColorWhite);
        }
    }
}