// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/OrderReport/Pdf/OrderReportPdfExporter.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — PDF export (2026-09-06)
//
// PURPOSE:
//   Renders an OrderReportBundle to a single A4 PDF (one section per
//   Proforma Invoice) using the same QuestPDF + DocumentStyle + PdfChrome
//   pipeline as the Statement Generator. The report prints:
//     1. Order header (PI + job-release)
//     2. Proforma spec rows
//     3. Delivery Notes (delivered/balance + weights + attachment count)
//     4. Tax Invoices (invoiced value + balance)
//     5. Reconciliation status line + attachment completeness
//     6. Totals
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ProGlassAutomation.Services.OrderReport.Pdf
{
    public static class OrderReportPdfExporter
    {
        private static string OutputFolder
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation", "OrderReports");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        private static void EnsureLicense()
            => QuestPDF.Settings.License = LicenseType.Community;

        /// <summary>Generates the PDF and returns its full path.</summary>
        public static string Generate(OrderReportBundle bundle)
        {
            EnsureLicense();
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            string number = string.IsNullOrWhiteSpace(bundle.ReportNumber)
                ? "REPORT"
                : bundle.ReportNumber.Replace("/", "-").Replace("\\", "-");

            string path = Path.Combine(OutputFolder, $"{number}.pdf");

            var document = new OrderReportPdfDocument(bundle);
            document.GeneratePdf(path);

            AppLogService.Debug($"[OrderReport] PDF written to {path}");
            return path;
        }

        /// <summary>Opens the generated PDF in the default viewer.</summary>
        public static void OpenPdf(string? pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return;

            try
            {
                var guard = ProGlassAutomation.Services.Security.ProcessStartGuard
                    .TryOpenFile(pdfPath, ProGlassAutomation.Services.Common.CorrelationContext.NewIdFor("OrderReport", "OpenPdf"));
                if (guard.IsFailure)
                    throw new InvalidOperationException(guard.Error);
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"[OrderReport] OpenPdf failed: {ex.Message}");
            }
        }

        /// <summary>Deletes all PDFs for a report number (best-effort).</summary>
        public static void DeletePdfs(string reportNumber)
        {
            try
            {
                string path = Path.Combine(OutputFolder, $"{reportNumber}.pdf");
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[OrderReport] DeletePdfs failed: {ex.Message}");
            }
        }
    }

    /// <summary>QuestPDF document for one generated order report bundle.</summary>
    public class OrderReportPdfDocument : IDocument
    {
        private static string ACCENT => DocumentStyle.ColorAccent;
        private static string DARK => DocumentStyle.ColorText;
        private static string MUTED => DocumentStyle.ColorMuted;
        private static string LINE => DocumentStyle.ColorBorder;
        private static string SURFACE => DocumentStyle.ColorSurface;
        private static string HEADER => DocumentStyle.ColorTableHeader;
        private static string GREEN => DocumentStyle.ColorSuccess;
        private static string AMBER => DocumentStyle.ColorWarning;
        private static string RED => DocumentStyle.ColorDanger;

        private readonly OrderReportBundle _bundle;

        public OrderReportPdfDocument(OrderReportBundle bundle)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = _bundle.Title,
            Subject = "Order Fulfilment Report",
            Keywords = "order report, proforma, delivery note, tax invoice, glass",
            Creator = "ProGlass Automation",
            Producer = "QuestPDF"
        };

        public DocumentSettings GetSettings() => new() { PdfA = false };

        public void Compose(IDocumentContainer container)
        {
            PdfThemeCatalog.ApplySaved();
            PdfChrome.Reset();

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(DocumentStyle.MarginMm, Unit.Millimetre);
                page.DefaultTextStyle(t =>
                    t.FontFamily(DocumentStyle.FontFamily).FontSize(9).FontColor(DARK));

                page.Header().Column(h =>
                {
                    h.Item().ShowOnce().Element(RenderTitleHeader);
                    h.Item().SkipOnce().Element(c =>
                        PdfChrome.Continuation(c, new LetterheadInfo
                        {
                            CompanyName = _bundle.Sections.FirstOrDefault()?.Header.CompanyName ?? "",
                            DocumentTitle = "ORDER REPORT",
                            DocumentId = _bundle.ReportNumber ?? ""
                        }));
                });

                page.Content().PaddingTop(8).Element(RenderContent);
                page.Footer().Element(f => PdfChrome.Footer(f, _bundle.ReportNumber));
            });
        }

        private void RenderTitleHeader(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text(_bundle.Title ?? "Order Report")
                    .FontSize(15).SemiBold().FontColor(ACCENT);
                col.Item().Text($"Report {_bundle.ReportNumber}  ·  Generated {_bundle.GeneratedAt:dd MMM yyyy HH:mm}")
                    .FontSize(9).FontColor(MUTED);
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(LINE);
            });
        }

        private void RenderContent(IContainer container)
        {
            container.Column(col =>
            {
                foreach (var section in _bundle.Sections)
                {
                    col.Item().Element(c => RenderSection(c, section));
                }

                col.Item().PaddingTop(14).Text(
                    "This report is computer-generated from live ProGlass data. " +
                    "Attachment counts reflect documents currently stored in the system.")
                    .FontSize(8).Italic().FontColor(MUTED);
            });
        }

        private void RenderSection(IContainer container, OrderReportSnapshot s)
        {
            container.Column(col =>
            {
                // ── 1. Header ─────────────────────────────────────────────
                col.Item().PaddingVertical(6)
                    .Background(SURFACE).Padding(10)
                    .Column(h =>
                    {
                        h.Item().Text($"Order Report — {s.Header.ProformaId}")
                            .FontSize(12).SemiBold().FontColor(ACCENT);
                        h.Item().Text(
                            $"PI Date {s.Header.IssueDateDisplay}  ·  Company {s.Header.CompanyName}  ·  " +
                            $"Customer {s.Header.CustomerName}  ·  Project {s.Header.ProjectName}")
                            .FontSize(9);
                        h.Item().Text(
                            $"Job Release: {s.Header.JobOrderIds}  ·  Status {s.Header.JobOrderStatus}  ·  " +
                            $"Priority {s.Header.JobOrderPriority}  ·  Release {s.Header.ReleaseDateDisplay}")
                            .FontSize(9).FontColor(s.Header.HasJobOrder ? DARK : MUTED);
                    });

                // ── 2. Spec rows ─────────────────────────────────────────
                col.Item().PaddingTop(8).Text("Proforma Invoice — Specification Rows")
                    .FontSize(10).SemiBold();
                col.Item().Element(c => RenderSpecTable(c, s));

                // ── 3. Delivery notes ─────────────────────────────────────
                if (s.DeliveryNotes.Count > 0)
                {
                    col.Item().PaddingTop(10).Text("Delivery Notes")
                        .FontSize(10).SemiBold();
                    foreach (var dn in s.DeliveryNotes)
                        col.Item().Element(c => RenderDn(c, dn));
                }

                // ── 4. Tax invoices ───────────────────────────────────────
                if (s.TaxInvoices.Count > 0)
                {
                    col.Item().PaddingTop(10).Text("Tax Invoices")
                        .FontSize(10).SemiBold();
                    foreach (var ti in s.TaxInvoices)
                        col.Item().Element(c => RenderTi(c, ti));
                }

                // ── 5. Attachment completeness + totals ───────────────────
                col.Item().PaddingTop(8).Element(c => RenderCompleteness(c, s));

                col.Item().PaddingTop(12).LineHorizontal(1).LineColor(LINE);
            });
        }

        private static void RenderSpecTable(IContainer container, OrderReportSnapshot s)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.6f);   // spec
                    c.RelativeColumn(1.4f);   // dims
                    c.RelativeColumn(0.8f);   // qty
                    c.RelativeColumn(1.0f);   // sqm
                    c.RelativeColumn(0.9f);   // delivered
                    c.RelativeColumn(0.9f);   // balance
                    c.RelativeColumn(1.2f);   // amount
                });

                table.Header(h =>
                {
                    h.Cell().Background(HEADER).Padding(3).Text("Specification").FontColor(DocumentStyle.ColorWhite).SemiBold();
                    h.Cell().Background(HEADER).Padding(3).Text("Dimensions").FontColor(DocumentStyle.ColorWhite).SemiBold();
                    h.Cell().Background(HEADER).Padding(3).Text("Qty").FontColor(DocumentStyle.ColorWhite).SemiBold();
                    h.Cell().Background(HEADER).Padding(3).Text("SQM").FontColor(DocumentStyle.ColorWhite).SemiBold();
                    h.Cell().Background(HEADER).Padding(3).Text("Deliv.").FontColor(DocumentStyle.ColorWhite).SemiBold();
                    h.Cell().Background(HEADER).Padding(3).Text("Balance").FontColor(DocumentStyle.ColorWhite).SemiBold();
                    h.Cell().Background(HEADER).Padding(3).Text("Amount").FontColor(DocumentStyle.ColorWhite).SemiBold();
                });

                foreach (var row in s.SpecRows)
                {
                    string spec = string.IsNullOrWhiteSpace(row.SpecName)
                        ? (row.GlassRef + " · " + (row.GroupName ?? ""))
                        : row.SpecName;
                    table.Cell().Padding(3).Text(spec);
                    table.Cell().Padding(3).Text(row.DimensionsDisplay);
                    table.Cell().Padding(3).Text(row.Qty.ToString());
                    table.Cell().Padding(3).Text(row.TotalSqm.ToString("N3", CultureInfo.InvariantCulture));
                    table.Cell().Padding(3).Text(row.DeliveredQty.ToString());
                    table.Cell().Padding(3).Text(row.BalanceQty.ToString());
                    table.Cell().Padding(3).AlignRight().Text(row.Amount.ToString("N2", CultureInfo.InvariantCulture));
                }
            });
        }

        private static void RenderDn(IContainer container, OrderReportDeliveryNote dn)
        {
            container.Column(col =>
            {
                col.Item().Text(
                    $"{dn.DeliveryNoteId}  ·  {dn.DeliveryDateDisplay} {dn.DeliveryTime}  ·  " +
                    $"Delivered {dn.TotalDeliveredQty}  ·  Balance {dn.TotalBalanceQty}  ·  " +
                    $"Weight {dn.TotalWeightKg:N1} kg  ·  Attachments {dn.AttachmentCount}  ·  " +
                    $"Driver {dn.DriverName} ({dn.VehicleNumber})")
                    .FontSize(9).SemiBold();
                if (dn.Items.Count > 0)
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(1.0f);
                            c.RelativeColumn(1.0f);
                            c.RelativeColumn(1.0f);
                            c.RelativeColumn(1.2f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(SURFACE).Padding(2).Text("Glass").SemiBold();
                            h.Cell().Background(SURFACE).Padding(2).Text("Ordered").SemiBold();
                            h.Cell().Background(SURFACE).Padding(2).Text("This DN").SemiBold();
                            h.Cell().Background(SURFACE).Padding(2).Text("Balance").SemiBold();
                            h.Cell().Background(SURFACE).Padding(2).Text("Weight kg").SemiBold();
                        });
                        foreach (var item in dn.Items)
                        {
                            table.Cell().Padding(2).Text(item.GlassRef ?? "—");
                            table.Cell().Padding(2).Text(item.OrderedQty.ToString());
                            table.Cell().Padding(2).Text(item.DeliveredQty.ToString());
                            table.Cell().Padding(2).Text(item.BalanceQty.ToString());
                            table.Cell().Padding(2).Text(item.TotalWeight.ToString("N1", CultureInfo.InvariantCulture));
                        }
                    });
                }
                else
                {
                    col.Item().Text("No line items.").FontSize(8).FontColor(MUTED);
                }
            });
        }

        private static void RenderTi(IContainer container, OrderReportTaxInvoice ti)
        {
            container.Column(col =>
            {
                col.Item().Text(
                    $"{ti.InvoiceNumber} ({ti.InvoiceType})  ·  {ti.IssueDateDisplay}  ·  Status {ti.Status}  ·  " +
                    $"Qty {ti.TotalQty}  ·  Subtotal {ti.Subtotal:N2}  ·  VAT {ti.VatAmount:N2}  ·  " +
                    $"Grand Total {ti.GrandTotal:N2}  ·  Attachments {ti.AttachmentCount}")
                    .FontSize(9).SemiBold();
                if (!string.IsNullOrWhiteSpace(ti.DeliveryNoteReference))
                    col.Item().Text($"Covers delivery note: {ti.DeliveryNoteReference}")
                        .FontSize(8).FontColor(MUTED);
            });
        }

        private static void RenderCompleteness(IContainer container, OrderReportSnapshot s)
        {
            var a = s.Attachments;
            var t = s.Totals;

            container.Column(col =>
            {
                col.Item().PaddingTop(6).Text("Attachment Completeness")
                    .FontSize(10).SemiBold();
                col.Item().Text(
                    $"Proforma attachments: {a.ProformaAttachmentCount}  ·  " +
                    $"Delivery notes: {a.DeliveryNotesPrepared} prepared / {a.DeliveryNotesAttached} attached / " +
                    $"{a.DeliveryNotesMissing} missing  ·  Tax invoice attachments: {a.TaxInvoiceAttachmentCount}")
                    .FontSize(9);
                if (a.HasMissingDocuments)
                    col.Item().Text(a.MissingDocumentsDisplay)
                        .FontSize(9).FontColor(RED).SemiBold();

                col.Item().PaddingTop(8).Text("Totals")
                    .FontSize(10).SemiBold();
                col.Item().Text(
                    $"Ordered: {t.OrderedQty} pcs / {t.OrderedWeightKg:N1} kg / AED {t.OrderedValue:N2}  ·  " +
                    $"Delivered: {t.DeliveredQty} pcs / {t.DeliveredWeightKg:N1} kg  ·  " +
                    $"Balance: {t.BalanceQty} pcs / {t.BalanceWeightKg:N1} kg  ·  " +
                    $"Invoiced: {t.InvoicedQty} pcs / AED {t.InvoicedValue:N2}  ·  " +
                    $"Invoiced−Delivered: {t.InvoicedMinusDeliveredQty} pcs")
                    .FontSize(9);
            });
        }
    }
}
