# ProGlass Automation — Product Requirements Document

**Version:** 3.0 (Chat 10 update — through Phase 10.9a)
**Owner:** Juber Saleem (jubersaleem@gmail.com)
**Last updated:** 2026 Q2
**Status:** Living document — updated with each phase

---

## 1. Product Vision

ProGlass Automation is a Windows desktop ERP application for UAE-based
glass manufacturing companies. It manages the complete production
workflow from raw material inventory through cutting, processing,
tempering, and delivery — with integrated invoicing, job tracking,
delivery notes, and reporting.

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
- **Dispatch team** — prepare delivery notes with photo proof
- **Accounts team** — generate tax invoices, track payments
- **Management** — dashboard KPIs, sales reports

---

## 3. Technology Stack

| Layer            | Technology                                   |
|------------------|----------------------------------------------|
| Platform         | Windows 10/11 desktop                        |
| UI framework     | WPF (.NET 8, C#)                             |
| Pattern          | MVVM (ViewModelBase + RelayCommand)          |
| Database         | SQLite + Entity Framework Core 8 (5 files)   |
| PDF generation   | QuestPDF 2024.12.2 (Community license)       |
| Excel export     | ClosedXML 0.105.0                            |
| CSV export       | CsvHelper 33.1.0                             |
| AI Assistant     | Google Gemini (gemini-flash-latest)          |
| Themes           | Custom Light/Dark with 25-brush palette      |
| Shared exports   | IPdfLayout / IExcelLayout / ICsvLayout       |

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
    Delivery Note (to site, with photo proof + balance tracking)
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
- **Balance (DN):** `Ordered − Delivered` (NEVER sum snapshots)

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

- Animated full-screen splash (light theme, green accent, custom
  glass-stack logo, feature preview row, rotating messages)
- MainWindow with hamburger menu (left) + settings (right)
- AI Chat panel (floating bottom-right, Gemini powered)
- Light/Dark theme toggle (iOS-style animated switch)
- Custom glassy vertical scrollbar for sidebar
- 15 module placeholders with grouped sidebar navigation

#### Module 2 — Sheet Store ✅

Full CRUD glass sheet inventory management with SQM-based pricing,
Buy/Use dialogs, CSV/Excel/PDF export, history audit trail, and
automatic backup (last 7 copies on exit).

#### Module 3 — AI Infrastructure ✅

Gemini AI service with API key config, online/offline detection,
chat panel with glass production system prompt.

#### Module 4 — Proforma Invoice ✅

7-step wizard hosted by ProformaInvoiceViewModel. List-first
landing page. Company Setup → Project Specs → Client Profile →
Pricing Estimator → DataGrid → Other Charges → Print Preview.
JSON snapshot save/load. PDF/Excel/CSV export via shared engine
(migrated Phase 9.5b from legacy service).

#### Module 5 — Dashboard ✅

Multi-page shell with underline tab navigation. Sub-pages:
- **Overview** — hero card, KPIs, monthly sales chart, activity feed
- **Sheet Store** — inventory KPIs + category SQM chart
- **Proforma Invoice** — status distribution + top customers
- **Job Order** — sub-dashboard (Phase 9.4)

**Phase 10.9a full redesign:** 2-column parent-card grid layout
with nested tiles (Sales · Production · Delivery · Inventory),
chat-style Recent Activity inside grid, smooth pixel-based
scrolling.

#### Module 6 — Job Order ✅

Convert-from-PI architecture (Path B — no wizard duplication):
- 1 PI = 1 Job Order (no splitting)
- Numbering: JOB-YYYY-NNN (derived from PI-YYYY-NNN)
- Status: Draft → Released → InProduction → Completed → Delivered
- Priority: High / Medium / Low
- 4th SQLite database: `joborders.db`
- Two-column detail view: read-only PI snapshot (left) + editable
  production panel (right)
- PDF/Excel/CSV export via shared engine (production sheet — no pricing)
- Job Order sub-dashboard

#### Module 7 — Shared Export Engine ✅

`Services/Export/` — 3 contracts (IPdfLayout, IExcelLayout, ICsvLayout)
+ 3 engines. Modules provide layouts, engine handles page structure,
file dialog, save/open flow. Style tokens in `DocumentStyle` (PDF)
and `WorkbookStyle` (Excel). Print support via `PdfExportEngine.PrintAsync`.

#### Module 8 — Delivery Note ✅ (Phase 10.1–10.9a)

**Fully complete DN module** — the most sophisticated module built
to date.

Features:
- 5th SQLite database: `deliverynotes.db`
- DN ID format: `DN-2026-001-01` (JO seq + delivery seq)
- 4 delivery types: Original | Dummy | Rework | Return
- 5 statuses: Draft | Dispatched | Delivered | Signed | Received
- **Sibling DN chain**: JO → DN-01 → DN-02 (Dummy for balance) → DN-03...
- **Auto-generated Dummy DN** when balance qty remains after dispatch
- **Self-healing PREV column** — always fresh from confirmed siblings
- **Balance formula lock-in**: `Ordered − Delivered` (never sum snapshots)
- **Attachments** — disk-based storage (%AppData%\...) for photos + PDFs
- **Rename dialog** on upload (frameless glassy modal)
- **Clickable history rows** navigate to sibling DN
- **Items grouped by SpecGroupName** in detail grid
- **Zero-qty save warning**
- **Delete DN command** (removes files too)
- **JO Picker dialog** to create DN from scratch
- **Draft-DN-exists check** prevents duplicates
- **Invoice button** per row (placeholder for Phase 19)
- **Tax Invoice link fields** on entity (Phase 19 prep)
- **PDF/Excel/CSV export** via shared engine (5-column totals strip
  with Prev Delivered column)
- **Premium detail page** — hero card + gradient section headers +
  4 stat tiles (Ordered · Prev · This DN · Balance) + spec group
  accent bars
- **Dashboard integration** — Delivery Snapshot tile group +
  DN events in activity feed + "Deliveries" quick action

### 6.2 PLANNED MODULES

| Module               | Phase  | Status                       |
|----------------------|--------|------------------------------|
| Tax Invoice link     | 10.10  | NEXT — wire placeholders     |
| Cutting              | 11     | Planned                      |
| Holes & Cutouts      | 12     | Planned                      |
| Polish               | 13     | Planned                      |
| Edge Deletion        | 14     | Planned                      |
| Mitring              | 15     | Planned                      |
| Tempering            | 16     | Planned                      |
| Pairing              | 17     | Planned                      |
| Doubling/Lamination  | 18     | Planned                      |
| Tax Invoice          | 19     | Planned (wires up DN links)  |
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
- **5 separate database files** (isolation between modules):
    1. `proglass.db` — Sheet Store
    2. `specifications.db` — Saved glass specs library
    3. `proformainvoices.db` — Saved invoices
    4. `joborders.db` — Job orders
    5. `deliverynotes.db` — Delivery notes + attachments metadata
- Location: `%AppData%\ProGlassAutomation\`
- Attachment files: `%AppData%\ProGlassAutomation\DeliveryNotes\Attachments\`
- Automatic backup on exit (keeps last 7 copies)

### 7.3 Performance

- App startup: < 4 seconds (including splash animation)
- Dashboard load: < 500ms per sub-page
- Invoice save: < 200ms
- PDF generation: < 2 seconds for typical invoice (30 rows)
- Excel/CSV export: < 1 second
- Paste of 150 rows: < 1 second, no UI freeze
- Smooth pixel-based scrolling on all dashboards (no lag)
- DN attachment upload: instant (disk copy)

### 7.4 Reliability

- No app crash on 2nd paste
- No data loss on row delete
- Footer always accurate after any operation
- Serial numbers always correct after add/remove/paste
- No hidden state
- Fail loudly, not silently
- Zero data loss across 100+ invoices saved
- Self-healing PREV values prevent stale DN snapshots

### 7.5 UI/UX

- Light + Dark theme with **instant swap** (no navigation required)
- Green accent color throughout
- ClearType text rendering enforced on all major surfaces
- Responsive to window resize (min 1280×720)
- Consistent card design language across modules
- Tab navigation matches Excel behavior
- Paste works exactly like pasting into Excel grid
- Errors shown via MessageBox with clear message
- **Smooth scrolling** — `CanContentScroll=False` + `PanningMode=VerticalOnly`

### 7.6 Print Output

- Vector-crisp (not rasterized)
- PDF export matches on-screen A4 preview layout exactly
- Two rendering paths (XAML preview + code-built FixedDocument)
  must stay in sync — any layout change must update BOTH
- Shared export engine ensures consistent letterhead + footer
- Letterhead appears on page 1 only via `.ShowOnce()`
- Footer with DocumentId + page X of Y on every page

### 7.7 Localization

Currently English-only, UAE-specific (AED, 5% VAT). Future phases
may add Arabic RTL support.

### 7.8 Security

- No cloud sync (local-only app)
- Gemini API key stored in `%AppData%\ProGlassAutomation\config.json`
- No PII/PCI compliance requirements (B2B invoicing tool)
- Attachments stored on local disk with GUID subfolders per DN

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
- ✅ Replace Excel-based quoting for target user
- ✅ Zero data loss across 100+ invoices saved
- ✅ Print output matches or exceeds Excel template quality
- ✅ Theme switch works instantly across all modules
- ✅ New feature phases can be added without breaking existing modules
- ✅ New developer can understand architecture in < 30 minutes
- ✅ AI can extend system without breaking structure
- ✅ Delivery Note module operates independently and cleanly
- ✅ Dashboard shows real-time cross-module business health
- 🎯 90%+ unit test coverage (post-Phase 21)