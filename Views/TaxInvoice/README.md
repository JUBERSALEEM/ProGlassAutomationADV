# ProGlass Automation - Tax Invoice Module

## Version
Chat 16 - Family-grouped list plus Invoice Family sidebar

## Author
Juber Saleem (jubersaleem@gmail.com)

## Stack
- WPF (.NET 8)
- MVVM (ViewModelBase + RelayCommand)
- SQLite + EF Core 8
- QuestPDF 2024.12.2
- Currency: AED (UAE)
- VAT: 5 percent (UAE standard)

---

## Module Overview

The Tax Invoice module handles UAE VAT-compliant invoicing with support
for partial invoicing. This is the industry-standard practice where a
single Proforma Invoice (PI) can be billed in multiple installments as
deliveries are made.

---

## Key Concept - The Invoice Family

Every Tax Invoice belongs to a family identified by its ProformaId.
A family contains:

- 1 Master Invoice (numbered INV-YYYY-NNN) - usually the first invoice
- 0 to N Partial Invoices (numbered INV-YYYY-NNN-A, -B, -C) - subsequent billings

Example:

    PI-2026-001 (customer: ABC Trading, total: AED 100,000)
        INV-2026-001      (Master)   AED 40,000  - first partial delivery
        INV-2026-001-A    (Partial)  AED 35,000  - second partial delivery
        INV-2026-001-B    (Partial)  AED 25,000  - final delivery
                                     ----------
                          Family Total: AED 100,000 (fully invoiced)

---

## UI Design Philosophy

The Tax Invoice module mirrors the Delivery Note module pattern:

- List Page shows ONE row per PI family (not per invoice)
- Detail Page shows one invoice plus sidebar with ALL family members
- Click a family member in sidebar to load that invoice plus its attachments

This matches how UAE glass factories think about invoicing:
Show me all invoices for this customers job, not a flat list of
every invoice ever created.

---

## Workflow - Creating Tax Invoices

### Flow 1: Master Invoice (aggregate all delivered qty)

    DN List - JO group row - Click Master Invoice button
    Confirmation dialog - OK
    Tax Invoice module opens
    Detail view shows aggregated qty from ALL sibling DNs for that PI
    User adjusts if needed then Save
    Invoice saved as INV-YYYY-NNN (if first) or INV-YYYY-NNN-A/B/C (if partial)

Use case: Customer wants a single consolidated invoice covering
all deliveries made so far.

### Flow 2: Convert Single DN (per-DN partial invoice)

    DN Detail view - History sidebar - Right-click a DN entry
    Select Convert to Tax Invoice
    Tax Invoice detail opens with ONLY that DNs delivered qty
    User adjusts if needed then Save
    Invoice saved as partial (A/B/C sequence)

Use case: Each delivery gets its own invoice. Most common workflow
for progress billing.

### Flow 3: New Invoice from PI Picker

    Tax Invoice List - New Invoice button
    Proforma Invoice picker dialog opens
    User selects PI
    If deliveries exist then Master mode (aggregated delivered qty)
    If no deliveries yet then Full PI import (advance invoicing)

Use case: Creating an invoice without going through DN module first.

---

## Detail View - Layout

The detail view has a right sidebar showing all family members:

    ACTION BAR: Back, Save, Issue, Mark Paid, PDF, Delete

    ROW 2 (side by side):
        LEFT:  Financial Stats (Subtotal, VAT, Discount, Grand Total)
        RIGHT: Attachments (switches per family member click)

    ROW 3 (side by side):
        LEFT (3 star width):
            Hero strip (Customer, Project, PI, TRN, Grand Total)
            Header form (4 rows of fields)
            Glass Items (scrollable spec groups with rows)

        RIGHT (1 star width, min 350px):
            INVOICE FAMILY sidebar
                Header: Invoice Family (X invoices)
                Scrollable list of family members
                    Each entry shows:
                        Invoice number
                        Type (Full/Partial A/B/C)
                        Status pill (Draft/Issued/Paid/Overdue)
                        Issue date
                        Grand total
                        Attachment count badge
                        Green highlight = currently editing
                Family Totals (fixed at bottom):
                    PI Total
                    Invoiced
                    Balance

### Family sidebar interactions

- Click a family member to load that invoice into main area
- Its attachments appear in the Attachments panel
- Green highlight moves to the clicked entry
- Family totals stay constant (they reflect the whole family)

### Attachments panel behavior

- Header dynamically shows which family members attachments are displayed
- Warning banner appears when viewing a different family members attachments
- Add and Remove attachments always affect the currently-loaded invoice

---

## List Page - Layout

    Tax Invoices                              New Invoice button
    One row per Proforma Invoice

    KPI Cards: Total Invoiced, Draft Families, Issued, Paid, Overdue

    Filters: Search text, Status filter, Clear, Refresh

    Tabs: ALL FAMILIES, HAS DRAFT, HAS ISSUED, FULLY PAID

    Each row shows:
        PI number (e.g. PI-2026-001)
        Invoice count badge (e.g. 3 invoices)
        Family status badge (e.g. PartiallyInvoiced)
        Overdue warning (if any member overdue)
        Customer name and Project name
        Latest invoice info (date, number, status)
        Progress bar (percent invoiced)
        Invoiced total and Balance
        Open button

### Tab filtering logic

- ALL FAMILIES: every PI family
- HAS DRAFT: families where at least one invoice is Draft
- HAS ISSUED: families where at least one invoice is Issued
- FULLY PAID: families where ALL invoices are Paid

### Row interactions

- Single click: select family (highlights with indigo border)
- Double click: open family (opens latest invoice in detail)
- Right click: context menu (Open Latest, family info, Refresh)
- Green border: fully paid family
- Red border: has overdue invoices

---

## Database

- File: %AppData%\ProGlassAutomation\taxinvoices.db
- Tables:
    - TaxInvoices: one row per invoice (header plus JSON snapshot)
    - TaxInvoiceItems: normalized glass item rows
    - TaxInvoiceAttachments: file blobs (byte array in DB)

Family grouping is computed in-memory from ProformaId. No separate
family table exists.

---

## PDF Output

Professional A4 PDF via QuestPDF with:

- Company letterhead (name, TRN, location)
- Full-width metadata strip (Invoice No, Issue Date, Due Date, Status)
- Supplier and Customer blocks
- References bar (PI, DN, PO, Payment Terms)
- Spec group tables with column-aligned totals
- Financial summary (Subtotal, Discount, VAT 5 percent, Grand Total)
- Amount in words (AED)
- Bank details
- Signature blocks (Prepared By plus Customer Acknowledgement)
- Page numbers

---

## Cross-Module Integration

From DN List Master Invoice button:
    Goes to Tax Invoice Detail
    Uses TaxInvoiceVM.CreateFromProformaAsync(piId)
    Aggregates all sibling DNs delivered qty

From DN Detail Convert button:
    Goes to Tax Invoice Detail
    Uses TaxInvoiceVM.CreateFromDeliveryNoteAsync(dnId)
    Single DN qty only

From PI Picker (Tax Invoice list New Invoice):
    Goes to Tax Invoice Detail
    Master if deliveries exist, else full PI import

From Dashboard KPI cards:
    Goes to Tax Invoice List
    Navigate to specific tab (Draft, Issued, Overdue)

---

## File Structure

    Models/TaxInvoice/
        SavedTaxInvoice/
            SavedTaxInvoice.cs          - EF entity plus TaxInvoiceItem
            TaxInvoiceSnapshot.cs       - JSON snapshot POCO
        TaxInvoiceFamily/
            TaxInvoiceFamily.cs         - computed family model
        Attachment/
            TaxInvoiceAttachment.cs     - byte array blob attachment entity

    Services/TaxInvoice/
        Database/
            TaxInvoiceDbContext.cs       - EF Core context
        Repository/
            ITaxInvoiceRepository.cs    - contract
            TaxInvoiceRepository.cs     - implementation
        Mapper/
            TaxInvoiceMapper.cs         - VM to Snapshot to Entity
        Pdf/
            TaxInvoicePdfDocument.cs    - QuestPDF layout
            TaxInvoicePdfExporter.cs    - PDF generation plus save

    ViewModels/TaxInvoice/
        TaxInvoiceViewModel.cs          - shell (list to detail toggle)
        TaxInvoiceList/
            TaxInvoiceListViewModel.cs
            TaxInvoiceListRowViewModels.cs - row plus family VMs
        TaxInvoiceDetail/
            TaxInvoiceDetailViewModel.cs
            InvSpecGroupViewModel.cs    - spec group plus rows
            InvAttachmentViewModel.cs

    Views/TaxInvoice/
        TaxInvoiceView.xaml             - shell (list to detail toggle)
        TaxInvoiceList/
            TaxInvoiceListView.xaml
            ProformaPickerDialog.xaml   - PI picker
        TaxInvoiceDetail/
            TaxInvoiceDetailView.xaml   - includes family sidebar

---

## Development History

    Chat 14: Initial Tax Invoice module (22 files)
    Chat 14+: Bug fixes - partial invoicing, App.TaxInvoices, PDF layout
    Chat 15: Dashboard integration (KPI cards, widgets, navigation)
    Chat 16: Family-grouped list plus Invoice Family sidebar plus attachment switching

---

## Invoice Statuses

    Draft          - Invoice created but not yet sent to customer
    Issued         - Invoice sent to customer, awaiting payment
    Paid           - Customer has paid in full
    PartiallyPaid  - Customer has made partial payment
    Overdue        - Payment past due date and not yet paid
    Cancelled      - Invoice cancelled (not billed)
    Void           - Invoice voided (was issued but invalidated)

---

## Business Rules

    SQM Calculation:
        Panel SQM = (Width mm x Height mm) / 1,000,000
        Minimum billable SQM per panel = 0.5 sqm
        If actual SQM is less than 0.5 then bill as 0.5

    VAT: 5 percent UAE standard, applied AFTER discount
    TRN: 15 digits exactly (Tax Registration Number)
    Currency: AED only (UAE Dirhams)

    Invoice Numbering:
        Full invoice:    INV-YYYY-NNN       (e.g. INV-2026-001)
        Partial invoice: INV-YYYY-NNN-A     (e.g. INV-2026-001-A)
        Derived from ProformaId: PI-2026-001 becomes INV-2026-001

    Partial Invoicing:
        Each delivery note can generate its own partial invoice
        Master invoice aggregates all delivered qty across sibling DNs
        Rows with 0 delivered qty are excluded from invoice
        Rate comes from PI, Qty comes from DN (delivered qty)

---

## Known Limitations

    1. PDF attachment merging requires PdfSharp package (not yet added)
    2. Statement generator (combined PI plus DN plus Invoice PDF) is future work
    3. Currency other than AED is not supported (UAE-only app)
    4. Payment tracking is status only (no payment log or ledger)
    5. Email sending is not implemented (manual PDF sharing)

---

## Support

    Developer: Juber Saleem
    Email: jubersaleem@gmail.com
    Business context: UAE glass fabrication factories
    Standards: UAE VAT 5 percent, TRN 15 digits, AED currency

---

End of Tax Invoice Module Documentation.