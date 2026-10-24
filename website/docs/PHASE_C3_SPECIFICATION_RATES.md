# Phase C3: Step 5 specification rates in the website PI

Implementation checkpoint. The website could price a line but had no way to enter
the specification's BASE rate and SURCHARGE %, so an operator had to compute the
oversize rate by hand and key it into every row. This phase adds that entry, using
the rule the repository already holds in `src/data/pricingRules.ts`, which is read
from `SpecGroup` in the desktop application.

No migration was added, no function was changed, and nothing was applied or
deployed.

---

## 1. WPF source inspected for this phase

| File | What it settled |
| --- | --- |
| `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs` | `BaseRate` (102) and `SurchargeRate => MoneyRounding.Round(BaseRate * (1 + SurchargePercent/100))` (135) |
| `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs` | `var targetRate = target.Sqm1 >= 4m ? SurchargeRate : BaseRate;` (327), applied from `UpdateAllRowRates` |
| `ViewModels/ProformaInvoice/Step5DataGrid/GlassItemRow.cs` | `SuppressRateAutoAdjust` (247, 504): a rate typed over by hand survives recalculation; `Value = MoneyRounding.Round(Sqm1 * Qty * Rate)` (376) |
| `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part2.cs` | `EstimatedRatePerSqm` comes from `CalculateSguRate`, `CalculateDguRate`, `CalculateLamRate`, `CalculateDguLamRate` with margin: desktop costing, not a website input |
| `ViewModels/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryViewModel.cs` | The final summary is glass materials plus other charges, discount applied to the taxable value, then VAT |

The implementation reuses the existing port of these rules rather than restating
them: `pricingRules.surchargedRate` and `pricingRules.rowRate` already encode
`SurchargeRate` and `Sqm1 >= 4 ? SurchargeRate : BaseRate`, and the new module calls
them. There is still exactly one implementation of the rule in the website.

---

## 2. What the website now computes, and what it must not

Computed on the website, because the rule is a pure function of what the operator
types and the desktop does the same arithmetic:

| Rule | Implementation |
| --- | --- |
| Oversize rate | `surchargedRate(base, percent)` = `round(base * (1 + percent/100), 2)`, half away from zero |
| Rate per row | `rowRate(base, percent, billableSqm)` = the oversize rate at `billableSqm >= 4`, the base rate below it |
| Which area decides | The billable panel area after the 0.5 m2 floor, exactly as `Sqm1` decides on the desktop |
| Which rows are touched | Only the specification the operator applies, and only rows that have an area |

Not computed on the website, deliberately:

| Rule | Why not |
| --- | --- |
| Step 4 estimated rate per glass family (SGU, DGU, LAM, DGU+LAM, margin) | It is the desktop's costing engine. Reimplementing it would create a second pricing authority, which is the one outcome this work is meant to prevent. The website takes an approved rate |
| Step 6 other charges, wastages and discount | These are not stored for a website proforma. Adding them needs a schema decision and an approved rule set |
| VAT | Presentation of VAT on a portal document is an open business decision (section 7) |
| The stored money | `save_inquiry_proforma_v1` still computes each line's area and value from the rate it receives. The website only proposes the rate |

---

## 3. The workflow implemented

1. Every specification block in the PI workspace now has a rate bar: `BASE RATE
   (AED/m²)` and `SURCHARGE %`, the resulting oversize rate, and a count of how
   many rows of that specification reach the 4 m2 switch.
2. `Apply to specification N` writes the base rate into every row of that
   specification and the oversize rate into the rows at or above the threshold,
   formatted to the two decimals the rate column stores. The rate column remains
   editable, so a deliberate one-off rate is still possible.
3. The bar states, in its own words, that the base rate and percentage are a
   working aid and are not stored, and that each line saves the rate in its RATE
   cell. Nothing on the page pretends the two inputs are a persisted header.
4. The button is disabled until a usable base rate exists and the specification
   has a row, and the applied notice names the base rate, the oversize rate and how
   many rows took it.
5. An incomplete row (no dimensions, so no area) keeps the rate it already carries
   rather than being priced against an area that does not exist yet.
6. The figures flow through the existing save: `toLineInputs` sends each row's
   rate, and the database recomputes and stores the money.

Why the base rate is not persisted: the Phase 15 tables store lines, not
specification headers. Persisting a header would put a second copy of the price in
the database, and it would need a new table plus a change to the write function,
which the current authorisation does not cover. The consequence is honest and
visible: after a reload the base rate box is empty again, while the rates that were
saved are intact in every row.

---

## 4. Files added or changed

Added:

- `website/src/lib/proforma-rates.ts` — `parseRateInput`, `parsePercentInput`,
  `planRates`, `applyRatePlan`, `formatRate`, `rowsThatWouldChange`.
- `website/src/components/inquiries/proforma/SpecificationRateBar.tsx` — the bar.
- `website/tests/proforma-rates.test.tsx` — 11 tests.
- `website/docs/PHASE_C3_SPECIFICATION_RATES.md` — this report.

Changed:

- `website/src/components/inquiries/proforma/ProformaSpecification.tsx` — renders the
  bar per specification and takes the setting props.
- `website/src/components/inquiries/ProformaWorkspace.tsx` — holds the per
  specification setting, applies a plan to the draft, and reports what was applied.
- `website/tests/inquiry-inbox-cards.test.tsx` — the existing specification render
  test passes the three new props. No assertion changed.

Unchanged: the Phase 15 schema, `save_inquiry_proforma_v1`, the API routes,
`pricingRules.ts`, the Phase C1 migration, and every WPF file.

---

## 5. Migrations

**None.** No schema change was needed, and the C1 migration is still
unapplied anywhere real and unchanged (`sha256 52d42d62...`).

---

## 6. Tests run and their results

Run from `website/` on 2026-10-24.

| Command | Result |
| --- | --- |
| `npx vitest run tests/proforma-rates.test.tsx` | 11 tests, 11 pass |
| `npx vitest run` | 29 files, 304 tests, all pass (was 28 / 293) |
| `node --test scripts/*.test.mjs` | 627 tests, 627 pass (unchanged) |
| `npm run lint` | clean |
| `npx tsc --noEmit` | clean |
| `npm run build` | succeeded |

The new tests cover: the 4 m2 switch at the exact boundary; the desktop rounding of
`99.99 * 1.2 = 119.99`; the 0.5 m2 floor keeping a small panel at base rate; only
the chosen specification being touched; an empty percentage meaning no surcharge;
invalid base rates producing no plan; an incomplete row keeping its own rate; rates
written as two decimals with other groups untouched; and, at the component level,
that the bar calls back, reports the oversize count, disables itself without a base
rate, reports typed input upward, and says the base rate is not stored.

Not tested: the flow against a live Supabase project, because no operator session is
available in this sandbox, and real browser rendering of the bar at narrow widths.

---

## 7. Decisions still blocking further work

1. **4 m2 threshold and the 20 percent standard.** The code uses 4 m2 and treats 20
   percent as the shop standard, matching the desktop, but whether these should be
   configuration with an effective date, or remain constants, is undecided. Until it
   is decided they stay where they are and the percentage is editable per
   specification.
2. **VAT presentation.** Whether the portal shows VAT, other charges and terms on a
   document, and at what point, is undecided. No VAT figure is computed or shown.
3. **PI numbering for website documents.** `PI-WEB-YYYY-NNNNNN` is allocated by the
   database and is already in use. Whether a website PI may ever carry the desktop
   number is unresolved, and no attempt is made to reconcile the two.
4. **Status meanings.** `Draft`, `Issued` and `Void` exist in the schema with no
   write path to `Issued` or `Void`, and the `Sent` versus `Issued` question is
   open. Nothing in this phase writes a status.
5. **Persistence of specification headers.** If the business wants the base rate and
   percentage to survive a reload, that needs an approved additive table plus a
   write path. This phase does not assume it.

Independent work still available without those decisions: line-level validation
mirroring the write function's limits in the grid, duplicate and reorder row
actions, and grouping summaries on the saved document.

---

## 8. Complete versus unfinished

Complete: specification level rate entry for website proformas, applying the
desktop's base and oversize rule, with the saved record still priced by the
database. Covered by 11 tests, with the whole suite green.

Unfinished: Step 4 estimation (must stay on the desktop), Step 6 charges, discount
and VAT (blocked on decisions), issuance and acceptance (blocked on status
decisions), desktop publication (needs a contract), and a server rendered PDF that
matches the desktop document.
