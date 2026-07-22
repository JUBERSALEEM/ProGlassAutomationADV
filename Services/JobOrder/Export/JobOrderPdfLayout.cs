using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Linq;

namespace ProGlassAutomation.Services.JobOrder.Export
{
    /// <summary>
    /// PDF layout for a Job Order factory-floor production sheet.
    ///
    /// Renders:
    /// - Letterhead (company from embedded PI snapshot, JOB ID accent)
    /// - Client + Site Delivery block (2 columns)
    /// - Production instructions block (Assigned To, Cutting, Drawing, Notes)
    /// - Cut list table per spec group (W1/H1/W2/H2, qty — NO PRICING)
    /// - Signature block (Prepared / Verified / Received)
    ///
    /// Deliberately excludes:
    /// - All money fields (rate, line total, VAT, grand total)
    /// - Terms and conditions
    /// - Discount lines
    ///
    /// This is a production instruction, not a customer document.
    /// </summary>
    public class JobOrderPdfLayout : IPdfLayout
    {
        private readonly SavedJobOrder _entity;
        private readonly JobOrderSnapshot _snapshot;
        private readonly ProformaInvoiceSnapshot _invoice;

        public JobOrderPdfLayout(SavedJobOrder entity, JobOrderSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _invoice = snapshot.EmbeddedInvoice ?? new ProformaInvoiceSnapshot();
        }

        // ── IDocumentLayout ──────────────────────────────────────────
        public string DocumentPrefix => "JO";
        public string DocumentId => _entity.JobOrderId ?? _snapshot.JobOrderId;
        public string DocumentTitle => "Job Order — Production Sheet";
        public string CompanyName => _invoice.Client?.CustomerName ?? string.Empty;
        public string Reference => ResolveReference();
        public DateTime DocumentDate => _snapshot.ConvertedAt != default
                                           ? _snapshot.ConvertedAt
                                           : _entity.CreatedAt;

        // ── IPdfLayout ───────────────────────────────────────────────
        public PageLayout PageLayout => PageLayout.A4Portrait;

        public LetterheadInfo? Letterhead => new LetterheadInfo
        {
            CompanyName = _invoice.Company?.CompanyName ?? string.Empty,
            CompanyLocation = _invoice.Company?.CompanyLocation ?? string.Empty,
            CompanyTrn = _invoice.Company?.CompanyTrn ?? string.Empty,
            DocumentTitle = DocumentTitle,
            DocumentId = DocumentId,
            SubtitleLine = BuildSubtitle()
        };

        public void RenderContent(ColumnDescriptor content)
        {
            // ── Priority + Delivery + Status strip ────────────────────
            content.Item().Element(RenderStatusStrip);

            // ── Client + Site Delivery ────────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "CLIENT & DELIVERY"));
            content.Item().Element(RenderClientAndDelivery);

            // ── Production instructions ───────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "PRODUCTION INSTRUCTIONS"));
            content.Item().Element(RenderProductionInstructions);

            // ── Cut lists per spec group ──────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "CUT LIST"));

            if (_invoice.SpecGroups == null || _invoice.SpecGroups.Count == 0)
            {
                content.Item().Text("No glass items in source invoice.")
                    .Italic()
                    .FontColor(DocumentStyle.ColorMuted);
            }
            else
            {
                foreach (var group in _invoice.SpecGroups
                                              .OrderBy(g => g.GroupNumber))
                {
                    content.Item().Element(c => RenderSpecGroupTable(c, group));
                }

                content.Item().Element(RenderProductionTotals);
            }

            // ── Signature block ───────────────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "SIGN-OFF"));
            content.Item().Element(RenderSignatureBlock);
        }

        // ═════════════════════════════════════════════════════════════
        //  SECTION RENDERERS
        // ═════════════════════════════════════════════════════════════

        private void RenderStatusStrip(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("STATUS")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                    col.Item().Text(FormatStatus(_snapshot.Production?.Status))
                        .FontSize(DocumentStyle.FontSizeH2)
                        .SemiBold()
                        .FontColor(DocumentStyle.ColorAccent);
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("PRIORITY")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                    col.Item().Text(_snapshot.Production?.Priority ?? "Medium")
                        .FontSize(DocumentStyle.FontSizeH2)
                        .SemiBold()
                        .FontColor(ResolvePriorityColor(_snapshot.Production?.Priority));
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("DELIVERY DATE")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                    col.Item().Text(_snapshot.Production?.DeliveryDate.HasValue == true
                                    ? _snapshot.Production.DeliveryDate.Value.ToString("dd MMM yyyy")
                                    : "Not set")
                        .FontSize(DocumentStyle.FontSizeH2)
                        .SemiBold()
                        .FontColor(DocumentStyle.ColorText);
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("SOURCE PI")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                    col.Item().Text(_snapshot.SourceProformaId ?? "—")
                        .FontSize(DocumentStyle.FontSizeH2)
                        .SemiBold()
                        .FontColor(DocumentStyle.ColorText);
                });
            });
        }

        private void RenderClientAndDelivery(IContainer container)
        {
            container.Row(row =>
            {
                // Left — Client
                row.RelativeItem().Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                    .Padding(8)
                    .Column(col =>
                    {
                        col.Item().Text("CLIENT")
                            .FontSize(DocumentStyle.FontSizeCaption).SemiBold()
                            .FontColor(DocumentStyle.ColorAccent);

                        col.Item().PaddingTop(4).Text(_invoice.Client?.CustomerName ?? "—")
                            .FontSize(DocumentStyle.FontSizeH2).Bold();

                        if (!string.IsNullOrWhiteSpace(_invoice.Client?.CustomerTrn))
                            col.Item().Text($"TRN: {_invoice.Client.CustomerTrn}")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .FontColor(DocumentStyle.ColorMuted);

                        if (!string.IsNullOrWhiteSpace(_invoice.Project?.ProjectIdentifierName))
                            col.Item().Text($"Project: {_invoice.Project.ProjectIdentifierName}")
                                .FontSize(DocumentStyle.FontSizeSmall);

                        if (!string.IsNullOrWhiteSpace(_invoice.Project?.ProjectContractNo))
                            col.Item().Text($"Contract: {_invoice.Project.ProjectContractNo}")
                                .FontSize(DocumentStyle.FontSizeSmall);

                        if (!string.IsNullOrWhiteSpace(_invoice.Project?.LpoNumber))
                            col.Item().Text($"LPO: {_invoice.Project.LpoNumber}")
                                .FontSize(DocumentStyle.FontSizeSmall);
                    });

                row.ConstantItem(10);

                // Right — Site Delivery
                row.RelativeItem().Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                    .Padding(8)
                    .Column(col =>
                    {
                        col.Item().Text("SITE DELIVERY")
                            .FontSize(DocumentStyle.FontSizeCaption).SemiBold()
                            .FontColor(DocumentStyle.ColorAccent);

                        var prod = _snapshot.Production;
                        string contact = prod?.SiteDeliveryContact ?? string.Empty;
                        string phone = prod?.SiteDeliveryPhone ?? string.Empty;
                        string addr = prod?.SiteDeliveryAddress ?? string.Empty;

                        col.Item().PaddingTop(4).Text(
                            string.IsNullOrWhiteSpace(contact) ? "No contact set" : contact)
                            .FontSize(DocumentStyle.FontSizeH2).Bold();

                        if (!string.IsNullOrWhiteSpace(phone))
                            col.Item().Text($"📞 {phone}")
                                .FontSize(DocumentStyle.FontSizeSmall);

                        if (!string.IsNullOrWhiteSpace(addr))
                            col.Item().PaddingTop(2).Text(addr)
                                .FontSize(DocumentStyle.FontSizeSmall);

                        // Fallback to source-invoice site location if empty
                        if (string.IsNullOrWhiteSpace(addr)
                            && !string.IsNullOrWhiteSpace(_invoice.Project?.SiteDeliveryLocation))
                        {
                            col.Item().PaddingTop(2)
                                .Text($"(from PI: {_invoice.Project.SiteDeliveryLocation})")
                                .FontSize(DocumentStyle.FontSizeCaption)
                                .Italic()
                                .FontColor(DocumentStyle.ColorMuted);
                        }
                    });
            });
        }

        private void RenderProductionInstructions(IContainer container)
        {
            var prod = _snapshot.Production;
            container.Column(col =>
            {
                col.Spacing(4);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("ASSIGNED TO")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        c.Item().Text(string.IsNullOrWhiteSpace(prod?.AssignedTo)
                                      ? "Not assigned" : prod.AssignedTo)
                            .FontSize(DocumentStyle.FontSizeBody).SemiBold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("DRAWING REFERENCE")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        c.Item().Text(string.IsNullOrWhiteSpace(prod?.DrawingReference)
                                      ? "—" : prod.DrawingReference)
                            .FontSize(DocumentStyle.FontSizeBody).SemiBold();
                    });
                });

                if (!string.IsNullOrWhiteSpace(prod?.CuttingInstructions))
                {
                    col.Item().PaddingTop(6).Text("CUTTING INSTRUCTIONS")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                    col.Item().Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                        .Padding(6).Text(prod.CuttingInstructions)
                        .FontSize(DocumentStyle.FontSizeSmall);
                }

                if (!string.IsNullOrWhiteSpace(prod?.InternalNotes))
                {
                    col.Item().PaddingTop(6).Text("INTERNAL NOTES")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                    col.Item().Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                        .Padding(6).Text(prod.InternalNotes)
                        .FontSize(DocumentStyle.FontSizeSmall);
                }
            });
        }

        private void RenderSpecGroupTable(IContainer container, SpecGroupSnapshot group)
        {
            container.PaddingBottom(6).Column(col =>
            {
                // Group header
                col.Item().Background(DocumentStyle.ColorAccentLight)
                    .PaddingHorizontal(6).PaddingVertical(3)
                    .Row(row =>
                    {
                        row.RelativeItem().Text(
                            $"SPEC {group.GroupNumber}: {group.SelectedSpecName}")
                            .FontSize(DocumentStyle.FontSizeBody).Bold()
                            .FontColor(DocumentStyle.ColorAccent);

                        row.ConstantItem(120).AlignRight().Text(
                            $"Type: {group.SelectedGlassType}")
                            .FontSize(DocumentStyle.FontSizeSmall)
                            .FontColor(DocumentStyle.ColorMuted);
                    });

                // Table
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(28);   // #
                        cols.RelativeColumn(2);    // Glass Ref
                        cols.ConstantColumn(42);   // W1
                        cols.ConstantColumn(42);   // H1
                        cols.ConstantColumn(42);   // W2
                        cols.ConstantColumn(42);   // H2
                        cols.ConstantColumn(40);   // Qty
                    });

                    // Header row
                    t.Header(h =>
                    {
                        HeaderCell(h.Cell(), "#");
                        HeaderCell(h.Cell(), "GLASS REF");
                        HeaderCell(h.Cell(), "W1");
                        HeaderCell(h.Cell(), "H1");
                        HeaderCell(h.Cell(), "W2");
                        HeaderCell(h.Cell(), "H2");
                        HeaderCell(h.Cell(), "QTY");
                    });

                    // Data rows
                    if (group.ItemRows != null)
                    {
                        foreach (var r in group.ItemRows)
                        {
                            DataCell(t.Cell(), r.SerialNumber.ToString());
                            DataCell(t.Cell(), r.GlassRef ?? "—");
                            DataCell(t.Cell(), r.W1 > 0 ? r.W1.ToString() : "—");
                            DataCell(t.Cell(), r.H1 > 0 ? r.H1.ToString() : "—");
                            DataCell(t.Cell(), r.W2 > 0 ? r.W2.ToString() : "—");
                            DataCell(t.Cell(), r.H2 > 0 ? r.H2.ToString() : "—");
                            DataCell(t.Cell(), r.Qty.ToString());
                        }
                    }
                });

                // Group total pieces
                if (group.ItemRows != null && group.ItemRows.Count > 0)
                {
                    int groupPieces = group.ItemRows.Sum(r => r.Qty);
                    col.Item().PaddingTop(2).AlignRight()
                        .Text($"Group total: {groupPieces} piece(s)")
                        .FontSize(DocumentStyle.FontSizeCaption)
                        .FontColor(DocumentStyle.ColorMuted);
                }
            });
        }

        private void RenderProductionTotals(IContainer container)
        {
            int totalPieces = _invoice.SpecGroups?
                .SelectMany(g => g.ItemRows ?? Enumerable.Empty<GlassItemRowSnapshot>())
                .Sum(r => r.Qty) ?? 0;

            int totalSpecs = _invoice.SpecGroups?.Count ?? 0;

            container.Background(DocumentStyle.ColorTableHeader)
                .Padding(8)
                .Row(row =>
                {
                    row.RelativeItem().Text($"TOTAL SPECS: {totalSpecs}")
                        .FontSize(DocumentStyle.FontSizeBody).Bold();

                    row.RelativeItem().AlignRight()
                        .Text($"TOTAL PIECES: {totalPieces}")
                        .FontSize(DocumentStyle.FontSizeBody).Bold()
                        .FontColor(DocumentStyle.ColorAccent);
                });
        }

        private void RenderSignatureBlock(IContainer container)
        {
            container.Row(row =>
            {
                SignatureCell(row, "Prepared By");
                row.ConstantItem(8);
                SignatureCell(row, "Verified By");
                row.ConstantItem(8);
                SignatureCell(row, "Received By");
            });
        }

        private static void SignatureCell(RowDescriptor row, string label)
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().PaddingTop(30)
                    .BorderBottom(0.75f)
                    .BorderColor(DocumentStyle.ColorText);
                col.Item().PaddingTop(3).Text(label)
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
                col.Item().Text("Name: _____________________")
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
                col.Item().Text("Date: _______________________")
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
            });
        }

        // ── Table cell helpers ──────────────────────────────────────
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

        private static void DataCell(IContainer cell, string text)
        {
            cell.Border(0.5f)
                .BorderColor(DocumentStyle.ColorBorder)
                .PaddingHorizontal(DocumentStyle.TableCellPaddingH)
                .PaddingVertical(DocumentStyle.TableCellPaddingV)
                .AlignCenter()
                .Text(text)
                .FontSize(DocumentStyle.FontSizeSmall);
        }

        // ── Helpers ──────────────────────────────────────────────────
        private string ResolveReference()
        {
            var proj = _invoice.Project;
            if (proj == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(proj.ProjectIdentifierName))
                return proj.ProjectIdentifierName;
            if (!string.IsNullOrWhiteSpace(proj.ProjectContractNo))
                return proj.ProjectContractNo;
            if (!string.IsNullOrWhiteSpace(proj.LpoNumber))
                return proj.LpoNumber;

            return string.Empty;
        }

        private string BuildSubtitle()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(_snapshot.SourceProformaId))
                parts.Add($"From: {_snapshot.SourceProformaId}");

            if (_snapshot.Production?.DeliveryDate.HasValue == true)
                parts.Add($"Due: {_snapshot.Production.DeliveryDate.Value:dd MMM yyyy}");

            return string.Join("  ·  ", parts);
        }

        private static string FormatStatus(string? status) =>
            status switch
            {
                "InProduction" => "IN PRODUCTION",
                null or "" => "DRAFT",
                _ => status.ToUpperInvariant()
            };

        private static string ResolvePriorityColor(string? priority) =>
            priority switch
            {
                "High" => DocumentStyle.ColorDanger,
                "Medium" => DocumentStyle.ColorWarning,
                "Low" => DocumentStyle.ColorSuccess,
                _ => DocumentStyle.ColorText
            };
    }
}