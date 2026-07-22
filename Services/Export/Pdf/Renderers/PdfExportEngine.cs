using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.FileSystem;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// The single PDF export engine used by every module.
    ///
    /// Responsibilities:
    /// - QuestPDF page setup (size, orientation, margins)
    /// - Renders shared letterhead + footer on every page
    /// - Delegates body rendering to layout.RenderContent()
    /// - Shows SaveFileDialog when requested
    /// - Writes bytes to disk
    /// - Launches file after save when requested
    /// - Wraps all errors into ExportResult
    ///
    /// Modules never call QuestPDF directly for page setup — only for
    /// content rendering inside RenderContent.
    /// </summary>
    public class PdfExportEngine
    {
        private readonly SaveFileDialogService _dialogService;

        public PdfExportEngine()
        {
            _dialogService = new SaveFileDialogService();

            // QuestPDF community license — set once, safe to set multiple times
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<ExportResult> ExportAsync(
            IPdfLayout layout,
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
                                   ?? ExportFileNamer.Build(layout, ExportFormat.Pdf);
                path = _dialogService.PromptSavePath(
                    layout.DocumentTitle,
                    suggested,
                    "PDF Files (*.pdf)|*.pdf");
                if (string.IsNullOrEmpty(path))
                    return ExportResult.Cancel();
            }

            if (string.IsNullOrWhiteSpace(path))
                return ExportResult.Fail("No output path provided.");

            // ── 2. Build document ─────────────────────────────────────
            try
            {
                var pageLayout = layout.PageLayout ?? PageLayout.A4Portrait;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        // Page size
                        page.Size(ResolvePageSize(pageLayout));
                        page.Margin(pageLayout.MarginMm, Unit.Millimetre);
                        page.DefaultTextStyle(t => t
                            .FontFamily(DocumentStyle.FontFamily)
                            .FontSize(DocumentStyle.FontSizeBody)
                            .FontColor(DocumentStyle.ColorText));

                        // Header (letterhead)
                        if (layout.Letterhead != null)
                        {
                            page.Header().Element(h =>
                                LetterheadRenderer.Render(h, layout.Letterhead));
                        }

                        // Content — module renders here
                        page.Content().Column(col =>
                        {
                            col.Spacing(DocumentStyle.SpaceSection);
                            layout.RenderContent(col);
                        });

                        // Footer (shared page number + printed-at)
                        page.Footer().Element(FooterRenderer.Render);
                    });
                });

                // ── 3. Write to disk ──────────────────────────────────
                await Task.Run(() =>
                {
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    document.GeneratePdf(path);
                });

                // ── 4. Open after save if requested ───────────────────
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
                        // File saved OK, just couldn't launch viewer — non-fatal
                    }
                }

                return ExportResult.Ok(path);
            }
            catch (Exception ex)
            {
                return ExportResult.Fail(
                    $"PDF export failed: {ex.Message}", ex);
            }
        }

        private static QuestPDF.Helpers.PageSize ResolvePageSize(PageLayout layout)
        {
            var basePage = layout.Size switch
            {
                Pdf.PageSize.A3 => PageSizes.A3,
                Pdf.PageSize.Letter => PageSizes.Letter,
                _ => PageSizes.A4
            };

            return layout.Orientation == PageOrientation.Landscape
                ? basePage.Landscape()
                : basePage.Portrait();
        }
    }
}