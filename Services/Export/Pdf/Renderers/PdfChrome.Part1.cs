// ═══════════════════════════════════════════════════════════════════════
// FILE: PdfChrome.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.MasterData;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
        public static partial class PdfChrome
    {
            private static PdfTheme T => DocumentStyle.Current;
        private static PdfLayoutKind K => T.Kind;
        private static int _sectionIndex;

        public static void Reset() => _sectionIndex = 0;

        // ═════════════════════════════════════════════════════════════
        //  COMPANY LOGO — loaded from the saved company record and placed
        //  per the chosen layout (None / LeftPortrait / TopPanel). Loading is
        //  fully defensive: a missing file or corrupt image must never crash
        //  a money document; the letterhead simply falls back to text.
        // ═════════════════════════════════════════════════════════════

        private static byte[]? LoadLogoBytes(LetterheadInfo info)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(info.LogoBase64))
                {
                    byte[] b64 = Convert.FromBase64String(info.LogoBase64);
                    if (b64.Length > 0) return b64;
                }
                if (!string.IsNullOrWhiteSpace(info.LogoPath)
                    && System.IO.File.Exists(info.LogoPath))
                    return System.IO.File.ReadAllBytes(info.LogoPath);
            }
            catch
            {
                // invalid logo must never crash the document
            }
            return null;
        }

        private static bool HasLogo(LetterheadInfo info) =>
            info.LogoLayout != CompanyLogoLayout.None
            && LoadLogoBytes(info) != null;

        private static float LogoW(LetterheadInfo info) =>
            Math.Clamp((float)(info.LogoWidthMm > 0 ? info.LogoWidthMm : 22), 8f, 60f);

        private static float LogoH(LetterheadInfo info) =>
            Math.Clamp((float)(info.LogoHeightMm > 0 ? info.LogoHeightMm : 26), 8f, 60f);

        /// <summary>
        /// Renders the sized logo box on a white chip (so a transparent PNG
        /// stays legible on the navy / steel mastheads). No-op without a logo.
        ///
        /// Two layouts:
        ///   LeftPortrait — portrait box at the configured W × H.
        ///   TopPanel     — the box is capped to masthead height (≤14 mm) so
        ///                  it reads as a top-panel lockup rather than a tall
        ///                  portrait.
        /// </summary>
        private static void LogoBox(IContainer container, LetterheadInfo info)
        {
            byte[]? bytes = LoadLogoBytes(info);
            if (bytes == null) return;
            float h = info.LogoLayout == CompanyLogoLayout.TopPanel
                ? Math.Clamp(LogoH(info), 8f, 14f)
                : LogoH(info);
            container
                .Background(T.White)
                .Border(0.5f)
                .BorderColor(T.Border)
                .Padding(3)
                .Width(LogoW(info), Unit.Millimetre)
                .Height(h, Unit.Millimetre)
                .Image(bytes)
                .FitArea();
        }

        // ═════════════════════════════════════════════════════════════
        //  LETTERHEAD — five page-1 compositions
        // ═════════════════════════════════════════════════════════════

        public static void Letterhead(IContainer container, LetterheadInfo info)
        {
            Reset();

            // TopPanel (2026-09-07): the logo lives in a FULL-WIDTH masthead
            // band across the top of the page, with the company identity and
            // document reference beside it. The regular composition follows
            // below with its inline logo suppressed so the logo never
            // renders twice. LeftPortrait / None render exactly as before
            // (logo inline at the left of the company identity).
            if (info.LogoLayout == CompanyLogoLayout.TopPanel && HasLogo(info))
            {
                container.Column(col =>
                {
                    col.Item().Element(c => TopPanelMasthead(c, info));
                    col.Item().PaddingTop(8)
                        .Element(c => RenderHeader(c, info, suppressLogo: true));
                });
                return;
            }

            RenderHeader(container, info, suppressLogo: false);
        }

        private static void RenderHeader(
            IContainer container, LetterheadInfo info, bool suppressLogo)
        {
            switch (K)
            {
                case PdfLayoutKind.SplitConsole:
                    LetterheadSplit(container, info, suppressLogo);
                    break;
                case PdfLayoutKind.Editorial:
                    LetterheadEditorial(container, info, suppressLogo);
                    break;
                case PdfLayoutKind.Workshop:
                    LetterheadWorkshop(container, info, suppressLogo);
                    break;
                case PdfLayoutKind.QuietForm:
                    LetterheadQuiet(container, info, suppressLogo);
                    break;
                default:
                    LetterheadFormal(container, info, suppressLogo);
                    break;
            }
        }

        /// <summary>
        /// Full-width industrial masthead for the TopPanel logo layout: the
        /// logo sits in a framed white chip on the left, the company identity
        /// beside it, and the document title + reference on the right — all
        /// on one accent band that spans the whole page width.
        /// </summary>
        private static void TopPanelMasthead(IContainer container, LetterheadInfo info)
        {
            byte[]? bytes = LoadLogoBytes(info);
            if (bytes == null) return;

            container.Background(T.Accent)
                .PaddingHorizontal(12).PaddingVertical(8)
                .Row(row =>
                {
                    row.AutoItem()
                        .AlignMiddle()
                        .Background(T.White)
                        .Border(0.5f)
                        .BorderColor(T.Border)
                        .Padding(2)
                        .Width(LogoW(info), Unit.Millimetre)
                        .Height(Math.Clamp(LogoH(info), 8f, 14f), Unit.Millimetre)
                        .Image(bytes)
                        .FitArea();

                    row.RelativeItem().PaddingLeft(10).Column(left =>
                    {
                        left.Item().Text(info.CompanyName ?? "")
                            .FontSize(T.TitleSize).Bold().FontColor(T.AccentOn);

                        var sub = info.CompanyLocation ?? "";
                        if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                            sub = string.IsNullOrWhiteSpace(sub)
                                ? $"TRN {info.CompanyTrn}"
                                : $"{sub}  ·  TRN {info.CompanyTrn}";
                        if (!string.IsNullOrWhiteSpace(sub))
                            left.Item().PaddingTop(2).Text(sub)
                                .FontSize(8).FontColor(T.AccentSoft);
                    });

                    row.ConstantItem(200).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight()
                            .Text((info.DocumentTitle ?? "").ToUpperInvariant())
                            .FontSize(8).Bold().FontColor(T.AccentOn);
                        right.Item().AlignRight().PaddingTop(2)
                            .Text(info.DocumentId ?? "")
                            .FontSize(T.IdSize).Bold().FontColor(T.Highlight);
                    });
                });
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

        private static void LetterheadFormal(IContainer container, LetterheadInfo info, bool suppressLogo)
        {
            container.Column(col =>
            {
                col.Item().Background(T.Accent)
                    .PaddingHorizontal(12).PaddingVertical(10)
                    .Row(row =>
                    {
                        if (!suppressLogo && HasLogo(info))
                            row.AutoItem().PaddingRight(12)
                                .AlignMiddle()
                                .Element(c => LogoBox(c, info));

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

        private static void LetterheadSplit(IContainer container, LetterheadInfo info, bool suppressLogo)
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
                            right.Item().Row(r =>
                            {
                                if (!suppressLogo && HasLogo(info))
                                    r.AutoItem().PaddingRight(8)
                                        .AlignMiddle()
                                        .Element(c => LogoBox(c, info));
                                r.RelativeItem().Column(txt =>
                                {
                                    txt.Item().Text(info.CompanyName ?? "")
                                        .FontSize(11).Bold().FontColor(T.Accent);
                                    if (!string.IsNullOrWhiteSpace(info.CompanyLocation))
                                        txt.Item().PaddingTop(2)
                                            .Text(info.CompanyLocation)
                                            .FontSize(8).FontColor(T.Muted);
                                    if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                                        txt.Item().PaddingTop(4)
                                            .Background(T.Accent)
                                            .PaddingHorizontal(6).PaddingVertical(2)
                                            .Text($"TRN  {info.CompanyTrn}")
                                            .FontSize(7).Bold().FontColor(T.AccentOn);
                                });
                            });
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

        private static void LetterheadEditorial(IContainer container, LetterheadInfo info, bool suppressLogo)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    if (!suppressLogo && HasLogo(info))
                        row.AutoItem().PaddingRight(12)
                            .AlignMiddle()
                            .Element(c => LogoBox(c, info));

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

        private static void LetterheadWorkshop(IContainer container, LetterheadInfo info, bool suppressLogo)
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
                            if (!suppressLogo && HasLogo(info))
                                inner.AutoItem().PaddingRight(10)
                                    .AlignMiddle()
                                    .Element(c => LogoBox(c, info));

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

        private static void LetterheadQuiet(IContainer container, LetterheadInfo info, bool suppressLogo)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    if (!suppressLogo && HasLogo(info))
                        row.AutoItem().PaddingRight(8)
                            .AlignMiddle()
                            .Element(c => LogoBox(c, info));

                    row.RelativeItem().Column(col2 =>
                    {
                        col2.Item().Text(info.CompanyName ?? "")
                            .FontSize(11).FontColor(T.Text);
                        if (!string.IsNullOrWhiteSpace(info.CompanyLocation)
                            || !string.IsNullOrWhiteSpace(info.CompanyTrn))
                        {
                            var line = info.CompanyLocation ?? "";
                            if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                                line = string.IsNullOrWhiteSpace(line)
                                    ? $"TRN {info.CompanyTrn}"
                                    : $"{line}  ·  TRN {info.CompanyTrn}";
                            col2.Item().PaddingTop(1).Text(line)
                                .FontSize(8).FontColor(T.Muted);
                        }
                    });
                });

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
            bool center = true, bool right = false,
            string? background = null)
        {
            IContainer painted;
            if (!string.IsNullOrWhiteSpace(background))
            {
                painted = cell.Background(background)
                    .Border(T.TableGrid ? 0.6f : 0)
                    .BorderColor(T.AccentDark);
            }
            else
            {
                painted = K switch
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
            }

            painted = painted
                .PaddingHorizontal(T.CellPadH)
                .PaddingVertical(T.CellPadV);

            if (right) painted = painted.AlignRight();
            else if (center) painted = painted.AlignCenter();

            var color = !string.IsNullOrWhiteSpace(background)
                ? T.AccentOn
                : K is PdfLayoutKind.Editorial
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
            string? color = null,
            string? background = null)
        {
            var bg = !string.IsNullOrWhiteSpace(background)
                ? background
                : (T.Zebra && alt) ? T.TableAlt : T.White;
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
    }
}
