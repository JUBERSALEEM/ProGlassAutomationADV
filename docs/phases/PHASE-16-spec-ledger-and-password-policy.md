# Phase 16 — Specification Ledger Ground Truth + Password Policy

**Applied 2026-09-09 · ERP industrial-hardening pass**

Two unrelated subsystems, one shared principle: **the app reads what it HAS,
not what it HAD** — except for the legitimate restore-stage needs (audit
trail, version history), which are append-only by design and untouched.

---

## Part A — Specifications: counts from saved invoices only

### The rot (verified before changing)

- `PricingEstimatorViewModel.ApplyRateAsync` called `SaveOrUpdateAsync`
  (+1 `UsageCount`) **and** `RecordUsageAsync` (usage row with the draft PI
  id) on every Step-4 APPLY — including drafts that were never saved, plus
  the silent auto-save on Step-4 navigation.
- `MarkAsUsedAsync` had **zero call sites**: nothing ever recorded actual
  Step-5 use.
- `ProformaInvoiceRepository.DeleteAsync` never touched spec data.
- Net effect: `UsageCount` counted APPLY presses, usage rows cited PI
  numbers that were never saved or were deleted, and the Center's KPIs and
  Step-5 dropdown ordering (`UsageCount DESC`) trusted all of it.

### Ground truth, stated once

| Data | Owner | Source |
|---|---|---|
| Library rows (name, rate, catalogue details) | Step 4 (`SaveOrUpdateAsync`) | Estimator input — the Step-5 dropdown must offer a spec pre-save |
| `UsageCount`, `LastUsedAt`, `ProformaId` | `SpecificationLedgerService` | Saved PI snapshots' Step-5 groups only |
| `SpecificationUsages` rows | `SpecificationLedgerService` | Same — one row per (PI, group), price = quoted `BaseRate` |
| `PricePerSqm` (catalogue rate) | Estimator (Step 4) | The ledger never touches it — a fresh estimate must not be clobbered by an old saved rate |

Consequences: 2 PIs × 2 specs = 4 verified uses; a third PI reusing the
same specs increments them; a Step-4-only estimate increments nothing; a
spec on no saved invoice shows count 0 with a "not on any saved invoice"
hint (the row stays — it is the estimator's working library).

### The ledger (`Services/Specifications/SpecificationLedgerService.cs`)

- Runs at startup (heals existing installs on first launch), after every
  invoice write/delete via `SavedInvoicesChanged` (debounced ~1.5s), and on
  demand from the Center's Refresh button.
- Single-flight with re-run coalescing; never throws; audit-logged under
  the new `Specifications` section only when something changed.
- Multiset diff per (PI, type, name, price): a PI quoting the same spec
  twice at different rates keeps both rows; kept rows preserve first-quoted
  `UsedAt`; new rows take the PI's `UpdatedAt` (best-known quote time).
- Purges usage rows for PIs that no longer exist (deleted + never-saved
  drafts) and rows for groups a PI no longer carries. Derived fiction —
  re-derivable, so deleting loses no fact.
- Recreates catalogue rows for specs quoted on saved PIs but missing from
  the catalogue (freehand Step-5 entry, deleted catalogue row), parsing
  colour/thickness from the name for the filters.
- `GetLastActivityAsync()` feeds the Center's new LAST ACTIVITY card (spec
  + PI + company + date). Company/customer/project come from the saved
  invoice row — authoritative, not draft-time context (new `CompanyName`
  column on usages, migration 1004).

### Center changes

- Subscribes to **both** `SpecCatalogNotifier` and `SavedInvoicesChanged`
  with a coalesced reload; Refresh reconciles first.
- Delete is now a verified guard: specs on saved invoices show WHERE
  (PI list) before confirming; saved invoices are unaffected either way
  (snapshots are self-contained), and the ledger re-lists the spec while
  invoices still quote it.
- New locale keys `Specifications.LastActivity` / `Specifications.DraftOnly`
  in all 15 language files (English fallback text).

### Blind-read fix in the same module

`SpecPaneResolver.ResolveAsync` returned stored `PanesJson` verbatim when
present — sizes edited/deleted in Sheet Store afterwards still showed. Now
the stored size wins only when the **current** store still stocks that
colour/thickness in exactly that size (colour/thickness verified too, so a
renamed spec cannot donate its size); otherwise the live match wins, and an
honest "no match" shows when the store has nothing.

`SavedSpecification.SheetId` is noted as write-only / dead-but-harmless
(nothing ever read it); left for schema compatibility.

---

## Part B — Passwords: min 6, no max, industrial policy

- New single home `Services/Security/PasswordPolicy.cs`, enforced by
  `AuditService` (create/change/admin-reset) and mirrored by all four UI
  pre-checks (User Center, first-run, reset, forced rotation).
- Floor 6, **no maximum** (operator freedom; PBKDF2 cost is effectively
  constant in password length and the stored value is fixed-size).
- NIST-aligned: no forced composition; rejects only known-bad —
  blocklist (~120 worst + keyboard walks + product/domain words, incl.
  trailing-digit variants), single-char repeats, full-length sequences,
  username/display-name equality or containment (4+ chars, so `ali` does
  not veto `quality123`). Everything else accepted with a 0–4 strength
  score + one guidance line, shown on live meters in all four screens
  (shared `Views/Shared/PasswordMeterHelper.cs` — one mapping, no drift).
- `PasswordHasher`: `MinLength` aliases the policy; `Verify` now also
  reports `NeedsUpgrade` for below-current PBKDF2 iteration counts, so a
  future iteration raise migrates silently on next sign-in.
- Consistency fixes: `AdminResetPasswordAsync` and
  `IsUsingDefaultPasswordAsync` now match usernames case-insensitively like
  sign-in/change; reset/force windows' stale "minimum 8 characters" hints
  now read `PasswordPolicy.HintText`; the `MinLength == 12` test (failing
  since the 12→6 change) corrected to 6.

Policy governs only passwords being chosen now — `Verify` never consults
it, so no existing credential stops working.

---

## Part C — Orphan-read audit (all modules)

| Site | Verdict |
|---|---|
| Spec counts/history/provenance | **Fixed** (ledger) |
| Spec pane sizes from stored JSON | **Fixed** (live-validated) |
| Statement aggregator + workflow engine | Clean — null-checks missing PIs, warns/continues |
| AI context builder | Clean — reports "not found" |
| Tax Invoice import, JO convert, DN detail | Clean — error/return on missing source |
| `DependencyGuard` | Clean — blocks deletes with live dependents |
| PI-embedded company/client snapshots | Clean — MasterData deletes cannot corrupt documents |
| MasterData `UsageCount` | Clean — bumps only on deliberate editor saves (honest) |
| Audit trail / version history | Exempt — legitimate restore-stage need, append-only |
| Dashboard / OrderReport | Clean — live reads, null-checked, no stale caches |

## Tests

- `Tests/Security/PasswordPolicyTests.cs` — floor/ceiling, blocklist,
  repeats, sequences, identity, scoring, hint pinning (14 cases).
- `Tests/Security/PasswordHasherTests.cs` — stale `== 12` fixed to 6 +
  single-source assertion.
- `Tests/Specifications/SpecificationLedgerTests.cs` — 8 hermetic
  behavioural tests against real SQLite (counts, third-PI increment, edit,
  delete, freehand recreate, corrupt snapshot, last activity ×2).
- `Tests/Specifications/SpecificationLedgerContractTests.cs` — 8
  source-level architecture pins (no Step-4 usage writes, no repo bumps,
  ledger wiring, Center signals, pane validation, policy single-home).
- Shared non-parallel `HermeticDb` xUnit collection; the two pre-existing
  folder-redirecting suites joined it (fixes their latent mid-run race).
