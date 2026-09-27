# ProGlass SaaS — production deployment & hardening guide (Phase 15)

This is the operator's guide for running the SaaS in production. The
WPF desktop deployment concerns (Velopack updates, machine licenses,
SQLite WAL hardening) do not apply; their SaaS equivalents are listed in
`WPF_TO_SAAS_MAPPING.md` §1.14.

## 1. Prerequisites

| Component | Requirement |
|---|---|
| PostgreSQL | 14+ (verified against 16; extensions: none beyond `pgcrypto`-provided `gen_random_uuid()`, which is core since 13) |
| .NET SDK | 8.0 — **the compile/test gate**: the whole codebase is authored and syntax-verified in a no-SDK environment; run `dotnet build && dotnet test` in `saas/backend` before the first production start and after every pull |
| Reverse proxy | nginx/Caddy/traefik — TLS termination, request body limits, (multi-instance) rate limiting |
| OS | any .NET 8 host (Linux recommended) |

## 2. Database roles (least privilege — verified by the hardening audit)

Three roles, created by `0001_platform_identity.sql` / the bootstrap:

| Role | Privileges | Used by |
|---|---|---|
| `saas_owner` | schema owner, DDL | migration runner, backups |
| `saas_app` | DML on tenant tables; **SELECT-only** on platform tables (tenants/plans/features); no superuser/createdb/createrole | the API's runtime connection |
| `saas_test_app` | same shape as `saas_app` | verification harness / integration tests |

The API NEVER provisions tenants on its runtime connection —
registration uses the elevated `SAAS_PLATFORM_DB` connection
(`TenantProvisioner`, ADR-011). Keep that credential separate and
minimally exposed.

## 3. Environment / configuration

| Variable (appsettings / env) | Meaning | Production value |
|---|---|---|
| `SAAS_DB` | runtime (RLS-restricted) connection | the `saas_app` role's connection string |
| `SAAS_PLATFORM_DB` | elevated connection | `saas_owner`; required for registration + the subscription admin port |
| `Jwt:SigningKey` | HS256 key | ≥ 32 random bytes; **rotate via config, never commit** (startup fails on a short key) |
| `Jwt:Issuer` / `Audience` / lifetimes | token settings | short access lifetime (default 15 min) |
| `Saas:MigrationsDirectory` | where `SqlMigrationRunner` looks | the repo's `saas/database/migrations` |
| `Saas:MigrateOnStartup` | apply pending migrations at boot | `true` for single-instance; `false` when migrations run as a release step |
| `ASPNETCORE_ENVIRONMENT` | — | `Production` (disables detailed error pages) |

## 4. First start

```bash
cd saas/backend
dotnet build -c Release
dotnet test                      # the standing compile/test gate
cd src/ProGlass.Saas.Api
dotnet run -c Release            # boots, applies migrations if enabled
curl -f http://localhost:8080/health   # {"status":"ok","database":"reachable"}
```

## 5. The hardening layers (all in the repo, all verified)

| Layer | Where | Verified by |
|---|---|---|
| Row-Level Security on all 40 tenant tables (FORCED, per-tenant policy) | `0005_rls.sql` | `database/verify/hardening_audit.py` → `verification/HARDENING_AUDIT.md` (14/14) |
| Role least-privilege | `0001` + audit checks 3–4 | hardening audit |
| Tenant erasure (ON DELETE CASCADE on every tenant FK) + orphan scan | schema + audit checks 5–6 | hardening audit |
| CHECK-constrained vocabularies (52 checks), unique fiscal numbers | migrations | hardening audit + harness 59/59 |
| FK index coverage (49 indexes added in `0018`) | `0018_hardening_fk_indexes.sql` | hardening audit check 9 |
| Argon2id password hashing + the WPF password policy | `Argon2idPasswordHasher`, `PasswordPolicy` | Phase 4 tests |
| Per-user lockout (WPF-verbatim) + per-IP credential rate limiting | AuthService + `LoginRateLimitMiddleware` | Phase 4 tests; middleware is compile-gated |
| Security headers on every response | `SecurityHeadersMiddleware` | compile-gated |
| Unhandled exceptions → the `{code,message}` envelope, no internals leaked | `GlobalExceptionHandlerMiddleware` | compile-gated |
| Subscription read-only/suspended enforcement at the HTTP edge | `SubscriptionGateMiddleware` (Phase 13) | Phase 13 tests |
| Immutable audit trail | `audit_logs` + `IAuditWriter` on every write | per-phase tests |

"Compile-gated" = the code is authored and syntax-verified here; the
`dotnet build && dotnet test` gate runs in the .NET 8 environment (the
sandbox has no SDK — documented since Phase 3).

## 6. Backups (the WPF BackupService port)

```bash
cd saas
.venv/bin/python devbox/backup_db.py --db-uri "$SAAS_OWNER_DB" \
    --backup-dir /var/backups/pglass --reason scheduled
# verify a backup is actually restorable (RestoreDrillService port):
.venv/bin/python devbox/backup_db.py --db-uri "$SAAS_OWNER_DB" \
    --backup-dir /var/backups/pglass --restore-drill
```

Policy (WPF-verbatim constants): backup SETS with retention 10; the
manual-warn window refuses a second set within 10 minutes unless
`--force`; the restore drill restores the newest set into a scratch
database, verifies the archive checksum and every per-table row count,
then drops the scratch database. **Schedule the drill** (cron) — a
backup that was never restored is a hope, not a backup. Restore
procedure: `pg_restore --dbname <new-or-restored-db> --no-owner
--no-privileges <set>/database.dump`, then point `SAAS_DB` at it.

## 7. Monitoring hooks

- `GET /health` — liveness + database reachability (no auth).
- `hardening_audit.py --db-uri …` — run on a schedule; non-zero exit on
  any failed check (alert on change).
- `apply_and_test.py` — the same checks as release-time verification.
- Logs: the API logs structured events (`LogError` with correlation
  ids from the exception handler); ship to your aggregator.

## 8. Production checklist

- [ ] `dotnet build -c Release && dotnet test` green in the .NET 8 environment
- [ ] `SAAS_DB` uses `saas_app`; `SAAS_PLATFORM_DB` kept separate
- [ ] JWT signing key ≥ 32 random bytes, from the secret store
- [ ] TLS at the proxy; HSTS at the proxy; the API bound to localhost or an internal interface
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] migrations applied (startup flag or release step)
- [ ] hardening audit 14/14 against the production database
- [ ] backup schedule + restore drill schedule both green
- [ ] plan catalog seeded (0017) and tenant onboarding tested
