// ═══════════════════════════════════════════════════════════════════════
// FILE: PdfChrome.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Export.Pdf.Renderers
{
        public static partial class PdfChrome
    {

        // ═════════════════════════════════════════════════════════════
        //  INDUSTRIAL ERP DOCUMENT CONTROL PANEL
        // ═════════════════════════════════════════════════════════════

        public static void IndustrialDocumentPanel(
            IContainer container,
            string title,
            string subtitle,
            params (string Label, string Value, string? Color)[] metrics)
        {
            var shown = (metrics ?? Array.Empty<(string, string, string?)>())
                .Where(m => !string.IsNullOrWhiteSpace(m.Label)
                    && !string.IsNullOrWhiteSpace(m.Value))
                .Take(5)
                .ToList();

            IContainer box = K switch
            {
                PdfLayoutKind.Workshop => container.BorderLeft(4).BorderColor(T.Highlight)
                    .Background(T.AccentDark),
                PdfLayoutKind.SplitConsole => container.BorderLeft(3).BorderColor(T.Accent)
                    .Background(T.Surface),
                PdfLayoutKind.Editorial => container.BorderTop(1.2f).BorderBottom(0.6f)
                    .BorderColor(T.Highlight),
                PdfLayoutKind.QuietForm => container.BorderTop(0.8f).BorderBottom(0.4f)
                    .BorderColor(T.Text),
                _ => container.Border(0.6f).BorderColor(T.Border)
                    .Background(T.Surface)
            };

            bool dark = K == PdfLayoutKind.Workshop;
            string titleColor = dark ? T.AccentOn : T.Accent;
            string subtitleColor = dark ? T.AccentSoft : T.Muted;
            string valueFallback = dark ? T.AccentOn : T.Text;

            box.PaddingHorizontal(10).PaddingVertical(8).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text((title ?? "").ToUpperInvariant())
                            .FontSize(9).Bold().FontColor(titleColor);
                        if (!string.IsNullOrWhiteSpace(subtitle))
                            left.Item().PaddingTop(2).Text(subtitle)
                                .FontSize(7.5f).FontColor(subtitleColor);
                    });

                    row.AutoItem().AlignMiddle()
                        .Background(dark ? T.Highlight : T.Accent)
                        .PaddingHorizontal(7).PaddingVertical(3)
                        .Text("ERP CONTROLLED")
                        .FontSize(7).Bold()
                        .FontColor(dark ? T.AccentDark : T.AccentOn);
                });

                if (shown.Count > 0)
                {
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        foreach (var metric in shown)
                        {
                            row.RelativeItem().Element(c =>
                                IndustrialMetric(c, metric.Label, metric.Value,
                                    metric.Color ?? valueFallback, dark));
                        }
                    });
                }
            });
        }

        private static void IndustrialMetric(
            IContainer container,
            string label,
            string value,
            string valueColor,
            bool dark)
        {
            container.BorderLeft(1.4f)
                .BorderColor(dark ? T.Highlight : T.Accent)
                .PaddingLeft(6)
                .Column(col =>
                {
                    col.Item().Text(label.ToUpperInvariant())
                        .FontSize(6.5f)
                        .FontColor(dark ? T.AccentSoft : T.Muted);
                    col.Item().PaddingTop(1).Text(value)
                        .FontSize(10).Bold()
                        .FontColor(valueColor);
                });
        }

        public static void IndustrialAssuranceNote(
            IContainer container,
            string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            container.BorderLeft(2).BorderColor(T.Accent)
                .Background(T.Surface)
                .PaddingHorizontal(8).PaddingVertical(5)
                .Text(text)
                .FontSize(7.5f)
                .Italic()
                .FontColor(T.Muted);
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
}
