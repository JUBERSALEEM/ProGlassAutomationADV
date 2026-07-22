using CsvHelper;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.FileSystem;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Export.Csv
{
    /// <summary>
    /// The single CSV export engine used by every module.
    ///
    /// Responsibilities:
    /// - Opens a StreamWriter with UTF-8 BOM (Excel-friendly)
    /// - Creates a CsvWriter with invariant culture
    /// - Delegates all row writing to layout.WriteRows()
    /// - Shows SaveFileDialog when requested
    /// - Launches file after save when requested
    /// - Wraps all errors into ExportResult
    ///
    /// UTF-8 BOM is critical — without it Excel misinterprets Unicode
    /// characters like Arabic company names or the AED symbol.
    /// </summary>
    public class CsvExportEngine
    {
        private readonly SaveFileDialogService _dialogService;

        public CsvExportEngine()
        {
            _dialogService = new SaveFileDialogService();
        }

        public async Task<ExportResult> ExportAsync(
            ICsvLayout layout,
            ExportRequest request)
        {
            if (layout == null)
                return ExportResult.Fail("Layout is null.");
            if (request == null)
                return ExportResult.Fail("Request is null.");

            // ── 1. Resolve output path ────────────────────────────────
            string? path = request.OutputPath;
            if (request.PromptForLocation)
            {
                string suggested = request.SuggestedFileName
                                   ?? ExportFileNamer.Build(layout, ExportFormat.Csv);
                path = _dialogService.PromptSavePath(
                    layout.DocumentTitle,
                    suggested,
                    "CSV File (*.csv)|*.csv");
                if (string.IsNullOrEmpty(path))
                    return ExportResult.Cancel();
            }

            if (string.IsNullOrWhiteSpace(path))
                return ExportResult.Fail("No output path provided.");

            // ── 2. Write file ─────────────────────────────────────────
            try
            {
                await Task.Run(() =>
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    // UTF-8 with BOM — critical for Excel to render
                    // Unicode chars like Arabic customer names correctly
                    using var stream = new StreamWriter(
                        path,
                        append: false,
                        encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                    using var csv = new CsvWriter(stream, CultureInfo.InvariantCulture);

                    // Module writes rows here
                    layout.WriteRows(csv);
                });

                // ── 3. Open after save if requested ───────────────────
                if (request.OpenAfterSave && File.Exists(path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // File saved OK — non-fatal if launcher fails
                    }
                }

                return ExportResult.Ok(path);
            }
            catch (Exception ex)
            {
                return ExportResult.Fail(
                    $"CSV export failed: {ex.Message}", ex);
            }
        }
    }
}