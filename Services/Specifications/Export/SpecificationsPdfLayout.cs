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
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Pdf;
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
        /// rendering a company header block.
        /// </summary>
        public LetterheadInfo? Letterhead => null;

        public void RenderContent(ColumnDescriptor content)
        {
            content.Item().Text("SPECIFICATIONS CATALOGUE")
                   .FontSize(16).Bold();

            content.Item().PaddingTop(2).Text(
                $"Generated {DateTime.Now:dd-MMM-yyyy HH:mm}")
                   .FontSize(8).FontColor(DocumentStyle.ColorMuted);

            RenderSummary(content);
            RenderTable(content);
        }

        private void RenderSummary(ColumnDescriptor content)
        {
            int total = _rows.Count;
            int uses = _rows.Sum(s => s.UsageCount);
            decimal avg = total == 0 ? 0m : Math.Round(_rows.Average(s => s.PricePerSqm), 2);

            content.Item().PaddingVertical(8).Row(row =>
            {
                void Cell(string label, string value)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(label).FontSize(8)
                           .FontColor(DocumentStyle.ColorMuted);
                        col.Item().Text(value).FontSize(13).Bold();
                    });
                }

                Cell("SPECIFICATIONS", total.ToString("N0"));
                Cell("TOTAL USES", uses.ToString("N0"));
                Cell("AVERAGE RATE / SQM", avg.ToString("N2"));
            });
        }

        private void RenderTable(ColumnDescriptor content)
        {
            content.Item().PaddingTop(4).Table(table =>
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
                    void Head(string text, bool right = false)
                    {
                        var cell = h.Cell().Background(DocumentStyle.ColorTableHeader)
                                    .PaddingVertical(4).PaddingHorizontal(4);
                        var t = cell.Text(text).FontSize(8).Bold();
                        if (right) t.AlignRight();
                    }

                    Head("TYPE");
                    Head("SPECIFICATION");
                    Head("COLOUR");
                    Head("THK");
                    Head("RATE / SQM", right: true);
                    Head("USED", right: true);
                });

                foreach (var s in _rows)
                {
                    table.Cell().PaddingVertical(3).PaddingHorizontal(4)
                         .Text(s.GlassType ?? "").FontSize(8);

                    table.Cell().PaddingVertical(3).PaddingHorizontal(4)
                         .Text(s.SpecName ?? "").FontSize(8);

                    table.Cell().PaddingVertical(3).PaddingHorizontal(4)
                         .Text(s.Color ?? "").FontSize(8);

                    table.Cell().PaddingVertical(3).PaddingHorizontal(4)
                         .Text(s.ThicknessMm > 0 ? $"{s.ThicknessMm}mm" : "")
                         .FontSize(8);

                    table.Cell().PaddingVertical(3).PaddingHorizontal(4)
                         .Text(s.PricePerSqm.ToString("N2")).FontSize(8).AlignRight();

                    table.Cell().PaddingVertical(3).PaddingHorizontal(4)
                         .Text(s.UsageCount.ToString("N0")).FontSize(8).AlignRight();
                }
            });

            if (_rows.Count == 0)
            {
                content.Item().PaddingTop(20)
                       .Text("No specifications match the current filters.")
                       .FontSize(9).FontColor(DocumentStyle.ColorMuted);
            }
        }
    }
}
