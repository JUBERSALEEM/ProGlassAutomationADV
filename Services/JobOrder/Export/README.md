# Job Order Export

Module-specific export layouts for Job Order documents. All three
layouts are thin adapters over the shared export engine
(`Services/Export/`).

Introduced in Phase 9.5a-r3 as the first consumer of the shared
export engine.

## Files

    JobOrderPdfLayout.cs      implements IPdfLayout
    JobOrderExcelLayout.cs    implements IExcelLayout
    JobOrderCsvLayout.cs      implements ICsvLayout

## Design decisions (locked in during Round 3)

### No pricing in any export format

Job Order is a **factory-floor production instruction**, not a
customer document. Factory workers do not need to see rates, line
totals, VAT, or grand totals. Excluding money data:

- Keeps the printout focused on production
- Prevents accidental disclosure of margins to factory staff
- Simplifies the cut list to actionable dimensions

If the customer needs a priced document, they already have the
source Proforma Invoice (which is preserved immutably inside
`JobOrderSnapshot.EmbeddedInvoice`).

### Filename convention

Per shared engine `ExportFileNamer`:

    {JobOrderId}_{CustomerName}_{Reference}_{ConvertedAt:dd-MM-yyyy}.{ext}

Where `Reference` = ProjectName → ContractNo → LpoNumber (first
non-empty wins).

Examples:

    JOB-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.pdf
    JOB-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.xlsx
    JOB-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.csv

## PDF layout

Single A4 portrait page (or multi-page if cut list is very long).

Sections in order:

1. **Letterhead** (rendered by shared `LetterheadRenderer`) —
   company name/TRN/location from `_invoice.Company.*`, job ID
   accent color, subtitle with source PI + delivery date
2. **Status strip** — 4 columns showing Status, Priority (color
   coded), Delivery Date, Source PI
3. **Client & Delivery block** — 2 columns: left = customer + TRN
   + project/contract/LPO, right = site contact + phone + address
4. **Production instructions block** — Assigned To, Drawing
   Reference, Cutting Instructions (boxed), Internal Notes (boxed)
5. **Cut list per spec group** — accent header per group, 7-column
   table (#, Glass Ref, W1, H1, W2, H2, Qty), group total pieces
6. **Production totals** — total specs count + total pieces count
7. **Signature block** — 3 signature cells (Prepared / Verified /
   Received) with underline + name/date placeholders
8. **Footer** (rendered by shared `FooterRenderer`) — printed
   timestamp + Page X of Y

## Excel layout — multi-sheet workbook (design decision Q4-B)

### Sheet 1: Summary

Two-column vertical layout with section headers. Contains job
identity, company, client, project, site delivery, and production
totals. Serves as the human-friendly overview.

### Sheet 2: Cut List

Flat 14-column table with auto-filter enabled. One row per glass
item plus one placeholder row per spec that has no items.
Includes:

    Spec #    Spec Name    Glass Type    Row #    Glass Ref
    W1        H1           W2            H2       Qty
    Job ID    Customer     Delivery Date Overdue

The `Overdue` column (design decision Q2) shows `YES` in red when
delivery is past due and status is not Completed/Delivered,
otherwise `NO`. Overdue rows also get a soft red row background so
the foreman can spot them in a scroll.

Header row frozen. First column pinned via auto-filter panel.

### Sheet 3: Production Notes

Wrapped-text cells for Cutting Instructions and Internal Notes.
Small audit footer showing Last Edited At + Last Edited By.

## CSV layout — flat cut list only (design decision Q3-A)

Single 18-column flat table. Header row + one data row per glass
item. Designed for machine ingestion (import into cutting
optimizer software, ERP systems, or database staging tables).

No section headers, no blank rows, no summary — CSV should stay
strictly parseable.

UTF-8 with BOM (handled by shared `CsvExportEngine`) so Excel
opens Arabic customer names correctly.

## Consumption from ViewModels

### From `JobOrderDetailViewModel`

    // ExportPdfCommand handler
    var layout  = new JobOrderPdfLayout(_entity, _snapshot);
    var engine  = new PdfExportEngine();
    var request = new ExportRequest
    {
        PromptForLocation = true,
        OpenAfterSave     = true
    };
    var result = await engine.ExportAsync(layout, request);
    if (result.Success)
        MessageBox.Show($"Saved to {result.SavedPath}");
    else if (!result.Cancelled)
        MessageBox.Show($"Failed: {result.ErrorMessage}");

Same pattern for Excel (`JobOrderExcelLayout` +
`ExcelExportEngine`) and CSV (`JobOrderCsvLayout` +
`CsvExportEngine`).

### From `SavedJobOrdersListViewModel`

The list view exposes `ExportPdfAsync(row)` and
`ExportExcelAsync(row)` which:

1. Load the full entity via `App.Jobs.GetByIdAsync(row.Id)`
2. Deserialize the snapshot via `JobOrderMapper.DeserializeSnapshot()`
3. Build the layout and call the engine

CSV export from list view is not wired to right-click menu (design
choice — CSV is a data-workflow format, only power users want it
from the detail view where they see full context).

## Gotchas

- `_snapshot.EmbeddedInvoice` may have any of Company/Client/Project
  as null on very old records — all accessors use `?.` and
  `?? string.Empty` fallbacks
- `_snapshot.Production` is nullable — always coalesce with `new
  ProductionDetails()` if you need a non-null reference
- Spec groups can have zero item rows — Excel emits a `(no rows)`
  placeholder, PDF skips silently
- `SpecGroupSnapshot.SelectedGlassType` may be empty string when
  the wizard was left with no glass type selected — display as-is