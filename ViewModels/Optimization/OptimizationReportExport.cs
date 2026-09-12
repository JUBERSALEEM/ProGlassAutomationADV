// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Optimization/OptimizationReportExport.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — report export (CSV / Excel / PDF)
//
// WHY THIS FILE
//   The REPORT tab exported CSV only. This helper routes every session
//   summary (industrial, layout, parts, stocks) and the combined
//   "export all three summaries" action through the SAME central
//   ReportingExportService the Reports hub uses, so optimization exports
//   behave and look identical to the rest of the product while supporting
//   all three formats. A leading "SECTION" column turns the combined
//   export into one document whose Excel/PDF/CSV layouts already know how
//   to group (see ReportingExportService sectioned handling).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProGlassAutomation.ViewModels.Optimization
{
    /// <summary>
    /// Shared CSV / Excel / PDF export for the Optimization session
    /// reports. Row types are the optimization VM's bindable rows, so the
    /// mappers are typed against the real data.
    /// </summary>
    public static class OptimizationReportExport
    {
        /// <summary>
        /// Dispatches a single-table report to the chosen format. Excel
        /// receives typed values (numbers stay numbers); CSV/PDF receive
        /// display text.
        /// </summary>
        public static async System.Threading.Tasks.Task<ExportResult> RunAsync<T>(
            ExportFormat format,
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, string[]> textMapper,
            Func<T, object[]> excelMapper,
            string suggestedFileName)
        {
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true,
                SuggestedFileName = suggestedFileName
            };

            return format switch
            {
                ExportFormat.Csv => await ReportingExportService.ExportCsvAsync(
                    rows, reportTitle, headers, textMapper, request),
                ExportFormat.Excel => await ReportingExportService.ExportExcelAsync(
                    rows, reportTitle, headers, excelMapper, request),
                _ => await ReportingExportService.ExportPdfAsync(
                    rows, reportTitle, headers, textMapper, request)
            };
        }

        // ── combined "export all" ──────────────────────────────────────

        /// <summary>One row of the combined layout + parts + stocks document.</summary>
        public sealed class CombinedRow
        {
            public string Section = string.Empty;
            public string GlassSheet = string.Empty;
            public string SizeThickness = string.Empty;
            public string Pieces = string.Empty;
            public string UtilArea = string.Empty;
            public string OffcutUsed = string.Empty;
            public string Source = string.Empty;
        }

        public static readonly string[] CombinedHeaders =
        {
            "SECTION", "GLASS / SHEET", "SIZE / THICKNESS", "PIECES",
            "UTILIZATION / AREA", "OFFCUT / USED", "SOURCE"
        };

        /// <summary>
        /// Exports the three session summaries as ONE sectioned document
        /// (CSV / Excel / PDF). The leading SECTION column groups rows into
        /// LAYOUT SUMMARY / PARTS SUMMARY / STOCKS SUMMARY blocks.
        /// </summary>
        public static async System.Threading.Tasks.Task<ExportResult> RunCombinedAsync(
            ExportFormat format,
            IReadOnlyList<CombinedRow> rows)
        {
            const string title = "Optimization — Session Report";
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true,
                SuggestedFileName = "Optimization_Session_Report"
            };

            static string[] Text(CombinedRow r) => new[]
            {
                r.Section, r.GlassSheet, r.SizeThickness, r.Pieces,
                r.UtilArea, r.OffcutUsed, r.Source
            };
            static object[] Excel(CombinedRow r) => new object[]
            {
                r.Section, r.GlassSheet, r.SizeThickness, r.Pieces,
                r.UtilArea, r.OffcutUsed, r.Source
            };

            return format switch
            {
                ExportFormat.Csv => await ReportingExportService.ExportCsvAsync(
                    rows, title, CombinedHeaders, Text, request),
                ExportFormat.Excel => await ReportingExportService.ExportExcelAsync(
                    rows, title, CombinedHeaders, Excel, request),
                _ => await ReportingExportService.ExportPdfAsync(
                    rows, title, CombinedHeaders, Text, request)
            };
        }
    }
}
