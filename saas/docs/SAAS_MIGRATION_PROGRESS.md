# SAAS MIGRATION PROGRESS

> **Document status:** Living document (STEP 8). Read this FIRST at the start of every
> continuation session, then `SAAS_ARCHITECTURE.md`, then git state, then continue from the
> last verified state. Do not restart analysis unnecessarily (master prompt §60).

**Last updated:** 2026-09-27 (Phase 7b, specifications registry done)
**Current phase:** 🔄 Phase 7 (Quotation / Proforma) — **7a document core + 7b SPEC REGISTRY COMPLETE**: on top of 7a (numbering/lifecycle/void/revisions/Step 5 grid), the Step 4 APPLY library and the LEDGER-derived usage trail are live — migration 0008 (catalogue parity columns + natural key + specification_usages), `SpecificationsService` (preserve-on-blank upsert, counts never touched by APPLY), the set-based reconcile (purge/insert/heal in one transaction, runs after every grid save + on demand), `/api/v1/specifications` with the WPF permission map, spec-group → registry linkage (SpecId + 400 on unknown refs); 13 service unit tests + 5 integration tests for the ledger; harness **43/43**, syntax **100/100**. Compile-pending like everything else (**no .NET SDK in this sandbox — unchanged**).
**Next:** Phase 7b remainder — Step 6 other charges (incl. wastage rows), Step 9 P&L, Step 7 PDF/print, duplicate action + FullSnapshotJson round-trip, inquiry convert-to-PI. Standing compile gate unchanged: in a .NET 8 SDK env run `dotnet build saas/backend/ProGlass.Saas.sln` → `dotnet test` → flip `TreatWarningsAsErrors` on.

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
| 5 | Core ERP (master data, settings) | ✅ DONE (compile pending) | Companies/clients/projects/salesmen: WPF-verbatim upserts (natural keys, usage counting, preserve-on-blank, single default, rename-collision refusal), audit before/after payloads, `/api/v1/{companies,clients,projects,salesmen}` with the WPF permission map, migration 0007 (parity columns the Phase 3 schema missed), 25 unit + 5 integration tests. Settings/pricing config endpoints pair with Phase 6 (shared WPF sources). **Gate:** same standing compile gate |
| 6 | Calculators (SGU/DGU/LAM/DGU+LAM/wastage) | 🔄 CODE COMPLETE (compile pending) | RateCalculator (4 formulas + cost bases, BUG-40/41 warnings verbatim), SpacerPricing, WastageFormula, SqmCalculator, PricingDefaults (WPF catalogs/panel defaults) in Domain; PricingService + EF PricingConfigStore + `/api/v1/pricing/{config,preview}`; golden unit tests (SGU 108.24, DGU 119.40/103.82, LAM 228.65, DGU+LAM 344.75/287.29, wastage 100.84) + 5 integration tests. **Gate:** same standing compile gate |
| 7 | Quotation / Proforma | 🔄 7a + 7b-specs + 7b-step6 DONE | 7a: document core — numbering/lifecycle/void/revision/Step 5 grid (mapping §1.2a). 7b: specifications registry + ledger (§1.3a, migration 0008); Step 6 other charges/wastages/customs (§1.3b, migration 0009) with server-side derivation. Harness 51/51. Remaining: P&L, PDF, duplicate/snapshot, inquiry convert, automation suggestions. **Gate:** same standing compile gate |
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

### Phase 5 implementation facts (master data)

38. Master data semantics ported from `Services/MasterData/Repository/MasterDataRepository.cs`: natural-key upserts (company name+TRN, client name+TRN, project identifier+customer, salesman name — all case-insensitive), usage_count+1 + last_used_at on every re-save, insert with usage_count=1
39. WPF audit-fix behaviors preserved: F-1 id-first lookup for company edits (renames must update, not duplicate), F-1b rename-collision refusal, F-1c editor latest-data-wins (blank clears), F-2 invoice auto-save preserve-on-blank (never erases profile values, never touches IsDefault)
40. Migration 0007 restored three WPF-parity columns the Phase 3 schema missed (companies.logo_layout/width/height with defaults 22×26 mm + layout CHECK; clients.document_prepared_by) — found by porting against the WPF source; generator now parses inline `ADD COLUMN ... NOT NULL`
41. Projects reference clients by `customer_id` (schema normalization; WPF stored a customer NAME string) — the same-identifier-different-customer rule is preserved through the id; deleting a client SET NULLs the project reference (WPF masterdata.db rows never touched invoices)
42. API permission map is the WPF guard map verbatim: company/salesman save+delete and client/project delete = Settings.Manage; client/project save = ProformaInvoice.Edit; reads open to all authenticated users
43. Audit before/after payloads: new `IAuditWriter` overload writes `audit_logs.before_json/after_json` on every master-data write (the WPF Activity log's field-diff trail, machine-readable)
44. Salesman blank-department preserve quirk ported verbatim (blank never erases a stored department)
45. DB harness now 35/35 (4 new Phase 5 checks: 0007 columns + defaults fill + layout CHECK)
46. SaaS differences from WPF, all documented in WPF_TO_SAAS_MAPPING §1.10a: DELETE → 404 (was silent no-op), UTC (was local time), logo object-storage keys (was file paths), pagination on lists

### Phase 6 implementation facts (calculators + pricing settings)

47. All four rate formulas ported VERBATIM as pure functions over parameter records (`Domain/Pricing/RateCalculator.cs`): SGU `(base/yield+cutting+tempering)×(1+margin/100)`; DGU `((outer+inner)/yield+spacer)×…`; LAM `((outer+inner)/yield+PVB+cuttings+temperings)×…`; DGU+LAM `(glass/yield+totalCutting+totalTempering+interlayerSell+spacer)×…` — yield divides GLASS ONLY, the interlayer is INSIDE the margin (110×1.20=132), cutting/tempering on ALL THREE panes, spacer opt-in at 0. Every rate ends in `MoneyRounding.Round`; cost bases use SafeYield silently (no warning), same as WPF
48. Warning texts are byte-identical to WPF (BUG-40/41): `⚠  {SGU|DGU|LAM} yield factor must be > 0 (currently {0.###}). Using 1.0 as fallback.` / DGU+LAM `⚠  Yield factor must be greater than 0 (currently {0.###}). Using 1.0 instead.` / `⚠  No selling price set for the {InterlayerType} interlayer, so it is NOT included in this rate.` — returned in `POST /api/v1/pricing/preview` → `warnings[]`
49. Golden values pinned from WPF's own tests/comments and re-verified with Python `decimal` (no xUnit in sandbox): SGU defaults → 108.24 (basis 94.12); DGU worked example (25+25)/0.85+45 → basis 103.82, rate 119.40; LAM example → 228.65; DGU+LAM business chain → 344.75 (basis 287.29, yield-1 variant 324.00, spacer+45 → 398.75 = 344.75+45×1.2, interlayer delta 132.00); wastage 40/70%/10m² → 57.142857/47.058824/10.084033/100.84; spacer 12mm ASP+NMA → 45.00
50. Spacer price formula verbatim incl. case-SENSITIVE material switch (NMA 0, Black Alu 5, Warm Edge 15, Black Warm Edge 20, SS 10, TGI 12, unknown→0) and ParseWidthMm fallback 12 ("digits before mm" parse, not leading digits)
51. Wastage port: S1 = price/(opt%/100) and S2 = price/(85%/100) at 6dp, rate = S1−S2, amount = MoneyRounding(rate×sqm); manual mode = round(sqm×rate, 2) (WPF line 1005); `BaselineOptimizationPercent = 85` const (WPF L15); non-positive inputs zero the row
52. SqmCalculator verbatim: panel round3 + 0.5 m² floor, total = panel×qty at 3dp, sheet round4 NO floor; invalid dims → 0
53. PricingDefaults (Domain): WPF panel constructor defaults + all dropdown lists verbatim; catalog merges documented in WPF_TO_SAAS_MAPPING §1.4a (thickness union incl. both 15mm and the DGU+LAM 16mm quirk; "FT Glass" display form; interlayer thicknesses incl. LAM-only 3.04mm; panel extras = DGU 18-list). Interlayer price pairs PVB 100/110, EVA 75/95, SGP 0/0 (never invent — warn)
54. `GET /api/v1/pricing/config` returns WPF defaults when no row exists (NO row created on read; per-catalog fallback for empty `'[]'` columns too); `PUT` (Settings.Manage) validates schema CHECKs (yield > 0, VAT 0–1, 3-letter currency, no blank catalog entries — empty lists allowed) and upserts BOTH tenant_pricing_config and tenant_vat_config in ONE SaveChanges/transaction; `POST /api/v1/pricing/preview` is authenticated-only (pure calculation, WPF panel had no extra guard) and dispatches by glassType (case-insensitive)
55. Panel defaults + fixed catalogs (spacer widths/materials/extras, edge polish, PVB/interlayer colors, interlayer prices) are READ-ONLY in the config response — WPF hardcodes them; Phase 13+ may promote them to editable. Colors stay owned by the Sheet Store feed (2026-09-10 WPF change preserved)
56. `pricing/apply` (stamping rates onto Step 5 spec groups) deliberately deferred to Phase 7 where spec groups exist as entities — documented in mapping §1.4a
57. No new migration: schema 0003 already had tenant_pricing_config/tenant_vat_config with the right defaults; DB harness stays 35/35

### Phase 7a implementation facts (proforma document core)

58. Numbering ported verbatim to `Domain/Proforma/ProformaNumberSequence`: batch prefix load (BUG-08), pad width from the seed, int.TryParse overflow degradation (V-1 — "PI-99999999999" restarts numbering instead of crashing), 100k loop, GUID fallback capped at 50 chars (BUG-09), regex `^(?<prefix>.*?)(?<num>\d+)$` (BUG-11), case-insensitive existing set. Seed `PI-{year}-000` is unallocatable (IsUnallocatedSeed, blank included)
59. Number uniqueness is `UNIQUE(tenant_id, proforma_no)` — per-tenant (two tenants may both own PI-2026-001, integration-tested); the WPF global unique index + 5-attempt unique-violation retry loop is preserved over `TryInsertAsync` (false on PostgreSQL 23505, caught by SqlState — immune to error-message drift), `NumberExhausted` → 409 after 5
60. Lifecycle machine verbatim in `Domain/Proforma/InvoiceLifecycle`: 7 states, 11 transitions with WPF descriptions + RequiresApproval flags, IsTerminalState (Cancelled/Confirmed), IsEditable (Draft/Rejected), legacy "Pending"→PendingApproval and "Active"→Sent parsing; never throws — invalid transitions are typed 409s naming the valid set (WPF no-ops/dialogs → documented difference)
61. status vs lifecycle_state: the WPF had two loosely-coupled concepts (free-text picker status + the machine); the SaaS stores BOTH columns (schema 0003) — lifecycle_state is the machine truth, status stays the operator's display vocabulary written exactly where WPF wrote it (Step 1 picker, void). Void sets status="Cancelled" (DocumentLifecyclePolicy.VoidStatus[ProformaInvoice]) and — SaaS addition — lifecycle_state=Cancelled + voided_by = user id (WPF stored a username string)
62. Void verbatim: demands ProformaInvoice.Delete (controller), idempotent (second void = 200 no write), blank reason → "(no reason given)", row/number/snapshot/audit trail kept (ERP lifecycle hardening Phase 1)
63. Revision verbatim: gates (Draft → 409 "already editable"; voided/Cancelled → 409 "terminal history"), root = RootProformaId ?? own id (UUID FK — the WPF stored the root's NUMBER string; normalized, documented), chain includes the root (rev 0) so max+1 is next, number "{rootNo}-R{n}" with "-{guard}" collision suffixes (≤5), copies header refs + dates + 5 totals + snapshot + furthest_step as a fresh Draft
64. Step 5 grid verbatim: phantom rows (blank position + all dims zero) allowed by validation, dropped before store (schema CHECK qty>0 respected); caps 10000mm/10000qty; row math from GlassItemRow.RecalculateAll — Sqm1 via SqmCalculator (3dp + 0.5 floor), TotalSqm round3, dual W2/H2 measurement, Lm = round3((W+H)/1000 × perimeterFactor × qty) with factor default 2, Value = MoneyRounding(Sqm1 × Qty × Rate); glass_materials_subtotal = Σ row values (Step 7 GrandTotalValue); Sqm2/Lm derived on read (no sqm2 column — the generated EF model is not hand-edited, ADR-010)
65. Whole-grid replace is ONE transaction (tracked invoice field-copy + ExecuteDelete groups + insert groups/items + single SaveChanges); grid save refused unless Draft/Rejected (WPF IsEditable) or voided
66. Step 1 defaults: IssueDate = today, ValidUntil = +30 days (CompanySetupViewModel L538–539); create validates the status picker against ProformaStatuses.All (DB CHECK — unknown → 400, not pass-through: pass-through stays a READ behavior)
67. API permission map (WPF OperationGuard): create/revision = ProformaInvoice.Create; transitions/spec-groups = ProformaInvoice.Edit (the WPF has no separate approve grant — manager roles hold Edit; RequiresApproval travels in the audit entry); void = ProformaInvoice.Delete; reads authenticated. List = CreatedAt DESC (WPF list order) with status filter + case-insensitive number search
68. DatabaseFixture gained CreateTenant(label) — per-test tenants for document sets (config/invoices are one-row-per-tenant; sharing TenantA would make outcomes depend on test order)

### Phase 7b implementation facts (Step 6 — other charges/wastages, 0009)

76. Migration 0009 replaces the Phase-3 Step 6 guesses with the WPF contract: other_charges gains template_key/description/unit/metric_qty/polish_sub_type/custom_polish_* and the REAL metric registry CHECK (LM1, LM2, LM_COMBINED, SQM1, SQM2, SQM_COMBINED, DGU_CAVITY, QTY, DOUBLE_GLASS — 'SQM'/'Fixed' now rejected); wastage_rows drops spec_group_id/stock_sqm/glass_sqm (no WPF counterpart — a wastage row targets a SET of pane sides by "{spec}-{SideLabel}" keys) and gains total_sqm/is_manually_added/use_optimization_formula/checked_source_keys/sort_order; NEW custom_charges (RLS, 0005 pattern)
77. Wastage precision: rate widened numeric(18,2)→(18,6) (the optimization rate is DERIVED at 6 dp — price÷opt% − price÷85%, WastageRow.RecalculateOptimizationResults), formula price →(18,4); 0003's 2 dp truncated both on save
78. Step 6 needs Step 5 provenance the 7a schema lacked: spec_groups + Step 4 Phase G columns (outer/inner/third glass colours, per-side stock-sheet width/height/price — "a saved invoice always re-costs against the sheet it was QUOTED on") and spec_group_items.perimeter_factor (default 2) — the 7a READ path recomputed LM/SQM2 with the default factor, so a row saved with pf≠2 round-tripped wrong (fixed)
79. SpecGrammar.cs = PnLSpecParser grammar verbatim (Glass/Spacer/Interlayer regexes incl. the "Kibbing Nova 55" digit fix, ResolveTwoPanes outer-first/inner-LAST, ResolveAllPanes middle labels, SpacerCount) + ResolveEffectiveGlassType (spec-name markers win; type field authoritative when ambiguous; SGU fallback) + LaminateGlassMatcher (lowercase colour compare, known-colour scan in declaration order, layer split on '+')
80. ChargeMetrics.cs: per-spec metrics (footer 3 dp → hub 2 dp read model, pane factor DGU+LAM=3/DGU-LAM=2/else 1), TargetedMetric (Σ over checked specs), PolishLm (12 sub-type formulas, both panes of double builds, same-glass LAM 2× on W1-only rows, 3 dp), wastage pane sources (Outer always; Inner when DGU/LAM or ΣSQM2>0 with inner=ΣSQM2 else ΣSQM1; middle panes for 3+ builds; DGU+LAM=Outer+Lam Outer+Lam Inner), WastageSqm (Σ checked source values), amounts + subtotal (RecalculateTotal's plain Math.Round banker's — preserved, documented)
81. SaveStep6Async (PUT /{id}/step6, ProformaEdit, Draft/Rejected only, whole-section replace in ONE transaction): every metric qty and amount recomputed SERVER-SIDE from the saved grid (WPF recomputes live — never trusted from the client); DELIVERY=1 TRIP (Chat 7 Bug 4c), polish=sub-type formula, optimization rate DERIVED (client rate ignored); validation: real metric keys, known template keys, checked specs ⊆ grid, wastage source keys ⊆ available sources (stale → 400 "re-fetch after saving the grid" — documented difference vs the WPF's silent re-sync), units Sqm/Lm/Qty; subtotal lands on proforma_invoices.other_charges_subtotal
82. GET /{id}/step6 returns rows + the derived read model: per-spec metrics strip (the Step 6 top panel) + available wastage sources with the group's Step 4 provenance (colours, sheet sizes, prices) — the web client builds the section from this
83. EF generator extended (devbox/generate_ef_model.py): DROP COLUMN / DROP CONSTRAINT / DROP INDEX / ALTER COLUMN SET DEFAULT / ALTER COLUMN TYPE passes — 0009's single-action ALTER form is the supported syntax; multi-action ALTERs are NOT parsed (ValueError or silent skip risk)

### Phase 7b implementation facts (specifications registry + ledger)

69. Migration 0008 restores the WPF specifications model the Phase 3 schema missed (found by porting, same discipline as 0007): catalogue columns (category, color, thickness_mm, sheet_width/height_mm, sheet_id → glass_sheets FK SET NULL, panes_json, last_proforma_no), the natural key UNIQUE(tenant_id, glass_type, spec_name) — the WPF dedups saves by exactly this pair — and the specification_usages table with UNIQUE(spec_id, proforma_id) + RLS (0005 pattern; DEFAULT PRIVILEGES already grant saas_app)
70. usage_count column default corrected 1 → 0: the WPF starts a new spec at ZERO ("being built in Step 4 is not being used"); Phase 3's default of 1 was ungrounded
71. APPLY (POST /api/v1/specifications) is verbatim SaveOrUpdateAsync: dedup by (glassType, specName), rate ALWAYS refreshed, catalogue columns only when supplied (preserve-on-blank — a 3-arg-style APPLY must not wipe an earlier save's details), UsageCount/LastUsedAt/LastUsedAt NEVER touched by APPLY; permission ProformaEdit ("a saved spec sets the rate Step 5 and the P&L price against")
72. The LEDGER (SpecificationLedgerService ground truth) is a set-based ReconcileUsageAsync in ONE transaction: (1) PURGE usage pairs that no longer exist in spec_groups; (2) INSERT missing (spec_id, proforma_id) pairs with names/number from the SAVED invoice row (companies/clients/projects joined by id — "authoritative, not the draft-time context Step 4 saw"); (3) HEAL usage_count/last_used_at/last_proforma_no (DISTINCT ON carries the latest use's invoice). rate_per_sqm is NEVER touched (estimator-owned; per-invoice quoted rates live on the usage rows)
73. Reconcile runs after every Step 5 grid save — TOLERANTLY (try/catch, failure audited as specifications.reconcile_failed, the save still succeeds — WPF "NEVER THROWS") — and on POST /api/v1/specifications/reconcile (the Center's Refresh). One row per (spec, invoice) makes it idempotent ("2 PIs × 2 specs = 4 verified uses"); the WPF's per-save appends were exactly what its own ledger had to purge
74. Step 5 groups link to the registry: SpecGroupRequest.SpecId (optional, validated — unknown id → 400, not a DB FK 500); spec_groups.spec_id was already in schema 0003
75. Reads: list smart sort UsageCount DESC, LastUsedAt DESC (WPF GetAllAsync); usage history UsedAt DESC (404 on unknown spec); recent usage default 200, limit ≤ 0 → 200 (WPF). Delete demands ProformaDelete, 404 on missing (documented REST difference), usages cascade

### Environment constraint (documented per §46)

- This devbox: no .NET SDK, no NuGet egress (allowlisted: GitHub/PyPI/npm only) → **the .NET solution has not been compiled here**. It is syntax-checked (105/105 files, `saas/devbox/syntax_check_cs.py`) and maps to a schema verified live (43/43 checks, `saas/database/verify/apply_and_test.py`). First `dotnet build`/`dotnet test` in a .NET environment is the standing compile gate (auth was its first trigger); flip `TreatWarningsAsErrors` on in `saas/backend/Directory.Build.props` after that clean build.
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
| 2026-09-27 | 3 | Phase 4 (Authentication) authored: `ProGlass.Saas.Application` (AuthService, ports, WPF-verbatim PasswordPolicy), Infrastructure (Argon2id hasher, JWT service, tenant-explicit stores, platform-connection TenantProvisioner, AuditWriter, DI), API (auth endpoints `/api/v1/auth/*`, JwtBearer, permission policy provider/handler, TenantContextMiddleware), migration 0006 + generator ALTER-TABLE support, harness → **31/31 PASS**, 52/52 syntax, ~46 unit + 7 integration tests, ADR-011, docs | `2309aaa` |
| 2026-09-27 | 4 | Phase 5 master data: WPF MasterDataRepository semantics ported (natural-key upserts, usage counting, preserve-on-blank, single default, rename-collision refusal, salesman department quirk), `MasterDataService` + `IMasterDataStore` + EF store, `/api/v1/{companies,clients,projects,salesmen}` with WPF permission map, audit before/after payloads (IAuditWriter overload), migration 0007 (logo layout/size + document_prepared_by parity columns), 25 unit + 5 integration tests, harness **35/35 PASS**, 64/64 syntax, docs (mapping §1.10a/b, progress) | `88ecfb9` |
| 2026-09-27 | 5 | Phase 6 calculators + pricing settings: `Domain/Pricing/` (RateCalculator 4 formulas + cost bases + BUG-40/41 warnings verbatim, SpacerPricing, WastageFormula, SqmCalculator, PricingDefaults catalogs/panel defaults), `PricingService` + `IPricingConfigStore` + EF `PricingConfigStore` (both config rows in one transaction), `/api/v1/pricing/{config,preview}` (PUT = Settings.Manage), 43 golden unit tests + 5 integration tests (per-test tenants — config is one row per tenant), all golden values re-verified with Python `decimal` (no xUnit in sandbox), syntax 79/79, docs (mapping §1.4a + §1.10 backfill, progress) | `bb4b06e` |
| 2026-09-27 | 6 | Phase 7a proforma document core: `Domain/Proforma/` (ProformaNumberSequence BUG-08/09/11+V-1, InvoiceLifecycle machine, ProformaStatuses, SpecGroup row math + validation), `ProformaService` + `IProformaStore` + EF `ProformaStore` (TryInsert on 23505, one-transaction grid replace), `/api/v1/proforma-invoices` (create/list/get/transitions/void/revisions/spec-groups) with the WPF permission map, 47 domain golden + 30 service unit + 8 integration tests (per-test tenants), Python-decimal re-verification, syntax 92/92, harness 35/35, docs (mapping §1.2a, progress facts 58–68) | `814eb3d` |
| 2026-09-27 | 7 | Phase 7b specifications registry: migration 0008 (catalogue parity + natural key + specification_usages with RLS), EF model regenerated, `SpecificationsService` + `ISpecificationsStore` + EF store (set-based ledger reconcile: purge/insert/heal), `/api/v1/specifications` (apply/list/get/usage-history/recent/delete/reconcile, WPF permission map), grid→registry SpecId linkage + tolerant reconcile after grid saves, harness extended → **43/43**, 13 unit + 5 integration tests, syntax 100/100, docs (mapping §1.3a, progress facts 69–75) | (this commit) |
