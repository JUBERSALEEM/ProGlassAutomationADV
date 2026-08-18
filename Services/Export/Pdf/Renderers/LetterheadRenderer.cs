using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Professional page-1 letterhead. Accent rail, company block, document badge.
    /// </summary>
    public static class LetterheadRenderer
    {
        public static void Render(IContainer container, LetterheadInfo info)
        {
            container.Column(col =>
            {
                col.Item().Height(4).Background(DocumentStyle.ColorAccent);

                col.Item().PaddingTop(10).PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(info.CompanyName ?? "")
                            .FontSize(15).Bold()
                            .FontColor(DocumentStyle.ColorAccent);

                        if (!string.IsNullOrWhiteSpace(info.CompanyLocation))
                            left.Item().PaddingTop(2).Text(info.CompanyLocation)
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);

                        if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                            left.Item().PaddingTop(1)
                                .Text($"TRN  {info.CompanyTrn}")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .SemiBold()
                                .FontColor(DocumentStyle.ColorText);
                    });

                    row.ConstantItem(200).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight()
                            .Background(DocumentStyle.ColorAccent)
                            .PaddingHorizontal(10).PaddingVertical(4)
                            .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                            .FontSize(8).Bold()
                            .FontColor(DocumentStyle.ColorAccentOn);

                        right.Item().AlignRight().PaddingTop(6)
                            .Text(info.DocumentId ?? "")
                            .FontSize(16).Bold()
                            .FontColor(DocumentStyle.ColorAccent);

                        if (!string.IsNullOrWhiteSpace(info.SubtitleLine))
                            right.Item().AlignRight().PaddingTop(2)
                                .Text(info.SubtitleLine)
                                .FontSize(DocumentStyle.FontSizeCaption)
                                .FontColor(DocumentStyle.ColorMuted);
                    });
                });

                col.Item().Height(1).Background(DocumentStyle.ColorAccent);
            });
        }
    }
}
