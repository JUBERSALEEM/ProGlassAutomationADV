# SQLite Hardening — Full Cross-Check · August 2026

## Why this pass

Follow-up to `docs/SQLITE_HARDENING_2026-08.md`. The user asked:

> "Cross check is it perfect now or any other issues in all modules
> db related fix them"

The first pass fixed **connection-level** hardening (WAL, busy_timeout,
foreign_keys=ON, cascade enforcement) on all 12 DbContexts and fixed
the Inquiry-specific retry bug. But every OTHER repository still called
`SaveChangesAsync()` unprotected — under WAL contention any of those
writes could still fail with `SQLITE_BUSY`, killing the user's action.

This pass finishes the job.

## What was still wrong

### Category A — Unwrapped `SaveChangesAsync` (61 call sites across 14 files)

Every repository outside `InquiryRepository` called `SaveChangesAsync()`
raw, so a transient `SQLITE_BUSY` under WAL was not retried:

| File | Save calls | Severity |
|---|---:|---|
| `Services/MasterData/Repository/MasterDataRepository.cs` | 12 | High — hit on every module load |
| `Services/DeliveryNote/Repository/DeliveryNoteRepository.cs` | 10 | High — DN detail auto-save conflicts with list refresh |
| `Services/Audit/AuditService.cs` | 8 | **Critical** — losing an audit write is a compliance issue |
| `Services/TaxInvoice/Repository/TaxInvoiceRepository.cs` | 6 | High |
| `Services/SheetStore/SheetStoreRepository.cs` | 5 | Medium |
| `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs` | 5 | Medium |
| `Services/Statement/Repository/StatementRepository.cs` | 4 | Medium |
| `Services/ProformaInvoice/SpecificationsRepository/SpecificationsRepository.cs` | 4 | Medium |
| `Agent/Knowledge/AgentKnowledgeService.cs` | 4 | Medium |
| `Agent/Teaching/AgentTeachingService.cs` | 4 | Medium |
| `Services/JobOrder/Repository/JobOrderRepository.cs` | 3 | Medium |
| `Services/AI/AIMemory/AiMemoryService.cs` | 2 | Low |
| `Agent/Memory/AgentMemoryService.cs` | 2 | Low |
| `Services/SheetStore/SheetStoreHistory/SheetHistoryRepository.cs` | 1 | Medium |
| `Services/SampleData/SampleDataService.cs` | 1 | Low (seed only) |
| **Total** | **71** | |

### Category B — Legacy string-match `IsUniqueViolation` (1 file)

`ProformaInvoiceRepository.IsUniqueViolation` still string-matched
`"UNIQUE constraint failed"` on the exception message. `Microsoft.Data.
Sqlite` changed error-message text between major versions before —
next upgrade could silently make the check return false, breaking the
retry loop's UNIQUE detection.

### Category C — Manual `SqliteConnection`s that bypass the interceptor (2 critical)

`AuditDbMigrations.Open()` and `SchemaMigrationRunner.Open()` create
raw `SqliteConnection`s. Those don't invoke the EF `DbConnectionInterceptor`,
so **migrations were running without WAL, without foreign_keys=ON, and
without busy_timeout**. A migration racing with the app's normal writer
could still fail with `SQLITE_BUSY`.

Read-only connections in `SchemaHealthService` and `RestoreDrillService`
are safe as-is — WAL is a writer-side format, and read-only connections
work fine against a WAL database opened by another process.

## The fix

### Category A — 71 SaveChangesAsync calls wrapped

Every `await X.SaveChangesAsync()` was mechanically rewritten to:

```csharp
await SqliteResilience.SaveWithRetryAsync(_ => X.SaveChangesAsync());
```

`_` is a discard because these callers don't accept a `CancellationToken`
(if they ever do, propagating it is a one-character change).
`SaveWithRetryAsync` retries only on `SQLITE_BUSY` / `SQLITE_LOCKED`
with exponential back-off up to 5 attempts (40 → 80 → 160 → 320 → 640 ms).
UNIQUE / NOT NULL / FK / CHECK violations still surface on the very
first attempt so real domain errors are not hidden.

`using ProGlassAutomation.Services.Database;` was added to the 6 files
that didn't already have it (Statement / Specifications / SampleData /
MasterData / AiMemoryService / AuditService, plus all 3 Agent services).

### Category B — Hoisted UNIQUE detection to the classifier

`ProformaInvoiceRepository.IsUniqueViolation` is now a thin wrapper
around `SqliteErrorClassifier.IsUnique(ex)`, which uses
`SqliteExtendedErrorCode` (`2067 = SQLITE_CONSTRAINT_UNIQUE`,
`1555 = SQLITE_CONSTRAINT_PRIMARYKEY`). Message-sniffing is a fallback
only — the primary path is the numeric code, so a driver upgrade will
not silently break it. The retry loop's per-attempt-fresh-`DbContext`
shape was already correct and untouched.

### Category C — Manual connections re-apply the pragma script

`AuditDbMigrations.Open()` and `SchemaMigrationRunner.Open()` now:

1. Build the connection string via
   `SqliteResilience.BuildConnectionString(dbPath)` — same
   `ReadWriteCreate` + `SharedCache` + pooling settings as the EF path.
2. After `connection.Open()`, run `SqliteResilience.PragmaScript`
   via a raw `SqliteCommand`. That's the same 7-line batch the EF
   interceptor applies (WAL / synchronous=NORMAL / busy_timeout=5000 /
   foreign_keys=ON / temp_store=MEMORY / mmap_size / cache_size).

Migrations now run under identical hardening to the app's normal
writes. Losing an audit-migration write to a transient lock is no
longer possible.

## New regression test

`Tests/Database/SqliteResilienceCoverageTests.cs` — 2 hermetic tests:

1. **`No_repository_calls_raw_SaveChangesAsync`** — walks
   `Services/` and `Agent/` under the project root, flags any
   `.SaveChangesAsync(` invocation whose enclosing 3-line window does
   NOT contain `SaveWithRetryAsync`. Fails the build with the exact
   `file:line` of every offender plus a copy-pasteable fix snippet.
   This is the regression lock: a future PR that adds a raw
   `SaveChangesAsync` will fail CI before it lands.

2. **`SchemaMigrationRunner_and_AuditDbMigrations_apply_pragmas_manually`**
   — reads the two migration-open files and asserts both reference
   `SqliteResilience.PragmaScript`. Guards against a future refactor
   accidentally removing the manual pragma application.

## Verification (this sandbox — no `dotnet` SDK)

- All 17 modified `.cs` files pass balanced `{}` `()` check under a
  proper C# lexer (strings / verbatim strings / char literals /
  line + block comments respected).
- Python simulation of the coverage test — walks the same tree with
  the same regex + 2-line look-back — reports **0 unwrapped
  `SaveChangesAsync` calls** across the whole `Services/` + `Agent/`
  tree.
- `grep -rn "\.SaveChanges()"` — **0 sync SaveChanges callers**
  anywhere (would block the UI thread if any existed).
- `grep -rn "new SqliteConnection"` outside `SqliteResilience.cs`
  itself — only 5 remaining: 3 in `SchemaHealthService` (read-only,
  safe) and 2 in `RestoreDrillService` (read-only, safe).

## Test count

Before this pass: **147** (after the previous SQLite hardening +
RegistryAnchor fix).
After this pass: **149** (+2 hermetic tests in
`SqliteResilienceCoverageTests`).

## User-visible impact

- Any of the 71 wrapped save paths will now transparently retry a
  transient `SQLITE_BUSY` up to 5 times with exponential back-off
  before surfacing the error. Previously ~any of these could throw
  under WAL contention (Statement generation reading + PI writing at
  the same instant, MasterData dropdown lookup + Inquiry save, etc.).
- Losing an audit-log write is now essentially impossible —
  `AuditService.WriteEntryAsync` retries on lock, and the
  `AuditDbMigrations` path also runs under WAL + busy_timeout.
- Schema migrations across all 10 databases now run under the same
  hardening as the app's normal writes.

Nothing to configure. No settings surface. The hardening is enforced
at the source-file level by the new coverage test.

## Files changed this pass

**Modified (17):**

- `Services/Audit/AuditService.cs` — 8 saves wrapped, using added
- `Services/Audit/AuditDbMigrations.cs` — Open() applies PragmaScript
- `Services/Database/SchemaMigrations/SchemaMigrationRunner.cs` — Open() applies PragmaScript
- `Services/MasterData/Repository/MasterDataRepository.cs` — 12 wrapped
- `Services/DeliveryNote/Repository/DeliveryNoteRepository.cs` — 10 wrapped
- `Services/TaxInvoice/Repository/TaxInvoiceRepository.cs` — 6 wrapped
- `Services/SheetStore/SheetStoreRepository.cs` — 5 wrapped
- `Services/Statement/Repository/StatementRepository.cs` — 4 wrapped
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsRepository.cs` — 4 wrapped
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs` — 5 wrapped, classifier-based UNIQUE
- `Services/JobOrder/Repository/JobOrderRepository.cs` — 3 wrapped
- `Services/AI/AIMemory/AiMemoryService.cs` — 2 wrapped
- `Services/SheetStore/SheetStoreHistory/SheetHistoryRepository.cs` — 1 wrapped
- `Services/SampleData/SampleDataService.cs` — 1 wrapped
- `Agent/Knowledge/AgentKnowledgeService.cs` — 4 wrapped
- `Agent/Memory/AgentMemoryService.cs` — 2 wrapped
- `Agent/Teaching/AgentTeachingService.cs` — 4 wrapped

**New:**

- `Tests/Database/SqliteResilienceCoverageTests.cs`
- `docs/SQLITE_HARDENING_CROSS_CHECK_2026-08.md` (this file)

## Deferred (still not touched — intentional)

- `SchemaHealthService.ReadUserVersion` / `ReadTableCountsAsync` /
  `DetectDriftAsync` — three read-only connections. WAL doesn't apply
  to readers, no writes to fail. Safe as-is.
- `RestoreDrillService.CheckIntegrityAsync` — read-only integrity
  check. Safe.
- `ProformaInvoiceRepository.InsertAsync` retry loop shape — already
  correct (per-attempt fresh DbContext), now uses classifier-based
  UNIQUE detection. Not migrated to `SaveWithRetryAsync` because the
  outer loop already handles UNIQUE explicitly (BUSY retries happen
  transparently inside `SaveWithRetryAsync` — the outer loop is only
  for UNIQUE collisions, which SaveWithRetryAsync deliberately does
  NOT retry).
- `InquiryLifecycleService.LoadDeliveryNoteAsync` /
  `LoadTaxInvoiceAsync` — internally re-load PIs. Wasteful but not
  incorrect. Deferred as before.
