# ProGlass SaaS Backend

ASP.NET Core 8 + EF Core 8 (Npgsql) + PostgreSQL 16. Fully isolated from the
WPF application at the repository root — no shared projects, no shared files.

## Projects

| Project | Purpose |
|---|---|
| `src/ProGlass.Saas.Domain` | Entities, tenancy primitives, WPF-ported roles/permissions + money policy. Zero dependencies. |
| `src/ProGlass.Saas.Infrastructure` | EF Core model (generated from the verified DDL), `SqlMigrationRunner`, `TenantSessionInterceptor` (RLS session). |
| `src/ProGlass.Saas.Api` | API host (health endpoint now; auth + modules from Phase 4). |
| `tests/ProGlass.Saas.Domain.Tests` | Unit tests — role/permission parity with WPF, money rounding, tenant context. |
| `tests/ProGlass.Saas.IntegrationTests` | Database tests — tenant isolation (query filters + RLS), migration runner. |

> The `Application` layer project (use-cases/DTOs/validators per
> `saas/docs/SAAS_ARCHITECTURE.md`) is deliberately not scaffolded yet — it
> gets real content in Phase 4 (authentication). No empty placeholder projects.

## Build & test (needs .NET 8 SDK + NuGet access)

```
cd saas/backend
dotnet build ProGlass.Saas.sln
dotnet test ProGlass.Saas.sln
```

> **Status note (honest, per master prompt §46):** this solution was authored
> in a sandbox with **no .NET SDK and no NuGet egress** (see
> `saas/devbox/README.md`). Every file passes a tree-sitter C# syntax check
> (`python3 saas/devbox/syntax_check_cs.py` — 29/29), and the schema it maps
> to is fully verified against live PostgreSQL (23/23 checks), but the
> solution has **not yet been compiled**. First build in a proper .NET
> environment is the gate for Phase 4; flip `TreatWarningsAsErrors` on in
> `Directory.Build.props` after that first clean build.

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
itself and prints the 23-check report; the steps above are the .NET twin.)

## Schema changes

1. Edit/add `saas/database/migrations/*.sql` (SQL is the source of truth —
   never edit an applied migration, add a new numbered one).
2. Run `python3 saas/database/verify/apply_and_test.py` — must stay green.
3. Regenerate the EF model: `python3 saas/devbox/generate_ef_model.py`.
4. Run `python3 saas/devbox/syntax_check_cs.py`.
5. `dotnet build` + `dotnet test` in a .NET environment.
