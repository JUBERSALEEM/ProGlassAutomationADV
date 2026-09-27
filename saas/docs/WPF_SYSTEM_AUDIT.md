# WPF SYSTEM AUDIT — ProGlass Automation Desktop ERP

> **Document status:** Phase 1 deliverable (STEP 2 of the SaaS master plan).
> **Method:** Every fact below was read from source code in this repository — not inferred from filenames. File paths are cited so every claim can be re-verified.
> **Date:** 2026-09-27 · **App version audited:** 2.0.11

---

## 1. Executive Summary

ProGlass Automation is a **single-company, multi-user Windows desktop ERP** for an industrial
glass fabrication business in the UAE (AED books, 5% VAT, FTA compliance). It is a mature,
heavily-audited codebase (~1,200+ C# files, ~1,148 tests) implementing the full order-to-cash
chain:

```
INQUIRY ──convert──▶ PROFORMA INVOICE ──confirm──▶ JOB ORDER ──dispatch──▶ DELIVERY NOTE
  + BOQ                9-step wizard                (factory floor)          (signed/received)
                              │                                                   │
                              │                                                   ▼
                              └────────── Step 9 P&L snapshot ──────────▶ TAX INVOICE (5% VAT)
                                                                                  │
                                                                                  ▼
                                                                        STATEMENT + REPORTS
```

The business chain (from `docs/WORKFLOW.md`) is: every handoff is a **real conversion with a
real audit record**, not re-keying. Downstream documents never invent data upstream did not
carry. This principle is the core behavioral contract the SaaS must preserve.

**Key architectural facts that shape the SaaS design:**

| Fact | Detail | SaaS consequence |
|---|---|---|
| 16 separate SQLite database files | Each module owns its own `.db` under one data root | Consolidate into one PostgreSQL schema with `TenantId`; migration must read all 16 |
| Multi-user via LAN file share | `AppDataRoot` supports a shared data root with explicit fallback state (`SharedDataRootFallbackReason`) | Replaced by a real server; keep "explicit degraded-state" UX principle |
| Documents stored as JSON snapshots | `SavedProformaInvoice.FullSnapshotJson` etc. + indexed metadata columns | Normalize hot query fields; keep snapshot JSON for wizard-state fidelity |
| Roles/permissions enforced in operations | `OperationGuard.Demand(...)` at service layer, not just hidden buttons | Reproduce as server-side authorization policy |
| Money = `decimal`, TEXT affinity in SQLite | `MoneyRounding` = 2 dp, half-away-from-zero, AED | `NUMERIC(18,2)` in PostgreSQL, same rounding mode |
| Deterministic optimization engine | Pinned by oracle tests; calibrated against a real DGU benchmark | Port algorithm verbatim; keep oracle tests as regression suite |

---

## 2. Technology Stack (verified from `.csproj` files)

- **Target:** `net8.0-windows`, C# 12, WPF, MVVM (manual `ViewModelBase`/`OnPropertyChanged`)
- **Solution:** `ProGlassAutomation.slnx` containing:
  - `ProGlassAutomation.csproj` — the WPF app (v2.0.11)
  - `Licensing/ProGlass.Licensing.csproj` — license contract library (`net8.0`)
  - `Licensing/ProGlass.Licensing.Tests/` — licensing tests
  - `Tools/LicenseGenerator/` — offline license generator tool
  - `Tests/ProGlassAutomation.Tests.csproj` — 288 test files (~1,148 tests)
- **Persistence:** EF Core (`Microsoft.EntityFrameworkCore.Sqlite`) — 16 `DbContext`s, no
  EF migrations; `EnsureCreated` + custom `AppSchemaMigrations` (1,582 lines) and
  `SchemaMigrationRunner`
- **SQLite hardening:** `SqliteResilience` — WAL, `busy_timeout=5s`, `foreign_keys=ON`;
  decimal columns stored as TEXT affinity (D-6 finding) to avoid REAL float drift
- **Excel:** ClosedXML · **CSV:** CsvHelper · **PDF:** QuestPDF + PdfSharp-WPF · **Graphics:**
  SkiaSharp (layout rendering, 3D glass icons)
- **AI:** LLamaSharp (+CPU backend) local LLM, Gemini/Groq/Cohere cloud services,
  Whisper.net speech-to-text, NAudio capture, `aimemory.db`
- **Deployment:** Velopack auto-updates · **DI:** `Services/DependencyInjection/ServiceRegistration.cs`
- **Other:** `System.Management` (machine fingerprint), DPAPI `ProtectedData` (SecretVault),
  `System.Speech`, screen recording (`Services/Recording`)

---

## 3. Data Root & Persistence Model

`Services/Database/AppDataRoot.cs` — single folder for all ERP SQLite files:
`%AppData%\ProGlassAutomation\` (demo mode uses `ProGlassAutomation.Demo`). Supports a
**shared LAN/Wi-Fi data root** configured from Settings; an unreachable share falls back to
local **explicitly** (red banner + `SharedRootFallbackReason`) rather than silently diverging.
That explicit-degradation principle must survive into SaaS (e.g., degraded read replicas).

### 3.1 Database inventory (all 16 files, verified by grep across DbContexts)

| # | File | DbContext | Contents |
|---|---|---|---|
| 1 | `proglass.db` | `AppDbContext` | `GlassSheets`, `SheetHistories`, `InventoryItems` (Sheet Store + consumables) |
| 2 | `proformainvoices.db` | `ProformaInvoicesDbContext` | `SavedProformaInvoice` (metadata + `FullSnapshotJson`) |
| 3 | `specifications.db` | `SpecificationsDbContext` | Saved specifications (priced glass configs from Step 4) |
| 4 | `inquiries.db` | `InquiryDbContext` | `InquiryRecord` + lifecycle |
| 5 | `joborders.db` | `JobOrdersDbContext` | `SavedJobOrder` (+ analytics rows) |
| 6 | `deliverynotes.db` | `DeliveryNotesDbContext` | `SavedDeliveryNote`, items, attachments |
| 7 | `taxinvoices.db` | `TaxInvoiceDbContext` | `SavedTaxInvoice` (family: PI → DN → JO links) |
| 8 | `creditnotes.db` | `CreditNoteDbContext` | `SavedCreditNote` (fiscal corrections) |
| 9 | `statements.db` | `StatementDbContext` | `SavedStatement`, line items, reconciliation |
| 10 | `masterdata.db` | `MasterDataDbContext` | `SavedClient`, `SavedCompany`, `SavedProject`, `SavedSalesman` |
| 11 | `optimization.db` | `OptimizationDbContext` | Optimization sessions/results (document-keyed) |
| 12 | `production.db` | `ProductionDbContext` | Production pieces, stages, QC, material reservations |
| 13 | `orderreports.db` | `OrderReportDbContext` | Order report documents |
| 14 | `audit.db` | `AuditDbContext` | `AppUser` (login accounts), `ActivityLogEntry` |
| 15 | `aimemory.db` | `AiMemoryDbContext` | AI agent memory |
| 16 | (agent) | `Agent/Storage/AgentDbContext` | In-app AI agent state |

### 3.2 Document persistence pattern (critical for migration)

Financial/production documents follow one pattern (verified in
`Models/ProformaInvoice/SavedInvoice/SavedProformaInvoice.cs`):
- **Indexed metadata columns** for lists/filters: `ProformaId` (unique index), `InquiryId`,
  `CompanyName`, `CustomerName`, `ProjectName`, `Status`, `IssueDate`, `ValidUntil`,
  `CreatedAt`, `UpdatedAt`, totals (`GlassMaterialsSubtotal`, `OtherChargesSubtotal`,
  `DiscountAmount`, `VatAmount`, `GrandTotalVatInclusive` as `DECIMAL(18,2)`), `IsVoid`/
  `VoidedAtUtc`/`VoidedBy`/`VoidReason` (soft void, not delete), `RowVersion` (optimistic
  concurrency), `RootProformaId` + `RevisionNumber` (revision chain)
- **`FullSnapshotJson`** — complete wizard state as a versioned POCO snapshot
  (`SchemaVersion`, `FurthestStepReached`, step-by-step snapshot classes). Backward
  compatibility is explicit: new fields deserialize to defaults, old JSON always loads.

---

## 4. Module Inventory (verified from `Views/`, `ViewModels/`, `Services/`, `Models/`)

| Module | Evidence | What it does |
|---|---|---|
| **Dashboard** | `Views/Dashboard/` (Overview + panels per module, `IndigoDashboardPanel`) | Metrics per business area: DeliveryNote, JobOrder, ProformaInvoice, SheetStore, Statement, TaxInvoice |
| **Inquiry** | `Views/Inquiry/`, `Services/Inquiry/` | Intake register: `Received · Prepared · Sent · Confirmed · Hold · Cancelled`; attachments (preview window), BOQ capture, priority, convert → PI (`InquiryBoqConversion`) |
| **Proforma Invoice** | `Views/ProformaInvoice/` — 9-step wizard | Step 1 Company, 2 Client Profile, 3 Project Specs, 4 Pricing Estimator, 5 Data Grid, 6 Other Charges (+ Wastages), 7 Print Preview, 8 Attachments, 9 Profit & Loss |
| **Specifications** | `Views/Specifications/`, `Services/Specifications/` | Registry of priced glass specs produced by Step 4 APPLY; reused in Step 5; `SpecificationLedgerService`, `SpecPaneResolver`, `SpecificationUsage` |
| **Sheet Store** | `Views/SheetStore/` (Buy/Use/History/Items/CSV) | Glass sheet inventory + consumables (`InventoryItem`: spacer, sealant, desiccant, cavity gas, hardware — stocked in LM) |
| **Optimization** | `Views/Optimization/`, `Services/Optimization/` | Cutting optimization engine (see §7) with sessions, jobs, cut lists, CNC export, preview, margins, costing |
| **Job Order** | `Views/JobOrder/` (+GodView), `Services/JobOrder/` | Factory job documents; `InProduction · Completed · Delivered · Approved` |
| **Production** | `Views/Production/` (Cutting, DoublingLamination, EdgeDeletion, HolesCutouts, Mitring, Pairing, Polish, Process/Control centers), `Services/Production/` | Stage work centers; piece identity, QC, breakage, cancellation, release, material reservation/allocation, distribution engine, physical inventory consumption, optimization bridge, snapshot integrity |
| **Job Profitability** | `Views/JobProfitability/` | Profit analysis per job |
| **Delivery Note** | `Views/DeliveryNote/` (+GodView) | `Draft · Dispatching · Dispatched · Signed · Received · Delivered`; partial delivery + returns; SQM summaries per job |
| **Tax Invoice** | `Views/TaxInvoice/` (+GodView) | `Issued · Paid · Overdue`; linked to PI/DN/JO; `InvoiceTotalsCalculator` |
| **Credit Note** | `Models/CreditNote/`, `Services/CreditNote/` | Fiscal correction document; delete/void is Manager/Admin-only |
| **Statement** | `Views/Reports/Statement*`, `Services/Statement/` | Customer statements with filters (company/customer/project/salesman/date), reconciliation, workflow pipeline (Filter→Pipeline→Preview→Review) |
| **VAT Submission** | `Views/Reports/VatSubmission/`, `Services/Reporting/ReportingDataService.Vat.cs` | UAE FTA 5% VAT reporting from issued tax invoices |
| **Reports** | `Views/Reports/` | Company, Customer, Delivery, Executive, GlassSpec, Jo, Order, Pi, Profitability, Project, Salesman, Statement, TaxInvoice + Cutting/ProductionControl/ProductionProcess reports, `CncExport`, `ReportingExportService`, `SpecificationIntelligenceService` |
| **Company Center** | `Views/CompanyCenter/`, masterdata.db | Companies (TRN, logo, rep, bank details), Clients (TRN, billing address, contact), Projects (contract no, LPO, site location), Salesmen |
| **User Center** | `Views/UserCenter/`, audit.db | User accounts, roles, password policy, lockout |
| **Settings** | `Views/Settings/`, `Services/Settings/` | Data root (LAN share), margin defaults (SETTINGS tab of optimizer), localization (15 languages), currency, theme, optimization house margins |
| **Licensing** | `Views/Licensing/`, `Services/Licensing/`, `Licensing/` | Trial (auto-trial manager, trial ledger), signed license keys, offline activation, read-only gate, machine fingerprint |
| **AI** | `Services/AI/`, `Views/AI/`, `Agent/` | Cloud + local LLM assistance, voice, memory, in-app agent (NLU/intents/knowledge) |
| **Sharing** | `Views/Sharing/`, `Services/Sharing/` | WhatsApp/email document sharing with FROM identity (user name/role/mobile/email) |
| **Diagnostics / Crash Reports** | `Views/Diagnostics/`, `Views/CrashReports/` | Health checks, crash reporting |
| **Documents / Recording** | `Services/Documents/`, `Services/Recording/` | Attachments per document; screen recording |
| **Network presence** | `Services/Network/UserPresenceService.cs` | Which workstation/user is active on the shared DB |
| **Backup/Restore** | `Services/Database/DatabaseBackup/` | `BackupService`, `RestoreDrillService` (restore drills are a feature!) |
| **Audit** | `Services/Audit/` | `ActivityLogEntry`, `AuditService`, `OperationGuard`, `RecordMetadataService`, `VersionHistoryService` |
| **Demo Hub / Sample Data** | `Services/SampleData/`, demo mode | Isolated demo data root |

---

## 5. Business Rules & Formulas (extracted verbatim from source)

### 5.1 Money policy — `Services/Reporting/MoneyRounding.cs`

- Money = **`decimal`** only; SQLite stores money as **TEXT affinity** (D-6) to keep exactness
- **2 decimal places** (AED fils; pinned to `CurrencyCatalog.Find(BaseCurrency).Decimals` by test)
- **`MidpointRounding.AwayFromZero`** (half rounds up — matches hand reconciliation; the
  legally-filed invoice/VAT layer always used this, so it wins)
- Areas/quantities/percentages are **NOT** money-rounded: panels 3 dp, sheet areas 4 dp

### 5.2 VAT — `Services/Reporting/VatCalculator.cs` (UAE FTA, 5%)

```
VAT_RATE = 5%       VAT_MULTIPLIER = 1.05
VatFromSubtotal(gross)  = Round(subtotal × 0.05)
VatFromGross(gross)     = Round(gross) − Round(gross / 1.05)   // back-calculation
GrossFromSubtotal(sub)  = Round(sub × 1.05)
ValidateVat(sub, vat)   = |expected − actual| ≤ 0.01 (1 fils tolerance)
```
Tax configuration is currently a **constant**, not per-tenant — SaaS must make the rate
per-tenant config (default 5%) while preserving the formulas.

### 5.3 SQM / units — `Utilities/SqmCalculator.cs` (single source of truth)

```
Panel SQM (billable) = Round(W_mm × H_mm / 1_000_000, 3)   with 0.5 m² MINIMUM FLOOR
Total SQM            = Round(PanelSqm × Qty, 3)
Sheet area (physical)= Round(W_mm × H_mm / 1_000_000, 4)   NO floor (inventory/cost only)
```
The 0.5 m² billable floor is a UAE glass-industry rule. Optimizer areas also round to 4 dp
with `AwayFromZero` (`SheetLayout.UsedSqm/StockSqm`).

### 5.4 Pricing engine — Step 4 Pricing Estimator (authoritative formulas)

Source: `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part2.cs`
and `DguLamParameters/DguLamParametersViewModel.cs`. All rates are **AED per SQM**, final
result passed through `MoneyRounding.Round`. Yield factor ≤ 0 falls back to 1.0 **with a
visible warning** (BUG-40/41 — never silently reset).

**SGU (single glazed unit)** — `CalculateSguRate()`:
```
step1 = BasePricePerSqm
step2 = step1 / YieldFactor
step3 = step2 + CuttingCostPerSqm
step4 = step3 + TemperingCostPerSqm
rate  = Round(step4 × (1 + MarginPercent/100))
```
Defaults: 6mm Clear FT Glass, Base 46, Cutting 15, Tempering 25, Yield 0.85, Margin 15%.
Cost basis (pre-margin): `Base/Yield + Cutting + Tempering`.

**DGU (double glazed unit)** — `CalculateDguRate()`:
```
step1 = OuterBasePrice + InnerBasePrice
step2 = step1 / YieldFactor
step3 = step2 + SpacerPrice            // ASP = aluminium spacer profile, per SQM
rate  = Round(step3 × (1 + MarginPercent/100))
```
ASP widths 6–26mm; optional "With U-Insert". Spec name pattern:
`"6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass"`.

**LAM (lamination)** — `CalculateLamRate()`:
```
step1 = OuterPrice + InnerPrice
step2 = step1 / YieldFactor
step3 = step2 + PvbMaterialRate
step4 = step3 + OuterCutting + InnerCutting
step5 = step4 + OuterTempering + InnerTempering
rate  = Round(step5 × (1 + MarginPercent/100))
```
Interlayer chemistries: PVB, SGP, EVA (all outsourced — each has bought & sold prices);
thicknesses 0.38–2.28mm.

**DGU+LAM (hybrid: 3 panes, 1 air spacer, 1 interlayer)** — `DguLamParametersViewModel.CalculateRate()`:
```
glass   = OuterBasePrice + LamOuterBasePrice + LamInnerBasePrice
running = glass / YieldFactor
running += TotalCuttingCost + TotalTemperingCost + InterlayerSellPrice + SpacerPrice
rate    = Round(running × (1 + MarginPercent/100))
```
Warning (not error) when `InterlayerSellPrice ≤ 0` — "it is NOT included in this rate".

**Other Step 4 behaviors to preserve:**
- Generated spec name: `{Thickness} {Color} {Treatment}[ {Polish}]` — e.g. `"6mm Clear FT
  Glass With Fanhole"`; polish options are a fixed UAE fabrication list (edges, beveled,
  mitring, frosted, sandblasted, fanhole, holes, cutouts, DWG variants, side polish)
- Glass colors load **exclusively from Sheet Store inventory** (built-in defaults removed
  2026-09-10 — never suggest glass not stocked)
- APPLY saves the priced spec to `specifications.db` and stamps existing Step 5 groups using
  it (`GlassPriceStamper`); rate reuse tracked (`SpecificationUsage`)
- `TargetPricing/` — target-price mode: `SpecificationTargetCalculator`, `Step5PriceSource`,
  `TargetSnapshotRecompute` (recompute on snapshot reload)
- Sheet Store integration: `*.SheetStore.cs` partials pull live purchase prices per config

### 5.5 Invoice totals & numbering

- `SavedProformaInvoice` totals: `GlassMaterialsSubtotal + OtherChargesSubtotal −
  DiscountAmount` → `VatAmount` → `GrandTotalVatInclusive` (DECIMAL(18,2))
- Tax invoice: `Services/TaxInvoice/InvoiceTotalsCalculator.cs`; `TaxInvoiceDetailViewModel.FinancialRecalc`
- **PI numbering** (`ProformaInvoiceRepository.SuggestNextIdAsync`): `PI-{year}-NNN`
  auto-increment of trailing digits; single prefix-scoped query; up to 100k probe; GUID
  fallback; 5-attempt retry loop on unique-index collision (concurrent creators bump to next
  free id — never a swallowed DbUpdateException). Unallocated seed ids reallocated on insert.
- **Revisions:** `RootProformaId` + `RevisionNumber` chain; `-R{n}` ids; originals with blank
  root resolve by own id. Duplicate action scrubs snapshot (`ProformaDuplicateSnapshotScrub`).
- **Round-trip integrity:** `InvoiceRoundTo`, `ProfitLossIntegrityCheck`
- **Other charges** (Step 6): `OtherChargeAutomationRules`, predefined charges with per-spec
  targeting (`SelectedMetricKey` + `CheckedSpecGroupNumbers` — SQM/LM/Qty basis)
- **Wastages section** (`WastagesSectionViewModel`): rows tie to optimization racks —
  `StockSqm`, `GlassSqm`, `UtilizationPercent`, `SheetQuantity`, `OptimizationPercent`,
  `FormulaPricePerSqm`, `Rate`; builds per-group wastage charge rows for outer/inner panes

### 5.6 Status vocabularies & lifecycle (the contract — see `docs/WORKFLOW.md`)

- **Inquiry:** `Received · Prepared · Sent · Confirmed · Hold · Cancelled`
- **Proforma (doc status):** `Draft · Sent · Approved · Rejected · Expired · Cancelled`
- **PI approval workflow** (`InvoiceLifecycleStateMachine`): `Draft → PendingApproval →
  Approved → Sent → Confirmed`, any → `Cancelled`, `Rejected` returns to Draft. Both
  `CanTransition()` and UI-driven transitions exist; descriptions attached to every edge.
- **Job Order:** `InProduction · Completed · Delivered · Approved`
- **Delivery Note:** `Draft · Dispatching · Dispatched · Signed · Received · Delivered`
- **Tax Invoice:** `Issued · Paid · Overdue`
- Unknown statuses **pass through untouched** — never remapped ("the single worst failure
  mode an ERP can have"). Preserve in SaaS.
- Fiscal documents use **void, not delete** (`IsVoid/VoidedAtUtc/VoidedBy/VoidReason`), with
  delete/void of posted fiscal docs restricted to Manager/Admin (ERP Phase 3 G-6).

### 5.7 Roles & permissions — `Services/Audit/Permissions.cs`

Roles: **Administrator, Manager, Estimator, Sales, Production, Store, Accountant, Viewer**
(+ legacy `Operator` mapped to Store set). Permissions are `Module.Capability` strings:
Dashboard.View; Inquiry.View/Create/Edit/Delete; ProformaInvoice.View/Create/Edit/Delete/
Export/Print; JobOrder.View/Edit/Delete; DeliveryNote.View/Edit; TaxInvoice.View/Edit/Export/
Delete; CreditNote.View/Edit/Export/Delete; SheetStore.View/Create/Edit/Delete;
Reports.View/Export; Statement.Generate; Logs.View; Audit.View; Settings.Manage; `*` wildcard
for Administrator. **Enforcement is in the operation** (`OperationGuard.Demand` inside
repository/service methods), never only in hidden UI buttons. Example grants: Manager gets
all views + full create/edit/delete incl. fiscal deletes; Estimator gets inquiry+proforma
create/edit but no fiscal delete; Accountant edits/exports but cannot delete posted invoices.

### 5.8 Users & authentication — `Models/Audit/AppUser.cs`, `Services/Security/`

- Local accounts in `audit.db`: Username (unique), DisplayName, Role, Mobile, Email,
  `PasswordHash = SHA-256(salt + password)` hex + per-user salt, IsActive (never delete
  users — orphaned audit), CreatedAtUtc, LastActiveAtUtc, FailedAttemptCount, LockedUntilUtc
  (lockout survives restarts). `PasswordPolicy`, `PasswordHasher`, `AuthSessionService`,
  `SecretVault` (DPAPI), `ProcessStartGuard`, attachment validation service.
- **SaaS note:** SHA-256(salt+pwd) is weaker than modern KDFs; the SaaS must use
  Argon2id/bcrypt/PBKDF2 (architectural translation, documented, not a behavior change).

---

## 6. Sheet Store (inventory) rules — `Models/SheetStore/`, `Services/SheetStore/`

- `GlassSheet` identity = **unique index (Category, ThicknessMm, Color, WidthMm, HeightMm,
  Supplier)**; prices per SQM (purchase & sell); stock tracked as **sheet quantity**
  (`Stock`, `Used`, `Balance = Stock − Used`); computed values (TotalSqmInStock/Used/Balance,
  TotalPurchaseValue, TotalSellValue, ProfitValue, UsedCostValue/UsedSellValue/UsedProfitValue)
  — all `SqmPerSheet × qty × price` with 4 dp sheet areas
- `SheetHistory` — **every stock movement auditable** (indexed by SheetId + Timestamp);
  `ValueChanged` stored as TEXT (exact decimal)
- `InventoryItem` — non-glass consumables: spacer bar (**stocked in linear metres**, e.g.
  45.55 LM), sealant, desiccant, cavity gas, hardware; Stock/Used/ReorderLevel and prices as
  TEXT-affinity decimals; category & supplier indexes; append-only INT enum persistence
- Views: Buy / Use / History / Items / CSV import-export; low-stock alerts
  (`GlassSheetLowStock`); `SheetStorePricingService` + `SheetStockLinkService` link PI specs
  ↔ live store prices; optimization consumes balances via `StockSize.MaxSheets` with
  explicit sentinels (`UnlimitedSheets = 0`, `EffectivelyUnlimitedSheets = 99999999`)

---

## 7. Optimization Engine — `Services/Optimization/` (the crown jewel)

**Do not rewrite casually.** ~10,900 lines across `GuillotinePacker.*`. Pure domain models —
no WPF, no EF; fully unit-testable and pinned by oracle tests. Calibrated against the owner's
real DGU benchmark: **Low-E 17 sheets @ 82.77%, Clear 16 sheets @ 84.77/84.92%, Ramly 1 sheet
on 2500×1875** (15mm margins, kerf 3, adaptive 0/90).

### 7.1 Domain enums (actual code — supersedes any assumption)

- `RotationPolicy`: `Locked` (exact W×H) · `Free90` (**ADAPTIVE**: keep original orientation
  unless it doesn't fit; the floor's preferred 0/90 mix; default)
- `SplitMode`: `HorizontalFirst` (calibrated default) · `VerticalFirst` · `LongerLeftover`
- `PlatePattern`: `Yield` (default: fewest unplaced → fewest sheets → utilization) ·
  `FillFirstPlate` · `FewestCuts` · `MinimiseStockChanges` · `RemnantFirst` · `BalancedSpread`
  — all constrained to never add sheets or unplaced pieces vs the Yield winner
- `CutComplexity`: `FreeGuillotine` (default, bit-identical legacy) · `SimpleFirstX` ·
  `SimpleFirstY` · `Simple` (2-stage auto) · `ThreeStage` · `ComplexTrims` (Z/W trim cuts).
  Resolved into immutable `CutPolicy` (MaxStages, forced first direction, AllowTrimCuts,
  MinTrimWidthMm). **LAMINATE RULE: Z/W trims never cut into laminated stock** (plies don't
  hand-break; refused trim costs yield, trim into laminate costs the sheet).
- `ScaleTier`: `Standard / Large / Huge` — deterministic adaptive search budget by part count
  (`OptimizationSearchGovernor`); reductions recorded as visible `ScaleNotes`, never silent
- `ThicknessBucket`: `Thin (≤6mm) · Medium (7–8) · Thick (≥9)`; unlisted thicknesses **round
  UP** (conservative); unknown/non-positive → Thin (6mm fallback)

### 7.2 Margin profiles (house defaults, editable in SETTINGS tab)

| Bucket | LM/RM/TM/BM | Kerf | MinBreakout |
|---|---|---|---|
| Thin ≤6mm | 15/15/15/15 | 3mm | 15mm |
| Medium 7–8mm | 30/30/30/30 | 3mm | 30mm |
| Thick ≥9mm | **50/50/0/50** (owner-confirmed 2026-09-21: no top trim) | 4mm (≥10mm) | 50mm, switchable to **40mm** (only sanctioned alternative, thick bucket only) |

`IsCustomized` is an **explicit fact** decided where operator input is read — never inferred
from which numbers are zero (Phase 1 finding 3).

### 7.3 Algorithm stages

- **PackQuick** — deterministic portfolio: area-first shelf, capacity strips, bounded pattern
  beam, horizontal-first guillotine splits, adaptive rotation, consolidation
- **PackOptimize** — multi-start (orderings × split modes; up to **147 complete plans**),
  per-sheet split selection + beam (`BestSplit.cs`), smart placement policy
  (`SmartPlacement.cs`), each consolidated, best wins on **sheets first, then utilization**
  (utilization is purely a function of sheet count — total glass area is fixed)
- **PackSmart** — capacity-preserving pass; scores placements by what SURVIVES (least area
  condemned to sub-breakout trim, then largest remaining free rectangle); both split
  directions evaluated per placement; honors `MinBreakoutMm`
- Specialized partials: `Advanced` (MaxRects), `LocalSearch`, `OffcutReuse`, `Remnants`,
  `RepeatedPatterns/RepeatMaster/RepeatPricing/RepeatInteger/RepeatTail`, `Partitioned`,
  `PatternSearch`, `CapacityStrips`, `FillFirstPlate`, `Smart`, `BestSplit`

### 7.4 Data model & guarantees

- `StockSize` (W×H×thickness×color×label×MaterialKey + `MaxSheets` cap from Sheet Store
  balance), `CutPiece` (job/glass refs, spec label, position, color, thickness, W×H,
  AllowRotate, piece label like `"G-101 · 3/24"`, full provenance: ProformaId, JobOrderId,
  SpecGroupNumber, SourceRowSerial, GlassType, Processing, Coating, W1/H1/W2/H2, SheetStoreSku,
  sheet size + price, Qty), `PlacedPiece` (integer mm X/Y/W/H + Rotated), `SheetLayout`
  (usable rect, pieces, `Cuts` list, `Leftovers`, TrimCutCount, UsedSqm/StockSqm/UtilizationPct),
  `CutStep` (Seq, Axis, AtMm, rect, Stage, IsTrim), `RemnantRegion`
- **Two independent oracles** (result carries the verdict):
  - `Verify` — geometry: margins, rotation policy, dimension integrity, **no overlap**, kerf
  - `ValidateResult` — conservation: every input piece placed exactly once, nothing
    duplicated, `input count == placed + unplaced`
  - A failing plan is displayed for inspection but **never exported to a cut file and never
    feeds production** — this is exactly the server-side validation the SaaS API must enforce
- Instrumentation: UncuttableSheetCount, InconsistentCutSheetCount, SheetsLowerBound (area
  bound), OptimalityGapSheets/Percent
- Sessions persisted in `optimization.db` keyed by document (`OptimizationDocumentKey`);
  `OptimizationSessionIdentity`, `CompositionKey`, `OptimizationMaterialIdentity`;
  CNC export (`CncExport`), cut lists replayed **in stage order**, report export
- `ProductionOptimizationBridge` feeds validated layouts into `production.db` pieces

---

## 8. Production module (factory floor)

Stage work centers (Views/Production): Cutting, Doubling/Lamination, Edge Deletion,
Holes/Cutouts, Mitring, Pairing, Polish, Process + Control centers. Services: piece identity,
QC, breakage, cancellation, release, `MaterialReservationService`/`MaterialAllocationService`
(reserve glass before cutting), `ProductionDistributionEngine` (distribute pieces to stages),
`PhysicalInventoryConsumptionContract` (consume Sheet Store on physical use),
`ProductionInventoryConsistencyScanner`, `ProductionDeliveryGateway`,
`ProductionSnapshotIntegrity`, `ActualCostService`, `DguDimensionSemantics`.

---

## 9. Reporting & import/export inventory

- **Reports (server-side data, filters, export):** Company, Customer, Delivery, Executive
  (ExecutiveSummary), GlassSpec, JobOrder, Order, PI, Profitability, Project, Salesman,
  Statement, TaxInvoice, VAT Submission, Cutting report, Production Control, Production
  Process, Job Profitability, Specification Intelligence (`SpecificationMatcher` + cache)
- **Analytics:** `ReportingDataService.*` partials per domain (Pi/Jo/Delivery/TaxInvoice/
  Statement/Vat/Profitability/Executive/GlassSpecs/MasterData/DrillDown), `ReportingAnalyticsBuilder`,
  `ReportingCacheService`, `RowTraceabilityService`
- **Excel/CSV:** ClosedXML engine + CsvHelper engine with layout interfaces
  (`IExcelLayout`, `ICsvLayout`, `WorkbookStyle`); Sheet Store CSV import/export; PI Excel/CSV
  import/export; `CutListGridIo` (Step 5 cut-list round-trip)
- **PDF/print:** QuestPDF + PdfSharp engines, `PageLayout`/`PdfTheme`/`DocumentStyle`,
  letterhead, print preview, screenshot service
- **CNC export** for cutting tables

---

## 10. Licensing (desktop model — to be redesigned for SaaS)

`Licensing/` contract library + `Services/Licensing/`: signed license documents
(`LicenseSigner`/`LicenseVerifier`/`LicensePublicKey`, RSA), `MachineFingerprint` binding
(Unbound/SingleDevice/MultiDevice + DeviceLimit), version policy (min/max/allowed majors),
trial system (`AutoTrialManager`, `TrialLedger`, `TrialKeyVault`), **offline activation**
(request/response store), `ReadOnlyGate` (expired trial = read-only, blocking writes at the
service layer), `RegistryAnchor`, assembly integrity, legacy migration inspector, plans
catalog (`LicensePlanCatalog`), and the `Tools/LicenseGenerator` console. SaaS replaces the
machine-bound key with tenant subscriptions (see SAAS_ARCHITECTURE.md §Subscriptions) while
keeping the read-only/degraded-state concept.

---

## 11. Cross-cutting concerns

- **Audit:** `ActivityLogEntry` (who/what/when/what-changed), `AuditChange`,
  `RecordMetadataService` (row-level created/updated by/at), `VersionHistoryService`
- **Logging/observability:** `Services/Logging`, `Services/Observability`, structured logs,
  crash helpers (`App.CrashHelpers.cs`), CrashReports view, diagnostics
- **Localization:** 15 languages, `CurrencyService` (BaseCurrency=AED books vs display
  currency — storage rounds to books precision, display converts), `GeoService`
- **Backup/restore:** `BackupService` + **`RestoreDrillService`** (restore is tested, not
  assumed); DocumentLifecycle; `SchemaHealthService`
- **Concurrency:** `RowVersion` optimistic concurrency; `ConcurrencyConflictException`;
  SQLite `busy_timeout`; 5-attempt id-allocation retry
- **Updates:** Velopack; splash + `App.StartupCore.cs` (59 KB startup pipeline)
- **AI/Agent:** cloud (Gemini/Groq/Cohere) + local LLamaSharp, Whisper.net voice, agent with
  NLU/intent/knowledge/memory, `aimemory.db`

---

## 12. Risks & Technical Debt (SaaS-relevant)

1. **16 separate DBs** — no cross-module FKs; integrity is enforced in services. SaaS schema
   must add real constraints; migration must handle inter-DB references by natural keys.
2. **Snapshot JSON as system of record** — wizard state (steps 1–9) lives in JSON; totals are
   denormalized columns. SaaS needs normalized line items for reporting **plus** a snapshot
   for fidelity; reconciliation between them is a migration risk.
3. **`DateTime.Now` (local) in many entities** vs UTC in audit — SaaS must standardize on UTC
   with tenant timezone for display.
4. **SHA-256 password hashing** — upgrade to Argon2id at migration (documented difference).
5. **Status stored as free strings** with normalizing homes — SaaS should use constrained
   enums + "unknown passes through" policy preserved at API boundary.
6. **Optimization engine complexity** (~11k lines, 20+ partials) — port as-is to a domain
   assembly; do NOT simplify; oracle tests are the acceptance gate.
7. **ViewModel-embedded business logic** (pricing in Step 4 VMs, wastage in Step 6 VM) — must
   be extracted into testable domain services in SaaS (single source, per master prompt §40).
8. **No EF migrations** (EnsureCreated + custom SQL) — SaaS uses versioned EF Core
   migrations from day one.
9. **Single-company data** — every table lacks tenant ownership; multi-tenancy is the biggest
   single change and must be enforced at the query layer (global query filters), never only
   in the UI.
10. **Hard-coded product constants** (AED, 5% VAT, thickness lists, fabrication option
    lists, margin house table) — make per-tenant config with WPF values as defaults.

---

## 13. Migration Requirements (inputs to SAAS_MIGRATION_PLAN.md)

- Read all 16 SQLite files from one data root; map by natural keys (ProformaId, JobOrderId,
  InvoiceNumber, company/customer/project/salesman names)
- Preserve: financial totals (DECIMAL 18,2, AwayFromZero), status strings (pass-through),
  revision chains, void semantics, sheet history, audit trail, optimization sessions
- Convert: local `DateTime.Now` → UTC; password hashes → Argon2id (force reset acceptable);
  file attachments → object storage keys; machine licenses → tenant subscriptions
- Validate: totals recomputation (GlassMaterials + OtherCharges − Discount → VAT → Grand),
  conservation of pieces per optimization session, row counts per table, orphan detection
- The original SQLite files are **never modified** — copy-then-migrate

---

## 14. Verification checklist for this audit

- [x] All `.csproj` files read · [x] All 16 DbContexts located and their tables listed
- [x] SGU/DGU/LAM/DGU+LAM formulas read from implementation (not docs)
- [x] VAT + rounding policy read · [x] SQM rules read · [x] Margin profiles read
- [x] Optimization enums, stages, oracles, calibration benchmark read
- [x] Roles/permissions enumeration read · [x] User model read · [x] PI numbering read
- [x] Lifecycle state machine + status vocabularies read
- [x] Licensing model read · [x] Import/export stack verified from package refs
- [x] Production/Sheet Store/Reports inventories verified from Views+Services

*Anything not listed here that exists in the repo (AI agent internals, theme system, splash,
setup wizard) is presentation-layer or auxiliary and is inventoried in §4 without deep
formula extraction — it carries no pricing/VAT/stock rules.*
