# Phase 12 — One Home for Every Rule

**Severity: Moderate · Estimate: 3 days · Prerequisite: green build**

## Status — 12.3 APPLIED 2026-09-09; 12.1 was already done

| | |
|---|---|
| 12.1 Proforma status canonical home | **already existed** — see the correction below |
| 12.2 `DeliveryTypeValues` | **already done** |
| 12.3 sweep for the next one | **done** — one real duplicate found and fixed |
| Fix | `Models/SheetStore/GlassSheetLowStock.cs` + 4 tests |

### 12.1 was wrong — the canonical home already exists

This phase claimed Proforma status has no canonical home. It does:
`Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs`, with all six
constants (`Draft`, `Sent`, `Approved`, `Rejected`, `Expired`, `Cancelled`), an
`All` list and a `Normalize` method. `StatusLiteralRatchetTests` already lists
it as the Proforma home and pins the raw-literal baseline at 60.

The six-value set was confirmed against the authority rather than by counting
literals: `Services/Workflow/StatusTransitions.cs:70-75` is the transition
table. That mattered — a naive count of status-looking strings across the
Proforma directories also returns `"Confirmed"` (6) and `"Pending"` (8), which
belong to other entities. Acting on the count would have invented two statuses.

### 12.3 — the sweep found one real duplicate

Scanned every `const` with a literal value, grouped by name, and looked for the
same name declared in two or more files with different values: **17 candidates**.
Fifteen are false positives — the same name naming different things:
`MaxRows` per agent tool, `ProviderKey` where each provider names itself,
`FontSizeBody` at 10pt for Excel and 10f for PDF, `"Sheet Store"` as an audit
display label against `"SheetStore"` as a wallpaper module key.

One was real:

```
Agent/Tools/Read/GetInventoryTool.cs:31              LowStockThreshold = 2
Services/Dashboard/Core/DashboardMetricsService.cs:60            = 5
```

Same rule, same `GlassSheet` column, two values. A sheet type holding 3 or 4
sheets was counted as low stock on the Overview dashboard and reported as fine
by the AI agent, asked the same question from the same data at the same moment.
No exception, nothing logged — exactly the defect class the other five fixes
came from.

`Models/SheetStore/GlassSheetLowStock.cs` is now the single home and both read
it. **The canonical value is 5**, because the dashboard is the number an
operator has been acting on and its own comment records that 5 matched what the
Overview already used; unifying on 2 would have silently shrunk a count someone
trusts. Moving a reorder alert in the earlier-warning direction is also the safe
direction. The dashboard's figure does not change; the agent's does.

`Tests/SheetStore/GlassSheetLowStockTests.cs` (4 tests) pins the value, the
inclusive `<=` boundary, and — the part that holds — fails if either consumer
stops reading the canonical constant or a new file declares its own. Proven to
fail: restoring a local `= 5` in the dashboard trips it.

`InventoryItem` is untouched. Consumables have a per-item `ReorderLevel` and
their own `IsLowStock`, which is a better mechanism than a constant; this fix is
specific to `GlassSheet`, which has no per-sheet reorder level.

### Found and deliberately not changed

`LicenseFormat.FileExtension = ".proglasslicense"` (the contract library)
against `LicenseSigner.FileExtension = ".lic"` (the runtime signer, whose
comment says the file picker and the generator both use it). Two declarations of
one thing, but a licence file's extension is cosmetic — the payload is a JSON
envelope — and changing the filter would stop users selecting licence files
they already have on disk. That needs a decision about existing installations,
not a blind edit.


## The defect class behind most of what was fixed in 2026-09-08

A status, threshold or rate declared more than once — two spellings, two values,
or two names — with no single home and nothing that errors when they disagree.

Six instances were found and fixed. Each one produced a *silently wrong number*,
not a crash:

- Job Order status: `"InProduction"` (enum) vs `"In Production"` (older rows),
  both live in the database. The metrics service matched one spelling, so every
  recently-written job was excluded from the In Production count.
- Delivery Note: `DeliveryNoteStatus` has no `Dispatching` member, yet
  `DeliveryNoteMapper` writes it. Notes landed in neither bucket and
  `Preparing + Dispatched ≠ TotalNotes`.
- Target-pricing tolerance declared eight times with two values (`0.005m` and
  `0.05m`).
- VAT hardcoded in three places, one named *divisor* against a canonical
  *multiplier*.
- Master DN filter present in every repository query and absent from the
  dashboard metrics service, in **two** places.

## The pattern that fixed them

Each fix had the same three parts, and all three are needed:

1. **A canonical home** — `JobOrderStatusValues`, `DeliveryNoteStatusValues`,
   `TargetMarginTolerance`, `VatCalculator`, `SearchPattern`.
2. **Every consumer routed through it.**
3. **A ratchet test** that fails in both directions — on a new offender *and* on
   a stale allow-list entry. Four exist:
   `StatusLiteralRatchetTests`, `TargetMarginToleranceTests`,
   `VatRateSingleSourceTests`, `PaginationRatchetTests`, plus the new
   `MasterDnFilterParityTests`.

## Remaining work

### 12.1 — Proforma status has no canonical home

Job Order and Delivery Note each have a `*StatusValues` class. Proforma does
not. Its status literals are scattered, and `ReportingDataService` keeps its own
both-spelling checks — correct today, but it is the same shape that broke twice
already.

Create `Models/ProformaInvoice/Common/ProformaStatusValues.cs` following the
existing two exactly, then ratchet it.

### 12.2 — `DeliveryTypeValues` had zero call sites

The canonical home for `"Original"` / `"Dummy"` / `"Rework"` / `"Return"` existed
while `DnDashboardViewModel` re-declared all four as private constants and
`DeliveryTypeMeta` re-declared them as dictionary keys. One consumer was added
in 2026-09-08; the others remain.

Add a ratchet forbidding those four literals outside `DeliveryTypeValues`, in the
same style as the status-literal test.

### 12.3 — Sweep for the next one

The reliable way to find these is not to read everything. It is to list every
`public const string` whose value also appears as a literal elsewhere:

```bash
grep -rhn 'public const string [A-Za-z]* = "[^"]*"' --include=*.cs Models/ Services/
```

Take each value and grep for the bare literal. Any hit outside the declaring
file is a candidate. This is how the eight tolerance declarations were found
after an initial count of four — **count the occurrences before concluding you
found them all.**

### 12.4 — A note on classification

Not every duplicated literal is a defect. Before fixing, ask what the thing is:

- **master data vs transaction data** — a table bounded by variety stocked does
  not need the same treatment as one bounded by transaction volume
- **display string vs stored value** — a label that happens to match a status is
  not a second declaration of that status
- **latent fault vs bug** — a literal that disagrees today but is never compared
  is a latent fault. Label it correctly; do not call it a bug.

## Acceptance criteria

- [ ] Proforma status has one home and a ratchet.
- [ ] The four delivery-type literals appear only in `DeliveryTypeValues`.
- [ ] A written sweep procedure exists so the next contributor can repeat it.
- [ ] Every new `*Values` class ships with its ratchet in the same commit.
