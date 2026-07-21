# PDF Export Service — Phase 7.17

Generates a branded A4 PDF file from the proforma invoice wizard using
QuestPDF. The output matches the on-screen A4 preview layout with
professional typography and page numbering.

## Purpose

Users need to share invoices as PDF files — via email, WhatsApp, or
file-sharing platforms. This service produces a print-ready PDF that
exactly matches the reference design, without requiring the user to
manually "Print to PDF" via the system print dialog.

## How It Works

1. User clicks EXPORT PDF button on Step 7 toolbar
2. SaveFileDialog opens with smart default filename (e.g. PI-2026-001_2026-07-21.pdf)
3. PdfExportContextBuilder converts live wizard VMs → PdfExportContext (pure POCO)
4. ProformaInvoicePdfService builds the QuestPDF Document from the context
5. PDF saved to disk
6. User prompted to open the file in default PDF viewer

## Files in This Folder

| File                                | Purpose                                       |
|-------------------------------------|-----------------------------------------------|
| IProformaInvoicePdfService.cs       | Interface for PDF generation                  |
| ProformaInvoicePdfService.cs        | QuestPDF document builder                     |
| PdfExportContext.cs                 | Pure data carrier (no WPF types)              |
| PdfExportContextBuilder.cs          | Converts wizard VMs to PdfExportContext        |
| README.md                           | This documentation                            |

## Architecture

    PrintableInvoiceViewModel
    |
    +-- ExecuteExportPdf()
          |
          +-- PdfExportContextBuilder.Build(VMs...) → PdfExportContext
          |
          +-- ProformaInvoicePdfService.GeneratePdf(ctx, filePath)
                |
                +-- QuestPDF Document.Create()
                      +-- ComposeHeader()
                      +-- ComposeContent()
                      |     +-- ComposeClientBlock()
                      |     +-- ComposeProjectBlock()
                      |     +-- ComposeSpecTable() × N
                      |     +-- ComposeOtherCharges()
                      |     +-- ComposeFinancialSummary()
                      |     +-- ComposeTermsAndConditions()
                      |     +-- ComposeSignatureBlocks()
                      +-- ComposePageFooter()

## Design Decisions

- PdfExportContext is a pure POCO tree with no WPF types. This means
  the PDF service is testable independently and could be reused in a
  server-side API if needed.
- QuestPDF handles pagination automatically — content flows across
  pages with proper page breaks.
- Page numbers ("Page 1 of 3") appear in the footer of every page.
- The company header appears on every page (QuestPDF page.Header).
- Colors match the on-screen A4 preview exactly (same hex values).
- Font is Segoe UI 9pt to match the preview.

## PDF Layout Sections (top to bottom)

1. Company Header (brand name + Proforma badge) — repeated per page
2. Client + Project blocks — side by side
3. Specification tables — one per spec group with row data + subtotals
4. Other Charges — active predefined + wastages + custom
5. Financial Summary — subtotal → discount → VAT → grand total band
6. Terms & Conditions — active clauses with bullets
7. Footer Disclaimer + Terms text
8. Prepared By + Client Signature blocks
9. Page footer — "Page X of Y" centered

## QuestPDF License

Uses QuestPDF Community license (free for companies under $1M revenue).
Set via:

    QuestPDF.Settings.License = LicenseType.Community;

## Default Save Location

    %AppData%\ProGlassAutomation\PDFs\

Auto-created on first export. User can change via SaveFileDialog.

## Default Filename Format

    {ProformaId}_{IssueDate}.pdf

Example: PI-2026-001_2026-07-21.pdf

Special characters (/, \, :) are replaced with dashes for file-system safety.

## Testing Checklist

- [ ] Click EXPORT PDF → SaveFileDialog opens
- [ ] Default filename matches ProformaId + date
- [ ] Click Save → PDF generated without errors
- [ ] PDF opens in default viewer
- [ ] Company header appears on every page
- [ ] Page numbers appear on every page footer
- [ ] Client + Project blocks show correct data from Steps 1-3
- [ ] Spec tables show all rows with correct dimensions + values
- [ ] Other charges show only active charges
- [ ] Financial summary shows subtotal → discount → VAT → grand total
- [ ] Terms & Conditions show all active clauses with bullet sub-lines
- [ ] Prepared By shows Step 3 DocumentPreparedBy (or override)
- [ ] Status shows Step 1 ProformaStatus (or override)
- [ ] Signature lines are clean and printable
- [ ] Multi-page invoice flows correctly (no clipping)
- [ ] Empty invoice (no data) → PDF generates with placeholders
- [ ] Grand total in PDF matches Step 7 summary card

## Future Enhancements

- Phase 7.18 — Excel/CSV Import/Export
- Phase 7.20 — Send Email with PDF attachment
- Company logo image in header
- Watermark for Draft status
- Digital signature field
- QR code linking to online invoice portal
- Multi-language PDF (English + Arabic side by side)