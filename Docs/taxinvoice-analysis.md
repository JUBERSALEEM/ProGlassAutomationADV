# ProGlass Automation ERP — Tax Invoice Module Analysis

## 1. Existing Delivery Note Analysis
A Delivery Note (DN) is generated from a Job Order. If dispatched with remaining balance, a "Dummy" DN is auto-created for the balance quantity.
- **Database File:** `%AppData%\ProGlassAutomation\deliverynotes.db`
- **Key Tables:** `DeliveryNotes` (headers) and `DeliveryNoteItems` (item rows, mapped from Job Order spec groups).
- **Tax Invoice Placeholders:** `SavedDeliveryNote` already contains placeholder fields:
  - `string TaxInvoiceId`: ID of the generated Tax Invoice.
  - `bool HasTaxInvoice`: Whether a Tax Invoice has been generated from this DN.
- **Status Options:** `Draft`, `Dispatched`, `Delivered`, `Signed`, `Received`.
- **Invoicing Trigger:** Only DNs with status of `Delivered`, `Signed`, or `Received` can be invoiced (`CanGenerateInvoice` in `DeliveryNoteRowViewModel.cs`).

---

## 2. Database Relationship Plan
To support partial invoicing, prevent duplicate invoicing, and link Delivery Notes to Tax Invoices, we will structure the database relationship as follows:

### 2.1 Extension to `deliverynotes.db` (`DeliveryNoteItems` table)
- Add column `InvoicedQty` (integer, default 0) to `DeliveryNoteItem` entity.
- Compute remaining delivered quantity available for invoicing:
  $$\text{RemainingQty} = \text{DeliveredQty} - \text{InvoicedQty}$$
- This prevents duplicate invoicing of the same delivered quantity.

### 2.2 New Database `taxinvoices.db`
Located at `%AppData%\ProGlassAutomation\taxinvoices.db`.

#### Table: `TaxInvoices` (Header)
- `Id` (PK, int, Autoincrement)
- `InvoiceNumber` (string, Unique, indexed) e.g., `INV-2026-001`
- `InvoiceDate` (DateTime)
- `DeliveryNoteId` (string, FK-like link to `DeliveryNotes.DeliveryNoteId`)
- `JobOrderId` (string, Link to parent Job Order)
- `ProformaId` (string, Link to parent Proforma Invoice)
- `CustomerName` (string)
- `ProjectName` (string)
- `CustomerTrn` (string)
- `CompanyTrn` (string)
- `Subtotal` (decimal)
- `Discount` (decimal)
- `TaxableAmount` (decimal)
- `VatAmount` (decimal)
- `GrandTotal` (decimal)
- `Currency` (string, default "AED")
- `ExchangeRate` (decimal, default 1.0)
- `PaymentStatus` (string, default "Unpaid", options: "Unpaid" | "Paid" | "Partially Paid")
- `FullSnapshotJson` (string, JSON serialization blob of the invoice state)
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime)

#### Table: `TaxInvoiceItems` (Line Items)
- `Id` (PK, int, Autoincrement)
- `TaxInvoiceId` (string, FK to `TaxInvoices.InvoiceNumber`)
- `SpecGroupNumber` (int)
- `SpecGroupName` (string)
- `SerialNumber` (int)
- `GlassRef` (string)
- `W1`, `H1`, `W2`, `H2` (int, dimensions)
- `DeliveredQty` (int, the delivered qty on the source DN)
- `InvoicedQty` (int, the qty billed in this Tax Invoice)
- `Rate` (decimal, inherited from JO spec item row)
- `Amount` (decimal, calculated as `Sqm * InvoicedQty * Rate`)
- `Notes` (string)

#### Table: `TaxInvoiceHistory` (Invoice Timeline)
- `Id` (PK, int)
- `InvoiceNumber` (string, FK)
- `Status` (string)
- `EventDate` (DateTime)
- `Notes` (string)

---

## 3. File Structure Plan
Every file will be placed in its exact mandated sub-directory:

### Models
- `Models/TaxInvoice/TaxInvoice.cs`
- `Models/TaxInvoice/TaxInvoiceItem.cs`
- `Models/TaxInvoice/TaxInvoiceSummary.cs`
- `Models/TaxInvoice/TaxInvoiceHistory.cs`

### Services
- `Services/TaxInvoice/Database/TaxInvoiceDbContext.cs`
- `Services/TaxInvoice/Repository/ITaxInvoiceRepository.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceRepository.cs`
- `Services/TaxInvoice/Mapper/TaxInvoiceMapper.cs`
- `Services/TaxInvoice/CalculationService/TaxCalculationService.cs`
- `Services/TaxInvoice/CalculationService/InvoiceCalculationService.cs`
- `Services/TaxInvoice/Export/TaxInvoicePdfService.cs`
- `Services/TaxInvoice/Export/TaxInvoiceExcelService.cs`
- `Services/TaxInvoice/CurrencyService.cs`

### ViewModels
- `ViewModels/TaxInvoice/TaxInvoiceViewModel.cs`
- `ViewModels/TaxInvoice/TaxInvoiceListViewModel.cs`
- `ViewModels/TaxInvoice/TaxInvoiceCreateViewModel.cs`

### Views
- `Views/TaxInvoice/TaxInvoiceView.xaml`
- `Views/TaxInvoice/TaxInvoiceListView.xaml`
- `Views/TaxInvoice/TaxInvoiceCreateView.xaml`

---

## 4. Implementation Plan

### 4.1 Bringing up models and services (Phase 2)
1. Write entity and snapshot classes.
2. Establish DB contexts and repositories. Run schema initialization.
3. Hook database and services in `App.xaml.cs`.
4. Register the Tax Invoice shell ViewModel in navigation.

### 4.2 Conversion Logic & Quantity Tracking (Phase 3)
- **Make Tax Invoice Right-click Menu:** Under `DeliveryNoteListView.xaml.cs`, append right-click options for a Delivery Note row.
- **Full vs Partial Logic:**
  - **Full Conversion:** Auto-populates invoice with remaining quantities available on the DN (`InvoicedQty = DeliveredQty - InvoicedQty`).
  - **Partial Conversion:** Pre-populates invoice, but allows the user to edit `InvoicedQty` (bounded by `0 <= InvoicedQty <= DeliveredQty - PreviousInvoicedQty`).
- **Prevent Duplicate Invoicing:** Every saved invoice commits invoiced quantities back to the `DeliveryNoteItem.InvoicedQty` column. If a DN is fully invoiced (all items `RemainingQty == 0`), `SavedDeliveryNote.HasTaxInvoice` is marked as true.

### 4.3 Multi-Currency Support
- Support currencies: `AED` (Default), `USD`, `EUR`, and others dynamically.
- `CurrencyService` stores symbols and exchange rates to prevent hardcoding.

### 4.4 Dashboard Integration (Phase 4)
- **Tax Invoice sub-page:** Create tabbed Overview with metrics: Total Invoices, Paid Invoices, Pending Invoices, Total Invoiced Amount, and Monthly Chart.
- **Connect Overview:** Feed stats (Total Invoiced, Pending Payment, Recent Activity) into the Main Overview Dashboard.
