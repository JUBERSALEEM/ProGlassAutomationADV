namespace ProGlassAutomation.Services.ProformaInvoice.PdfExport
{
    /// <summary>
    /// Generates a branded PDF file from the current proforma invoice wizard state.
    /// </summary>
    public interface IProformaInvoicePdfService
    {
        /// <summary>
        /// Builds the PDF document and saves it to the specified file path.
        /// Returns true if successful.
        /// </summary>
        bool GeneratePdf(PdfExportContext context, string outputFilePath);
    }
}