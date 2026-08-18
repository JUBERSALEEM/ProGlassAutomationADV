using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    public static class FooterRenderer
    {
        public static void Render(IContainer container, string? documentId = null)
        {
            container.Column(col =>
            {
                col.Item().Height(1.5f).Background(DocumentStyle.ColorAccent);

                col.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(x => x
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted));

                        if (!string.IsNullOrWhiteSpace(documentId))
                        {
                            text.Span(documentId)
                                .SemiBold()
                                .FontColor(DocumentStyle.ColorText);
                            text.Span("  ·  ");
                        }

                        text.Span($"Printed {DateTime.Now:dd MMM yyyy  HH:mm}");
                    });

                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(x => x
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted));
                        text.Span("Page ");
                        text.CurrentPageNumber().SemiBold().FontColor(DocumentStyle.ColorText);
                        text.Span("  /  ");
                        text.TotalPages().SemiBold().FontColor(DocumentStyle.ColorText);
                    });
                });
            });
        }
    }
}
