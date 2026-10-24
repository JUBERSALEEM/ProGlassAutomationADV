# Phase C2: the saved proforma document on the website

Implementation checkpoint. This phase adds the Step 7 equivalent the website was
missing: a document view of a **saved** PI revision, printed from the values the
database stored and never recomputed in the browser.

Nothing was deployed, no migration was applied, and no WPF source was modified.

---

## 1. WPF Steps 4 to 7, files inspected

The WPF application lives in this repository (`ProGlassAutomation.csproj`), so the
behaviour below was read from source, not from a report.

| File | What it settled |
| --- | --- |
| `Utilities/SqmCalculator.cs` | `MinimumSqmPerPanel = 0.5`; `CalculatePanel(w,h)` = `Math.Round(w*h/1_000_000, 3)` then the 0.5 floor; `CalculateTotal` = `Math.Round(panel*qty, 3)`; invalid dimensions return 0, not the floor |
| `ViewModels/ProformaInvoice/Step5DataGrid/GlassItemRow.cs` | `RecalculateAll` (355 to 382): `Sqm1` from `SqmCalculator.CalculatePanel(W1,H1)`, `TotalSqm = Math.Round(Sqm1*Qty, 3)`, `Value = MoneyRounding.Round(Sqm1*Qty*Rate)`; `SuppressRateAutoAdjust` (247, 504) is what keeps a typed-over rate |
| `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs` | `BaseRate` (102), `SurchargeRate => MoneyRounding.Round(BaseRate * (1 + SurchargePercent/100))` (135) |
| `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs` | `var targetRate = target.Sqm1 >= 4m ? SurchargeRate : BaseRate;` (327), applied on dimension, base-rate and surcharge changes |
| `ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.Part2.cs` | The estimator derives `EstimatedRatePerSqm` per glass family (`CalculateSguRate`, `CalculateDguRate`, `CalculateLamRate`, `CalculateDguLamRate`) with margin. This is desktop authoring; the website does not reproduce it |
| `ViewModels/ProformaInvoice/Step6OtherCharges/*` | Custom charges, wastages and metric discovery: the desktop adds charges the website proforma tables do not store |
| `ViewModels/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryViewModel.cs` | The final summary is glass materials + other charges, discount applied to the taxable value, then VAT (default 5%), producing a VAT-inclusive grand total |
| `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs` | The printed document carries `TotalAmount` (glass lines), `DiscountAmount`, `VatAmount` and `GrandTotal` (415 to 416) |
| `ViewModels/ProformaInvoice/Step7PrintPreview/TermsAndConditions/*` | Terms are part of the desktop document and are not modelled on the website yet |

Website side, read to avoid a second source of truth:

- `src/data/pricingRules.ts` (panel floor, the 4 m2 switch, the 20 percent standard)
- `src/lib/proforma-draft.ts` (`priceDraft`, `lineValue`, `draftTotals`, formatting)
- `src/lib/proforma-api.ts` (role gate: `inquiry_admin` writes, `inquiry_admin` and `inquiry_auditor` read)
- `src/components/inquiries/ProformaWorkspace.tsx`, `proforma/ProformaBuilder.tsx`,
  `proforma/ProformaSpecification.tsx`
- `src/types/proforma.ts`, `supabase-inquiry-proforma-phase15.sql`,
  `supabase-inquiry-proforma-phaseC1.sql`, `docs/PHASE_C1_IMPLEMENTATION_REPORT.md`

---

## 2. Verified business rules and formulas used

These were taken from the files above and are implemented exactly once in the
website today (`pricingRules.ts` and `proforma-draft.ts`, mirrored by
`save_inquiry_proforma_v1`). This phase adds no new arithmetic to them.

| Rule | Source | Formula |
| --- | --- | --- |
| Panel area | `SqmCalculator.CalculatePanel` | `w_mm * h_mm / 1_000_000`, rounded to 3 dp |
| Minimum billable area | `MinimumSqmPerPanel` | a panel below 0.5 m2 bills at 0.5 m2; 0 mm in either direction is 0, not 0.5 |
| Line area | `GlassItemRow.RecalculateAll` | `round(panel_area * qty, 3)` |
| Line value | `GlassItemRow.RecalculateAll` | `MoneyRounding.Round(panel_area * qty * rate)`, half away from zero |
| Oversize switch | `SpecGroup.Part2/Part3` | a row whose billable panel area is `>= 4 m2` takes `SurchargeRate`, the rest take `BaseRate`; it is a rate substitution, not an added charge |
| Surcharge rate | `SpecGroup.Part1` | `round(BaseRate * (1 + SurchargePercent / 100))`; the percentage is per specification, default 0, shop standard 20 |
| Manual rate override | `SuppressRateAutoAdjust` | a rate a person typed over survives recalculation and reload |
| Document subtotal | `PrintableInvoiceViewModel` | `TotalAmount` is the glass lines' total; discount and VAT are applied by the main summary, not by the lines |
| VAT | `InvoiceMainSummaryViewModel` | 5 percent by default, applied to net minus discount |

The website proforma tables store only the glass lines and their values. They do
not store other charges, discount, VAT or terms, so the website cannot and does
not produce a tax total.

---

## 3. The workflow implemented

Before this phase the workspace could author, price, save, revise and list saves,
but no page showed what the saved revision actually looks like as a document. The
only printable artefact was a working grid of a draft. This phase adds the missing
half of Step 7 for website-origin proformas:

1. The workspace header now has a two-state view switch, `Specification` and
   `Document`. `Document` is disabled, with a reason in its tooltip, until a
   revision has been saved, so no control claims a document exists when none does.
2. `Document` renders `buildProformaDocument(proforma, lines)` from the record the
   server returned. Every figure on the page is a stored column: `sqm`,
   `total_sqm`, `approved_rate`, `line_value`, and the header's `line_count`,
   `total_sqm`, `total_value`.
3. Lines are grouped by `group_no` and ordered by group then line. Each
   specification gets a subtotal row, and the page ends with the glass materials
   subtotal, labelled as such rather than as an invoice total.
4. The document states its own status honestly: a `Draft` revision carries "this
   revision is a draft, it has not been issued"; a `Void` revision says it must not
   be presented as current; and if the stored header disagrees with the stored
   lines, both figures are shown with the disagreement named, never repaired.
5. When the grid holds edits that the saved revision does not, a banner says so.
   The comparison is over the fields a save actually transmits
   (`draftDiffersFromSaved`), so a cosmetic difference cannot raise it.
6. `Print document` calls `window.print()`. The document is styled for print
   (`print:` variants, black on white, toolbar hidden) so the browser's own print
   to PDF is the export path. No PDF is generated on the server, which keeps the
   desktop as the authority for the issued PDF.
7. Nothing recalculates. The component imports no pricing function; only sum
   addition and formatting. A unit test feeds a stored line whose value disagrees
   with its dimensions and asserts the stored value is printed unchanged.

Deliberately not built in this phase, because the behaviour would have to be
guessed or would duplicate the desktop:

- Step 4 rate estimation (SGU, DGU, LAM, DGU+LAM, margin). The website takes an
  approved rate per line; deriving one would create a second pricing authority.
- Step 6 other charges, wastages, discount and VAT. Not stored for a website
  proforma, and whether the portal shows VAT is an open business decision.
- Terms and conditions, signature blocks and the desktop PDF layout.
- Issuing, acceptance and job creation. The status vocabulary and the
  `Sent` versus `Issued` question are unresolved, and the schema currently permits
  only `Draft`, `Issued` and `Void` with no write path to the last two.
- Desktop-origin documents. `inquiry_proformas.source_system = 'desktop'` exists,
  but nothing publishes a desktop PI yet, so the document view would have nothing
  to read. The reader itself is origin agnostic and will render one when it exists.

---

## 4. Files added or changed

Added:

- `website/src/lib/proforma-document.ts` — the document model (`buildProformaDocument`,
  `documentCaveats`, `draftDiffersFromSaved`), with stored-value totals and the
  integrity comparison.
- `website/src/components/inquiries/proforma/ProformaDocument.tsx` — the printable
  document, the caveat banners, `printSqm`, `printDocument`.
- `website/tests/proforma-document.test.tsx` — 11 tests.
- `website/docs/PHASE_C2_PROFORMA_DOCUMENT.md` — this report.

Changed:

- `website/src/components/inquiries/ProformaWorkspace.tsx` — the view switch, the
  disabled-document state, and passing stored lines plus the unsaved-changes flag
  to the document.

Nothing else was touched. The Phase 15 schema, the save function, the API routes,
the pricing rules, the C1 migration and every WPF file are unchanged.

---

## 5. Database migrations

**None added in this phase.** The document reads existing Phase 15 columns
(`inquiry_proformas` and `inquiry_proforma_lines`), so no schema change was needed.

The C1 migration remains **not applied anywhere real**: it was validated against a
disposable PostgreSQL 17.9 reconstruction, not against a Supabase branch, and this
phase did not change or apply it.

---

## 6. Tests run and their results

All run from `website/` on 2026-10-24.

| Command | Result |
| --- | --- |
| `npx vitest run tests/proforma-document.test.tsx` | 11 tests, 11 pass |
| `npx vitest run` | 28 files, 293 tests, all pass (baseline 27 files / 282 tests, so +1 file / +11 tests) |
| `node --test scripts/*.test.mjs` | 627 tests, 627 pass (unchanged) |
| `npm run lint` | clean |
| `npx tsc --noEmit` | clean |

The new tests cover: grouping and stored totals; a stored value that disagrees with
its own dimensions being reprinted rather than recomputed; a header that disagrees
with its lines being reported and not repaired; the half-fil and fourth-decimal
tolerances; the draft and void caveats; the unsaved-changes comparison; the four
decimal area format; the print call; and the rendered document containing the
number, revision, customer, inquiry, stored value, caveat and print control.

Not tested, because it cannot be exercised here: the printed page in a real
browser, the visual layout at print sizes, and the flow against a live Supabase
project (no operator session is available in this sandbox).

---

## 7. Remaining decisions, dependencies and risks

Business decisions still open, unchanged by this phase:

1. Whether the portal displays VAT, other charges and terms at all. The document
   therefore prints the glass materials subtotal and says what it excludes.
2. `Sent` versus `Issued`, and whether `Approved` is customer acceptance. Until
   these are settled no issue or acceptance transition is implemented.
3. Whether the 4 m2 threshold and the 20 percent standard are policy or local
   practice. The website already mirrors the current behaviour, but it is not
   configurable.

Dependencies and risks:

- Desktop publication does not exist yet. Until a publisher writes
  `source_system = 'desktop'` rows, the website has no desktop PI to display, and
  `inquiries.web_submission_id` stays null.
- The C1 migration is validated but not applied. Nothing in this phase depends on
  it, but the desktop-facing work does.
- Printing depends on the browser. A server-rendered PDF that matches the desktop
  document to the fil is still future work and needs the desktop layout as its
  reference.
- The document shows the last saved revision, so a document can lag the grid by
  design; the unsaved-changes banner is the only warning, and it is not a block.

---

## 8. Complete versus unfinished

Complete and working: the saved-revision document for website proformas, reached
from the workspace, printing stored values only, with status caveats, an integrity
check and an unsaved-changes warning, covered by 11 tests.

Unfinished: Step 4 estimation, Step 6 charges and discount, VAT, terms, issuance
and acceptance, the desktop publisher, desktop-origin display, a server PDF, and
applying C1 to a Supabase branch. Those are the next coherent phases, in that
order, and each needs either a decision or a schema step that is not yet approved.
