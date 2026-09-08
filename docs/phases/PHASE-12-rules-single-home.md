# Phase 12 — One Home for Every Rule

**Severity: Moderate · Estimate: 3 days · Prerequisite: green build**

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
