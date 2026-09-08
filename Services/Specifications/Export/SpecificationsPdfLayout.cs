// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Specifications/Export/SpecificationsPdfLayout.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02
//
//  PDF of the specification catalogue.
//
//  Landscape A4: the specification NAME is the widest column in the app
//  ("6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass" is ~50
//  characters) and wrapping it across two lines per row makes the table
//  unreadable at a glance.
//
//  A summary band goes at the top because a printed catalogue is usually
//  read by someone who wants the totals first — how many specs, how many
//  uses, what the average rate is — before scanning rows.
//
//  LAYOUT PARITY (print-layout upgrade)
//  ────────────────────────────────────
//  This layout used to be the one module that bypassed the shared print
//  chrome: it painted its own header fill from DocumentStyle.ColorTableHeader
//  with a hand-typed FontSize(8) and PaddingVertical(3)/PaddingHorizontal(4).
//  So switching Settings → PDF Layout recoloured the catalogue but never
//  re-architected it — no section numbering, no zebra, no density response,
//  and its type scale was unrelated to every other document in the app.
//  It now renders through PdfChrome like PI / JO / DN / TI / Statement,
//  so all five layouts apply to it properly.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Specifications.Export
{
    public class SpecificationsPdfLayout : IPdfLayout
    {
        private readonly IReadOnlyList<SavedSpecification> _rows;

        public SpecificationsPdfLayout(IReadOnlyList<SavedSpecification> rows)
        {
            _rows = rows ?? Array.Empty<SavedSpecification>();
        }

        public string DocumentPrefix => "SPEC";
        public string DocumentId => "Specifications";
        public string DocumentTitle => "Specifications Catalogue";
        public string CompanyName => string.Empty;
        public string Reference => string.Empty;
        public DateTime DocumentDate => DateTime.Now;

        public PageLayout PageLayout => PageLayout.A4Landscape;

        /// <summary>
        /// No letterhead: this is an internal reference list, not a
        /// customer-facing document. Returning null keeps the engine from
        /// rendering a company header block. Everything below still follows
        /// the active print layout via PdfChrome.
        /// </summary>
        public LetterheadInfo? Letterhead => null;

        public void RenderContent(ColumnDescriptor content)
        {
            var m = DocumentStyle.Metrics;

            content.Item().Text("SPECIFICATIONS CATALOGUE")
                   .FontSize(m.GrandValueFont).Bold()
                   .FontColor(DocumentStyle.ColorAccent);

            content.Item().PaddingTop(m.StackGap).Text(
                $"Generated {DateTime.Now:dd-MMM-yyyy HH:mm}")
                   .FontSize(m.CaptionFont).FontColor(DocumentStyle.ColorMuted);

            RenderSummary(content);
            RenderTable(content);
        }

        private void RenderSummary(ColumnDescriptor content)
        {
            int total = _rows.Count;
            int uses = _rows.Sum(s => s.UsageCount);
            decimal avg = total == 0 ? 0m : Math.Round(_rows.Average(s => s.PricePerSqm), 2);

            // Shared KPI strip: same component, same density response as the
            // metrics every other module prints.
            content.Item().PaddingTop(DocumentStyle.Metrics.BlockGap)
                   .Element(c => PdfChrome.KpiStrip(c,
                       ("SPECIFICATIONS", total.ToString("N0"), null),
                       ("TOTAL USES", uses.ToString("N0"), null),
                       ("AVERAGE RATE / SQM", avg.ToString("N2"), null)));
        }

        private void RenderTable(ColumnDescriptor content)
        {
            var m = DocumentStyle.Metrics;

            content.Item().PaddingTop(m.BlockGap)
                   .Element(c => PdfChrome.Section(c, "Specification register"));

            content.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(55);   // type
                    c.RelativeColumn(4);    // specification
                    c.RelativeColumn(1.2f); // colour
                    c.ConstantColumn(55);   // thickness
                    c.ConstantColumn(70);   // rate
                    c.ConstantColumn(45);   // used
                });

                table.Header(h =>
                {
                    PdfChrome.HeaderCell(h.Cell(), "TYPE", center: false);
                    PdfChrome.HeaderCell(h.Cell(), "SPECIFICATION", center: false);
                    PdfChrome.HeaderCell(h.Cell(), "COLOUR", center: false);
                    PdfChrome.HeaderCell(h.Cell(), "THK");
                    PdfChrome.HeaderCell(h.Cell(), "RATE / SQM", right: true, center: false);
                    PdfChrome.HeaderCell(h.Cell(), "USED", right: true, center: false);
                });

                // alt drives the theme's zebra striping, exactly as the
                // invoice spec tables do.
                int i = 0;
                foreach (var s in _rows)
                {
                    bool alt = (i++ % 2) == 1;

                    PdfChrome.DataCell(table.Cell(), s.GlassType ?? "", alt);
                    PdfChrome.DataCell(table.Cell(), s.SpecName ?? "", alt);
                    PdfChrome.DataCell(table.Cell(), s.Color ?? "", alt);
                    PdfChrome.DataCell(table.Cell(),
                        s.ThicknessMm > 0 ? $"{s.ThicknessMm}mm" : "", alt, center: true);
                    PdfChrome.DataCell(table.Cell(),
                        s.PricePerSqm.ToString("N2"), alt, right: true);
                    PdfChrome.DataCell(table.Cell(),
                        s.UsageCount.ToString("N0"), alt, right: true, bold: true);
                }
            });

            if (_rows.Count == 0)
            {
                content.Item().PaddingTop(DocumentStyle.Metrics.SectionGap)
                       .Text("No specifications match the current filters.")
                       .FontSize(m.MetaValueFont).FontColor(DocumentStyle.ColorMuted);
            }
        }
    }
}
