# Money Rounding Policy

The single rule for rounding monetary values in ProGlass Automation.
Implemented by [`Services/Reporting/MoneyRounding.cs`](../Services/Reporting/MoneyRounding.cs).

Added 2026-09-09 (Phase 2). Before this, the money pipeline ran two rounding
policies at once and there was no document to appeal to when they disagreed.

## The rule

| | |
|---|---|
| **Mode** | `MidpointRounding.AwayFromZero` — a half rounds away from zero |
| **Precision** | 2 decimal places (AED fils) |
| **Applied at** | the **line**, then summed |
| **Entry point** | `MoneyRounding.Round(decimal)` |

```csharp
decimal line = MoneyRounding.Round(unitRate * quantity);
decimal subtotal = lines.Sum();          // sum of rounded lines
decimal vat = MoneyRounding.Round(subtotal * rate);
decimal grand = MoneyRounding.Round(subtotal + vat);
```

## Why away from zero, not banker's rounding

`MidpointRounding.ToEven` is statistically fairer across a large population,
which is why it is the .NET default and why 400-odd call sites in this codebase
were silently using it.

Away from zero wins here for two reasons:

1. **It is what the legally-filed layer already did.** `InvoiceRoundTo` and
   `VatCalculator` both used it. Routing those two through `MoneyRounding`
   therefore changed no arithmetic at all — the VAT and invoice layer, whose
   output goes to the FTA, produces byte-identical figures to before.
2. **An invoice is checked by hand, one line at a time.** Someone reconciling a
   quotation against a purchase order expects 100.005 to become 100.01 every
   time. Banker's rounding gives 100.00 for that and 100.02 for 100.015, which
   reads as an error even though it is not.

## What this does NOT cover

`MoneyRounding` is for money only. These have their own precisions and must not
be routed through it:

| Kind | Precision | Where |
|---|---|---|
| Square metres | 3 or 4 dp | `Utilities/SqmCalculator.cs`, `TaxInvoiceLineService`, delivery-note projections |
| Quantities / pieces | 0-3 dp | Step 9 charge quantities, cut-list parsing |
| Percentages | 1-2 dp | margins, delivery percentages, portfolio figures |
| Colours, pixels, scale | integers | `Services/Theme/ColorMath.cs`, `ViewportService`, `ScreenshotService` |

Rounding a square-metre figure to a currency precision silently corrupts a
measurement. This is why the Phase 2 migration is done file by file with a
classification step, and not with a global find-and-replace.

## A deliberate intermediate rounding

`SpecificationTargetCalculator` rounds the base **rate** to 2 dp before applying
the uplift:

```csharp
decimal basePerSqm = Math.Round(preUplift, 2);
result.SpecificationTargetPerSqm = Math.Round(basePerSqm * upliftFactor, 2);
```

That is a third rounding before `rate × area` is rounded again, and it is
intentional. The reasoning is recorded in the calculator's own header: the shop
writes `(25 + 25) ÷ 0.85 + 45 = 103.82 → × 1.15 = 119.39` on the card by hand,
and the software has to reproduce the number a human derived, not the more
precise one. Lifting the intermediate rounding would change every target price
in the product.

The policy accommodates it rather than forbidding it: **a rate may be rounded
for display and reconciliation, provided the line total is rounded once more
after multiplication and the final sum is of rounded lines.**

## The invariant

For every invoice:

```
sum(round(line)) == round(sum(lines))   within 0.01 × lineCount
subtotal + vat   == grand               exactly
```

The second is the one that failed before this phase, because pricing rounded
half-to-even and invoicing rounded half-away-from-zero. It is asserted by
`Tests/Reporting/MoneyRoundingPolicyTests.cs`.

## Migration status

`MoneyRounding` is the home, and `VatCalculator.Round` and `InvoiceRoundTo` —
the eight sites that already rounded away from zero — route through it. Those
were chosen first precisely because the change is behaviour-neutral.

The remaining `Math.Round(x, 2)` call sites in the money namespaces still use
the default `ToEven`. They are **not** converted blind, because the same
two-argument call rounds square metres to 3 dp and counts to integers elsewhere
in the same files, and there is no compiler in this environment to catch a
misclassification.

`Tests/Architecture/MoneyRoundingRatchetTests.cs` pins the current count per
namespace and fails if it grows. Convert one file at a time, with a build
between each, and remove it from the allow-list as you go — the same pattern as
`PaginationRatchetTests`.

**Ordered migration queue** (money-dense first):

1. `Services/Statement/Aggregation/StatementAggregator.Part1.cs` — aging buckets, all money
2. `Services/Statement/Workflow/StatementSnapshotProjector.cs` — same
3. `Services/ProformaInvoice/TargetPricing/TargetPriceModels.cs:443` — `RequiredTargetValue`
4. `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part2.cs` — six basis calculations
5. `ViewModels/JobProfitability/JobDetailViewModel.cs:337` — `DifferencePerSqm`
6. `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs` — **careful**: this file mixes money and `Quantity` fields; classify line by line
