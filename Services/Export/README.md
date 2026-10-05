# Shared Export Engine

<!-- pg-nav -->
> **[Repository root](../../README.md)** · [All documents](../../docs/README.md) · [Application workflow](../../docs/WORKFLOW.md) · [Design system](../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Single source of truth for all document exports across all ProGlass
Automation modules. Modules implement thin layout contracts and
delegate every operational concern (page setup, file dialog, filename,
error handling) to the shared engine.

Introduced in Phase 9.5a. Job Order is the first consumer (Phase
9.5a-r3). Proforma Invoice migrates in Phase 9.5b. All future modules
use this engine from day 1.

## Why this exists

Before the engine: every module owned its own PdfExportService +
ExcelExportService + CsvExportService. With 15 planned modules that
would mean 45 export services with 45 different SaveFileDialog
implementations, 45 different filename conventions, and 45 places to
fix any bug.

After the engine: 3 engines (PDF, Excel, CSV) + 15 layout classes.
One filename convention. One dialog appearance. One place to fix bugs.

## Folder layout

    Services/Export/
        Core/                   Universal building blocks
            ExportFormat.cs         Pdf / Excel / Csv enum
            ExportRequest.cs        Input: path, prompt?, open?
            ExportResult.cs         Output: Success / Cancelled / Failed
            IDocumentLayout.cs      Base contract for every layout

        Pdf/                    PDF-specific
            PageLayout.cs           A4/A3/Letter × Portrait/Landscape
            LetterheadInfo.cs       Company header data
            DocumentStyle.cs        Fonts, colors, spacing tokens
            IPdfLayout.cs           Module PDF contract
            PdfExportEngine.cs      QuestPDF orchestrator
            Renderers/
                LetterheadRenderer.cs      Top-of-page company block
                FooterRenderer.cs          Page X of Y
                SectionHeaderRenderer.cs   Accent color title bar

        Excel/                  Excel-specific
            WorkbookStyle.cs        Fonts, colors, column widths
            IExcelLayout.cs         Module Excel contract
            ExcelExportEngine.cs    ClosedXML orchestrator

        Csv/                    CSV-specific
            ICsvLayout.cs           Module CSV contract
            CsvExportEngine.cs      CsvHelper orchestrator

        FileSystem/             Shared I/O
            SaveFileDialogService.cs   Central SaveFileDialog
            ExportFileNamer.cs         Filename convention + sanitizer

## Filename convention

Every export uses the same filename format:

    {DocumentId}_{CompanyName}_{Reference}_{DocumentDate:dd-MM-yyyy}.{ext}

### Examples

    PI-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.pdf
    JOB-2026-001_ALDAR-PROPERTIES_YAS-TWR-B_03-08-2026.pdf
    DN-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.xlsx
    TAX-2026-001_MERAAS_BB-VILLAS_15-09-2026.csv

### Sanitization rules (per segment)

- Uppercase
- Spaces converted to hyphens
- Windows-invalid characters stripped
- Multiple consecutive hyphens collapsed to one
- Segment capped at 40 characters
- Leading and trailing hyphens trimmed
- Empty segments skipped entirely (no placeholders)

### Date source

`DocumentDate` uses the document's own created/updated date, NOT the
export timestamp. This means exporting 5 old PIs today produces 5
filenames each with their own historical date — so folders sort
meaningfully by document date, not export date.

## How to build a new module export

Say you want to add PDF export to a new Cutting module.

### Step 1 — Create a layout class

    // Services/Cutting/Export/CuttingPdfLayout.cs
    using ProGlassAutomation.Services.Export.Core;
    using ProGlassAutomation.Services.Export.Pdf;
    using ProGlassAutomation.Services.Export.Pdf.Renderers;
    using QuestPDF.Fluent;
    using System;

    public class CuttingPdfLayout : IPdfLayout
    {
        private readonly CuttingSheet _sheet;

        public CuttingPdfLayout(CuttingSheet sheet)
        {
            _sheet = sheet;
        }

        // Identity (from IDocumentLayout)
        public string DocumentPrefix => "CUT";
        public string DocumentId     => _sheet.CuttingSheetId;
        public string DocumentTitle  => "Cutting Sheet";
        public string CompanyName    => _sheet.CustomerName;
        public string Reference      => _sheet.ProjectName;
        public DateTime DocumentDate => _sheet.CreatedAt;

        // PDF-specific
        public PageLayout PageLayout => PageLayout.A4Landscape;

        public LetterheadInfo Letterhead => new LetterheadInfo
        {
            CompanyName     = App.Config.CompanyName,
            CompanyLocation = App.Config.CompanyLocation,
            CompanyTrn      = App.Config.CompanyTrn,
            DocumentTitle   = DocumentTitle,
            DocumentId      = DocumentId,
            SubtitleLine    = $"Machine: {_sheet.MachineName}"
        };

        public void RenderContent(ColumnDescriptor content)
        {
            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "CUT LIST"));

            content.Item().Table(t =>
            {
                // ... render your table
            });

            content.Item().Element(c =>
                SectionHeaderRenderer.Render(c, "OPERATOR NOTES"));

            content.Item().Text(_sheet.Notes);
        }
    }

### Step 2 — Call the engine from your ViewModel

    var engine  = new PdfExportEngine();
    var layout  = new CuttingPdfLayout(sheet);
    var request = new ExportRequest
    {
        PromptForLocation = true,
        OpenAfterSave     = true
    };

    var result = await engine.ExportAsync(layout, request);

    if (result.Success)
        MessageBox.Show($"Saved to {result.SavedPath}");
    else if (!result.Cancelled)
        MessageBox.Show($"Export failed: {result.ErrorMessage}");

That's it. Filename, dialog, letterhead, page numbers, error handling
all handled by the engine. Module writes only its own content.

## Adding Excel/CSV support to a module

Same pattern — implement `IExcelLayout` or `ICsvLayout` instead of
`IPdfLayout`. One module can implement all three by having 3 separate
layout classes:

    CuttingPdfLayout    : IPdfLayout
    CuttingExcelLayout  : IExcelLayout
    CuttingCsvLayout    : ICsvLayout

Then in the ViewModel expose 3 commands (`ExportPdfCommand`,
`ExportExcelCommand`, `ExportCsvCommand`) that instantiate the right
layout and call the right engine.

## What each engine handles (module doesn't touch)

    Concern                              Engine    Module
    ----------------------------------   ------    ------
    SaveFileDialog appearance             Yes       No
    Filename convention                   Yes       No
    Windows-invalid char sanitization     Yes       No
    Directory creation if missing         Yes       No
    File extension                        Yes       No
    Overwrite confirmation                Yes       No
    Error handling and result wrapping    Yes       No
    Launch file after save                Yes       No
    QuestPDF license setup                Yes       No
    UTF-8 BOM for CSV                     Yes       No
    Page setup (size, orientation)        Yes       No
    Page numbers                          Yes       No
    Company letterhead block              Yes       No
    Font family and default sizes         Yes       No
    Cell/section rendering                No        Yes
    Column definitions                    No        Yes
    Business data                         No        Yes
    Layout order                          No        Yes

## Cross-cutting features (planned, not yet built)

- Batch export — one operation produces N documents into a folder
- Export history log — record every export with timestamp + user
- Digital signature via System.Security.Cryptography.Pkcs
- Watermark support (DRAFT, PAID, VOID)
- QR code containing DocumentId for scanning
- Email attachment integration
- Template overrides per company (multi-tenant future)

Each becomes a single engine change → all modules benefit
automatically.

## Migration status

    Module                Status
    ----------------------  --------------------
    Job Order              Round 3 (in progress)
    Proforma Invoice       Phase 9.5b (planned)
    Sheet Store            Phase 9.5c (planned)
    Cutting                Phase 10 (native)
    Delivery Note          Phase 18 (native)
    Tax Invoice            Phase 19 (native)
    Sales Report           Phase 20 (native)

"Native" means the module is built against the engine from day 1 with
no legacy code to migrate.

## Gotchas learned

- QuestPDF 2024.12.2: use `.PaddingHorizontal()` + `.PaddingVertical()`
  never `.Padding(h, v)` — throws CS1503 Unit error
- CSV: always write with UTF-8 BOM or Excel breaks on Arabic/Unicode
- Sanitizer strips underscores intentionally — segments use hyphens
  only so `_` is the reliable segment separator
- Empty segments skipped (no `_NO-COMPANY_` placeholders) to keep
  filenames clean
- QuestPDF license set inside `PdfExportEngine` constructor — safe to
  set multiple times, but never inside `Document.Create` (that
  crashes)

<!-- pg-nav -->
---

*This README documents `Services/Export/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../docs/README.md).*
<!-- pg-nav -->
