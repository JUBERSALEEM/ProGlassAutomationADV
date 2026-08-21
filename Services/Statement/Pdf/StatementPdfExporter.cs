// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Pdf/StatementPdfExporter.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report
//
// CHAT 18 CHANGES:
//   BUG 1 FIX: ExportFromWorkflowAsync Step 4 — iterates
//              invPdf.Attachments and appends each as its own chunk.
//   BUG 5 FIX: ConvertToRawPdfBytes — enhanced PDF detection with
//              4 strategies (header check → PdfSharp reader test →
//              extension fallback → image wrap). Full debug logging
//              on every file to show exactly which strategy matched
//              or why the file was rejected.
//              Fixes: PDF attachments were being skipped when their
//              header was not perfectly %PDF- (BOM, whitespace, etc).
//
// PUBLIC ENTRY POINTS:
//   ExportFromWorkflowAsync(WorkflowResult)  ← PREFERRED (validated)
//   ExportCombinedAsync(StatementSnapshot)   ← LEGACY (backward compat)
//   ExportStatementOnlyAsync(StatementSnapshot) ← LEGACY (statement only)
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Services.TaxInvoice.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Pdf
{
    public static class StatementPdfExporter
    {
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

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // QuestPDF requires the licence to be set before any document is
        // generated; the Community tier covers this business (< USD 1M annual
        // gross revenue). Setting it is idempotent, so call it defensively at
        // every generation point rather than relying on one call-site.
        private static void EnsureLicense()
            => QuestPDF.Settings.License = LicenseType.Community;

        // ═════════════════════════════════════════════════════════════════
        //  NEW ENTRY POINT — WORKFLOW-BASED EXPORT
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates final combined PDF from a pre-validated WorkflowResult.
        ///
        /// PDF structure (in order):
        ///   [1]   Statement summary (1-2 pages)
        ///   [2]   Signed PI attachment (if present)
        ///   [3+]  DN attachments (one file per entry, all DNs)
        ///   [N+]  Per invoice:
        ///           [a] Regenerated Tax Invoice PDF
        ///           [b] TI attachments (SignedPI, SignedDN, Other from DB)
        ///
        /// All data was pre-collected by StatementWorkflowEngine —
        /// this method only merges, no runtime DB calls needed.
        /// </summary>
        public static async Task<string> ExportFromWorkflowAsync(
            WorkflowResult workflowResult,
            IProgress<string>? progress = null)
        {
            if (workflowResult.Snapshot == null)
                throw new InvalidOperationException(
                    "Cannot export — WorkflowResult has no Snapshot.");

            AppLogService.Debug("[Exporter] ═══════════════════════════════");
            AppLogService.Debug("[Exporter] ExportFromWorkflowAsync CALLED");
            AppLogService.Debug($"[Exporter] Outcome:      {workflowResult.Outcome}");
            AppLogService.Debug($"[Exporter] Invoices:     {workflowResult.TaxInvoicePdfs.Count}");
            AppLogService.Debug($"[Exporter] Signed PI:    " +
                $"{(workflowResult.SignedProformaInvoiceBytes != null ? "YES" : "NO")}");
            AppLogService.Debug($"[Exporter] DNs:          {workflowResult.SignedDeliveryNotes.Count}");
            int tiAttTotal = workflowResult.TaxInvoicePdfs.Sum(p => p.AttachmentFileCount);
            AppLogService.Debug($"[Exporter] TI att total: {tiAttTotal}");
            AppLogService.Debug("[Exporter] ═══════════════════════════════");

            var snapshot = workflowResult.Snapshot;
            var pdfChunks = new List<byte[]>();

            // ── Step 1: Statement summary pages ───────────────────────────
            progress?.Report("Generating statement summary...");
            byte[] statementBytes = await Task.Run(
                () => GenerateStatementBytes(snapshot));
            pdfChunks.Add(statementBytes);
            AppLogService.Debug(
                $"[Exporter] Statement: {statementBytes.Length:N0} bytes");

            // ── Step 2: Signed Proforma Invoice attachment ────────────────
            if (workflowResult.SignedProformaInvoiceBytes != null &&
                workflowResult.SignedProformaInvoiceBytes.Length > 0)
            {
                progress?.Report("Adding signed proforma invoice...");
                byte[]? piPdf = ConvertToRawPdfBytes(
                    workflowResult.SignedProformaInvoiceBytes,
                    workflowResult.SignedProformaInvoiceFileName);
                if (piPdf != null)
                {
                    pdfChunks.Add(piPdf);
                    AppLogService.Debug(
                        $"[Exporter] Signed PI: {piPdf.Length:N0} bytes");
                }
            }

            // ── Step 3: Signed Delivery Note attachments ──────────────────
            int dnIndex = 0;
            foreach (var dn in workflowResult.SignedDeliveryNotes)
            {
                dnIndex++;
                if (dn.AttachmentBytes.Count == 0) continue;

                progress?.Report(
                    $"Adding DN {dnIndex}/{workflowResult.SignedDeliveryNotes.Count} " +
                    $"attachments ({dn.DeliveryNoteId})...");

                for (int i = 0; i < dn.AttachmentBytes.Count; i++)
                {
                    string attName = i < dn.AttachmentFileNames.Count
                        ? dn.AttachmentFileNames[i]
                        : $"dn_attachment_{i}";

                    byte[]? attPdf = ConvertToRawPdfBytes(
                        dn.AttachmentBytes[i], attName);

                    if (attPdf != null)
                    {
                        pdfChunks.Add(attPdf);
                        AppLogService.Debug(
                            $"[Exporter] DN {dn.DeliveryNoteId} att[{i + 1}]: " +
                            $"{attPdf.Length:N0} bytes ({attName})");
                    }
                }
            }

            // ── Step 4: Tax Invoice PDFs + their own TI attachments ───────
            int invIndex = 0;
            foreach (var invPdf in workflowResult.TaxInvoicePdfs)
            {
                invIndex++;
                progress?.Report(
                    $"Adding invoice {invIndex}/{workflowResult.TaxInvoicePdfs.Count}: " +
                    $"{invPdf.InvoiceNumber}...");

                // 4a — Invoice PDF itself (or placeholder if failed)
                if (invPdf.IsValid)
                {
                    pdfChunks.Add(invPdf.Bytes!);
                    AppLogService.Debug(
                        $"[Exporter] Invoice {invPdf.InvoiceNumber}: " +
                        $"{invPdf.Bytes!.Length:N0} bytes");
                }
                else
                {
                    byte[] placeholder = GenerateInvoicePlaceholderPdf(
                        invPdf.InvoiceNumber,
                        invPdf.Error ?? "Regeneration failed");
                    pdfChunks.Add(placeholder);
                    AppLogService.Debug(
                        $"[Exporter] Invoice {invPdf.InvoiceNumber}: " +
                        $"PLACEHOLDER (reason: {invPdf.Error})");
                }

                // 4b — TI attachments for this invoice
                if (invPdf.Attachments.Count > 0)
                {
                    progress?.Report(
                        $"Adding {invPdf.Attachments.Count} attachment(s) " +
                        $"for {invPdf.InvoiceNumber}...");

                    int tiIdx = 0;
                    foreach (var (fileBytes, fileName) in invPdf.Attachments)
                    {
                        tiIdx++;
                        if (fileBytes == null || fileBytes.Length == 0)
                        {
                            AppLogService.Debug(
                                $"[Exporter]   TI att[{tiIdx}] {fileName}: " +
                                "SKIPPED (empty bytes)");
                            continue;
                        }

                        byte[]? attPdf = ConvertToRawPdfBytes(fileBytes, fileName);
                        if (attPdf != null)
                        {
                            pdfChunks.Add(attPdf);
                            AppLogService.Debug(
                                $"[Exporter]   TI att[{tiIdx}] {fileName}: " +
                                $"{attPdf.Length:N0} bytes");
                        }
                        else
                        {
                            AppLogService.Debug(
                                $"[Exporter]   TI att[{tiIdx}] {fileName}: " +
                                "SKIPPED (not PDF or image)");
                        }
                    }
                }
                else
                {
                    AppLogService.Debug(
                        $"[Exporter] Invoice {invPdf.InvoiceNumber}: " +
                        "no TI attachments");
                }
            }

            // ── Step 5: Merge all chunks into final PDF ───────────────────
            progress?.Report($"Merging {pdfChunks.Count} sections...");

            bool hasExtras =
                workflowResult.SignedProformaInvoiceBytes != null
                || workflowResult.SignedDeliveryNotes
                       .Any(d => d.AttachmentBytes.Count > 0)
                || workflowResult.TaxInvoicePdfs.Count > 0;

            string baseFileName = hasExtras
                ? $"{snapshot.Header.StatementNumber}-combined.pdf"
                : $"{snapshot.Header.StatementNumber}.pdf";

            string outputPath = ResolveUniquePath(baseFileName);
            await Task.Run(() => MergePdfs(pdfChunks, outputPath));

            AppLogService.Debug($"[Exporter] ✓ Final PDF: {outputPath}");
            AppLogService.Debug($"[Exporter] Total chunks merged: {pdfChunks.Count}");
            AppLogService.Debug("[Exporter] ═══════════════════════════════");

            progress?.Report("PDF ready.");
            return outputPath;
        }

        // ═════════════════════════════════════════════════════════════════
        //  LEGACY ENTRY POINTS (backward compat)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates statement PDF only (no attachments, no invoices).
        /// LEGACY — use ExportFromWorkflowAsync for validated exports.
        /// </summary>
        public static async Task<string> ExportStatementOnlyAsync(
            StatementSnapshot snapshot,
            IProgress<string>? progress = null)
        {
            progress?.Report("Generating statement PDF...");
            return await Task.Run(() =>
            {
                byte[] pdfBytes = GenerateStatementBytes(snapshot);
                string fileName = $"{snapshot.Header.StatementNumber}.pdf";
                string path = ResolveUniquePath(fileName);
                File.WriteAllBytes(path, pdfBytes);
                return path;
            });
        }

        /// <summary>
        /// LEGACY — combined PDF export without workflow validation.
        /// Loads all invoices + attachments for the snapshot's invoice rows
        /// and merges into one combined PDF.
        /// Kept for backward compatibility with code that hasn't migrated
        /// to the workflow engine yet.
        /// </summary>
        public static async Task<string> ExportCombinedAsync(
            StatementSnapshot snapshot,
            IProgress<string>? progress = null)
        {
            AppLogService.Debug("[Exporter] ═══════════════════════════════");
            AppLogService.Debug("[Exporter] ExportCombinedAsync CALLED (LEGACY)");
            AppLogService.Debug($"[Exporter] Invoices: {snapshot.InvoiceRows.Count}");
            AppLogService.Debug("[Exporter] ═══════════════════════════════");

            progress?.Report("Generating statement summary...");
            byte[] statementBytes = await Task.Run(
                () => GenerateStatementBytes(snapshot));
            var pdfChunks = new List<byte[]> { statementBytes };

            int totalRows = snapshot.InvoiceRows.Count;
            int currentRow = 0;

            foreach (var row in snapshot.InvoiceRows)
            {
                currentRow++;
                progress?.Report(
                    $"Processing invoice {currentRow}/{totalRows}: " +
                    $"{row.InvoiceNumber}");

                // Regenerate Tax Invoice PDF
                byte[]? invPdf = null;
                string? invError = null;
                try
                {
                    invPdf = await GenerateInvoicePdfBytesLegacy(row.InvoiceNumber);
                    if (invPdf == null)
                        invError = "Entity/snapshot missing";
                    else if (!IsValidPdf(invPdf))
                    {
                        invError = "Regenerated bytes are not valid PDF";
                        invPdf = null;
                    }
                }
                catch (Exception ex)
                {
                    invError = ex.Message;
                    invPdf = null;
                }

                if (invPdf != null)
                {
                    pdfChunks.Add(invPdf);
                    AppLogService.Debug(
                        $"[Exporter]   ✓ Invoice PDF: {invPdf.Length:N0} bytes");
                }
                else
                {
                    byte[] placeholder = GenerateInvoicePlaceholderPdf(
                        row.InvoiceNumber, invError ?? "Unknown error");
                    pdfChunks.Add(placeholder);
                    AppLogService.Debug(
                        $"[Exporter]   ✗ Placeholder: {row.InvoiceNumber}: " +
                        $"{invError}");
                }

                // Load attachments
                try
                {
                    var attachments = await App.TaxInvoices
                        .GetAttachmentsAsync(row.InvoiceNumber);

                    var ordered = attachments
                        .Where(a => a.FileBytes != null &&
                                    a.FileBytes.Length > 0)
                        .OrderBy(a => GetAttachmentOrder(a.AttachmentType))
                        .ThenBy(a => a.UploadedAt)
                        .ToList();

                    foreach (var att in ordered)
                    {
                        try
                        {
                            byte[]? attPdf = ConvertToRawPdfBytes(
                                att.FileBytes!, att.FileName ?? "");
                            if (attPdf != null)
                            {
                                pdfChunks.Add(attPdf);
                                AppLogService.Debug(
                                    $"[Exporter]   ✓ Attachment: {att.FileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogService.Debug(
                                $"[Exporter]   ✗ Attachment error " +
                                $"{att.FileName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogService.Debug(
                        $"[Exporter]   ✗ Attachment load error " +
                        $"{row.InvoiceNumber}: {ex.Message}");
                }
            }

            progress?.Report($"Merging {pdfChunks.Count} sections...");

            bool hasAttachments = snapshot.InvoiceRows.Any(
                r => r.AttachmentCount > 0);
            string baseFileName = hasAttachments
                ? $"{snapshot.Header.StatementNumber}-combined.pdf"
                : $"{snapshot.Header.StatementNumber}.pdf";

            string outputPath = ResolveUniquePath(baseFileName);
            await Task.Run(() => MergePdfs(pdfChunks, outputPath));

            AppLogService.Debug($"[Exporter] ✓ Saved: {outputPath}");
            AppLogService.Debug("[Exporter] ═══════════════════════════════");

            progress?.Report("PDF ready.");
            return outputPath;
        }

        // ═════════════════════════════════════════════════════════════════
        //  LEGACY HELPER — Tax Invoice regen
        // ═════════════════════════════════════════════════════════════════

        private static async Task<byte[]?> GenerateInvoicePdfBytesLegacy(
            string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return null;

            var entity = await App.TaxInvoices
                .GetByInvoiceNumberAsync(invoiceNumber);
            if (entity == null) return null;

            if (string.IsNullOrWhiteSpace(entity.FullSnapshotJson) ||
                entity.FullSnapshotJson == "{}")
                return null;

            var snapshot = TaxInvoiceMapper.DeserializeSnapshot(entity);
            if (snapshot == null) return null;

            return await TaxInvoicePdfExporter.GenerateAsync(entity);
        }

        // ═════════════════════════════════════════════════════════════════
        //  ATTACHMENT CONVERTER (PDF passthrough or image → PDF wrap)
        //
        //  BUG 5 FIX: Enhanced PDF detection with 4-strategy fallback.
        //    Strategy 1: %PDF- header at position 0 (standard)
        //    Strategy 2: Search first 1024 bytes for %PDF- signature
        //                (handles BOM, whitespace, or leading junk)
        //    Strategy 3: PdfSharp reader test (catches valid PDFs with
        //                unusual structures that fail signature checks)
        //    Strategy 4: Image detection (PNG/JPEG) → wrap in PDF
        //  Full debug logging on every file to show exactly what happened.
        // ═════════════════════════════════════════════════════════════════

        private static byte[]? ConvertToRawPdfBytes(
            byte[] bytes, string fileName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                AppLogService.Debug(
                    $"[Exporter] ConvertToRawPdfBytes: " +
                    $"'{fileName}' — SKIP (null/empty bytes)");
                return null;
            }

            // Header preview for diagnostics
            string headerHex = string.Join(" ",
                bytes.Take(Math.Min(8, bytes.Length))
                     .Select(b => b.ToString("X2")));

            AppLogService.Debug(
                $"[Exporter] ConvertToRawPdfBytes: '{fileName}' — " +
                $"{bytes.Length:N0} bytes — header: [{headerHex}]");

            // ── Strategy 1: %PDF- at position 0 ───────────────────────────
            if (IsValidPdf(bytes))
            {
                AppLogService.Debug(
                    $"[Exporter]   ✓ Strategy 1: Valid PDF (header match)");
                return bytes;
            }

            // ── Strategy 2: %PDF- within first 1024 bytes ─────────────────
            // Some PDFs have UTF-8 BOM (EF BB BF) or leading whitespace
            // before the actual %PDF- signature.
            int pdfOffset = FindPdfSignature(bytes);
            if (pdfOffset > 0)
            {
                AppLogService.Debug(
                    $"[Exporter]   ⚠ Strategy 2: %PDF- found at " +
                    $"offset {pdfOffset} — stripping prefix");

                byte[] trimmed = new byte[bytes.Length - pdfOffset];
                Array.Copy(bytes, pdfOffset, trimmed, 0, trimmed.Length);

                if (TryValidatePdfWithReader(trimmed))
                {
                    AppLogService.Debug(
                        $"[Exporter]   ✓ Strategy 2: Trimmed PDF opens OK " +
                        $"({trimmed.Length:N0} bytes)");
                    return trimmed;
                }

                AppLogService.Debug(
                    $"[Exporter]   ✗ Strategy 2: Trimmed bytes " +
                    $"still not readable");
            }

            // ── Strategy 3: PdfSharp reader on original bytes ─────────────
            if (TryValidatePdfWithReader(bytes))
            {
                AppLogService.Debug(
                    $"[Exporter]   ✓ Strategy 3: PdfSharp reader accepted");
                return bytes;
            }

            // ── Strategy 4: Image detection ───────────────────────────────
            if (IsImageBytes(bytes))
            {
                AppLogService.Debug(
                    $"[Exporter]   → Strategy 4: Image detected — wrap");
                try
                {
                    byte[] wrapped = WrapImageInPdf(bytes, fileName);
                    AppLogService.Debug(
                        $"[Exporter]   ✓ Strategy 4: Image wrapped " +
                        $"({wrapped.Length:N0} bytes)");
                    return wrapped;
                }
                catch (Exception ex)
                {
                    AppLogService.Debug(
                        $"[Exporter]   ✗ Strategy 4: Image wrap failed " +
                        $"— {ex.Message}");
                    return null;
                }
            }

            // ── All strategies failed ─────────────────────────────────────
            AppLogService.Debug(
                $"[Exporter]   ✗ ALL STRATEGIES FAILED — SKIPPED: " +
                $"'{fileName}'");
            return null;
        }

        /// <summary>
        /// Searches the first 1024 bytes for the %PDF- signature.
        /// Returns the byte offset where signature starts, or -1 if not found.
        /// Handles PDFs with BOM (EF BB BF) or leading whitespace.
        /// </summary>
        private static int FindPdfSignature(byte[] bytes)
        {
            int scanLen = Math.Min(1024, bytes.Length - 5);
            for (int i = 0; i <= scanLen; i++)
            {
                if (bytes[i] == 0x25 && // %
                    bytes[i + 1] == 0x50 && // P
                    bytes[i + 2] == 0x44 && // D
                    bytes[i + 3] == 0x46 && // F
                    bytes[i + 4] == 0x2D)   // -
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Attempts to open bytes as PDF using PdfSharp reader.
        /// Handles PDFs with unusual structures that fail signature checks.
        /// Returns true if PdfSharp can parse it successfully.
        /// </summary>
        private static bool TryValidatePdfWithReader(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return false;

            try
            {
                using var ms = new MemoryStream(bytes);
                using var doc = PdfReader.Open(
                    ms, PdfDocumentOpenMode.Import);
                return doc.PageCount > 0;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] WrapImageInPdf(byte[] imageBytes, string fileName)
        {
            EnsureLicense();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);

                    page.Header().PaddingBottom(10).Column(col =>
                    {
                        col.Item()
                           .Text($"Attachment: {fileName}")
                           .FontSize(10).Bold().FontColor(ProGlassAutomation.Services.Export.Pdf.DocumentStyle.ColorAccent);
                        col.Item().PaddingTop(2)
                           .LineHorizontal(1).LineColor(ProGlassAutomation.Services.Export.Pdf.DocumentStyle.ColorBorder);
                    });

                    page.Content().AlignCenter().AlignMiddle()
                        .Image(imageBytes)
                        .FitArea();
                });
            }).GeneratePdf();
        }

        private static byte[] GenerateInvoicePlaceholderPdf(
            string invoiceNumber, string errorMessage)
        {
            EnsureLicense();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);

                    page.Content().AlignCenter().AlignMiddle().Column(col =>
                    {
                        col.Item().AlignCenter()
                           .Text("⚠ INVOICE NOT AVAILABLE")
                           .FontSize(20).Bold().FontColor(ProGlassAutomation.Services.Export.Pdf.DocumentStyle.ColorDanger);
                        col.Item().PaddingTop(16).AlignCenter()
                           .Text($"Invoice: {invoiceNumber}")
                           .FontSize(14).SemiBold().FontColor(ProGlassAutomation.Services.Export.Pdf.DocumentStyle.ColorText);
                        col.Item().PaddingTop(8).AlignCenter()
                           .Text("This invoice could not be regenerated.")
                           .FontSize(11).FontColor(ProGlassAutomation.Services.Export.Pdf.DocumentStyle.ColorMuted);
                        col.Item().PaddingTop(12).AlignCenter()
                           .Text($"Reason: {errorMessage}")
                           .FontSize(9).Italic().FontColor(ProGlassAutomation.Services.Export.Pdf.DocumentStyle.ColorMuted);
                    });
                });
            }).GeneratePdf();
        }

        private static byte[] GenerateStatementBytes(StatementSnapshot snapshot)
        {
            EnsureLicense();
            var doc = new StatementPdfDocument(snapshot);
            return doc.GeneratePdf();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF MERGE
        // ═════════════════════════════════════════════════════════════════

        private static void MergePdfs(List<byte[]> pdfChunks, string outputPath)
        {
            using var output = new PdfDocument();
            int mergedCount = 0;
            int skippedCount = 0;

            foreach (var chunk in pdfChunks)
            {
                if (chunk == null || chunk.Length < 8)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    using var ms = new MemoryStream(chunk);
                    using var source = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < source.PageCount; i++)
                        output.AddPage(source.Pages[i]);
                    mergedCount++;
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[Exporter] Merge skip: {ex.Message}");
                    skippedCount++;
                }
            }

            AppLogService.Debug(
                $"[Exporter] Merge complete: {mergedCount} chunks merged, " +
                $"{skippedCount} skipped, {output.PageCount} total pages");

            output.Save(outputPath);
        }

        // ═════════════════════════════════════════════════════════════════
        //  UTILITY
        // ═════════════════════════════════════════════════════════════════

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
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[StatementPdfExporter] Failed to open generated PDF", ex);
            }
        }

        private static int GetAttachmentOrder(string? type)
        {
            if (string.Equals(type, "SignedPI",
                StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(type, "SignedDN",
                StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private static bool IsValidPdf(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 5) return false;
            return bytes[0] == 0x25 && // %
                   bytes[1] == 0x50 && // P
                   bytes[2] == 0x44 && // D
                   bytes[3] == 0x46 && // F
                   bytes[4] == 0x2D;   // -
        }

        private static bool IsImageBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return false;
            // PNG
            if (bytes[0] == 0x89 && bytes[1] == 0x50 &&
                bytes[2] == 0x4E && bytes[3] == 0x47) return true;
            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 &&
                bytes[2] == 0xFF) return true;
            return false;
        }

        private static string ResolveUniquePath(string baseFileName)
        {
            string path = Path.Combine(OutputFolder, baseFileName);
            if (!File.Exists(path)) return path;

            string nameOnly = Path.GetFileNameWithoutExtension(baseFileName);
            string ext = Path.GetExtension(baseFileName);

            for (int counter = 1; counter < 1000; counter++)
            {
                string candidate = Path.Combine(
                    OutputFolder, $"{nameOnly}_({counter}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
            return path;
        }

        public static string GetExpectedPath(
            string statementNumber, bool hasAttachments = false)
        {
            string fileName = hasAttachments
                ? $"{statementNumber}-combined.pdf"
                : $"{statementNumber}.pdf";
            return Path.Combine(OutputFolder, fileName);
        }

        public static void DeletePdfs(string statementNumber)
        {
            foreach (var variant in new[] { "", "-combined" })
            {
                string pattern = $"{statementNumber}{variant}*.pdf";
                try
                {
                    foreach (var file in Directory.GetFiles(
                        OutputFolder, pattern))
                    {
                        try { File.Delete(file); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
        }
    }
}