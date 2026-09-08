# Phase 1 — Transactions and Optimistic Concurrency

**Severity: Critical · Estimate: 4-6 days · Prerequisite: green build**

## Why this is first

Two operators using this app at the same time can silently corrupt stock
quantities, and no business write in the codebase is atomic. Everything else in
this list is a defect a single user can hit; this one only appears in production,
under load, after the customer has trusted the numbers for a while.

## Evidence

**No transactions.** `grep -rn "BeginTransaction"` across 1,285 C# files returns
one hit — `Services/Database/SchemaMigrations/SchemaMigrationRunner.cs`. Not one
business write path is transactional.

**No concurrency control.** No `IsRowVersion`, `[Timestamp]`, or
`IsConcurrencyToken` exists anywhere.

**The racy write.** `Services/SheetStore/SheetStoreRepository.cs:161-176`:

```csharp
using var db = new AppDbContext();
var sheet = await db.GlassSheets.FindAsync(sheetId);   // READ
if (sheet.Balance < quantity) throw ...;               // CHECK
sheet.Used += quantity;                                // MODIFY
await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());  // WRITE
```

`BuyStockAsync` at line 142 is the same shape against `Stock`.

**Multi-user is a shipped scenario.** `AppDataRoot.ConfigureFrom(Config)`
supports a shared LAN data folder; `UserPresenceService` exists to show
colleagues on the same network. This is not a single-seat app.

**The app already knows writes diverge.** `TaxInvoiceDnReconciler` runs at
startup to heal tax-invoice→delivery-note links "left broken by a crash / failed
write". That is cleanup for a problem transactions would prevent.

## Steps

### 1.1 — Add optimistic concurrency to the entities that are read-modify-written

Start with `GlassSheet`. Add a concurrency token:

```csharp
[Timestamp]
public byte[] RowVersion { get; set; }
```

Configure it in `AppDbContext.OnModelCreating`. **SQLite has no native
`rowversion`** — EF Core's SQLite provider requires a trigger-based approach or a
manually maintained integer. The reliable pattern here:

```csharp
public long Version { get; set; }
// OnModelCreating:
entity.Property(e => e.Version).IsConcurrencyToken();
// and on every save: entity.Version = existingVersion + 1;
```

Do the same for `SavedJobOrder`, `SavedDeliveryNote`, `SavedTaxInvoice`,
`InventoryItem` and `SavedStatement` — every entity with a quantity or money
field that is read, modified and written.

**This needs a schema migration.** Read `Services/Database/SchemaMigrations/`
first and follow the existing runner's conventions exactly. Do not let EF
recreate the database.

### 1.2 — Convert the racy writes to compare-and-swap

`UseStockAsync` becomes:

```csharp
using var db = new AppDbContext();
using var tx = await db.Database.BeginTransactionAsync(ct);

var sheet = await db.GlassSheets.FindAsync(new object[] { sheetId }, ct);
if (sheet == null) return;
if (sheet.Balance < quantity) throw new InvalidOperationException(...);

sheet.Used += quantity;
sheet.Version++;
sheet.LastUpdated = DateTime.UtcNow;

try
{
    await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync(ct));
    await tx.CommitAsync(ct);
}
catch (DbUpdateConcurrencyException)
{
    // Another operator moved this sheet. Do NOT retry blindly — the
    // availability check must run again against fresh data.
    throw new ConcurrencyConflictException(sheetId);
}
```

**The retry must re-read.** `SaveWithRetryAsync` retries `SQLITE_BUSY` on the
same context, which is correct for a lock. It must never be used to retry a
concurrency conflict — retrying a stale delta is how the lost update happens in
the first place.

Better still, for stock specifically: express it as a single atomic UPDATE and
let the database do the arithmetic:

```csharp
await db.GlassSheets
    .Where(s => s.Id == sheetId && (s.Stock - s.Used) >= quantity)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Used, x => x.Used + quantity), ct);
```

If `RowsAffected == 0`, the availability check failed — report it. No read, no
window, no race.

### 1.3 — Wrap multi-write operations in a unit of work

`ViewModels/SheetStore/SheetStoreViewModel.ImportCsvAsync`,
`PersistNewSheetAsync`, and every equivalent write sheet + history + audit.

These span **different SQLite files**, so a single `BeginTransaction` will not
cover them. Two options, in order of preference:

1. **Move history into the same database as its subject.** Stock history belongs
   in `sheetstore.db`. Then one transaction covers both.
2. **Where that is impossible, use an outbox.** Write the intended side-effect as
   a row in the primary transaction, and let a dispatcher apply it. This is what
   makes the operation recoverable without a reconciler guessing.

### 1.4 — Reconsider `SqliteCacheMode.Shared`

`SqliteResilience.BuildConnectionString` uses shared cache for local databases.
Shared cache introduces **table-level** locking inside a single process, which
surfaces as `SQLITE_LOCKED` (code 6) — the same code the retry helper loops on.
WAL already provides the concurrency benefit. Measure with it set to
`SqliteCacheMode.Default` and keep whichever wins; the comment in that file
claims shared cache "avoids duplicated buffers", which is a memory argument, not
a correctness one.

### 1.5 — Retire what transactions make unnecessary

Once 1.3 lands, `TaxInvoiceDnReconciler`'s startup sweep should become a
**diagnostic** that reports divergence rather than a mechanism that silently
heals it. A reconciler that quietly repairs data hides the bug that caused the
divergence. Keep it, but log loudly and count repairs in the health report.

## Acceptance criteria

- [ ] Every entity with a quantity or money field carries a concurrency token.
- [ ] `UseStockAsync` and `BuyStockAsync` cannot lose an update under
      concurrent execution — proven by a test that runs both against the same
      row and asserts the final quantity.
- [ ] No stock value can go negative. Add a `CHECK (Used <= Stock)` constraint
      if the schema migration allows it; that is the last line of defence and it
      belongs in the database, not only in C#.
- [ ] Sheet add / CSV import / stock use are atomic: either the subject row and
      its history row both exist, or neither does.
- [ ] `SaveWithRetryAsync` is never used to retry a
      `DbUpdateConcurrencyException`.
- [ ] `TaxInvoiceDnReconciler` reports repairs instead of only performing them.

## Must not change

- `SqliteResilience`'s pragma scripts. They are correct, including the separate
  network-share script. Changing `journal_mode` on a deployed database is a
  one-way operation.
- `ReadOnlyGate.Demand(...)` calls at the top of every write. They are the
  licensing boundary.
- The `SqliteErrorClassifier` categories.

## Verification

```bash
# no business write left non-transactional
grep -rn "SaveChangesAsync" --include=*.cs Services/ | grep -v obj
# every one must be inside a transaction or be a single-statement ExecuteUpdate

grep -rn "BeginTransaction" --include=*.cs Services/ | grep -v obj
# should now be many, not one

grep -rn "IsConcurrencyToken\|\[Timestamp\]" --include=*.cs . | grep -v obj
```

Add `Tests/Concurrency/StockConcurrencyTests.cs` running N parallel issues
against one sheet and asserting the final balance. That test is the whole point
of this phase — write it first and watch it fail.
