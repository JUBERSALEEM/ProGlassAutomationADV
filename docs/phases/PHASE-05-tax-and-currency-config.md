# Phase 5 — Tax and Currency as Configuration

**Severity: Major · Estimate: 5-8 days · Prerequisite: Phases 2 and 3**

## Status — 5.3 APPLIED 2026-09-09; 5.1, 5.2, 5.4, 5.5 blocked

| | |
|---|---|
| 5.3 currency minor unit | **applied** — precision tied to the books currency and checked |
| 5.1 introduce a tax profile | **blocked** — see below |
| 5.2 replace the constants with the profile | **blocked** |
| 5.4 tax treatment per line | **blocked** |
| 5.5 effective dating | **blocked** |

### 5.3 — the infrastructure was already there

`Services/Localization/CurrencyCatalog.cs` already carries the minor unit of
every currency it lists: AED 2, JPY 0, BHD 3, KRW 0. `CurrencyService` already
separates `BaseCurrency` (the books, a `const "AED"`) from `DisplayCurrency`
(what the operator chose to read prices in).

So the work was not to build a currency model — it was to connect
`MoneyRounding.DecimalPlaces`, hard-coded at 2, to the thing that already knew
the answer, and to make that connection fail loudly rather than drift.

`MoneyRoundingPolicyTests` now asserts `MoneyRounding.DecimalPlaces` equals
`CurrencyCatalog.Find(CurrencyService.BaseCurrency).Decimals`. If the base
currency ever changes, that fails instead of silently rounding money to the
wrong precision. Verified against the source: `Find` is case-insensitive, AED
is 2, JPY is 0, BHD is 3.

### The distinction that matters, now written down and tested

Rounding precision follows the **books** currency, never the **display**
currency. That is not a detail — it is the difference between a correct change
and a data-corrupting one. If `MoneyRounding` followed `DisplayCurrency`, an
operator switching the UI to yen would quietly truncate every stored amount to
whole units, because JPY's minor unit is 0. Display converts; storage rounds.

`MoneyRounding`'s doc comment now says so, and a second test pins JPY at 0 and
BHD at 3 so the two concepts provably can diverge — which makes the distinction
live rather than hypothetical.

### Why 5.1 and 5.2 are still blocked

`VatCalculator` declares its rate as `public const decimal VAT_RATE_PERCENT =
5.0m`. A `const decimal` is **inlined at compile time into every call site**, so
replacing it with a configured value is not a one-line change — it is a
signature change rippling through pricing, invoicing and reporting at once, with
no compiler here to find the sites that assumed a compile-time constant.

It is also the wrong order. Phase 2's migration of 334 rounding call sites is
still open, and a configurable rate multiplies that surface: every rounding
decision becomes per-currency at the same moment the rate becomes per-jurisdiction.
Finishing Phase 2 first keeps one variable moving at a time.

5.4 (tax treatment per line) and 5.5 (effective dating) both need a schema
change and a green build.


## The problem

`Services/Reporting/VatCalculator.cs` declares UAE VAT as compile-time constants:

```csharp
public const decimal VAT_RATE_PERCENT  = 5.0m;
public const decimal VAT_RATE_DECIMAL  = 0.05m;
public const decimal VAT_MULTIPLIER    = 1.05m;
```

These are `const`, so they are inlined into every consuming assembly at compile
time — they cannot even be overridden at runtime without a rebuild.

Meanwhile the app already resolves country and currency dynamically:
`Services/Localization/GeoService` detects the region,
`Services/Localization/CurrencyService` holds FX rates and refreshes them, and
`LocalizationService` keeps UI language independent of currency. The
architecture is multi-region everywhere except the single number printed on
every invoice.

## Why this blocks "industrial ERP grade"

A commercial glass ERP sold beyond the UAE needs, at minimum:

- a **tax rate per jurisdiction**, not per build
- **tax registration numbers** on the invoice (UAE TRN, EU VAT ID, Indian GSTIN)
- **reverse charge** and **zero-rated** categories, which are not "rate = 0" —
  they are distinct legal treatments with different reporting
- **tax applied per line**, because mixed-rate invoices are normal
- a currency **minor unit** that is not always 2 (JPY has 0, BHD has 3)

## Steps

### 5.1 — Introduce a tax profile

```csharp
public sealed class TaxProfile
{
    public string JurisdictionCode { get; set; }   // "AE", "SA", "IN"
    public string RateLabel { get; set; }          // "VAT", "GST"
    public decimal StandardRate { get; set; }
    public int CurrencyDecimalPlaces { get; set; }
    public string TaxRegistrationLabel { get; set; } // "TRN"
}
```

Persist per company, editable in Settings, defaulted to UAE 5% so **existing
installations behave identically**.

### 5.2 — Replace the constants with the profile

Keep `VatCalculator`'s method names. Change the signatures to take a rate:

```csharp
public static decimal CalculateVatFromSubtotal(decimal subtotal, decimal rate)
```

Provide an overload defaulting to the configured profile so the 122 existing
call sites migrate incrementally rather than all at once. **Delete the constants
only at the end** — while they exist, a new call site can silently reintroduce
the hardcode. Add a ratchet test forbidding `0.05m` and `1.05m` literals outside
the profile, in the same style as the existing
`VatRateSingleSourceTests`.

### 5.3 — Currency minor unit

`MoneyRounding.DecimalPlaces` from Phase 2 becomes a function of the active
currency. Every `:N2` format string in PDF and XAML becomes `:N{places}`. Search:

```bash
grep -rn ':N2' --include=*.cs --include=*.xaml . | grep -v obj
```

This is the highest-volume, lowest-risk part of the phase, and the easiest to
half-finish. Do it completely or not at all.

### 5.4 — Tax treatment per line

Add a `TaxTreatment` to line items: `Standard` / `ZeroRated` / `Exempt` /
`ReverseCharge`. The invoice total becomes a sum over treatments, and the PDF
must show the breakdown — that is a legal requirement in most jurisdictions,
not a nicety.

### 5.5 — Effective dating

Tax rates change. An invoice issued in 2025 must keep the 2025 rate forever.
Store the rate **on the invoice line at issue time**, and never recompute a
saved invoice from the current profile. `TaxInvoiceSnapshot` already carries a
snapshot; extend it rather than adding a parallel mechanism.

## Acceptance criteria

- [ ] No `0.05m` / `1.05m` / `5.0m` literal outside the tax profile; enforced by
      a ratchet test.
- [ ] A company can be configured with a non-5% rate and a non-2 minor unit, and
      every invoice, PDF and report reflects it.
- [ ] Existing UAE installations produce byte-identical output to before.
- [ ] Saved invoices are immune to later profile changes.
- [ ] Tax treatments render as a breakdown on the printed invoice.

## Must not change

- The rounding mode decided in Phase 2 — this phase changes *how many* decimals,
  not *which way* to round.
- `SavedStatement`'s TEXT money columns. Out of scope here.
