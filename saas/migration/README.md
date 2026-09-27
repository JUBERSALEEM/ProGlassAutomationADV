# ProGlass WPF → SaaS migration tool (Phase 14)

`proglass_migrate` imports a WPF ProGlassAutomation data root (the
SQLite files EF Core created) into a SaaS tenant on PostgreSQL.
Governing document: `saas/docs/SAAS_MIGRATION_PLAN.md`.

**The WPF data root is read-only input.** Every SQLite file is opened
with `mode=ro`; the tool never writes back and originals are never
destroyed.

## Implementation note

The plan (a Phase 2 deliverable) sketches a .NET 8 console worker; this
environment has no .NET SDK, so the tool is implemented in Python with
the same CLI shape, module order and transform rules — fully executable
and tested here (end-to-end against a disposable PostgreSQL; see
`tests/test_migration_tool.py`). Porting the proven logic to the .NET
worker later is mechanical. Documented in
`saas/docs/WPF_TO_SAAS_MAPPING.md`.

## Usage

```bash
cd saas/migration

# What can import today (and what is deferred, with reasons):
../.venv/bin/python -m proglass_migrate list-modules

# Dry run first — loads exactly what an apply would, then discards:
../.venv/bin/python -m proglass_migrate import \
  --source "/path/to/ProGlassAutomation" \
  --db-uri "postgresql://..." \
  --tenant "company-slug" \
  --dry-run --report-dir out

# Apply (each module = one transaction; a failed module rolls back
# only itself and is reported):
../.venv/bin/python -m proglass_migrate import ... --apply

# Post-import validation report (HTML + JSON):
../.venv/bin/python -m proglass_migrate validate \
  --db-uri ... --tenant ... --report-dir out

# End-to-end test (fixture SQLite → disposable PostgreSQL):
../.venv/bin/python tests/test_migration_tool.py
```

`--tenant` accepts the tenant slug or its UUID; the tenant must already
exist (register it first). `--modules users,masterdata` imports a
subset.

## Semantics

- **Per-module transactions** (apply): a failed module rolls back only
  itself; a rejected ROW (CHECK/UNIQUE/FK) rolls back only that row via
  a savepoint and becomes an error finding — never half-loaded rows.
- **Dry-run**: the whole run shares ONE transaction (later modules'
  foreign keys resolve exactly as an apply would), discarded at the
  end.
- **Findings** (info/warning/error) are the report's core: balance
  mismatches, VAT recomputation differences, unknown statuses/roles,
  unresolvable references. Stored WPF values are imported VERBATIM;
  mismatches are reported, never silently "fixed".
- **Timestamps**: WPF local `DateTime.Now` values convert Asia/Dubai
  (+04:00, no DST) → UTC; `ActivityLog.TimestampUtc` passes through as
  UTC (plan §3).
- **Passwords** cannot migrate (WPF SHA-256 vs Argon2id): imported
  users get a valid Argon2id hash of a random unknowable secret and
  `must_reset_password = true`.
- **Machine licenses** are not migrated — registration creates the
  tenant's subscription (Phase 13).

## Module status

Implemented (plan §2 order): `users`, `masterdata`, `sheetstore`
(sheets + history → stock ledger with reconstructed balances),
`taxinvoices`, `creditnotes`, `statements` (+ ledger lines),
`activitylog`.

Deferred (listed by `list-modules`, never silently skipped):
specifications, inquiries, proformas (FullSnapshotJson → normalized
grids), optimization (oracle re-runs), joborders, production,
deliverynotes, attachments.
