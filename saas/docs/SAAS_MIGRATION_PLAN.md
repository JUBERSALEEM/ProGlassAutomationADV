# SAAS MIGRATION PLAN — WPF SQLite → SaaS PostgreSQL

> **Document status:** Phase 2 deliverable (STEP 7 of the SaaS master plan).
> Governing rule: the WPF data root is **read-only input**. The migration tool opens every
> SQLite file in read-only mode and never writes back. Originals are never destroyed.

## 1. Tooling

A standalone console worker `/saas/migration/ProGlass.Saas.Migration` (.NET 8):

```
proglass-migrate import \
  --source "C:\...\ProGlassAutomation" \      # or a backup copy of the data root
  --connection "<postgres>" \
  --tenant "Company Name" \
  --plan default \                            # or per-module plan files
  --dry-run | --apply
proglass-migrate validate --report out.html   # post-import validation report
proglass-migrate seed-demo                    # optional demo tenant from WPF sample data
```

Stages per module: **Extract → Transform → Validate → Load → Report**. Each module imports
inside one transaction; a failed module rolls back only itself and is reported (never
half-loaded rows).

## 2. Module import order (dependency-aware)

| # | Source (SQLite) | Depends on | Notes |
|---|---|---|---|
| 1 | `audit.db` Users | — | Users become tenant users; password hashes **cannot** migrate (Argon2id) — import as `must_reset_password` |
| 2 | `masterdata.db` | — | Companies/clients/projects/salesmen; map names → ids |
| 3 | `proglass.db` | — | GlassSheets + SheetHistory → sheets + stock_transactions; balances recomputed from history and **compared** to stored Stock/Used |
| 4 | `specifications.db` | stock colors | Priced specs registry |
| 5 | `inquiries.db` | clients/projects | BOQ JSON preserved |
| 6 | `proformainvoices.db` | 2,4,5 | Parse `FullSnapshotJson` → normalized spec groups/items/other charges/wastage rows **+** keep snapshot JSON |
| 7 | `optimization.db` | 6,3 | Sessions re-run through both oracles; failures quarantined but imported with `validation_problems` |
| 8 | `joborders.db` / `production.db` | 6,7 | Pieces, stages, events, reservations |
| 9 | `deliverynotes.db` | 8 | Items + delivered/returned |
| 10 | `taxinvoices.db` / `creditnotes.db` | 6,9 | Fiscal docs |
| 11 | `statements.db` | 10 | Statements + lines |
| 12 | `audit.db` ActivityLog | 1..11 | Audit history appended (actor mapped to imported users) |
| 13 | attachments on disk | — | **Implemented (Phase 24)**: registry rows from DeliveryNoteAttachments + InquiryAttachments → `saas.attachments`; bytes COPIED (never moved) into the tenant-scoped attachment storage (`SAAS_ATTACHMENT_ROOT` / `--attachments-dir`) with generated keys `{tenant}/{entity}/{entity-id}/{unique-name}`; original names preserved; on-disk size wins over the registered size (drift reported); missing files / unimported owners are errors, never silent drops; dry-run writes no bytes |

## 3. Transform rules (explicit, per §21 and §49)

| WPF | SaaS | Rule |
|---|---|---|
| Local `DateTime.Now` values | `TIMESTAMPTZ` | Convert with tenant timezone (default Asia/Dubai) to UTC; audit's UTC values pass through |
| Money TEXT/DECIMAL columns | `NUMERIC(18,2)` | Parse exact decimal; re-verify totals by recomputation (§4) |
| Status free strings | CHECK enums + `status_text` overflow | **Unknown statuses pass through**, never remapped (WPF contract) |
| `RootProformaId`/`RevisionNumber` | same | Legacy blank root resolves to own id (WPF rule) |
| `IsVoid` docs | void columns | Preserve voided reason/actor |
| Sheet balances | transactions | `balance_after` reconstructed from history order |
| Attachment paths | storage keys | Path traversal impossible; missing files reported, not fatal |
| Passwords (SHA-256) | Argon2id | Force reset on first login (documented difference) |
| Machine licenses | tenant subscription | Not migrated — subscription created fresh (trial or granted plan) |

## 4. Validation report (per §21 + §48)

`validate` produces an HTML + JSON report:

- **Counts:** per source table → imported / skipped / invalid / duplicates
- **Financial integrity:** for every PI/TI: recompute
  `glass + other − discount → vat → grand` and compare to stored totals (tolerance 0.01);
  list every document that fails with both values
- **Referential:** orphans (items without doc, pieces without job, movements without sheet)
- **Optimization:** sessions failing conservation/geometry oracles (quarantined list)
- **Stock:** recomputed balance vs stored balance per sheet
- **Conflicts:** duplicate natural keys within source (e.g. same PI id), resolution applied

Acceptance gate: 0 failed financial integrity checks; all conflicts resolved or explicitly
acknowledged in the report sign-off.

## 5. Rollout strategy

1. **Pilot tenant:** first real company migrates to Staging; business owner spot-checks
   (list counts, one PI end-to-end, one optimization session, stock balances).
2. **Parallel run:** WPF continues in production; SaaS daily re-import (idempotent upsert by
   natural keys) until cutover decision.
3. **Cutover:** final delta import → WPF switches to read-only (the app's own ReadOnlyGate
   concept) → SaaS becomes system of record.
4. **Rollback:** WPF data root untouched throughout → fallback is reopening WPF (new SaaS
   data would need re-export, documented as accepted risk after sign-off).

## 6. Risks

| Risk | Mitigation |
|---|---|
| Snapshot JSON contains fields not modeled in normalized tables | Snapshot preserved verbatim; normalized projection is additive |
| Inter-DB references by name only | Name-resolution map built in step order; unresolved → report, not guess |
| Financial drift between snapshot and metadata columns | Recompute + report; 1-fil tolerance; owner sign-off on exceptions |
| Huge optimization histories | Import last N sessions per document by default (configurable) |
| Concurrent WPF writes during parallel run | Daily re-import uses updated_at watermarks; final delta at cutover |
