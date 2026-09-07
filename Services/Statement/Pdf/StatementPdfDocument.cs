// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Pdf/StatementPdfDocument.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report
//
// ROOT CAUSE (found by reading debug output properly):
//   RenderStatementHeader used AutoItem() for filter summary text.
//   AutoItem takes exactly what it needs — no shrinking allowed.
//   The filter summary "Company: X | Customer: Y | Project: Z |
//   Salesman: W | Status: A, B, C" is often 700+ pixels on one line.
//   When container is 507px, AutoItem demanded more than available,
//   forcing RelativeItem into negative width → CRASH.
//
// FIX:
//   Filter summary now on its OWN row below period, using full width
//   with proper text wrapping. No more Row + AutoItem conflict.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ProGlassAutomation.Services.Statement.Pdf
{
    public class StatementPdfDocument : IDocument
    {
        // ── Brand colors ──────────────────────────────────────────────────
        private static string ACCENT => DocumentStyle.ColorAccent;
        private static string DARK => DocumentStyle.ColorText;
        private static string MUTED => DocumentStyle.ColorMuted;
        private static string LINE => DocumentStyle.ColorBorder;
        private static string LIGHT_BG => DocumentStyle.ColorSurface;
        private static string TABLE_HDR => DocumentStyle.ColorTableHeader;
        private static string GREEN => DocumentStyle.ColorSuccess;
        private static string AMBER => DocumentStyle.ColorWarning;
        private static string RED => DocumentStyle.ColorDanger;
        private static string WHITE => DocumentStyle.ColorWhite;

        private readonly StatementSnapshot _snap;

        public StatementPdfDocument(StatementSnapshot snap)
        {
            _snap = snap ?? throw new ArgumentNullException(nameof(snap));
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = _snap.Header.Title,
            Author = _snap.CustomerInfo.CompanyName,
            Subject = "Account Statement",
            Keywords = "statement, invoice, UAE, glass",
            Creator = "ProGlass Automation",
            Producer = "QuestPDF"
        };

        public DocumentSettings GetSettings() => new()
        {
            PdfA = false
        };

        public void Compose(IDocumentContainer container)
        {
            PdfThemeCatalog.ApplySaved();
            PdfChrome.Reset();
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(DocumentStyle.MarginMm, Unit.Millimetre);
                page.DefaultTextStyle(t =>
                    t.FontFamily(DocumentStyle.FontFamily)
                     .FontSize(9)
                     .FontColor(DARK));

                page.Header().Column(h =>
                {
                    h.Item().ShowOnce().Element(RenderStatementHeader);
                    h.Item().SkipOnce().Element(c =>
                    {
                        var info = new LetterheadInfo
                        {
                            CompanyName = _snap.CustomerInfo.CompanyName ?? "",
                            CompanyTrn = _snap.CustomerInfo.CompanyTrn ?? "",
                            DocumentTitle = "ACCOUNT STATEMENT",
                            DocumentId = _snap.Header.StatementNumber ?? ""
                        };
                        PdfChrome.Continuation(c, info);
                    });
                });
                page.Content().PaddingTop(8).Element(RenderContent);
                page.Footer().Element(f =>
                    PdfChrome.Footer(f, _snap.Header.StatementNumber));
            });
        }

        private void RenderContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(RenderCustomerBlock);

                col.Item().PaddingTop(8).Element(RenderAppliedFilters);

                col.Item().PaddingTop(10).PaddingBottom(6)
                   .LineHorizontal(1).LineColor(LINE);

                col.Item().Element(RenderInvoiceTable);

                col.Item().PaddingTop(8).PaddingBottom(8)
                   .LineHorizontal(1).LineColor(LINE);

                col.Item().Element(RenderFinancialSummary);

                col.Item().PaddingTop(10).PaddingBottom(10)
                   .LineHorizontal(1).LineColor(LINE);

                col.Item().Element(RenderAgingAnalysis);

                col.Item().PaddingTop(16).Element(RenderSignatureBlock);

                col.Item().PaddingTop(10)
                   .BorderTop(1).BorderColor(LINE).PaddingTop(6)
                   .Text("This statement is computer-generated. " +
                         "Please contact us if you find discrepancies.")
                   .FontSize(8).Italic().FontColor(MUTED);
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  STATEMENT HEADER — ROOT CAUSE FIX
        //
        //  OLD (crashed):
        //    Row with RelativeItem (period) + AutoItem (filter summary)
        //    Filter summary was 759px wide on single line.
        //    AutoItem could not shrink → RelativeItem went negative → CRASH.
        //
        //  NEW (works):
        //    Period bar on its own line.
        //    Filter summary on its OWN line below, wraps naturally,
        //    no Row conflict, no AutoItem overflow.
        // ═════════════════════════════════════════════════════════════════

        private void RenderStatementHeader(IContainer container)
        {
            var info = new LetterheadInfo
            {
                CompanyName = _snap.CustomerInfo.CompanyName ?? "",
                CompanyTrn = _snap.CustomerInfo.CompanyTrn ?? "",
                DocumentTitle = "ACCOUNT STATEMENT",
                DocumentId = _snap.Header.StatementNumber ?? "",
                SubtitleLine = _snap.Header.Title,
                LogoPath = _snap.CustomerInfo.LogoPath ?? "",
                LogoBase64 = _snap.CustomerInfo.LogoBase64 ?? "",
                LogoLayout = _snap.CustomerInfo.LogoLayout,
                LogoWidthMm = _snap.CustomerInfo.LogoWidthMm,
                LogoHeightMm = _snap.CustomerInfo.LogoHeightMm
            };
            info.Meta.Add(("Period", _snap.Header.PeriodDisplay ?? ""));
            info.Meta.Add(("Generated",
                _snap.Header.GeneratedAt.ToString("dd MMM yyyy  HH:mm", CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(_snap.Header.FilterSummary))
                info.Meta.Add(("Filter", _snap.Header.FilterSummary));

            PdfChrome.Letterhead(container, info);
        }

        private void RenderCustomerBlock(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(c =>
                    PdfChrome.InfoCard(c, "From / Supplier", col =>
                    {
                        col.Item().Text(_snap.CustomerInfo.CompanyName ?? "")
                            .FontSize(11).Bold().FontColor(DARK);
                        if (!string.IsNullOrWhiteSpace(_snap.CustomerInfo.SalesmanName))
                        {
                            col.Item().PaddingTop(4);
                            PdfChrome.LabelValue(col, "Salesman",
                                _snap.CustomerInfo.SalesmanName);
                        }
                    }));
                row.ConstantItem(12);
                row.RelativeItem().Element(c =>
                    PdfChrome.InfoCard(c, "Bill To / Customer", col =>
                    {
                        col.Item().Text(_snap.CustomerInfo.CustomerName ?? "")
                            .FontSize(11).Bold().FontColor(DARK);
                        if (!string.IsNullOrWhiteSpace(_snap.CustomerInfo.ProjectName))
                        {
                            col.Item().PaddingTop(4);
                            PdfChrome.LabelValue(col, "Project",
                                _snap.CustomerInfo.ProjectName);
                        }
                    }));
            });
        }

        /// <summary>
        /// Renders a dedicated "Applied filters" block so the statement always
        /// shows exactly which filters were used (customer / project / company /
        /// salesman / period / status) — not just crammed into the header meta.
        /// </summary>
        private void RenderAppliedFilters(IContainer container)
        {
            var raw = _snap.Header.FilterSummary ?? "";
            if (string.IsNullOrWhiteSpace(raw)) return;

            var filters = raw.Split(
                new[] { "  |  ", " | ", " |", "| " },
                StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => f.Length > 0)
                .ToList();

            if (filters.Count == 0) return;

            container.Background(LIGHT_BG)
                .Border(0.6f).BorderColor(LINE)
                .Padding(10)
                .Column(col =>
                {
                    col.Item().Text("APPLIED FILTERS")
                        .FontSize(8).Bold().FontColor(ACCENT);
                    col.Item().PaddingTop(6);

                    foreach (var f in filters)
                    {
                        var idx = f.IndexOf(':');
                        if (idx > 0)
                        {
                            var label = f.Substring(0, idx).Trim();
                            var value = f.Substring(idx + 1).Trim();
                            col.Item().PaddingTop(2).Row(r =>
                            {
                                r.ConstantItem(70)
                                    .Text(label.ToUpperInvariant())
                                    .FontSize(7.5f).FontColor(MUTED);
                                r.RelativeItem().PaddingLeft(4)
                                    .Text(value)
                                    .FontSize(8.5f).SemiBold().FontColor(DARK);
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(2).Text(f)
                                .FontSize(8.5f).SemiBold().FontColor(DARK);
                        }
                    }
                });
        }

        private void RenderInvoiceTable(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Element(c =>
                    PdfChrome.Section(c, "Invoice summary"));

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(1.0f);
                        c.RelativeColumn(1.1f);
                        c.ConstantColumn(30);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(0.9f);
                        c.ConstantColumn(56);
                    });

                    table.Header(header =>
                    {
                        var heads = new[]
                        {
                            "INVOICE NO","DATE","PI REF",
                            "DN REF","QTY","TOTAL (AED)",
                            "BALANCE","STATUS"
                        };
                        for (int i = 0; i < heads.Length; i++)
                            PdfChrome.HeaderCell(header.Cell(), heads[i],
                                center: i >= 4, right: i == 5 || i == 6);
                    });

                    bool alt = false;
                    foreach (var inv in _snap.InvoiceRows)
                    {
                        string bg = alt ? LIGHT_BG : WHITE;
                        alt = !alt;
                        string statusColor = GetStatusColor(inv.Status);
                        string balColor = inv.Balance > 0
                            ? (inv.IsOverdue ? RED : AMBER) : GREEN;

                        AddCell(table, inv.InvoiceNumber ?? "", bg,
                            bold: true, color: ACCENT);
                        AddCell(table, inv.IssueDateDisplay, bg);
                        AddCell(table, inv.ProformaId ?? "", bg, color: MUTED);
                        AddCell(table,
                            string.IsNullOrWhiteSpace(inv.DeliveryNoteRef)
                                ? "—" : inv.DeliveryNoteRef,
                            bg, color: MUTED);
                        AddCellRight(table, inv.TotalQty.ToString(), bg);
                        AddCellRight(table,
                            $"AED {inv.GrandTotal:N2}", bg, bold: true);
                        AddCellRight(table,
                            inv.Balance > 0
                                ? $"AED {inv.Balance:N2}" : "PAID",
                            bg, bold: inv.Balance > 0, color: balColor);
                        AddCellCenter(table, inv.Status ?? "", bg,
                            color: statusColor, bold: true);
                    }
                });

                col.Item().Element(c =>
                    PdfChrome.GrandTotal(c,
                        $"{_snap.Financial.TotalInvoiceCount} invoices  ·  {_snap.Financial.TotalQty} pcs",
                        $"AED {_snap.Financial.TotalOutstanding:N2} outstanding"));
            });
        }

        private void RenderFinancialSummary(IContainer container)
        {
            var fin = _snap.Financial;

            container.Row(row =>
            {
                row.RelativeItem(2).Column(col =>
                {
                    col.Item().Text("FINANCIAL SUMMARY")
                       .FontSize(10).Bold().FontColor(DARK);

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(120);
                        });

                        AddSummaryRow(t, "Total Invoiced:",
                            $"AED {fin.TotalInvoiced:N2}");
                        AddSummaryRow(t, "Total VAT (5%):",
                            $"AED {fin.TotalVat:N2}");
                        AddSummaryRow(t, "Total Paid:",
                            $"AED {fin.TotalPaid:N2}", color: GREEN);
                        AddSummaryRow(t, "Total Outstanding:",
                            $"AED {fin.TotalOutstanding:N2}",
                            color: fin.TotalOutstanding > 0 ? AMBER : GREEN,
                            bold: true);
                        AddSummaryRow(t, "Overdue Amount:",
                            fin.OverdueAmount > 0
                                ? $"AED {fin.OverdueAmount:N2}" : "None",
                            color: fin.OverdueAmount > 0 ? RED : GREEN,
                            bold: fin.OverdueAmount > 0);
                        AddSummaryRow(t, "Total Qty (pcs):",
                            fin.TotalQty.ToString());
                        AddSummaryRow(t, "Total Area:",
                            $"{fin.TotalSqm:N3} m²");
                    });
                });

                row.ConstantItem(20);

                row.RelativeItem(1).Column(col =>
                {
                    col.Item().Element(c =>
                        PdfChrome.GrandTotal(c,
                            "Total outstanding",
                            $"AED {fin.TotalOutstanding:N2}"));

                    if (fin.OverdueAmount > 0)
                    {
                        col.Item().PaddingTop(8)
                           .Background(RED).Padding(10).Column(inner =>
                           {
                               inner.Item().AlignCenter()
                                    .Text("OVERDUE")
                                    .FontSize(8).Bold().FontColor(WHITE);
                               inner.Item().PaddingTop(4).AlignCenter()
                                    .Text($"AED {fin.OverdueAmount:N2}")
                                    .FontSize(12).Bold().FontColor(WHITE);
                           });
                    }
                });
            });
        }

        private void RenderAgingAnalysis(IContainer container)
        {
            var aging = _snap.Aging;
            var total = aging.Total;

            container.Column(col =>
            {
                col.Item().Element(c =>
                    PdfChrome.Section(c, "Aging analysis"));

                col.Item().PaddingTop(8)
                   .Background(LIGHT_BG)
                   .Border(1).BorderColor(LINE)
                   .Padding(12)
                   .Column(inner =>
                   {
                       string svg = BuildAgingChartSvg(aging, total);
                       inner.Item().Svg(svg);

                       inner.Item().PaddingTop(6).Table(t =>
                       {
                           t.ColumnsDefinition(c =>
                           {
                               c.RelativeColumn();
                               c.RelativeColumn();
                               c.RelativeColumn();
                               c.RelativeColumn();
                               c.RelativeColumn();
                           });

                           var buckets = new[]
                           {
                               ("Current",    aging.Current,    GREEN),
                               ("0-30 Days",  aging.Days0To30,  AMBER),
                               ("31-60 Days", aging.Days31To60, AMBER),
                               ("61-90 Days", aging.Days61To90, RED),
                               ("90+ Days",   aging.Days90Plus, RED)
                           };

                           foreach (var (label, _, _) in buckets)
                               t.Cell().AlignCenter()
                                .Text(label).FontSize(8).Bold()
                                .FontColor(MUTED);

                           foreach (var (_, amount, color) in buckets)
                               t.Cell().AlignCenter()
                                .Text(amount > 0
                                    ? $"AED {amount:N0}" : "—")
                                .FontSize(9).Bold().FontColor(color);

                           foreach (var (_, amount, _) in buckets)
                           {
                               decimal pct = total > 0
                                   ? Math.Round(amount / total * 100, 1)
                                   : 0m;
                               t.Cell().AlignCenter()
                                .Text(pct > 0 ? $"{pct}%" : "")
                                .FontSize(8).FontColor(MUTED);
                           }
                       });
                   });
            });
        }

        private static string BuildAgingChartSvg(
            StatementAgingBuckets aging,
            decimal total)
        {
            var inv = CultureInfo.InvariantCulture;

            const int viewW = 1000;
            const int viewH = 100;
            const int baselineY = viewH - 6;
            const int maxBarH = viewH - 12;

            float barW = viewW / 5f;
            float pad = barW * 0.2f;
            float bw = barW - pad * 2f;

            var buckets = new[]
            {
                (Amount: aging.Current,    Color: GREEN),
                (Amount: aging.Days0To30,  Color: AMBER),
                (Amount: aging.Days31To60, Color: AMBER),
                (Amount: aging.Days61To90, Color: RED),
                (Amount: aging.Days90Plus, Color: RED)
            };

            var sb = new StringBuilder();
            sb.Append("<svg xmlns='http://www.w3.org/2000/svg' ");
            sb.Append($"viewBox='0 0 {viewW} {viewH}' ");
            sb.Append("preserveAspectRatio='xMidYMid meet'>");

            sb.Append($"<line x1='0' y1='{baselineY.ToString(inv)}' ");
            sb.Append($"x2='{viewW.ToString(inv)}' y2='{baselineY.ToString(inv)}' ");
            sb.Append($"stroke='{LINE}' stroke-width='1' />");

            for (int i = 0; i < buckets.Length; i++)
            {
                var (amount, color) = buckets[i];
                if (total <= 0m || amount <= 0m) continue;

                float ratio = (float)(amount / total);
                float barH = maxBarH * ratio;
                float x = i * barW + pad;
                float y = baselineY - barH;

                sb.Append("<rect ");
                sb.Append($"x='{x.ToString("0.##", inv)}' ");
                sb.Append($"y='{y.ToString("0.##", inv)}' ");
                sb.Append($"width='{bw.ToString("0.##", inv)}' ");
                sb.Append($"height='{barH.ToString("0.##", inv)}' ");
                sb.Append("rx='4' ry='4' ");
                sb.Append($"fill='{color}' />");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        private void RenderSignatureBlock(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem()
                   .Border(1).BorderColor(LINE).Padding(10)
                   .Column(col =>
                   {
                       col.Item().Text("PREPARED BY")
                          .FontSize(9).Bold().FontColor(ACCENT);
                       col.Item().PaddingTop(4)
                          .Text(!string.IsNullOrWhiteSpace(
                                    _snap.CustomerInfo.SalesmanName)
                                ? _snap.CustomerInfo.SalesmanName : "—")
                          .FontSize(10).SemiBold().FontColor(DARK);
                       col.Item().PaddingTop(16)
                          .BorderBottom(1).BorderColor(LINE)
                          .Text("Signature").FontSize(8).FontColor(MUTED);
                   });

                row.ConstantItem(16);

                row.RelativeItem()
                   .Border(1).BorderColor(LINE).Padding(10)
                   .Column(col =>
                   {
                       col.Item().Text("CUSTOMER ACKNOWLEDGEMENT")
                          .FontSize(9).Bold().FontColor(ACCENT);
                       col.Item().PaddingTop(6).Row(r =>
                       {
                           r.AutoItem().Text("Name: ")
                            .FontSize(9).FontColor(MUTED);
                           r.RelativeItem().PaddingLeft(4)
                            .BorderBottom(1).BorderColor(LINE)
                            .Text(" ").FontSize(9);
                       });
                       col.Item().PaddingTop(8).Row(r =>
                       {
                           r.AutoItem().Text("Signature: ")
                            .FontSize(9).FontColor(MUTED);
                           r.RelativeItem().PaddingLeft(4)
                            .BorderBottom(1).BorderColor(LINE)
                            .Text(" ").FontSize(9);
                       });
                       col.Item().PaddingTop(8).Row(r =>
                       {
                           r.AutoItem().Text("Date: ")
                            .FontSize(9).FontColor(MUTED);
                           r.RelativeItem().PaddingLeft(4)
                            .BorderBottom(1).BorderColor(LINE)
                            .Text(" ").FontSize(9);
                       });
                       col.Item().PaddingTop(6)
                          .Text("Company Stamp / Seal")
                          .FontSize(8).FontColor(MUTED);
                   });
            });
        }

        private void RenderFooter(IContainer container)
        {
            container
                .BorderTop(1).BorderColor(LINE)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem()
                       .Text(_snap.Header.StatementNumber ?? "")
                       .FontSize(8).FontColor(MUTED);

                    row.AutoItem().AlignCenter().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(MUTED);
                        t.CurrentPageNumber().FontSize(8).FontColor(MUTED);
                        t.Span(" of ").FontSize(8).FontColor(MUTED);
                        t.TotalPages().FontSize(8).FontColor(MUTED);
                    });

                    row.RelativeItem().AlignRight()
                       .Text("ProGlass Automation")
                       .FontSize(8).FontColor(MUTED);
                });
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        private static string GetStatusColor(string? status) => (status ?? "") switch
        {
            "Paid" => GREEN,
            "PartiallyPaid" => GREEN,
            "Overdue" => RED,
            "Cancelled" => RED,
            "Void" => RED,
            "Issued" => AMBER,
            _ => DARK
        };

        private static void AddCell(
            TableDescriptor table, string text, string bg,
            bool bold = false, string? color = null)
        {
            bool alt = string.Equals(bg, LIGHT_BG, StringComparison.OrdinalIgnoreCase);
            PdfChrome.DataCell(table.Cell(), text, alt: alt, bold: bold, color: color);
        }

        private static void AddCellRight(
            TableDescriptor table, string text, string bg,
            bool bold = false, string? color = null)
        {
            bool alt = string.Equals(bg, LIGHT_BG, StringComparison.OrdinalIgnoreCase);
            PdfChrome.DataCell(table.Cell(), text, alt: alt, right: true, bold: bold, color: color);
        }

        private static void AddCellCenter(
            TableDescriptor table, string text, string bg,
            bool bold = false, string? color = null)
        {
            bool alt = string.Equals(bg, LIGHT_BG, StringComparison.OrdinalIgnoreCase);
            PdfChrome.DataCell(table.Cell(), text, alt: alt, center: true, bold: bold, color: color);
        }

        private static void AddSummaryRow(
            TableDescriptor table, string label, string value,
            bool bold = false, string? color = null)
        {
            var lbl = table.Cell().PaddingVertical(3)
                .Text(label).FontSize(9);
            if (color != null) lbl.FontColor(color);
            if (bold) lbl.Bold();

            var val = table.Cell().PaddingVertical(3).AlignRight()
                .Text(value).FontSize(9);
            if (color != null) val.FontColor(color);
            if (bold) val.Bold();
        }
    }
}