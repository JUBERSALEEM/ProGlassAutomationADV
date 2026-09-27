# devbox — tooling for restricted environments

The sandbox this SaaS is being built in has **allowlisted egress**: GitHub,
PyPI and npm are reachable; `api.nuget.org`, `dot.net` and Debian/Ubuntu
package mirrors are **not**. Therefore:

- No .NET SDK is available here → the backend cannot be compiled in-sandbox.
  Every `.cs` file passes a tree-sitter C# **syntax** check instead, and the
  first real `dotnet build` happens in an environment with the SDK
  (documented in `saas/backend/README.md`).
- No apt PostgreSQL → a real PostgreSQL 16 runs via the PyPI `pgserver`
  package (self-contained binaries), so the **schema and tenant isolation
  are fully verified here, not just designed**.

## One-time setup

```
python3 -m venv ~/.venv 2>/dev/null || python3 -m venv /home/user/.venv
/home/user/.venv/bin/pip install pgserver psycopg2-binary tree-sitter tree-sitter-c-sharp
```

(The venv lives outside the repo and outside snapshots; recreate any time.)

## Tools

| Script | What it does |
|---|---|
| `../database/verify/apply_and_test.py` | Boots PostgreSQL 16 (pgserver), applies all migrations, runs the 23 tenant-isolation/constraint/runner checks, writes the verification report. **This is the Phase 3 acceptance gate.** |
| `generate_ef_model.py` | Regenerates the EF Core entities + configurations from the verified DDL (single source of truth). |
| `syntax_check_cs.py` | Parses every `.cs` file under `saas/backend` with tree-sitter-c-sharp. Minimum local gate while no SDK exists. |

## Verified environment

- Python 3.11, Node 22, PostgreSQL 16.4 (pgserver), tree-sitter 0.2x
- Last full verification: 23/23 checks green — see
  `saas/database/verify/verification/PHASE3_VERIFICATION.md`
