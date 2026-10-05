# Inquiry + Proforma Invoice Industrial Hardening Audit — 2026-09-07

## Scope audited

- Inquiry editor/header save, BOQ scope-of-supply rows, repository persistence and inquiry-to-PI conversion.
- Proforma Invoice customer-facing financial waterfall: Step 5 glass total, Step 6 other charges, Step 7 discount/VAT/grand total, Step 9 profitability validation signal.
- Customer-output routes: save, print, PDF/Excel/CSV export.

## Senior findings

1. **Inquiry BOQ could contain unsafe commercial inputs.** The editor allowed negative or implausible dimensions/quantity/rate values to sit on a row. Complete rows with an unknown glass build could be converted into Step 5 without an exact PI build, risking a default group type being used.
2. **Inquiry-to-PI conversion needed a service boundary gate.** UI dropdowns normally prevent bad build codes, but legacy/imported rows and non-UI callers can bypass that. Conversion eligibility now checks the BOQ with shared rules.
3. **Step 7 validation snapshot was incomplete.** It validated identity and high-level totals but did not validate the complete financial waterfall: glass subtotal reconciliation, net subtotal, discount amount, VAT basis, VAT amount, and grand total.
4. **Validation failure was not fail-closed.** If cross-step validation itself threw during Save Invoice, the old path logged and continued. For a customer financial document that is too risky.
5. **Print/export routes did not share the save validation gate.** A wrong total can damage reputation even if the saved row is later corrected. Customer-facing output now runs the same strict calculation gate.

## Industrial hardening implemented

### Inquiry

- Added `InquiryValidationRules` as the shared service-layer ruleset.
- Blocks negative inquiry value and over-limit values.
- Blocks negative BOQ width/height/quantity/rate.
- Blocks implausible BOQ dimensions, quantity and AED/sqm rate using wide industrial limits.
- Allows partial BOQ rows as intake notes, but complete rows that can become PI pricing must use exact `SGU`, `DGU`, `LAM`, or `DGU+LAM`.
- Inquiry editor `CanSave` and `SaveAsync` now both use the same rules.
- Inquiry repository `SaveLineItemsAsync` now enforces the same rules so shortcuts/agent/tests cannot persist unsafe BOQ data.
- Inquiry-to-PI eligibility now blocks unsafe BOQ conversion with a clear message.
- Pure BOQ converter now refuses unknown complete build rows instead of letting Step 5 inherit a default group type.

### Proforma Invoice

- Step 7 VAT percentage setter now clamps values to `0..100`.
- Cross-step validation snapshot now includes the full Step 7 waterfall:
  - Step 7 glass subtotal
  - Step 7 other charges subtotal
  - net subtotal
  - discount amount and derived discount percent
  - subtotal before VAT
  - VAT applicability/rate/amount
  - VAT-inclusive grand total
- Validator blocks:
  - negative Step 5/6/7 financial values
  - Step 7 glass subtotal mismatching Step 5 glass total
  - net subtotal mismatch vs glass + other charges
  - discount amount exceeding net subtotal
  - subtotal-before-VAT mismatch
  - VAT percent outside `0..100`
  - VAT amount mismatch vs taxable subtotal and VAT rate
  - grand total mismatch vs subtotal before VAT + VAT
- Save now **fails closed** when validation cannot execute.
- Print and PDF/Excel/CSV export now run the same calculation gate before producing customer-facing output.

## Design pattern now enforced

- **Inquiry is intake, not estimation.** Partial/unknown rows may remain notes only; complete rows must be exact and safe before PI conversion.
- **PI totals are a waterfall.** Step 5 + Step 6 → net subtotal → discount → taxable subtotal → VAT → grand total. Every link is validated before save/output.
- **Customer-facing documents are guarded.** Save, print and export share the same strict validation boundary.
- **No guessing.** Unknown glass builds are not defaulted, fuzzy matched or silently converted.

## Validation performed in sandbox

- Python XAML parse: pass for all XAML files.
- C# brace-balance checks on edited files: pass.
- `git diff --check`: pass.
- `dotnet build ProGlassAutomation.csproj --no-restore`: unavailable in this sandbox because `dotnet` is not installed.

## Known limitation

These hardening rules significantly reduce business/calculation risk, but no static audit can mathematically guarantee “zero chance” of error. Runtime WPF binding/resource resolution and full C# compilation still need verification in a machine with the .NET SDK installed.
