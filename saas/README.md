# ProGlass Cloud ERP — SaaS (work in progress)

Multi-tenant SaaS re-engineering of the ProGlass Automation WPF desktop ERP.

**This folder is fully self-contained and shares nothing with the WPF application.**
The WPF app at the repository root is the *business-logic reference* and remains untouched:
no project references, no shared files, no modified WPF code. Business rules are **ported,
never linked**.

## Layout

| Path | Purpose |
|---|---|
| `docs/` | All SaaS documentation — start with `WPF_SYSTEM_AUDIT.md` |
| `backend/` | ASP.NET Core 8 API + Domain + Application + Infrastructure (Phase 3+) |
| `frontend/` | React + TypeScript SPA (Phase 28 — foundation + login/dashboard/job-orders workbench/stock; `npm run build` is the gate) |
| `database/` | Migration SQL, seeds, diagrams |
| `migration/` | WPF SQLite → PostgreSQL import/validation tooling (Phase 14) |
| `devops/` | Docker, compose, CI |

## Key documents

- `docs/WPF_SYSTEM_AUDIT.md` — complete reverse-engineered inventory of the WPF app
  (modules, 16 databases, every pricing/VAT/SQM formula, optimization engine, roles)
- `docs/WPF_TO_SAAS_MAPPING.md` — feature parity matrix
- `docs/SAAS_ARCHITECTURE.md` — stack decision + architecture + ADRs
- `docs/SAAS_DATABASE_DESIGN.md` — PostgreSQL schema, tenant isolation
- `docs/SAAS_API_DESIGN.md` — `/api/v1` contract
- `docs/SAAS_MIGRATION_PLAN.md` — SQLite → PostgreSQL migration strategy
- `docs/SAAS_MIGRATION_PROGRESS.md` — living progress tracker (read first each session)

## Ground rules

1. The WPF application is the source of truth for business behavior; differences require a
   documented reason (see `SAAS_MIGRATION_PROGRESS.md` → ADR log).
2. Pricing, VAT, stock, totals and optimization validation are **server-authoritative**.
3. No placeholder implementations marked complete; incomplete work is marked incomplete.
4. Every tenant-owned row carries `tenant_id`; isolation is enforced server-side and tested.
