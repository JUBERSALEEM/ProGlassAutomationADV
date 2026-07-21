# Invoice Export/Import — Phase 7.18

Excel and CSV export services for proforma invoice data.
Reuses the PdfExportContext from Phase 7.17 as the data source.

## Purpose

Users need invoice data in spreadsheet format for:
- Sharing with accounts/procurement teams
- Importing into other ERP systems
- Archival in company file systems
- Client-requested data format

## Export Formats

### Excel (.xlsx) — via ClosedXML
Multi-sheet workbook:
- Sheet 1: Invoice Summary (company, client, project, financials)
- Sheet 2: Glass Panels (all spec groups + rows with subtotals)
- Sheet 3: Other Charges (active predefined + wastages + custom)
- Sheet 4: Terms & Conditions (numbered clauses)

Features:
- Branded header with accent colors
- Dark navy column headers matching PDF design
- Zebra-striped rows for readability
- Number formatting (AED amounts, SQM areas)
- Per-spec-group subtotal rows
- Grand total row with accent background
- Auto-fitted column widths

### CSV (.csv) — via CsvHelper
Single flat file with glass panel data + summary totals.
Intended for data import into external systems.

## Architecture

Both services consume PdfExportContext (the same POCO built by
PdfExportContextBuilder from Phase 7.17). This means:
- Zero duplicate data-gathering code
- Consistent data across PDF, Excel, and CSV exports
- Any fix to the context builder benefits all three formats

    PdfExportContextBuilder.Build(VMs...) → PdfExportContext
        ├── ProformaInvoicePdfService  → .pdf  (Phase 7.17)
        ├── InvoiceExcelExportService  → .xlsx (Phase 7.18)
        └── InvoiceCsvExportService    → .csv  (Phase 7.18)

## Default Save Location

    %AppData%\ProGlassAutomation\Exports\

Auto-created on first export.

## Default Filename Format

    {ProformaId}_{IssueDate}.xlsx
    {ProformaId}_{IssueDate}.csv

## Files in This Folder

| File                                | Purpose                              |
|-------------------------------------|--------------------------------------|
| IInvoiceExportService.cs            | Interface                            |
| InvoiceExcelExportService.cs        | ClosedXML Excel builder              |
| InvoiceCsvExportService.cs          | CsvHelper CSV builder                |
| README.md                           | This documentation                   |

## Testing Checklist

- [ ] Click EXCEL button → SaveFileDialog opens with .xlsx filter
- [ ] File saves and opens in Excel
- [ ] Sheet 1 (Summary): all company/client/project/financial fields correct
- [ ] Sheet 2 (Glass Panels): all rows with correct dimensions and values
- [ ] Sheet 2: per-spec subtotal rows with colored backgrounds
- [ ] Sheet 2: grand total row matches Step 7 summary
- [ ] Sheet 3 (Other Charges): only active charges listed
- [ ] Sheet 4 (Terms): all active clauses with bullet sub-lines
- [ ] Click CSV button → SaveFileDialog opens with .csv filter
- [ ] CSV opens in Excel/text editor with correct columns
- [ ] Summary totals at bottom of CSV match Step 7

## Future Enhancements

- Excel import (parse existing Excel → populate wizard)
- CSV import (parse glass panel CSV → populate Step 5 DataGrid)
- Batch export (export all saved invoices at once)
- Template-based Excel (user-designed .xlsx template with placeholder cells)