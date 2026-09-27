# SAAS MIGRATION PROGRESS

> **Document status:** Living document (STEP 8). Read this FIRST at the start of every
> continuation session, then `SAAS_ARCHITECTURE.md`, then git state, then continue from the
> last verified state. Do not restart analysis unnecessarily (master prompt §60).

**Last updated:** 2026-09-27
**Current phase:** ✅ Phase 1 complete (WPF audit) · ✅ Phase 2 complete (architecture docs) ·
**Next:** Phase 3 — Database (scaffold SaaS solution + migrations)

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
| 3 | Database implementation | ⬜ NOT STARTED | Scaffold `/saas/backend`, EF migrations, tenant isolation tests |
| 4 | Authentication | ⬜ NOT STARTED | |
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
| 2026-09-27 | 1 | Phase 1 deep audit (read pricing, VAT, SQM, optimizer, roles, PI repo, stock, master data, lifecycle); wrote all 8 analysis documents in `/saas/docs` | (this commit) |
