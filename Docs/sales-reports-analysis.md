# ProGlassAutomation ERP — Sales Reports & Analytics Analysis

This document describes the complete system analysis, database design, and architecture for the **Sales Reports & Analytics Module**.

---

## 1. Existing Reports Architecture Analysis

The existing reports module (`Views/Reports/SalesReportView.xaml` and `ViewModels/Reports/SalesReportViewModel.cs`) is currently a placeholder pointing to the `ComingSoonView` and `ComingSoonViewModelBase`. To build an enterprise-grade reporting system, we will completely replace this placeholder with a fully-functional, high-performance sales analytics engine.

### Core Data Flows
- **Proforma Invoice (PI):** Standard starting point containing the Customer, Project, Assigned Salesman (`SalesRepresentative`), Base Rates, and item configurations.
- **Job Order (JO):** Contains the accepted glass specifications, rates, and original quantities.
- **Delivery Note (DN):** Tracks actual shipped quantities per item.
- **Tax Invoice (TI):** The legal billing document created from full or partial delivery notes. It calculates subtotal, pre-VAT discount, 5% standard VAT, and grand totals in multiple currencies (AED default).

### The Role of Master Data
- The `masterdata.db` SQLite database stores `SavedSalesman`, `SavedCompany`, `SavedClient`, and `SavedProject` records.
- Salesmen are auto-populated when saving Proforma Invoices. This forms a rich registry we can query to populate drop-downs and autocomplete fields in our reporting filters.

---

## 2. Tax Invoice Data Mapping & Database Schema Relationships

To achieve high-speed analytical queries without duplicating sales data unnecessarily, the Sales Report module will query the existing `taxinvoices.db` database.

We will enrich the `TaxInvoice` database model by adding a `Salesman` column, which speeds up indexing and avoids expensive runtime JSON deserializations.

### Key Fields mapped from Tax Invoice:
- `InvoiceNumber` (string, Unique Key)
- `InvoiceDate` (DateTime)
- `CustomerName` (string)
- `ProjectName` (string)
- `Salesman` (string) — *New indexed column populated from Job Order's Client Representative.*
- `Subtotal` (decimal)
- `Discount` (decimal)
- `TaxableAmount` (decimal)
- `VatAmount` (decimal, standard 5%)
- `GrandTotal` (decimal)
- `Currency` (string, AED default)
- `PaymentStatus` (string: Paid, Unpaid, Partially Paid)

### Database Relationships:
```
[Proforma Invoice (proformainvoices.db)]
      │
      ▼
[Job Order (joborders.db)]  ──► [Master Data (masterdata.db)] (Salesmen, Clients, Projects)
      │
      ▼
[Delivery Note (deliverynotes.db)]
      │
      ▼
[Tax Invoice (taxinvoices.db)]  ◄── [Sales Reports & Analytics (reports)]
```

---

## 3. UI Design Plan

The Reporting UI will integrate seamlessly into the existing ProGlass Dark/Light premium theme, using identical spacing, borders, cards, and DataGrid styles.

The screen will be structured around a **Tabbed Layout**:
1. **Sales Overview (Dashboard):**
   - Row of KPI Cards: Total Sales, Invoiced VAT, Total Payments, Total Outstanding, Invoice Count.
   - Monthly Sales Chart: Displaying billing trends over the last 6 months.
   - Customer Ranking Grid and Salesman performance charts.
2. **Statement Generator (Statement of Account):**
   - Reusable filter bar (Date range, Customer, Project, Company, Salesperson).
   - Detailed billing list with "Generate Statement" (PDF/Excel) and CSV Export features.
3. **Reference Chain Finder:**
   - Input invoice number (e.g. `INV-2026-001`) to view the entire workflow chain (PI -> JO -> DN -> TI) in a clean graphical timeline.

---

## 4. Multi-Currency & UAE FTA Compliance
- Financial calculations default to AED (United Arab Emirates Dirham).
- Currency symbols are fetched dynamically from the global `CurrencyService`.
- 5% VAT is strictly enforced on the taxable subtotal (net of pre-VAT discount) in compliance with UAE FTA regulations.
- Payments and Outstanding amounts are tracked dynamically per invoice.
