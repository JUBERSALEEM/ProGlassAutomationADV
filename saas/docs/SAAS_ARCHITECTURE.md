# SAAS ARCHITECTURE — ProGlass Cloud ERP

> **Document status:** Phase 2 deliverable (STEP 4 of the SaaS master plan).
> **Stack decision (documented per master prompt §14 before implementation):**

## 1. Technology Stack Decision

| Layer | Choice | Why |
|---|---|---|
| Backend | **ASP.NET Core 8 (`net8.0`) Web API**, C# 12 | The WPF business logic (pricing, optimization engine, VAT, totaling) is C# and is **the source of truth**. Porting to another language would force re-implementation of ~11k lines of optimizer + all formulas and lose the oracle-test safety net. .NET 8 matches the WPF target framework exactly, so domain code ports with minimal friction. |
| Domain | **.NET class library** `ProGlass.Saas.Domain` — zero WPF/EF dependencies (mirrors `Services/Optimization` purity) | Business rules must be testable and reusable (§35, §40) |
| Database | **PostgreSQL 16** | Multi-tenant server DB, row-level scalability, `NUMERIC` exact decimals, JSONB for snapshots, partitioning path for 10k tenants |
| ORM | **EF Core 8 + Npgsql** with versioned migrations | Same ORM family as WPF (EF Core), real migration tracking (§42) |
| Frontend | **React 18 + TypeScript + Vite** | Production-grade SPA ecosystem; web-native ERP components (AG Grid-style data grids, react-hook-form, TanStack Query) |
| AuthN | JWT access tokens + refresh tokens (rotation), Argon2id password hashing, lockout, email verification, optional MFA (TOTP) | §17; ASP.NET Core Identity customized |
| Background jobs | **Hangfire** (PostgreSQL storage) | Optimization runs, report generation, imports, consistency scans (§29) |
| Files | **Object storage abstraction** (S3-compatible; local filesystem adapter for dev) | §26 |
| PDF/Excel | QuestPDF + ClosedXML + CsvHelper (same libraries as WPF → identical output possible) | §9 audit |
| Cache/pubsub | Redis (optional at first; interface from day 1) | §29 |
| Observability | Serilog structured logs + OpenTelemetry + `/health` checks, correlation IDs | §30 |
| Containers | Docker Compose (api, worker, postgres, redis, minio) → Kubernetes-ready | §44 |

**Non-choices (documented):** No microservices initially — a modular monolith with clean
layering gives transactional integrity across ERP documents (invoice + stock + audit in one
transaction) at 10–10k tenants, and can be split later along module seams. No GraphQL — REST
with versioning (§41). No client-side pricing authority — preview-only (§35).

## 2. Repository layout (complete separation from WPF — user requirement)

```
/ProGlassAutomationADV          ← existing WPF app — UNTOUCHED (no SaaS references)
/saas
  /docs                         ← all SaaS documents (this folder)
  /backend
    /src
      /ProGlass.Saas.Api                ← ASP.NET Core host (controllers, middleware, DI)
      /ProGlass.Saas.Application        ← use-cases, DTOs, validators, services
      /ProGlass.Saas.Domain             ← entities, pricing, optimization port, state machines
      /ProGlass.Saas.Infrastructure     ← EF Core, repositories, storage, jobs, external ports
    /tests
      /ProGlass.Saas.Domain.Tests       ← pricing, VAT, SQM, optimizer oracle tests
      /ProGlass.Saas.IntegrationTests   ← API + DB + tenant isolation
  /frontend                       ← React + TypeScript SPA
  /database                       ← migration SQL, seed, diagrams
  /migration                      ← WPF SQLite → PostgreSQL import tooling (reads WPF files, never writes)
  /devops                         ← Dockerfiles, compose, CI
  /docs                           ← this documentation set
```

The WPF `.slnx` is **never modified**. The SaaS gets its own solution file under `/saas/backend`.
No project references cross the boundary. Shared business concepts are **ported, not linked**
(master prompt §67; user's standing instruction).

## 3. Runtime architecture

```
Browser (React SPA)          Android app (future — same API)
        │                              │
        ▼                              ▼
   ┌─────────────────────────────────────────┐
   │  API Gateway (ASP.NET Core, /api/v1)    │
   │  auth · tenant-context · rate limit ·   │
   │  validation · audit · versioning        │
   └───────────────┬─────────────────────────┘
                   │
     ┌─────────────┼───────────────┬──────────────────┐
     ▼             ▼               ▼                  ▼
 Application   Domain         Infrastructure     Hangfire Worker
 services      (pricing,       (EF Core repos,    (optimization runs,
 (use cases,   optimization,   PostgreSQL,        report rendering,
 validators,   state machines, object storage,    imports, scans)
 DTOs)         VAT/Money)      email/storage ports)
     └─────────────┴───────────────┴──────────────────┘
                   │
             PostgreSQL 16  (+ Redis cache, + S3 storage)
```

**Request pipeline order:** HTTPS → security headers → rate limiting → JWT auth →
tenant resolution (from claims, NEVER from client body — §24) → permission policy →
model validation → subscription gate (write-block on expired) → use case →
EF Core tenant-scoped DbContext → audit log entry.

## 4. Multi-tenancy design (critical — §13)

- **Model:** single database, shared schema, discriminator column **`tenant_id UUID`** on
  every business table (plus composite indexes leading with `tenant_id`).
- **Enforcement layers (defense in depth):**
  1. EF Core **global query filter** `HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`
     on every tenant-owned entity — the default path physically cannot read another tenant.
  2. Repository/base-service guard that asserts every loaded entity's `TenantId` matches.
  3. Authorization policy per module; ownership re-check on every mutation by id.
  4. Integration test suite that reads/writes as Tenant A and asserts 404/403 against every
     Tenant B resource id (CI-enforced).
- **Tenant identity:** JWT carries `tenant_id`, `user_id`, role, permissions; resolved once
  per request into `ITenantContext`. Controllers/DTOs never accept `tenantId` as input.
- **Shared vs tenant data:** plans/features/subscription catalog are platform tables;
  everything business-owned (customers, invoices, stock, optimization, settings, users) is
  tenant-scoped. Users belong to exactly one tenant in v1 (member-of-many deferred).
- **Scale path:** 10 → 100 tenants on one node; 1,000+ with read replicas + pg_partman
  partitioning by `tenant_id` (table design already partition-friendly: every index leads
  with tenant_id); 10,000+ → schema-per-tenant option preserved by keeping tenancy in the
  query layer, not sprinkled through services.

## 5. Authentication & authorization (§17, §18)

- Registration → tenant created + Owner user (first user = Owner role) + trial subscription;
  email verification; login with lockout (mirrors WPF `FailedAttemptCount`/`LockedUntilUtc`,
  which survives restarts → persisted in DB); password reset; refresh-token rotation with
  reuse detection; optional TOTP MFA; session list + revoke.
- Passwords: **Argon2id** (documented improvement over WPF SHA-256+salt — §49 allows
  architectural security translation; migration forces reset or re-hash-on-first-login).
- **Roles & permission catalog ported verbatim from `Services/Audit/Permissions.cs`:**
  Administrator (`*`), Manager, Estimator, Sales, Production, Store, Accountant, Viewer;
  permissions `Module.Capability` (Inquiry.View/Create/Edit/Delete, ProformaInvoice.*,
  JobOrder.*, DeliveryNote.*, TaxInvoice.*, CreditNote.*, SheetStore.*, Reports.View/Export,
  Statement.Generate, Logs.View, Audit.View, Settings.Manage). Enforcement in **application
  services and authorization policies** — hiding UI buttons is never the boundary (same
  principle as WPF `OperationGuard`).
- SaaS platform roles (super-admin) are separate from tenant roles.

## 6. Subscriptions & licensing (§19)

- `Plan` (name, price, billing period) · `Feature` (key) · `PlanFeature` (limit values) ·
  `TenantSubscription` (plan, status: Trialing/Active/PastDue/Suspended/Cancelled, period
  bounds, trial end) · `TenantFeature` (per-tenant override/flag).
- **Limits enforced in application services** (users, invoices/month, storage GB,
  optimization jobs/month, reports/month) — hard-coded nowhere (§34); 429/402 semantics
  with explicit, operator-readable error payloads.
- **Write-gate on expiry** mirrors WPF `ReadOnlyGate`: suspended/expired tenants keep full
  read access, writes blocked at the service boundary with the same explicit messaging
  philosophy (the WPF red-banner precedent).
- Payment provider isolated behind `IPaymentGateway` port — no provider in core v1.

## 7. Business-logic placement (§35, §40 — one definition each)

| Concern | Single owner (Domain) |
|---|---|
| SGU/DGU/LAM/DGU+LAM rates & cost bases | `PricingCalculator` (formulas ported verbatim from `PricingEstimatorViewModel.Part2.cs`) |
| VAT (5%, back-calc, 1-fil tolerance) | `VatCalculator` (per-tenant rate config, default 5%) |
| Money rounding (2dp AwayFromZero) | `Money` value object |
| SQM (3dp panel + 0.5 m² floor; 4dp sheet) | `SqmCalculator` |
| PI/J/DN/TI state machines + status pass-through | `DocumentStateMachines` |
| Optimization | `GuillotinePacker` port + oracles `Verify`/`ValidateResult` |
| Numbering (`PI-{year}-NNN`, retry on collision) | `DocumentNumberSequencer` |
| Stock movements | `StockTransactionService` (no quantity update without a transaction row) |

Frontend may compute **previews only** (e.g., live rate preview in the pricing form) — the
server recomputes and is authoritative on every save.

## 8. Background jobs (§29)

| Job | Trigger | Notes |
|---|---|---|
| Optimization run | `POST /api/v1/optimization/jobs` → Hangfire | Progress via polling endpoint or SSE; results re-validated by oracles before persisting; deterministic (same input → same layout) |
| Excel/CSV import | upload → parse → validate → preview → confirm | Commit only after explicit confirmation (§27) |
| Report/PDF rendering | on demand | Large exports streamed from object storage |
| Consistency scans | scheduled | Port of `ProductionInventoryConsistencyScanner` + `ProfitLossIntegrityCheck` ideas |
| Statement/aging snapshots | nightly | Dashboard aggregates |
| Backup verification | scheduled | Postgres backups + restore drill (WPF `RestoreDrillService` precedent) |

## 9. API shape

REST, versioned `/api/v1` (§16, §41), DTO-only (never EF entities), FluentValidation,
consistent error envelope (`{ code, message, correlationId, validation? }`), RFC-7807-style
problem details, pagination (`?page&pageSize` + totals), idempotency keys on POST of
financial docs and imports (§53). Full contract in `SAAS_API_DESIGN.md`.

## 10. Security (§24)

HTTPS-only, HSTS; secure HttpOnly cookies for refresh token (or BFF pattern); JWT short
TTL; CSRF protection where cookies are used; strict CSP + security headers; rate limiting
per IP + per tenant; input validation everywhere; parameterized SQL via EF Core; XSS-safe
React rendering (no `dangerouslySetInnerHTML`); file uploads validated by type/size with
generated storage keys; secrets from environment/secret store, never committed (§43);
audit log on every important action (§25); no stack traces to clients (§33); request
correlation IDs (§30).

## 11. Deployment (§44) & environments

- `docker compose up` → api, worker, postgres, redis, minio, frontend (nginx).
- Environments: Development, Testing, Staging, Production (§43) via `ASPNETCORE_ENVIRONMENT`
  + `appsettings.{env}.json` + env vars.
- CI: build + test (unit/integration/tenant-isolation) + migration check + container publish.
- Backups (§45): automated daily Postgres dumps + WAL archiving; retention policy;
  documented, **drill-tested** restore procedure (tenant-restore runbook).

## 12. Future Android app (§57)

The SPA and a future Android client consume the **same** versioned API. No Android-specific
business logic; all rules server-side. Refresh-token flow supports mobile clients; OpenAPI
spec published from CI for client generation.

## 13. Extensibility (§58)

Ports defined for: `IPaymentGateway`, `IEmailSender`, `IWhatsAppSender`,
`IBackgroundJobClient`, `IFileStorage`, `IBarcodeService`. Nothing implemented before core
works.

## 14. ADRs (architecture decision records)

| # | Decision | Rationale |
|---|---|---|
| ADR-001 | Modular monolith, not microservices | ERP needs cross-document transactions; module seams allow later extraction |
| ADR-002 | PostgreSQL single-DB shared-schema multi-tenancy | Cheapest correct model for 10–10k tenants; partition-ready |
| ADR-003 | Port optimization engine as-is (C#) | 11k lines, calibrated, oracle-pinned; rewrite risk unacceptable |
| ADR-004 | Same output libraries (QuestPDF/ClosedXML/CsvHelper) | Document parity with WPF output |
| ADR-005 | React+TS over server-rendered MVC | ERP-grade grid/form UX, future offline/PWA option |
| ADR-006 | Argon2id replacing SHA-256 | Security translation, documented (§49) |
| ADR-007 | UTC storage + tenant timezone display | WPF mixed local/UTC; SaaS standardizes |
| ADR-008 | **SQL-first migrations** (`saas/database/migrations/*.sql` + `SqlMigrationRunner`) instead of EF Core migrations | The WPF app already runs a hand-rolled SQL migration runner (`SchemaMigrationRunner`) — team-familiar pattern; plain SQL DDL is directly verifiable against live PostgreSQL without the .NET SDK (the devbox has none); EF maps the schema but never owns it. Applied migrations are checksum-tracked (`saas._saas_migrations`); edits to applied files are hard errors. |
| ADR-009 | **PostgreSQL Row-Level Security as the database half of tenant isolation** (plus EF global query filters as the application half) | Master prompt §13 requires enforcement at the server/database layer. RLS policy on every tenant-owned table (`tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid`, ENABLE + FORCE), set per session by `TenantSessionInterceptor`; the EF query filter is the primary read path — either layer alone blocks cross-tenant access, so a bug in one cannot leak data. Verified by 23 live checks (RLS SELECT/UPDATE/DELETE/INSERT-block, fail-closed on unset/empty context). |
| ADR-010 | EF entities/configurations are **generated from the verified DDL** (`saas/devbox/generate_ef_model.py`) | One source of truth for the schema; the C# model cannot drift from what was actually applied and tested; regeneration is part of the documented schema-change procedure. |
