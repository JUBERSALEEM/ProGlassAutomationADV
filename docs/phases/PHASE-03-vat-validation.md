# Phase 3 — Make VAT Submission Validation Real

**Severity: Critical · Estimate: 1 day · Prerequisite: green build**

The smallest phase in this list and one of the most important, because it is a
validation step that reports success while validating nothing — on a tax filing.

## The defect

`Services/Reporting/VatCalculator.cs:131`:

```csharp
var result = new VatValidationResult { IsValid = true };
```

Nothing in the file ever sets `IsValid = false`. Nothing ever adds to
`VatValidationResult.Errors`. Every problem path appends to `Warnings`:

- negative VAT amount → warning
- negative grand total → warning
- future issue date → warning
- stored VAT not matching the recomputed VAT → warning

Then `Services/Reporting/ReportingDataService.Vat.cs:250-254`:

```csharp
if (!validation.IsValid)
{
    invalidCount++;
    continue; // Skip invalid data
}
```

`invalidCount` is permanently zero. Line 307 prints
`"(filtered from {N}, {invalidCount} invalid)"` to the operator on every
submission, so the report actively asserts that nothing was invalid.

## Steps

### 3.1 — Decide what "invalid" means

Three categories, and they must not be conflated:

| Category | Examples | Effect |
|---|---|---|
| **Reject** | VAT > 0 but grand total 0; VAT ≠ 5% of subtotal beyond tolerance | Exclude from the filing, count it, name the invoice |
| **Warn** | Future issue date, zero-amount invoice | Include, surface in the warnings list |
| **Pass** | Everything else | Include silently |

Negative amounts need a deliberate decision. They are legitimate for credit
notes and corrections, so rejecting them outright will break a real workflow.
The current code warns and includes, which is defensible — but then the warning
must actually reach the user, and today it is buried in a list.

### 3.2 — Populate `Errors` and set `IsValid`

In `VatCalculator.ValidateInvoice`, for each reject condition:

```csharp
result.Errors.Add($"Stored VAT {vatAmount:N2} does not reconcile with " +
                  $"grand total {grandTotalVatInclusive:N2} " +
                  $"(expected {expectedVat:N2}, tolerance {tolerance:N2}).");
result.IsValid = false;
```

Keep `Warnings` for the non-fatal conditions so the existing warning plumbing
keeps working.

### 3.3 — Make the caller report properly

`ReportingDataService.Vat.cs` currently increments a counter and drops the row.
Collect the rejected invoices with their reasons and return them alongside the
result, so the UI can show *which* invoices were excluded and *why*. A tax
filing that silently omits rows is worse than one that refuses to generate.

### 3.4 — Add the tests that were missing

`ValidateInvoice` has no test that asserts a failure. Add:

- VAT inconsistent with gross → `IsValid == false`, one error
- VAT consistent within tolerance → `IsValid == true`
- negative VAT → matches the decision from 3.1
- both zero → valid and empty
- future issue date → warning, still valid

## Acceptance criteria

- [ ] `VatValidationResult.IsValid` can be `false`, and is, for the reject
      conditions.
- [ ] `Errors` is populated with a message naming the actual figures.
- [ ] The submission report names excluded invoices rather than printing a
      hardcoded zero.
- [ ] Unit tests cover at least one failure per reject condition.

## Must not change

- `VatCalculator`'s arithmetic. Only the validation outcome.
- The tolerance of `0.01m` (one fils) unless the business says otherwise.

## Verification

```bash
grep -n "IsValid" Services/Reporting/VatCalculator.cs
# must show at least one assignment to false

grep -n "invalidCount" Services/Reporting/ReportingDataService.Vat.cs
# the counter must be reachable
```
