using CsvHelper;
using CsvHelper.Configuration;
using ProGlassAutomation.Services.ProformaInvoice.PdfExport;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.InvoiceExportImport
{
    public class InvoiceCsvExportService
    {
        public bool ExportToCsv(PdfExportContext ctx, string outputFilePath)
        {
            using var writer = new StreamWriter(outputFilePath);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            // ══════════════════════════════════════════════════════════════
            //  Phase 7.19d — 12 columns:
            //  Spec # | Spec Name | Row # | Glass Ref | W1 | H1 | W2 | H2 | Qty | Area | Rate | Total
            // ══════════════════════════════════════════════════════════════
            csv.WriteField("Spec #");
            csv.WriteField("Spec Name");
            csv.WriteField("Row #");
            csv.WriteField("Glass Ref");
            csv.WriteField("W1");
            csv.WriteField("H1");
            csv.WriteField("W2");
            csv.WriteField("H2");
            csv.WriteField("Qty");
            csv.WriteField("Area (sqm)");
            csv.WriteField("Rate (AED)");
            csv.WriteField("Total (AED)");
            csv.NextRecord();

            foreach (var group in ctx.SpecGroups)
            {
                foreach (var row in group.Rows)
                {
                    csv.WriteField(group.GroupNumber);
                    csv.WriteField(group.GroupName);
                    csv.WriteField(row.Serial);
                    csv.WriteField(row.GlassRef);
                    csv.WriteField(row.W1);
                    csv.WriteField(row.H1);
                    csv.WriteField(row.W2);
                    csv.WriteField(row.H2);
                    csv.WriteField(row.Qty);
                    csv.WriteField(row.Area.ToString("0.000"));
                    csv.WriteField(row.Rate.ToString("N2"));
                    csv.WriteField(row.Total.ToString("N2"));
                    csv.NextRecord();
                }
            }

            csv.NextRecord();

            // Summary section — 11 empty fields + label + value = 12 fields
            for (int i = 0; i < 10; i++) csv.WriteField("");
            csv.WriteField("Glass Materials Total");
            csv.WriteField(ctx.GlassMaterialsTotal.ToString("N2"));
            csv.NextRecord();

            for (int i = 0; i < 10; i++) csv.WriteField("");
            csv.WriteField("Other Charges Total");
            csv.WriteField(ctx.OtherChargesTotal.ToString("N2"));
            csv.NextRecord();

            for (int i = 0; i < 10; i++) csv.WriteField("");
            csv.WriteField("GRAND TOTAL (VAT INC)");
            csv.WriteField(ctx.GrandTotal.ToString("N2"));
            csv.NextRecord();

            return true;
        }
    }
}