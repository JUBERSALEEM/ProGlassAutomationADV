# ProGlass Automation — Product Requirements Document

**Version:** 2.0 (merged authoritative version)
**Owner:** Juber Saleem (jubersaleem@gmail.com)
**Last updated:** 2026
**Status:** Living document — updated with each phase

---

## 1. Product Vision

ProGlass Automation is a Windows desktop ERP application for UAE-based
glass manufacturing companies. It manages the complete production
workflow from raw material inventory through cutting, processing,
tempering, and delivery — with integrated invoicing, job tracking,
and reporting.

The system replaces spreadsheet-driven processes with software that is:

- Modular (per-feature sub-folders, isolated databases)
- Deterministic (pure calculation formulas, no hidden state)
- Testable (repository pattern, MVVM boundaries)
- AI-safe (documented architecture, strict conventions)
- Extensible (add modules without breaking existing ones)

---

## 2. Target Users

- **Glass factory estimators** — create proforma invoices
- **Production managers** — track job orders through factory stages
- **Warehouse staff** — manage raw glass sheet inventory
- **Accounts team** — generate tax invoices, track payments
- **Management** — dashboard KPIs, sales reports

---

## 3. Technology Stack

| Layer            | Technology                                   |
|------------------|----------------------------------------------|
| Platform         | Windows 10/11 desktop                        |
| UI framework     | WPF (.NET 8, C#)                             |
| Pattern          | MVVM (ViewModelBase + RelayCommand)          |
| Database         | SQLite + Entity Framework Core 8             |
| PDF generation   | QuestPDF 2024.12.2 (Community license)       |
| Excel export     | ClosedXML 0.105.0                            |
| CSV export       | CsvHelper 33.1.0                             |
| AI Assistant     | Google Gemini (gemini-flash-latest)          |
| Themes           | Custom Light/Dark with 25-brush palette      |

---

## 4. Business Domain

### 4.1 Workflow

    Raw Sheet Purchase
         ↓
    Sheet Store (inventory)
         ↓
    Proforma Invoice (quote to client)
         ↓
    Job Order (approved for production)
         ↓
    Cutting → Holes/Cutouts → Polish → Edge Deletion
              → Mitring → Tempering → Pairing → Doubling/Lamination
         ↓
    Delivery Note (to site)
         ↓
    Tax Invoice (final billing)

### 4.2 Currency + Tax

- All monetary values in **AED** (UAE Dirham) — never INR, never USD
- VAT rate: **5%** (toggleable per invoice for zero-rated exports)
- Currency format: `AED 12,345.67` (comma thousands, 2 decimals)

### 4.3 Glass Types

| Code    | Full name              | Structure           | Perimeter Factor |
|---------|------------------------|---------------------|------------------|
| SGU     | Single Glazed Unit     | 1 pane              | 2                |
| DGU     | Double Glazed Unit     | 2 panes + air gap   | 2                |
| LAM     | Laminated              | 2 panes + PVB film  | 4 (all sides)    |
| DGU+LAM | Combined DGU + LAM     | 3 panes             | 2                |

### 4.4 Key Calculations

See `data/d04-calculations.md` for full formulas with worked examples
and edge cases.

Summary:

- **Area (SQM):** `(Width × Height) / 1,000,000`
- **Minimum billable SQM per piece:** `0.5 sqm`
- **LM for SGU/DGU:** `(W + H) / 1000 × 2 × Qty`
- **LM for LAM:** `(W + H) / 1000 × 4 × Qty`
- **Rate auto-adjust:** SQM ≥ 4 → EffectiveRate, SQM < 4 → BaseRate
- **VAT:** `NetSubtotal × 5%`

---

## 5. Sidebar Navigation Groups

The hamburger menu organizes modules into 5 groups:

| Group             | Modules                                              |
|-------------------|------------------------------------------------------|
| MAIN              | Dashboard, About                                     |
| INVENTORY         | Sheet Store                                          |
| SALES & REPORTS   | Proforma Invoice, Tax Invoice, Sales Report          |
| PRODUCTION        | Job Order, Cutting, Holes/Cutouts, Polish,           |
|                   | Edge Deletion, Mitring, Tempering, Optimization      |
| DISPATCH          | Pairing, Doubling/Lamination, Delivery Note          |

---

## 6. Feature Module Map

### 6.1 COMPLETED MODULES

#### Module 1 — App Shell ✅

- Animated full-screen splash screen (light theme, green accent)
    - Custom glass-stack logo (3 rotated glass sheets with PG monogram)
    - Feature preview row (7 module badges, staggered animation)
    - Rotating loading messages (5 messages, 600ms interval)
    - Shimmer sweep animation on logo
    - Three glass-style footer info cards (Developer, Copyright,
      Version 2026)
- MainWindow with hamburger menu (left drawer) + settings (right drawer)
- Floating buttons: hamburger (top-left), ProGlass + Info (top-right)
- AI Chat panel (floating bottom-right, Gemini powered)
- Light/Dark theme toggle (iOS-style animated switch)
- Custom glassy vertical scrollbar for sidebar
- Custom app icon (favicon.ico)
- 15 module placeholders with grouped sidebar navigation

#### Module 2 — Sheet Store (Raw Material Inventory) ✅

Full CRUD glass sheet inventory management.

- AddEditSheetView dialog (Add/Edit/Delete)
- Buy dialog (Stock/Used/Balance, SQM/sheet, live cost preview)
- Use dialog (Job Order reference, balance capped, live value preview)
- SQM-based pricing: `TotalValue = SqmPerSheet × Stock × PricePerSqm`
- GlassSheet entity fields: Category, ThicknessMm, Color, WidthMm,
  HeightMm, Supplier, PurchasePricePerSqm, SellPricePerSqm, Stock, Used
- Live per-row stat switching (click row → stats show that row)
- 4 filter dropdowns (Category, Thickness, Color, Supplier) with AND logic
- 10 sort presets, live search, clear all filters
- CSV export/import with duplicate resolution dialog
- Excel export (.xlsx, branded header, zebra rows, totals)
- PDF report (QuestPDF, landscape A4, summary cards, page numbers)
- Full history audit trail (SheetHistory entity)
- History dialog with action filter + date range + search
- Buying Value / Selling Value / Profit stat cards
- Auto-backup on app exit (last 7 copies)

#### Module 3 — AI Infrastructure ✅

- `Services/AI/AIService/` — IAiService + GeminiAiService
- `Services/AI/AIConnectivity/` — online/offline detection
- `Services/AI/AIConfig/` — config persistence (`%AppData%/config.json`)
- Settings drawer: Enable AI toggle, API key input, Test Connection
- AI Chat: floating button + chat panel
- System prompt: glass production expert

#### Module 4 — Proforma Invoice (22 sub-phases, FULLY COMPLETE) ✅

**Architecture:**

- 7-step wizard hosted by ProformaInvoiceViewModel
- List-first landing page (SavedInvoicesList)
- IsListView / IsWizardView toggle for navigation
- Step 6 takes Step5VM via constructor injection
- Step 7 takes Step1-3, Step5-6 VMs for preview rendering

**Wizard Steps:**

- **Step 1 — Company Setup**
    - CompanyName, CompanyTrn, CompanyLocation
    - ProformaId (auto-generated), IssueDate, ValidUntil, ProformaStatus
    - Saved company profile cards (fast-load)

- **Step 2 — Project Specs**
    - ProjectIdentifierName, ProjectContractNo, SiteDeliveryLocation,
      LpoNumber

- **Step 3 — Client Profile**
    - CustomerName, CustomerTrn, CustomerReferenceCode
    - SalesRepresentative, DocumentPreparedBy
    - BillingAddress, AttentionExecutiveName, ClientContactPhone

- **Step 4 — Pricing Estimator**
    - SGU/DGU/LAM configurator with Guardian & AGC catalog
    - Auto-generates spec names, saves to specifications.db
    - Apply calculated rate to selected spec groups

- **Step 5 — DataGrid**
    - Multiple SpecGroups with add/remove
    - GlassItemRow fields: W1, H1, W2, H2, Qty, Rate → computed
      Sqm1, TotalSqm, Lm1, Lm2, Value
    - Excel-like keyboard navigation (DataGridNavigationBehavior +
      IExcelPasteable interface)
    - Smart paste from Excel (150+ rows, batched, no crash)
    - ListView with VirtualizingStackPanel recycling
    - Per-group footer totals + Grand Total Summary
    - SpecDisplayName computed property for print headers
    - Phase 7.19d: Split combined W1×H1/W2×H2 into 4 separate columns
      across preview, print, PDF, Excel, CSV rendering paths

- **Step 6 — Other Charges**
    - MetricDiscoveryHubViewModel (live aggregates from DataGrid)
    - 9 GridSourceOptions (LM1, LM2, LM_COMBINED, SQM1, SQM2,
      SQM_COMBINED, DGU_CAVITY, TOTAL_PIECES, DOUBLE_GLASS)
    - 8 predefined charges (Argon, Silicon, Overlap, Shutter, Mitring,
      Polish, Cutout, Holes)
    - Polish: PolishSubType enum + PolishSubTypeMeta (extracted from
      nested class due to XamlWriter crash)
    - WastagesSectionViewModel (dynamic color-aggregated rows)
    - CustomChargesSectionViewModel (free-form line items)
    - TotalOtherCharges = predefined + wastages + custom

- **Step 7 — Print Preview**
    - InvoiceMainSummaryViewModel (Discount % or AED + VAT 5%)
    - TermsAndConditionsViewModel (6 UAE defaults, validity 2-7 days)
    - PrintableInvoiceViewModel (Print/Save/Load/PDF/Excel/CSV commands)
    - TWO rendering paths: on-screen XAML + code-built FixedDocument
    - Toolbar: PRINT | SAVE | PDF | EXCEL | CSV | EMAIL (placeholder)

**Save/Load:**

- `proformainvoices.db` with JSON blob strategy
- ProformaInvoiceSnapshot POCO tree (no WPF types)
- ProformaInvoiceMapper (BuildEntityFromWizard +
  RestoreWizardFromSnapshot)
- Smart duplicate detection (Update / Auto-increment / Cancel)

**List Page:**

- Module landing = SavedInvoicesList
- "+ NEW INVOICE" button → fresh wizard
- "Invoice List" button in sidebar top + bottom nav for returning
  to list (Phase 7.19c — prominent accent button)
- OPEN button per row (accent pill)
- Right-click context menu: Open, Duplicate, Export PDF, Export Excel,
  Change Status (sub-menu), Convert to Job Order (placeholder), Delete
- Right-click menu built in code-behind (OnRowRightClick) —
  XAML ContextMenu bindings unreliable
- Search, filter (status + date range), sort presets
- Stat cards: Total Invoices, Showing, Total Value

#### Module 5 — Dashboard (Phase 8.1–8.2f, FULLY COMPLETE) ✅

Multi-page dashboard shell with underline tab navigation.

**Architecture:**

- DashboardViewModel (shell) owns `List<DashboardPageBase> Pages`
- DashboardPageBase abstract: IsLoading, HasError, TabTitle, TabIcon,
  LoadAsync with reentrancy guard, LoadCoreAsync abstract,
  OnThemeChanged virtual hook (Phase 8.2f)
- Lazy loading via ActivePage setter
- ContentPresenter with DataTemplate auto-selects the View
- ReferenceEqualityConverter drives active tab underline highlight

**Sub-pages (all extend DashboardPageBase):**

- **OverviewDashboardView** — Total Sales hero card (dark green
  gradient), 3 KPI cards, Monthly Sales bar chart last 6 months,
  Recent Activity Feed merged from invoices + sheet history, Quick
  Actions (New Invoice, Saved Invoices, View Stock, Export Report)

- **SheetStoreDashboardView** — 4 KPI cards (Sheet Types, Inventory
  Value, Available SQM, Low/Out Stock), Category SQM chart

- **PiDashboardView** — 4 KPI cards by status, Status Distribution
  bars, Top 5 Customers bars

**Navigation Callbacks (Phase 8.2):**

- `DashboardVM.NavigateTo = target => MainViewModel.Navigate(target)`
- `DashboardVM.TriggerNewInvoice = () => ProformaInvoiceVM.TriggerNewInvoice()`
- `ProformaInvoiceViewModel.TriggerNewInvoice()` public method wraps
  private `StartNewInvoice()` so quick action can open wizard

**Recent Activity Feed:**

- Merges last 10 invoices (by CreatedAt/UpdatedAt) + last 10 sheet
  history rows (from SheetHistories table via AppDbContext)
- ActivityFeedItem model: Icon, Title, SubTitle, ValueDisplay,
  StatusLabel, StatusColor, TintColor, ModuleTag, SortDate
- Ordered by SortDate descending, taken 10

**Design System (Phase 8.2c–8.2f):**

- Complete 25-brush theme palette (was 8)
- Green accent (was blue) matching splash logo
- BrushKeyConverter resolves VM string properties like `"AccentBrush"`
  to actual theme brushes at render time
- Chart bars keep hex colors intentionally (data-driven identity)
- Instant theme swap via ThemeManager.ThemeChanged event subscription
  in DashboardPageBase — sub-pages reassign ObservableCollections to
  force DataTemplate rebuild

### 6.2 PLANNED MODULES

| Module               | Phase  | Status                       |
|----------------------|--------|------------------------------|
| Job Order            | 9      | NEXT (design questions open) |
| Cutting              | 10     | Planned                      |
| Holes & Cutouts      | 11     | Planned                      |
| Polish               | 12     | Planned                      |
| Edge Deletion        | 13     | Planned                      |
| Mitring              | 14     | Planned                      |
| Tempering            | 15     | Planned                      |
| Pairing              | 16     | Planned                      |
| Doubling/Lamination  | 17     | Planned                      |
| Delivery Note        | 18     | Planned                      |
| Tax Invoice          | 19     | Planned                      |
| Sales Report         | 20     | Planned                      |
| Optimization         | 21     | Planned                      |

---

## 7. Non-Functional Requirements

### 7.1 Platform

- Windows 10 / 11 (64-bit)
- .NET 8 Desktop Runtime
- WPF (not WinUI, not MAUI, not Blazor)
- Standalone .exe deployment
- Single-user desktop app (no multi-user auth)

### 7.2 Data

- SQLite via EF Core 8
- 3 separate database files (isolation between modules):
    1. `proglass.db` — Sheet Store (GlassSheets + SheetHistories)
    2. `specifications.db` — Saved glass specs library
    3. `proformainvoices.db` — Saved invoices with JSON snapshots
- Location: `%AppData%\ProGlassAutomation\`
- Automatic backup on exit (keeps last 7 copies)

### 7.3 Performance

- App startup: < 4 seconds (including splash animation)
- Dashboard load: < 500ms per sub-page
- Invoice save: < 200ms
- PDF generation: < 2 seconds for typical invoice (30 rows)
- Excel/CSV export: < 1 second
- Paste of 150 rows: < 1 second, no UI freeze
- Use bulk update pattern (`SetAllSilent` + `FireAllChanged`)
- Suppress PropertyChanged cascades during bulk operations

### 7.4 Reliability

- No app crash on 2nd paste
- No data loss on row delete
- Footer always accurate after any operation
- Serial numbers always correct after add/remove/paste
- No hidden state
- Fail loudly, not silently
- Zero data loss across 100+ invoices saved

### 7.5 UI/UX

- Light + Dark theme with **instant swap** (no navigation required)
- Green accent color throughout (matches glass industry aesthetic)
- ClearType text rendering enforced on all major surfaces
- Responsive to window resize (min 1280×720)
- Consistent card design language across modules
- Tab navigation matches Excel behavior
- Enter moves down same column like Excel
- Paste works exactly like pasting into Excel grid
- Errors shown via MessageBox with clear message

### 7.6 Print Output

- Vector-crisp (not rasterized)
- PDF export must match on-screen A4 preview layout exactly
- Two rendering paths (XAML preview + code-built FixedDocument) must
  stay in sync — any layout change must update BOTH

### 7.7 Localization

Currently English-only, UAE-specific (AED, 5% VAT). Future phases
may add Arabic RTL support.

### 7.8 Security

- No cloud sync (local-only app)
- Gemini API key stored in `%AppData%\ProGlassAutomation\config.json`
- No PII/PCI compliance requirements (B2B invoicing tool)

---

## 8. Out of Scope

- Web version
- Mobile version
- Multi-user / server sync
- Real-time collaboration
- Cloud storage (may add later)
- Marketing logic / analytics dashboards
- Non-deterministic AI logic in core calculations

---

## 9. Success Criteria

- ✅ All calculations reproducible + deterministic
- ✅ Replace Excel-based quoting for target user (Juber's business)
- ✅ Zero data loss across 100+ invoices saved
- ✅ Print output matches or exceeds Excel template quality
- ✅ Theme switch works instantly across all modules
- ✅ New feature phases can be added without breaking existing modules
- ✅ New developer can understand architecture in < 30 minutes
- ✅ AI can extend system without breaking structure
- 🎯 90%+ unit test coverage (post-Phase 21)