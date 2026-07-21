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

            // Header row
            csv.WriteField("Spec #");
            csv.WriteField("Spec Name");
            csv.WriteField("Row #");
            csv.WriteField("Glass Ref");
            csv.WriteField("W1×H1 (mm)");
            csv.WriteField("W2×H2 (mm)");
            csv.WriteField("Qty");
            csv.WriteField("Area (sqm)");
            csv.WriteField("Rate (AED)");
            csv.WriteField("Total (AED)");
            csv.NextRecord();

            // Data rows
            foreach (var group in ctx.SpecGroups)
            {
                foreach (var row in group.Rows)
                {
                    csv.WriteField(group.GroupNumber);
                    csv.WriteField(group.GroupName);
                    csv.WriteField(row.Serial);
                    csv.WriteField(row.GlassRef);
                    csv.WriteField(row.SizeW1H1);
                    csv.WriteField(row.SizeW2H2);
                    csv.WriteField(row.Qty);
                    csv.WriteField(row.Area.ToString("0.000"));
                    csv.WriteField(row.Rate.ToString("N2"));
                    csv.WriteField(row.Total.ToString("N2"));
                    csv.NextRecord();
                }
            }

            // Empty row
            csv.NextRecord();

            // Summary section
            csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField("");
            csv.WriteField("Glass Materials Total");
            csv.WriteField(ctx.GlassMaterialsTotal.ToString("N2"));
            csv.NextRecord();

            csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField("");
            csv.WriteField("Other Charges Total");
            csv.WriteField(ctx.OtherChargesTotal.ToString("N2"));
            csv.NextRecord();

            csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField(""); csv.WriteField("");
            csv.WriteField("GRAND TOTAL (VAT INC)");
            csv.WriteField(ctx.GrandTotal.ToString("N2"));
            csv.NextRecord();

            return true;
        }
    }
}