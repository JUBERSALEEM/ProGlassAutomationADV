using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Renders the bottom-of-page footer.
    ///
    /// Left  — DocumentId · Printed timestamp
    /// Right — Page X of Y
    ///
    /// Phase 9.5a-r4: Now accepts documentId parameter so continuation
    /// pages self-identify. This matters when a multi-page invoice gets
    /// physically separated — every page still shows which document it
    /// belongs to.
    ///
    /// DocumentId segment is skipped if null/empty so callers who don't
    /// have an ID still get a clean footer identical to r3 behavior.
    /// </summary>
    public static class FooterRenderer
    {
        public static void Render(IContainer container, string? documentId = null)
        {
            container
                .PaddingTop(6)
                .BorderTop(1)
                .BorderColor(DocumentStyle.ColorBorder)
                .Row(row =>
                {
                    // Left cell — DocumentId + printed timestamp
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

                        text.Span($"Printed {DateTime.Now:dd MMM yyyy HH:mm}");
                    });

                    // Right cell — Page X of Y
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