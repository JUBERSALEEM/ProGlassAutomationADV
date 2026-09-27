# SAAS MIGRATION PROGRESS

> **Document status:** Living document (STEP 8). Read this FIRST at the start of every
> continuation session, then `SAAS_ARCHITECTURE.md`, then git state, then continue from the
> last verified state. Do not restart analysis unnecessarily (master prompt §60).

**Last updated:** 2026-09-27 (Phase 3)
**Current phase:** ✅ Phase 3 complete — **database schema + tenant isolation VERIFIED against live PostgreSQL 16 (23/23 checks)** · .NET backend authored (syntax-checked; compile pending .NET SDK env)
**Next:** Phase 4 — Authentication (first task when a .NET 8 SDK environment is available: `dotnet build saas/backend/ProGlass.Saas.sln` and `dotnet test`, then flip `TreatWarningsAsErrors` on)

---

## Phase tracker

| # | Phase (master prompt §37) | Status | Notes |
|---|---|---|---|
| 1 | WPF audit | ✅ DONE | `WPF_SYSTEM_AUDIT.md` — modules, 16 DBs, formulas, engine, roles, risks |
| 2 | SaaS architecture | ✅ DONE | `SAAS_ARCHITECTURE.md` (stack decision documented) |
| — | Feature parity matrix | ✅ DONE | `WPF_TO_SAAS_MAPPING.md` |
| — | Database design | ✅ DONE | `SAAS_DATABASE_DESIGN.md` |
| — | API design | ✅ DONE | `SAAS_API_DESIGN.md` |
| — | Migration plan | ✅ DONE | `SAAS_MIGRATION_PLAN.md` |
| 3 | Database implementation | ✅ DONE (DB layer verified; .NET compile pending) | 5 SQL migrations, 41 tables, RLS on 36 tenant tables, `SqlMigrationRunner`, EF model generated from DDL, xUnit suites. **Gate:** `saas/database/verify/apply_and_test.py` = 23/23 PASS |
| 4 | Authentication | ⬜ NOT STARTED | Needs .NET SDK env (first `dotnet build` gate) |
| 5 | Core ERP (master data, settings) | ⬜ NOT STARTED | |
| 6 | Calculators (SGU/DGU/LAM/DGU+LAM/wastage) | ⬜ NOT STARTED | Port formulas + golden tests |
| 7 | Quotation / Proforma | ⬜ NOT STARTED | |
| 8 | Stock (Sheet Store) | ⬜ NOT STARTED | |
| 9 | Jobs / Daily Works / Production | ⬜ NOT STARTED | |
| 10 | Delivery | ⬜ NOT STARTED | |
| 11 | Optimization port | ⬜ NOT STARTED | Copy `Services/Optimization` + oracle tests as-is; background jobs |
| 12 | Reporting / VAT | ⬜ NOT STARTED | |
| 13 | Subscriptions | ⬜ NOT STARTED | |
| 14 | Migration tooling | ⬜ NOT STARTED | `/saas/migration` |
| 15 | Production hardening | ⬜ NOT STARTED | |

---

## Verified WPF behavior log (decisions already grounded in source)

Add entries here as behavior is discovered. Format: **fact → source file**.

1. SGU/DGU/LAM/DGU+LAM rate formulas + cost bases → `PricingEstimatorViewModel.Part2.cs`, `DguLamParametersViewModel.CalculateRate`
2. Money: 2 dp, AwayFromZero, AED books → `Services/Reporting/MoneyRounding.cs`
3. VAT 5% incl. back-calculation + 1-fil tolerance → `Services/Reporting/VatCalculator.cs`
4. SQM: panel 3 dp + 0.5 m² floor; sheet 4 dp no floor → `Utilities/SqmCalculator.cs`
5. PI numbering `PI-{year}-NNN`, collision retry ×5, GUID fallback → `ProformaInvoiceRepository.SuggestNextIdAsync/InsertAsync`
6. PI lifecycle state machine + status vocabularies (pass-through unknown) → `InvoiceLifecycleStateMachine.cs`, `docs/WORKFLOW.md`
7. Roles/permissions catalog + OperationGuard enforcement → `Services/Audit/Permissions.cs`
8. Margin house table (Thin 15/15/15/15 kerf3 breakout15 · Medium 30s kerf3 · Thick 50/50/0/50 kerf4 breakout 50|40) → `OptimizationModels.cs MarginProfile`
9. Optimizer enums, oracles (Verify geometry / ValidateResult conservation), calibration benchmark (Low-E 17 @ 82.77%, Clear 16 @ 84.92%, Ramly 1 × 2500×1875) → `GuillotinePacker.cs`, `OptimizationModels.cs`
10. Laminate rule: Z/W trims never cut into laminated stock → `CutPolicy.ResolveFor`
11. Stock movement ledger requirement → `Models/SheetStore/`, `SheetHistory`
12. Fiscal delete restricted to Manager/Admin; void-not-delete → `Permissions.cs` (TaxInvoice.Delete), `SavedProformaInvoice.IsVoid*`
13. 16 SQLite DBs + document snapshot pattern (metadata + FullSnapshotJson) → all `*DbContext.cs`
14. Users: SHA-256(salt+pwd), lockout persisted, never-delete → `Models/Audit/AppUser.cs`
15. Sheet Store colors are the only color source in pricing (no built-in palette) → `SguParametersViewModel` (2026-09-10 change)
16. StockSize sentinels: `0 = unlimited (explicit)`, `99999999 = default availability` → `OptimizationModels.cs`
17. Read-only gate on expired license blocks writes at service layer → `Services/Licensing/ReadOnlyGate.cs`

### Phase 3 implementation facts (what exists now)

18. Schema = 41 tables in 5 migrations (`saas/database/migrations/`), all CHECK/FK/unique constraints applied to live PG16; status vocabularies are CHECK-constrained with `status_text` overflow for WPF unknowns (pass-through contract preserved)
19. RLS policy per tenant table uses `NULLIF(current_setting('app.tenant_id', true), '')::uuid` — BOTH unset and reset-to-empty contexts fail closed (verified live; a plain `::uuid` cast would error on `''`)
20. `updated_at` maintained by DB trigger `saas.touch_updated_at()` — catches were the test caught missing triggers on 14 tables
21. Tenant-scoped uniqueness verified: same `proforma_no` allowed in two tenants, duplicate within one rejected (WPF PI numbering semantics)
22. Immutable ledgers enforced at GRANT level: `audit_logs`, `stock_transactions`, `production_events`, `optimization_placements/cuts/leftovers` are INSERT-only for the runtime role
23. Two DB roles: `saas_owner` (DDL/migrations) and `saas_app` (DML only) — the application never owns schema objects
24. Roles/permissions catalog ported grant-for-grant from WPF `Permissions.cs` and pinned by `RolePermissionsTests` (12 tests incl. the G-6 fiscal-delete rule)
25. `MoneyRounding` ported verbatim; parity tests distinguish half-away-from-zero from banker's rounding (the original WPF Phase-2 bug)
26. EF entities + configurations are GENERATED from the DDL (ADR-010) — 29/29 C# files pass tree-sitter syntax check

### Environment constraint (documented per §46)

- This devbox: no .NET SDK, no NuGet egress (allowlisted: GitHub/PyPI/npm only) → **the .NET solution has not been compiled here**. It is syntax-checked (29/29) and maps to a schema verified live (23/23). First `dotnet build`/`dotnet test` in a .NET environment is the Phase 4 entry gate.
- PostgreSQL 16 runs via PyPI `pgserver` (see `saas/devbox/README.md`).

---

## Architectural decisions (ADR log)

See `SAAS_ARCHITECTURE.md` §14 (ADR-001…007). New decisions get appended there and
referenced here.

---

## Known issues / open questions

| # | Issue | Impact | Status |
|---|---|---|---|
| 1 | WPF stores some timestamps as local `DateTime.Now` | Migration timezone conversion needs tenant TZ default (Asia/Dubai) | Rule defined in `SAAS_MIGRATION_PLAN.md` §3 |
| 2 | Password hashes not portable | Imported users must reset password | Accepted (ADR-006) |
| 3 | AI/agent/voice modules deferred | Not in initial SaaS scope | Documented in mapping §1.12 |
| 4 | Optimizer is ~11k LOC across partials | Port must be mechanical (file-by-file), not conceptual | Phase 11 plan |

---

## Definition of Done per module (from master prompt §62)

A module is complete only when: UI ✓ API ✓ DB ✓ validation ✓ authorization ✓ tenant
isolation ✓ business rules match WPF ✓ errors handled ✓ tests ✓ reports/IO ✓ audit ✓
docs updated ✓. Update the module row above only after all twelve pass.

## Session log

| Date | Session | Work done | Commit |
|---|---|---|---|
| 2026-09-27 | 1 | Phase 1 deep audit (read pricing, VAT, SQM, optimizer, roles, PI repo, stock, master data, lifecycle); wrote all 8 analysis documents in `/saas/docs` | `e5046ac` |
| 2026-09-27 | 2 | Phase 3: 5 SQL migrations (41 tables), RLS tenant isolation, migration runner, verification harness **23/23 PASS vs live PG16**; .NET solution scaffold (Domain/Infrastructure/Api + 2 test projects), EF model generated from DDL, WPF roles/permissions + MoneyRounding ported with parity tests; ADR-008/009/010 | (this commit) |
