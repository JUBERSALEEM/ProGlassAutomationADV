using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.TaxInvoice.Pdf
{
    /// <summary>
    /// IPdfLayout implementation for Tax Invoice.
    ///
    /// CHAT 17 CRITICAL FIXES:
    ///   1. RenderFinancialSummary: removed .Width(320) fixed width.
    ///      Replaced with proper Row(2/1 split) layout — no measurement
    ///      conflict when this document is embedded/merged into a
    ///      Statement PDF pipeline.
    ///   2. RenderSignatureBlocks: removed .Height(1) on signature
    ///      underlines — BorderBottom(1) alone renders the line.
    ///      .Height(1) inside a Row causes measurement pass conflicts.
    ///
    /// UAE VAT RULES:
    ///   VAT = 5% applied to AfterDiscount
    ///   TRN = 15 digits, printed prominently
    ///   Currency = AED only
    /// </summary>
    public class TaxInvoicePdfDocument : IPdfLayout
    {
        // ── Brand colors (match app palette) ─────────────────────────────
        private const string ACCENT = "#4338CA";
        private const string ACCENT_DARK = "#3730A3";
        private const string DARK = "#0F172A";
        private const string MUTED = "#475569";
        private const string LINE = "#CBD5E1";
        private const string LIGHT_BG = "#F1F5F9";
        private const string TABLE_HDR = "#1E293B";
        private const string GREEN = "#047857";
        private const string AMBER = "#B45309";
        private const string RED = "#B91C1C";
        private const string WHITE = "#FFFFFF";

        private readonly SavedTaxInvoice _entity;
        private readonly TaxInvoiceSnapshot _snapshot;
        private readonly TaxInvoiceRenderCtx _ctx;

        // ── Constructor ───────────────────────────────────────────────────

        public TaxInvoicePdfDocument(
            SavedTaxInvoice entity,
            TaxInvoiceSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _ctx = BuildRenderContext(entity, snapshot);
        }

        // ── IDocumentLayout ───────────────────────────────────────────────

        public string DocumentPrefix => "INV";
        public string DocumentId => _entity.InvoiceNumber;
        public string DocumentTitle => "TAX INVOICE";
        public string CompanyName => _entity.CustomerName;
        public string Reference => _entity.ProjectName;
        public DateTime DocumentDate => _entity.IssueDate;

        // ── IPdfLayout ────────────────────────────────────────────────────

        public PageLayout PageLayout => PageLayout.A4Portrait;

        public LetterheadInfo? Letterhead => new LetterheadInfo
        {
            CompanyName = _ctx.CompanyName,
            CompanyLocation = _ctx.CompanyLocation,
            CompanyTrn = _ctx.CompanyTrn,
            DocumentTitle = DocumentTitle,
            DocumentId = DocumentId
        };

        public void RenderContent(ColumnDescriptor content)
        {
            // 1. Full-width invoice metadata strip
            content.Item().Element(RenderInvoiceBadge);

            content.Item().PaddingTop(6).PaddingBottom(6)
                .LineHorizontal(1).LineColor(LINE);

            // 2. Supplier | Customer blocks
            content.Item().Row(row =>
            {
                row.RelativeItem().Element(RenderSupplierBlock);
                row.ConstantItem(16);
                row.RelativeItem().Element(RenderCustomerBlock);
            });

            content.Item().PaddingTop(8).PaddingBottom(8)
    .LineHorizontal(1).LineColor(LINE);

            // 2b. Project details — dedicated section
            content.Item().Element(RenderProjectBlock);

            content.Item().PaddingTop(6).PaddingBottom(6)
                .LineHorizontal(1).LineColor(LINE);

            // 3. Invoice references bar
            content.Item().Element(RenderReferencesBar);

            content.Item().PaddingTop(6).PaddingBottom(6)
                .LineHorizontal(1).LineColor(LINE);

            // 4. Spec group tables
            foreach (var group in _ctx.SpecGroups)
                content.Item().Element(c => RenderSpecTable(c, group));

            if (_ctx.SpecGroups.Any())
                content.Item().Element(RenderGlassTotalsRow);

            // 5. Financial summary
            content.Item().Element(RenderFinancialSummary);

            // 6. Amount in words
            content.Item().PaddingTop(8).Element(RenderAmountInWords);

            // 7. Bank details
            content.Item().PaddingTop(10).Element(RenderBankDetails);

            // 8. Signature blocks
            content.Item().PaddingTop(12).Element(RenderSignatureBlocks);

            // 9. Footer note
            content.Item().PaddingTop(8)
                .BorderTop(1).BorderColor(LINE).PaddingTop(6)
                .Text("This is a computer-generated Tax Invoice. " +
                      "No signature is required for electronic copies.")
                .FontSize(8).Italic().FontColor(MUTED);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SECTION RENDERERS
        // ════════════════════════════════════════════════════════════════════

        private void RenderInvoiceBadge(IContainer container)
        {
            container
                .Border(1).BorderColor(LINE)
                .Background(LIGHT_BG)
                .Column(strip =>
                {
                    strip.Item()
                        .Background(ACCENT)
                        .PaddingHorizontal(12).PaddingVertical(6)
                        .Row(titleRow =>
                        {
                            titleRow.RelativeItem()
                                .Text("TAX INVOICE")
                                .FontSize(13).Bold().FontColor(WHITE);

                            titleRow.AutoItem()
                                .AlignRight()
                                .Text(_ctx.InvoiceNumber)
                                .FontSize(12).Bold().FontColor(WHITE);
                        });

                    strip.Item()
                        .PaddingHorizontal(12).PaddingVertical(8)
                        .Row(metaRow =>
                        {
                            metaRow.RelativeItem().Column(c =>
                            {
                                c.Item().Text("INVOICE NO")
                                    .FontSize(8).Bold().FontColor(MUTED);
                                c.Item().PaddingTop(2)
                                    .Text(_ctx.InvoiceNumber)
                                    .FontSize(10).SemiBold().FontColor(DARK);
                            });

                            metaRow.RelativeItem().Column(c =>
                            {
                                c.Item().Text("ISSUE DATE")
                                    .FontSize(8).Bold().FontColor(MUTED);
                                c.Item().PaddingTop(2)
                                    .Text(_ctx.IssueDateDisplay)
                                    .FontSize(10).SemiBold().FontColor(DARK);
                            });

                            metaRow.RelativeItem().Column(c =>
                            {
                                c.Item().Text("DUE DATE")
                                    .FontSize(8).Bold().FontColor(MUTED);
                                c.Item().PaddingTop(2)
                                    .Text(_ctx.DueDateDisplay)
                                    .FontSize(10).SemiBold()
                                    .FontColor(_ctx.IsOverdue ? RED : DARK);
                            });

                            metaRow.RelativeItem().Column(c =>
                            {
                                c.Item().Text("STATUS")
                                    .FontSize(8).Bold().FontColor(MUTED);
                                c.Item().PaddingTop(2)
                                    .Text(_ctx.Status)
                                    .FontSize(10).SemiBold()
                                    .FontColor(GetStatusColor(_ctx.Status));
                            });
                        });

                    if (_ctx.IsPartial)
                    {
                        strip.Item()
                            .Background(AMBER)
                            .PaddingHorizontal(12).PaddingVertical(5)
                            .AlignCenter()
                            .Text($"⚠  PARTIAL INVOICE — SEQUENCE ({_ctx.SequenceLetter})  " +
                                  $"·  This invoice covers a portion of the total order.")
                            .FontSize(9).Bold().FontColor(WHITE);
                    }
                });
        }

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

        private void RenderSupplierBlock(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("FROM / SUPPLIER:")
                    .FontSize(10).Bold().FontColor(DARK);

                AddLabelValue(col, "Company:", _ctx.CompanyName);
                AddLabelValue(col, "TRN:", _ctx.CompanyTrn);
                AddLabelValue(col, "Location:", _ctx.CompanyLocation);
                AddLabelValue(col, "Phone:", _ctx.CompanyPhone);
                AddLabelValue(col, "Email:", _ctx.CompanyEmail);
            });
        }

        private void RenderCustomerBlock(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("BILL TO / CUSTOMER:")
                    .FontSize(10).Bold().FontColor(DARK);

                AddLabelValue(col, "Customer:", _ctx.CustomerName);
                AddLabelValue(col, "Customer TRN:", _ctx.CustomerTrn);
                AddLabelValue(col, "Contact:", _ctx.ContactPerson);

                if (!string.IsNullOrWhiteSpace(_ctx.CustomerAddress))
                    AddLabelValue(col, "Address:", _ctx.CustomerAddress);
            });
        }

        /// <summary>
        /// CHAT 20: Dedicated project section — separated from customer block
        /// for professional layout. Shows all 4 project fields in a bordered
        /// card with indigo accent header.
        /// </summary>
        private void RenderProjectBlock(IContainer container)
        {
            // Skip entire section if no project data at all
            bool hasAnyProjectData =
                !string.IsNullOrWhiteSpace(_ctx.ProjectIdentifierName) ||
                !string.IsNullOrWhiteSpace(_ctx.ProjectContractNo) ||
                !string.IsNullOrWhiteSpace(_ctx.ProjectReference) ||
                !string.IsNullOrWhiteSpace(_ctx.SiteDeliveryLocation);

            if (!hasAnyProjectData) return;

            container
                .Border(1).BorderColor(LINE)
                .Column(col =>
                {
                    // Section header — indigo accent bar
                    col.Item()
                        .Background(ACCENT)
                        .PaddingHorizontal(10).PaddingVertical(5)
                        .Text("PROJECT DETAILS")
                        .FontSize(9).Bold().FontColor(WHITE);

                    // Project fields — 2-column layout for balanced spacing
                    col.Item()
                        .Background(LIGHT_BG)
                        .Padding(10)
                        .Row(row =>
                        {
                            // Left column
                            row.RelativeItem().Column(left =>
                            {
                                if (!string.IsNullOrWhiteSpace(_ctx.ProjectIdentifierName))
                                    AddLabelValue(left, "Project Identifier Name:",
                                        _ctx.ProjectIdentifierName);

                                if (!string.IsNullOrWhiteSpace(_ctx.ProjectContractNo))
                                    AddLabelValue(left, "Project Name / No:",
                                        _ctx.ProjectContractNo);
                            });

                            row.ConstantItem(20);

                            // Right column
                            row.RelativeItem().Column(right =>
                            {
                                if (!string.IsNullOrWhiteSpace(_ctx.ProjectReference))
                                    AddLabelValue(right, "Project Reference:",
                                        _ctx.ProjectReference);

                                if (!string.IsNullOrWhiteSpace(_ctx.SiteDeliveryLocation))
                                    AddLabelValue(right, "Site / Delivery Location:",
                                        _ctx.SiteDeliveryLocation);
                            });
                        });
                });
        }

        private void RenderReferencesBar(IContainer container)
        {
            container
                .Background(LIGHT_BG)
                .Padding(8)
                .Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PI REFERENCE")
                            .FontSize(8).Bold().FontColor(MUTED);
                        col.Item().Text(_ctx.ProformaId)
                            .FontSize(10).SemiBold().FontColor(DARK);
                    });

                    if (!string.IsNullOrWhiteSpace(_ctx.DeliveryNoteRef))
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("DN REFERENCE")
                                .FontSize(8).Bold().FontColor(MUTED);
                            col.Item().Text(_ctx.DeliveryNoteRef)
                                .FontSize(10).SemiBold().FontColor(DARK);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(_ctx.PurchaseOrderNumber))
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("CUSTOMER PO")
                                .FontSize(8).Bold().FontColor(MUTED);
                            col.Item().Text(_ctx.PurchaseOrderNumber)
                                .FontSize(10).SemiBold().FontColor(DARK);
                        });
                    }

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PAYMENT TERMS")
                            .FontSize(8).Bold().FontColor(MUTED);
                        col.Item().Text(_ctx.PaymentTerms)
                            .FontSize(10).SemiBold().FontColor(DARK);
                    });
                });
        }

        private void RenderSpecTable(IContainer container, InvRenderSpecGroup group)
        {
            container.PaddingBottom(10).Column(col =>
            {
                col.Item()
                    .Background(ACCENT)
                    .PaddingHorizontal(8).PaddingVertical(5)
                    .Text($"SPECIFICATION {group.GroupNumber}: {group.GroupName}")
                    .FontSize(10).Bold().FontColor(Colors.White);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(24);
                        c.RelativeColumn(1.6f);
                        c.ConstantColumn(36);
                        c.ConstantColumn(36);
                        c.ConstantColumn(32);
                        c.ConstantColumn(32);
                        c.ConstantColumn(30);
                        c.RelativeColumn(0.75f);
                        c.RelativeColumn(0.85f);
                        c.RelativeColumn(0.80f);
                        c.RelativeColumn(0.95f);
                    });

                    table.Header(header =>
                    {
                        foreach (var h in new[]
                        {
                            "#", "GLASS REF", "W1", "H1", "W2", "H2",
                            "QTY", "SQM", "T.SQM", "RATE", "VALUE"
                        })
                        {
                            header.Cell()
                                .Background(TABLE_HDR)
                                .PaddingHorizontal(4).PaddingVertical(4)
                                .Text(h).FontSize(8).Bold()
                                .FontColor(Colors.White);
                        }
                    });

                    bool altRow = false;
                    foreach (var row in group.Rows)
                    {
                        string rowBg = altRow ? LIGHT_BG : WHITE;
                        altRow = !altRow;

                        AddTableCell(table, row.Serial.ToString(),
                            rowBg, center: true);
                        AddTableCell(table, row.GlassRef, rowBg);
                        AddTableCellRight(table, row.W1.ToString(), rowBg);
                        AddTableCellRight(table, row.H1.ToString(), rowBg);
                        AddTableCellRight(table,
                            row.W2 > 0 ? row.W2.ToString() : "—", rowBg);
                        AddTableCellRight(table,
                            row.H2 > 0 ? row.H2.ToString() : "—", rowBg);
                        AddTableCellRight(table, row.Qty.ToString(), rowBg,
                            bold: true);
                        AddTableCellRight(table,
                            row.Sqm.ToString("0.000"), rowBg);
                        AddTableCellRight(table,
                            row.TotalSqm.ToString("0.000"), rowBg,
                            bold: true);
                        AddTableCellRight(table,
                            $"AED {row.Rate:N2}", rowBg);
                        AddTableCellRight(table,
                            $"AED {row.Value:N2}", rowBg,
                            bold: true, color: GREEN);
                    }
                });

                col.Item()
                    .Background(LIGHT_BG)
                    .BorderBottom(1).BorderColor(LINE)
                    .Table(footer =>
                    {
                        footer.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.RelativeColumn(1.6f);
                            c.ConstantColumn(36);
                            c.ConstantColumn(36);
                            c.ConstantColumn(32);
                            c.ConstantColumn(32);
                            c.ConstantColumn(30);
                            c.RelativeColumn(0.75f);
                            c.RelativeColumn(0.85f);
                            c.RelativeColumn(0.80f);
                            c.RelativeColumn(0.95f);
                        });

                        footer.Cell().ColumnSpan(6)
                            .PaddingHorizontal(6).PaddingVertical(6)
                            .Text("GROUP TOTAL:")
                            .FontSize(9).Bold().FontColor(ACCENT);

                        footer.Cell()
                            .PaddingHorizontal(4).PaddingVertical(6)
                            .AlignRight()
                            .Text(group.TotalQty.ToString())
                            .FontSize(9).Bold().FontColor(ACCENT);

                        footer.Cell()
                            .PaddingHorizontal(4).PaddingVertical(6)
                            .Text("");

                        footer.Cell()
                            .PaddingHorizontal(4).PaddingVertical(6)
                            .AlignRight()
                            .Text(group.TotalSqm.ToString("0.000"))
                            .FontSize(9).Bold().FontColor(ACCENT);

                        footer.Cell()
                            .PaddingHorizontal(4).PaddingVertical(6)
                            .Text("");

                        footer.Cell()
                            .PaddingHorizontal(4).PaddingVertical(6)
                            .AlignRight()
                            .Text($"AED {group.TotalValue:N2}")
                            .FontSize(10).Bold().FontColor(GREEN);
                    });
            });
        }

        private void RenderGlassTotalsRow(IContainer container)
        {
            int totalQty = _ctx.SpecGroups.Sum(g => g.TotalQty);
            decimal totalSqm = _ctx.SpecGroups.Sum(g => g.TotalSqm);
            decimal totalValue = _ctx.SpecGroups.Sum(g => g.TotalValue);

            container.PaddingBottom(6)
                .Background(ACCENT)
                .Table(footer =>
                {
                    footer.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(24);
                        c.RelativeColumn(1.6f);
                        c.ConstantColumn(36);
                        c.ConstantColumn(36);
                        c.ConstantColumn(32);
                        c.ConstantColumn(32);
                        c.ConstantColumn(30);
                        c.RelativeColumn(0.75f);
                        c.RelativeColumn(0.85f);
                        c.RelativeColumn(0.80f);
                        c.RelativeColumn(0.95f);
                    });

                    footer.Cell().ColumnSpan(6)
                        .PaddingHorizontal(6).PaddingVertical(7)
                        .Text("GLASS ITEMS TOTAL:")
                        .FontSize(10).Bold().FontColor(Colors.White);

                    footer.Cell()
                        .PaddingHorizontal(4).PaddingVertical(7)
                        .AlignRight()
                        .Text(totalQty.ToString())
                        .FontSize(10).Bold().FontColor(Colors.White);

                    footer.Cell()
                        .PaddingHorizontal(4).PaddingVertical(7)
                        .Text("");

                    footer.Cell()
                        .PaddingHorizontal(4).PaddingVertical(7)
                        .AlignRight()
                        .Text(totalSqm.ToString("0.000"))
                        .FontSize(10).Bold().FontColor(Colors.White);

                    footer.Cell()
                        .PaddingHorizontal(4).PaddingVertical(7)
                        .Text("");

                    footer.Cell()
                        .PaddingHorizontal(4).PaddingVertical(7)
                        .AlignRight()
                        .Text($"AED {totalValue:N2}")
                        .FontSize(11).Bold().FontColor(Colors.White);
                });
        }

        // ═════════════════════════════════════════════════════════════════
        //  FINANCIAL SUMMARY — CHAT 17 FIX
        //  Removed .Width(320) fixed constraint.
        //  Now uses Row(2 relative parts) — flex layout, no crash.
        // ═════════════════════════════════════════════════════════════════

        private void RenderFinancialSummary(IContainer container)
        {
            container
                .PaddingTop(10)
                .BorderTop(1).BorderColor(LINE)
                .PaddingTop(10)
                .Row(outer =>
                {
                    // Left spacer — pushes summary to right side
                    outer.RelativeItem(1);

                    // Right side — summary content (flex, not fixed width)
                    outer.RelativeItem(1).Column(col =>
                    {
                        AddSummaryRow(col, "Subtotal:",
                            $"AED {_ctx.Subtotal:N2}");

                        if (_ctx.DiscountAmount > 0)
                        {
                            AddSummaryRow(col,
                                $"Discount ({_ctx.DiscountPercent:0.##}%):",
                                $"- AED {_ctx.DiscountAmount:N2}",
                                color: AMBER);
                        }

                        AddSummaryRow(col, "After Discount:",
                            $"AED {_ctx.AfterDiscount:N2}");

                        col.Item().PaddingVertical(3)
                            .LineHorizontal(1).LineColor(LINE);

                        AddSummaryRow(col, "UAE VAT (5%):",
                            $"AED {_ctx.VatAmount:N2}",
                            size: 10);

                        col.Item().PaddingTop(6)
                            .Background(ACCENT)
                            .PaddingHorizontal(12).PaddingVertical(8)
                            .Row(r =>
                            {
                                r.RelativeItem()
                                    .Text("GRAND TOTAL (VAT INCLUSIVE):")
                                    .FontSize(10).Bold().FontColor(Colors.White);
                                r.AutoItem()
                                    .Text($"AED {_ctx.GrandTotal:N2}")
                                    .FontSize(12).Bold().FontColor(Colors.White);
                            });

                        if (_ctx.IsPartial && _ctx.PreviouslyInvoiced > 0)
                        {
                            col.Item().PaddingTop(6).Column(partial =>
                            {
                                AddSummaryRow(partial,
                                    "Previously Invoiced:",
                                    $"AED {_ctx.PreviouslyInvoiced:N2}",
                                    color: MUTED);

                                AddSummaryRow(partial,
                                    "Remaining Balance:",
                                    $"AED {_ctx.RemainingBalance:N2}",
                                    bold: true, color: AMBER);
                            });
                        }
                    });
                });
        }

        private void RenderAmountInWords(IContainer container)
        {
            container
                .Background(LIGHT_BG)
                .Border(1).BorderColor(LINE)
                .Padding(10)
                .Column(col =>
                {
                    col.Item().Text("AMOUNT IN WORDS:")
                        .FontSize(9).Bold().FontColor(MUTED);

                    col.Item().PaddingTop(3)
                        .Text(TaxInvoiceMapper.AmountToWords(_ctx.GrandTotal))
                        .FontSize(11).Bold().Italic().FontColor(DARK);
                });
        }

        private void RenderBankDetails(IContainer container)
        {
            if (string.IsNullOrWhiteSpace(_ctx.BankName)
                && string.IsNullOrWhiteSpace(_ctx.Iban))
                return;

            container.Column(col =>
            {
                col.Item()
                    .Background(ACCENT)
                    .PaddingHorizontal(8).PaddingVertical(5)
                    .Text("BANK / PAYMENT DETAILS")
                    .FontSize(10).Bold().FontColor(Colors.White);

                col.Item()
                    .Background(LIGHT_BG)
                    .Border(1).BorderColor(LINE)
                    .Padding(10)
                    .Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            AddLabelValue(left, "Bank Name:",
                                _ctx.BankName);
                            AddLabelValue(left, "Account Number:",
                                _ctx.BankAccountNumber);
                        });

                        row.ConstantItem(20);

                        row.RelativeItem().Column(right =>
                        {
                            AddLabelValue(right, "IBAN:", _ctx.Iban);
                            AddLabelValue(right, "SWIFT/BIC:",
                                _ctx.SwiftCode);
                        });
                    });

                if (!string.IsNullOrWhiteSpace(_ctx.PaymentNotes))
                {
                    col.Item()
                        .PaddingTop(4).PaddingHorizontal(4)
                        .Text(_ctx.PaymentNotes)
                        .FontSize(9).Italic().FontColor(MUTED);
                }
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  SIGNATURE BLOCKS — CHAT 17 FIX
        //  Removed .Height(1) on signature underlines.
        //  BorderBottom(1) alone renders the line without measurement crash.
        //  Added Text(" ") as content since BorderBottom needs content
        //  to have measurable height.
        // ═════════════════════════════════════════════════════════════════

        private void RenderSignatureBlocks(IContainer container)
        {
            container.Row(row =>
            {
                // Prepared by
                row.RelativeItem()
                    .Border(1).BorderColor(LINE).Padding(10)
                    .Column(col =>
                    {
                        col.Item().Text("PREPARED BY")
                            .FontSize(9).Bold().FontColor(ACCENT);

                        col.Item().PaddingTop(4)
                            .Text(string.IsNullOrWhiteSpace(_ctx.PreparedByName)
                                ? "—" : _ctx.PreparedByName)
                            .FontSize(10).SemiBold().FontColor(DARK);

                        if (!string.IsNullOrWhiteSpace(_ctx.PreparedByTitle))
                            col.Item().PaddingTop(2)
                                .Text(_ctx.PreparedByTitle)
                                .FontSize(9).FontColor(MUTED);

                        col.Item().PaddingTop(18)
                            .BorderBottom(1).BorderColor(LINE)
                            .Text("Signature").FontSize(8).FontColor(MUTED);
                    });

                row.ConstantItem(16);

                // Customer approval
                row.RelativeItem()
                    .Border(1).BorderColor(LINE).Padding(10)
                    .Column(col =>
                    {
                        col.Item().Text("CUSTOMER ACKNOWLEDGEMENT")
                            .FontSize(9).Bold().FontColor(ACCENT);

                        // FIX: Removed .Height(1) — replaced with Text(" ")
                        // which gives BorderBottom something measurable.
                        col.Item().PaddingTop(4).Row(r =>
                        {
                            r.AutoItem().Text("Name: ")
                                .FontSize(9).FontColor(MUTED);
                            r.RelativeItem().PaddingLeft(6)
                                .BorderBottom(1).BorderColor(LINE)
                                .Text(" ").FontSize(9);
                        });

                        col.Item().PaddingTop(6).Row(r =>
                        {
                            r.AutoItem().Text("Signature: ")
                                .FontSize(9).FontColor(MUTED);
                            r.RelativeItem().PaddingLeft(6)
                                .BorderBottom(1).BorderColor(LINE)
                                .Text(" ").FontSize(9);
                        });

                        col.Item().PaddingTop(6).Row(r =>
                        {
                            r.AutoItem().Text("Date: ")
                                .FontSize(9).FontColor(MUTED);
                            r.RelativeItem().PaddingLeft(6)
                                .BorderBottom(1).BorderColor(LINE)
                                .Text(" ").FontSize(9);
                        });

                        col.Item().PaddingTop(6)
                            .Text("Stamp / Company Seal")
                            .FontSize(8).FontColor(MUTED);
                    });
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  SNAPSHOT → RENDER CONTEXT
        // ════════════════════════════════════════════════════════════════════

        private static TaxInvoiceRenderCtx BuildRenderContext(
            SavedTaxInvoice entity,
            TaxInvoiceSnapshot snap)
        {
            var ctx = new TaxInvoiceRenderCtx
            {
                InvoiceNumber = entity.InvoiceNumber ?? "",
                IssueDateDisplay = entity.IssueDate == default
                    ? "—" : entity.IssueDate.ToString("dd MMM yyyy"),
                DueDateDisplay = entity.DueDate == default
                    ? "—" : entity.DueDate.ToString("dd MMM yyyy"),
                IsOverdue = entity.DueDate != default
                    && entity.DueDate < DateTime.Today
                    && entity.Status != "Paid"
                    && entity.Status != "Cancelled"
                    && entity.Status != "Void",
                Status = entity.Status ?? "",
                IsPartial = string.Equals(entity.InvoiceType,
                    "Partial", StringComparison.OrdinalIgnoreCase),
                SequenceLetter = entity.SequenceLetter ?? "",

                CompanyName = snap.Company?.CompanyName ?? "",
                CompanyTrn = snap.Company?.CompanyTrn ?? "",
                CompanyLocation = snap.Company?.CompanyLocation ?? "",
                CompanyPhone = snap.Company?.CompanyPhone ?? "",
                CompanyEmail = snap.Company?.CompanyEmail ?? "",

                CustomerName = snap.Customer?.CustomerName ?? "",
                CustomerTrn = snap.Customer?.CustomerTrn ?? "",
                ContactPerson = snap.Customer?.ContactPerson ?? "",
                CustomerAddress = snap.Customer?.CustomerAddress ?? "",

                // CHAT 20: Read new fields first, fall back to legacy fields
                // for invoices saved before the field rename.
#pragma warning disable CS0612
                ProjectIdentifierName = !string.IsNullOrWhiteSpace(snap.Project?.ProjectIdentifierName)
                    ? snap.Project.ProjectIdentifierName
                    : snap.Project?.ProjectName ?? "",

                ProjectContractNo = snap.Project?.ProjectContractNo ?? "",
                ProjectReference = snap.Project?.ProjectReference ?? "",

                SiteDeliveryLocation = !string.IsNullOrWhiteSpace(snap.Project?.SiteDeliveryLocation)
                    ? snap.Project.SiteDeliveryLocation
                    : snap.Project?.ProjectLocation ?? "",
#pragma warning restore CS0612

                ProformaId = snap.Header?.ProformaId ?? "",
                DeliveryNoteRef = snap.Header?.DeliveryNoteReference ?? "",
                PurchaseOrderNumber = snap.Header?.PurchaseOrderNumber ?? "",
                PaymentTerms = snap.Bank?.PaymentTerms ?? "",

                Subtotal = snap.Financial?.Subtotal ?? 0,
                DiscountPercent = snap.Financial?.DiscountPercent ?? 0,
                DiscountAmount = snap.Financial?.DiscountAmount ?? 0,
                AfterDiscount = snap.Financial?.AfterDiscount ?? 0,
                VatAmount = snap.Financial?.VatAmount ?? 0,
                GrandTotal = snap.Financial?.GrandTotal ?? 0,
                PreviouslyInvoiced = snap.Financial?.PreviouslyInvoicedTotal ?? 0,
                RemainingBalance = snap.Financial?.RemainingBalance ?? 0,

                BankName = snap.Bank?.BankName ?? "",
                BankAccountNumber = snap.Bank?.BankAccountNumber ?? "",
                Iban = snap.Bank?.Iban ?? "",
                SwiftCode = snap.Bank?.SwiftCode ?? "",
                PaymentNotes = snap.Bank?.PaymentNotes ?? "",

                PreparedByName = snap.PreparedBy?.Name ?? "",
                PreparedByTitle = snap.PreparedBy?.Title ?? ""
            };

            if (snap.SpecGroups != null)
            {
                foreach (var g in snap.SpecGroups)
                {
                    var group = new InvRenderSpecGroup
                    {
                        GroupNumber = g.GroupNumber,
                        GroupName = !string.IsNullOrWhiteSpace(g.SpecName)
                            ? g.SpecName
                            : (!string.IsNullOrWhiteSpace(g.GroupName)
                                ? g.GroupName
                                : $"Spec Group {g.GroupNumber}")
                    };

                    if (g.Rows != null)
                    {
                        foreach (var r in g.Rows)
                        {
                            decimal sqm = SqmCalculator.CalculatePanel(r.W1, r.H1);
                            decimal totalSqm = Math.Round(sqm * r.Qty, 3);
                            decimal value = Math.Round(r.Rate * r.Qty * sqm, 2);

                            group.Rows.Add(new InvRenderRow
                            {
                                Serial = r.SerialNumber,
                                GlassRef = r.GlassRef ?? "",
                                W1 = r.W1,
                                H1 = r.H1,
                                W2 = r.W2,
                                H2 = r.H2,
                                Qty = r.Qty,
                                Sqm = sqm,
                                TotalSqm = totalSqm,
                                Rate = r.Rate,
                                Value = value
                            });

                            group.TotalQty += r.Qty;
                            group.TotalSqm += totalSqm;
                            group.TotalValue += value;
                        }
                    }

                    group.TotalSqm = Math.Round(group.TotalSqm, 3);
                    group.TotalValue = Math.Round(group.TotalValue, 2);
                    ctx.SpecGroups.Add(group);
                }
            }

            return ctx;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TABLE CELL HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static void AddLabelValue(
            ColumnDescriptor col, string label, string value)
        {
            col.Item().PaddingTop(1).Row(r =>
            {
                r.AutoItem()
                    .Text($"{label} ")
                    .FontSize(9).FontColor(MUTED);
                r.RelativeItem()
                    .Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(9).SemiBold().FontColor(DARK);
            });
        }

        private static void AddSummaryRow(
            ColumnDescriptor col,
            string label,
            string value,
            bool bold = false,
            int size = 9,
            string? color = null)
        {
            col.Item().PaddingBottom(3).Row(r =>
            {
                var lbl = r.RelativeItem().Text(label).FontSize(size);
                if (bold) lbl.Bold();
                if (color != null) lbl.FontColor(color);

                var val = r.AutoItem().Text(value).FontSize(size);
                if (bold) val.Bold();
                if (color != null) val.FontColor(color);
            });
        }

        private static void AddTableCell(
            TableDescriptor table,
            string text,
            string bg,
            bool center = false,
            bool bold = false,
            string? color = null)
        {
            var cell = table.Cell()
                .Background(bg)
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(4).PaddingVertical(3);

            var txt = (center ? cell.AlignCenter() : cell).Text(text).FontSize(8);
            if (bold) txt.Bold();
            if (color != null) txt.FontColor(color);
        }

        private static void AddTableCellRight(
            TableDescriptor table,
            string text,
            string bg,
            bool bold = false,
            string? color = null)
        {
            var cell = table.Cell()
                .Background(bg)
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(4).PaddingVertical(3)
                .AlignRight();

            var txt = cell.Text(text).FontSize(8);
            if (bold) txt.Bold();
            if (color != null) txt.FontColor(color);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE RENDER CONTEXT DTOs
        // ════════════════════════════════════════════════════════════════════

        private sealed class TaxInvoiceRenderCtx
        {
            public string InvoiceNumber { get; set; } = "";
            public string IssueDateDisplay { get; set; } = "";
            public string DueDateDisplay { get; set; } = "";
            public bool IsOverdue { get; set; }
            public string Status { get; set; } = "";
            public bool IsPartial { get; set; }
            public string SequenceLetter { get; set; } = "";

            public string CompanyName { get; set; } = "";
            public string CompanyTrn { get; set; } = "";
            public string CompanyLocation { get; set; } = "";
            public string CompanyPhone { get; set; } = "";
            public string CompanyEmail { get; set; } = "";

            public string CustomerName { get; set; } = "";
            public string CustomerTrn { get; set; } = "";
            public string ContactPerson { get; set; } = "";
            public string CustomerAddress { get; set; } = "";

            public string ProjectIdentifierName { get; set; } = "";
            public string ProjectContractNo { get; set; } = "";
            public string ProjectReference { get; set; } = "";
            public string SiteDeliveryLocation { get; set; } = "";

            public string ProformaId { get; set; } = "";
            public string DeliveryNoteRef { get; set; } = "";
            public string PurchaseOrderNumber { get; set; } = "";
            public string PaymentTerms { get; set; } = "";

            public decimal Subtotal { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal AfterDiscount { get; set; }
            public decimal VatAmount { get; set; }
            public decimal GrandTotal { get; set; }
            public decimal PreviouslyInvoiced { get; set; }
            public decimal RemainingBalance { get; set; }

            public string BankName { get; set; } = "";
            public string BankAccountNumber { get; set; } = "";
            public string Iban { get; set; } = "";
            public string SwiftCode { get; set; } = "";
            public string PaymentNotes { get; set; } = "";

            public string PreparedByName { get; set; } = "";
            public string PreparedByTitle { get; set; } = "";

            public List<InvRenderSpecGroup> SpecGroups { get; } = new();
        }

        private sealed class InvRenderSpecGroup
        {
            public int GroupNumber { get; set; }
            public string GroupName { get; set; } = "";
            public int TotalQty { get; set; }
            public decimal TotalSqm { get; set; }
            public decimal TotalValue { get; set; }
            public List<InvRenderRow> Rows { get; } = new();
        }

        private sealed class InvRenderRow
        {
            public int Serial { get; set; }
            public string GlassRef { get; set; } = "";
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public int Qty { get; set; }
            public decimal Sqm { get; set; }
            public decimal TotalSqm { get; set; }
            public decimal Rate { get; set; }
            public decimal Value { get; set; }
        }
    }
}