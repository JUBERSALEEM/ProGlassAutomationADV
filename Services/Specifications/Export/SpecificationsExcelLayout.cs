// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Specifications/Export/SpecificationsExcelLayout.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02
//
//  Excel workbook for the specification catalogue.
//
//  Two sheets, because they answer different questions:
//    • "Catalogue"  — one row per specification, the reference list.
//    • "Usage"      — one row per USE, which is where the money question
//                     lives: the same spec quoted to different customers
//                     at different rates.
//
//  A single flat sheet cannot express that, because a spec row holds only
//  the LATEST price while each usage row holds what that invoice actually
//  paid.
// ═══════════════════════════════════════════════════════════════════════

using ClosedXML.Excel;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Specifications.Export
{
    public class SpecificationsExcelLayout : IExcelLayout
    {
        private readonly IReadOnlyList<SavedSpecification> _rows;
        private readonly IReadOnlyList<SpecificationUsage> _usage;

        public SpecificationsExcelLayout(
            IReadOnlyList<SavedSpecification> rows,
            IReadOnlyList<SpecificationUsage>? usage = null)
        {
            _rows = rows ?? Array.Empty<SavedSpecification>();
            _usage = usage ?? Array.Empty<SpecificationUsage>();
        }

        public string DocumentPrefix => "SPEC";
        public string DocumentId => "Specifications";
        public string DocumentTitle => "Specifications Catalogue";
        public string CompanyName => string.Empty;
        public string Reference => string.Empty;
        public DateTime DocumentDate => DateTime.Now;

        public void RenderWorkbook(IXLWorkbook workbook)
        {
            BuildCatalogueSheet(workbook);
            if (_usage.Count > 0) BuildUsageSheet(workbook);
        }

        private void BuildCatalogueSheet(IXLWorkbook workbook)
        {
            var ws = workbook.Worksheets.Add("Catalogue");
            ws.ShowGridLines = false;
            int r = 1;

            ws.Cell(r, 1).Value = "SPECIFICATIONS CATALOGUE";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 14;
            ws.Range(r, 1, r, 8).Merge();
            r += 2;

            string[] headers =
            {
                "Glass Type", "Specification", "Colour", "Thickness (mm)",
                "Rate / SQM", "Times Used", "Created", "Last Used"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFEFEF");
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }
            r++;

            foreach (var s in _rows)
            {
                ws.Cell(r, 1).Value = s.GlassType ?? "";
                ws.Cell(r, 2).Value = s.SpecName ?? "";
                ws.Cell(r, 3).Value = s.Color ?? "";
                ws.Cell(r, 4).Value = s.ThicknessMm;
                ws.Cell(r, 5).Value = s.PricePerSqm;
                ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 6).Value = s.UsageCount;
                ws.Cell(r, 7).Value = s.CreatedAt;
                ws.Cell(r, 7).Style.NumberFormat.Format = "dd-mmm-yyyy";
                ws.Cell(r, 8).Value = s.LastUsedAt;
                ws.Cell(r, 8).Style.NumberFormat.Format = "dd-mmm-yyyy";
                r++;
            }

            if (_rows.Count > 0)
            {
                ws.Cell(r, 4).Value = "TOTAL";
                ws.Cell(r, 4).Style.Font.Bold = true;
                ws.Cell(r, 6).FormulaA1 = $"SUM(F{r - _rows.Count}:F{r - 1})";
                ws.Cell(r, 6).Style.Font.Bold = true;
                ws.Range(r, 1, r, 8).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns(1, 8).AdjustToContents();
            ws.SheetView.FreezeRows(3);
        }

        private void BuildUsageSheet(IXLWorkbook workbook)
        {
            var ws = workbook.Worksheets.Add("Usage");
            ws.ShowGridLines = false;
            int r = 1;

            ws.Cell(r, 1).Value = "SPECIFICATION USAGE — every proforma, at the rate it was quoted";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 12;
            ws.Range(r, 1, r, 7).Merge();
            r += 2;

            string[] headers =
            {
                "Specification", "Glass Type", "Proforma",
                "Customer", "Project", "Rate / SQM", "Used On"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFEFEF");
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }
            r++;

            foreach (var u in _usage.OrderByDescending(u => u.UsedAt))
            {
                ws.Cell(r, 1).Value = u.SpecName ?? "";
                ws.Cell(r, 2).Value = u.GlassType ?? "";
                ws.Cell(r, 3).Value = u.ProformaDisplay;
                ws.Cell(r, 4).Value = u.CustomerName ?? "";
                ws.Cell(r, 5).Value = u.ProjectName ?? "";
                ws.Cell(r, 6).Value = u.PricePerSqm;
                ws.Cell(r, 6).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 7).Value = u.UsedAt;
                ws.Cell(r, 7).Style.NumberFormat.Format = "dd-mmm-yyyy hh:mm";
                r++;
            }

            ws.Columns(1, 7).AdjustToContents();
            ws.SheetView.FreezeRows(3);
        }
    }
}
