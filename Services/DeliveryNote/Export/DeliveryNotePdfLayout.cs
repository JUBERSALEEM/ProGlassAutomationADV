using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Linq;

namespace ProGlassAutomation.Services.DeliveryNote.Export
{
    /// <summary>
    /// PDF layout for a Delivery Note.
    ///
    /// Renders:
    /// - Letterhead (company from JO ref snapshot · DN ID · "Delivery Note")
    /// - Delivery logistics block (Customer · Driver · Vehicle · Date · Time)
    /// - Items table grouped by SpecGroupName
    ///   Columns: # · Glass Ref · Ordered · Prev · This DN · Balance
    /// - Totals strip (Ordered · Prev Delivered · This DN · Balance · Sequence)
    /// - Signature block (Dispatched By · Received By · Date)
    ///
    /// BUG-60: DocumentTitle now uses DeliveryNoteExportConstants.DocumentTitle
    ///         for consistency across PDF / Excel / CSV layouts.
    ///
    /// Phase 10.8a: Added Prev Delivered to totals strip.
    /// No pricing — delivery document only.
    /// </summary>
    public class DeliveryNotePdfLayout : IPdfLayout
    {
        private readonly SavedDeliveryNote _entity;
        private readonly DeliveryNoteSnapshot _snapshot;
        private readonly DnJobOrderRefSnapshot _joRef;

        public DeliveryNotePdfLayout(
            SavedDeliveryNote entity,
            DeliveryNoteSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _joRef = snapshot.JobOrderRef ?? new DnJobOrderRefSnapshot();
        }

        // ── IDocumentLayout ──────────────────────────────────────────────
        public string DocumentPrefix => "DN";
        public string DocumentId => _entity.DeliveryNoteId;

        // BUG-60: shared constant — single source of truth
        public string DocumentTitle => DeliveryNoteExportConstants.DocumentTitle;

        public string CompanyName => _joRef.CustomerName;
        public string Reference => _joRef.ProjectName;
        public DateTime DocumentDate =>
            _entity.DeliveryDate != default
                ? _entity.DeliveryDate
                : _entity.CreatedAt;

        // ── IPdfLayout ───────────────────────────────────────────────────
        public PageLayout PageLayout => PageLayout.A4Portrait;

        public LetterheadInfo? Letterhead
        {
            get
            {
                var info = new LetterheadInfo
                {
                    CompanyName = _joRef.CompanyName,
                    CompanyLocation = _joRef.CompanyLocation,
                    CompanyTrn = _joRef.CompanyTrn,
                    DocumentTitle = DocumentTitle,
                    DocumentId = DocumentId,
                    SubtitleLine = BuildSubtitle()
                };
                info.Meta.Add(("Date", _entity.DeliveryDate.ToString("dd MMM yyyy")));
                info.Meta.Add(("Status", _entity.Status ?? ""));
                info.Meta.Add(("Job Order", _entity.JobOrderId ?? ""));
                info.Meta.Add(("This DN", _entity.TotalDeliveredQty.ToString()));
                return info;
            }
        }

        public void RenderContent(ColumnDescriptor content)
        {
            // ── Delivery logistics ────────────────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "DELIVERY DETAILS"));
            content.Item().Element(RenderDeliveryDetails);

            // ── Items table ───────────────────────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "GLASS ITEMS"));

            var groups = (_entity.Items ?? new())
                .GroupBy(i => new { i.SpecGroupNumber, i.SpecGroupName })
                .OrderBy(g => g.Key.SpecGroupNumber)
                .ToList();

            if (groups.Count == 0)
            {
                content.Item()
                    .Text("No items on this delivery note.")
                    .Italic()
                    .FontColor(DocumentStyle.ColorMuted);
            }
            else
            {
                foreach (var group in groups)
                    content.Item().Element(c =>
                        RenderSpecGroupTable(c,
                            group.Key.SpecGroupName,
                            group.ToList()));

                content.Item().Element(RenderTotalsStrip);
            }

            // ── Signature block ───────────────────────────────────────────
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "SIGN-OFF"));
            content.Item().Element(RenderSignatureBlock);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SECTION RENDERERS
        // ═════════════════════════════════════════════════════════════════

        private void RenderDeliveryDetails(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(c =>
                    PdfChrome.InfoCard(c, "Customer", col =>
                    {
                        col.Item().Text(_joRef.CustomerName)
                            .FontSize(DocumentStyle.FontSizeH2).Bold();
                        if (!string.IsNullOrWhiteSpace(_joRef.ProjectName))
                            PdfChrome.LabelValue(col, "Project Name", _joRef.ProjectName);
                        if (!string.IsNullOrWhiteSpace(_joRef.ProjectReference))
                            PdfChrome.LabelValue(col, "Project Reference", _joRef.ProjectReference);
                        if (!string.IsNullOrWhiteSpace(_joRef.CompanyTrn))
                            PdfChrome.LabelValue(col, "TRN", _joRef.CompanyTrn);
                        PdfChrome.LabelValue(col, "Job order", _entity.JobOrderId);
                        if (!string.IsNullOrWhiteSpace(_entity.ProformaId))
                            PdfChrome.LabelValue(col, "PI", _entity.ProformaId);
                    }));

                row.ConstantItem(10);

                row.RelativeItem().Element(c =>
                    PdfChrome.InfoCard(c, "Logistics", col =>
                    {
                        PdfChrome.LabelValue(col, "Date",
                            _entity.DeliveryDate.ToString("dd MMM yyyy"));
                        PdfChrome.LabelValue(col, "Time", _entity.DeliveryTime);
                        PdfChrome.LabelValue(col, "Driver",
                            string.IsNullOrWhiteSpace(_entity.DriverName)
                                ? "—" : _entity.DriverName);
                        PdfChrome.LabelValue(col, "Vehicle",
                            string.IsNullOrWhiteSpace(_entity.VehicleNumber)
                                ? "—" : _entity.VehicleNumber);
                        PdfChrome.LabelValue(col, "Receiver",
                            string.IsNullOrWhiteSpace(_entity.ReceiverName)
                                ? "—" : _entity.ReceiverName);
                        PdfChrome.LabelValue(col, "Status", _entity.Status);
                        PdfChrome.LabelValue(col, "Type", _entity.DeliveryType);

                        if (_entity.IsFoc)
                        {
                            col.Item().PaddingTop(4)
                                .Background(DocumentStyle.ColorWarning)
                                .PaddingHorizontal(6).PaddingVertical(3)
                                .Text("FREE OF CHARGE (FOC)")
                                .FontSize(DocumentStyle.FontSizeSmall)
                                .Bold()
                                .FontColor(DocumentStyle.ColorWhite);
                        }

                        if (!string.IsNullOrWhiteSpace(_entity.Notes))
                            PdfChrome.LabelValue(col, "Notes", _entity.Notes);
                    }));
            });
        }

        private void RenderSpecGroupTable(
            IContainer container,
            string specGroupName,
            System.Collections.Generic.List<DeliveryNoteItem> items)
        {
            container.PaddingBottom(6).Column(col =>
            {
                col.Item().Element(c =>
                    PdfChrome.GroupBanner(c, specGroupName));

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(28);   // #
                        cols.RelativeColumn(2);    // Glass Ref
                        cols.ConstantColumn(52);   // Ordered
                        cols.ConstantColumn(46);   // Prev
                        cols.ConstantColumn(52);   // This DN
                        cols.ConstantColumn(52);   // Balance
                    });

                    t.Header(h =>
                    {
                        HeaderCell(h.Cell(), "#");
                        HeaderCell(h.Cell(), "GLASS REF");
                        HeaderCell(h.Cell(), "ORDERED");
                        HeaderCell(h.Cell(), "PREV");
                        HeaderCell(h.Cell(), "THIS DN");
                        HeaderCell(h.Cell(), "BALANCE");
                    });

                    bool alt = false;
                    foreach (var item in items.OrderBy(i => i.SerialNumber))
                    {
                        string bg = alt
                            ? DocumentStyle.ColorTableAlt
                            : DocumentStyle.ColorWhite;

                        DataCell(t.Cell(), item.SerialNumber.ToString(), bg);
                        DataCell(t.Cell(), item.GlassRef ?? "—", bg,
                            align: false);
                        DataCell(t.Cell(), item.OrderedQty.ToString(), bg);
                        DataCell(t.Cell(),
                            item.PreviouslyDeliveredQty.ToString(), bg);
                        DataCell(t.Cell(), item.DeliveredQty.ToString(), bg,
                            highlight: item.DeliveredQty > 0);
                        DataCell(t.Cell(), item.BalanceQty.ToString(), bg,
                            highlight: item.BalanceQty > 0,
                            highlightColor: DocumentStyle.ColorDanger);

                        alt = !alt;
                    }
                });

                int groupDelivered = items.Sum(i => i.DeliveredQty);
                int groupBalance = items.Sum(i => i.BalanceQty);
                col.Item().AlignRight().PaddingTop(2)
                    .Text($"Group: {groupDelivered} delivered · " +
                          $"{groupBalance} balance")
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
            });
        }

        private void RenderTotalsStrip(IContainer container)
        {
            container
                .Background(DocumentStyle.ColorTableHeader)
                .Padding(8)
                .Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("TOTAL ORDERED")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        col.Item().Text(_entity.TotalOrderedQty.ToString())
                            .FontSize(DocumentStyle.FontSizeH1).Bold()
                            .FontColor(DocumentStyle.ColorText);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PREV DELIVERED")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        col.Item()
                            .Text(_entity.TotalPreviouslyDeliveredQty.ToString())
                            .FontSize(DocumentStyle.FontSizeH1).Bold()
                            .FontColor(DocumentStyle.ColorMuted);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("THIS DN")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        col.Item().Text(_entity.TotalDeliveredQty.ToString())
                            .FontSize(DocumentStyle.FontSizeH1).Bold()
                            .FontColor(DocumentStyle.ColorAccent);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("BALANCE")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        col.Item().Text(_entity.TotalBalanceQty.ToString())
                            .FontSize(DocumentStyle.FontSizeH1).Bold()
                            .FontColor(_entity.TotalBalanceQty > 0
                                ? DocumentStyle.ColorDanger
                                : DocumentStyle.ColorSuccess);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("DN SEQUENCE")
                            .FontSize(DocumentStyle.FontSizeCaption)
                            .FontColor(DocumentStyle.ColorMuted);
                        col.Item().Text($"#{_entity.SequenceNumber} of job")
                            .FontSize(DocumentStyle.FontSizeH1).Bold()
                            .FontColor(DocumentStyle.ColorText);
                    });
                });
        }

        private static void RenderSignatureBlock(IContainer container)
        {
            container.Row(row =>
            {
                SignatureCell(row, "Dispatched By");
                row.ConstantItem(8);
                SignatureCell(row, "Received By");
                row.ConstantItem(8);
                SignatureCell(row, "Date");
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

        // ── Table cell helpers ───────────────────────────────────────────

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

            var textEl = align
                ? bordered.AlignCenter().Text(text)
                : bordered.Text(text);

            textEl.FontSize(DocumentStyle.FontSizeSmall);

            if (highlight)
                textEl.Bold().FontColor(
                    highlightColor ?? DocumentStyle.ColorAccent);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private string BuildSubtitle()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(_entity.JobOrderId))
                parts.Add($"JO: {_entity.JobOrderId}");

            parts.Add(_entity.DeliveryDate.ToString("dd MMM yyyy"));

            if (!string.IsNullOrWhiteSpace(_entity.Status))
                parts.Add(_entity.Status.ToUpperInvariant());

            return string.Join("  ·  ", parts);
        }
    }
}