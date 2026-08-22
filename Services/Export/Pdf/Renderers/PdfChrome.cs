using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
    /// <summary>
    /// Layout-aware print chrome. Every document calls these helpers
    /// instead of painting its own bars — switching Settings → PDF Layout
    /// rebuilds letterhead, sections, tables, cards, totals and
    /// signatures as five different page architectures.
    /// </summary>
    public static class PdfChrome
    {
        private static PdfTheme T => DocumentStyle.Current;
        private static PdfLayoutKind K => T.Kind;
        private static int _sectionIndex;

        public static void Reset() => _sectionIndex = 0;

        // ═════════════════════════════════════════════════════════════
        //  LETTERHEAD — five page-1 compositions
        // ═════════════════════════════════════════════════════════════

        public static void Letterhead(IContainer container, LetterheadInfo info)
        {
            Reset();
            switch (K)
            {
                case PdfLayoutKind.SplitConsole:
                    LetterheadSplit(container, info);
                    break;
                case PdfLayoutKind.Editorial:
                    LetterheadEditorial(container, info);
                    break;
                case PdfLayoutKind.Workshop:
                    LetterheadWorkshop(container, info);
                    break;
                case PdfLayoutKind.QuietForm:
                    LetterheadQuiet(container, info);
                    break;
                default:
                    LetterheadFormal(container, info);
                    break;
            }
        }

        public static void Continuation(IContainer container, LetterheadInfo info)
        {
            switch (K)
            {
                case PdfLayoutKind.Workshop:
                    container.Background(T.AccentDark)
                        .PaddingHorizontal(8).PaddingVertical(4)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(info.CompanyName ?? "")
                                .FontSize(8).SemiBold().FontColor(T.AccentOn);
                            row.RelativeItem().AlignCenter()
                                .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                                .FontSize(8).FontColor(T.AccentSoft);
                            row.RelativeItem().AlignRight()
                                .Text(info.DocumentId ?? "")
                                .FontSize(8).Bold().FontColor(T.Highlight);
                        });
                    break;

                case PdfLayoutKind.QuietForm:
                    container.BorderBottom(0.4f).BorderColor(T.Border)
                        .PaddingBottom(4)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(info.CompanyName ?? "")
                                .FontSize(8).FontColor(T.Muted);
                            row.RelativeItem().AlignRight()
                                .Text($"{info.DocumentTitle}  {info.DocumentId}")
                                .FontSize(8).FontColor(T.Text);
                        });
                    break;

                case PdfLayoutKind.Editorial:
                    container.BorderBottom(0.6f).BorderColor(T.Highlight)
                        .PaddingBottom(4)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                                .FontSize(8).FontColor(T.Muted);
                            row.RelativeItem().AlignRight()
                                .Text(info.DocumentId ?? "")
                                .FontSize(9).SemiBold().FontColor(T.Text);
                        });
                    break;

                default:
                    container.Column(col =>
                    {
                        col.Item().Height(2).Background(T.Accent);
                        col.Item().PaddingTop(4).PaddingBottom(2).Row(row =>
                        {
                            row.RelativeItem().Text(info.CompanyName ?? "")
                                .FontSize(8).SemiBold().FontColor(T.Accent);
                            row.RelativeItem().AlignRight()
                                .Text($"{info.DocumentTitle}  ·  {info.DocumentId}")
                                .FontSize(8).FontColor(T.Muted);
                        });
                    });
                    break;
            }
        }

        private static void LetterheadFormal(IContainer container, LetterheadInfo info)
        {
            container.Column(col =>
            {
                col.Item().Background(T.Accent)
                    .PaddingHorizontal(12).PaddingVertical(10)
                    .Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(info.CompanyName ?? "")
                                .FontSize(T.TitleSize).Bold().FontColor(T.AccentOn);
                            if (!string.IsNullOrWhiteSpace(info.CompanyLocation))
                                left.Item().PaddingTop(2)
                                    .Text(info.CompanyLocation)
                                    .FontSize(8).FontColor(T.AccentSoft);
                            if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                                left.Item().PaddingTop(1)
                                    .Text($"TRN  {info.CompanyTrn}")
                                    .FontSize(8).SemiBold().FontColor(T.AccentOn);
                        });

                        row.ConstantItem(210).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight()
                                .Background(T.Highlight)
                                .PaddingHorizontal(8).PaddingVertical(3)
                                .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                                .FontSize(8).Bold().FontColor(T.AccentDark);
                            right.Item().AlignRight().PaddingTop(5)
                                .Text(info.DocumentId ?? "")
                                .FontSize(T.IdSize).Bold().FontColor(T.AccentOn);
                            if (!string.IsNullOrWhiteSpace(info.SubtitleLine))
                                right.Item().AlignRight().PaddingTop(2)
                                    .Text(info.SubtitleLine)
                                    .FontSize(7).FontColor(T.AccentSoft);
                        });
                    });

                if (info.Meta.Count > 0)
                    col.Item().Element(c => MetaStrip(c, info.Meta));
            });
        }

        private static void LetterheadSplit(IContainer container, LetterheadInfo info)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem(3).Column(left =>
                    {
                        left.Item().Text((info.DocumentTitle ?? "").ToUpperInvariant())
                            .FontSize(T.TitleSize).Bold().FontColor(T.Accent);
                        left.Item().PaddingTop(4).Text(info.DocumentId ?? "")
                            .FontSize(T.IdSize).SemiBold().FontColor(T.Text);
                        if (!string.IsNullOrWhiteSpace(info.SubtitleLine))
                            left.Item().PaddingTop(2).Text(info.SubtitleLine)
                                .FontSize(8).FontColor(T.Muted);
                    });

                    row.ConstantItem(16);

                    row.RelativeItem(2)
                        .Background(T.Surface)
                        .Padding(10)
                        .Column(right =>
                        {
                            right.Item().Text(info.CompanyName ?? "")
                                .FontSize(11).Bold().FontColor(T.Accent);
                            if (!string.IsNullOrWhiteSpace(info.CompanyLocation))
                                right.Item().PaddingTop(2)
                                    .Text(info.CompanyLocation)
                                    .FontSize(8).FontColor(T.Muted);
                            if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                                right.Item().PaddingTop(4)
                                    .Background(T.Accent)
                                    .PaddingHorizontal(6).PaddingVertical(2)
                                    .Text($"TRN  {info.CompanyTrn}")
                                    .FontSize(7).Bold().FontColor(T.AccentOn);
                            foreach (var (label, value) in info.Meta.Take(3))
                            {
                                if (string.IsNullOrWhiteSpace(value)) continue;
                                right.Item().PaddingTop(3).Row(r =>
                                {
                                    r.ConstantItem(70).Text(label.ToUpperInvariant())
                                        .FontSize(7).FontColor(T.Muted);
                                    r.RelativeItem().Text(value)
                                        .FontSize(8).SemiBold().FontColor(T.Text);
                                });
                            }
                        });
                });

                col.Item().PaddingTop(8).Height(1.5f).Background(T.Accent);
            });
        }

        private static void LetterheadEditorial(IContainer container, LetterheadInfo info)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(info.DocumentTitle ?? "")
                        .FontSize(T.TitleSize).FontColor(T.Text);
                    row.ConstantItem(160).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(info.CompanyName ?? "")
                            .FontSize(8).SemiBold().FontColor(T.Muted);
                        if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                            right.Item().AlignRight().PaddingTop(1)
                                .Text($"TRN {info.CompanyTrn}")
                                .FontSize(7).FontColor(T.Muted);
                    });
                });

                col.Item().PaddingTop(4).Height(2).Background(T.Highlight);

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("DOCUMENT")
                            .FontSize(7).FontColor(T.Muted);
                        c.Item().Text(info.DocumentId ?? "—")
                            .FontSize(11).SemiBold().FontColor(T.Text);
                    });

                    foreach (var (label, value) in info.Meta.Take(3))
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(label.ToUpperInvariant())
                                .FontSize(7).FontColor(T.Muted);
                            c.Item().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                                .FontSize(10).SemiBold().FontColor(T.Text);
                        });
                    }
                });

                if (!string.IsNullOrWhiteSpace(info.CompanyLocation))
                    col.Item().PaddingTop(6).Text(info.CompanyLocation)
                        .FontSize(8).FontColor(T.Muted);

                col.Item().PaddingTop(8).Height(0.4f).Background(T.Border);
            });
        }

        private static void LetterheadWorkshop(IContainer container, LetterheadInfo info)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(6).Background(T.Highlight);
                    row.RelativeItem().Background(T.AccentDark)
                        .PaddingHorizontal(10).PaddingVertical(10)
                        .Row(inner =>
                        {
                            inner.RelativeItem().Column(left =>
                            {
                                left.Item().Text(info.CompanyName ?? "")
                                    .FontSize(12).Bold().FontColor(T.AccentOn);
                                var sub = info.CompanyLocation ?? "";
                                if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                                    sub = string.IsNullOrWhiteSpace(sub)
                                        ? $"TRN {info.CompanyTrn}"
                                        : $"{sub}  ·  TRN {info.CompanyTrn}";
                                if (!string.IsNullOrWhiteSpace(sub))
                                    left.Item().PaddingTop(2).Text(sub)
                                        .FontSize(8).FontColor(T.AccentSoft);
                            });

                            inner.ConstantItem(200).AlignRight().Column(right =>
                            {
                                right.Item().AlignRight()
                                    .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                                    .FontSize(T.TitleSize).Bold().FontColor(T.Highlight);
                                right.Item().AlignRight().PaddingTop(2)
                                    .Text(info.DocumentId ?? "")
                                    .FontSize(T.IdSize).Bold().FontColor(T.AccentOn);
                            });
                        });
                });

                var kpi = new List<(string, string)>();
                if (!string.IsNullOrWhiteSpace(info.SubtitleLine))
                    kpi.Add(("REF", info.SubtitleLine));
                foreach (var m in info.Meta.Take(4))
                    if (!string.IsNullOrWhiteSpace(m.Value))
                        kpi.Add((m.Label.ToUpperInvariant(), m.Value));

                if (kpi.Count > 0)
                {
                    col.Item().Background(T.Surface)
                        .Border(0.6f).BorderColor(T.Border)
                        .PaddingHorizontal(8).PaddingVertical(6)
                        .Row(row =>
                        {
                            foreach (var (label, value) in kpi)
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(label)
                                        .FontSize(7).Bold().FontColor(T.Muted);
                                    c.Item().Text(value)
                                        .FontSize(10).Bold().FontColor(T.Text);
                                });
                            }
                        });
                }
            });
        }

        private static void LetterheadQuiet(IContainer container, LetterheadInfo info)
        {
            container.Column(col =>
            {
                col.Item().Text(info.CompanyName ?? "")
                    .FontSize(11).FontColor(T.Text);
                if (!string.IsNullOrWhiteSpace(info.CompanyLocation)
                    || !string.IsNullOrWhiteSpace(info.CompanyTrn))
                {
                    var line = info.CompanyLocation ?? "";
                    if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                        line = string.IsNullOrWhiteSpace(line)
                            ? $"TRN {info.CompanyTrn}"
                            : $"{line}  ·  TRN {info.CompanyTrn}";
                    col.Item().PaddingTop(1).Text(line)
                        .FontSize(8).FontColor(T.Muted);
                }

                col.Item().PaddingTop(8).Height(0.4f).Background(T.Text);

                col.Item().PaddingTop(10)
                    .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                    .FontSize(T.TitleSize).FontColor(T.Muted);

                col.Item().PaddingTop(2).Text(info.DocumentId ?? "")
                    .FontSize(T.IdSize).FontColor(T.Text);

                if (info.Meta.Count > 0)
                {
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        foreach (var (label, value) in info.Meta.Take(4))
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(label.ToUpperInvariant())
                                    .FontSize(7).FontColor(T.Muted);
                                c.Item().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                                    .FontSize(9).FontColor(T.Text);
                            });
                        }
                    });
                }

                col.Item().PaddingTop(10).Height(0.4f).Background(T.Border);
            });
        }

        // ═════════════════════════════════════════════════════════════
        //  FOOTER — five compositions
        // ═════════════════════════════════════════════════════════════

        public static void Footer(IContainer container, string? documentId)
        {
            switch (K)
            {
                case PdfLayoutKind.Workshop:
                    container.Background(T.AccentDark)
                        .PaddingHorizontal(8).PaddingVertical(5)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(7).FontColor(T.AccentSoft));
                                if (!string.IsNullOrWhiteSpace(documentId))
                                {
                                    text.Span(documentId).SemiBold().FontColor(T.AccentOn);
                                    text.Span("   ");
                                }
                                text.Span($"Printed {DateTime.Now:dd MMM yyyy  HH:mm}");
                            });
                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(7).FontColor(T.AccentSoft));
                                text.Span("PAGE ");
                                text.CurrentPageNumber().Bold().FontColor(T.AccentOn);
                                text.Span(" / ");
                                text.TotalPages().Bold().FontColor(T.AccentOn);
                            });
                        });
                    break;

                case PdfLayoutKind.Editorial:
                    container.Column(col =>
                    {
                        col.Item().Height(0.5f).Background(T.Highlight);
                        col.Item().PaddingTop(6).AlignCenter().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(7).FontColor(T.Muted));
                            if (!string.IsNullOrWhiteSpace(documentId))
                            {
                                text.Span(documentId.ToUpperInvariant());
                                text.Span("   ·   ");
                            }
                            text.Span("PAGE ");
                            text.CurrentPageNumber();
                            text.Span(" OF ");
                            text.TotalPages();
                        });
                    });
                    break;

                case PdfLayoutKind.QuietForm:
                    container.AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(7).FontColor(T.Muted));
                        text.CurrentPageNumber();
                        text.Span("  /  ");
                        text.TotalPages();
                    });
                    break;

                case PdfLayoutKind.SplitConsole:
                    container.Column(col =>
                    {
                        col.Item().Height(0.6f).Background(T.Border);
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text($"Printed {DateTime.Now:dd MMM yyyy}")
                                .FontSize(7).FontColor(T.Muted);
                            row.RelativeItem().AlignCenter().Text(documentId ?? "")
                                .FontSize(7).SemiBold().FontColor(T.Text);
                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(7).FontColor(T.Muted));
                                text.CurrentPageNumber().SemiBold().FontColor(T.Text);
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                    break;

                default:
                    container.Column(col =>
                    {
                        col.Item().Height(1.5f).Background(T.Accent);
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(x => x
                                    .FontSize(DocumentStyle.FontSizeCaption)
                                    .FontColor(T.Muted));
                                if (!string.IsNullOrWhiteSpace(documentId))
                                {
                                    text.Span(documentId).SemiBold().FontColor(T.Text);
                                    text.Span("  ·  ");
                                }
                                text.Span($"Printed {DateTime.Now:dd MMM yyyy  HH:mm}");
                            });
                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(x => x
                                    .FontSize(DocumentStyle.FontSizeCaption)
                                    .FontColor(T.Muted));
                                text.Span("Page ");
                                text.CurrentPageNumber().SemiBold().FontColor(T.Text);
                                text.Span("  /  ");
                                text.TotalPages().SemiBold().FontColor(T.Text);
                            });
                        });
                    });
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  SECTION / GROUP / HAIRLINE
        // ═════════════════════════════════════════════════════════════

        public static void Section(IContainer container, string title)
        {
            _sectionIndex++;
            var label = title ?? "";

            switch (K)
            {
                case PdfLayoutKind.SplitConsole:
                    container.Row(row =>
                    {
                        row.ConstantItem(8).Height(8).Background(T.Accent);
                        row.RelativeItem().PaddingLeft(6)
                            .Text(label)
                            .FontSize(10).SemiBold().FontColor(T.Accent);
                    });
                    break;

                case PdfLayoutKind.Editorial:
                    container.Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.AutoItem().Text($"{_sectionIndex:00}")
                                .FontSize(9).FontColor(T.Highlight);
                            row.RelativeItem().PaddingLeft(8)
                                .Text(label.ToUpperInvariant())
                                .FontSize(8).FontColor(T.Text);
                        });
                        col.Item().PaddingTop(3).Height(0.5f).Background(T.Highlight);
                    });
                    break;

                case PdfLayoutKind.Workshop:
                    container.Row(row =>
                    {
                        row.ConstantItem(4).Background(T.Highlight);
                        row.RelativeItem().Background(T.Accent)
                            .PaddingHorizontal(8).PaddingVertical(4)
                            .Text(label.ToUpperInvariant())
                            .FontSize(8).Bold().FontColor(T.AccentOn);
                    });
                    break;

                case PdfLayoutKind.QuietForm:
                    container.Column(col =>
                    {
                        col.Item().Text(label.ToUpperInvariant())
                            .FontSize(8).FontColor(T.Muted);
                        col.Item().PaddingTop(2).Height(0.4f).Background(T.Text);
                    });
                    break;

                default:
                    container.Background(T.Accent)
                        .PaddingHorizontal(8).PaddingVertical(4)
                        .Text(label.ToUpperInvariant())
                        .FontSize(8).Bold().FontColor(T.AccentOn);
                    break;
            }
        }

        public static void GroupBanner(IContainer container, string text, string? trailing = null)
        {
            switch (K)
            {
                case PdfLayoutKind.Editorial:
                case PdfLayoutKind.QuietForm:
                    container.PaddingBottom(3).Row(row =>
                    {
                        row.RelativeItem().Text(text ?? "")
                            .FontSize(9).SemiBold().FontColor(T.Text);
                        if (!string.IsNullOrWhiteSpace(trailing))
                            row.AutoItem().Text(trailing)
                                .FontSize(8).FontColor(T.Muted);
                    });
                    break;

                case PdfLayoutKind.SplitConsole:
                    container.Background(T.Surface)
                        .PaddingHorizontal(8).PaddingVertical(4)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(text ?? "")
                                .FontSize(9).SemiBold().FontColor(T.Accent);
                            if (!string.IsNullOrWhiteSpace(trailing))
                                row.AutoItem().Text(trailing)
                                    .FontSize(8).FontColor(T.Muted);
                        });
                    break;

                case PdfLayoutKind.Workshop:
                    container.Row(row =>
                    {
                        row.ConstantItem(3).Background(T.Highlight);
                        row.RelativeItem().Background(T.AccentLight)
                            .PaddingHorizontal(6).PaddingVertical(3)
                            .Row(inner =>
                            {
                                inner.RelativeItem().Text(text ?? "")
                                    .FontSize(8).Bold().FontColor(T.AccentDark);
                                if (!string.IsNullOrWhiteSpace(trailing))
                                    inner.AutoItem().Text(trailing)
                                        .FontSize(7).FontColor(T.Muted);
                            });
                    });
                    break;

                default:
                    container.Background(T.Accent)
                        .PaddingHorizontal(8).PaddingVertical(4)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(text ?? "")
                                .FontSize(9).Bold().FontColor(T.AccentOn);
                            if (!string.IsNullOrWhiteSpace(trailing))
                                row.AutoItem().Text(trailing)
                                    .FontSize(8).FontColor(T.AccentSoft);
                        });
                    break;
            }
        }

        public static void Hairline(IContainer container)
        {
            float h = K switch
            {
                PdfLayoutKind.Workshop => 0.8f,
                PdfLayoutKind.QuietForm => 0.4f,
                PdfLayoutKind.Editorial => 0.5f,
                _ => 0.75f
            };
            string color = K switch
            {
                PdfLayoutKind.Editorial => T.Highlight,
                PdfLayoutKind.Workshop => T.Accent,
                _ => T.Border
            };
            container.Height(h).Background(color);
        }

        // ═════════════════════════════════════════════════════════════
        //  INFO CARDS / META / LABEL-VALUE
        // ═════════════════════════════════════════════════════════════

        public static void InfoCard(
            IContainer container,
            string title,
            Action<ColumnDescriptor> body)
        {
            switch (K)
            {
                case PdfLayoutKind.SplitConsole:
                    container.Background(T.Surface).Padding(10).Column(col =>
                    {
                        col.Item().Text(title)
                            .FontSize(8).SemiBold().FontColor(T.Accent);
                        col.Item().PaddingTop(4);
                        body(col);
                    });
                    break;

                case PdfLayoutKind.Editorial:
                case PdfLayoutKind.QuietForm:
                    container.Column(col =>
                    {
                        col.Item().Text(title.ToUpperInvariant())
                            .FontSize(7).FontColor(T.Muted);
                        col.Item().PaddingTop(4);
                        body(col);
                    });
                    break;

                case PdfLayoutKind.Workshop:
                    container.Border(0.7f).BorderColor(T.Border).Column(col =>
                    {
                        col.Item().Background(T.AccentLight)
                            .PaddingHorizontal(6).PaddingVertical(3)
                            .Text(title.ToUpperInvariant())
                            .FontSize(7).Bold().FontColor(T.AccentDark);
                        col.Item().Padding(8);
                        body(col);
                    });
                    break;

                default:
                    container.Border(0.6f).BorderColor(T.Border).Padding(8).Column(col =>
                    {
                        col.Item().Text(title.ToUpperInvariant())
                            .FontSize(8).Bold().FontColor(T.Accent);
                        col.Item().PaddingTop(4);
                        body(col);
                    });
                    break;
            }
        }

        public static void MetaStrip(
            IContainer container,
            IReadOnlyList<(string Label, string Value)> items)
        {
            var shown = (items ?? Array.Empty<(string, string)>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Value))
                .Take(5)
                .ToList();
            if (shown.Count == 0)
            {
                container.Height(0.01f).Background(T.White);
                return;
            }

            IContainer box = K switch
            {
                PdfLayoutKind.Workshop => container.Background(T.Surface)
                    .BorderBottom(0.6f).BorderColor(T.Border),
                PdfLayoutKind.SplitConsole => container.Background(T.Surface),
                PdfLayoutKind.FormalBand => container.Background(T.Surface)
                    .BorderBottom(0.6f).BorderColor(T.Border),
                _ => container
            };

            box.PaddingHorizontal(10).PaddingVertical(6).Row(row =>
            {
                foreach (var (label, value) in shown)
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(label.ToUpperInvariant())
                            .FontSize(7).FontColor(T.Muted);
                        c.Item().PaddingTop(1).Text(value)
                            .FontSize(9).SemiBold().FontColor(T.Text);
                    });
                }
            });
        }

        public static void LabelValue(ColumnDescriptor col, string label, string? value)
        {
            col.Item().PaddingTop(3).Row(r =>
            {
                r.AutoItem()
                    .Text($"{label}")
                    .FontSize(8).FontColor(T.Muted);
                r.RelativeItem().PaddingLeft(6)
                    .Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(8).SemiBold().FontColor(T.Text);
            });
        }

        // ═════════════════════════════════════════════════════════════
        //  TABLE CELLS
        // ═════════════════════════════════════════════════════════════

        public static void HeaderCell(
            IContainer cell, string text,
            bool center = true, bool right = false)
        {
            IContainer painted = K switch
            {
                PdfLayoutKind.SplitConsole =>
                    cell.Background(T.AccentLight)
                        .BorderBottom(1).BorderColor(T.Accent),
                PdfLayoutKind.Editorial =>
                    cell.BorderBottom(1.2f).BorderColor(T.Text)
                        .BorderTop(0.4f).BorderColor(T.Text),
                PdfLayoutKind.QuietForm =>
                    cell.BorderBottom(0.6f).BorderColor(T.Text),
                PdfLayoutKind.Workshop =>
                    cell.Background(T.TableHeader)
                        .Border(0.6f).BorderColor(T.AccentDark),
                _ =>
                    cell.Background(T.TableHeader)
            };

            painted = painted
                .PaddingHorizontal(T.CellPadH)
                .PaddingVertical(T.CellPadV);

            if (right) painted = painted.AlignRight();
            else if (center) painted = painted.AlignCenter();

            var color = K is PdfLayoutKind.Editorial
                or PdfLayoutKind.QuietForm
                or PdfLayoutKind.SplitConsole
                ? T.Text
                : T.AccentOn;

            painted.Text(text ?? "")
                .FontSize(Math.Max(7f, T.TableFont - 0.5f))
                .Bold()
                .FontColor(color);
        }

        public static void DataCell(
            IContainer cell,
            string text,
            bool alt = false,
            bool center = false,
            bool right = false,
            bool bold = false,
            string? color = null)
        {
            var bg = (T.Zebra && alt) ? T.TableAlt : T.White;
            IContainer painted = cell.Background(bg);

            if (T.TableGrid)
                painted = painted.Border(0.55f).BorderColor(T.Border);
            else
                painted = painted.BorderBottom(0.45f).BorderColor(T.Border);

            painted = painted
                .PaddingHorizontal(T.CellPadH)
                .PaddingVertical(T.CellPadV);

            if (right) painted = painted.AlignRight();
            else if (center) painted = painted.AlignCenter();

            var txt = painted.Text(text ?? "").FontSize(T.TableFont);
            if (bold) txt.Bold();
            txt.FontColor(color ?? T.Text);
        }

        // ═════════════════════════════════════════════════════════════
        //  TOTALS
        // ═════════════════════════════════════════════════════════════

        public static void GrandTotal(IContainer container, string label, string amount)
        {
            switch (K)
            {
                case PdfLayoutKind.SplitConsole:
                    container.Background(T.Surface)
                        .BorderLeft(3).BorderColor(T.Accent)
                        .PaddingHorizontal(12).PaddingVertical(8)
                        .Row(r =>
                        {
                            r.RelativeItem().Text(label)
                                .FontSize(10).SemiBold().FontColor(T.Accent);
                            r.AutoItem().Text(amount)
                                .FontSize(13).Bold().FontColor(T.Accent);
                        });
                    break;

                case PdfLayoutKind.Editorial:
                    container.Column(col =>
                    {
                        col.Item().Height(0.6f).Background(T.Text);
                        col.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text(label.ToUpperInvariant())
                                .FontSize(8).FontColor(T.Muted);
                            r.AutoItem().Text(amount)
                                .FontSize(16).FontColor(T.Text);
                        });
                    });
                    break;

                case PdfLayoutKind.QuietForm:
                    container.BorderTop(1).BorderColor(T.Text)
                        .PaddingTop(6)
                        .Row(r =>
                        {
                            r.RelativeItem().Text(label)
                                .FontSize(9).FontColor(T.Text);
                            r.AutoItem().Text(amount)
                                .FontSize(12).FontColor(T.Text);
                        });
                    break;

                case PdfLayoutKind.Workshop:
                    container.Background(T.AccentDark)
                        .PaddingHorizontal(10).PaddingVertical(8)
                        .Row(r =>
                        {
                            r.RelativeItem().Text(label.ToUpperInvariant())
                                .FontSize(9).Bold().FontColor(T.Highlight);
                            r.AutoItem().Text(amount)
                                .FontSize(13).Bold().FontColor(T.AccentOn);
                        });
                    break;

                default:
                    container.Background(T.Accent)
                        .PaddingHorizontal(12).PaddingVertical(8)
                        .Row(r =>
                        {
                            r.RelativeItem().Text(label)
                                .FontSize(10).Bold().FontColor(T.AccentOn);
                            r.AutoItem().Text(amount)
                                .FontSize(13).Bold().FontColor(T.AccentOn);
                        });
                    break;
            }
        }

        public static void SubtotalBar(IContainer container, string left, string right)
        {
            switch (K)
            {
                case PdfLayoutKind.Editorial:
                case PdfLayoutKind.QuietForm:
                    container.PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text(left)
                            .FontSize(8).FontColor(T.Muted);
                        r.AutoItem().Text(right)
                            .FontSize(9).SemiBold().FontColor(T.Text);
                    });
                    break;

                case PdfLayoutKind.SplitConsole:
                    container.Background(T.Surface)
                        .PaddingHorizontal(8).PaddingVertical(4)
                        .Row(r =>
                        {
                            r.RelativeItem().Text(left)
                                .FontSize(8).FontColor(T.Accent);
                            r.AutoItem().Text(right)
                                .FontSize(9).Bold().FontColor(T.Accent);
                        });
                    break;

                default:
                    container.Background(T.Surface)
                        .BorderBottom(K == PdfLayoutKind.Workshop ? 0.6f : 1)
                        .BorderColor(T.Border)
                        .PaddingHorizontal(8).PaddingVertical(5)
                        .Row(r =>
                        {
                            r.RelativeItem().Text(left)
                                .FontSize(8).Bold().FontColor(T.Accent);
                            r.AutoItem().Text(right)
                                .FontSize(9).Bold().FontColor(T.Success);
                        });
                    break;
            }
        }

        public static void KpiStrip(
            IContainer container,
            params (string Label, string Value, string? Color)[] items)
        {
            IContainer box = K switch
            {
                PdfLayoutKind.Workshop => container.Background(T.AccentDark),
                PdfLayoutKind.FormalBand => container.Background(T.TableHeader),
                PdfLayoutKind.SplitConsole => container.Background(T.Surface),
                _ => container.BorderTop(0.6f).BorderColor(T.Border)
            };

            var labelColor = K is PdfLayoutKind.Workshop or PdfLayoutKind.FormalBand
                ? T.AccentSoft : T.Muted;
            var valueFallback = K is PdfLayoutKind.Workshop or PdfLayoutKind.FormalBand
                ? T.AccentOn : T.Text;

            box.Padding(8).Row(row =>
            {
                foreach (var (label, value, color) in items)
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(label.ToUpperInvariant())
                            .FontSize(7).FontColor(labelColor);
                        c.Item().Text(value)
                            .FontSize(12).Bold()
                            .FontColor(color ?? valueFallback);
                    });
                }
            });
        }

        // ═════════════════════════════════════════════════════════════
        //  SIGNATURES
        // ═════════════════════════════════════════════════════════════

        public static void Signatures(IContainer container, params SignatureSlot[] slots)
        {
            if (slots == null || slots.Length == 0) return;

            container.Row(row =>
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (i > 0) row.ConstantItem(10);
                    var slot = slots[i];
                    row.RelativeItem().Element(c => RenderSignatureSlot(c, slot));
                }
            });
        }

        private static void RenderSignatureSlot(IContainer container, SignatureSlot slot)
        {
            switch (K)
            {
                case PdfLayoutKind.SplitConsole:
                case PdfLayoutKind.Editorial:
                case PdfLayoutKind.QuietForm:
                    container.Column(col =>
                    {
                        col.Item().Height(K == PdfLayoutKind.Editorial ? 36 : 28)
                            .BorderBottom(0.6f).BorderColor(T.Text);
                        col.Item().PaddingTop(4).Text(slot.Title)
                            .FontSize(8).FontColor(T.Muted);
                        if (slot.Body != null)
                            slot.Body(col);
                        else
                        {
                            col.Item().PaddingTop(2).Text("Name  ____________________")
                                .FontSize(7).FontColor(T.Muted);
                            col.Item().Text("Date  ____________________")
                                .FontSize(7).FontColor(T.Muted);
                        }
                    });
                    break;

                case PdfLayoutKind.Workshop:
                    container.Border(0.8f).BorderColor(T.Accent).Padding(8).Column(col =>
                    {
                        col.Item().Text(slot.Title.ToUpperInvariant())
                            .FontSize(7).Bold().FontColor(T.Highlight);
                        col.Item().PaddingTop(18)
                            .BorderBottom(0.8f).BorderColor(T.Text)
                            .Text(" ").FontSize(8);
                        if (slot.Body != null)
                            slot.Body(col);
                        else
                        {
                            col.Item().PaddingTop(4).Text("NAME / STAMP")
                                .FontSize(7).FontColor(T.Muted);
                            col.Item().Text("DATE _______________")
                                .FontSize(7).FontColor(T.Muted);
                        }
                    });
                    break;

                default:
                    container.Border(0.6f).BorderColor(T.Border).Padding(10).Column(col =>
                    {
                        col.Item().Text(slot.Title.ToUpperInvariant())
                            .FontSize(8).Bold().FontColor(T.Accent);
                        if (slot.Body != null)
                            slot.Body(col);
                        else
                        {
                            col.Item().PaddingTop(22)
                                .BorderBottom(0.6f).BorderColor(T.Border)
                                .Text(" ").FontSize(8);
                            col.Item().PaddingTop(3).Text("Name / Date")
                                .FontSize(7).FontColor(T.Muted);
                        }
                    });
                    break;
            }
        }
    }

    public sealed class SignatureSlot
    {
        public string Title { get; init; } = "";
        public Action<ColumnDescriptor>? Body { get; init; }
    }
}
