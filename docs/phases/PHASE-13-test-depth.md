# Phase 13 — Test Depth Where Money Is Computed

**Severity: Major · Estimate: 4-6 days · Prerequisite: green build**

## Status — the real gap APPLIED 2026-09-09

Added `Tests/ProformaInvoice/SpecificationTargetBuildTypeTests.cs` (7 tests).

### What was already covered — do not redo it

`TargetPriceModelTests.cs` is already deep, and an earlier note in this phase
implied the worked example was missing. It is not. That file already asserts:

- the shop's own vector, `103.82 → 119.39` (`User_example_base_trigger_and_target_103_82_and_119_39`)
- the approved DGU vector at `178.93`
- every missing-input status — `Missing_base_price`, `Missing_glass_rate`,
  `Missing_asp_rate`, `Missing_yield`
- all four U-insert treatments and the margin judgement for each
- snapshot JSON round-tripping and pre-target deserialisation

Anyone extending this area should read that file first. Writing tests against a
calculator without checking what already pins it produces duplicates and a false
sense of added coverage.

### What was genuinely untested

Measured, not assumed — occurrences anywhere under `Tests/`:

| | count |
|---|---|
| `GlassBuildType.Sgu` | **0** |
| `GlassBuildType.Lam` | **0** |
| `GlassBuildType.DguLam` | **0** |
| `DefaultTargetUpliftPercent` | **0** |
| negative-area clamp | **0** |

Every existing case is a **DGU**. Three of the four build types had never been
exercised, and they are not cosmetic variants: `GlassRateSum`,
`HasRequiredGlassRates` and `RequiresAsp` all switch on the build type and
decide how many lites are summed, whether a missing third lite blocks the
calculation, and whether an ASP rate is demanded at all.

The new file covers the glass sum per build type, the missing-lite guard for
each, the ASP asymmetry (DGU and DGU+LAM require it, SGU and LAM do not — a LAM
has no cavity and therefore no spacer bar), the `TargetUpliftPercent <= 0`
fallback to 15%, and the `Math.Max(0m, areaSqm)` clamp.

One assertion is worth calling out. The LAM vector lands on `77.625`, an exact
half, so it pins the rounding mode these sites actually use: `Math.Round(v, 2)`
with no mode argument is `ToEven`, giving `77.62`. Away from zero would give
`77.63`. When Phase 2's migration eventually reaches this file, that assertion
is what reports the change — and that it was deliberate.

Every expected value was computed independently in base-10 decimal with
`ROUND_HALF_EVEN`, matching `Math.Round(value, 2)`. 11 of 11 model checks agree
with the assertions as written.

### What remains open in this phase

Property-based coverage of the wider pricing pipeline (`InvoiceTotalsCalculator`,
`SheetStorePricingService`, `ProformaChargeMetricsCalculator`) is untouched, and
the test ratio is unchanged at roughly 1 : 7.4. Those need a green build to add
against, because a property test that never ran proves nothing.


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
