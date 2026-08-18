# PROGLASS AUTOMATION — COMPLETE REPOSITORY DEEP AUDIT

**Date:** 2026-08-18
**Branch audited:** `arena/01a00f5c-proglassautomationadv` @ `f4b2a5c` (HEAD == remote, working tree clean)
**Scope:** Audit only — no code was modified, no commits pushed.
**Method:** Read project/solution files, all 517 `.cs` (~109k LOC) and 134 `.xaml` (~49k LOC) via targeted deep reads, grep scans, and per-module tracing. Anything not provable from source is marked **UNKNOWN**.

---

## A. EXECUTIVE SUMMARY

### Overall health
This is a **large, real, and substantially functional desktop ERP** — not a demo. The primary commercial vertical (Proforma Invoice → Job Order → Delivery Note → Tax Invoice → Statement) is implemented end-to-end with a genuinely impressive amount of engineering: a real (if hand-rolled) design system, DPAPI-protected API keys, PBKDF2 password hashing, a multi-database SQLite architecture with per-operation `DbContext` lifetimes, dashboard analytics, AI assistant + agent, import/export (CSV/Excel/PDF), attachments, version history, audit trail, and a custom backup/restore service.

Strengths observed in code:
- Security posture for credentials is genuinely good: `SecretVault` (DPAPI) for API keys, `ConfigService.Save` masks keys before serialization, `PasswordHasher` uses PBKDF2-HMAC-SHA256 @ 210,000 iterations.
- Robust global exception handling: `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` all wired in `App.xaml.cs`.
- Dashboard metrics use SQL `Take()`/projection rather than loading everything.
- A centralized design system exists (`DesignTokens.xaml`, `ButtonStyles.xaml`, `CardStyles.xaml`, `PillStyles.xaml`, `TypographyStyles.xaml`, `ContextMenuStyles.xaml`, `DashboardStyles.xaml`, `ModuleTabStyles.xaml`).
- 19 xUnit test files covering pricing, permissions, password hashing, profit-loss integrity, offline behaviour, data boundaries.

### Estimated maturity
**DEVELOPMENT READY → BETA READY.** The quote-to-invoice core is beta-quality. The app is **NOT** production-ready for a glass factory end-to-end because the **entire Production module (8 sub-modules) and Optimization are "Coming Soon" placeholders** — i.e., the actual factory workflow is not implemented, and the sidebar presents them as if they were active modules.

### Biggest problems
1. **Production / Optimization are placeholders** shown as real navigation targets (`ComingSoonView`).
2. **No cross-database transactions** across 10 SQLite files → cross-DB business writes can be left half-complete (e.g., Tax Invoice saved, then Delivery Notes marked invoiced in a different DB).
3. **Inconsistent schema-evolution strategy**: only `audit.db` has a hand-written migration (`AuditDbMigrations.cs`); every other DB uses `EnsureCreated()` — schema changes cannot upgrade existing installs.
4. **Sparse permission enforcement**: `OperationGuard.Demand` (9 sites) + `PermissionService.Require` (11 sites) gate far fewer operations than the RBAC model implies.
5. **Inconsistent SQM / value calculations** duplicated inline with different rounding (3 vs 4 decimals).
6. **God ViewModels** (6 files exceed 1,900 lines) make maintenance and testing hard.
7. **~30 views define their own local styles** bypassing the design system.
8. **88 silent `catch {}`** swallow errors (some intentional, some hiding real bugs).
9. **Default `admin/admin` credential** persists until the user changes it (surface risk for a local ERP).
10. **`Debug.WriteLine` (305 uses)** disappears in Release builds — several failure paths rely on it for diagnostics.

### Most important missing systems
- Real Production execution module (cutting/holes/polish/mitring/tempering/pairing/lamination).
- Cross-database transaction/compensation layer for the multi-DB writes.
- A migration/upgrade strategy for the 9 `EnsureCreated` databases.
- Consistent, enforced authorization across all mutating operations.
- A centralized calculation library for SQM/value/profit with a single rounding policy.
- Pagination / server-side filtering for the lists that currently load-all-and-filter-in-memory.

### Release readiness
**NOT PRODUCTION READY** (by the strict standard requested). Core quote→invoice is **BETA READY**; the factory/production workflow is absent. See §P.

### Top 10 issues
1. P0 — Production + Optimization modules are placeholders but exposed as live modules (workflow gap).
2. P0 — Cross-DB write in Tax Invoice save can leave DNs un-invoiced (no transaction/compensation).
3. P0 — 9 of 10 databases use `EnsureCreated` (no upgrade path); schema drift will strand existing installs.
4. P1 — Authorization is inconsistently enforced (sparse `OperationGuard`/`PermissionService`).
5. P1 — SQM/value calculations duplicated inline with inconsistent rounding.
6. P1 — God ViewModels (DataGrid 2225, DeliveryNoteDetail 2134, TaxInvoiceDetail 2119, StatementGen 2074, ProfitLoss 1924, Settings 1795).
7. P1 — Design-system bypass in ~30 views with local styles.
8. P2 — 88 silent `catch {}` + 305 `Debug.WriteLine` (invisible in Release).
9. P2 — In-memory list filtering (load-all) scales poorly to 1k+ rows.
10. P2 — Default `admin/admin` and in-memory-only login lockout.

---

## B. COMPLETE ARCHITECTURE MAP

**Stack:** WPF, .NET 8 (`net8.0-windows`), C#, MVVM, EF Core 8 + SQLite, no DI container.

### Startup flow (`App.xaml.cs`)
1. `Application_Startup` → `StartupCoreAsync`.
2. `AppLogService.Initialize()` (log file in `%AppData%\ProGlassAutomation`).
3. Global exception handlers installed (Dispatcher/AppDomain/UnobservedTask).
4. `ConfigService.Load()` → `App.Config` (keys hydrated from `SecretVault`).
5. `DatabaseInitializer.Initialize()` → `EnsureCreated` on `proglass.db`.
6. Repository singletons created on `App`:
   `Specs, Invoices, Jobs, DeliveryNotes, MasterData, TaxInvoices, Statements`.
7. Parallel `InitializeAsync()` on all repos (EnsureCreated on each DB).
8. `AuditService.InitializeAsync()` (audit.db + hand migration + seed admin), `AiMemoryService.InitializeAsync()`.
9. Theme/outline/accent/card-border applied; `App.Ai = AiProviderFactory.CreateActive(...)`.
10. Speech/text services, `Connectivity`, `Presence`.
11. `SplashView` → `LoginWindow` → `MainWindow`.

### Dependency injection
**NONE.** The app uses a **static service locator**: `App.Config`, `App.Invoices`, `App.Jobs`, `App.DeliveryNotes`, `App.TaxInvoices`, `App.Statements`, `App.Specs`, `App.MasterData`, `App.DashboardMetrics`, `App.Ai`, `App.Connectivity`, `App.SpeechToText`, `App.TextToSpeech`, `App.Presence` are all public static properties. ViewModels reach services via `App.X`.

**Why it matters:** no IoC container makes unit-testing ViewModels hard (they depend on static `App`), and the static graph is created in one place but referenced globally.

### Navigation
- Shell: `MainWindow` → `MainViewModel.CurrentViewModel` (a `ViewModelBase`) → implicit `DataTemplate` resolution (23 templates in `MainWindow.xaml`).
- `MainViewModel.Navigate(target)` switch drives all navigation (sidebar, global search, agent, dashboards).
- Per-module list↔detail toggles (e.g. JobOrder `IsListView`, TaxInvoice `IsListView`, Statement `ShowList`).

### View → ViewModel → Service → Repo → DB flow
`View(.xaml)` binds to `VM`; VM calls `App.<Repo>`; repo `new DbContext()` per op → EF → SQLite file. **No Service layer between VM and Repo for most modules** — business logic lives in the VM (see god-ViewModel finding).

### Global state
`App.Config` (AppConfig), theme via `ThemeManager` static, `App.Ai`, connectivity, presence, audit session (`AuditService.SessionId`).

### Theme system
`ThemeManager.ApplyTheme` swaps Light/Dark dictionaries; `ApplyAccent` derives hover/tint/dark colors; `VisualThemeEngine.Apply` applies Normal/Monet/Professional surface families; `ApplyOutline` and `ApplyCardBorderThickness` overlay user prefs. Resources in both `LightTheme.xaml` (60 keys) and `DarkTheme.xaml` (60 keys).

### Settings system
`AppConfig` → `config.json` in `%AppData%\ProGlassAutomation`. Persisted via `ConfigService.Save`. API keys excluded from JSON (vault-backed).

### Logging / error system
`AppLogService` (file + ring buffer), `AuditService` (audit.db), three global crash handlers, `CriticalOperationTracker` (shutdown guard).

---

## C. COMPLETE MODULE MAP

Legend: status from source.

| Module | View | VM | Status | DB(s) | Notes |
|---|---|---|---|---|---|
| Dashboard (Overview + 7 sub) | `Views/Dashboard/*` | `*DashboardViewModel` | **COMPLETE** | all | real KPIs/analytics |
| Sheet Store | `Views/SheetStore/*` | `SheetStoreViewModel` 953 | **COMPLETE** | proglass.db | CRUD, buy/use, history, CSV/Excel/PDF, import |
| Proforma Invoice (9-step wizard) | `Views/ProformaInvoice/*` | `ProformaInvoiceViewModel` 750 | **COMPLETE** | proformainvoices.db, specifications.db | attachments, versions, snapshots, Step-8 approval gate |
| Job Order | `Views/JobOrder/*` | `JobOrderViewModel` 713 | **COMPLETE** | joborders.db | list/detail, auto-create on PI approval |
| Delivery Note | `Views/DeliveryNote/*` | `DeliveryNoteViewModel` 713 | **COMPLETE** | deliverynotes.db | master/split, history, attachments |
| Tax Invoice | `Views/TaxInvoice/*` | `TaxInvoiceViewModel` | **COMPLETE** | taxinvoices.db | family, from PI/DN, marks DNs invoiced |
| Statement / Sales Report | `Views/Reports/Statement*` | `SalesReportViewModel` (shell) + `StatementGeneratorViewModel` 2074 | **COMPLETE** | statements.db | list + generator + reconciliation |
| Optimization | — | `OptimizationViewModel` (10 lines) | **PLACEHOLDER** | — | `ComingSoonView` |
| Cutting | — | `CuttingViewModel` (10 lines) | **PLACEHOLDER** | — | ComingSoon |
| Holes & Cutouts | — | `HolesCutoutsViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Polish | — | `PolishViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Edge Deletion | — | `EdgeDeletionViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Mitring | — | `MitringViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Tempering | — | `TemperingViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Pairing | — | `PairingViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Doubling / Lamination | — | `DoublingLaminationViewModel` | **PLACEHOLDER** | — | ComingSoon |
| Diagnostics (Logs & Activity) | `Views/Diagnostics/*` | `DiagnosticsViewModel` 271 | **COMPLETE** | audit.db | app log + activity + per-module |
| System Center | `Views/SystemCenter/*` | `SystemCenterViewModel` 462 | **COMPLETE** | audit.db | users, roles, permissions, sessions |
| Company Center | `Views/CompanyCenter/*` | `CompanyCenterViewModel` 411 | **COMPLETE** | masterdata.db | |
| User Center | `Views/UserCenter/*` | `UserCenterViewModel` 295 | **COMPLETE** | audit.db | |
| Job Profitability | `Views/JobProfitability/*` | `JobProfitabilityViewModel` 654 | **COMPLETE** | cross | |
| Settings (7 pages) | `Views/Settings/*` | `SettingsViewModel` 1795 | **COMPLETE** | config.json | |
| Login / Splash | `Views/Login|Splash` | — | **COMPLETE** | audit.db | |
| AI Assistant + Agent | `Views/AI/*`, `Agent/*` | `AiChatViewModel` 918 | **COMPLETE/PARTIAL** | aimemory.db | multi-provider, voice, agent tools |

**Placeholder detection evidence:** `ViewModels/Production/*.cs` and `ViewModels/Optimization/OptimizationViewModel.cs` are all 10 lines extending `ComingSoonViewModelBase` (`ModuleName` + `Description` + `Icon`). `Views/Shared/ComingSoonView.xaml` renders them. Sidebar binds them via `NavigateCommand` as if live.

---

## D. COMPLETE WORKFLOW MAP

**Quote → PI → Job → Production → Delivery → Tax Invoice → Statement**

1. **Proforma Invoice** (9-step wizard): Company → Client → Project/LPO → Pricing → Specs (data grid) → Other charges → Print preview → Attachments → Profit & Loss. Saved as `SavedProformaInvoice` + `FullSnapshotJson`. Version history stored.
2. **Approval gate:** Draft→Approved requires Step-8 attachments (signed PI or LPO). On confirmation the PI is auto-converted to a Job Order. (Verified in code — this was recently implemented.)
3. **Job Order:** created from approved PI via `JobOrderMapper.BuildFromProformaInvoice`. Deleting/rejecting a JO cascades the source PI to `Rejected` (a 1-DB cascade, safe).
4. **Delivery Note:** master + split DNs per job; delivery history; partial delivery with balance tracking.
5. **Tax Invoice:** created from a PI (master) or a DN (partial); marks DNs invoiced.
6. **Statement:** aggregates PIs/TIs; reconciliation logic present (`OrderReconciliationService`).
7. **Reports:** Sales Report shell → Statement list + generator.

### Workflow integrity findings
- **Cross-DB write (P0):** Tax Invoice save writes `taxinvoices.db`, then marks Delivery Notes invoiced in `deliverynotes.db` (`TaxInvoiceDetailViewModel.SaveAsync` → `MarkMultipleAsInvoicedAsync`). If the second write throws, the TI exists but DNs remain un-invoiced; no transaction spans the two files. Re-saving is idempotent but the gap is real.
- **Orphan risk (P1):** No foreign keys across DBs by design. Deleting a PI does not check for existing JO/DN/TI; deleting a JO (with DNs) is possible. Status cascade only handles the approved→rejected case.
- **Status transitions (P1):** Manual "Change Status" menus allow arbitrary transitions (e.g. a DN can be set to `Received` without ever being `Dispatched`); no state-machine validation. Verified in all three list code-behinds (free-form status submenus).
- **Partial delivery (OK):** DN `TotalOrderedQty/TotalDeliveredQty/TotalBalanceQty` tracked; `IsFullyDelivered` computed.
- **Historical integrity (P1):** Completed/approved documents can still be edited and re-saved (no edit-lock on Issued/Approved). `VersionHistoryService` exists but versioning is user-driven, not enforced on every mutation.
- **Half-completed transactions (P1):** Multi-step operations (import, approve-and-convert) do best-effort with per-step DB writes and no rollback; a mid-import failure reloads but does not roll back already-inserted rows.

---

## E. UI/UX AUDIT

### Design consistency
- The app **does** have a global system, but adoption is incomplete. Evidence: **31 views define local `Style x:Key`** resources instead of using the global ones. Worst offenders:
  - `DnSectionStyles.xaml` (38 local styles)
  - `SettingsSectionStyles.xaml` (28)
  - `WizardStyles.xaml` (19)
  - `AboutView.xaml` (12), `DataGridView.xaml` (11), `PrintableInvoiceView.xaml` (10), `JoSectionStyles.xaml` (10)
- Local button styles, corner radii, and paddings in these files diverge from the global `ButtonStyles.xaml`.
- `BorderThickness` was recently centralized to `CardBorderThickness` (good), but many dialogs/inputs still use local `BorderThickness="1.5"` that was *not* migrated (only card/panel borders were converted to `{DynamicResource CardBorderThickness}`; inputs and some controls still hardcode).

### Professional quality flags
- **Placeholder modules shown as live** — `ComingSoonView` for 9 navigation items (misleading).
- God-layout views (`DataGridView.xaml` 1708 lines, `DeliveryNoteListView.xaml` 1202) — hard to maintain, likely layout drift.
- Inconsistent dialog styles: several hand-built window dialogs (`RenameAttachmentDialog`, `VersionsDialog`, `InvoiceApprovalDialog`) each restyle the same chrome.
- **UNKNOWN (runtime):** actual visual contrast, spacing, and overflow can only be confirmed on Windows; I could not run the app in this sandbox.

---

## F. DESIGN SYSTEM AUDIT

**Verdict: PARTIALLY GLOBAL.**

Present & global:
- `DesignTokens.xaml`, `ButtonStyles.xaml`, `CardStyles.xaml`, `PillStyles.xaml`, `TypographyStyles.xaml`, `IconBadgeStyles.xaml`, `ModuleTabStyles.xaml`, `ContextMenuStyles.xaml`, `DashboardStyles.xaml`, `WizardStyles.xaml`.
- Light/Dark theme brushes (60 keys each), accent, Monet/Professional visual engine, `CardBorderThickness`, `ControlCornerRadius`/`CardCornerRadius`.
- Global converters in `App.xaml`.

Violations:
- Per-module style dictionaries (`DnSectionStyles`, `TiSectionStyles`, `JoSectionStyles`, `SettingsSectionStyles`, `WizardStyles`) re-declare typography/buttons/cards locally rather than `BasedOn` the global system.
- `AboutView`, `LoginWindow`, `SplashView`, and most dialogs define local chrome.
- No single **dialog** or **input** or **scrollbar** design token shared app-wide.

Recommended resource architecture (not "one giant file"): keep the theme dictionary + `DesignTokens` + the shared `ButtonStyles`/`CardStyles`/`InputStyles` as global; move the per-section dictionaries to consume the global keys via `BasedOn`; add shared `DialogStyles.xaml`, `InputStyles.xaml`, `DataGridStyles.xaml`.

---

## G. WPF / MVVM ARCHITECTURE AUDIT

- **MVVM largely followed**, but with violations:
  - **Business logic in code-behind:** `JobOrderProformaPickerDialog.xaml.cs` (538 lines), `SplashView.xaml.cs` (530 lines), `DeliveryNoteListView.xaml.cs` (361), `TaxInvoiceList/ProformaPickerDialog.xaml.cs` (319). The list VMs are thin but the code-behind carries conversion/navigation logic.
  - **God ViewModels:** `DataGridViewModel` 2225, `DeliveryNoteDetailViewModel` 2134, `TaxInvoiceDetailViewModel` 2119, `StatementGeneratorViewModel` 2074, `ProfitLossStepViewModel` 1924, `SettingsViewModel` 1795, `PrintableInvoiceViewModel` 1563.
- **Static service locator** (`App.*`) — no DI; hard to unit test VMs.
- **`async void`:** 65 occurrences (mostly event handlers — acceptable but several lack internal try/catch → unhandled exceptions land in the global handler rather than the UI). 
- **Event subscription leaks (MEDIUM):** many VMs subscribe to static events in their constructor (`SampleDataService.DataSeeded`, `ThemeManager.ThemeChanged`, `ProformaInvoiceRepository.SavedInvoicesChanged`, `AuditService.CurrentUserChanged`). Module VMs are singletons in `MainViewModel`, so today this is mostly benign; but any VM created per-dialog/record that subscribes to a static event leaks. `SavedInvoicesListViewModel` subscribes to `SavedInvoicesChanged` (created once — OK).
- **ObservableCollection misuse:** `SheetStoreViewModel.ApplyFilters` clears+refills on every keystroke (acceptable at small scale).
- **Dispatcher:** dashboard metrics deliberately run sequentially (correctly avoiding SQLite lock); many VM updates are already marshalled to the UI thread via `Dispatcher.InvokeAsync` in event handlers — good.
- **Fire-and-forget:** several `_ = Task.Run(...)` / `BeginInvoke` fire-and-forget without awaiting or logging failures (see §J).

---

## H. DATABASE AUDIT

### Architecture
**10 separate SQLite files**, one DbContext each, `EnsureCreated` (no migrations except audit.db):
`proglass.db, joborders.db, deliverynotes.db, taxinvoices.db, proformainvoices.db, specifications.db, audit.db, masterdata.db, statements.db, aimemory.db`

DbContexts: `AppDbContext`, `JobOrdersDbContext`, `DeliveryNotesDbContext`, `TaxInvoiceDbContext`, `ProformaInvoicesDbContext`, `SpecificationsDbContext`, `AuditDbContext`, `MasterDataDbContext`, `StatementDbContext`, `AiMemoryDbContext`.

### Findings
- **No cross-DB foreign keys** (by design — modular isolation). Consequence: no cascade delete, orphan records possible. (P1)
- **Migration strategy inconsistent (P0):** `audit.db` uses a hand-written `AuditDbMigrations.cs`; the other 9 use `EnsureCreated()`. `EnsureCreated` cannot alter an existing DB schema. Any added column/table after a user has run the app will not appear on their install. This is the highest-impact database risk.
- **Unique constraints / indexes:** `GlassSheet` has a unique index on (Category, ThicknessMm, Color, WidthMm, HeightMm, Supplier). Money columns use TEXT affinity (D-6 decimal precision) — good. SheetHistory has indexes.
- **Decimal affinity (good):** `TEXT` for decimal columns avoids SQLite REAL float loss.
- **Transactions:** per-op `SaveChanges`; **no multi-DB transaction** (see cross-DB TI/DN gap, P0).
- **Connection handling (good):** one `using new DbContext()` per operation; sequential dashboard queries avoid lock contention.
- **Concurrency:** no optimistic-concurrency tokens (`RowVersion`) anywhere — last-write-wins (P2).
- **Backup/restore:** `BackupService` — full-set snapshot, retention, pre-restore snapshot, restore logging, legacy 7-backup support. Solid. Restore copies files over live DBs (caller confirms).
- **Corruption handling:** `EnsureCreated` failure is logged but non-fatal (app runs degraded). No DB-integrity check (`PRAGMA integrity_check`) on startup — UNKNOWN if any exists elsewhere.

---

## I. PERFORMANCE AUDIT

- **Load-all + in-memory filter (P2):** Sheet Store, and list VMs fetch `GetAllAsync()` then filter/sort in C# (31 `GetAllAsync` callers). Fine for dozens–hundreds; degrades at 1k+ rows.
- **Dashboard analytics (good):** bounded `Take(limit)`, projected, `AsNoTracking`, sequential to avoid SQLite locks.
- **N+1 risk (P1):** `SavedInvoicesListViewModel.CompleteApprovalAsync` and status flows do per-invoice `GetById` after a list load — at list scale these are per-row round-trips. 
- **Big-view rendering (P2):** `DataGridView.xaml` 1708 lines with heavy per-row bindings; virtualized ListBoxes exist but nested converters/bindings add cost.
- **Screenshot/PDF/Excel (P2):** SkiaSharp stitching + QuestPDF/ClosedXML — inherently heavy; run on UI thread in places (fire-and-forget `Dispatcher.BeginInvoke`) → UI freeze risk on large exports. **UNKNOWN** whether these block the UI thread at scale.
- **Startup:** 10 DBs `EnsureCreated` + parallel repo init + AI provider + speech services — measured on Windows required (UNKNOWN latency).

---

## J. ERROR / CRASH AUDIT

- **Global handlers present (good):** `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`.
- **88 silent `catch {}` / `catch { /* comment */ }`** — many are "best-effort" (audit/history) which is defensible; several (e.g. `AiContextBuilder` multiple `catch {}`) hide real DB/parse failures with no log. (P2)
- **337 `catch (Exception)`** — a large share log + MessageBox; some re-swallow after `Debug.WriteLine` (invisible in Release).
- **305 `Debug.WriteLine`** — all vanish in Release; `DatabaseInitializer` explicitly documents one such past failure (R-1). Several "never block X" paths rely on `Debug.WriteLine` only. (P2)
- **Null-cast risks (P2):** many `as` conversions then `== null` guards (good), but a handful of `(Style)FindResource(...)` in code-behind without fallback will throw if a resource key is missing (seen in the SheetStore `ActionsMenu` fix).
- **Fire-and-forget (P2):** `_ = Task.Run(...)`, `Dispatcher.BeginInvoke(async ...)` without `try/catch` in several places — failures become unobserved tasks (surfaced via `UnobservedTaskException` but not actionable).
- **Async command errors:** `RelayCommand(async ...)` with internal try/catch is common — good; a few are unguarded.

---

## K. SECURITY / PRIVACY AUDIT

- **API keys (good):** DPAPI `SecretVault` (CurrentUser + entropy), `ConfigService.Save` masks key properties, `RememberApiKeys=false` clears the vault. Keys never in config.json plaintext.
- **Passwords (good):** PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte random salt; transparent migration from legacy SHA-256.
- **Default credential (P2):** `admin/admin` seeded; the login UI shows a first-run hint. If the user never changes it, the local "gate" is symbolic. Documented as non-security-boundary.
- **Login lockout (P3):** in-memory only, resets on restart. Documented trade-off for offline local app.
- **Authorization (P1):** `OperationGuard.Demand` (9) + `PermissionService.Require` (11) gate only a minority of mutating operations. The RBAC model (roles) exists but is enforced sparsely; most delete/edit/status flows call the repo without a permission check at the VM layer. The repo-level `OperationGuard` is the real boundary for the operations that use it.
- **Sensitive data in logs:** audit trail logs usernames and entity refs (by design). No API keys observed in logs. 
- **Path traversal (P2):** import/open/attachment paths come from dialogs; attachment filenames are sanitized (`SanitizeName`, `ResolveUniquePath`). `StatementPdfExporter`, `SheetStorePdfService` write to user-chosen paths (safe). **UNKNOWN** whether any user-controlled path reaches `Path.Combine` unsanitized in restore (restore copies files by name into `%AppData%`).
- **Serialization:** JSON snapshots use `System.Text.Json` with fixed options; no binary deserialization. Good.

---

## L. DATA VALIDATION AUDIT

- **Sheet Store:** import validates type + size limit; duplicate detection keyed on 6 fields; negative/zero guarded for buy/use (`quantity <= 0` early-return); use stock guards `Balance < quantity`.
- **Proforma wizard:** many fields validated, but **UNKNOWN** whether dimensions (W/H) reject zero/negative consistently at every step; `SqmCalculator` returns 0 for invalid but UI may still allow saving.
- **Numeric/decimal (P1):** SQLite TEXT affinity avoids float loss, but **input-side** decimal parsing/rounding is inconsistent (see §M).
- **Duplicate IDs:** `SuggestNextIdAsync` prevents ID collisions for PI/JO/DN/TI; `ExistsByInvoiceNumberAsync` guards TI. Good.
- **Date validation:** `IssueDate/ValidUntil` set; **UNKNOWN** if a future/dodgy date is rejected at input.
- **Status validation (P1):** arbitrary status strings accepted via menus (no enum/state-machine guard).

---

## M. BUSINESS LOGIC / CALCULATION AUDIT

- **SQM duplication (P1):** panel SQM computed inline in ≥5 places with **different rounding**:
  - `Utilities/SqmCalculator.cs` (shared) — `CalculatePanel`.
  - `DataGridViewModel` line 692/698: `(W*H)/1_000_000m :0.####`.
  - `PrintableInvoiceViewModel` line 968: `Math.Round(..., 3)`.
  - `ProfitLossStepViewModel` line 1714/1717: `.../1_000_000m` (no explicit rounding).
  - `SheetStorePricingService` line 181: `Math.Round(..., 4)`.
  - `GlassSheet.SqmPerSheet` and `DashboardMetricsService` (nested) both duplicate `(W*H)/1_000_000m`.
  → Same panel can show different SQM values in different screens. **Centralize with one rounding policy.**
- **Profit:** `GlassSheet.ProfitValue = TotalSellValue - TotalPurchaseValue`; `ProfitLossStepViewModel` computes P&L independently. **UNKNOWN** whether the two reconcile; both read from the same inputs but the profit-loss engine may use different cost basis (purchase vs selling — a `CostAtSellingPrice` flag exists).
- **Rounding (P1):** no app-wide rounding policy; `N0/N2/N4` format strings and `Math.Round(...,3/4)` mix freely.
- **Tax (OK):** 5% VAT centralized in snapshot (`VatPercent`), zero-rated export support noted in About text; **UNKNOWN** whether VAT recomputation is consistent across PI/statement.
- **Delivery/balance (OK):** ordered/delivered/balance tracked in DN; master-DN summary aggregates split DNs.

---

## N. CODE DUPLICATION AUDIT

| Item | Verdict |
|---|---|
| Panel SQM calc (5 sites) | **CENTRALIZE** (P1) |
| `GlassSheet.SqmPerSheet` vs `DashboardMetricsService` nested | **CENTRALIZE** |
| Per-section style dictionaries (DN/TI/JO/Settings/Wizard) | **REFACTOR** to consume global via `BasedOn` |
| Dialog chrome (Rename/Versions/Approval/Upload) | **CENTRALIZE** a `DialogStyles.xaml` |
| `catch { Debug.WriteLine }` pattern | **CENTRALIZE** logging helper |
| Context-menu construction repeated in 4 list code-behinds | **CENTRALIZE** a shared menu builder (styles already shared) |
| Statement VMs (`StatementGeneratorViewModel` + `StatementGeneratorViewModel2` = separate `StatementPreviewViewModel`, not a true dup) | **KEEP** (distinct concerns) |
| `IsDescendantOf`/visual-tree helpers | **CENTRALIZE** (duplicated in MainWindow + SheetStoreView) |

---

## O. FEATURE COMPLETENESS AUDIT

| Feature | Status |
|---|---|
| Proforma Invoice (wizard, snapshots, versions, approvals) | COMPLETE |
| Job Order (list/detail, auto-convert) | COMPLETE |
| Delivery Note (master/split, history, attachments) | COMPLETE |
| Tax Invoice (family, from PI/DN, invoiced flags) | COMPLETE |
| Statement / Sales Report + reconciliation | COMPLETE (shell + generator) |
| Dashboard (7) | COMPLETE |
| Sheet Store | COMPLETE |
| Settings (7 pages), theme, accent, wallpapers | COMPLETE |
| System Center / Users / Permissions | COMPLETE (permissions partially enforced) |
| Diagnostics / Logs | COMPLETE |
| AI Assistant (Gemini/Groq/Cohere), voice, agent | COMPLETE/PARTIAL |
| **Optimization** | **PLACEHOLDER** (ComingSoon) |
| **Production (8 sub-modules)** | **PLACEHOLDER** (ComingSoon) |
| Backup/Restore | COMPLETE |
| Screenshot system | COMPLETE |

Buttons/commands without real behaviour: the Production/Optimization sidebar entries and any `ComingSoon` routes. All other major buttons traced to commands.

---

## P. RELEASE READINESS AUDIT

**Verdict: NOT PRODUCTION READY** for an end-to-end factory deployment. **BETA READY** for the quote→invoice vertical.

| Criterion | Rating | Evidence |
|---|---|---|
| Stability | MEDIUM | no runtime run possible here; global crash handlers good |
| Data safety | MEDIUM | cross-DB gap (P0), no migrations (P0) |
| Database integrity | MEDIUM | no FK, EnsureCreated, no integrity check |
| UI consistency | MEDIUM | design system exists but ~30 views bypass it |
| UX quality | MEDIUM | placeholders shown as live; some crowding |
| Performance | MEDIUM | load-all filtering, heavy views |
| Error handling | MEDIUM | good global handlers, many silent catches |
| Security | MEDIUM-HIGH | good creds; sparse authorization |
| Privacy | HIGH | keys vaulted, local-only |
| Backup/recovery | HIGH | solid BackupService |
| Upgrade/migration | **LOW** | 9/10 DBs EnsureCreated only |
| Logging | MEDIUM | heavy Debug.WriteLine reliance |
| Crash handling | HIGH | three global handlers + shutdown guard |
| Workflow correctness | MEDIUM | cross-DB TI/DN, arbitrary status transitions |
| Maintainability | LOW-MEDIUM | god VMs, code-behind logic, local styles |
| Code quality | MEDIUM | clear naming + comments; some drift |

---

## EVERY-ISSUE SECTION (selected, highest-impact)

### ID-001
- **Severity:** CRITICAL · **Priority:** P0
- **Category:** Database / Upgrade
- **Location:** `Services/*/Database/*DbContext.cs` (9 files); `App.xaml.cs` `EnsureCreated` calls
- **Current behavior:** `EnsureCreated()` on 9 of 10 databases; only `audit.db` uses `AuditDbMigrations.cs`.
- **Why it is a problem:** `EnsureCreated` cannot alter an existing DB. Any schema change after a user has launched once silently won't apply on that user's machine.
- **Potential consequence:** Shipped users stuck on old schema; new fields/tables missing; possible runtime exceptions on queries referencing missing columns.
- **Evidence:** `DatabaseInitializer.cs`, each `*DbContext.OnConfiguring`, `App.xaml.cs` repo `InitializeAsync()`.
- **Recommended solution:** Adopt a migration tool (EF Migrations with a version table, or a hand-rolled per-DB `PRAGMA user_version` upgrade script like `audit.db`'s).
- **Dependencies:** all modules.

### ID-002
- **Severity:** CRITICAL · **Priority:** P0
- **Category:** Workflow / Data integrity
- **Location:** `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.SaveAsync` (≈line 1423–1460)
- **Current behavior:** TI saved to `taxinvoices.db`; then Delivery Notes marked invoiced in `deliverynotes.db` — no transaction spans the two DBs.
- **Why it is a problem:** If the DN-marking write fails, a TI exists whose source DNs are still "not invoiced".
- **Potential consequence:** Double-invoicing or reconciliation mismatches.
- **Evidence:** `await App.TaxInvoices.InsertAsync(entity)` then `MarkMultipleAsInvoicedAsync`.
- **Recommended solution:** Compensation/retry in a shared service; or a reconciliation sweep on startup that re-marks DNs whose TI exists (idempotent).
- **Dependencies:** TaxInvoice, DeliveryNote.

### ID-003
- **Severity:** HIGH · **Priority:** P1
- **Category:** Authorization
- **Location:** `Services/Audit/OperationGuard.cs`, `Services/Audit/Permissions.cs`; only 9 `OperationGuard.Demand` + 11 `PermissionService.Require` call sites.
- **Current behavior:** RBAC roles exist but most delete/edit/status/export operations do not check a permission.
- **Why it is a problem:** The permission model implies access control that is not actually enforced uniformly.
- **Potential consequence:** Any user can perform operations the UI implies are role-gated.
- **Recommended solution:** Add the permission check at the repository/service boundary for every mutating operation (the repo `OperationGuard` is the right chokepoint).

### ID-004
- **Severity:** HIGH · **Priority:** P1
- **Category:** Calculation consistency
- **Location:** `Utilities/SqmCalculator.cs` vs `DataGridViewModel` (692,698), `PrintableInvoiceViewModel` (968), `ProfitLossStepViewModel` (1714,1717), `SheetStorePricingService` (181), `GlassSheet.cs` (62)
- **Current behavior:** Panel SQM computed inline in ≥5 places with `Math.Round(...,3)`, `...,4`, and no rounding.
- **Why it is a problem:** The same panel can display different SQM (and hence value/profit) across screens.
- **Potential consequence:** Quote vs invoice vs profitability mismatch.
- **Recommended solution:** Single `SqmCalculator` API with one rounding policy; replace all inline computations.
- **Dependencies:** PI wizard, Profit & Loss, Sheet Store pricing, dashboards.

### ID-005
- **Severity:** HIGH · **Priority:** P1
- **Category:** Architecture / Maintainability
- **Location:** `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs` (2225), `DeliveryNoteDetailViewModel.cs` (2134), `TaxInvoiceDetailViewModel.cs` (2119), `StatementGeneratorViewModel.cs` (2074), `ProfitLossStepViewModel.cs` (1924), `SettingsViewModel.cs` (1795)
- **Current behavior:** God ViewModels combining state, validation, calculations, and persistence orchestration.
- **Why it is a problem:** Hard to test, merge, and reason about; high regression risk.
- **Potential consequence:** Every change risks breaking unrelated features.
- **Recommended solution:** Extract calculation services and sub-VMs (partial classes as an interim step).

### ID-006
- **Severity:** HIGH · **Priority:** P1
- **Category:** Feature completeness / UX honesty
- **Location:** `ViewModels/Production/*.cs` (8), `ViewModels/Optimization/OptimizationViewModel.cs`; `MainWindow.xaml` sidebar bindings
- **Current behavior:** 9 sidebar navigation targets render `ComingSoonView` as if they were active modules.
- **Why it is a problem:** Misleading; users click a real-looking "Cutting"/"Optimization" and get a stub.
- **Potential consequence:** Trust/expectation gap; blocks end-to-end factory rollout.
- **Recommended solution:** Either implement the modules or visibly mark them "Coming soon" in the sidebar.

### ID-007
- **Severity:** MEDIUM · **Priority:** P2
- **Category:** Error handling / Observability
- **Location:** 88 `catch {}` sites; 305 `Debug.WriteLine` sites
- **Current behavior:** Exceptions swallowed or logged only via `Debug.WriteLine` (removed in Release).
- **Why it is a problem:** Release-build failures are invisible and unrecoverable.
- **Potential consequence:** Silent data/feature failures that support cannot diagnose.
- **Recommended solution:** Replace `Debug.WriteLine` in non-debug paths with `AppLogService`; make intentional best-effort catches explicit + logged at debug level.

### ID-008
- **Severity:** MEDIUM · **Priority:** P2
- **Category:** UI consistency
- **Location:** ~31 views define local `Style x:Key` (e.g. `DnSectionStyles.xaml` 38, `SettingsSectionStyles.xaml` 28, `AboutView.xaml` 12)
- **Current behavior:** Local styles diverge from the global design system (button shapes, radii, paddings).
- **Why it is a problem:** Inconsistent look and duplicated design decisions.
- **Potential consequence:** App feels unpolished; theme changes don't propagate.
- **Recommended solution:** Refactor local dictionaries to `BasedOn` global keys; add shared `DialogStyles.xaml`/`InputStyles.xaml`.

### ID-009
- **Severity:** MEDIUM · **Priority:** P2
- **Category:** Performance
- **Location:** `SheetStoreViewModel`, `SavedInvoicesListViewModel`, `SavedJobOrdersListViewModel`, `DeliveryNoteListViewModel` — load-all + in-memory filter
- **Current behavior:** `GetAllAsync()` then filter/sort in C#.
- **Why it is a problem:** O(n) memory and CPU per keystroke; degrades at scale.
- **Potential consequence:** UI jank with large datasets.
- **Recommended solution:** Push filtering/sorting into SQL (IQueryable) with `.Take()` for paging; or debounce.

### ID-010
- **Severity:** MEDIUM · **Priority:** P2
- **Category:** Security
- **Location:** `Views/Login/LoginWindow.xaml.cs`; `Services/Audit/AuditService.cs` (DefaultAdminPassword)
- **Current behavior:** Seed `admin/admin`; lockout in-memory only; first-run hint surfaces the password.
- **Why it is a problem:** If never changed, the local gate is symbolic.
- **Potential consequence:** Anyone with the machine can sign in as admin.
- **Recommended solution:** Force a password change on first admin login (documented trade-off; at least warn periodically).

### ID-011
- **Severity:** MEDIUM · **Priority:** P2
- **Category:** WPF/MVVM
- **Location:** `ViewModels/*` constructor static-event subscriptions; `65 async void` handlers
- **Current behavior:** VMs subscribe to static events; some async void handlers lack internal try/catch.
- **Why it is a problem:** Potential leaks if VMs are recreated; unhandled async void exceptions.
- **Potential consequence:** Memory growth; silent crashes.
- **Recommended solution:** Unsubscribe in dispose; wrap async void in try/catch; prefer `async Task` with the shared `AsyncUtility.SafeFireAndForget`.

### ID-012
- **Severity:** MEDIUM · **Priority:** P2
- **Category:** Workflow / Data integrity
- **Location:** list views' "Change Status" submenus (Proforma, JobOrder, DeliveryNote, TaxInvoice)
- **Current behavior:** Arbitrary status strings selectable; no transition validation.
- **Why it is a problem:** A DN can go straight to "Received"; a PI can jump to "Approved" without the gate in some flows (approval gate exists only for the Approved→Step-8 path).
- **Potential consequence:** Invalid business states, misleading reports.
- **Recommended solution:** A per-module status state machine.

### ID-013
- **Severity:** LOW · **Priority:** P2
- **Category:** MVVM / code-behind
- **Location:** `JobOrderProformaPickerDialog.xaml.cs` (538), `SplashView.xaml.cs` (530), `DeliveryNoteListView.xaml.cs` (361)
- **Current behavior:** Substantial business/navigation logic in code-behind.
- **Why it is a problem:** MVVM contract broken; logic not unit-testable.
- **Potential consequence:** Higher regression risk.
- **Recommended solution:** Move to VM/services.

---

## FINAL PRIORITY MATRIX

### P0 — MUST FIX BEFORE RELEASE
1. Migration/upgrade strategy for the 9 `EnsureCreated` databases (ID-001).
2. Cross-DB Tax Invoice → DN invoiced compensation/transaction (ID-002).
3. (If Production is to be sold as ready) implement or gate the Production/Optimization placeholders.

### P1 — SHOULD FIX BEFORE RELEASE
4. Enforce RBAC at the repository boundary for all mutations (ID-003).
5. Centralize SQM/value/profit calculations with one rounding policy (ID-004).
6. Refactor god ViewModels (ID-005).
7. Mark/implement placeholder modules honestly (ID-006).
8. Reconcile PI/statement/TI statuses and add state-machine validation (ID-012).
9. Orphan prevention / cross-module delete guards.

### P2 — IMPORTANT IMPROVEMENTS
10. Replace `Debug.WriteLine`/silent catches with app logging (ID-007).
11. Converge ~30 views on the global design system; add shared dialog/input styles (ID-008).
12. Push filtering/sorting into SQL with paging (ID-009).
13. Force first-admin password change (ID-010).
14. Fix static-event leak potential + async void safety (ID-011).
15. Reduce code-behind business logic (ID-013).

### P3 — POLISH
16. Standardize dialog chrome; unify spacing/padding/corner radius; smooth the remaining hand-built popups/sliders; visual pass across all themes on Windows.

---

**Verification limit (honest):** This audit was performed statically — no .NET SDK/Windows runtime was available, so all runtime-rendering, DPI, layout-at-resolution, and visual-contrast conclusions are **UNKNOWN** and require the on-Windows visual pass the README already lists as outstanding. Every non-runtime claim above is backed by the cited source.
