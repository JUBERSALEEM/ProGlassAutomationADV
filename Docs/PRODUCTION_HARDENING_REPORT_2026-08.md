# PROGLASS AUTOMATION — PRODUCTION HARDENING REPORT (INTERIM)

**Date:** 2026-08-18
**Branch:** `arena/01a00f5c-proglassautomationadv`
**Constraint:** This sandbox has **no .NET SDK and no Windows**, so `dotnet build` and `dotnet test` could not be run. Every phase passed the project's static gate (`tools/layout-verify/verify_all.sh`). Windows build/test is the required next step.

---

## Summary of what was applied (commits)

| Phase | Scope | Commit | Status |
|---|---|---|---|
| 0 — Baseline | Recorded branch/HEAD/clean-tree, remote sync | — | ✅ |
| 1 — Database migration/upgrade (P0) | Versioned `SchemaMigrationRunner` + `AppSchemaMigrations` wired into all 9 `EnsureCreated` DBs; backup-before-migrate; `PRAGMA user_version`; additive/idempotent | `9c14257` | ✅ (build pending) |
| 2 — Cross-DB integrity (P0) | `TaxInvoiceDnReconciler` full sweep; idempotent repair + verify; background startup sweep | `edafa75` | ✅ (build pending) |
| 3 — Workflow state machines (P1) | Centralized `StatusTransitions` table; enforced at 5 service/VM boundaries | `c421db5` | ✅ (build pending) |
| 4 — RBAC (P1) | **Verified existing**: 39+ `OperationGuard.Demand` at repo layer (fail-closed); `MarkAsInvoiced*` intentionally unguarded (system) | — | ✅ verified |
| 5 — Central calculations (P1) | `SqmCalculator.CalculateSheetArea`; deduped GlassSheet + DashboardMetrics + PrintableInvoice; unit tests | `4d617ee` | ✅ (tests pending) |
| 6 — Production/Optimization | Marked placeholders honestly (sidebar "Coming soon" tooltips); `ComingSoonView` already clear | `cb0c12d` | ✅ |
| 7 — Error/observability (P2) | Converted Release-invisible `Debug.WriteLine` failures to `AppLogService` (MasterData, Statement, AiMemory) | `c0c0612` | ✅ |
| 8 — Design system (P2) | Added shared `InputStyles.xaml` (TextBox/PasswordBox/ComboBox), registered globally | `b5c0302` | ✅ |
| 14 — Orphan protection | Added `DependencyGuard` (cross-DB delete guards for PI/JO/DN), wired into delete flows | `8216476` | ✅ (build pending) |
| 15 — Testing | Added `SqmCalculatorTests`, `StatusTransitionsTests`, `TaxInvoiceDnReconcilerTests` | `4d617ee`, `ee0bdf9`, `…` | ✅ (run pending) |
| 10 (additive) | Added SQL-side filtered/sorted/paged `SheetStoreRepository.SearchAsync` (no behavior change to existing flows) | `…` | ✅ (build verified) |

**Build status (2026-08-18):** user confirmed `dotnet build` succeeds with no errors on Windows.

---

## Files changed (hardening)

**New framework/registry**
- `Services/Database/SchemaMigrations/SchemaMigration.cs`
- `Services/Database/SchemaMigrations/SchemaMigrationSql.cs`
- `Services/Database/SchemaMigrations/SchemaMigrationRunner.cs`
- `Services/Database/SchemaMigrations/AppSchemaMigrations.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceDnReconciler.cs`
- `Services/Workflow/StatusTransitions.cs`
- `Themes/Styles/InputStyles.xaml`
- `Tests/Agent/SqmCalculatorTests.cs`
- `Tests/Agent/StatusTransitionsTests.cs`

**Wired/accessors**
- `App.xaml.cs` (reconciliation sweep)
- `Services/Database/DatabaseInitializer.cs`
- `Services/JobOrder/Repository/JobOrderRepository.cs`
- `Services/DeliveryNote/Repository/DeliveryNoteRepository.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs`
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsRepository.cs`
- `Services/MasterData/Repository/MasterDataRepository.cs`
- `Services/Statement/Repository/StatementRepository.cs`
- `Services/AI/AIMemory/AiMemoryService.cs`
- `Services/DeliveryNote/Database/DeliveryNotesDbContext.cs`
- `Services/MasterData/Database/MasterDataDbContext.cs`
- `Services/Statement/Database/StatementDbContext.cs`
- `Services/TaxInvoice/Database/TaxInvoiceDbContext.cs`
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsDbContext.cs`
- `Models/SheetStore/GlassSheet.cs`
- `Services/Dashboard/Core/DashboardMetricsService.cs`
- `Utilities/SqmCalculator.cs`
- `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.cs`
- `ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.cs`
- `ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.cs`
- `ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.cs`
- `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs`
- `Services/Statement/Aggregation/StatementAggregator.cs`
- `Services/Statement/Workflow/StatementWorkflowEngine.cs`
- `App.xaml`, `MainWindow.xaml`

---

## What was deliberately deferred — and why

The following phases/parts are **behavior-changing or UI-heavy** and must be **build-verified on Windows** and, for business-rule items, **signed off** before being applied. Pushing them blind would violate the project's golden rule and risk real data.

| Phase | Deferred item | Why |
|---|---|---|
| 9 — UI/UX pass | Restyle ~30 views to the design system; fix spacing/DPI | Requires runtime rendering (Windows) to verify; no build here. High regression risk. (Empty-state already shared across 7 list views.) |
| 10 — Performance | Push `GetAllAsync` filtering/sorting/paging into SQL | Changes query behavior across 31 callers; must be measured + tested on real DBs. |
| 11/12 — Architecture/code-behind | Split God ViewModels; move code-behind business logic | Large refactors with zero behavioral margin; must be build-verified incrementally. |
| 13 — Security | Force first-run password change; login lockout persistence | Breaks existing installs / changes auth UX; needs a migration strategy + sign-off (the DPAPI key storage is already sound). |
| 15 — Testing | Run the full suite | Requires `dotnet test` (Windows). Tests added; execution pending. |
| 16–20 — Final QA / re-audit / release verdict | Full second audit + Release build | Requires a working build + runtime. Cannot be honestly claimed from this sandbox. |

---

## Release readiness

Per the strict standard in the master prompt, the honest verdict is:

**BETA READY (quote→invoice vertical), NOT PRODUCTION READY** — because:
- The P0 DB-migration and cross-DB-reconciliation foundations are in place but **not yet build-verified on Windows**.
- Production/Optimization modules remain placeholders (honestly marked).
- Several P1 items (authorization completeness edge cases, status rules are now enforced but not runtime-tested, SQM centralization partial) still need build + test confirmation.

A Production-Ready claim cannot be made until `dotnet build` + `dotnet test` pass on Windows and the deferred phases are completed with verification.

---

## Next steps (recommended order)
1. On Windows: `dotnet build` — fix any compile issues from Phases 1–8.
2. `dotnet test` — run the added SqmCalculator + StatusTransitions tests.
3. Smoke-test: fresh DB creation, upgrade path, TI→DN reconciliation, valid/invalid status transitions.
4. Then proceed to Phases 9–14 (design consolidation, SQL filtering, architecture, security, orphan protection) with build verification per phase.
5. Final re-audit + Release build before any Production-Ready claim.
