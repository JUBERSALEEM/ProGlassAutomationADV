// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Pdf/StatementPdfExporter.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIXES vs PREVIOUS VERSION:
//   1. att.FileContent → att.FileBytes (confirmed property name)
//   2. TaxInvoicePdfDocument.GeneratePdf() → wrapped in PdfDocumentWrapper
//      TaxInvoicePdfDocument implements IPdfLayout not IDocument.
//      PdfDocumentWrapper (defined in TaxInvoicePdfExporter.cs) bridges
//      IPdfLayout → IDocument → GeneratePdf() works correctly.
//   3. PdfSharp namespaces corrected for PdfSharp-WPF 6.1.1:
//      using PdfSharp.Pdf;
//      using PdfSharp.Pdf.IO;
//   4. PdfReader.Open signature corrected for PdfSharp 6.x.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Services.TaxInvoice.Pdf;
using QuestPDF.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Pdf
{
    /// <summary>
    /// Generates and exports Account Statement PDFs.
    /// Optionally merges invoice PDFs + attachments into one combined PDF.
    ///
    /// PDF MERGE: Uses PdfSharp-WPF 6.1.1 (added to .csproj in Chat 16).
    /// </summary>
    public static class StatementPdfExporter
    {
        // ── Output folder ─────────────────────────────────────────────────
        private static string OutputFolder
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation",
                    "Statements");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        // ── JSON options ──────────────────────────────────────────────────
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINTS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates the statement-only PDF (no invoice attachments).
        /// Returns the full path to the saved PDF file.
        /// </summary>
        public static async Task<string> ExportStatementOnlyAsync(
            StatementSnapshot snapshot)
        {
            return await Task.Run(() =>
            {
                byte[] pdfBytes = GenerateStatementBytes(snapshot);

                string fileName = $"{snapshot.Header.StatementNumber}.pdf";
                string path = Path.Combine(OutputFolder, fileName);

                File.WriteAllBytes(path, pdfBytes);
                return path;
            });
        }

        /// <summary>
        /// Generates the combined PDF:
        ///   Statement pages + Tax Invoice PDFs + Signed attachments.
        ///
        /// Page order per invoice:
        ///   1. Tax Invoice PDF (regenerated from snapshot)
        ///   2. SignedPI attachment (if present)
        ///   3. SignedDN attachment (if present)
        ///   4. Other attachments (if present)
        ///
        /// Returns the full path to the saved combined PDF file.
        /// </summary>
        public static async Task<string> ExportCombinedAsync(
            StatementSnapshot snapshot)
        {
            // ── Step 1: Generate statement PDF bytes ──────────────────────
            byte[] statementBytes = await Task.Run(
                () => GenerateStatementBytes(snapshot));

            // ── Step 2: Collect all PDF byte[] chunks to merge ────────────
            var pdfChunks = new List<byte[]> { statementBytes };

            foreach (var row in snapshot.InvoiceRows)
            {
                // Regenerate Tax Invoice PDF from snapshot
                byte[]? invPdf = await GenerateInvoicePdfBytesAsync(
                    row.InvoiceNumber);
                if (invPdf != null)
                    pdfChunks.Add(invPdf);

                // Load attachments: SignedPI → SignedDN → Other
                var attachments = await App.TaxInvoices
                    .GetAttachmentsAsync(row.InvoiceNumber);

                // SignedPI first
                foreach (var att in attachments.Where(a =>
                    string.Equals(a.AttachmentType, "SignedPI",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    // FIX: att.FileBytes (not att.FileContent)
                    if (att.FileBytes != null && att.FileBytes.Length > 0)
                        pdfChunks.Add(att.FileBytes);
                }

                // SignedDN second
                foreach (var att in attachments.Where(a =>
                    string.Equals(a.AttachmentType, "SignedDN",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    // FIX: att.FileBytes (not att.FileContent)
                    if (att.FileBytes != null && att.FileBytes.Length > 0)
                        pdfChunks.Add(att.FileBytes);
                }

                // Other attachments last
                foreach (var att in attachments.Where(a =>
                    !string.Equals(a.AttachmentType, "SignedPI",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(a.AttachmentType, "SignedDN",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    // FIX: att.FileBytes (not att.FileContent)
                    if (att.FileBytes != null && att.FileBytes.Length > 0)
                        pdfChunks.Add(att.FileBytes);
                }
            }

            // ── Step 3: Merge all chunks with PdfSharp ───────────────────
            string fileName = snapshot.InvoiceRows.Any(r =>
                                  r.AttachmentCount > 0)
                ? $"{snapshot.Header.StatementNumber}-combined.pdf"
                : $"{snapshot.Header.StatementNumber}.pdf";

            string outputPath = Path.Combine(OutputFolder, fileName);

            await Task.Run(() => MergePdfs(pdfChunks, outputPath));

            return outputPath;
        }

        /// <summary>
        /// Opens an existing PDF at the given path using the system
        /// default PDF viewer. Silently does nothing if file missing.
        /// </summary>
        public static void OpenPdf(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath)) return;
            if (!File.Exists(pdfPath)) return;

            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true
                    });
            }
            catch { /* Silently ignore if no PDF viewer installed */ }
        }

        // ═════════════════════════════════════════════════════════════════
        //  STATEMENT PDF GENERATION
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates statement PDF bytes using QuestPDF.
        /// StatementPdfDocument implements IDocument directly →
        /// can call GeneratePdf() directly (no wrapper needed).
        /// </summary>
        private static byte[] GenerateStatementBytes(
            StatementSnapshot snapshot)
        {
            // StatementPdfDocument implements QuestPDF.Infrastructure.IDocument
            // so GeneratePdf() extension method resolves correctly.
            var doc = new StatementPdfDocument(snapshot);
            return doc.GeneratePdf();
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAX INVOICE PDF REGENERATION
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads a Tax Invoice and regenerates its PDF.
        ///
        /// FIX: TaxInvoicePdfDocument implements IPdfLayout (not IDocument).
        /// Must wrap in PdfDocumentWrapper (defined in TaxInvoicePdfExporter.cs)
        /// to get an IDocument that QuestPDF's GeneratePdf() can call.
        ///
        /// Returns null on any error — statement pages still generated.
        /// </summary>
        private static async Task<byte[]?> GenerateInvoicePdfBytesAsync(
            string invoiceNumber)
        {
            try
            {
                var entity = await App.TaxInvoices
                    .GetByInvoiceNumberAsync(invoiceNumber);

                if (entity == null)
                    return null;

                if (string.IsNullOrWhiteSpace(entity.FullSnapshotJson)
                    || entity.FullSnapshotJson == "{}")
                    return null;

                // Deserialize snapshot using existing mapper
                var snapshot = TaxInvoiceMapper
                    .DeserializeSnapshot(entity);

                if (snapshot == null)
                    return null;

                return await Task.Run(() =>
                {
                    // FIX: TaxInvoicePdfDocument implements IPdfLayout.
                    // Wrap in PdfDocumentWrapper to get IDocument.
                    // PdfDocumentWrapper is internal to TaxInvoicePdfExporter.cs
                    // — we use TaxInvoicePdfExporter.GenerateAsync() instead
                    // which handles the wrapper internally.
                    return TaxInvoicePdfExporter.GenerateAsync(entity)
                        .GetAwaiter().GetResult();
                });
            }
            catch
            {
                // Silently skip — statement pages still included
                return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF MERGE (PdfSharp-WPF 6.1.1)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Merges multiple PDF byte[] chunks into a single output file.
        ///
        /// FIX: PdfSharp 6.x namespaces:
        ///   using PdfSharp.Pdf;
        ///   using PdfSharp.Pdf.IO;
        ///   PdfReader.Open(stream, PdfDocumentOpenMode.Import)
        ///
        /// Invalid/corrupt chunks are silently skipped.
        /// </summary>
        private static void MergePdfs(
            List<byte[]> pdfChunks,
            string outputPath)
        {
            using var output = new PdfDocument();

            foreach (var chunk in pdfChunks)
            {
                if (chunk == null || chunk.Length < 8) continue;

                try
                {
                    using var ms = new MemoryStream(chunk);
                    using var source = PdfReader.Open(
                        ms,
                        PdfDocumentOpenMode.Import);

                    for (int i = 0; i < source.PageCount; i++)
                        output.AddPage(source.Pages[i]);
                }
                catch
                {
                    // Skip corrupt/non-PDF chunk, continue merge
                }
            }

            output.Save(outputPath);
        }

        // ═════════════════════════════════════════════════════════════════
        //  UTILITY
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns expected output path for a statement number.
        /// Used by ViewModel to check if PDF already exists.
        /// </summary>
        public static string GetExpectedPath(
            string statementNumber,
            bool hasAttachments = false)
        {
            string fileName = hasAttachments
                ? $"{statementNumber}-combined.pdf"
                : $"{statementNumber}.pdf";

            return Path.Combine(OutputFolder, fileName);
        }

        /// <summary>
        /// Deletes the PDF files for a statement number (both variants).
        /// Called when a statement is deleted from the list.
        /// </summary>
        public static void DeletePdfs(string statementNumber)
        {
            foreach (var variant in new[] { "", "-combined" })
            {
                string path = Path.Combine(OutputFolder,
                    $"{statementNumber}{variant}.pdf");
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch { /* Ignore — file may be open */ }
            }
        }
    }
}