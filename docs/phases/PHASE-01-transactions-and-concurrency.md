# Phase 1 — Transactions and Optimistic Concurrency

**Severity: Critical · Estimate: 4-6 days · Prerequisite: green build**

## Status — the glass-sheet race APPLIED 2026-09-09

| | |
|---|---|
| `SheetStoreRepository.UseStockAsync` | **fixed** — guarded atomic UPDATE |
| `SheetStoreRepository.BuyStockAsync` | **fixed** — atomic UPDATE |
| `SqliteResilience.ExecuteWithRetryAsync` | **added** — same retry policy for bulk writes |
| Regression tests | **added** — `Tests/Database/StockConcurrencyTests.cs` (6 tests, real SQLite) |
| `InventoryItemRepository` (consumables) | **NOT fixed — see below** |
| Concurrency tokens / `RowVersion` | **not started** |
| Transactions across multi-entity writes | **not started** |

### What the fix is

The balance check used to run in C# between the read and the write. It now runs
inside the UPDATE's WHERE clause, so SQLite evaluates the guard and applies the
increment under one write lock:

```sql
UPDATE GlassSheets SET Used = Used + @q, LastUpdated = @now
WHERE Id = @id AND Stock - Used >= @q
```

Zero rows affected means the guard refused. The method then re-reads *only* to
choose between the two outcomes the old code produced — a missing sheet returns
silently, a short sheet throws — because callers depend on the throw
(`UpdateSheetStockTool`, `SheetStoreDashboardViewModel`). The decision about
whether to write was already made atomically, so that read cannot corrupt
anything.

`Balance` is a computed property (`Stock - Used`), not a column, so the guard is
written as the arithmetic EF translates it to.

`GlassSheet.Stock` and `Used` are `int` on INTEGER-affinity columns. SQL-side
integer arithmetic is exact; there is no float anywhere in this path.

### Why the consumables were deliberately left alone

`InventoryItemRepository.ReceiveAsync` and `ConsumeAsync` have the identical
race, and the same fix looks obvious. It is wrong there.

`InventoryItem.Stock` and `Used` are declared **TEXT affinity** (the D-6 block in
`AppDbContext.OnModelCreating`) so quantities like 45.55 linear metres are
stored and read as exact .NET decimals and never touch a binary float. Moving
the addition into SQL moves it into SQLite's REAL type, and the result is
converted back to TEXT with **15 significant digits**. Measured on SQLite
3.40.1:

| value in | value out | |
|---|---|---|
| `45.55` | `45.55` | exact |
| `1234567.89` | `1234567.89` | exact |
| `12345678901234.56` | `12345678901234.6` | **lost** |
| `9999999999999999.99` | `1.0e+16` | **lost** |

Accumulation is fine for realistic stock — a million additions of `0.07` came
back exactly `69998.81` — but the schema declares `18,2`, which is sixteen
significant digits, so the column can legally hold a value this path would
silently corrupt. That is a worse failure than the race it fixes.

The correct fix for consumables is a transaction around the read-modify-write
with a retry on conflict, which keeps the arithmetic in `decimal`. That needs a
build and a real database. Both methods now carry this reasoning inline so the
next reader does not apply the SQL fix by analogy.

### What is still open

Concurrency tokens (`RowVersion`) and transactions across multi-entity writes
are untouched. Both need a schema migration and a green build, and doing either
blind is how a working application stops starting.

Note one nuance the original finding overstated: EF Core already wraps a
multi-statement `SaveChangesAsync` in a transaction automatically, so a single
`SaveChanges` touching several entities *is* atomic. `SheetHistoryRepository.ReverseAsync`
relies on that and is correct. The genuine gap is read-modify-write across
separate contexts — the lost update — which is what this phase actually fixes.

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
