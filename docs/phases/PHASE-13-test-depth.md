# Phase 13 — Test Depth Where Money Is Computed

**Severity: Major · Estimate: 4-6 days · Prerequisite: green build**

## Current state

171 test files, 1,306 `[Fact]`/`[Theory]` methods, 30,272 lines against 224,583
production lines — a ratio of **1 : 7.4**.

The coverage that exists is well-aimed: characterisation tests pin the pricing
branches, ratchet tests hold architectural invariants, and the status-vocabulary
tests assert on source text rather than behaviour so a rename breaks the build.
The gap is not quality, it is **where** the tests are.

## The specific gaps

### 13.1 — No test asserts a validation can fail

`VatCalculator.ValidateInvoice` returns a result whose `IsValid` is never false.
No test covers a failing case, which is why the defect survived. Every validator
in the codebase needs at least one test per rejection path — see Phase 3.

### 13.2 — No concurrency test anywhere

Phase 1 introduces optimistic concurrency. The test that justifies it must exist
first: N parallel stock issues against one sheet, asserting the final quantity
and that none is lost. **Write it before the fix and watch it fail.**

### 13.3 — No reconciliation property test

The invariant `subtotal + vat == grand` is not asserted anywhere. It currently
fails on a predictable subset of inputs because of the two rounding policies
(Phase 2). A property-based test over generated line sets is the only way to
find which inputs, and it will keep finding them as the pricing engine evolves.

### 13.4 — Dashboard figures have no snapshot tests

Seven dashboards now read from `IDashboardMetricsService`. Each migration was
verified by simulating old logic against new — a real check, but a one-off
script, not a test. Add tests asserting the documented figures for a fixed
dataset, including the four deliberate differences recorded in
`docs/REMEDIATION_PLAN.md` § Phase 3.

### 13.5 — The pricing engine deserves property tests

`SpecificationTargetCalculator` is pure, deterministic and WPF-free — an ideal
property-test target. Assert:

- status is never `UpToDate` when a required input is missing
- margin sign agrees with `DifferencePerSqm` sign
- `AtTarget` only within `TargetMarginTolerance`
- the formula reproduces the worked example in the file header
  (`(25 + 25) ÷ 0.85 + 45 = 103.82 → × 1.15 = 119.39`)

That last one is the highest-value test in the product: it is the arithmetic a
quotation is judged on.

## Approach

- **Property tests before unit tests** for the money paths. Hand-written cases
  confirm what you already thought; generated cases find what you did not.
- **Characterisation before change.** Where behaviour is uncertain, pin current
  behaviour first, then change it deliberately. This is already the established
  pattern — `OtherChargeRowMetricQtyCharacterisationTests` does exactly this and
  marks two tests **WILL CHANGE**.
- **Every ratchet must fail in both directions.** A ratchet that only catches
  new offenders silently rots as its allow-list goes stale. All five current
  ratchets do this correctly; keep it that way.

## Acceptance criteria

- [ ] Every validator has a failing-case test.
- [ ] A concurrency test proves stock cannot lose an update.
- [ ] `subtotal + vat == grand` asserted as a property, with no tolerance.
- [ ] Every dashboard's figures pinned against a fixed dataset.
- [ ] The pricing formula's worked example asserted.
- [ ] Test-to-production ratio improved, with the improvement concentrated in
      the money paths rather than spread evenly.

## Must not change

- The ratchet pattern. It is the most effective thing in the test suite.
- Tests marked **WILL CHANGE** — they are intentional, and flipping them is the
  signal that a planned behaviour change landed.
