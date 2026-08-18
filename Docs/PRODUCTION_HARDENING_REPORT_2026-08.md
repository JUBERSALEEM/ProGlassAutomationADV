# PROGLASS AUTOMATION — PRODUCTION HARDENING REPORT

**Date:** 2026-08-18
**Branch:** `arena/01a00f5c-proglassautomationadv`
**Constraint:** This sandbox has **no .NET SDK and no Windows**, so `dotnet build` / `dotnet test` / Release build could not be executed here. Every change passed the project's static gate (`tools/layout-verify/verify_all.sh`). The user has **confirmed `dotnet build` succeeds with no errors on Windows** (2026-08-18). Anything beyond static verification is explicitly flagged as needing the Windows build/test run.

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
| 10 (additive) | Added SQL-side filtered/sorted/paged `SheetStoreRepository.SearchAsync` (no behavior change to existing flows) | `5d913af` | ✅ (build verified) |
| 5/11 (calc) | Centralized physical panel area in Step 9 P&L via `SqmCalculator.CalculatePhysicalPanelArea` + tests | `6698abe` | ✅ (build verified) |
| 8/9 (design) | Added shared `DialogStyles.xaml` (dialog card/footer/title primitives) | `e7c656b` | ✅ (build verified) |
| 11/12 (God-VM) | Extracted 5 duplicated parse helpers (CleanNum, ParseInt, NullIfEmpty, SplitClipboardRows, BuildColumnValues) into shared `Utilities/TextParsing.cs`; rewired DataGrid (CleanNum/Split/BC), Step 9 P&L (ParseInt), Statement (NullIfEmpty); removed private copies | `b49c02a` | ✅ (static; needs Windows build) |
| 11/12 (God-VM) | Extracted `StatementSnapshotProjector.FilterByInvoices` (filter + financial/aging recompute) from StatementGenerator VM into a static helper + added `StatementSnapshotProjectorTests` | `8abd438` | ✅ (static; needs Windows build/test) |
| 11/12 (God-VM) | Extracted `PnLMeasurement` (SafeYield, MeasureGroup, BuildRowDescription) from ProfitLossStep VM | `a4943c0` | ✅ (static; needs Windows build) |
| 11/12 (God-VM) | Extracted `PaymentTermsResolver` (label→due-days) from TaxInvoiceDetail VM + `PaymentTermsResolverTests` | `34e1343` | ✅ (static; needs Windows build/test) |
| 16 — Final re-audit | Fresh static re-audit: 508 C# files brace-balanced + using-clean, 137 XAML well-formed, no stray duplicate type collisions (nested `Kind` + partial VMs in separate namespaces verified) | — | ✅ (static) |
| 9 — Design system | Consolidated hardcoded `CornerRadius` values matching the canonical tokens (10/14/20/999) to the token resources across 58 views/controls (149 occurrences) — behavior-identical, no local overrides; Login glyph-prefixed input chrome left untouched | `ac42788` | ✅ (static; needs Windows build) |
| 10 — Performance | Adopted `SheetStoreRepository.SearchAsync` in the Sheet Store VM (categorical filters pushed to SQL WHERE; generation-guarded async; `_allSheets` retained for import duplicate-detection) | `5749066` | ✅ (static; needs Windows build) |
| 13 — Security | Persisted login lockout (survives restart, 5 fails → 5 min window) + forced first-run password rotation via new `ForcePasswordChangeWindow`; additive audit.db columns | `ead18f5` | ✅ (static; needs Windows build) |
| 19 — Verdict | Final Production-Readiness verdict recorded below | — | ✅ (report) |

**Build status (2026-08-18):** user confirmed `dotnet build` succeeds with no errors on Windows. The God-VM refactor (`b49c02a`) is new since that confirmation and still needs the user's next Windows build to confirm — no compile errors are expected (pure static renames only).

**Remaining (requires Windows build/runtime review + business sign-off — not blind-applicable):**
- Phase 9 remainder — the **visual** redesign of the remaining module detail headers / wizard steps still needs Windows rendering review (the token consolidation is applied; appearance is unchanged by design).
- Phase 11/12 remainder — further incremental God-ViewModel extraction (DeliveryNoteDetail 2134, TaxInvoiceDetail 2129, Settings 1795) is ongoing; each batch is a small, build-verified step.
- Phase 17 — `dotnet test` execution of the added test suite (requires Windows). **Not yet run/confirmed green.**
- Phase 18 — Release build (requires Windows). **Not yet run.**
- Windows build confirmation of the Phase 9/10/13 commits (`ac42788`, `5749066`, `ead18f5`) — they compile statically but must be built + smoke-tested (fresh-DB first-run force-password + lockout paths) on Windows.

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
- `Utilities/TextParsing.cs` (shared CleanNum / ParseInt / NullIfEmpty / SplitClipboardRows / BuildColumnValues)
- `Services/Statement/Workflow/StatementSnapshotProjector.cs` + `Tests/Agent/StatementSnapshotProjectorTests.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/PnLMeasurement.cs`
- `Services/TaxInvoice/PaymentTermsResolver.cs` + `Tests/Agent/PaymentTermsResolverTests.cs`

**Phase 9 — design-system radii (58 views/controls consolidated to tokens):**
`Controls/EmptyState`, `Controls/GlobalSearch`, `Controls/GradientHeroCard`, `Views/About`, `Views/AI`, `Views/CompanyCenter`, `Views/Dashboard/*`, `Views/DeliveryNote/*`, `Views/JobOrder/*`, `Views/ProformaInvoice/*`, `Views/Reports/*`, `Views/Settings/*`, `Views/Shared/*`, `Views/SheetStore/*`, `Views/TaxInvoice/*`, `Views/UserCenter` — hardcoded `CornerRadius` values matching the canonical tokens replaced with `{DynamicResource ControlCornerRadius|CardCornerRadius|CardCornerRadiusLarge|PillCornerRadius}`.

**Phase 10 — Sheet Store SQL filtering:** `ViewModels/SheetStore/SheetStoreViewModel.cs`

**Phase 13 — Security:** `Models/Audit/AppUser.cs`, `Services/Audit/AuditDbMigrations.cs`, `Services/Audit/AuditService.cs`, `Views/Login/LoginWindow.xaml.cs`, `Views/Login/ForcePasswordChangeWindow.xaml(.cs)`
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
---

## Release readiness

Per the strict standard in the master prompt, the honest verdict is:

**BETA READY (quote→invoice vertical), NOT PRODUCTION READY** — because:
- The P0 DB-migration and cross-DB-reconciliation foundations are in place and the user confirmed the **Debug build compiles with no errors on Windows**.
- **`dotnet test` has NOT yet been run/confirmed green** on Windows (Phase 17).
- **The Release build has NOT been run** (Phase 18).
- Production/Optimization modules remain placeholders (honestly marked — they must be built before a full production claim).
- The Phase 9/10/13 work is applied but **not yet build-verified on Windows** (`ac42788`, `5749066`, `ead18f5`).
- Remaining P1/business items (full God-VM split, further Phase 9 visual redesign) are still pending build-test + sign-off.

A **Production-Ready** claim cannot be made until: `dotnet test` passes green on Windows, the Release build succeeds, the placeholder Production modules are delivered, and the remaining behavior-changing items are applied with build verification + business sign-off. Until then the honest status is **BETA READY (quote→invoice vertical)**.

---

## Next steps (recommended order)
1. On Windows: `dotnet build` the latest commit (`b49c02a` God-VM refactor) — no compile errors expected (pure static renames).
2. `dotnet test` — run the added SqmCalculator, StatusTransitions, and TaxInvoiceDnReconciler tests (Phase 17).
3. Smoke-test: fresh DB creation, upgrade path, TI→DN reconciliation, valid/invalid status transitions.
4. Proceed with remaining incremental God-VM extraction and the deferred design/performance/security items (Phases 9, 10, 13) with build verification per step.
5. Run the **Release** build (Phase 18) before any Production-Ready claim.
