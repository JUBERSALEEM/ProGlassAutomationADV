using ProGlassAutomation.Services.ProformaInvoice.PdfExport;

namespace ProGlassAutomation.Services.ProformaInvoice.InvoiceExportImport
{
    public interface IInvoiceExportService
    {
        /// <summary>Exports the invoice data to an Excel (.xlsx) file.</summary>
        bool ExportToExcel(PdfExportContext context, string outputFilePath);

        /// <summary>Exports the invoice data to a CSV (.csv) file.</summary>
        bool ExportToCsv(PdfExportContext context, string outputFilePath);
    }
}