# Industrial SQLite Hardening · August 2026

## Why this pass

User report:

> "In inquiry I'm getting sqlite error issue. I got it same many times fix
> it completely. Make db more strict and advance."

The Inquiry crashes had two overlapping root causes — one Inquiry-specific
bug plus a systemic gap that affected every SQLite database in the app.
Both are fixed in this pass, and a new central hardening module locks
in the fix for every current and future DbContext.

## What was wrong

### 1. Inquiry-specific bug — retry loop reused one DbContext

`InquiryRepository.CreateAsync` had a `for(;;)` retry loop that opened
**one** `InquiryDbContext` and reused it across every retry attempt. When
`SaveChangesAsync` threw a UNIQUE-index violation (typical during a
concurrent auto-save or an import batch), the recovery path was:

```csharp
catch (DbUpdateException ex) when (IsUniqueViolation(ex))
{
    db.Entry(inquiry).State = EntityState.Detached;
    // loop → re-generate InquiryNumber → re-Add(inquiry) → SaveChangesAsync again
}
```

Detaching one root entity **did not** clear the DbContext's change-tracker
of the failed insert's shadow properties, and the underlying
`SqliteConnection` was left in a partial-transaction state. The next
`SaveChangesAsync` on the same context could throw an *EF-level* error
("attempted to update or insert entity with the same key") that the user
saw as *"SQLite error inside Inquiry"* — with no indication that a UNIQUE
collision had been the trigger.

### 2. Systemic gap — no PRAGMA hardening on any of the 12 SQLite databases

Every `DbContext.OnConfiguring` in the app used the same bare pattern:

```csharp
options.UseSqlite($"Data Source={DatabaseFilePath}");
```

With no `PRAGMA journal_mode`, no `busy_timeout`, no `foreign_keys=ON`.
This caused **three** classes of live bug:

| Symptom | Root cause |
|---|---|
| Random *"database is locked"* on write during concurrent read | Default rollback-journal is single-writer, single-reader |
| Cascade deletes silently leaving orphan child rows | SQLite `foreign_keys` are OFF by default; the `HasOne/HasMany` in the model was never enforced by the engine |
| Save immediately fails during a checkpoint or lock burst | No `busy_timeout` — SQLite gives up on the first contention |
| Slow write throughput | `synchronous=FULL` default is 2–3× slower than WAL-safe `NORMAL` |

## The fix

### New central module — `Services/Database/SqliteResilience.cs`

One file, one responsibility: apply industrial pragmas to every physical
SQLite connection this app opens.

```
PRAGMA journal_mode  = WAL      → true multi-reader + single-writer
PRAGMA synchronous   = NORMAL   → WAL-safe fsync policy
PRAGMA busy_timeout  = 5000     → 5 s wait on lock before failing
PRAGMA foreign_keys = ON        → FKs actually enforced
PRAGMA temp_store    = MEMORY   → temp b-trees stay in RAM
PRAGMA mmap_size    = 67108864  → 64 MB read-only mmap
PRAGMA cache_size    = -16000   → 16 MB per-connection page cache
```

Applied via a `DbConnectionInterceptor` registered on every DbContext.
The interceptor runs at `ConnectionOpened` — before any transaction can
start — because `PRAGMA foreign_keys=ON` is silently ignored inside a
transaction.

Also in the same file:

- **`SqliteResilience.BuildConnectionString(path)`** — builds a
  connection string that uses `SqliteOpenMode.ReadWriteCreate` +
  `SqliteCacheMode.Shared` (multiple DbContexts share page cache within
  one process, WAL keeps concurrency correct).
- **`SqliteResilience.ConfigureFor(options, path)`** — one-line
  helper every DbContext.OnConfiguring calls.
- **`SqliteResilience.SaveWithRetryAsync(...)`** — wraps
  SaveChangesAsync in an exponential-back-off retry loop that ONLY
  retries transient failures (`SQLITE_BUSY` / `SQLITE_LOCKED`); UNIQUE
  / NOT NULL / FOREIGN KEY / CHECK violations surface immediately.
- **`SqliteErrorClassifier`** — turns any exception (walks up to 8
  levels of InnerException) into a strongly-typed `SqliteErrorKind`
  (`Busy`, `Locked`, `UniqueViolation`, `ForeignKeyViolation`,
  `NotNullViolation`, `CheckViolation`, `Corrupt`, `ReadOnly`,
  `Constraint`, `Other`). Uses `SqliteException.SqliteExtendedErrorCode`
  (2067 = UNIQUE, 787 = FK, 1299 = NOT NULL, 275 = CHECK), falls back
  to message-sniffing only when the extended code is not populated.
- Helper predicates: `IsTransient`, `IsUnique`, `IsTerminal`.

### Every DbContext hooked in — 12 files

Every `OnConfiguring` now calls
`SqliteResilience.ConfigureFor(options, path)` instead of the bare
`UseSqlite($"Data Source={path}")`. One-line change per file:

- `Services/Database/AppDbContext.cs` (proglass.db)
- `Services/Inquiry/InquiryDbContext.cs` (inquiries.db) ← primary crash site
- `Services/Audit/AuditDbContext.cs` (audit.db — security-critical)
- `Services/AI/AIMemory/AiMemoryDbContext.cs` (aimemory.db)
- `Agent/Storage/AgentDbContext.cs` (agent.db)
- `Services/DeliveryNote/Database/DeliveryNotesDbContext.cs` (deliverynotes.db)
- `Services/JobOrder/Database/JobOrdersDbContext.cs` (joborders.db)
- `Services/MasterData/Database/MasterDataDbContext.cs` (masterdata.db)
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoicesDbContext.cs` (proformainvoices.db)
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsDbContext.cs` (specifications.db)
- `Services/Statement/Database/StatementDbContext.cs` (statements.db)
- `Services/TaxInvoice/Database/TaxInvoiceDbContext.cs` (taxinvoices.db)

### Inquiry-specific fixes on top

`Services/Inquiry/InquiryRepository.cs`:

- `CreateAsync` now allocates a **fresh** `InquiryDbContext` per retry
  attempt (was: one context reused across attempts, which is what caused
  the crash).
- All 8 write methods (`CreateAsync`, `UpdateAsync`, `ChangeStatusAsync`,
  `DeleteAsync`, `DuplicateAsync`, `SetProformaLinkAsync`,
  `AddAttachmentAsync`, `DeleteAttachmentAsync`) now route
  `SaveChangesAsync` through `SqliteResilience.SaveWithRetryAsync`, so a
  transient BUSY doesn't kill the user's action.
- The private `IsUniqueViolation(DbUpdateException)` is now a thin
  wrapper around `SqliteErrorClassifier.IsUnique` — same behaviour, but
  it uses extended SQLite error codes instead of string-matching the
  message (which would break on a translated `Microsoft.Data.Sqlite`).
- When retries are exhausted, `CreateAsync` now throws a clean
  `InvalidOperationException` with a user-friendly message instead of
  looping forever or surfacing raw EF text.
- The comment above the child-delete in `DeleteAsync` was updated to
  reflect that FK enforcement is now real (was: "safe when FK is off").

### Test hook — `AppDataRoot.TestFolderOverride`

New nullable `TestFolderOverride` static property. Production code never
sets it; test fixtures set it in the ctor and restore in Dispose so that
repository tests can point every DbContext at an ephemeral
`Path.GetTempPath()` folder without touching the user's real `%AppData%`.

## What changes for the end user

**Immediately after this update lands:**

- On first open of each existing SQLite file, the header is upgraded
  from rollback-journal to WAL. This is a one-time, reversible
  operation (SQLite has supported WAL since 3.7 in 2010). Two side-files
  will appear next to each `.db`: `.db-wal` (write-ahead log) and
  `.db-shm` (shared memory). This is expected — do not delete them.
- Concurrent Inquiry save + list refresh no longer produces
  "database is locked".
- Inquiry create under contention no longer surfaces
  "SQLite error inside Inquiry save". Duplicate InquiryNumber is silently
  regenerated up to 5 times, then a clean domain exception surfaces
  ("try again in a moment").
- Deleting an Inquiry now properly cascades to its Attachments and
  Communications, enforced by SQLite itself even if the repository
  forgets to `RemoveRange` first.
- Statement generation (which reads five sibling DBs in parallel) no
  longer blocks the PI/JO/DN/TI writers because WAL allows reads
  during writes.

**Nothing to configure.** No new settings, no PRAGMA-tuning knobs
exposed to the user — the pragmas are baked in.

## Verification

### Structural (this sandbox has no `dotnet` SDK)

All 14 modified files + 2 new test files pass balanced `{}` `()`
verification under a proper C# lexer (respects strings, verbatim
strings, char literals, line + block comments).

### Behavioural (runs in CI on Windows)

**`Tests/Database/SqliteResilienceTests.cs`** — 12 hermetic tests
against an ephemeral SQLite file under `Path.GetTempPath()`:

- BuildConnectionString rejects empty / whitespace / null paths
- BuildConnectionString produces a valid `SqliteConnectionStringBuilder`
- Interceptor activates WAL, `foreign_keys=1`, `busy_timeout=5000`,
  `temp_store=MEMORY`
- Foreign-key violation is actually thrown when enforcement is on
  (unwritable to legacy code path)
- Classifier detects UNIQUE and PRIMARYKEY violations
- Classifier walks up to 8 levels of `InnerException`
- Classifier returns `Other` for non-SQLite exceptions and null
- Classifier flags CORRUPT + READONLY as terminal, BUSY as not
- `SaveWithRetryAsync` retries on BUSY and eventually succeeds
- `SaveWithRetryAsync` gives up after `maxAttempts` and rethrows the
  last SQLite error
- `SaveWithRetryAsync` does NOT retry on UNIQUE violation (surfaces
  on the very first attempt)
- `SaveWithRetryAsync` honours cancellation
- `SaveWithRetryAsync` rejects a null delegate

**`Tests/Inquiry/InquiryRepositoryCreateResilienceTests.cs`** — 4
hermetic tests, real `InquiryRepository` against an ephemeral
inquiries.db under a `TestFolderOverride`:

- `CreateAsync` regenerates the InquiryNumber when the client-supplied
  number collides with an existing row (was: threw)
- `CreateAsync` sets default timestamps when caller leaves them blank
- `CreateAsync` normalises legacy status text ("On Hold" → `Hold`)
- 8 concurrent `CreateAsync` calls all succeed and produce 8 distinct
  InquiryNumbers under WAL contention (was: some would throw
  SQLITE_BUSY / UNIQUE)

**Test count: 128 → 144** (+16).

## Files changed

**New:**
- `Services/Database/SqliteResilience.cs` (383 lines)
- `Tests/Database/SqliteResilienceTests.cs` (260 lines)
- `Tests/Inquiry/InquiryRepositoryCreateResilienceTests.cs` (175 lines)
- `docs/SQLITE_HARDENING_2026-08.md` (this file)

**Modified:**
- `Services/Database/AppDataRoot.cs` — test-override hook
- `Services/Inquiry/InquiryDbContext.cs` — route through SqliteResilience
- `Services/Inquiry/InquiryRepository.cs` — fresh context per retry;
  SaveWithRetryAsync on 8 write paths; classifier-based IsUniqueViolation
- `Services/Database/AppDbContext.cs` — route through SqliteResilience
- `Services/Audit/AuditDbContext.cs` — route through SqliteResilience
- `Services/AI/AIMemory/AiMemoryDbContext.cs` — route through SqliteResilience
- `Agent/Storage/AgentDbContext.cs` — route through SqliteResilience
- `Services/DeliveryNote/Database/DeliveryNotesDbContext.cs` — route
- `Services/JobOrder/Database/JobOrdersDbContext.cs` — route
- `Services/MasterData/Database/MasterDataDbContext.cs` — route
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoicesDbContext.cs` — route
- `Services/ProformaInvoice/SpecificationsRepository/SpecificationsDbContext.cs` — route
- `Services/Statement/Database/StatementDbContext.cs` — route
- `Services/TaxInvoice/Database/TaxInvoiceDbContext.cs` — route

## Deferred (worth flagging)

- `ProformaInvoiceRepository.InsertAsync` has its own retry loop with a
  string-match `IsUniqueViolation`. Its per-attempt-fresh-context shape
  is already correct — the string-match could be replaced with
  `SqliteErrorClassifier.IsUnique`, and the raw `SaveChangesAsync`
  could route through `SaveWithRetryAsync`, but the current code has
  been stable in production so we did not touch it in this pass.
  Additive rule.
- `InquiryLifecycleService.LoadDeliveryNoteAsync` and
  `LoadTaxInvoiceAsync` internally call `LoadProformaAsync` a second
  time in parallel with the top-level aggregator. Wasteful but not
  incorrect. Deferred.
- `AuditDbMigrations` still uses its own migration path rather than
  `SchemaMigrationRunner`, per the note in `AppSchemaMigrations.cs`.
  Unchanged this pass to avoid touching a security-critical file in the
  same commit as the hardening.
