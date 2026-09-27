# ProGlass SaaS Backend

ASP.NET Core 8 + EF Core 8 (Npgsql) + PostgreSQL 16. Fully isolated from the
WPF application at the repository root — no shared projects, no shared files.

## Projects

| Project | Purpose |
|---|---|
| `src/ProGlass.Saas.Domain` | Entities, tenancy primitives, WPF-ported roles/permissions + money policy, and `Pricing/` — the Step 4 calculators ported verbatim (`RateCalculator` SGU/DGU/LAM/DGU+LAM + cost bases, `SpacerPricing`, `WastageFormula`, `SqmCalculator`, `PricingDefaults`). Zero dependencies. |
| `src/ProGlass.Saas.Application` | Use-cases: `AuthService` (register/login/refresh/logout/sessions/change-password) + `MasterDataService` (companies/clients/projects/salesmen with WPF-verbatim upsert semantics) + `PricingService` (tenant pricing/VAT config + Step 4 rate preview), ports, `AuthOptions`, WPF-verbatim `PasswordPolicy`. References only Domain + DI abstractions. |
| `src/ProGlass.Saas.Infrastructure` | EF Core model (generated from the verified DDL), `SqlMigrationRunner`, `TenantSessionInterceptor` (RLS session), Argon2id hasher, JWT service, tenant-explicit auth stores, `MasterDataStore`, `PricingConfigStore` (tenant_pricing_config + tenant_vat_config in one transaction), platform-connection `TenantProvisioner`, `AuditWriter`. |
| `src/ProGlass.Saas.Api` | API host — `/api/v1/auth/*` endpoints (Phase 4), JwtBearer authentication, permission authorization (`[HasPermission]`), `TenantContextMiddleware`, `/health`. |
| `tests/ProGlass.Saas.Domain.Tests` | Unit tests — role/permission parity with WPF, money rounding, tenant context, **rate/spacer/wastage/SQM golden values from the WPF's own tests** (DGU+LAM chain 344.75, DGU basis 103.82, LAM 228.65, wastage 100.84). |
| `tests/ProGlass.Saas.Application.Tests` | Unit tests — AuthService orchestration with in-memory fakes (lockout semantics, rotation, reuse detection, absolute lifetimes) + MasterDataService upsert semantics + PricingService config lifecycle/preview dispatch + WPF PasswordPolicy cases ported. |
| `tests/ProGlass.Saas.IntegrationTests` | Database tests — tenant isolation (query filters + RLS), migration runner, **AuthFlowTests** (full register→lockout→login→refresh→reuse→password-change flow against live PostgreSQL), Argon2id + JWT contract tests.

## Build & test (needs .NET 8 SDK + NuGet access)

```
cd saas/backend
dotnet build ProGlass.Saas.sln
dotnet test ProGlass.Saas.sln
```

> **Status note (honest, per master prompt §46):** this solution was authored
> in a sandbox with **no .NET SDK and no NuGet egress** (see
> `saas/devbox/README.md`). Every file passes a tree-sitter C# syntax check
> (`python3 saas/devbox/syntax_check_cs.py` — 79/79), and the schema +
> identity constraints it maps to are verified against live PostgreSQL
> (35/35 checks, `saas/database/verify/apply_and_test.py`), but the
> solution has **not yet been compiled**. First build + `dotnet test` in a
> proper .NET environment is the Phase 4 exit gate; flip
> `TreatWarningsAsErrors` on in `Directory.Build.props` after that clean build.

## Database tests with a live PostgreSQL

Integration tests need two connections (RLS assertions must run as a
NON-superuser — superusers bypass RLS):

| Env var | What it is |
|---|---|
| `SAAS_TEST_DB` | Owner/superuser connection — applies migrations, seeds platform rows |
| `SAAS_TEST_APP_DB` | Unprivileged runtime role connection (`saas_test_app`) |

Easiest local setup (same tooling the verification harness uses):

```
pip install pgserver psycopg2-binary

# Terminal 1 — boot a disposable PostgreSQL 16 and keep it alive:
python3 -c "import pgserver, time; pgserver.get_server('/tmp/pgdata'); time.sleep(3600)"

# Terminal 2 — create the test login role once (superuser, owner URI):
export SAAS_TEST_DB="postgresql://postgres:@/postgres?host=/tmp/pgdata"
python3 - <<'EOF'
import os, psycopg2
c = psycopg2.connect(os.environ["SAAS_TEST_DB"]); c.autocommit=True
cur = c.cursor()
cur.execute("SELECT 1 FROM pg_roles WHERE rolname='saas_test_app'")
if not cur.fetchone(): cur.execute("CREATE ROLE saas_test_app LOGIN IN ROLE saas_app")
print("saas_test_app ready")
EOF

# Then run the tests:
export SAAS_TEST_APP_DB="postgresql://saas_test_app:@/postgres?host=/tmp/pgdata"
dotnet test tests/ProGlass.Saas.IntegrationTests
```

(The Python harness `saas/database/verify/apply_and_test.py` does all of this
itself and prints the 31-check report; the steps above are the .NET twin.
AuthFlowTests additionally accept `SAAS_PLATFORM_DB` for tenant provisioning —
unset, it falls back to `SAAS_TEST_DB`, which is correct in the pgserver
setup where the owner connection is already elevated.)

## Running the API (Phase 4)

```
export SAAS_DB="postgresql://saas_test_app:@/postgres?host=/tmp/pgdata"   # runtime role
export SAAS_PLATFORM_DB="postgresql://postgres:@/postgres?host=/tmp/pgdata"  # elevated (provisioning)
export SAAS_JWT_SIGNING_KEY="$(openssl rand -base64 48)"                   # >= 32 bytes, REQUIRED
dotnet run --project src/ProGlass.Saas.Api
```

Endpoints: `POST /api/v1/auth/register|login|refresh|logout|change-password`,
`GET /api/v1/auth/sessions`, `DELETE /api/v1/auth/sessions/{id}`,
`GET/POST /api/v1/companies|clients|projects|salesmen` +
`GET/PUT/DELETE …/{id}` (WPF permission map, see the controllers),
`GET/PUT /api/v1/pricing/config` (PUT needs `Settings.Manage`; a fresh tenant
reads the WPF defaults and no row is written until the first PUT),
`POST /api/v1/pricing/preview` (rate + cost basis + `warnings[]` with the
WPF's own BUG-40/41 texts — pure calculation, authenticated), `GET /health`.
Failures use the shared error envelope `{"code":"auth.invalid_credentials",
"message":"…"}`; lockouts answer **423 + Retry-After** (API design §1).
Also settable via `ConnectionStrings:Saas` / `ConnectionStrings:SaasPlatform` /
`Authentication:Jwt:SigningKey` in appsettings (env vars win in the code above
only when the config value is empty).

## Schema changes

1. Edit/add `saas/database/migrations/*.sql` (SQL is the source of truth —
   never edit an applied migration, add a new numbered one).
2. Run `python3 saas/database/verify/apply_and_test.py` — must stay green.
3. Regenerate the EF model: `python3 saas/devbox/generate_ef_model.py`.
4. Run `python3 saas/devbox/syntax_check_cs.py`.
5. `dotnet build` + `dotnet test` in a .NET environment.
