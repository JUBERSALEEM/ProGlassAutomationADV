// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ExportCultureInvarianceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Release-gate source invariants — export file formatting
//
// WHY THIS TEST EXISTS
//   Customer-facing export files (CSV / Excel / PDF for PI, JO, DN,
//   Statements and the central reports) are generated on the OPERATOR'S
//   machine. Every ToString(...) that feeds an export had to be pinned to
//   CultureInfo.InvariantCulture: on an Arabic (`ar-AE`) or European
//   (`de-DE`, `fr-FR`) system locale, a bare `ToString("N2")` writes
//   `1.234,56` (and `dd MMM yyyy` in the OS language) — breaking CSV
//   re-import, Excel recognition and the amounts printed on the customer's
//   PDF (ar-AE even renders Eastern Arabic digits).
//
//   UI display strings keep following the user's culture by design; this
//   test ONLY pins the export boundary (Services/*/Export/* and the
//   reporting maps).
//
//   NOTE: the build suppresses CA1305 (specify IFormatProvider), so a
//   green build does NOT catch this class. This source scan does.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ExportCultureInvarianceTests
    {
        private static string Root()
        {
            var dir = AppContext.BaseDirectory;
            var d = new DirectoryInfo(dir);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "ProGlassAutomation.csproj")))
                d = d.Parent;
            return d?.FullName ?? dir;
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative));

        [Fact]
        public void Export_files_format_numbers_and_dates_invariant()
        {
            // The complete export boundary. Anything added here MUST be
            // culture-invariant; UI display is out of scope.
            string[] exportFiles =
            {
                "Services/Reporting/ReportingExportService.cs",
                "Services/Reporting/ReportDocumentContext.cs",
                "Services/ProformaInvoice/Export/ProformaInvoiceCsvLayout.cs",
                "Services/ProformaInvoice/Export/ProformaInvoiceExcelLayout.cs",
                "Services/ProformaInvoice/Export/ProformaInvoiceListExcelLayout.cs",
                "Services/ProformaInvoice/Export/ProformaInvoicePdfLayout.cs",
                "Services/DeliveryNote/Export/DeliveryNoteCsvLayout.cs",
                "Services/DeliveryNote/Export/DeliveryNoteExcelLayout.cs",
                "Services/DeliveryNote/Export/DeliveryNotePdfLayout.cs",
                "Services/JobOrder/Export/JobOrderCsvLayout.cs",
                "Services/JobOrder/Export/JobOrderExcelLayout.cs",
                "Services/JobOrder/Export/JobOrderPdfLayout.cs",
                "Services/Statement/Pdf/StatementPdfDocument.cs",
            };

            // Culture-sensitive formats: N1/N2/N3 (money/sqm), 0.000/0.##
            // (sqm/geometry), and any date in a written document. The
            // regex requires the CALL SITE to pass a culture — a bare
            // ToString("N2") is the defect.
            string[] formats =
            {
                "N1", "N2", "N3", "0.000", "0.##",
                "dd MMM yyyy  HH:mm", "dd MMM yyyy", "dd MMM yyyy HH:mm",
            };

            var offenders = new System.Collections.Generic.List<string>();
            foreach (var file in exportFiles)
            {
                string text;
                try { text = Read(file); }
                catch (FileNotFoundException) { continue; }

                foreach (var fmt in formats)
                {
                    // ToString("FMT") with NO IFormatProvider argument —
                    // uses the current culture. Ends after the closing
                    // quote + paren, and is NOT followed by a comma.
                    var bare = new Regex(
                        @"\.ToString\(""" + Regex.Escape(fmt) + @"""\)");
                    if (bare.IsMatch(text))
                        offenders.Add($"{file}: bare .ToString(\"{fmt}\")");
                }
            }

            Assert.True(offenders.Count == 0,
                "Export formatting must be culture-invariant.\n" +
                string.Join("\n", offenders) +
                "\nOn an ar-AE/de-DE/fr-FR machine these write 1.234,56 " +
                "and localized month names into customer files — pin " +
                "every export ToString to CultureInfo.InvariantCulture " +
                "(UI display may keep the user's culture).");
        }
    }
}
