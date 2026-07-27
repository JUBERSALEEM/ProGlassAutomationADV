# ProGlass Automation — Sales Report & Account Statement Module

**Module:** Sales Report / Account Statement
**Chat:** 16
**Developer:** Juber Saleem
**Architecture:** Claude (Senior WPF Architect)

---

## WHAT IS THIS MODULE?

This module lets you generate professional **Account Statements** for
your customers. Think of it like a bank statement — but for your glass
business. It shows all invoices issued to a customer, how much they
owe, what is overdue, and a full aging analysis.

You can generate:

- A **summary statement PDF** (2-3 pages)
- A **combined PDF** that includes the statement PLUS all signed
  documents (signed PI, signed delivery notes) merged into one file

Everything is saved so you can re-open, re-print, or re-send at
any time.

---

## HOW IT WORKS — STEP BY STEP GUIDE

### STEP 1 — Open the Module

```
Click the hamburger menu (top left)
→ Scroll to REPORTS section
→ Click "Sales Report"
```

You will see the **Saved Statements List** — all statements you have
generated before. On first use this will be empty.

---

### STEP 2 — Create a New Statement

```
Click "+ New Statement" button (top right of list page)
```

You will enter the **Statement Generator** — split into two panels:

```
LEFT PANEL  → Set your filters (who, when, what status)
RIGHT PANEL → Preview results appear here
```

---

### STEP 3 — Set Your Filters

You do not need to fill all filters. Use what makes sense.

#### Search Filters
| Filter | What it does |
|---|---|
| **Company** | Your company name (e.g. "Alpha Glass LLC") |
| **Customer** | The client you are generating the statement for |
| **Project** | Filter by a specific project name |
| **Salesman** | Filter by who prepared the invoices |

#### Date Range
```
From: start date of the period you want
To:   end date of the period you want

Example: From 01 Jan 2026   To 30 Jun 2026
         This shows all invoices issued in that 6-month period
```

#### Invoice Status
```
Check which invoice statuses to include:
  ✅ Issued        — sent to customer, awaiting payment
  ✅ Paid          — fully paid
  ✅ Partially Paid — partially settled
  ✅ Overdue       — past the due date, not paid
  ☐  Draft         — not yet issued (usually excluded)
  ☐  Cancelled     — cancelled invoices
  ☐  Void          — voided invoices
```

By default, Issued + Paid + Partially Paid + Overdue are selected.
This gives you the active account picture.

#### PDF Options
```
☐ Include Signed Documents
     When checked: the PDF will include signed PI and signed DN
     documents appended after the statement pages.
     Useful for sending a complete package to the customer.

☐ Show DN Breakdown
     When checked: expands delivery note detail rows.
```

---

### STEP 4 — Preview

```
Click "🔍 Preview" button
```

The right panel will populate with:

1. **Aging Analysis Strip** — 5 boxes showing how much money is in
   each aging bucket (Current, 0-30 days, 31-60 days, 61-90 days, 90+ days)

2. **Invoice List** — every matching invoice as a card showing:
   - Invoice number + date
   - PI reference + DN reference
   - Grand Total + Balance
   - Status (colour-coded)

3. **KPI Footer** — summary totals:
   - Total Invoiced | VAT (5%) | Outstanding | Overdue

If no invoices match your filters, you will see a message:
```
"No invoices match the selected filters. Try adjusting your criteria."
```

---

### STEP 5 — Generate the PDF

```
Click "📄 Generate PDF" button
```

The system will:

```
1. Generate the statement PDF (QuestPDF — A4 portrait)
2. If "Include Signed Documents" was checked:
     → Load Tax Invoice PDFs for each invoice
     → Load SignedPI attachments
     → Load SignedDN attachments
     → Merge everything into one combined PDF (PdfSharp)
3. Save the PDF to your computer:
     %AppData%\ProGlassAutomation\Statements\
4. Open the PDF automatically in your default PDF viewer
5. Save the statement record to the database
6. Return you to the Saved Statements List
```

---

### STEP 6 — View Your Saved Statements

Back on the list page you will see your statement with:

```
STMT-2026-001 — Customer Name
Invoiced: AED 125,000.00  |  Outstanding: AED 45,000.00  |  Overdue: AED 12,000.00
5 invoice(s)  ·  Generated 15 Jun 2026  10:30
```

From here you can:

| Button | What it does |
|---|---|
| **📄 Open PDF** | Opens the saved PDF in your PDF viewer |
| **✏ Edit** | Re-opens the generator with the same filters so you can re-run |
| **🗑 Delete** | Removes the statement record AND the PDF file |

---

### STEP 7 — Dashboard

```
Click Dashboard from the menu
→ Click the "Statements" tab (7th tab, 📊 icon)
```

Here you will see:

```
4 KPI Cards:
  📋 STATEMENTS     — total statements generated
  💰 TOTAL INVOICED — sum across all statements
  ⏳ OUTSTANDING    — total outstanding balance
  🔴 OVERDUE        — total overdue amount (red when > 0)

Quick Actions:
  + New Statement → jumps straight to generator
  View All Statements → opens the full list

Recent Statements:
  Last 5 statements with key financials at a glance
```

---

## AGING ANALYSIS EXPLAINED

Aging analysis answers the question:
**"How old is the money my customers owe me?"**

```
CURRENT      Money not yet past the due date       (healthy)
0-30 DAYS    1 to 30 days overdue                  (watch)
31-60 DAYS   31 to 60 days overdue                 (chase)
61-90 DAYS   61 to 90 days overdue                 (urgent)
90+ DAYS     More than 90 days overdue             (critical)
```

**Based on Due Date** (not invoice date) — UAE Accounts Receivable
standard. The due date is calculated from the payment terms set on
each Tax Invoice (e.g. Net 30, Net 60).

---

## PDF DOCUMENT STRUCTURE

### Statement Only (2-3 pages)

```
┌─────────────────────────────────────────────┐
│  ACCOUNT STATEMENT          STMT-2026-001   │  ← Header bar
│  Customer Statement — Alpha Glass LLC        │
│  Period: 01 Jan 2026 to 30 Jun 2026          │
├─────────────────────────────────────────────┤
│  FROM (your company)  │  BILL TO (customer) │  ← Info block
├─────────────────────────────────────────────┤
│  INVOICE SUMMARY TABLE                      │
│  Invoice No | Date | PI | DN | Qty | Total  │  ← All invoices
│  Balance | Status                           │
├─────────────────────────────────────────────┤
│  FINANCIAL SUMMARY       │  TOTAL           │
│  Invoiced: AED 125,000   │  OUTSTANDING     │
│  VAT:      AED   5,952   │  AED 45,000      │
│  Paid:     AED  80,000   │                  │
│  Overdue:  AED  12,000   │  OVERDUE         │
│                          │  AED 12,000      │
├─────────────────────────────────────────────┤
│  AGING ANALYSIS                             │
│  [Current] [0-30d] [31-60d] [61-90d] [90+] │  ← Bar chart
├─────────────────────────────────────────────┤
│  PREPARED BY    │  CUSTOMER ACKNOWLEDGEMENT │  ← Signatures
└─────────────────────────────────────────────┘
```

### Combined PDF (when Include Signed Documents is checked)

```
Pages 1-3   : Statement summary (above)
Pages 4-N   : Tax Invoice 1 — full PDF
Pages N+1   : Tax Invoice 1 — Signed PI document
Pages N+2   : Tax Invoice 1 — Signed DN document
Pages N+3.. : Tax Invoice 2 — full PDF
              Tax Invoice 2 — Signed PI document
              Tax Invoice 2 — Signed DN document
              ... repeats for every invoice in the statement
```

---

## WHERE ARE THE FILES SAVED?

```
Statements database:
  %AppData%\ProGlassAutomation\statements.db

PDF files:
  %AppData%\ProGlassAutomation\Statements\STMT-2026-001.pdf
  %AppData%\ProGlassAutomation\Statements\STMT-2026-001-combined.pdf
```

To find `%AppData%` on your computer:
```
Press Windows key + R
Type: %AppData%
Press Enter
Navigate to: ProGlassAutomation → Statements
```

---

## STATEMENT NUMBERING

```
Format  : STMT-YYYY-NNN
Example : STMT-2026-001
          STMT-2026-002
          STMT-2026-003

The number resets to 001 on January 1 each year.
STMT-2026-050 → next year becomes STMT-2027-001
```

---

## PROJECT FOLDER STRUCTURE

```
ProGlassAutomation/
│
│   Everything for the Reports module lives in these folders:
│
├── Models/
│   └── Statement/                    ← Data definitions
│       │
│       ├── StatementFilter.cs
│       │     What it is : The filter form — Company, Customer,
│       │                  Project, Salesman, Dates, Statuses
│       │     Think of it: Like a search form that the user fills in
│       │
│       ├── StatementSnapshot.cs
│       │     What it is : A complete saved copy of all computed data
│       │                  (totals, aging, invoice rows, customer info)
│       │     Think of it: A photograph of the statement at the moment
│       │                  it was generated — used to re-print the PDF
│       │                  without re-running all the calculations
│       │
│       ├── SavedStatement.cs
│       │     What it is : The database record for one saved statement
│       │                  (header info, filter used, totals, PDF path)
│       │     Think of it: The index card in a filing cabinet —
│       │                  lightweight, fast to search
│       │
│       └── StatementLineItem.cs
│             What it is : One invoice row stored per statement
│                          (invoice number, amounts, dates, status)
│             Think of it: Each line on the index card
│
├── Services/
│   └── Statement/                    ← Business logic
│       │
│       ├── Database/
│       │   └── StatementDbContext.cs
│       │         What it is : Connects the app to statements.db
│       │         Think of it: The bridge between the app and the file
│       │                      on your hard drive
│       │
│       ├── Repository/
│       │   ├── IStatementRepository.cs
│       │   │     What it is : The list of all database operations
│       │   │                  (save, load, delete, search)
│       │   │     Think of it: A menu of what the database can do
│       │   │
│       │   └── StatementRepository.cs
│       │         What it is : The actual code that does the database
│       │                      operations (the kitchen behind the menu)
│       │         Think of it: The chef who reads the menu and cooks
│       │
│       ├── Aggregation/
│       │   └── StatementAggregator.cs
│       │         What it is : The engine that collects data from
│       │                      Tax Invoices, Delivery Notes, and
│       │                      Proforma Invoices and combines them
│       │         Think of it: An accountant who pulls records from
│       │                      3 different filing cabinets and
│       │                      produces a summary report
│       │
│       └── Pdf/
│           ├── StatementPdfDocument.cs
│           │     What it is : The layout designer for the PDF —
│           │                  header, table, financial summary,
│           │                  aging bars, signature blocks
│           │     Think of it: The template/design of the printed page
│           │
│           └── StatementPdfExporter.cs
│                 What it is : Saves the PDF to disk and merges
│                              multiple PDFs into one combined file
│                 Think of it: The printer that assembles and prints
│                              all the pages in the right order
│
├── ViewModels/
│   │   These files contain the logic that powers the screens.
│   │   They are NOT the screens themselves — they are the brain
│   │   behind each screen.
│   │
│   ├── Dashboard/
│   │   └── Statement/
│   │       └── StDashboardViewModel.cs
│   │             What it is : Powers the Statements tab on the
│   │                          Dashboard — loads KPIs and recent list
│   │             Think of it: The data provider for the dashboard tile
│   │
│   └── Reports/
│       │
│       ├── SalesReportViewModel.cs
│       │     What it is : The main controller — decides whether to
│       │                  show the list page or the generator page
│       │     Think of it: The receptionist who sends you to the
│       │                  right room
│       │
│       ├── StatementList/
│       │   ├── StatementListViewModel.cs
│       │   │     What it is : Powers the saved statements list —
│       │   │                  search, KPI cards, open/edit/delete
│       │   │     Think of it: The brain behind the filing cabinet view
│       │   │
│       │   └── StatementListRowViewModel.cs
│       │         What it is : One row in the list — formats numbers,
│       │                      colours, and badges for display
│       │         Think of it: One index card formatted for display
│       │
│       └── StatementGenerator/
│           ├── StatementGeneratorViewModel.cs
│           │     What it is : Powers the left filter panel —
│           │                  dropdowns, checkboxes, date pickers,
│           │                  Preview button, Generate button
│           │     Think of it: The brain behind the search form
│           │
│           └── StatementGeneratorViewModel2.cs
│                 What it is : Powers the right preview panel —
│                              invoice list, aging bars, KPI footer
│                 Think of it: The brain behind the results area
│
├── Views/
│   │   These files ARE the actual screens the user sees.
│   │   Each .xaml file is the visual design.
│   │   Each .xaml.cs file is the minimal code connecting it.
│   │
│   ├── Dashboard/
│   │   └── Statement/
│   │       ├── StDashboardView.xaml
│   │       └── StDashboardView.xaml.cs
│   │             What it is : The Statements tab screen on Dashboard
│   │             Shows      : 4 KPI cards + quick actions + recent 5
│   │
│   └── Reports/
│       │
│       ├── SalesReportView.xaml
│       ├── SalesReportView.xaml.cs
│       │     What it is : The outer shell — just switches between
│       │                  list view and generator view
│       │
│       ├── StatementList/
│       │   ├── StatementListView.xaml
│       │   └── StatementListView.xaml.cs
│       │         What it is : The saved statements list screen
│       │         Shows      : KPI cards, search bar, statement cards,
│       │                      open/edit/delete buttons per row
│       │
│       ├── StatementGenerator/
│       │   ├── StatementGeneratorView.xaml
│       │   └── StatementGeneratorView.xaml.cs
│       │         What it is : The statement generator screen
│       │         Left panel : Filter cards (search, dates, status, options)
│       │         Right panel: Aging strip, invoice preview list, KPI footer
│       │
│       ├── README.md                 ← This file
│       └── PROJECT_STRUCTURE.md     ← Detailed technical structure
│
└── (Modified existing files)
    ├── App.xaml.cs                   ← Registered statements.db
    ├── ViewModels/Dashboard/
    │   └── DashboardViewModel.cs     ← Added 7th Statements tab
    ├── Views/Dashboard/
    │   └── DashboardView.xaml        ← Added Statements DataTemplate
    └── MainWindow.xaml               ← Rearranged sidebar menu groups
```

---

## HOW THE DATA FLOWS — VISUAL GUIDE

```
┌─────────────────────────────────────────────────────────────┐
│                     USER SETS FILTERS                        │
│         Company / Customer / Project / Dates / Status        │
└───────────────────────────┬─────────────────────────────────┘
                            │ clicks Preview
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  STATEMENT AGGREGATOR                        │
│                                                             │
│  1. Load all Tax Invoices (lightweight — no JSON parsing)   │
│  2. Apply filters in memory (fast)                          │
│  3. If Salesman filter: read JSON snapshots (slower)        │
│  4. Count attachments per invoice (one batch query)         │
│  5. Check attachment types — SignedPI? SignedDN?             │
│  6. Calculate DaysOverdue per invoice                       │
│  7. Sum up financial totals                                 │
│  8. Sort into 5 aging buckets                               │
└───────────────────────────┬─────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    PREVIEW SHOWN ON SCREEN                   │
│                                                             │
│  Aging Strip: Current | 0-30d | 31-60d | 61-90d | 90+d     │
│  Invoice List: each invoice as a card with amounts          │
│  KPI Footer: Invoiced | VAT | Outstanding | Overdue         │
└───────────────────────────┬─────────────────────────────────┘
                            │ clicks Generate PDF
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                     PDF GENERATION                           │
│                                                             │
│  Statement PDF (QuestPDF):                                  │
│    Header → Customer Info → Invoice Table →                 │
│    Financial Summary → Aging Chart → Signatures             │
│                                                             │
│  If "Include Signed Documents" checked (PdfSharp merge):    │
│    [Statement pages]                                        │
│    + [Invoice 1 PDF] + [Signed PI] + [Signed DN]           │
│    + [Invoice 2 PDF] + [Signed PI] + [Signed DN]           │
│    + ... repeats per invoice                                │
└───────────────────────────┬─────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      SAVED TO DISK                           │
│                                                             │
│  PDF: %AppData%\ProGlassAutomation\Statements\              │
│       STMT-2026-001.pdf                                     │
│       STMT-2026-001-combined.pdf (if attachments included)  │
│                                                             │
│  Database: statements.db                                    │
│    Record: STMT-2026-001 with all filter criteria saved     │
│    so you can re-run the same report any time               │
└───────────────────────────┬─────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              PDF OPENS AUTOMATICALLY                         │
│         Returns to Saved Statements List                     │
└─────────────────────────────────────────────────────────────┘
```

---

## WHAT DATA DOES IT PULL FROM?

```
┌─────────────────────────────────────────────────────────────┐
│                     DATA SOURCES                             │
├──────────────────┬──────────────────────────────────────────┤
│  Tax Invoice DB  │  Invoice numbers, amounts, VAT, status,  │
│  (taxinvoices.db)│  due dates, customer names, attachments  │
│                  │  (SignedPI, SignedDN byte files in DB)    │
├──────────────────┼──────────────────────────────────────────┤
│  Master Data DB  │  Company names, customer names, project  │
│  (masterdata.db) │  names, salesman names — powers the      │
│                  │  filter dropdowns                        │
├──────────────────┼──────────────────────────────────────────┤
│  Statements DB   │  Saved statement records + line items    │
│  (statements.db) │  PDF file paths, filter criteria         │
│   ← NEW in Ch16  │  for re-run and history                  │
└──────────────────┴──────────────────────────────────────────┘

Note: Delivery Note data is cross-referenced via the
DeliveryNoteReference field stored on each Tax Invoice.
The Delivery Note database is NOT directly queried by
the statement generator — Tax Invoice carries all needed references.
```

---

## COMMON USE CASES

### Monthly Customer Statement
```
Customer : "Al Futtaim Construction"
Date From: 01 May 2026
Date To  : 31 May 2026
Status   : Issued + Paid + Overdue
Options  : Include Signed Documents = ON

Result: Full statement for May with all invoices,
        signed documents appended. Send to customer.
```

### Overdue Chase Report
```
Customer : (leave blank — all customers)
Date From: (leave blank — all time)
Status   : Overdue only

Result: All overdue invoices across all customers.
        Aging analysis shows how long each has been overdue.
```

### Project Completion Summary
```
Project  : "Burj Al Arab Renovation Phase 2"
Status   : All statuses

Result: Complete financial picture for one project —
        total invoiced, total paid, outstanding balance.
```

### Annual Sales Report by Salesman
```
Salesman : "Ahmed Al Rashidi"
Date From: 01 Jan 2026
Date To  : 31 Dec 2026
Status   : Issued + Paid

Result: Full year sales report for one salesman.
```

---

## KNOWN LIMITATIONS

| Limitation | Impact | Future Fix |
|---|---|---|
| Partially Paid invoices show full balance | Outstanding may be overstated | Add PaidAmount field to Tax Invoice |
| Salesman filter is slower on large datasets | Acceptable up to ~500 invoices | Add indexed salesman column to Tax Invoice |
| DN breakdown rows not expanded in PDF | Statement table shows invoice level only | Future chat enhancement |
| PI attachments not included | Only Tax Invoice attachments merged | Future chat — add PI attachment support |

---

## DEPENDENCIES

| Package | Version | What it does |
|---|---|---|
| QuestPDF | 2024.12.2 | Designs and generates the statement PDF pages |
| PdfSharp-WPF | 6.1.1 | Merges multiple PDFs into one combined document |
| SkiaSharp | 2.88.8 | Draws the aging analysis bar chart inside the PDF |
| SkiaSharp.Views.WPF | 2.88.8 | WPF compatibility for SkiaSharp |
| EF Core SQLite | 8.0.10 | Saves and loads statements from statements.db |

---

*Built in Chat 16 — ProGlass Automation*
*Developer: Juber Saleem | Architecture: Claude Senior WPF Architect*