# Phase 6 — UTC Everywhere

**Severity: Major · Estimate: 3-4 days · Prerequisite: green build**

## Status — the safe subset APPLIED 2026-09-09

| | |
|---|---|
| 224 sites classified by what the value is used for | **done** |
| In-memory interval measurements → UTC | **done** (5 sites, 2 pairs) |
| Ratchet holding the local-time surface | **done** — `Tests/Architecture/LocalTimeRatchetTests.cs` (3 tests) |
| 89 persisted sites migrated | **NOT done — deliberately** |

### "Convert 245 sites" was the wrong framing

Classified every production `DateTime.Now` by what the value is actually used
for — 224 occurrences in 111 files once comment lines are excluded:

| use | count | verdict |
|---|---|---|
| assigned to a persisted field | 89 | the real migration; needs a data plan |
| passed as an argument | 60 | case by case |
| formatted for display or a filename | 59 | **correct as-is — do not convert** |
| compared or derived | 14 | timezone-sensitive; the in-memory ones are fixed |
| other | 7 | case by case |

The 59 display uses are not defects. A file named `20260909_141502.mp4` and a
log line reading "Started at 14:15:02" should show the operator's own clock.
Converting those to UTC would be a regression dressed as a fix, and it is what a
blind find-and-replace would have done to all 224.

### What was converted, and why it is safe

Two pairs of interval measurements — five sites — where a `DateTime` is only
ever subtracted from another `DateTime` inside the same process:

- `AiContextBuilder._cacheExpiry` (3 sites). Local time meant a spring-forward
  served stale AI context for an hour longer than `CacheSeconds`, and a
  fall-back discarded a good cache an hour early.
- `StatementGeneratorViewModel` generation stopwatch (2 sites). A DST
  transition during a long generation moved the reported seconds by exactly
  3600.

Both pairs were converted **together**. Converting only the read or only the
write would compare a UTC instant against a local one and be wrong by the whole
UTC offset — considerably worse than the bug being fixed. Neither value is
persisted, transmitted or displayed as a date, so no stored data is affected.

`LicenseActivatedDialog.cs:36` was left alone on purpose: it pairs
`DateTime.Now` with `EffectiveExpiresAt.ToLocalTime()`, so local is correct.
The log timestamp in `StatementGeneratorViewModel.Generate.cs:134` was left for
the same reason, and `LocalTimeRatchetTests` asserts it stays — so nobody
"fixes" it later.

### Why the 89 persisted sites are not touched

Existing rows hold local time. Converting the write side without migrating the
stored rows splits the timeline in half, and the symptom is not an error — it is
a report filtered on "this month" that silently gains or loses a day at the
boundary. That needs a migration written against a real database, plus a
decision about how to interpret rows already on customers' machines.

`LocalTimeRatchetTests` pins the baseline at 224 and fails in both directions,
so the surface cannot grow while that plan is written and cannot silently come
back afterwards.


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
