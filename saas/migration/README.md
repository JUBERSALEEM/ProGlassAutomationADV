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

Implemented (plan §2 order — all 15):

| Module | Source | Target(s) |
|---|---|---|
| `users` | audit.db Users | saas.users (Argon2id reset flow) |
| `masterdata` | masterdata.db | companies/clients/projects/salesmen (+ the name/int-id maps the document modules resolve against) |
| `sheetstore` | proglass.db | glass_sheets/inventory_items/stock_transactions (ledger replay + drift findings) |
| `specifications` | specifications.db | saas.specifications (library rows; sheet link, PanesJson validated) |
| `inquiries` | inquiries.db | saas.inquiries (BOQ rebuilt as the InquiryBoqLine camelCase JSON the SaaS ParseBoq reads; WPF InquiryStatuses.Normalize) |
| `proformas` | proformainvoices.db | proforma_invoices + spec_groups/spec_group_items/other_charges/wastage_rows/custom_charges parsed out of FullSnapshotJson (snapshot kept verbatim; WPF SqmCalculator/SpecGroupRowMath ported for row math); inquiry backlink |
| `specusages` | specifications.db | specification_usages (runs AFTER proformas — proforma_id is a NOT NULL FK) |
| `optimization` | optimization.db | optimization_sessions as historical completed runs (the WPF persists aggregate results only — placement geometry is not in optimization.db, so neither oracle can re-run; validation_problems records it; reserved `__latest_run__*` plumbing rows skipped + counted) |
| `joborders` | joborders.db | job_orders (NOT NULL source PI enforced; ProductionDetails from the snapshot; legacy "In Production" spelling collapsed; PI job_order_id backlink) |
| `production` | production.db | production_pieces/production_events/material_reservations (the four §1.7b shapes) **plus the Phase 26a/26b port**: process_executions/process_pairings/process_assemblies, workstations (per-tenant), production_control_holds/assignments/overrides (0021) and material_allocations/production_actual_costs/production_replacements/cutting_sessions/cutting_sheets/cutting_placed_pieces (0022) — int enums → the SaaS TEXT vocabularies, int work-item ids → the migrated production_pieces uuids; ProductionReleases + ProductionQcEntries remain the documented §1.7b deferrals (counted, never silent) |
| `deliverynotes` | deliverynotes.db | delivery_notes/delivery_note_items (master + dispatch notes, spec-group-item links) |
| `taxinvoices` | taxinvoices.db | tax_invoices + tax_invoice_items (links to the imported proforma/job order; print/bank/TRN columns since 0019; line items with the spec-group-item link since 0020) |
| `creditnotes` | creditnotes.db | credit_notes |
| `statements` | statements.db | statements/statement_lines (ledger + running balance) |
| `activitylog` | audit.db ActivityLog | audit_logs |

`attachments` — **implemented in Phase 24** (was the one deferred
module): `DeliveryNoteAttachments` + `InquiryAttachments` rows land in
`saas.attachments` and the BYTES are copied (never moved) from the WPF
data root into the tenant-scoped attachment storage. The storage root
is `SAAS_ATTACHMENT_ROOT` or `./attachments`; `--attachments-dir`
overrides it per run. Files are resolved by the `StoragePath` column,
then the WPF `AttachmentService` layouts
(`DeliveryNotes/Attachments/{Images|PDFs}/{DN}/` and
`Attachments/{InquiryNumber}/`); the on-disk size wins over the
registered size (drift is an info finding), a missing file or an
unimported owner is an error finding — counted, never silently
dropped. A dry-run inserts NO rows and writes NO bytes.
