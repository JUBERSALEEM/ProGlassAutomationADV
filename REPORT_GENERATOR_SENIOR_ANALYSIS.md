# Report Generator — Senior Analysis & Implementation Plan

**Date:** 2026-09-06
**Requested by:** Juber Saleem
**Scope:** Upgrade the "full report cycle" — one consolidated report per order that
walks **Proforma Invoice → Job Order → Delivery Note → Tax Invoice** with
quantities, weights, amounts, balances and **attachment completeness**
("3 delivery notes prepared but only 2 attached → 1 document missing").
**Status:** Analysis complete. Implementation NOT started (this is the plan the
user asked to read before any code is written).

---

## 1. Executive summary

The user wants ONE report that tells the whole story of an order, instead of
seven separate read-only reports that each show a single document type. Every
piece of *data* this report needs already exists in the codebase — the gap is
**presentation + attachment-completeness**, not raw data.

**Recommendation: build a new standalone module — "Report Generator" (an
Order Fulfilment Report) — that follows the Statement Generator's proven
4-step wizard + reconciliation pattern, exactly as the user suggested.**

This is the right call because:

1. The Statement Generator already walks the **exact same document chain**
   (PI → JO → DN → TI) and already reconciles **Ordered vs Delivered vs
   Invoiced** per glass line (`OrderReconciliationService`). The new module
   reuses that engine rather than duplicating it.
2. It already detects the user's headline bug class — "ordered 2, delivered 1,
   invoiced 2 → OVER-INVOICED" — plus the inverse (under-invoiced / revenue
   leakage).
3. The 4-step wizard (Filter → Preview → Pipeline → Review) already solves the
   "generate reports by applying filters" requirement the user described.
4. The module wiring pattern (`StatementModuleViewModel` = List ↔ Generator,
   single constructor + DI fallbacks, navigation callbacks) is a drop-in
   template for a new module.

The alternatives the user floated (Step 10 in the PI section, or inside Step 9)
are workable but inferior — rationale in **§11**.

---

## 2. What the existing Reports modules actually do

The user said "I have reports modules which currently I am not sure what they
do." This is the honest inventory.

### 2.1 The Reports hub (read-only dashboards)

`ViewModels/Reports/SalesReportViewModel.cs` is a **hub**. Its left rail has 7
read-only reports, all backed by `IReportingDataService`
(`Services/Reporting/ReportingDataService*.cs`) and sharing
`ReportBaseViewModel` (`ViewModels/Reports/Common/ReportBaseViewModel.cs` —
filter, search, date range, company/customer/project/salesman/status,
pagination, CSV/Excel/PDF export):

| Rail key | ViewModel | What it shows |
|---|---|---|
| Overview | (hub KPI cards) | Cross-module KPI dashboard |
| Pi | `PiReportViewModel` | PI list: status, value, converted/not; expand → spec rows + qty reconciliation |
| Jo | `JoReportViewModel` | Job Order list: production status, priority, delivery date |
| Delivery | `DeliveryReportViewModel` | DN line rows: ordered/delivered/balance per spec + per-DN deliveries |
| TaxInvoice | `TaxInvoiceReportViewModel` | TI list: issued/paid/overdue, amounts, VAT |
| Statement | `StatementReportViewModel` | Saved account statements (read-only) |
| VatSubmission | `VatSubmissionViewModel` | VAT submission summary |

These are **list + inline-expand** screens. None of them renders a single
document that spans PI → DN → TI in one view, and **none of them reconciles
attachments**.

### 2.2 The Statement Generator (interactive module — the template)

`ViewModels/Reports/StatementModuleViewModel.cs` owns:

- **`StatementListViewModel`** — saved statements list (KPI cards, search, open /
  edit / delete).
- **`StatementGeneratorViewModel`** — a **4-step wizard**:

  | Step | View | Purpose |
  |---|---|---|
  | 1 · Filter | `StatementGenerator/Filter/FilterStep.xaml` | company/customer/project/salesman/dates/status + PDF options |
  | 2 · Preview | `StatementGenerator/Preview/PreviewStep.xaml` | aging strip + selectable invoice list |
  | 3 · Pipeline | `StatementGenerator/Pipeline/ExecuteStep.xaml` | runs `StatementWorkflowEngine` |
  | 4 · Review | `StatementGenerator/Review/ReviewStep.xaml` | reconciliation tabs (Merged/Balance/Alerts), warnings with actions, PDF |

- The wizard is partial-split:
  `StatementGeneratorViewModel.{WizardState,Navigation,FilterState,Prepare,
  Preview,Pipeline,Generate,PdfBuild,Helpers,LiveSubscriptions}.cs`.

- **`Services/Statement/Workflow/StatementWorkflowEngine*.cs`** runs 8 internal
  steps: read PI + Step-8 attachments → read JO → per-DN line reconciliation →
  validate quantities → read TI → cross-match coverage (line reconciliation
  against `OrderReconciliationService`) → regenerate TI PDFs + load TI
  attachments → merge + build `MissingAttachmentSummary`.

- **`Services/Statement/Aggregation/StatementAggregator*.cs`** loads invoices,
  applies filters, counts attachments per invoice in one batch, computes aging.

- **`Models/Statement/Reconciliation/*`** — `OrderReconciliationRow` /
  `OrderReconciliationSummary` — is the core per-line ordered/delivered/
  invoiced/balance reconciliation (see §4.3).

The Statement Generator's only gaps vs. the new requirement are: it is a
**customer-statement (money/aging)** report, it does **not** print the PI spec
rows or DN spec rows in the PDF, and it only *flags* missing signed documents —
it does not compute "prepared X vs attached Y → Z missing" per document kind
in the way the user described.

---

## 3. Requirements → data mapping (every user phrase has a source)

All field names below are real properties found in the code.

### 3.1 PI header — "PI date, company name and other related details"

| User need | Source field | Where |
|---|---|---|
| PI number | `SavedProformaInvoice.ProformaId` | `Models/ProformaInvoice/SavedInvoice/` |
| PI date | `.IssueDate` | PI entity / `PiReportRow.IssueDate` |
| Company | `.CompanyName` | PI entity |
| Customer | `.CustomerName` | PI entity |
| Project | `.ProjectName` | PI entity |
| Salesman | snapshot `Client.SalesRepresentative` | `ProformaInvoiceSnapshot` |
| Prepared by | `PiReportRow.PreparedBy` | `Services/Reporting/ReportingModels.cs` |
| Status | `.Status` — `Draft/Sent/Approved/Rejected/Expired/Cancelled` | `Models/ProformaInvoice/Common/` `ProformaInvoiceStatus` |
| Valid until | `.ValidUntil` | PI entity |
| Grand total | `.GrandTotal` | PI entity / `PiReportRow.GrandTotal` |

### 3.2 "Job release data"

- `SavedJobOrder.DeliveryDate` (scheduled/release date) and `ConvertedAt`
  (PI → JO conversion time) on the JO, plus `SavedJobOrder.Status` / `Priority`.
- `JoReportRow` already exposes `DeliveryDate`, `Status`, `Priority`
  (`Services/Reporting/ReportingModels.cs`).

### 3.3 PI rows — "rows data are same like proforma invoice"

- `ReportingDataService.GetPiRowsDetailAsync(proformaId)` →
  `GlassSpecReportRow` — `GlassRef`, `SpecName`, `GlassType`, `OuterColor`,
  `InnerColor`, `W1/H1/W2/H2`, `Qty`, `Sqm1`, `TotalSqm`, `Rate`, `Amount`,
  `GroupNumber`, `SerialNumber`, sheet provenance, **and already**
  `DeliveredQty`, `BalanceQty`, `InvoicedQty`, `DeliveryStatus`, `InvoiceStatus`.

### 3.4 Delivery Note — "when delivered, what delivered, weight, balance"

| User need | Source |
|---|---|
| When delivered | `SavedDeliveryNote.DeliveryDate` + `.DeliveryTime` |
| What delivered | `DeliveryNoteItem` rows (spec group, serial, glass ref, dimensions, `DeliveredQty`) |
| Total weight of the order | `DeliveryNoteItem.WeightPerPiece` (kg) and computed `TotalWeight = WeightPerPiece × DeliveredQty` |
| How much delivered | `DeliveryNoteItem.DeliveredQty` (sum), `SavedDeliveryNote.TotalDeliveredQty` |
| How much balance | `DeliveryNoteItem.BalanceQty` / `ComputedBalanceQty`, `SavedDeliveryNote.TotalBalanceQty` |
| Rows with specs (as-is) | `DeliveryNoteItem.SpecGroupName`, `SpecGroupNumber`, `SerialNumber`, `GlassRef`, `W1/H1/W2/H2` |
| Driver/vehicle/receiver | `DriverName`, `VehicleNumber`, `ReceiverName` |
| DN status/type | `Status` (`Draft/Dispatched/Delivered/Signed/Received`), `DeliveryType` (`Original/Dummy/Rework/Return`), `IsFoc`, `IsMasterDn` |

**Important:** the reconciliation engine already **excludes** Master/Dummy DNs
and only sums **confirmed** DNs (`OrderReconciliationService` +
`GetPreviouslyDeliveredMapAsync`). The report must do the same so "delivered"
always means *real* deliveries.

### 3.5 Tax Invoice — "total invoiced as per partially delivered amount, then balance"

| User need | Source |
|---|---|
| Invoiced qty per line (partial) | `TaxInvoiceItem.Qty` keyed by `(SpecGroupNumber, SerialNumber)` |
| Invoice amount | `SavedTaxInvoice.Subtotal / DiscountAmount / VatAmount / GrandTotal` |
| What it covered | `DeliveryNoteReference` (which DN), `InvoiceType` (`Full` vs partial), `SequenceNumber` |
| Invoiced vs delivered (the "balance") | `OrderReconciliationRow.InvoiceVsDeliveryDiff = InvoicedQty − DeliveredQty` |
| Delivery balance | `OrderReconciliationRow.BalanceQty = MAX(0, OrderedQty − DeliveredQty)` |
| Invoice status | `SavedTaxInvoice.Status` (`Draft/Issued/Paid/PartiallyPaid/Overdue/Cancelled/Void`) |

"Total invoiced − delivered glass = balance" is precisely the existing
`InvoiceVsDeliveryDiff` (positive = over-invoiced, negative = under-invoiced)
and the `DeliveryState / InvoiceState` on `QuantityReconciliation`
(`NotDelivered / PartiallyDelivered / FullyDelivered`,
`Pending / PartiallyInvoiced / FullyInvoiced`).

### 3.6 Attachments — "count attachments from real data, detect missing"

| User need | Data source | Count API |
|---|---|---|
| PI attached documents | `ProformaAttachmentRecord` list in `manifest.json` + `Images/PDFs` folders | `ProformaAttachmentService.LoadManifestAsync(proformaId)` |
| DN attachments | `DeliveryNoteAttachment` rows (Image/Pdf), files on disk | `DeliveryNoteRepository.GetAttachmentCountAsync(dnId)` and batch `GetAttachmentCountsAsync(...)` |
| TI attachments | `TaxInvoiceAttachment` rows (`SignedPI/SignedDN/Other`, bytes in DB) | `TaxInvoiceRepository.GetAttachmentCountAsync(invoiceNumber)` and batch `GetAttachmentCountsAsync(...)` |

The user's exact scenario maps to:

```
prepared DNs        = count of SavedDeliveryNote rows for the JO/PI
                      where !IsMasterDn && DeliveryType != "Dummy"
attached DNs        = count of those DNs whose GetAttachmentCountAsync() > 0
missing documents   = prepared DNs − attached DNs   → "1 documents missing
                      in attachments of delivery note"
```

---

## 4. Reconciliation model to reuse (read this before designing anything)

### 4.1 `OrderReconciliationService`

`Models/Statement/Reconciliation/OrderReconciliationService.cs` →
`BuildForProformaAsync(proformaId)` returns `OrderReconciliationSummary`:

- Loads PI snapshot spec rows (authoritative `OrderedQty`).
- Loads JOs via `App.Jobs.GetBySourceProformaIdAsync`.
- Loads cumulative `DeliveredQty` from confirmed DNs
  (`GetPreviouslyDeliveredMapAsync`, keyed `"SpecGroupNumber|SerialNumber"`).
- Loads cumulative `InvoicedQty` from active TIs
  (excludes Cancelled/Void/Draft).
- Per line computes `BalanceQty = MAX(0, Ordered − Delivered)` and
  `InvoiceVsDeliveryDiff = Invoiced − Delivered`, then assigns a
  `ReconciliationStatus`:

  | Status | Meaning | Colour |
  |---|---|---|
  | FullyMatched | delivered = invoiced = ordered | green |
  | PartialDelivery | delivered < ordered, invoicing correct | amber |
  | OverInvoiced | invoiced > delivered (★ the bug) | red |
  | UnderInvoiced | delivered > invoiced (revenue leak) | blue |
  | NotYetStarted | nothing delivered/invoiced | gray |
  | OverDelivered | delivered > ordered | orange |

`BuildBatchAsync(IEnumerable<string> proformaIds)` already dedupes and batches
per PI — **use this directly for the filtered set.**

### 4.2 `RowTraceabilityService`

`Services/Reporting/RowTraceabilityService.cs`:
- `TraceRowLifecycleAsync(rowId)` → `RowTrace` (SourceRow + `TraceStep[]` per
  stage: PI / Job Order / Delivery / Tax Invoice / Profitability).
- `CalculateReconciliationAsync(proformaId, ...)` → `QuantityReconciliation`
  (`OrderedQty/JobOrderQty/DeliveredQty/RemainingQty/InvoicedQty` +
  `DeliveryState` + `InvoiceState`).

This is the per-row "same spec through every document" view the user wants.

### 4.3 What is NOT reused (the genuinely new work)

1. **A single consolidated document model** (header + PI rows + DN deliveries +
   TI invoices + reconciliation + attachments) — today these live in separate
   services and a loose `Dictionary<string,object>` in
   `GetDocumentChainAsync`.
2. **Attachment-completeness engine** (prepared-vs-attached per document kind).
3. **A report PDF** that prints all of the above (Statement PDF only prints
   invoice-level money tables).
4. **Saved-report persistence** (so a generated report can be reopened) —
   mirror `SavedStatement` / `StatementSnapshot`.
5. **Weights rollup** at the order level (DN items already carry weight; the
   report just sums them, and can compute order weight from PI spec × thickness
   via the existing `GlassWeightCalculator` if a PI-level weight is wanted).

---

## 5. Proposed design — "Report Generator" (Order Fulfilment Report)

### 5.1 Module shape (mirror Statement Generator exactly)

```
ReportGeneratorModuleViewModel          ← list ↔ wizard toggle (like StatementModuleViewModel)
 ├── ReportGeneratorListViewModel       ← saved reports list (open/edit/delete/regenerate)
 └── ReportGeneratorViewModel           ← 4-step wizard (partial-split like StatementGenerator)
        ├── Step 1  Filter
        ├── Step 2  Preview   (select PIs)
        ├── Step 3  Pipeline  (build the consolidated report)
        └── Step 4  Review    (the report itself + warnings + PDF)
```

### 5.2 The four steps

**Step 1 — Filter.** Same filter surface as Statement Generator
(company/customer/project/salesman/date range) plus:
- PI status (Approved/… / all) — the report is most useful for
  Approved/confirmed PIs, but must allow any.
- Document-kind toggles: include PI rows / include DN deliveries / include TI
  invoices / include attachments — default all on.
- Scope selector: **One PI** (pick from a PI list) or **Multiple PIs**
  (portfolio), so both "a single order report" and "reports by filter" work.

**Step 2 — Preview.** A PI list (reuse `PiReportRow` + quick KPIs: ordered /
delivered / invoiced / balance qty and value). User selects one or more PIs
(Select All / Deselect All, same as `StatementPreviewViewModel`).

**Step 3 — Pipeline.** Runs a new `OrderReportEngine` (see §5.4). Progress is
reported the same way `WorkflowProgressViewModel` reports the statement
workflow. Cancellable.

**Step 4 — Review.** One consolidated report per selected PI, laid out §5.5,
with a reconciliation tab (per-line Ordered/Delivered/Invoiced/Balance +
status pills) and an **attachments tab** (prepared vs attached vs missing),
plus warning cards (`WorkflowWarningItem` reuse) and
**Export PDF / open folder / back to list**.

### 5.3 The consolidated report layout (screen + PDF, single source)

```
┌──────────────────────────────────────────────────────────────┐
│ 1 · ORDER HEADER                                             │
│   PI number · issue date · company · customer · project      │
│   salesman · prepared by · PI status · valid-until           │
│   JOB RELEASE: JO number · release date · priority · status  │
├──────────────────────────────────────────────────────────────┤
│ 2 · PROFORMA INVOICE — SPECIFICATION ROWS                    │
│   (same columns the PI shows today: spec, glass ref, dims,   │
│    qty, sqm, rate, amount — grouped per spec group)          │
├──────────────────────────────────────────────────────────────┤
│ 3 · DELIVERY NOTES                                           │
│   per DN: DN number · delivered date/time · driver · vehicle │
│           receiver · status · type                           │
│   rows (same specs as PI): ordered · this-DN delivered ·     │
│   previously delivered · balance · weight/piece · weight     │
│   KPI strip: TOTAL ORDER WEIGHT · TOTAL DELIVERED (qty+kg) · │
│              TOTAL BALANCE (qty+kg)                          │
├──────────────────────────────────────────────────────────────┤
│ 4 · TAX INVOICES                                            │
│   per TI: invoice number · issue/due date · status ·         │
│           delivery-note reference · type (full/partial)      │
│   invoiced qty per line · subtotal · VAT · grand total       │
│   BALANCE: invoiced − delivered (qty) → over/under-invoiced  │
│            delivered-not-invoiced (revenue leak)             │
├──────────────────────────────────────────────────────────────┤
│ 5 · RECONCILIATION (per line, Ordered/Delivered/Invoiced/    │
│     Balance + status pill) — reuse OrderReconciliationRow    │
├──────────────────────────────────────────────────────────────┤
│ 6 · ATTACHMENTS (real counts)                                │
│   Proforma Invoice : N attached (Images + PDFs)              │
│   Delivery Notes   : X prepared · Y attached · Z MISSING     │
│                      (list the missing DN numbers)           │
│   Tax Invoices     : N attached (SignedPI/SignedDN/Other)    │
│   WARNINGS: "1 documents missing in attachments of           │
│             delivery note DN-…"                              │
├──────────────────────────────────────────────────────────────┤
│ 7 · TOTALS                                                   │
│   ordered qty/weight/value · delivered · invoiced · balance  │
└──────────────────────────────────────────────────────────────┘
```

### 5.4 New engine — `OrderReportEngine` (+ attachment completeness)

New `Services/OrderReport/OrderReportEngine.cs` (stateless, mirrors
`OrderReconciliationService` style). Per selected PI it calls, in order:

1. `ReportingDataService.GetDocumentChainAsync(pi)` (or the individual
   methods) for header/JO/DN/TI/glass rows.
2. `OrderReconciliationService.BuildForProformaAsync(pi)` for the line
   reconciliation.
3. **`AttachmentCompletenessService` (new)**:
   - PI count = `ProformaAttachmentService.LoadManifestAsync(pi).Count`.
   - DN prepared = `SavedDeliveryNote`s for the JO/PI where `!IsMasterDn &&
     DeliveryType != "Dummy"`; attached = batch `GetAttachmentCountsAsync(dnIds)`
     filter count > 0; missing = prepared − attached (with the actual DN ids).
   - TI counts = batch `GetAttachmentCountsAsync(invoiceNumbers)`.
   - Output `AttachmentCompletenessReport` per PI family.
4. Weight rollups: Σ `DeliveryNoteItem.TotalWeight` per DN and per order
   (delivered), plus Σ `WeightPerPiece × OrderedQty` for order weight.
5. Totals: ordered/delivered/invoiced/balance **qty**, **kg**, **AED**
   (AED from TI `Value` / PI `Amount`).

Result is one JSON-serializable `OrderReportSnapshot` (saved like
`StatementSnapshot`) so Review renders from it and the PDF builder reads it.

### 5.5 Persistence & PDF

- **`Models/OrderReport/`**: `OrderReportSnapshot`, `SavedOrderReport`,
  `OrderReportLineItem` — modelled on `Models/Statement/*`
  (`StatementSnapshot`, `SavedStatement`, `StatementLineItem`).
- **`Services/OrderReport/Database/OrderReportDbContext.cs`** +
  **`Repository/IOrderReportRepository.cs` / `OrderReportRepository.cs`** —
  new `orderreports.db` (mirror `statements.db` wiring in `App.StartupCore`).
- **`Services/OrderReport/Pdf/OrderReportPdfDocument.cs` +
  `OrderReportPdfExporter.cs`** — QuestPDF A4, sections 1–7 of §5.3, reuse
  `StatementPdfDocument` helpers/theme brushes; optional PdfSharp merge with
  attached documents (the Statement Generator already proves the merge path).

---

## 6. File manifest

### New files

```
Models/OrderReport/
  OrderReportSnapshot.cs            (header + sections + totals, JSON-safe)
  SavedOrderReport.cs               (DB row: id/number/filter/totals/pdf path)
  OrderReportLineItem.cs            (per-spec row incl. weight + status)
  AttachmentCompleteness.cs         (prepared/attached/missing per kind + ids)
Services/OrderReport/
  OrderReportEngine.cs              (build one snapshot per PI)
  AttachmentCompletenessService.cs  (PI/DN/TI prepared-vs-attached)
  Database/OrderReportDbContext.cs
  Repository/IOrderReportRepository.cs
  Repository/OrderReportRepository.cs
  Pdf/OrderReportPdfDocument.cs
  Pdf/OrderReportPdfExporter.cs
ViewModels/Reports/OrderReport/
  OrderReportModuleViewModel.cs                 (List ↔ wizard, StatementModule clone)
  OrderReportListViewModel.cs
  OrderReportListRowViewModel.cs
  OrderReportGeneratorViewModel.cs              (main partial)
  OrderReportGeneratorViewModel.WizardState.cs
  OrderReportGeneratorViewModel.Navigation.cs
  OrderReportGeneratorViewModel.FilterState.cs
  OrderReportGeneratorViewModel.Preview.cs
  OrderReportGeneratorViewModel.Pipeline.cs
  OrderReportGeneratorViewModel.Review.cs
  OrderReportGeneratorViewModel.PdfBuild.cs
  OrderReportPreviewViewModel.cs
  OrderReportAttachmentGroup.cs                 (per-DN/kind display groups)
Views/Reports/OrderReport/
  OrderReportModuleView.xaml(.cs)               (list ↔ generator shell)
  OrderReportListView.xaml(.cs)
  OrderReportGenerator/Shell/OrderReportGeneratorView.xaml(.cs)
  OrderReportGenerator/Filter/FilterStep.xaml(.cs)
  OrderReportGenerator/Preview/PreviewStep.xaml(.cs)
  OrderReportGenerator/Pipeline/ExecuteStep.xaml(.cs)
  OrderReportGenerator/Review/ReviewStep.xaml(.cs)
  OrderReportGenerator/Styles/WizardStyles.xaml  (or reuse existing WizardStyles)
Tests/Agent/OrderReportNavigationTests.cs       (source-level contract tests)
docs/REPORT_GENERATOR_IMPLEMENTATION.md         (post-build doc)
```

### Modified files

| File | Change |
|---|---|
| `ProGlassAutomation.csproj` | register new XAML pages + code-behind |
| `App.StartupCore.cs` | register `orderreports.db` + DI for new repository/engine |
| `ViewModels/MainViewModel.Part1.cs` | add lazy `OrderReportModuleVM` |
| `ViewModels/MainViewModel.Part2.cs` | nav `case "OrderReport"`, wallpapers, wire warning-action callbacks (open PI/JO/DN/TI) |
| `MainWindow.xaml` | nav item + `DataTemplate` for `OrderReportModuleViewModel` |
| `Services/Theme/WallpaperModules.cs` | add `OrderReport` wallpaper key (if a distinct key is wanted) |
| `Services/Licensing/LicenseGate.cs` | (only if the module should be plan-gated) |
| localization JSONs (`scripts/i18n/*`) | new nav/label keys |

---

## 7. Integration wiring checklist (copy the Statement Generator path)

1. **ViewModel** — `OrderReportModuleViewModel` with ONE real constructor +
   parameterless chaining and `TryGet<T>` DI fallback (the `ERR-633EEB` /
   `StatementModuleViewModel` pattern — never a half-built VM).
2. **Navigation** — add `case "OrderReport": CurrentViewModel = OrderReportModuleVM;`
   in `MainViewModel.Part2` + `ApplyModuleWallpaper` mapping.
3. **XAML** — `DataTemplate` for the module VM in `MainWindow.xaml`, nav button
   under the **Dispatch** group (next to Delivery Note / Tax Invoice / Statement
   Generator), localized label.
4. **Callbacks** — expose `OnOpenProformaByNumber / OnOpenJobOrderByNumber /
   OnOpenDeliveryNote / OnOpenTaxInvoiceByNumber` on the module VM and wire in
   `MainViewModel` so every warning card's action button actually navigates
   (Statement Generator already proves this wiring).
5. **PDF deps** — QuestPDF + PdfSharp + SkiaSharp are already referenced for
   statements; no new packages.

---

## 8. Phased implementation plan

| Phase | Deliverable | Est. |
|---|---|---|
| **P0 — Backbone** | `Models/OrderReport/*` + `OrderReportEngine` + `AttachmentCompletenessService` (reuse `OrderReconciliationService` + existing count APIs) + source-level tests | ~600 lines |
| **P1 — Persistence** | `OrderReportDbContext` + repository + `orderreports.db` registration | ~300 lines |
| **P2 — Module + wizard VM** | `OrderReportModuleViewModel`, list VM, generator VM (4-step partials, reusing Statement Generator's command/gating patterns) | ~1,600 lines |
| **P3 — Views** | module shell, list, Filter/Preview/Pipeline/Review XAML + styles; csproj registration | ~1,600 lines |
| **P4 — PDF + polish** | `OrderReportPdfDocument/Exporter`, merge-with-attachments, export wiring, localization, docs | ~700 lines |

Each phase lands compilable; the module can be merged only after P2–P3.

---

## 9. Risks & edge cases (senior review)

1. **"Attached" vs "signed" vs "prepared".** The user's example is
   "3 prepared, 2 attached → 1 missing". The report must count *any* attachment
   (Image/Pdf) as "attached" for the missing-count, and separately show
   SignedPI/SignedDN (TI attachment types) as "signed" — the two concepts must
   not be conflated. The existing `MissingAttachmentSummary`
   (`StatementWorkflowEngine`) already distinguishes signed-PI vs signed-DN;
   reuse its wording so the two modules agree.
2. **Master/Dummy DNs must never count as "prepared".** `IsMasterDn` +
   `DeliveryType == "Dummy"` rows are auto-generated trackers; including them
   would overstate "prepared". The reconciliation service already filters them —
   the completeness service must too.
3. **Partially-paid / partially-delivered money.** TI `Status` has
   `PartiallyPaid`; the report must show invoiced *value* and delivered *qty*
   side by side and derive "balance" from **qty** (reconciliation) not from
   payment state, to avoid mixing "unpaid money" with "undelivered glass".
4. **A PI with no JO / no DN / no TI.** The report must render gracefully:
   "Not converted to Job Order yet", "No delivery notes", "Not invoiced".
   `OrderReconciliationSummary.HasProformaData / HasDeliveryTracking` already
   model this.
5. **Weight only lives on DN items.** Order-level weight = Σ
   `WeightPerPiece × OrderedQty` across DN items (or compute from PI specs via
   `GlassWeightCalculator`). Confirm which is authoritative before P0.
6. **Delete/rename drift.** PI attachments live in a `manifest.json` (not a DB
   table) — completeness must read the manifest, not scan folders, so deleted
   files are counted consistently with what the PI module shows.
7. **WPF build.** The sandbox cannot compile `net8.0-windows`; every phase must
   keep XAML well-formed (run `scripts/layout-verify/xaml_wellformed.py`) and
   rely on the `windows-latest` CI for the real build, as the existing docs do.

---

## 10. Open questions for the user (blockers only — everything else is decided)

1. **Weight source:** show "total weight of the order" from Delivery-Note item
   weights (already stored) — or also compute an order-level weight from the PI
   specs using `GlassWeightCalculator`? (Recommendation: DN weights, with PI
   computed weight as an optional second column.)
2. **Portfolio vs single order:** the "generate by filter like Statement
   Generator" wording suggests multi-PI. Confirm Step-4 should render **one
   report section per PI** in a single PDF (recommended), vs one PDF per PI.
3. **Placement in the nav:** Dispatch group next to Statement Generator
   (recommended) vs inside the Reports hub as an 8th rail item.

---

## 11. Placement decision — why a standalone module (and not Step 9/Step 10)

| Option | Verdict |
|---|---|
| **Step 10 inside the PI section** | Works, but the PI wizard already has 9 steps; a full document-chain report + PDF + saved-reports list is a *different product* and would bloat the PI VM further. A Step-10 "Generate order report" button is a cheap *shortcut* worth adding later that just opens this module pre-filtered to the current PI. |
| **Inside Step 9 after Glass/Specs comparison** | Step 9 is a per-PI *costing* screen; the consolidated report needs cross-module data (JO/DN/TI/attachments) that Step 9 doesn't own. Mixing them couples the two modules for no gain. |
| **8th read-only report in the hub** | Matches the existing read-only reports but does not give the interactive filter→preview→pipeline→review flow or saved reports the user asked for. |
| **Standalone "Report Generator" module following Statement Generator** ✅ | Reuses the exact reconciliation + wizard + persistence machinery the user pointed at, gives the "button inside for overall report generation" + filters + all 4 steps, and stays independently maintainable. |

**Middle ground (recommended ship):** build the standalone module, and add a
tiny **"Generate order report"** button in the PI saved-list / Step 9 that
calls `MainViewModel` navigation → `OrderReport` with the PI pre-selected
(same callback pattern Statement Generator uses). This satisfies the
"keep it in the PI section" instinct without entangling the code.

---

## 12. Verification & test plan

- **Source-level contract tests** (`Tests/Agent/OrderReportNavigationTests.cs`):
  4 steps present, commands/gates named, `OrderReportEngine` calls
  `OrderReconciliationService.BuildForProformaAsync`, completeness counts use
  `GetAttachmentCountsAsync`/`LoadManifestAsync`, missing-count formula present.
- **Unit-testable pure logic** (extract from engine for `ProGlassAutomation.Tests`):
  - `AttachmentCompleteness` math: prepared 3 / attached 2 → missing 1 with id.
  - Reconciliation status table (reuse the existing service — test only the
    new wrapper's totals).
  - Weight rollup sums.
- **XAML lint:** `scripts/layout-verify/xaml_wellformed.py` on every new view.
- **Manual QA script** (Windows): create PI (2 specs) → convert to JO → 2 DNs
  (attach a file to only 1) → 1 TI (partial) → open Report Generator → confirm
  header/JO release/DN weights/TI balance/reconciliation/**"1 documents missing
  in attachments of delivery note"** and the PDF.

---

## 13. Files read for this analysis

`Views/Reports/README.md`, `ViewModels/Reports/StatementGenerator/*` (main +
WizardState + Navigation), `ViewModels/Reports/StatementModuleViewModel.cs`,
`ViewModels/Reports/Common/ReportBaseViewModel.cs`,
`ViewModels/Reports/PiReport/PiReportViewModel.cs`,
`ViewModels/Reports/SalesReportViewModel.cs`,
`Services/Statement/Workflow/StatementWorkflowEngine*.cs`,
`Models/Statement/Reconciliation/{ReconciliationModels,OrderReconciliationService}.cs`,
`Models/Statement/Workflow/WorkflowWarningItem.cs`,
`Services/Reporting/{ReportingDataService.DrillDown.cs, ReportingModels.cs,
RowTraceabilityService.cs}`, `Models/DeliveryNote/SavedDeliveryNote/*
(SavedDeliveryNote, DeliveryNoteItem, DeliveryNoteAttachment,
DeliveryNoteAnalyticsRow)`, `Services/DeliveryNote/Attachments/*`,
`Services/ProformaInvoice/Attachments/ProformaAttachmentService.cs`,
`Models/ProformaInvoice/Attachments/*`, `Models/TaxInvoice/*`,
`Services/DeliveryNote/Repository/*` (attachment counts),
`Services/TaxInvoice/Repository/*` (attachment counts),
`Models/JobOrder/SavedJobOrder/*`, `Models/ProformaInvoice/Common/*`,
`ViewModels/MainViewModel.Part1/Part2.cs`, `MainWindow.xaml`,
`IMPLEMENTATION_COMPLETE.md`, `IMPLEMENTATION_SUMMARY.md`.

---

## 14. Bug-fix round — filter dropdowns (commit b98e205)

**Reported:** "filters dropdown not getting applied … when I open dropdown click
on it it's still empty."

**Root cause.** `Views/Reports/OrderReport/Filter/FilterStep.xaml` declared the
four filter `ComboBox`es with `IsEditable="True"` but **no `Style`**, so they
inherited the global implicit `ComboBox` style (`Themes/Styles/InputStyles.xaml`
→ `BasedOn="{StaticResource ModernComboBoxStyle}"`). That template is
non-editable and has **no `PART_EditableTextBox`**. An editable WPF ComboBox
requires that part; without it the box opens empty and the `Text` binding never
receives the clicked/typed value, so the filter never applied.

**Fix.** Applied `ModernEditableComboBoxStyle` (editable template with
`PART_EditableTextBox`, same style used by `InquiryEditorWindow`) to all four
dropdowns, plus `IsTextSearchEnabled="True"`.

**Additional module bugs found & fixed in the same round:**

1. **Salesman filter was dead code.** `MatchesFilter` never checked
   `_filterSalesman` — the salesman name lives in the PI snapshot
   (`Client.SalesRepresentative`), not in `ProformaListRow`. Added
   `ApplySalesmanFilterAsync`, reading each candidate PI's `FullSnapshotJson`
   and matching `Client.SalesRepresentative` / `PreparedBy.Name` /
   `Client.DocumentPreparedBy` — identical keys to
   `StatementAggregator.SearchSalesmanInProformaInvoice`.
2. **No cascade scoping.** Customer/Project dropdowns were flat. Added
   `RefreshCustomersForSalesman()` and `RefreshProjectsForCustomer()`
   (mirroring `StatementGeneratorViewModel`), with stale-selection clearing.
   `_allClients` / `_allProjects` are kept in `LoadMasterDataAsync`.
3. **Status chips omitted "Sent".** Hardcoded `{ Draft, Approved, Rejected,
   Cancelled, Expired }` missed `Sent`; now uses `ProformaInvoiceStatus.All`
   (`Draft/Sent/Approved/Rejected/Expired/Cancelled`).
4. **Date range used `CreatedAt`, not the PI issue date.** Added
   `ProformaListRow.IssueDate` (projected from the saved PI) and filter on it,
   falling back to `CreatedAt` for legacy rows.
5. **No live feedback.** Added a `MatchCountDisplay` banner on the Filter step
   (auto-refreshes on every filter change) so users can see matches before
   committing to Generate.

---

## 15. Bug-fix round — sequence, company default & industrial redesign

**Reported:** "sequence is wrong its company salesman customer project. and company
should be auto selected as per our company module because its our company so
company step one should be here selected by default. and they show related data
only. and design you copied from statement generator … redesign industrial
pattern based with proper senior analysis."

**1. Field sequence.** Reordered Step 1 to `Company → Salesman → Customer →
Project` (was Company → Customer → Project → Salesman). The Filter step now
renders a 4-stage "SOURCE SEQUENCE" rail with numbered stage badges (01–04) and
connector arrows so the cascade reads left-to-right exactly as the business flow.

**2. Company auto-selected (our company).** `OrderReportGeneratorViewModel`
resolves "our company" from the Company module records in
`LoadMasterDataAsync`: `IsDefault` profile, else the most-used profile
(`GetAllCompaniesAsync` already returns UsageCount DESC). The resolved name is
cached in `_ourCompany` and pre-selected into `FilterCompany`:
- first load auto-selects when nothing else is restored;
- `ClearFilters()` resets Company back to our company (company is *context*,
  not a clearable filter);
- `PrepareEdit()` falls back to our company when a saved report has no company.

**3. Related-data cascade.** Salesman scopes Customer (`SavedClient.SalesRepresentative`),
Customer scopes Project (`SavedProject.CustomerName`), with stale-selection
clearing — carried over from round 14. Note: the master-data model has **no
company→salesman link** (`SavedSalesman` has only Name/Department), so the
Salesman list shows all of our company's salesmen; the downstream two stages
are the true related-data cascade.

**4. Industrial redesign.** `FilterStep.xaml` rewritten away from the Statement
Generator card layout: uppercase eyebrow headers with accent rule dividers,
numbered stage badges, a live match-counter chip in the header, a DATE WINDOW
section, and a STATUS GATE of LED chips (per-status LED colors via
DataTriggers). XAML passes `xaml_wellformed.py`, `stray_attr_check.py` and
`dup_property_check.py`; all StaticResource/DynamicResource keys verified
against the theme dictionaries; all 18 bindings verified against
`OrderReportGeneratorViewModel` / `StatusCheckItem`.

---

## 16. Round — Step 5 Specifications, export & professional layout

**Reported:** "specification etc these should be step 5 type in reports only related
to specifications only … include excel csv pdf export … for separation use like
pi 1 spec 1 - specification then row data and in end total sum result right
after the column where columns end exact same position in height … check step 9
that have few module which have advance report pattern … update the pdf layout
more professionally."

**Senior analysis.** The Proforma Invoice module's **Step 5 (DataGrid)** and
**Step 9 (Profit/Loss comparison)** are the house reference patterns: Step 5
renders spec groups with a numbered badge + "Specification" header, a shared
column grid, and a footer row whose **Qty / SQM / Value totals sit in the SAME
grid columns as the data** (lines 456–476 of `DataGridView.xaml`). Step 9 adds
KPI cards, header/subheader and row-highlight styling. The export machinery
(`Services/Reporting/ReportingExportService.cs` → `CsvExportEngine` /
`ExcelExportEngine` / `PdfExportEngine`) already provides generic
`ExportCsvAsync<T>` / `ExportExcelAsync<T>` / `ExportPdfAsync<T>` used by every
Reports-hub screen — no new engines needed.

**Implemented.**

1. **Step 5 — Specifications.** New `Views/Reports/OrderReport/Specifications/
   SpecificationsStep.xaml` (+ code-behind, csproj) renders the bundle as
   `PI 1 · <id>` cards, each containing `Spec 1 — <specification>` blocks with
   a shared 8-column grid (`# | Glass Ref | Specification | Dimensions | Qty |
   SQM | Rate | Amount`) and a **footer total row using the identical column
   definitions**, so sums land exactly under Qty / SQM / Amount. A PI-level
   grand-total strip (Qty / SQM / Value chips) follows the groups.
2. **Grouping model.** `Models/OrderReport/OrderReportSpecGroups.cs`:
   `OrderReportSpecPiGroup` / `OrderReportSpecGroup` (computed, not persisted)
   + `OrderReportSpecExportRow` (flat export row). The VM rebuilds them from
   `LastBundle` via `RebuildSpecificationGroups()`.
3. **Wizard 4 → 5 steps.** `OrderReportGeneratorViewModel` extended:
   `IsStep5Complete/Active`, `CanNavigateToStep5`, `CanGoNext < 5`,
   `ShowNextButton` (steps 1/3/4), `ShowGenerateButton` (step 2),
   `ShowFinishButton` (step 5), new `FinishCommand` (step 5 → save & back to
   list). `IsStep4Complete` now means "report exists" (was "PDF exists").
   Shell stepper + content + footer updated to 5 steps.
4. **Export.** `ExportCsvCommand` / `ExportExcelCommand` / `ExportPdfCommand`
   on the Step 5 toolbar — CSV/Excel stream the flattened spec table via
   `ReportingExportService`; PDF opens the generated report PDF.
5. **PDF.** `OrderReportPdfExporter.RenderSpecTable` now groups rows by spec
   group with a "Spec N — <name>" band, a column-aligned per-group total row
   (Qty / SQM / Amount under their columns) and a "PI Total" footer strip.

**Verification.** New/changed XAML passes `xaml_wellformed.py`,
`stray_attr_check.py`, `dup_property_check.py`; all resource keys resolve;
all bindings verified against the VM and the new group POCOs; csproj parses as
XML. No `Tests/` source-level contract references the old 4-step wizard, so
nothing regresses.
