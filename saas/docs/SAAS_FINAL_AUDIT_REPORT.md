# ProGlass SaaS — FINAL AUDIT REPORT

**Date:** 2026-09-27 · **Branch:** `arena/01a0e138-proglassautomationadv` ·
**Head at report time:** `bd24f00`
**Scope:** the complete WPF → SaaS port, Phases 1–15, audited after all
phases were applied and pushed.

---

## 1. Executive summary

The ProGlassAutomation WPF desktop ERP has been ported to a multi-tenant
SaaS (ASP.NET Core 8 + PostgreSQL, Row-Level Security) in its own
top-level `saas/` directory with **zero project references to the WPF
code** — every behavior was re-derived from the WPF source and
re-implemented, per the governing constraints (complete separation;
audit before code; no invented rules; no fake completions; WPF behavior
preserved or the difference documented).

All 15 planned phases are **complete** (core slices, with every
deferral documented in `WPF_TO_SAAS_MAPPING.md`). Final verification
sweep, run against a live PostgreSQL 16 immediately before this report:

| Verification | Result |
|---|---|
| C# syntax (tree-sitter, 182 files) | **182/182 clean** |
| Database harness (`apply_and_test.py`, 18 migrations, RLS/constraints/runner) | **ALL CHECKS PASSED 59/59** |
| Hardening audit (`hardening_audit.py`, 14 live checks) | **14/14** |
| Migration tool end-to-end (fixture SQLite → live PG) | **PASSED** |
| Backup + restore drill (live PG, incl. corruption detection) | **PASSED** |

**The one standing gate:** this environment has no .NET SDK — the C#
code has never been compiled or executed. It is authored, syntax-checked
and DB-verified; `dotnet build && dotnet test` in a .NET 8 environment
remains the release gate (SAAS_DEPLOYMENT.md §1). All executable
verification that CAN run here (PostgreSQL schema, RLS, migrations,
importer, backups, audits) has run and passes.

## 2. Phase-by-phase delivery

| Phase | Deliverable | Commit / status |
|---|---|---|
| 1 | Deep audit → 8 analysis docs in `saas/docs` | `e5046ac` |
| 2 | Migration plan, API design, DB design | done (docs) |
| 3 | 5 SQL migrations, 41 tables, RLS on tenant tables, migration runner, EF model, roles/permissions + MoneyRounding ports | `b4a7e9d` |
| 4 | Authentication (Argon2id, JWT, WPF-verbatim PasswordPolicy, lockout, tenant provisioning, audit trail) + migration 0006 | `2309aaa` |
| 5 | Master data (WPF upsert semantics) + migration 0007 | `88ecfb9` |
| 6 | Calculators + pricing config (RateCalculator 4 formulas, BUG-40/41 verbatim, goldens) | `bb4b06e` |
| 7 | Proforma document core + specifications registry + Step 6/7/9 + target pricing + duplicate/convert + automation suggestions (migrations 0008–0011) | `814eb3d`…`0c49704` |
| 8 | Sheet Store core (sheets/items/movements/ledger, atomic buy/use) | `51ff264` |
| 9 | Job Order core (0012) + Production core (0014) + reservations (0015) | `28dc6ff`, `3bdfb6f`, `0ca60e0` |
| 10 | Delivery core (master DN balance tracker, one-path create-or-open) + 0013 | `62ca43d` |
| 11 | Optimization core (GuillotinePacker QUICK verbatim, safety net, session persistence) | `dd88337` |
| 12 | Fiscal core (tax invoices w/ fiscal lock + family numbering + UAE VAT, credit notes, statements, VAT report) + 0016 | `b1669ab` |
| 13 | Subscriptions (trial seeding, plan/feature catalog, read-only gate, platform admin port) + 0017 | `67baf6b` |
| 14 | Migration tooling (`proglass-migrate`, 7 modules, dry-run/apply, reports) | `190d8b4` |
| 15 | Production hardening (audit 14/14, 0018 FK indexes, backup + restore drill, API middlewares, deployment guide) | `bd24f00` |

27 commits on the session branch from the base commit.

## 3. Inventory (measured, not estimated)

| Item | Count |
|---|---|
| SQL migrations | 18 (0001–0018) |
| PostgreSQL tables (saas schema) | 45 |
| C# source files | 182 (all parse clean) |
| EF entities | 44 |
| API endpoints (attribute-counted) | 125 across 15 controllers |
| Test files | 53 (279 domain golden facts + 73 integration flow facts) |
| Python tooling (harness, generator, importer, backup, audit) | 8 verified programs |
| Mapping doc sections | 24 parity sections (§1.x) |

## 4. Parity discipline (the port's core promise)

Every module's WPF source of truth is cited in `WPF_TO_SAAS_MAPPING.md`
with a per-row "Difference?" verdict — **Same**, **Documented
difference** (with the reason), or **Deferred, documented**. Verified
verbatim ports include: the roles/permissions catalog, MoneyRounding
(half-fil away-from-zero), the password policy, every StatusTransitions
table (proforma, job order, delivery, tax invoice, credit note), the
pricing rate formulas with their BUG-40/41 warnings, the P&L/Step-7
math incl. the gross/1.05 quirk, the optimizer's QUICK pass + margin
house table, the UAE VAT calculator, payment terms, family invoice
numbering, the fiscal lock, the ReadOnlyGate messaging, the licensing
edition tiers, and the backup retention policy.

### 4.1 Deferred work (documented, not dropped — mapping §1.12/§1.12a)

| Area | Deferred items |
|---|---|
| Documents | PDF/print exports (needs a render surface); attachment storage |
| Reporting | exports (CSV/Excel), reporting cache, executive summary/analytics dashboards, profitability, RowTraceability, CncExport, cutting/production-process reports, order reconciliation |
| Optimization | QUICK portfolio extras (pattern beams, plate patterns, consolidation, offcut reuse, local search), OPTIMIZE multi-start, staged cuts, remnant reuse |
| Production | process executions/pairings/assemblies, workstations, control holds, actual costs, replacement pieces, consistency scanner, allocation ledger, FOC replacement |
| Stock | CSV/Excel import, SheetStorePricing/SheetStockLink module links |
| Fiscal | FullSnapshotJson population, bank/TRN print fields, wider statement filters |
| Subscriptions | payments/billing provider; invoice/storage/optimization-job limits (columns exist) |
| Migration tool | 8 document modules (specifications, inquiries, proformas, optimization, joborders, production, deliverynotes, attachments) |
| Advanced automation | UseAdvancedAutomation P&L costing until the toggle is persisted |

## 5. Multi-tenant security posture (independently audited)

- **Row-Level Security** enabled + FORCED on all 40 tenant tables, one
  `tenant_isolation` policy each; runtime role is not the owner.
- **Least privilege:** `saas_app` has no INSERT/UPDATE/DELETE on
  platform tables (tenants/plans/features), no superuser/createdb/
  createrole; tenant provisioning and the subscription admin port run
  only on the separate elevated connection.
- **Tenant erasure:** every tenant FK cascades; the orphan scan is clean.
- **Vocabularies are DB-enforced** (52 CHECK constraints); fiscal
  document numbers unique per tenant.
- **Passwords** are Argon2id (PHC-shaped, audited); the WPF SHA-256
  hashes are non-migratable by design (must-reset on import).
- **API edge:** security headers, correlation-id'd error envelope with
  no internals leaked, per-IP credential rate limiting, per-user lockout
  (WPF-verbatim), subscription read-only/suspension enforcement.
- **FK index coverage** closed by migration 0018 (49 indexes) — a real
  finding by the audit, fixed and re-verified.

## 6. Data migration readiness

`saas/migration/proglass_migrate` (Python; the plan's .NET worker is the
documented porting target) imports users, master data, sheet stock (with
ledger reconstruction + balance comparison), tax invoices, credit notes,
statements and the activity log from the WPF SQLite data root — opened
**read-only**, per-module transactions, row-level savepoints, dry-run
that loads exactly what an apply would, findings for every anomaly
(unknown statuses/roles, VAT mismatches, unresolvable references),
HTML+JSON reports, and a post-import `validate` command. Tested
end-to-end against a WPF-shaped fixture including deliberate problems.

## 7. Operational readiness

`SAAS_DEPLOYMENT.md` covers prerequisites, the three-role model,
configuration, first start, the hardening layers table, backup +
restore-drill schedules (WPF policy constants, verified incl. corruption
detection), monitoring hooks, and the production checklist.

## 8. Risks & required follow-ups

| # | Risk / gap | Mitigation |
|---|---|---|
| 1 | **C# never compiled** (no SDK in the authoring environment) | Run `dotnet build && dotnet test` in .NET 8 before release — the standing gate since Phase 3; fix any compile-level findings there |
| 2 | Middlewares (rate limit, headers, exception handler) are syntax-verified only | Same gate; the middleware logic is deliberately minimal |
| 3 | PDF/print exports deferred | The fiscal DATA layer is complete; a render slice builds on it |
| 4 | Payment provider absent | Plan changes are instant/free; the admin port is isolated for the provider |
| 5 | 8 migration-tool document modules deferred | The engine + transform patterns are proven; each module is a reader slice |
| 6 | In-memory rate limiter is single-instance | Multi-instance: proxy-level limiting (documented) |
| 7 | Migration tool is Python, plan said .NET worker | Same CLI/semantics; porting is mechanical (documented in §1.12a) |

## 9. Conclusion

The port is **structurally complete and verified to the maximum extent
this environment allows**: 15/15 phases delivered, every deferral
documented with its reason, the database layer proven against live
PostgreSQL (59/59 harness, 14/14 hardening audit), and the operational
tooling (importer, backups, audits) exercised end-to-end. The remaining
release gate is the .NET compile/test run plus the deferred feature
slices listed in §4.1.
