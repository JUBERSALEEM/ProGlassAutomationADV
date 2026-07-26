using ProGlassAutomation.Models.TaxInvoice;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Linq;

namespace ProGlassAutomation.Services.TaxInvoice.Export
{
    /// <summary>
    /// PDF Layout for Tax Invoice complying with UAE 5% VAT FTA rules.
    /// Implements IPdfLayout to reuse the existing premium PDF export engine.
    /// </summary>
    public class TaxInvoicePdfService : IPdfLayout
    {
        private readonly Models.TaxInvoice.TaxInvoice _entity;
        private readonly TaxInvoiceSnapshot _snapshot;

        public TaxInvoicePdfService(Models.TaxInvoice.TaxInvoice entity)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = TaxInvoiceMapper.DeserializeSnapshot(entity);
        }

        // ── IDocumentLayout ──────────────────────────────────────────────
        public string DocumentPrefix => "INV";
        public string DocumentId => _entity.InvoiceNumber;
        public string DocumentTitle => "TAX INVOICE";

        public string CompanyName => _entity.CustomerName;
        public string Reference => _entity.ProjectName;
        public DateTime DocumentDate => _entity.InvoiceDate;

        // ── IPdfLayout ───────────────────────────────────────────────────
        public PageLayout PageLayout => PageLayout.A4Portrait;

        public LetterheadInfo? Letterhead => new LetterheadInfo
        {
            CompanyName = _snapshot.Items.FirstOrDefault() != null ? "ProGlass Automation ERP" : "ProGlass Automation",
            CompanyLocation = "Dubai, UAE",
            CompanyTrn = _entity.CompanyTrn,
            DocumentTitle = DocumentTitle,
            DocumentId = DocumentId,
            SubtitleLine = $"Invoice Date: {_entity.InvoiceDate:dd MMM yyyy}  ·  Currency: {_entity.Currency}"
        };

        public void RenderContent(ColumnDescriptor content)
        {
            // ── Section 1: Invoice details ──────────────────────────────────
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "INVOICE DETAILS"));
            content.Item().Element(RenderInvoiceDetails);

            // ── Section 2: Items table ──────────────────────────────────────
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "BILLED ITEMS"));

            var groups = _snapshot.Items
                .GroupBy(i => i.SpecGroupName)
                .ToList();

            if (groups.Count == 0)
            {
                content.Item().Text("No items on this tax invoice.").Italic().FontColor(DocumentStyle.ColorMuted);
            }
            else
            {
                foreach (var g in groups)
                {
                    content.Item().Element(c => RenderSpecGroupTable(c, g.Key, g.ToList()));
                }

                content.Item().Element(RenderSummarySection);
            }

            // ── Section 3: Signature ────────────────────────────────────────
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "SIGN-OFF & DECLARATION"));
            content.Item().Element(RenderSignOffBlock);
        }

        private void RenderInvoiceDetails(IContainer container)
        {
            container.Row(row =>
            {
                // Customer details (left column)
                row.RelativeItem()
                    .Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                    .Padding(8)
                    .Column(col =>
                    {
                        col.Item().Text("CUSTOMER")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .SemiBold()
                            .FontColor(DocumentStyle.ColorAccent);

                        col.Item().PaddingTop(4)
                            .Text(_entity.CustomerName)
                            .FontSize(DocumentStyle.FontSizeH2).Bold();

                        if (!string.IsNullOrWhiteSpace(_entity.ProjectName))
                            col.Item().Text($"Project: {_entity.ProjectName}")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);

                        if (!string.IsNullOrWhiteSpace(_entity.CustomerTrn))
                            col.Item().Text($"TRN: {_entity.CustomerTrn}")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);

                        col.Item().PaddingTop(6).Text("REFERENCES")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .SemiBold()
                            .FontColor(DocumentStyle.ColorAccent);

                        col.Item().PaddingTop(2)
                            .Text($"Delivery Note: {_entity.DeliveryNoteId}")
                            .FontSize(DocumentStyle.FontSizeSmall).SemiBold();

                        col.Item().Text($"Job Order: {_entity.JobOrderId}")
                            .FontSize(DocumentStyle.FontSizeSmall)
                            .FontColor(DocumentStyle.ColorMuted);

                        if (!string.IsNullOrWhiteSpace(_entity.ProformaId))
                            col.Item().Text($"Proforma Ref: {_entity.ProformaId}")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                    });

                row.ConstantItem(10);

                // Invoice metadata & UAE tax info (right column)
                row.RelativeItem()
                    .Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                    .Padding(8)
                    .Column(col =>
                    {
                        col.Item().Text("TAX & INVOICE METADATA")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .SemiBold()
                            .FontColor(DocumentStyle.ColorAccent);

                        col.Item().PaddingTop(4).Row(r =>
                        {
                            r.ConstantItem(80).Text("Invoice No:")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                            r.RelativeItem()
                                .Text(_entity.InvoiceNumber)
                                .FontSize(DocumentStyle.FontSizeSmall).Bold();
                        });

                        col.Item().Row(r =>
                        {
                            r.ConstantItem(80).Text("Invoice Date:")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                            r.RelativeItem()
                                .Text(_entity.InvoiceDate.ToString("dd MMM yyyy"))
                                .FontSize(DocumentStyle.FontSizeSmall).SemiBold();
                        });

                        col.Item().Row(r =>
                        {
                            r.ConstantItem(80).Text("Company TRN:")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                            r.RelativeItem()
                                .Text(string.IsNullOrWhiteSpace(_entity.CompanyTrn) ? "—" : _entity.CompanyTrn)
                                .FontSize(DocumentStyle.FontSizeSmall).SemiBold();
                        });

                        col.Item().Row(r =>
                        {
                            r.ConstantItem(80).Text("Currency:")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                            r.RelativeItem()
                                .Text(_entity.Currency)
                                .FontSize(DocumentStyle.FontSizeSmall).SemiBold();
                        });

                        col.Item().Row(r =>
                        {
                            r.ConstantItem(80).Text("Payment Status:")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);
                            r.RelativeItem()
                                .Text(_entity.PaymentStatus)
                                .FontSize(DocumentStyle.FontSizeSmall).SemiBold();
                        });

                        col.Item().PaddingTop(6).Text("TAX COMPLIANCE")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .SemiBold()
                            .FontColor(DocumentStyle.ColorAccent);
                        col.Item().Text("This Tax Invoice is issued pursuant to the UAE Federal Decree-Law No. (8) of 2017 on Value Added Tax. VAT rate of 5.0% applies to taxable items.")
                            .FontSize(8)
                            .FontColor(DocumentStyle.ColorMuted);
                    });
            });
        }

        private void RenderSpecGroupTable(
            IContainer container,
            string specGroupName,
            System.Collections.Generic.List<TaxInvoiceItemSnapshot> items)
        {
            container.PaddingBottom(6).Column(col =>
            {
                col.Item()
                    .Background(DocumentStyle.ColorAccentLight)
                    .PaddingHorizontal(6).PaddingVertical(3)
                    .Text(specGroupName)
                    .FontSize(DocumentStyle.FontSizeBody)
                    .Bold()
                    .FontColor(DocumentStyle.ColorAccent);

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(28);   // #
                        cols.RelativeColumn(2);    // Glass Ref
                        cols.ConstantColumn(70);   // Dimensions
                        cols.ConstantColumn(52);   // Sqm
                        cols.ConstantColumn(52);   // Qty Billed
                        cols.ConstantColumn(65);   // Rate (AED)
                        cols.ConstantColumn(80);   // Total Amount
                    });

                    t.Header(h =>
                    {
                        HeaderCell(h.Cell(), "#");
                        HeaderCell(h.Cell(), "GLASS REF");
                        HeaderCell(h.Cell(), "DIM (mm)");
                        HeaderCell(h.Cell(), "SQM");
                        HeaderCell(h.Cell(), "QTY");
                        HeaderCell(h.Cell(), $"RATE ({_entity.Currency})");
                        HeaderCell(h.Cell(), $"AMOUNT ({_entity.Currency})");
                    });

                    bool alt = false;
                    foreach (var item in items.OrderBy(i => i.SerialNumber))
                    {
                        string bg = alt ? DocumentStyle.ColorTableAlt : DocumentStyle.ColorWhite;

                        decimal sqm = SqmCalculator.CalculatePanel(item.W1, item.H1);
                        string dimStr = item.W2 > 0 || item.H2 > 0 ? $"{item.W1}x{item.H1} / {item.W2}x{item.H2}" : $"{item.W1}x{item.H1}";

                        // Rates/amounts are already in selected currency
                        decimal displayRate = Math.Round(item.Rate / _entity.ExchangeRate, 2);
                        decimal displayAmount = Math.Round(sqm * item.InvoicedQty * displayRate, 2);

                        DataCell(t.Cell(), item.SerialNumber.ToString(), bg);
                        DataCell(t.Cell(), item.GlassRef ?? "—", bg, align: false);
                        DataCell(t.Cell(), dimStr, bg);
                        DataCell(t.Cell(), sqm.ToString("0.000"), bg);
                        DataCell(t.Cell(), item.InvoicedQty.ToString(), bg, highlight: item.InvoicedQty > 0);
                        DataCell(t.Cell(), displayRate.ToString("N2"), bg);
                        DataCell(t.Cell(), displayAmount.ToString("N2"), bg, highlight: true);

                        alt = !alt;
                    }
                });
            });
        }

        private void RenderSummarySection(IContainer container)
        {
            container.PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Declaration & Terms").FontSize(DocumentStyle.FontSizeCaption).SemiBold().FontColor(DocumentStyle.ColorAccent);
                    col.Item().PaddingTop(4).Text("We certify that the items supplied on this invoice are in accordance with the specifications ordered. Goods once delivered are not returnable without prior written approval. Payments are subject to terms as agreed.").FontSize(8).FontColor(DocumentStyle.ColorMuted);
                });

                row.ConstantItem(20);

                row.ConstantItem(220).Border(0.5f).BorderColor(DocumentStyle.ColorBorder).Padding(8).Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Subtotal:").FontSize(10).FontColor(DocumentStyle.ColorMuted);
                        r.RelativeItem().AlignRight().Text($"{_entity.Currency} {_entity.Subtotal:N2}").FontSize(10).SemiBold();
                    });

                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("Discount:").FontSize(10).FontColor(DocumentStyle.ColorMuted);
                        r.RelativeItem().AlignRight().Text($"{_entity.Currency} {_entity.Discount:N2}").FontSize(10).SemiBold().FontColor(DocumentStyle.ColorDanger);
                    });

                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("Taxable Amount:").FontSize(10).FontColor(DocumentStyle.ColorMuted);
                        r.RelativeItem().AlignRight().Text($"{_entity.Currency} {_entity.TaxableAmount:N2}").FontSize(10).SemiBold();
                    });

                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("VAT (5.0%):").FontSize(10).FontColor(DocumentStyle.ColorMuted);
                        r.RelativeItem().AlignRight().Text($"{_entity.Currency} {_entity.VatAmount:N2}").FontSize(10).SemiBold();
                    });

                    col.Item().PaddingTop(4).BorderTop(0.5f).BorderColor(DocumentStyle.ColorBorder).PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("GRAND TOTAL:").FontSize(11).Bold().FontColor(DocumentStyle.ColorAccent);
                        r.RelativeItem().AlignRight().Text($"{_entity.Currency} {_entity.GrandTotal:N2}").FontSize(12).Bold().FontColor(DocumentStyle.ColorAccent);
                    });
                });
            });
        }

        private void RenderSignOffBlock(IContainer container)
        {
            container.Row(row =>
            {
                SignatureCell(row, "Prepared & Issued By");
                row.ConstantItem(20);
                SignatureCell(row, "Client Confirmation & Received By");
            });
        }

        private static void SignatureCell(RowDescriptor row, string label)
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().PaddingTop(24)
                    .BorderBottom(0.75f)
                    .BorderColor(DocumentStyle.ColorText);
                col.Item().PaddingTop(3).Text(label)
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
                col.Item().Text("Authorized Signature & Stamp")
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
                col.Item().Text("Date: _______________________")
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
            });
        }

        private static void HeaderCell(IContainer cell, string text)
        {
            cell.Background(DocumentStyle.ColorAccent)
                .PaddingHorizontal(DocumentStyle.TableCellPaddingH)
                .PaddingVertical(DocumentStyle.TableCellPaddingV)
                .AlignCenter()
                .Text(text)
                .FontSize(DocumentStyle.FontSizeSmall)
                .Bold()
                .FontColor(DocumentStyle.ColorWhite);
        }

        private static void DataCell(
            IContainer cell,
            string text,
            string bg,
            bool align = true,
            bool highlight = false,
            string? highlightColor = null)
        {
            var bordered = cell
                .Border(0.5f)
                .BorderColor(DocumentStyle.ColorBorder)
                .Background(bg)
                .PaddingHorizontal(DocumentStyle.TableCellPaddingH)
                .PaddingVertical(DocumentStyle.TableCellPaddingV);

            var textEl = align ? bordered.AlignCenter().Text(text) : bordered.Text(text);

            textEl.FontSize(DocumentStyle.FontSizeSmall);

            if (highlight)
                textEl.Bold().FontColor(highlightColor ?? DocumentStyle.ColorAccent);
        }
    }
}