using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Renders the top-of-page company header block.
    /// Left side: Company name + location + TRN.
    /// Right side: Document type + document ID (large, accent color).
    /// </summary>
    public static class LetterheadRenderer
    {
        public static void Render(IContainer container, LetterheadInfo info)
        {
            container
                .PaddingBottom(8)
                .BorderBottom(1)
                .BorderColor(DocumentStyle.ColorBorder)
                .Row(row =>
                {
                    // Left — company block
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(info.CompanyName)
                            .FontSize(DocumentStyle.FontSizeH1)
                            .Bold()
                            .FontColor(DocumentStyle.ColorAccent);

                        if (!string.IsNullOrWhiteSpace(info.CompanyLocation))
                            col.Item().Text(info.CompanyLocation)
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);

                        if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                            col.Item().Text($"TRN: {info.CompanyTrn}")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                    });

                    // Right — document identity
                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text(info.DocumentTitle)
                            .FontSize(DocumentStyle.FontSizeH2)
                            .SemiBold()
                            .FontColor(DocumentStyle.ColorMuted);

                        col.Item().AlignRight().Text(info.DocumentId)
                            .FontSize(DocumentStyle.FontSizeTitle)
                            .Bold()
                            .FontColor(DocumentStyle.ColorAccent);

                        if (!string.IsNullOrWhiteSpace(info.SubtitleLine))
                            col.Item().AlignRight().Text(info.SubtitleLine)
                                .FontSize(DocumentStyle.FontSizeCaption)
                                .FontColor(DocumentStyle.ColorMuted);
                    });
                });
        }
    }
}