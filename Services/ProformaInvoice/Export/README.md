# ProformaInvoice Export — Phase 9.5b

<!-- pg-nav -->
> **[Repository root](../../../README.md)** · [All documents](../../../docs/README.md) · [Application workflow](../../../docs/WORKFLOW.md) · [Design system](../../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Shared export engine integration for the Proforma Invoice module.
Part of the Phase 9.5 export engine migration (strangler pattern).

## Files

    Services/ProformaInvoice/Export/
        ProformaInvoicePdfLayout.cs     implements IPdfLayout
        ProformaInvoiceExcelLayout.cs   implements IExcelLayout
        ProformaInvoiceCsvLayout.cs     implements ICsvLayout
        README.md                       this file

## Usage — ViewModel

    // PDF
    var layout  = new ProformaInvoicePdfLayout(entity, snapshot);
    var engine  = new PdfExportEngine();
    var request = new ExportRequest { PromptForLocation = true, OpenAfterSave = true };
    var result  = await engine.ExportAsync(layout, request);
    if (!result.Success && !result.Cancelled)
        MessageBox.Show($"Export failed: {result.ErrorMessage}");

    // Excel
    var layout  = new ProformaInvoiceExcelLayout(entity, snapshot);
    var engine  = new ExcelExportEngine();
    var result  = await engine.ExportAsync(layout, request);

    // CSV
    var layout  = new ProformaInvoiceCsvLayout(entity, snapshot);
    var engine  = new CsvExportEngine();
    var result  = await engine.ExportAsync(layout, request);

    // Print (physical printer — Phase 9.5b Q2 addition)
    var layout  = new ProformaInvoicePdfLayout(entity, snapshot);
    var engine  = new PdfExportEngine();
    await engine.PrintAsync(layout, $"Proforma Invoice - {entity.ProformaId}");

## Constructors

Both constructors accept the same two parameters:

    ProformaInvoicePdfLayout(SavedProformaInvoice entity, ProformaInvoiceSnapshot snapshot)
    ProformaInvoiceExcelLayout(SavedProformaInvoice entity, ProformaInvoiceSnapshot snapshot)
    ProformaInvoiceCsvLayout(SavedProformaInvoice entity, ProformaInvoiceSnapshot snapshot)

The layouts work in two call paths:

Path A — Wizard export (PrintableInvoiceViewModel):
    Build entity + snapshot from live wizard VMs via ProformaInvoiceMapper,
    then pass to layout constructor.

Path B — List export (SavedInvoicesListViewModel):
    Load entity from DB, deserialize snapshot via
    ProformaInvoiceMapper.DeserializeSnapshot(entity), then pass to
    layout constructor. No wizard VMs needed (preserves gotcha #7).

## Strangler Pattern

Round 1 (this commit): New layouts + rewired ViewModels.
    Legacy services still compile:
        Services/ProformaInvoice/PdfExport/ProformaInvoicePdfService.cs
        Services/ProformaInvoice/InvoiceExportImport/InvoiceExcelExportService.cs
        Services/ProformaInvoice/InvoiceExportImport/InvoiceCsvExportService.cs
    Before deleting: grep entire solution for references to these types.

Round 2 (separate commit after Round 1 confirmed working):
    Delete the 3 legacy service files above.
    Delete Services/ProformaInvoice/PdfExport/PdfExportContextBuilder.cs
    (only after confirming no remaining references).
    PdfExportContext.cs may stay as it is still referenced by
    SavedInvoicesListViewModel.BuildExportContextFromSnapshot — check grep.

## Key design decisions

- Visual parity: PDF output matches legacy ProformaInvoicePdfService exactly.
  Same brand colours (8 constants), same 10-column spec table (Phase 7.19d),
  same section order, same T&C clause rendering with sub-bullets.

- Letterhead: Shared engine renders company block on page 1 via LetterheadRenderer.
  PI badge (Ref/Date/Status) renders below it via RenderPiBadgeRow().
  No letterhead on page 2+ (engine handles this automatically via .ShowOnce()).

- Pricing included: All rates, totals, VAT, discount, grand total shown.
  Unlike Job Order layouts which suppress pricing for factory floor.

- Grand total source: Always entity.GrandTotalVatInclusive (indexed column).
  Never recalculated from snapshot inputs. See gotcha #20 in rules.md.

- PreparedBy override pattern: Snapshot stores the resolved values
  (PreparedByName falls back to Client.DocumentPreparedBy at save time
  via ProformaInvoiceMapper). Layout reads snapshot.PreparedBy directly.

- PdfExportContext and PdfExportContextBuilder: Left untouched in Round 1.
  The new layouts use a private PiRenderContext (internal sealed class)
  instead of PdfExportContext to avoid coupling.

## PDF output structure

Page 1:
    [Shared engine letterhead]  — company name, location, TRN, document title
    [PI badge]                  — Ref, Date, Status (right-aligned 200px)
    [Horizontal rule]
    [Client block | Project block]
    [Horizontal rule]
    [Spec group tables × N]
    [Other charges]
    [Financial summary]
    [Terms & Conditions]
    [Footer text]
    [Signature blocks]
    [Engine footer: PI-YYYY-NNN · Printed timestamp    Page 1 of N]

Page 2+:
    [No letterhead — more content space]
    [Content continues]
    [Engine footer: PI-YYYY-NNN · Printed timestamp    Page X of N]

## Excel output structure

    Sheet 1: Invoice Summary    Company + Client + Project + Financial + PreparedBy
    Sheet 2: Glass Panels       12-col table with per-spec subtotals + grand total row
    Sheet 3: Other Charges      4-col table (only if charges exist)
    Sheet 4: Terms & Conditions 3-col table (only if clauses exist)

## CSV output structure

    Row 1:    Header (12 columns)
    Rows 2-N: One row per glass panel item (all spec groups combined)
    Row N+1:  Blank separator
    Row N+2:  Glass Materials Total
    Row N+3:  Other Charges Total
    Row N+4:  GRAND TOTAL (VAT INC)

## Filename convention (ExportFileNamer)

    PI_{CustomerName}_{ProjectName}_{IssueDate:dd-MM-yyyy}.{ext}

    Example:
        PI_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.pdf
        PI_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.xlsx
        PI_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.csv

<!-- pg-nav -->
---

*This README documents `Services/ProformaInvoice/Export/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../../docs/README.md).*
<!-- pg-nav -->
