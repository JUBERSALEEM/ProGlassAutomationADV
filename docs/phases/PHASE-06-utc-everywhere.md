# Phase 6 — UTC Everywhere

**Severity: Major · Estimate: 3-4 days · Prerequisite: green build**

## Evidence

Production code contains **245** uses of `DateTime.Now` and **151** of
`DateTime.UtcNow`. Both appear inside the same subsystems:

- `LicenseVerifier` compares expiry in UTC (`DateTime.UtcNow`)
- `SheetStoreRepository` stamps `LastUpdated = DateTime.Now`
- `VatCalculator.ValidateInvoice` checks `issueDate > DateTime.Now.AddDays(1)`
  against a stored date

So a stored value's meaning depends on which code path wrote it.

## Why it matters here specifically

- **Aging and overdue** reports compute `now - CreatedAt`. If the two sides use
  different bases, every boundary case is wrong by the local UTC offset — hours
  of error on a report measured in days.
- **Machine timezone or clock changes** rewrite the meaning of historical rows.
- **DST** gains or loses an hour twice a year in every date-range filter. "UAE
  has no DST" is not a durable answer for a product that already resolves
  country at runtime via `GeoService`.
- **Shared LAN data folder**: two machines in different timezones writing to the
  same SQLite file produce interleaved local times.

## Steps

### 6.1 — Set the rule

> All persisted `DateTime` values are UTC. All user-facing display converts to
> local. No `DateTime.Now` reaches a database column.

Write it into `docs/` next to the money rounding policy — both are the kind of
rule that erodes silently without a stated home.

### 6.2 — Find the persistence boundaries

Not every `DateTime.Now` is wrong. Formatting a timestamp for display is fine.
Classify each of the 245:

- **written to a database column** → must become `UtcNow`
- **compared against a stored value** → must match the stored basis
- **display only** → leave, and convert explicitly with `.ToLocalTime()`

```bash
grep -rn "DateTime\.Now" --include=*.cs . | grep -v obj | grep -v Tests
```

Work module by module. Do not do this in one pass across 245 sites — the failure
mode is converting a write without converting its readers, which is worse than
the inconsistency you started with.

### 6.3 — Migrate existing data

Stored local times cannot be converted reliably after the fact, because the
original offset is not recorded. Options, in order of preference:

1. Leave existing rows, add a `DateTimeKind`-aware read path that treats
   pre-migration rows as local and post-migration rows as UTC, keyed on a
   schema version.
2. Convert once at migration using the machine's offset **at migration time**,
   and record that this was approximate.

Either way, **state which you chose in the migration's own comment**. A future
reader looking at a 2024 date needs to know which basis it is in.

### 6.4 — Make the column type say so

EF Core maps `DateTime` to SQLite `TEXT`. Use `DateTimeOffset` for new columns,
or store UTC and document it in `OnModelCreating`. Add a model-level convention
so a new `DateTime` property cannot be added without a decision.

### 6.5 — Ratchet

Add an architecture test in the style of `PaginationRatchetTests`: count
`DateTime.Now` in `Services/**/Repository/**` and fail if it grows. Allow-list
the current count, remove entries as modules are converted.

## Acceptance criteria

- [ ] Zero `DateTime.Now` in any repository write path.
- [ ] Every date-range filter compares like with like.
- [ ] A ratchet prevents regression.
- [ ] The migration strategy is recorded in the migration itself.

## Must not change

- Display formatting. Users must keep seeing local time.
- `LicenseVerifier`'s UTC comparisons — already correct.
