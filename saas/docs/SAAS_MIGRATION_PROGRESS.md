# SAAS MIGRATION PROGRESS

> **Document status:** Living document (STEP 8). Read this FIRST at the start of every
> continuation session, then `SAAS_ARCHITECTURE.md`, then git state, then continue from the
> last verified state. Do not restart analysis unnecessarily (master prompt §60).

**Last updated:** 2026-09-27 (Phase 4)
**Current phase:** 🔄 Phase 4 (Authentication) — **code complete, syntax-checked 52/52, DB layer re-verified 31/31 vs live PG16** · register/login/refresh/logout/sessions/change-password + permission authorization + tenant-context middleware + unit/integration tests authored. **Compile + `dotnet test` still pending a .NET 8 SDK environment** (unchanged devbox constraint).
**Next:** First task in a .NET 8 SDK env: `dotnet build saas/backend/ProGlass.Saas.sln` → fix any compile errors → `dotnet test` (unit tests must pass; DB tests need SAAS_TEST_DB/SAAS_TEST_APP_DB) → flip `TreatWarningsAsErrors` on → then Phase 5 (Core ERP).

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
| 4 | Authentication | 🔄 CODE COMPLETE (compile pending) | Application layer (AuthService + ports + WPF-verbatim PasswordPolicy), Infrastructure (Argon2id, JWT, stores, platform-connection provisioner, audit writer), API (auth controller, JwtBearer, permission policies, tenant-context middleware), migration 0006 (absolute refresh deadline), 46 unit tests + 7 integration tests authored. DB checks 31/31 PASS. **Gate:** first `dotnet build`/`dotnet test` in .NET 8 env |
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
26. EF entities + configurations are GENERATED from the DDL (ADR-010) — 52/52 C# files pass tree-sitter syntax check (count grew with Phase 4)

### Phase 4 implementation facts (what exists now)

27. Lockout is WPF-verbatim (read from `Services/Audit/AuditService.Part1.cs` L605–633): 5 attempts → 5-minute window, PERSISTED (`failed_attempt_count`, `locked_until_utc`), the counter RESETS to 0 when the window opens, success clears everything — policy lives in `AuthService` (single tested place), the store is mechanical
28. Login failures are GENERIC ("Company code, username or password is incorrect.") — deliberate difference from WPF, which named the field (documented in WPF_TO_SAAS_MAPPING); unknown tenant/user paths burn a dummy Argon2 verify so response time does not reveal which part was wrong
29. Refresh tokens: `v1.{tenantId:N}.{base64url secret}` — tenant id IN the token because `refresh_tokens` is RLS-scoped (row must be found before any tenant context exists); only SHA-256 hex of the secret is stored; rotation on every refresh (`replaced_by_token_hash` links successor); reuse of a revoked token revokes the user's ENTIRE token family + audit `auth.refresh_reuse_detected`
30. Sessions have a 30-day SLIDING window capped by a 60-day ABSOLUTE deadline — `absolute_expires_at_utc` (migration 0006) is inherited UNCHANGED by every rotation; a per-token created_at check can never enforce this (sliding always expires first) — the original dead-code approach was caught and replaced, with a regression test
31. Tenant provisioning uses a separate ELEVATED connection (`SAAS_PLATFORM_DB`): the runtime role cannot INSERT `saas.tenants` by design; the provisioner runs tenant+user INSERTs in ONE transaction with `set_config('app.tenant_id', …, true)` (works under FORCE RLS as owner AND as superuser)
32. First user of a new tenant is `Administrator` (WPF 9-role catalog has no "owner"/"TenantAdmin" role — the users CHECK constraint enforces the WPF vocabulary)
33. `must_reset_password=true` marks WPF-migrated users (SHA-256 hashes not portable, ADR-006) — login still succeeds, the flag travels in LoginResult
34. Permission authorization: `[HasPermission("X.Y")]` — the policy NAME is the permission; `PermissionPolicyProvider` materializes policies on demand from the AppPermissions catalog (reflection, no duplicate list), `PermissionAuthorizationHandler` grants via `RolePermissions.Has(role, permission)` (WPF grant map)
35. `TenantContextMiddleware` is the ONLY writer of the request tenant context, from validated-JWT claims (`tid`/`sub`), never client input (§24); authenticated-but-malformed claims are rejected 401 (fail closed)
36. Auth audit events without a tenant (unknown company code at login) go to the APPLICATION log — RLS makes `saas.audit_logs` unwritable without a tenant context; never silently dropped
37. DB harness extended to 31 checks (was 23): 0006 column NOT NULL + index used by lookups, duplicate username within tenant rejected, same username in another tenant allowed, refresh-token NOT NULL + RLS WITH CHECK — `verification/DATABASE_VERIFICATION.md` (the old PHASE3_VERIFICATION.md stays as the Phase 3 record)

### Environment constraint (documented per §46)

- This devbox: no .NET SDK, no NuGet egress (allowlisted: GitHub/PyPI/npm only) → **the .NET solution has not been compiled here**. It is syntax-checked (52/52 files, `saas/devbox/syntax_check_cs.py`) and maps to a schema verified live (31/31 checks, `saas/database/verify/apply_and_test.py`). First `dotnet build`/`dotnet test` in a .NET environment is the Phase 4 exit gate; flip `TreatWarningsAsErrors` on in `saas/backend/Directory.Build.props` after that clean build.
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
| 2026-09-27 | 2 | Phase 3: 5 SQL migrations (41 tables), RLS tenant isolation, migration runner, verification harness **23/23 PASS vs live PG16**; .NET solution scaffold (Domain/Infrastructure/Api + 2 test projects), EF model generated from DDL, WPF roles/permissions + MoneyRounding ported with parity tests; ADR-008/009/010 | `b4a7e9d` |
| 2026-09-27 | 3 | Phase 4 (Authentication) authored: `ProGlass.Saas.Application` (AuthService, ports, WPF-verbatim PasswordPolicy), Infrastructure (Argon2id hasher, JWT service, tenant-explicit stores, platform-connection TenantProvisioner, AuditWriter, DI), API (auth endpoints `/api/v1/auth/*`, JwtBearer, permission policy provider/handler, TenantContextMiddleware), migration 0006 + generator ALTER-TABLE support, harness → **31/31 PASS**, 52/52 syntax, ~46 unit + 7 integration tests, ADR-011, docs | (this commit) |
