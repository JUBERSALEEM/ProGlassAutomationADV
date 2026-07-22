using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Renders the bottom-of-page footer with:
    /// Left  — printed-at timestamp
    /// Right — Page X of Y
    /// </summary>
    public static class FooterRenderer
    {
        public static void Render(IContainer container)
        {
            container
                .PaddingTop(6)
                .BorderTop(1)
                .BorderColor(DocumentStyle.ColorBorder)
                .Row(row =>
                {
                    row.RelativeItem().Text($"Printed {DateTime.Now:dd MMM yyyy HH:mm}")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);

                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(x => x
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
        }
    }
}