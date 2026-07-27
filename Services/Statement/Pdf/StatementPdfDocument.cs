// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Pdf/StatementPdfDocument.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIXES vs PREVIOUS VERSION:
//   1. using SkiaSharp; — now works because SkiaSharp 2.88.8 is in .csproj
//   2. Canvas callback: QuestPDF 2024.12.2 Canvas callback signature is:
//      .Canvas((canvas, size) => { })
//      where canvas is typed as SkiaSharp.SKCanvas directly.
//      The cast "if (canvas is not SKCanvas sk)" is WRONG —
//      canvas IS already SKCanvas. Just use it directly.
//   3. RenderAgingBars signature: parameter is SKCanvas (not object).
//      Remove the cast entirely.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ProGlassAutomation.Services.Statement.Pdf
{
    /// <summary>
    /// QuestPDF IDocument implementation for Account Statement.
    /// Implements IDocument directly — GeneratePdf() works without wrapper.
    /// </summary>
    public class StatementPdfDocument : IDocument
    {
        // ── Brand colors ──────────────────────────────────────────────────
        private const string ACCENT = "#4338CA";
        private const string DARK = "#0F172A";
        private const string MUTED = "#475569";
        private const string LINE = "#CBD5E1";
        private const string LIGHT_BG = "#F1F5F9";
        private const string TABLE_HDR = "#1E293B";
        private const string GREEN = "#047857";
        private const string AMBER = "#B45309";
        private const string RED = "#B91C1C";
        private const string WHITE = "#FFFFFF";

        private readonly StatementSnapshot _snap;

        public StatementPdfDocument(StatementSnapshot snap)
        {
            _snap = snap ?? throw new ArgumentNullException(nameof(snap));
        }

        // ── IDocument ─────────────────────────────────────────────────────

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
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t =>
                    t.FontFamily("Arial")
                     .FontSize(9)
                     .FontColor(DARK));

                page.Header().Element(RenderPageHeader);
                page.Content().Element(RenderContent);
                page.Footer().Element(RenderFooter);
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAGE HEADER
        // ═════════════════════════════════════════════════════════════════

        private void RenderPageHeader(IContainer container)
        {
            container
                .PaddingBottom(8)
                .Column(col =>
                {
                    col.Item()
                       .Background(ACCENT)
                       .PaddingHorizontal(14).PaddingVertical(8)
                       .Row(row =>
                       {
                           row.RelativeItem().Column(c =>
                           {
                               c.Item()
                                .Text("ACCOUNT STATEMENT")
                                .FontSize(14).Bold().FontColor(WHITE);
                               c.Item().PaddingTop(2)
                                .Text(_snap.Header.Title)
                                .FontSize(9).FontColor("#C7D2FE");
                           });

                           row.AutoItem().AlignRight().Column(c =>
                           {
                               c.Item().AlignRight()
                                .Text(_snap.Header.StatementNumber)
                                .FontSize(13).Bold().FontColor(WHITE);
                               c.Item().PaddingTop(2).AlignRight()
                                .Text("Generated: " +
                                      _snap.Header.GeneratedAt
                                           .ToString("dd MMM yyyy  HH:mm"))
                                .FontSize(8).FontColor("#C7D2FE");
                           });
                       });

                    col.Item()
                       .Background(TABLE_HDR)
                       .PaddingHorizontal(14).PaddingVertical(5)
                       .Row(row =>
                       {
                           row.RelativeItem()
                              .Text("Period: " + _snap.Header.PeriodDisplay)
                              .FontSize(8).FontColor("#94A3B8");
                           row.AutoItem().AlignRight()
                              .Text(_snap.Header.FilterSummary)
                              .FontSize(8).FontColor("#94A3B8");
                       });
                });
        }

        // ═════════════════════════════════════════════════════════════════
        //  MAIN CONTENT
        // ═════════════════════════════════════════════════════════════════

        private void RenderContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().PaddingTop(8).Element(RenderCustomerBlock);
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
        //  CUSTOMER BLOCK
        // ═════════════════════════════════════════════════════════════════

        private void RenderCustomerBlock(IContainer container)
        {
            container
                .Background(LIGHT_BG)
                .Border(1).BorderColor(LINE)
                .Padding(12)
                .Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("FROM / SUPPLIER")
                           .FontSize(8).Bold().FontColor(MUTED);
                        col.Item().PaddingTop(4)
                           .Text(_snap.CustomerInfo.CompanyName)
                           .FontSize(11).Bold().FontColor(DARK);
                        if (!string.IsNullOrWhiteSpace(
                                _snap.CustomerInfo.SalesmanName))
                            col.Item().PaddingTop(2)
                               .Text("Salesman: " +
                                     _snap.CustomerInfo.SalesmanName)
                               .FontSize(9).FontColor(MUTED);
                    });

                    row.ConstantItem(1).Background(LINE);
                    row.ConstantItem(20);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("BILL TO / CUSTOMER")
                           .FontSize(8).Bold().FontColor(MUTED);
                        col.Item().PaddingTop(4)
                           .Text(_snap.CustomerInfo.CustomerName)
                           .FontSize(11).Bold().FontColor(DARK);
                        if (!string.IsNullOrWhiteSpace(
                                _snap.CustomerInfo.ProjectName))
                            col.Item().PaddingTop(2)
                               .Text("Project: " +
                                     _snap.CustomerInfo.ProjectName)
                               .FontSize(9).FontColor(MUTED);
                    });
                });
        }

        // ═════════════════════════════════════════════════════════════════
        //  INVOICE TABLE
        // ═════════════════════════════════════════════════════════════════

        private void RenderInvoiceTable(IContainer container)
        {
            container.Column(col =>
            {
                col.Item()
                   .Background(TABLE_HDR)
                   .PaddingHorizontal(10).PaddingVertical(6)
                   .Text("INVOICE SUMMARY")
                   .FontSize(10).Bold().FontColor(WHITE);

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
                        foreach (var h in new[]
                        {
                            "INVOICE NO","DATE","PI REF",
                            "DN REF","QTY","TOTAL (AED)",
                            "BALANCE","STATUS"
                        })
                        {
                            header.Cell()
                                  .Background(ACCENT)
                                  .PaddingHorizontal(5).PaddingVertical(5)
                                  .Text(h).FontSize(8).Bold()
                                  .FontColor(WHITE);
                        }
                    });

                    bool alt = false;
                    foreach (var inv in _snap.InvoiceRows)
                    {
                        string bg = alt ? LIGHT_BG : WHITE;
                        alt = !alt;
                        string statusColor = GetStatusColor(inv.Status);
                        string balColor = inv.Balance > 0
                            ? (inv.IsOverdue ? RED : AMBER) : GREEN;

                        AddCell(table, inv.InvoiceNumber, bg,
                            bold: true, color: ACCENT);
                        AddCell(table, inv.IssueDateDisplay, bg);
                        AddCell(table, inv.ProformaId, bg, color: MUTED);
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
                        AddCellCenter(table, inv.Status, bg,
                            color: statusColor, bold: true);
                    }
                });

                col.Item().Background(TABLE_HDR).Table(footer =>
                {
                    footer.ColumnsDefinition(c =>
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

                    footer.Cell().ColumnSpan(4)
                          .PaddingHorizontal(8).PaddingVertical(6)
                          .Text($"TOTAL — " +
                                $"{_snap.Financial.TotalInvoiceCount} INVOICES")
                          .FontSize(9).Bold().FontColor(WHITE);

                    footer.Cell()
                          .PaddingHorizontal(5).PaddingVertical(6)
                          .AlignRight()
                          .Text(_snap.Financial.TotalQty.ToString())
                          .FontSize(9).Bold().FontColor(WHITE);

                    footer.Cell()
                          .PaddingHorizontal(5).PaddingVertical(6)
                          .AlignRight()
                          .Text($"AED {_snap.Financial.TotalInvoiced:N2}")
                          .FontSize(9).Bold().FontColor(WHITE);

                    footer.Cell()
                          .PaddingHorizontal(5).PaddingVertical(6)
                          .AlignRight()
                          .Text($"AED {_snap.Financial.TotalOutstanding:N2}")
                          .FontSize(9).Bold()
                          .FontColor(_snap.Financial.TotalOutstanding > 0
                              ? "#FCA5A5" : "#6EE7B7");

                    footer.Cell()
                          .PaddingHorizontal(5).PaddingVertical(6)
                          .Text("");
                });
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  FINANCIAL SUMMARY
        // ═════════════════════════════════════════════════════════════════

        private void RenderFinancialSummary(IContainer container)
        {
            var fin = _snap.Financial;

            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
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

                row.ConstantItem(24);

                row.ConstantItem(200).AlignMiddle().Column(col =>
                {
                    col.Item().Background(ACCENT).Padding(16).Column(inner =>
                    {
                        inner.Item().AlignCenter()
                             .Text("TOTAL OUTSTANDING")
                             .FontSize(9).Bold().FontColor("#C7D2FE");
                        inner.Item().PaddingTop(6).AlignCenter()
                             .Text($"AED {fin.TotalOutstanding:N2}")
                             .FontSize(18).Bold().FontColor(WHITE);
                        inner.Item().PaddingTop(4).AlignCenter()
                             .Text($"{fin.TotalInvoiceCount} Invoice(s)")
                             .FontSize(8).FontColor("#C7D2FE");
                    });

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
                                    .FontSize(13).Bold().FontColor(WHITE);
                           });
                    }
                });
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  AGING ANALYSIS
        // ═════════════════════════════════════════════════════════════════════

        private void RenderAgingAnalysis(IContainer container)
        {
            var aging = _snap.Aging;
            var total = aging.Total;

            container.Column(col =>
            {
                col.Item()
                   .Text("AGING ANALYSIS (Based on Due Date)")
                   .FontSize(10).Bold().FontColor(DARK);

                col.Item().PaddingTop(8)
                   .Background(LIGHT_BG)
                   .Border(1).BorderColor(LINE)
                   .Padding(12)
                   .Column(inner =>
                   {
                       // FIX: QuestPDF 2024.12.2 passes canvas as object.
                       // Cast explicitly to SkiaSharp.SKCanvas.
                       inner.Item().Height(80).Canvas((canvas, size) =>
                       {
                           var sk = (SkiaSharp.SKCanvas)canvas;
                           RenderAgingBars(sk, size, aging, total);
                       });

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

        /// <summary>
        /// Renders aging bars using SkiaSharp.
        /// Parameter is SkiaSharp.SKCanvas — cast done at call site.
        /// </summary>
        private static void RenderAgingBars(
            SkiaSharp.SKCanvas canvas,
            QuestPDF.Infrastructure.Size size,
            StatementAgingBuckets aging,
            decimal total)
        {
            float w = (float)size.Width;
            float h = (float)size.Height;
            float barW = w / 5f;
            float pad = barW * 0.2f;
            float maxH = h - 8f;

            var buckets = new[]
            {
        (aging.Current,    SkiaSharp.SKColor.Parse(GREEN)),
        (aging.Days0To30,  SkiaSharp.SKColor.Parse(AMBER)),
        (aging.Days31To60, SkiaSharp.SKColor.Parse(AMBER)),
        (aging.Days61To90, SkiaSharp.SKColor.Parse(RED)),
        (aging.Days90Plus, SkiaSharp.SKColor.Parse(RED))
    };

            // Baseline
            using var linePaint = new SkiaSharp.SKPaint
            {
                Color = SkiaSharp.SKColor.Parse(LINE),
                StrokeWidth = 1f,
                IsAntialias = true,
                IsStroke = true
            };
            canvas.DrawLine(0, h - 4, w, h - 4, linePaint);

            for (int i = 0; i < buckets.Length; i++)
            {
                var (amount, color) = buckets[i];
                if (total <= 0m || amount <= 0m) continue;

                float ratio = (float)(amount / total);
                float barH = maxH * ratio;
                float x = i * barW + pad;
                float bw = barW - pad * 2f;
                float y = h - 4f - barH;

                using var paint = new SkiaSharp.SKPaint
                {
                    Color = color,
                    IsAntialias = true
                };

                var rect = new SkiaSharp.SKRect(x, y, x + bw, h - 4f);
                canvas.DrawRoundRect(rect, 3f, 3f, paint);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SIGNATURE BLOCK
        // ═════════════════════════════════════════════════════════════════

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
                          .Text(_snap.CustomerInfo.SalesmanName.Length > 0
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
                            .BorderBottom(1).BorderColor(LINE).Height(1);
                       });
                       col.Item().PaddingTop(8).Row(r =>
                       {
                           r.AutoItem().Text("Signature: ")
                            .FontSize(9).FontColor(MUTED);
                           r.RelativeItem().PaddingLeft(4)
                            .BorderBottom(1).BorderColor(LINE).Height(1);
                       });
                       col.Item().PaddingTop(8).Row(r =>
                       {
                           r.AutoItem().Text("Date: ")
                            .FontSize(9).FontColor(MUTED);
                           r.RelativeItem().PaddingLeft(4)
                            .BorderBottom(1).BorderColor(LINE).Height(1);
                       });
                       col.Item().PaddingTop(6)
                          .Text("Company Stamp / Seal")
                          .FontSize(8).FontColor(MUTED);
                   });
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAGE FOOTER
        // ═════════════════════════════════════════════════════════════════

        private void RenderFooter(IContainer container)
        {
            container
                .BorderTop(1).BorderColor(LINE)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem()
                       .Text(_snap.Header.StatementNumber)
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

        private static string GetStatusColor(string status) => status switch
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
            var cell = table.Cell()
                .Background(bg)
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(5).PaddingVertical(4);
            var txt = cell.Text(text).FontSize(8);
            if (bold) txt.Bold();
            if (color != null) txt.FontColor(color);
        }

        private static void AddCellRight(
            TableDescriptor table, string text, string bg,
            bool bold = false, string? color = null)
        {
            var cell = table.Cell()
                .Background(bg)
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(5).PaddingVertical(4)
                .AlignRight();
            var txt = cell.Text(text).FontSize(8);
            if (bold) txt.Bold();
            if (color != null) txt.FontColor(color);
        }

        private static void AddCellCenter(
            TableDescriptor table, string text, string bg,
            bool bold = false, string? color = null)
        {
            var cell = table.Cell()
                .Background(bg)
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(4).PaddingVertical(4)
                .AlignCenter();
            var txt = cell.Text(text).FontSize(8);
            if (bold) txt.Bold();
            if (color != null) txt.FontColor(color);
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