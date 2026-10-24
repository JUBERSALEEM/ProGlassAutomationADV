# Phase B. Proforma Invoice source audit and target architecture

Read-only audit. No source was modified, no migration was created or applied, no
database was written, and no branch was switched, reset or pushed while this
report was produced.

Evidence base:

| Repository | Branch | Commit | Working tree |
| --- | --- | --- | --- |
| `JUBERSALEEM/ProGlassAutomationADV` | `arena/2611f8ff-proglassautomationadv` | `10f56b926430eaf784b33cfb917f31a25de81975` | clean, local equals `origin` |
| `JUBERSALEEM/ProglassAutomation3.0` | `arena/da841e06-proglassautomation3-0` | `812cf315180401db593d938e7c7f63d8664c4c22` (2026-10-24T09:07:57Z) | remote read through `gh api` only |

Labels used throughout:

- **Verified fact**: read directly from the source at the commit above.
- **Missing functionality**: no implementation exists at that commit.
- **Proposed design**: a recommendation, not present in any repository.
- **Requires business confirmation**: a decision that changes money, numbering or access and that the source cannot settle.

Every claim below is a static reading. No Windows build was executed, so runtime
behaviour, rendered PDF output and SQLite file contents are **unverified** unless
stated otherwise.

---

## 1. Executive summary

1. The desktop Proforma Invoice is not a form. It is a nine step ERP wizard with
   its own pricing catalogue, its own grid arithmetic, its own charge engine, its
   own document lifecycle and its own PDF renderer. Steps 4 to 7 are the
   commercially significant core, and they are coupled: Step 4 writes rates into
   a specification catalogue, Step 5 re-derives every line from dimensions, Step 6
   adds charges that are sometimes computed from Step 4 and Step 5 metrics, and
   Step 7 owns VAT, discount, rounding and persistence. **Verified fact.**
2. The website proforma is a different product with the same name. It stores
   lines only, has no charges, no discount, no VAT, no print, no PDF, and a status
   vocabulary of three words of which only one is ever written.
   **Verified fact** (`src/lib/proforma-api.ts`, `supabase-inquiry-proforma-phase15*.sql`).
3. Both sides already agree on the two rules that matter most for money: the
   0.5 m2 minimum billable panel and the 4 m2 surcharge switch.
   **Verified fact** (`Utilities/SqmCalculator.cs:25`,
   `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs:327,952`,
   `SpecGroup.Part3.cs:273`, `website/src/data/pricingRules.ts`).
4. The desktop holds three incompatible status vocabularies
   (`ProformaInvoiceStatus`, `InvoiceLifecycleState`, the website three word set)
   and two incompatible meanings of "cancel". **Verified fact.**
5. There is no desktop publisher and no desktop consumer of the website proforma
   tables. The website table was designed for desktop publication but its check
   constraint forbids a desktop row from carrying `inquiry_id`, which is exactly
   the link the business needs. **Verified fact**, and the single most important
   contract defect found.
6. The correct system of record is neither pure Supabase nor pure SQLite. It is a
   split by ownership with an explicit publication step, with the desktop owning
   its document and numbering and the website owning its own drafts and the
   shared read model. **Proposed design**, with reasons in section 15.

---

## 2. Verification of repository state and reading method

Commands executed (read only):

- `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, `git status --porcelain`,
  `git log -1`, `git ls-remote origin` on `ProGlassAutomationADV`.
- `gh api repos/JUBERSALEEM/ProglassAutomation3.0/branches/...` and the recursive
  tree at `812cf31`, then `gh api .../contents/<path>?ref=812cf31` for individual
  files, with no local clone of 3.0 written outside `/tmp`.
- `grep` and `sed` over the ADV source tree. No file opened for write.
- `grep` over `website/supabase-*.sql`, `website/src/**` and `website/docs/PHASE15_*`.

Scope of the desktop read, all **Verified fact** as present:

| Area | Files |
| --- | --- |
| Wizard host | `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs`, `WizardStep.cs`, `InvoiceItem.cs` |
| Step 4 | `Step4PricingEstimator/PricingEstimatorViewModel.cs`, `.Part1.cs` (1006 lines), `.Part2.cs` (740), `GlassTypeCard.cs`, `SpecTargetGroup.cs`, `SguParameters/`, `DguParameters/`, `LamParameters/`, `DguLamParameters/` |
| Step 5 | `Step5DataGrid/DataGridViewModel.cs`, `.ImportExport.cs`, `.Optimization.cs`, `SpecGroup.cs`, `.Part1.cs` (1405), `.Part2.cs` (993), `.Part3.cs` (279), `GlassItemRow.cs` (543), `GrandTotalSummary/` |
| Step 6 | `Step6OtherCharges/OtherChargesViewModel.cs` (901), `MetricDiscoveryHubViewModel.cs`, `Wastages/`, `CustomCharges/` |
| Step 7 | `Step7PrintPreview/PrintPreviewViewModel.cs`, `InvoiceMainSummary/InvoiceMainSummaryViewModel.cs`, `PrintableInvoice/` (3 files), `TermsAndConditions/` |
| Persistence | `Services/ProformaInvoice/ProformaInvoiceRepository/*`, `ProformaInvoiceMapper.Part1.cs`, `ActiveInvoiceContext.cs`, `SpecGroupService.cs`, `GlassPriceStamper.cs`, `SheetStorePricingService.cs`, `SheetStockLinkService.cs`, `Step4MaterialIntegrity.cs`, `SpecSheetRegistry.cs`, `ProformaDuplicateSnapshotScrub.cs` |
| Lifecycle | `InvoiceLifecycleStateMachine.cs`, `ProformaLifecycleSync.cs`, `InvoiceRoundTo.cs`, `Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs`, `Services/Workflow/StatusTransitions.cs`, `Services/Workflow/DocumentLifecyclePolicy.cs` |
| Export | `Export/ProformaInvoicePdfLayout.cs`, `PiPreviewParityRenderer.cs`, `ProformaChargeMetricsCalculator.cs`, `ProformaInvoiceExcelLayout.cs`, `ProformaInvoiceCsvLayout.cs`, `ProformaDossierPdfLayout.cs`, `ProformaDossierExcelLayout.cs`, `ProformaInvoiceListExcelLayout.cs` |
| Inquiry link | `Services/ProformaInvoice/InquiryIntegrationService.cs`, `InquiryLinkSync.cs`, `InquiryBoqConversion.cs` |

The wizard is not four steps. It has Steps 1 to 9 plus a saved invoice list, an
approval dialog and a versions dialog. Steps 4 to 7 are the middle of it.
**Verified fact** (inventory of `ViewModels/ProformaInvoice/**`).

---

## 3. Step 4. Surface, configuration and defaults

**Purpose, verified.** Step 4 is a pricing estimator that produces a rate per
square metre for a named glass build and writes that rate into the specification
catalogue, so Step 5 can select it. It is not an invoice editor and it holds no
customer, project or invoice number (`ActiveInvoiceContext.cs` says this
explicitly and exists only to stamp usage history).

**Surface, verified.**

| Element | Implementation | Note |
| --- | --- | --- |
| Four glass type cards | `GlassTypeCard.cs`, `PricingEstimatorViewModel.Part1.cs` | SGU, DGU, LAM, DGU+LAM |
| Per-type parameter panels | `SguParametersViewModel`, `DguParametersViewModel`, `LamParametersViewModel`, `DguLamParametersViewModel`, each with a `.SheetStore.cs` partial | Nested VMs owned by the estimator |
| Multiple calculation sections | `PricingEstimatorViewModel.Part1.cs` | Sections are captured individually by `CaptureSectionSnapshots()` and travel in the PI snapshot |
| Apply to catalogue | `ApplyRateAsync` in `Part2.cs` | Calls `ISpecificationsRepository.SaveOrUpdateAsync(glassType, specName, rate, ..., proformaId: null)` |
| Operation toggles | `PolishedEdges`, `DrillCutouts`, `CountersunkHoles`, `EdgeDeletionLayer` | Declared and reset to `false` at `Part1.cs:88-112` and `309-312`; no pricing expression and no XAML consumer reads them |

**The four operation toggles are dead surface. Verified fact.** They are not a
pricing input and must not be modelled as one.

**Defaults, verified.**

| Type | Defaults |
| --- | --- |
| SGU | 6 mm Clear FT, base 46.00 per m2, cutting 15.00, tempering 25.00, yield 0.85, margin 15 percent, polish "None (Standard)" |
| DGU | outer and inner both 6 mm Clear FT at 46.00, spacer 12 mm ASP with a suggested price from the sheet store, yield 0.85, margin 15 percent |
| LAM | outer and inner panes plus PVB rate and per-pane cut and temper |
| DGU+LAM | outer, laminated inner and third pane, spacer, interlayer, combined cut and temper |
| Colour lists | Sourced only from Sheet Store inventory (`SheetStorePricingService`, `SheetStockLinkService`, `SpecSheetRegistry`), never a hardcoded palette |
| Suggested spec name | `"{t} {colour} {treatment} Glass + {spacer} + {t} {colour} {treatment}[ extras] Glass"` via `GeneratedSpecName` |

**Gates, verified.** `ApplyRateAsync` refuses a blank specification name or a
rate at or below zero, and `_isApplyingRate` prevents re-entrancy while the save
is in flight. A yield at or below zero is caught by `SafeYield` and surfaced as
`YieldFactorWarning` rather than silently dividing.

---

## 4. Step 4. Pricing math, data flow and persistence

All rate formulas are in `PricingEstimatorViewModel.Part2.cs` (approximately
lines 490 to 760). **Verified fact.** `RecalculateEstimatedRate` dispatches to
`CalculateSguRate`, `CalculateDguRate`, `CalculateLamRate` and
`CalculateDguLamRate` from the basis values `SguBasis`, `DguBasis`, `LamBasis`,
`DguLamBasis`. Every result is wrapped in `MoneyRounding.Round`.

| Build | Formula (verified) |
| --- | --- |
| SGU | `(Base / Yield + Cutting + Tempering) * (1 + Margin/100)` |
| DGU | `((Outer + Inner) / Yield + Spacer) * (1 + Margin/100)` |
| LAM | `((Outer + Inner) / Yield + PVB + OuterCut + InnerCut + OuterTemp + InnerTemp) * (1 + Margin/100)` |
| DGU+LAM | delegated to `DguLamParametersViewModel.CalculateRate(out warning)`: `(Outer + LamOuter + LamInner) / Yield + TotalCut + TotalTemp + Interlayer + Spacer`, then margin |

Rounding policy, **verified**: `Services/Reporting/MoneyRounding.cs` sets
`DecimalPlaces = 2` and `Mode = MidpointRounding.AwayFromZero`, and the currency
is AED. There is no banker's rounding anywhere in the money path.

Data flow, **verified**:

```
Step 4 panel (per section)
  -> RecalculateEstimatedRate()            rate per m2, 2 dp, away from zero
  -> ApplyRateAsync()
       -> SpecificationsRepository.SaveOrUpdateAsync(
              glassType, specName, rate, ..., proformaId: null)
          persisted in specifications.db, not in the invoice row
  -> CaptureSectionSnapshots()
       -> Step4PricingSnapshot (all sections, with ComputeContentHash())
          persisted inside the PI's FullSnapshotJson
Step 5 group
  -> SelectedGlassType / SelectedSpecName / BaseRate / ImportedPrice
     plus LastSyncedStep4SpecName, LastSyncedStep4GlassType,
     LastSyncedStep4ContentHash and the three manual-override flags
```

Two persistence facts matter for the future contract. The catalogue write is
global (`proformaId: null`), so editing a rate in one invoice mutates the shared
catalogue. And the per-invoice copy is a snapshot, so two invoices can
legitimately hold the same specification name at different rates.
**Verified fact.**

Synchronisation is deliberate and one directional. Step 4 pushes a rate to
Step 5 only when the Step 4 content hash changed, and never over a manual Step 5
override. **Verified fact** (`ProformaInvoiceViewModel.cs` around lines 657 to
860, `SpecGroupSnapshot` fields at `ProformaInvoiceSnapshot.cs:391-431`).

---

## 5. Step 5. Specification groups and line items, field by field

**Verified fact**, from `SpecGroup.cs` and its partials, `GlassItemRow.cs` and
`ProformaInvoiceSnapshot.cs`.

| Field | Type | Written by | Meaning and notes |
| --- | --- | --- | --- |
| `GroupNumber` | int | operator | Position of the specification block on the invoice |
| `GroupName` | string | operator | Free text label |
| `SelectedGlassType` | string | Step 4 or operator | SGU / DGU / LAM / DGU+LAM |
| `SelectedSpecName` | string | Step 4 or operator | Catalogue name selected |
| `ImportedPrice` | decimal | Step 4 | The Step 4 reference rate as imported, kept separate from `BaseRate` because the operator may edit the latter |
| `BaseRate` | decimal | Step 4, catalogue, or operator | Rate per m2 below the surcharge threshold; a typed edit sets `HasManualStep5BaseRateOverride` |
| `SurchargePercent` | decimal | operator | Per specification input beside the base rate, default 0 |
| `SurchargeRate` | decimal, computed | `Round(BaseRate * (1 + SurchargePercent/100))` | Rate applied to a row at or above 4 m2 |
| `Step4EstimatedRatePerSqm` | decimal | Step 4 | Audit copy of the estimator output |
| `LastSyncedStep4SpecName` | string | sync | Provenance so a reopen cannot reclaim a manual edit |
| `LastSyncedStep4GlassType` | string | sync | As above |
| `LastSyncedStep4ContentHash` | string | sync | Fingerprint of the Step 4 section at last sync |
| `HasManualStep5SpecOverride` | bool | operator | Ownership flag |
| `HasManualStep5GlassTypeOverride` | bool | operator | Ownership flag |
| `HasManualStep5BaseRateOverride` | bool | operator | Ownership flag |
| `OuterGlassColor`, `InnerGlassColor`, `ThirdGlassColor` | string | sheet store | `ThirdGlassColor` is DGU+LAM only |
| sheet width, height, price | per side | sheet store | Used for material validity and P and L |
| `MaterialIsValid`, `MaterialInvalidReason` | bool? / string | integrity check | Nullable because pre Phase A snapshots lack it |
| `Step4SpacerPricePerSqm`, `Step4InterlayerCostPerSqm`, `Step4CuttingCostPerSqm`, `Step4TemperingCostPerSqm` | decimal? | Step 4 | Cost provenance for Step 9; nullable so Step 9 can fall back to the live panel |
| `FooterTotalSqm`, `FooterTotalSqm2`, `FooterTotalValue` | decimal, private set | recomputed | Per group footer, never stored as text |

Line item, `GlassItemRow`:

| Field | Type | Source | Notes |
| --- | --- | --- | --- |
| `SerialNumber` | int | generated | Display order |
| `GlassRef` | string | operator | Free text reference |
| `W1`, `H1` | int (mm) | operator | Outer pane, required |
| `W2`, `H2` | int (mm) | operator | Inner pane, zero for SGU |
| `Qty` | int | operator | Pieces |
| `Rate` | decimal | Step 5 base/surcharge or operator | Typed edit sets `SuppressRateAutoAdjust` |
| `PerimeterFactor` | int, default 2 | constant | Linear metre multiplier |
| `Sqm1`, `Sqm2` | decimal, computed | `SqmCalculator.CalculatePanel` | Billable panel area, 3 dp, 0.5 floor |
| `TotalSqm` | decimal, computed | `Round(Sqm1 * Qty, 3)` | Display and group totals |
| `Lm1`, `Lm2` | decimal, computed | `Round((W+H)/1000 * PerimeterFactor * Qty, 3)` | Feeds Step 6 linear metre charges |
| `Value` | decimal, computed | `MoneyRounding.Round(Sqm1 * Qty * Rate)` | Uses the panel area, not the displayed total |
| `SuppressRateAutoAdjust` | bool, internal | set when Rate is typed | Freezes the surcharge switch for that row |

Column order is fixed by `GridColumnIndex`: `GlassRef 0, W1 1, H1 2, W2 3, H2 4,
Qty 5, Rate 6`; everything after index 6 is computed.

---

## 6. Step 5. Calculations, rounding and rule classification

**Verified arithmetic.**

```
panel area     = W * H / 1e6, rounded 3 dp, floored at 0.50 m2 if positive, 0 if either side <= 0
total area     = Round(panel area * qty, 3)
linear metres  = Round((W + H) / 1000 * 2 * qty, 3)
row value      = Round(panel area * qty * rate, 2) away from zero
group rate     = if panel area >= 4 m2 then SurchargeRate else BaseRate (unless rate was typed)
surcharge rate = Round(BaseRate * (1 + SurchargePercent / 100), 2)
sheet area     = W * H / 1e6, rounded 4 dp, no floor (inventory only)
physical area  = W * H / 1e6, no floor, no rounding (P and L cost only)
```

The 4 m2 switch is implemented three times, at `SpecGroup.Part2.cs:327`,
`SpecGroup.Part2.cs:952` and `SpecGroup.Part3.cs:273`, and suppressed per row by
`GlassItemRow.SuppressRateAutoAdjust` (`GlassItemRow.cs:247,504`). **Verified
fact.** This is duplication, but it is intentional duplication with a shared
guard, so it is low risk. It is still three copies of one policy.

Classification of every pricing rule touched by Steps 4 to 7:

| Rule | Value | Class | Evidence |
| --- | --- | --- | --- |
| Minimum billable panel area | 0.50 m2 | Hardcoded constant | `SqmCalculator.cs:25` |
| Panel area decimals | 3 | Hardcoded | `SqmCalculator.CalculatePanel` |
| Sheet area decimals | 4 | Hardcoded | `CalculateSheetArea` |
| Surcharge threshold | 4 m2 | Hardcoded literal, three copies | `SpecGroup.Part2.cs:327,952`, `Part3.cs:273`, `GlassItemRow.cs:505` |
| Standard surcharge | 20 percent | Configurable per group, default 0 | `SurchargePercent`; the shop standard of 20 lives in website `pricingRules.ts` as a default, not in the desktop |
| Perimeter factor | 2 | Hardcoded, per row field | `GlassItemRow.PerimeterFactor` |
| Money rounding | 2 dp, away from zero | Hardcoded policy object | `MoneyRounding.cs:77,97` |
| SGU base, cut, temper, yield, margin | 46, 15, 25, 0.85, 15 | Hardcoded defaults, operator editable | Step 4 parameter VMs |
| DGU base pair, spacer, yield, margin | 46, 46, suggested, 0.85, 15 | Hardcoded defaults, operator editable | `DguParametersViewModel` |
| Catalogue rates | per specification | Database | `specifications.db` via `SpecificationsRepository` |
| Sheet colours and prices | per sheet | Database | Sheet store |
| VAT rate | 5 percent default, `IsVatApplicable` default true, setter clamped 0 to 100 | Configurable with documented legal basis | `InvoiceMainSummaryViewModel` `_vatPercent = 5m`, cited UAE Federal Decree-Law 8 of 2017 |
| Discount cap | not above net subtotal | Hardcoded guard | `Recalculate`, BUG-56 comment |
| Maximum discount percent | 50 | Hardcoded business threshold | `CrossStepValidator.MAXIMUM_DISCOUNT_PERCENT` |
| Quote validity | at least 1 day, warn above 365 | Hardcoded business threshold | `CrossStepValidator` |
| Delivery | 1 trip, hardcoded | Hardcoded technical debt | BUG-23 comment in `ProformaInvoicePdfLayout.cs`, `OtherChargeRow.FormulaDisplay` |
| Charge metrics in export | recomputed by a second implementation | Duplicated | `ProformaChargeMetricsCalculator` mirrors `OtherChargesViewModel.CalculatePolishLmForRow()` and `MetricDiscoveryHubViewModel.Refresh()` |
| Website re-implementation | 0.5 floor, 4 m2, 20 percent, 3 dp and 4 dp | Duplicated across repositories | `website/src/data/pricingRules.ts` |
| Operation toggles | none | Dead | `PricingEstimatorViewModel.Part1.cs:88-112,309-312` |

One money detail is worth stating plainly because a reconciliation test will
otherwise disagree with itself: the website rounds the billable panel to 4 dp
before multiplying (`round4` in `proforma-draft.ts`, `numeric(14,4)` columns),
while the desktop rounds to 3 dp (`SqmCalculator.CalculatePanel`). For a panel of
1234 by 567 mm the two areas differ in the fourth decimal, which changes a line
value whenever the rate is large enough. **Verified fact. Requires business
confirmation** of which precision is canonical, and until then the reconciliation
test must compare at 3 dp with a documented tolerance.

---

## 7. Step 6. Actual purpose

**Verified purpose.** Step 6 is the other charges step. It is not a payment step
and not a discount step. It builds the non glass revenue of the invoice from
four sub surfaces and hands one number, `TotalOtherCharges`, to Step 7.

| Surface | Implementation | What it contributes |
| --- | --- | --- |
| Targeted supplementary and fabrication charges | `OtherChargesViewModel` plus `OtherChargeRow` and `SpecTargetSelection` | Polish, bevel, drilling, tempering style charges applied to chosen specification groups |
| Wastages | `WastagesSectionViewModel` | Wastage rows whose quantity comes from the optimisation feed and from Step 5 metrics, not from a fixed percentage |
| Custom charges | `CustomChargesSectionViewModel` | Freeform named charges with their own unit and rate |
| Metric discovery | `MetricDiscoveryHubViewModel` | Discovers the quantities (SQM, LM, QTY, TRIP) a charge can be applied to, per specification |

Verified mechanics:

- `OtherChargeUnit` is `Sqm`, `Lm` or `Qty`; delivery is a fixed 1 trip.
- A row has an active flag, a suggested rate, a manual rate override
  (`HasManualRateOverride`, `ApplySuggestedRate`, `RestoreRate`) and a manual
  active override (`ApplySuggestedIsActive`, `RestoreIsActive`,
  `ClearManualActiveOverride`), so automation never overwrites a human decision.
- `UseAdvancedAutomation` defaults to `true`; manual mode stops the auto
  suggestions and keeps the rows the operator has edited.
- `ResultAmount` is metric quantity times rate, rounded through the money policy,
  and `FormulaDisplay` renders the exact expression shown to the operator.
- `TotalOtherCharges` is the sum of active rows across all four surfaces and is
  observed by Step 7 (`InvoiceMainSummaryViewModel.OnOtherChargesChanged`).

Step 6 therefore has three distinct sources of truth for a quantity (operator
input, Step 5 metrics, optimisation feed) and one aggregation point. That is the
right shape for the future module: a charge row must record which source produced
its metric, or the same charge will be recomputed differently on reload.

---

## 8. Step 7. Generation, numbering, status, save and reload

**What Step 7 is, verified.** `PrintPreviewViewModel` hosts four children:
`InvoiceMainSummaryViewModel` (money and VAT), `TermsAndConditionsViewModel`,
`PrintableInvoiceViewModel` (commands, exports, printing, saving) and a shared
`PrintLayoutViewModel` that drives the paper layout and theme.

**The money waterfall, verified** (`InvoiceMainSummaryViewModel.Recalculate`):

```
net          = GlassMaterialsSubtotal + OtherChargesSubtotal
discount     = percentage ? Round(net * value/100) : value
discount     = clamp(discount, 0, net)
beforeVat    = net - discount
vat          = IsVatApplicable ? Round(beforeVat * VatPercent/100) : 0
grand        = beforeVat + vat
```

`GlassMaterialsSubtotal` is pushed from
`DataGridViewModel.SummaryViewModel.GrandTotalValue`, which is itself
`MoneyRounding.Round` of the group sum. `RoundToGrandTotal` plus `ApplyRoundTo()`
back solves a discount so the grand total lands on a requested figure through
`InvoiceRoundTo.DiscountOnNet` and `InvoiceRoundTo.GrandFromDiscount`, and
refuses to round up. **Verified fact.**

**Numbering, verified**, in the order it happens:

1. New invoice: `CompanySetupViewModel.GenerateUniqueProformaIdAsync` seeds
   `PI-{year}-000` and calls `SuggestNextIdAsync`. On repository failure it logs
   a warning and falls back to `PI-{year}-{6 hex}`, which is a real degradation
   path, not a theoretical one.
2. `SuggestNextIdAsync` matches `^(?<prefix>.*?)(?<num>\d+)$`, increments the
   trailing number with `TryParse` overflow protection, keeps the original pad
   width, and scans the prefix family for the next free value. `PI-2026-001`
   becomes `PI-2026-002`; `INV-42` becomes `INV-43`; a bare `ABC` becomes
   `ABC-001`.
3. `InsertAsync` reallocates any unallocated seed (`^PI-\d{4}-000$`, blank or
   null) and retries up to five times on a unique violation, re-syncing the
   snapshot's `ProformaId` each time. A unique index on `ProformaId` is the real
   guard (`ProformaInvoicesDbContext.cs:55`).
4. Custom identifiers such as `INV-42` are legal and are preserved. **Verified
   fact.**

Consequence for the future: the desktop numbering space is not uniform. Any
contract that assumes `PI-YYYY-NNN` will reject legitimate rows.

**Status, verified.** `ProformaInvoiceStatus` defines `Draft`, `Sent`, `Approved`,
`Rejected`, `Expired`, `Cancelled`, plus an independent `IsVoid` flag with
`VoidedAtUtc`, `VoidedBy` and `VoidReason`. `StatusTransitions.Proforma` is the
authority for legal transitions:

| From | Allowed to |
| --- | --- |
| Draft | Draft, Sent, Approved, Rejected, Expired, Cancelled |
| Sent | Sent, Approved, Rejected, Expired, Cancelled |
| Approved | Approved, Rejected, Cancelled |
| Rejected | Rejected, Draft, Cancelled |
| Expired | Expired, Draft, Cancelled |
| Cancelled | Cancelled, Draft |

**Save paths, verified.** There are three, and they differ in risk:

| Path | Entry | Behaviour |
| --- | --- | --- |
| Manual save | `PrintableInvoiceViewModel.ExecuteSaveInvoiceAsync` | Duplicate detection dialog, `ProformaCreate` permission, duplicate id prompt with a suggested number, then insert or update, then stamps save artifacts |
| Silent draft save | `SilentDraftSaveAsync` | Runs on navigation, never throws and never shows a dialog, only when the invoice has a client name or a priced row, and refuses to rewrite a body that `DocumentLifecyclePolicy.CanEditBody` says is frozen |
| Revision copy | `SavedInvoicesListViewModel.Part1.cs` around 1131 and 1210 | Inserts a new row with a new number and `RootProformaId`, `RevisionNumber` |

**Reload, verified.** `ProformaInvoiceMapper.DeserializeSnapshot(entity)` returns
the snapshot from `FullSnapshotJson`, and `RestoreWizardFromSnapshot` pushes it
back into the wizard inside `BeginBatchUpdate()` and `EndBatchUpdate()` so the
money waterfall recalculates once instead of four times. `ProformaDuplicateSnapshotScrub`
cleans duplicated snapshot content on restore. Sheets and prices are re-primed by
`SheetStorePricingService.PrimeSyncSnapshot()`. **Verified fact.**

Persistence shape: one `ProformaInvoices` row per revision, carrying identity
columns plus `FullSnapshotJson`, `RowVersion` and `RevisionNumber`. Line items are
not rows in their own table. **Verified fact.**

---

## 9. Step 7. Print, export, approval and lifecycle transitions

**Verified export surface.**

| Output | Implementation | Notes |
| --- | --- | --- |
| On screen paper | `PrintLayoutViewModel` plus Step 7 views | Theme variants drive the look |
| PDF | `ProformaInvoicePdfLayout` with `PiPreviewParityRenderer`, QuestPDF | Deliberate WYSIWYG parity with the screen; the older five layout families were removed |
| Excel | `ProformaInvoiceExcelLayout` | Shares metrics through `ProformaChargeMetricsCalculator` |
| CSV | `ProformaInvoiceCsvLayout` | Line level |
| Dossier PDF | `ProformaDossierPdfLayout` | Multi document pack |
| Dossier Excel | `ProformaDossierExcelLayout` | As above |
| Saved invoice list Excel | `ProformaInvoiceListExcelLayout` | Register export |
| Step 5 cut list | `DataGridViewModel.ImportExport.cs`, `CutListGridIo.ExportPdfAsync` | Grid level, not the invoice |

Both PDF and Excel delegate metric aggregation to
`ProformaChargeMetricsCalculator`, which uses `SqmCalculator.CalculatePanel` so
the 0.5 m2 floor is applied once. That is a genuine single source of truth and
should survive the port. **Verified fact.** Delivery is hardcoded to one trip in
this calculator and in `OtherChargeRow.FormulaDisplay` (BUG-23, documented as
technical debt).

**Approval and lifecycle, verified.**

- `SavedInvoicesList/InvoiceApprovalViewModel.cs` with `ProformaStageChip.cs` and a
  versions dialog drive the human approval flow.
- `DocumentLifecyclePolicy` is the single authority for three questions:
  `CanHardDelete` (only Draft, Sent, Rejected, Expired for a PI),
  `CanEditBody` (false once posted) and `VoidTargetStatus`.
- `ProformaLifecycleSync.AfterDownstreamChangeAsync` runs only from downstream
  deletes. If no job order, delivery note or tax invoice remains, it calls
  `MarkCancelledAsync`, which writes status `Rejected` and stamps the snapshot as
  `Rejected`, while the list renders that as Cancelled.
- `VoidAsync` is a different operation: it sets `IsVoid`, `VoidedAtUtc`,
  `VoidedBy`, `VoidReason` and writes `VoidTargetStatus` (Cancelled).

So the desktop has two, non equivalent, cancellation mechanisms and one of them
does not mean what its name says. **Verified fact.** Any shared status contract
must model "cancelled because the pipeline was dismantled" and "voided by a user"
as different events, or the audit trail is lost.

`InvoiceLifecycleStateMachine` is a third vocabulary (`Draft`, `PendingApproval`,
`Approved`, `Sent`, `Confirmed`, `Cancelled`, `Rejected`) described as a pure
function with no side effects. It is not the table used by
`DocumentLifecyclePolicy` or by `StatusTransitions`. **Verified fact.**

---

## 10. End to end trace, Step 4 to Step 7

| # | Producer | Artefact | Stored where | Consumer |
| --- | --- | --- | --- | --- |
| 1 | Step 4 estimator | rate per m2 for a named build | `specifications.db` via `ISpecificationsRepository` | Step 5 spec selection |
| 2 | Step 4 sections | `Step4PricingSnapshot` list plus content hash | inside PI `FullSnapshotJson` | Step 5 sync, Step 9 costing, reload |
| 3 | Step 5 group | `BaseRate`, `SurchargePercent`, `SurchargeRate` | snapshot | `GlassItemRow.RecalculateAll` |
| 4 | Step 5 row | dimensions, qty, rate, computed SQM, LM, value | snapshot | group footers, Step 6 metrics, Step 7 |
| 5 | Step 5 summary | `GrandTotalValue` | in memory, then snapshot | Step 7 `GlassMaterialsSubtotal` |
| 6 | Step 6 | charge rows, wastage rows, custom rows | snapshot | `TotalOtherCharges` |
| 7 | Step 7 summary | net, discount, VAT, grand | `InvoiceMainSummary` properties, then entity columns and snapshot | print, export, approval |
| 8 | Save | `SavedProformaInvoice` row with number, status, totals, snapshot, `RowVersion` | `ProformaInvoices` SQLite table | list, approval, downstream documents, exports |
| 9 | Approval and downstream | status transitions, job order, delivery note, tax invoice | their own tables | `ProformaLifecycleSync`, P and L |
| 10 | Attachment step | `ProformaAttachmentRecord` | attachment store keyed by `ProformaId` | dossier export, approval gate |

Two breaks in the chain are worth flagging now. First, step 1 writes to a
catalogue outside the invoice transaction, so a failed save can still have
changed the shared catalogue. Second, step 8 is the only durable point; nothing
between steps 2 and 7 is recoverable after a crash except through the silent
draft save, and that is deliberately suppressed for frozen documents.
**Verified fact**, with the consequence that the future module needs an explicit
draft journal rather than relying on navigation saves.

---

## 11. ADV desktop versus website PI

| Dimension | ADV desktop (`10f56b9`) | Website (`website/` at the same commit) |
| --- | --- | --- |
| Purpose | Full quotation and ERP document | Line entry workspace for an inquiry |
| Steps or screens | 9 steps plus register, approval, versions | One workspace, two tabs (builder, specification) |
| Glass catalogue | Step 4 estimator writes `specifications.db` | None |
| Line model | Groups plus rows plus dimensions, colours, sheet prices, cost provenance | `inquiry_proforma_lines`: group, line, ref, w1/h1/w2/h2, qty, rate |
| Minimum billable area | 0.5 m2, 3 dp (`SqmCalculator`) | 0.5 m2, 4 dp (`pricingRules.ts`, `numeric(14,4)`) |
| Surcharge switch | 4 m2, per group percent, suppressible per row | 4 m2, per group percent, 20 percent standard exposed in the preview |
| Other charges | Full engine, four surfaces, optimisation feed | None |
| Discount | Percentage or fixed, capped at net, round to target | None |
| VAT | Switchable, default 5 percent, applied to net after discount | None |
| Rounding | 2 dp away from zero, `MoneyRounding` | `lineValue` uses integer scaling, 2 dp, half up |
| Numbering | `PI-YYYY-NNN` increment, custom ids allowed, seed reallocation, hex fallback | `PI-WEB-YYYY-NNNNNN` from `inquiry_proforma_number_seq` |
| Identity | `ProformaId` string, `Id` int, `InquiryId` int?, unique index | `id` uuid, `proforma_number` unique, `inquiry_id` uuid |
| Revisions | New row per revision, `RootProformaId` plus `RevisionNumber` | Same row, `revision` integer plus `inquiry_proforma_saves` ledger |
| Concurrency | `RowVersion` optimistic check in `UpdateAsync` | `expected_revision` plus `FOR UPDATE`, `40001` on mismatch |
| Status vocabulary | `Draft, Sent, Approved, Rejected, Expired, Cancelled` plus `IsVoid` | `Draft, Issued, Void`, of which only `Draft` is ever written |
| Approval | Explicit dialog and lifecycle policy | None |
| Print and PDF | QuestPDF with screen parity, Excel, CSV, dossier | None |
| Attachments | Step 8, dossier export | Intake documents only, not PI attachments |
| Persistence | SQLite, one row with a JSON snapshot | Postgres tables, normalised lines |
| Access control | `AppPermissions` inside the service layer, workstation operator, no real authentication | `inquiry_admin` writes, `inquiry_auditor` reads, RPC `SECURITY DEFINER`, RLS |
| Offline | Fully offline | Requires Supabase |
| Inquiry link | `InquiryId` int, `InquiryIntegrationService` eligibility, one PI per inquiry | `inquiry_id` uuid, any non tombstoned inquiry |
| Test coverage | xUnit around `SqmCalculator`, `CrossStepValidator` waterfall, inquiry integration | vitest suite, 282 tests including proforma contract tests |

**Verified fact**: the two systems share the 0.5 m2 floor, the 4 m2 switch, the
`PI-` prefix convention and the word "proforma". They share no record, no schema,
no identifier and no user account, and they differ on area precision, totals,
VAT, charges, status vocabulary and revision semantics.

**Do not** describe them as equivalent implementations of one feature. They are
two products that will be joined by an explicit contract.

---

## 12. Inquiry identity model

Five origins exist today. **Verified fact** for each identifier shape.

| Origin | Where the record lives | Identifier | Secondary identifier | Link to anything else |
| --- | --- | --- | --- | --- |
| Website submission | `public.inquiry_customer_submissions` | `id` uuid | `submission_reference` `WEB-YYYY-NNNNNN` | Documents in `inquiry-intake-documents`; delivery ledger rows |
| Website inquiry row | `public.inquiries` | `id` uuid | `inquiry_number` `WEB-YYYY-NNNNNN` | Project, customer text, estimator assignments |
| WPF 3.0 local inquiry | `inquiries.db` `Inquiries` | `Id` int | `InquiryNumber` `INQ-YYYY-NNNN` | `WebSubmissionId`, `WebSubmissionReference`, `ProformaInvoiceId`, `ProformaNumber`, `RowVersion` |
| Desktop published inquiry | website `inquiries` row written by the bridge | same uuid | `WEB-...` reference, `InquiryReference = "Website · " + type` | `inquiry_inbox_items` ledger entry in the desktop |
| ADV desktop PI | `ProformaInvoices` SQLite | `Id` int | `ProformaId` `PI-YYYY-NNN` | `InquiryId` int into the local inquiry table |
| Website PI | `public.inquiry_proformas` | `id` uuid | `proforma_number` `PI-WEB-YYYY-NNNNNN` | `inquiry_id` uuid, `source_system = 'website'` |

Structural facts that constrain the design:

1. `InquiryRecord` in 3.0 already carries `ProformaInvoiceId`, `ProformaNumber`,
   `WebSubmissionId` and `RowVersion`. **Verified fact**
   (`Inquiry/Models/InquiryRecord.cs:69-82`).
2. `inquiry_proformas` already anticipates desktop publication: `source_system`
   is `website` or `desktop`, `source_proforma_number` exists, and a unique index
   on `(source_system, source_proforma_number)` prevents a duplicate number per
   origin. **Verified fact** (`website/supabase-inquiry-proforma-phase15.sql:17-48`).
3. The check constraint `inquiry_proformas_source_identity` requires
   `inquiry_id IS NULL` for desktop rows. That makes a desktop PI unable to point
   at the inquiry it came from, even though the desktop knows
   `WebSubmissionId`. **Verified fact.** This is the central defect: it forces
   either a duplicate record or an unlinkable one.
4. No `WEB-` to `INQ-` join exists in either database. The only bridge is
   `Inquiries.WebSubmissionId` in the desktop file. **Verified fact.**
5. `admin` visibility of submissions is delivered and working, and there are 8
   submissions and 0 proforma rows in production as of 2026-10-24.
   **Verified fact** (live counts).

The identity model must therefore be: one inquiry, one website row, one local
desktop row, and a PI that can be recognised by origin without needing a name,
project or phone match. Anything else violates the standing rule that records are
never matched on unreliable text fields.

---

## 13. Canonical PI identity and numbering contract

**Verified constraints that cannot be changed silently:**

- Desktop numbers such as `PI-2026-001` exist in customer SQLite files and are
  quoted to customers. Changing their shape would rewrite history.
- The website allocates `PI-WEB-YYYY-NNNNNN` from one global sequence with no
  per year reset, so the six digit tail is global, not annual. Reusing the
  sequence for desktop rows would interleave the two origins and destroy the
  meaning of the prefix.
- The website table's check pattern accepts only `^PI-(WEB|DSK)-[0-9]{4}-[0-9]{6}$`
  for `proforma_number`. A raw desktop `PI-2026-001` cannot be stored there.
  **Verified fact** (`phase15.sql:20-21`).

**Proposed design** (nothing below is implemented):

| Concept | Rule |
| --- | --- |
| Canonical PI identity | `inquiry_proformas.id` uuid, for both origins |
| Origin | `source_system`, `website` or `desktop`, immutable after insert |
| Public document number | `proforma_number`, always `PI-WEB-...` or `PI-DSK-...`, always allocated by the server |
| Desktop's own number | kept verbatim in `source_proforma_number`, never rewritten, never used as an identity |
| Allocation | `PI-WEB` from the existing sequence; a separate `inquiry_proforma_desktop_number_seq` for `PI-DSK`, so a quiet year or a busy desktop does not disturb the other origin |
| Year component | the year of first publication, from the server clock, never from the client |
| Uniqueness | `(source_system, source_proforma_number)` for desktop, `(inquiry_id)` where `source_system = 'website' and status = 'Draft'` for website |
| Reuse | a number is never reused, including after void, including after a failed publication that already allocated |
| Custom desktop ids | `INV-42` and the hex fallback are stored as `source_proforma_number` unchanged; no parse, no normalisation |
| Inquiry linkage | desktop rows must be allowed to carry `inquiry_id`, resolved on the desktop from `WebSubmissionId`, never from customer or project text |

Two consequences must be stated to the business before implementation:

1. A customer will see two numbers for one commercial document if the desktop
   quotes `PI-2026-014` and the portal shows `PI-DSK-2026-000031`. **Requires
   business confirmation**: either accept the dual number with both printed on
   the PDF, or adopt `PI-DSK` as the primary. The second option changes the
   desktop's printed history, which the standing rule forbids without an explicit
   decision.
2. Relaxing `inquiry_proformas_source_identity` is a schema change on a table with
   zero rows today. That is the cheapest moment it will ever be. Deferring it
   guarantees a backfill later.

**Do not** attempt to make `inquiries.proforma_number` (the text column the
bridge writes) authoritative. The existing contract document already forbids it
and the source confirms that column is a display reference only.

---

## 14. Status and lifecycle contract

Three vocabularies exist. **Verified fact.**

| Vocabulary | States | Where | Used for |
| --- | --- | --- | --- |
| `ProformaInvoiceStatus` | Draft, Sent, Approved, Rejected, Expired, Cancelled, plus `IsVoid` | `Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs` | Persisted `Status` column, lists, chips |
| `InvoiceLifecycleState` | Draft, PendingApproval, Approved, Sent, Confirmed, Cancelled, Rejected | `InvoiceLifecycleStateMachine.cs` | Pure transition helper, not the authority for persistence |
| `StatusTransitions.Proforma` | the six above | `Services/Workflow/StatusTransitions.cs:68-77` | The actual gate used by `DocumentLifecyclePolicy` and repositories |
| Website | Draft, Issued, Void | `phase15.sql:26-27` | Only `Draft` is ever written |

Two desktop semantics that must not be collapsed:

| Operation | What it writes | What it means |
| --- | --- | --- |
| `VoidAsync` | `IsVoid = true`, `VoidedAtUtc`, `VoidedBy`, `VoidReason`, status `Cancelled` | A human voided the document; retrievable and attributable |
| `MarkCancelledAsync` | status `Rejected`, snapshot status `Rejected` | The pipeline that justified the PI was dismantled; the register displays Cancelled |

**Proposed canonical contract** for the shared model. It is additive and does not
rename any existing desktop status:

| Canonical state | Desktop source | Website storage | Meaning |
| --- | --- | --- | --- |
| `Draft` | `Draft` | `Draft` | Editable, unpriced commitment |
| `Issued` | `Sent` or `Approved` (the desktop has no single issued concept) | `Issued` | Given to the customer; body frozen |
| `Accepted` | `Approved` when the customer confirms, or inquiry `Confirmed` | `Issued` plus an inquiry confirmation | Customer committed |
| `Rejected` | `Rejected` or `Expired` | `Void` with a reason | Not proceeding |
| `Cancelled` | `Cancelled` or `IsVoid` | `Void` with a reason | Withdrawn |

Open questions that the source cannot answer and that must be decided, not
guessed. **Requires business confirmation**:

1. Is `Sent` the same event as `Issued`, or is issuing a separate act?
2. Does `Approved` mean internal approval or customer acceptance? The desktop
   uses it for both in different comments.
3. When a PI becomes `Void` on the website, must the desktop's PI be frozen, and
   vice versa? A shared rule must be one directional and explicit.
4. Is `Expired` computed from `ValidUntil` or set by a user?

Until those are answered, the publication contract must carry the desktop status
verbatim in a `source_status` field alongside a mapped canonical state, and the
website must display the mapped state with the source status available on hover
or in a detail line. That keeps the desktop's vocabulary intact while giving the
website one vocabulary.

---

## 15. System of record

| Option | What it means | Verdict |
| --- | --- | --- |
| A. Supabase is the only record | Both apps read and write `inquiry_proformas` | Rejected. The desktop is offline first with a local SQLite document model, 9 step snapshots and 6 export layouts. Forcing it online breaks quoting in a workshop with no connectivity, and the desktop has no authenticated operator identity to stamp writes |
| B. SQLite is the only record | Website reads the desktop through a service | Rejected. The website PI is created by an admin inside a browser session with no desktop present, and the desktop file is per workstation, not per company |
| C. Hybrid by ownership plus explicit publication | Desktop owns desktop documents and numbering; website owns its own drafts and the shared read model; a publication step replicates a finished desktop PI into `inquiry_proformas` with idempotency and conflict detection | Recommended |

**Proposed design, with reasoning from the source:**

1. The desktop document is already a self contained snapshot row with
   `RowVersion`, `RevisionNumber` and `RootProformaId`. It is a complete record, so
   it can be published without needing the website to recompute anything.
2. The website cannot compute the desktop's money: it has no charges, no discount
   and no VAT columns. Publishing the desktop's computed totals plus a JSON
   summary is the only way to be faithful.
3. The website cannot authenticate a desktop user. `CurrentOperator` in 3.0 is the
   Windows account name, and the ADV desktop uses `AuditService.CurrentUsername`
   plus `AppPermissions`. Publishing therefore must happen through one dedicated
   bridge identity with an enabled `inquiry_admin` role, and the real operator
   name must travel as data, not as an identity claim to be trusted for access.
4. One direction per artefact:
   - Website to desktop: inquiry intake only, which already exists.
   - Desktop to website: PI publication and status events only.
   - Website PI drafts are never pushed into the desktop, because the desktop
     cannot price them without Step 4 and Step 5 context.

So: the desktop is the system of record for a desktop PI; Supabase is the system
of record for a website PI and for the shared discovery view; publication is a
one way, idempotent, revisioned replication event. A record is never "the same"
PI in two places; it is one document with one canonical id and one origin.

---

## 16. V3.0 PI module architecture and the Inquiry page role

**Current 3.0 state, verified**: `Inquiry/` holds intake, register, editor,
inbox client, ingest, lifecycle snapshot and SQLite migrations 1001 to 1005.
There is no PI code. `InquiryLifecycleSnapshot` reserves `ProformaRowId`,
`ProformaNumber`, `ProformaStatus`, `ProformaUpdatedAt` and states that the link
is written by `SetProformaLinkAsync` "once the Proforma module lands".
`InquiryRecord` already has `ProformaInvoiceId` and `ProformaNumber` columns.

**Proposed module layout** (design only):

```
Inquiry/Models/Proforma
  ProformaInvoice.cs          Id int, PublicId Guid, ProformaNumber, RootProformaId,
                              RevisionNumber, InquiryId, WebInquiryId, Status,
                              SourceStatus, totals, RowVersion, IsVoid fields
  ProformaInvoiceLine.cs      InvoiceId, GroupNumber, LineNumber, GlassRef,
                              W1..H2, Qty, Rate, SortOrder
  ProformaCharge.cs           invoice scoped charges with unit and metric source
  ProformaSnapshot.cs         the full step 4 to 7 JSON, versioned
Inquiry/Services/Proforma
  IProformaRepository.cs      CRUD, revision chain, optimistic concurrency
  ProformaPricing.cs          pure port of SqmCalculator plus MoneyRounding plus
                              the 4 m2 switch, one implementation only
  ProformaNumberAllocator.cs  INQ style seed, unique retry, no parsing of custom ids
  ProformaLifecycle.cs        transitions, void vs cancel, freeze policy
  ProformaPublisher.cs        opt-in publication to Supabase, idempotent
  ProformaExport.cs           one renderer, parity with the screen
Inquiry/Services/Database/SchemaMigrations
  1006  ProformaInvoices, ProformaInvoiceLines, ProformaCharges, indexes, RowVersion
```

Non negotiable properties, each traceable to a fact above:

1. **One pricing implementation.** `ProformaPricing` must be the only place that
   applies the 0.5 m2 floor, the 3 dp area, the 4 m2 switch and the 2 dp away from
   zero money rounding. The ADV source proves that duplicating these produces
   drift (three copies of the switch, a second charge metric calculator, and a
   fourth decimal disagreement with the website).
2. **`PublicId` from the first insert**, so a PI created offline can later be
   published under a stable identity without renumbering. This mirrors the
   website uuid and avoids a second identity scheme.
3. **Totals are stored, not derived, at save time.** A published PI must be
   readable without the desktop's Step 4 catalogue.
4. **`SourceStatus` plus `Status`.** Store the desktop's own word and the mapped
   canonical state, so a future status decision does not rewrite history.
5. **Publication is explicit and manual first.** A "Publish to portal" action with
   a preview of exactly what will be sent, then the same action idempotent on
   retry. No background auto publish in the first pass, matching the standing rule
   that nothing is auto accepted.

**The Inquiry page role.** The Inquiry page stays the intake and constellation
view: what arrived, from where, with which documents, assigned to whom, and what
downstream artefact exists. It must not become a second PI editor.

- Creating a PI from an inquiry obeys the existing gate: `Prepared` or
  `Confirmed`, complete BOQ, and no existing PI for that inquiry.
- The register shows one PI chip per inquiry, reading
  `ProformaNumber` plus the canonical state, never a colour invented per screen.
- "Open Proforma Invoice" navigates to the PI module with the specific PI id; it
  does not open an inline form.
- The inquiry record never stores money. `InquiryValue` stays the estimate the
  operator typed; the PI is the commercial figure.

---

## 17. Security and authorization matrix

Existing controls, **verified**:

| Layer | Control | Evidence |
| --- | --- | --- |
| Desktop service layer | `OperationGuard.Demand(AppPermissions.ProformaCreate/ProformaEdit/ProformaDelete)`, `ReadOnlyGate.Demand` | `ProformaInvoiceRepository` insert, update, void, cancel |
| Desktop UI | Permission check before save, duplicate detection, freeze via `DocumentLifecyclePolicy` | `PrintableInvoiceViewModel.Part1.cs` |
| Desktop identity | Windows workstation account, no password, no roles | 3.0 `CurrentOperator`, ADV `AuditService.CurrentUsername` |
| Website API | 401 unauthenticated, 403 wrong role, 404 unknown inquiry, 409 revision conflict, 400 invalid, 503 not configured | `src/app/api/inquiries/[id]/proforma/route.ts`, `proforma-api.ts` |
| Website read | `inquiry_admin`, `inquiry_auditor` via `has_inquiry_role` policy | RLS policies in `phase15.sql` |
| Website write | `inquiry_admin` only, `SECURITY DEFINER` RPC, `FOR UPDATE`, server side totals, delete then reinsert lines | `save_inquiry_proforma_v1` |
| Website grants | `REVOKE ALL ... FROM PUBLIC, anon`; only `SELECT` to `authenticated`; sequence revoked from everyone | `phase15.sql:97-106` |
| Bridge | Password grant with an enabled admin account, Bearer token, `Content-Profile: public` | `InquiryInboxClient.cs` |

Proposed matrix for the future contract:

| Operation | Desktop operator | Website admin (`inquiry_admin`) | Website auditor (`inquiry_auditor`) | Bridge account | anon |
| --- | --- | --- | --- | --- | --- |
| Create a desktop PI | yes, `ProformaCreate` | not applicable | not applicable | no | no |
| Edit a desktop PI body | yes, only while `CanEditBody` | no | no | no | no |
| Approve a desktop PI | yes, `ProformaEdit` plus approval dialog | no | no | no | no |
| Publish a desktop PI | trigger, `ProformaEdit` | no | no | transport only, `inquiry_admin` role required | no |
| Read a published PI | yes, local | yes | yes | yes | no |
| Create or edit a website PI | no | yes | no | no | no |
| Void a website PI | no | yes, with a reason | no | no | no |
| Read the delivery ledger | yes, local | yes | yes | yes | no |
| Delete a published PI | no, void only | no, void only | no | no | no |

Gaps that must be closed or explicitly accepted:

1. **The desktop cannot prove who published.** With one bridge account, every
   publication stamps the same `created_by`. **Requires business confirmation**
   whether per operator attribution in the website is needed; if it is, the
   desktop must send an operator name as data and the website must store it in a
   separate `published_by_operator` column, never as an identity.
2. **The desktop has no role model.** Any `AppPermissions` check is a local
   convention, bypassable by editing the local file. The database policy is the
   only real gate, which is why publication must never widen access.
3. **`MarkCancelledAsync` demands `ProformaDelete`, not `ProformaEdit`.** So the
   role that can dismantle the pipeline can also cancel a PI without edit rights.
   That is a documented deliberate choice, and it must be mirrored in the
   published status event, not silently re-derived on the website.
4. **No `service_role` anywhere in desktop or browser.** Unchanged, and the
   publication RPC must be `SECURITY DEFINER` with `search_path` pinned,
   `REVOKE ALL ... FROM PUBLIC, anon`, `GRANT ... TO authenticated`, and the role
   check inside the function body.

---

## 18. Save, concurrency and recovery contract

Current behaviour, **verified**:

| Concern | Desktop | Website |
| --- | --- | --- |
| Concurrency token | `RowVersion` int, checked in `UpdateAsync(invoice, expectedRowVersion)` | `revision` integer, `expected_revision` argument |
| Conflict signal | Repository result object, surfaced by the caller | `40001` mapped to HTTP 409 with the copy "This proforma changed since it was loaded" |
| Save atomicity | One row, whole JSON snapshot, `SaveWithRetryAsync` | One transaction, header update, lines deleted and reinserted, `inquiry_proforma_saves` ledger row |
| Duplicate save | `Idempotency-Key` at the API, replay returns the same proforma id | Same |
| Draft recovery | Silent draft save on navigation, blocked for frozen documents | Every save is durable; no separate draft |
| Line history | None, lines live in the JSON snapshot | None, lines are replaced per save |
| Delete | Hard delete only in pre posting states, otherwise void | No delete, `Void` status exists but is never written |
| Crash recovery | Row level only, mid wizard work is lost unless the silent save ran | Not applicable |
| Clock | `DateTime.Now` local | `now()` server |

**Proposed contract:**

1. The desktop gains a **draft journal**: a small append only table
   (`ProformaDraftJournal`) written on every priced change, debounced, containing
   `PublicId`, `RevisionNumber`, `ContentHash` and the snapshot JSON. Recovery on
   start offers the newest journal entry that is newer than the saved row.
   Without it, the loss window is a whole wizard session.
2. Publication carries `source_revision` (the desktop `RevisionNumber`),
   `source_content_hash` (a hash of the published payload) and an
   `Idempotency-Key` derived from `PublicId + source_revision + content_hash`.
   A retry with the same three values returns the same canonical row.
3. Conflicts are never resolved by last write. A publication whose
   `expected_revision` does not match returns a conflict with both revisions, and
   the desktop shows the difference. Never delete website lines on a stale
   revision.
4. A published revision is immutable. A correction is a new revision, which
   matches the desktop's own new row per revision model and gives the website a
   real audit trail instead of an overwrite.
5. Money is never recomputed on the website from published lines when the source
   is desktop. The published totals are authoritative for that origin, and the
   website must not "fix" them with its own rules.
6. Recovery for a failed publication is a local outbox: publication attempts are
   queued with their idempotency key, retried on reconnect, and only removed on a
   2xx or a terminal 4xx that the operator has seen.

---

## 19. Database and API change proposal

Design only. No migration was created or applied. Names follow the established
conventions: SQL files in `website/` named `supabase-<phase>.sql`, functions
`SECURITY DEFINER` with pinned `search_path`, revoke from `PUBLIC, anon`, grant to
`authenticated`, authorization inside the body.

**Schema, additive:**

| Change | Statement shape | Why |
| --- | --- | --- |
| Allow desktop rows to carry the inquiry | Replace `inquiry_proformas_source_identity` with a version that permits `source_system = 'desktop'` with a non null `inquiry_id` or a null one, and requires `source_proforma_number <> ''` | The current constraint forbids the only join the business needs. Zero rows today, so the change is free |
| Publication provenance | `ADD COLUMN published_at timestamptz`, `published_by uuid`, `published_by_operator text`, `source_revision integer`, `source_content_hash text`, `source_status text` | Attribution without trusting a client identity, and conflict detection |
| Desktop summary | `ADD COLUMN summary jsonb NOT NULL DEFAULT '{}'` on `inquiry_proformas` | The website has no columns for charges, discount, VAT or round to. A faithful copy needs them, and JSON keeps the table stable while the desktop evolves |
| Revision immutability | `ADD COLUMN superseded_by uuid REFERENCES inquiry_proformas(id)` | A correction is a new row, not an overwrite |
| Desktop number sequence | `CREATE SEQUENCE inquiry_proforma_desktop_number_seq` | Keeps `PI-DSK` allocation independent of `PI-WEB` growth |
| Line provenance | `ADD COLUMN unit text`, `metric_source text`, `sort_order integer` on `inquiry_proforma_lines` | Charges and their source metric were the repeated source of drift in Step 6 |

**API, additive, all admin gated:**

| Function | Purpose | Returns |
| --- | --- | --- |
| `allocate_desktop_proforma_number_v1()` | Server side `PI-DSK-YYYY-NNNNNN` allocation, never client supplied | `text` |
| `publish_desktop_proforma_v1(p_proforma uuid, p_inquiry_id uuid, p_source_proforma_number text, p_source_revision integer, p_source_content_hash text, p_source_status text, p_operator text, p_summary jsonb, p_lines jsonb, p_idempotency_key uuid)` | Insert or supersede a published revision. Idempotent on the key, `40001` on stale revision, server side totals | `uuid, integer` |
| `void_published_proforma_v1(p_proforma uuid, p_reason text)` | Writes `Void` with a reason, never deletes | `void` |
| `get_proforma_publication_state_v1(p_inquiry_id uuid)` | What the website holds for this inquiry, including the revision cursor, for the desktop to compare before publishing | row set |
| `list_proforma_saves_after_v1(p_cursor bigint, p_limit integer)` | Uses the existing, populated and currently unused `inquiry_proforma_saves` table as an incremental read cursor | row set |

**Explicitly not proposed:** any change to `save_inquiry_proforma_v1`, any change
to the delivery ledger or the existing inbox RPCs, `service_role` in any client,
public buckets, new roles, or broadened select policies. Desktop read access stays
read only and goes through the existing admin or auditor gate.

---

## 20. Test, acceptance and phased plan

**Test plan, by layer. Every item is new work, none exists.**

| Layer | Test | Asserts |
| --- | --- | --- |
| Pricing parity | Shared fixture of 40 panels, run through `SqmCalculator` and `pricingRules.ts` | Same 3 dp area and same line value after rounding, or a documented, single, tolerance |
| Pricing parity | Panels at 0.49, 0.50, 0.51, 3.99, 4.00, 4.01 m2 | Floor and surcharge switch agree on both sides |
| Money | `MoneyRounding` versus `lineValue` on half cent cases | Both round away from zero, never banker's |
| Numbering | Seed, increment, collision retry, custom id, hex fallback, `PI-DSK` allocation | No number reused, no parse of a custom id |
| Lifecycle | Transition table enumeration | Illegal transitions rejected in both apps |
| Lifecycle | Void versus cancel | The two events stay distinguishable in storage and in the UI |
| Publication | Same key twice | One row, one revision, same id |
| Publication | Stale `expected_revision` | `40001`, no line deletion, no overwrite |
| Publication | Desktop PI for a website inquiry | Exactly one canonical row, linked by `WebSubmissionId`, not by name |
| Access | Anon, auditor, admin, disabled admin, bridge disabled | 401, 403 and success as specified, no anon read |
| Access | Desktop with the bridge disabled | Publication fails closed with a clear message |
| Recovery | Kill the app mid wizard | Draft journal restores the newest priced state |
| Export | Published PI rendered on the website | Totals match the desktop PDF to the fils (0.01 AED), no recomputation |
| Regression | Existing website suite plus desktop xUnit suite | No behaviour change in inquiry intake or inbox delivery |

**Acceptance criteria**, stated so they can fail:

1. One inquiry has at most one canonical PI per origin, and a desktop PI created
   from a website inquiry is linked, not duplicated.
2. Every number is allocated by a server or by the owning app, never by a client,
   and is never reused.
3. The same published revision renders the same total in the desktop PDF and in
   the website, to the fils (0.01 AED).
4. No status is inferred from a human readable string, and void is not the same
   event as cancel.
5. An auditor can read everything and write nothing. An anonymous request reads
   nothing. No client holds `service_role`.
6. A failed or offline publication is retried without creating a second document.
7. Every published document can be traced to the operator and to the revision that
   produced it.

**Phases. Approved work stops at the end of this report; nothing below has begun.**

| Phase | Scope | Exit criterion |
| --- | --- | --- |
| C. Contract and schema | Freeze the numbering, status mapping and publication semantics; write `supabase-inquiry-proforma-phaseB.sql` with the additive changes and the new functions; apply it; prove with rolled back probes as an enabled admin and as an auditor | Documented contract, applied migration, access probes recorded |
| D. V3.0 local PI module | Port the pricing core once, add migrations 1006 and up, the repository, the lifecycle service, the draft journal, one export renderer, and the Inquiry page chip and navigation | A PI can be created from an inquiry, saved, reopened, revised and exported, offline |
| E. Publication and consumption | Desktop publisher with an outbox and idempotency; website read of published revisions with no recomputation; conflict UI; publication state comparison before publishing | A desktop PI appears once on the website, a correction appears as a new revision, and a stale retry does not overwrite |
| F. Parity and hardening | Cross repository parity tests, VAT and charges on published documents, print and PDF acceptance, access matrix proofs, recovery drill, live verification | All acceptance criteria above pass on the live deployment |

**Requires business confirmation before phase C can close**, collected in one
place: the dual number question (section 13), the `Sent` versus `Issued`
question and the `Approved` meaning (section 14), whether the website displays
VAT and charges at all (section 19), whether per operator attribution is required
(section 17), and whether the 4 m2 switch and the 20 percent standard are policy or
local practice (section 6).

**Unverified in this report**: rendered PDF fidelity, runtime behaviour of the
wizard, the contents of any customer SQLite file, and whether the desktop
publication path can authenticate in production. Those require execution, which
Phase B does not permit.
