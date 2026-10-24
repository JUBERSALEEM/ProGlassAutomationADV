# Phase B review, architecture validation and Phase C readiness

Review-only document. No source file, SQL schema, migration or production row was
changed while producing it. No branch was merged, no commit was pushed and nothing
was deployed. The Phase B report at
`website/docs/PHASE_B_PI_AUDIT_AND_ARCHITECTURE.md` is left exactly as it was.

Labels used throughout:

- **Verified fact**: read from source or from a read-only database query during this review.
- **Recommendation**: a proposed decision, not implemented anywhere.
- **Assumption**: believed but not provable from the repositories.
- **Requires business approval**: changes money, numbering, access or customer-visible behaviour.
- **Not verified**: could not be executed or observed in this environment.

---

## 1. Executive summary

**Verdict.** The Phase B inventory is accurate and worth keeping. Two of its
architectural conclusions are wrong, one is materially overstated, and its central
recommendation (hybrid ownership) survives only after the identity mechanism is
rebuilt around the publication path that already exists.

**The finding that changes the plan.** `public.inquiries` is not the website intake
table. It is the **desktop-published mirror** of the desktop register: its
`inquiry_number` is constrained to `^INQ-[0-9]{4}-[0-9]{4,}$`, it carries
`source_id`, `source_inquiry_id`, `source_revision`, `source_fingerprint` and
`availability`, and the intake migration says in its own header that website
submissions are kept separate from it. `public.inquiry_proformas.inquiry_id`
references that table. Therefore the website proforma belongs to a **published
desktop inquiry**, not to a website submission. Phase B described it as the website
inquiry row, which is incorrect.

**Consequence, verified live.** The project holds 8 customer submissions, 0
`inquiries` rows, 0 proformas, 0 proforma saves, 1 `inquiry_sources` row with
`last_source_revision = 0`, `last_success_at` null and 0 sync runs. The desktop to
website inquiry publication has never executed here, so the website proforma
workspace has never held a real record either. Any statement that this path works
end to end would be false. This is now the single biggest delivery risk in the
programme, larger than any modelling question.

**The money finding.** The desktop applies the 4 m2 surcharge. The website proforma
workspace does not apply it at all. The constants exist in
`src/data/pricingRules.ts`, but only the marketing surfaces import them
(`worked-example`, `ProfitLossSurface`, `SpecificationSurface`, `liveDemos`,
`workedExample`). `ProformaBuilder.tsx` imports only `billablePanelSqm`,
`isFloored`, `moneyRate` and `physicalPanelSqm` through `proforma-draft.ts`. At a
typed rate of 100 with a 20 percent surcharge, a 4 m2 panel is 480.00 in the
desktop and 400.00 on the website. Phase B framed the divergence as a fourth
decimal of area precision. It is up to 20 percent of revenue.

**The good news.** A real publication mechanism already exists and is better than
anything Phase B proposed: `publish_inquiry_snapshot` plus `inquiry_sync_runs`,
`inquiry_sources.bridge_user_id`, a dedicated `inquiry_bridge` role, the anon key
(never `service_role`), monotonic `source_revision`, a content `source_fingerprint`,
and a `p_proforma_number` argument already carried by the snapshot. The PI
publication should extend that contract, not invent a parallel one.

**Recommendation.** Keep the hybrid, but state it precisely: the desktop authors
and prices every commercial PI; Supabase holds the shared read model and owns
website-origin drafts; a published desktop PI is an immutable revisioned replica
keyed by `(source_id, source_inquiry_id)` plus a desktop-minted uuid. One
customer-facing number per document, which is the desktop's own number, stored
verbatim. Do not introduce `PI-DSK`.

**Readiness.** Phase C is **conditionally ready**: C1 (contract plus additive
schema plus a parity fixture) can start once three decisions are approved
(numbering display, website workspace fate, VAT and charges scope). C2 to C5 must
not start before those approvals, because they hard-code numbering, ownership and
customer-visible content.

**Corrections made to Phase B** (detail in section 4): the `inquiry_id` target, the
website surcharge claim, the location of the delivery hardcode, the DGU+LAM
interlayer field, the publication identity proposal, and the meaning of `Approved`,
which the source settles and Phase B left open.

**Verified during this review.** Both repositories are at the commits Phase B
recorded, all cited files and symbols exist, the website suite passes at 27 files
and 282 tests, and the desktop bridge scripts pass 615 of 615 tests. Numeric
reconciliation results are in section 11.

---

## 2. Verified repositories, branches, and commits

| Repository | Branch | Commit | Working tree | Remote |
| --- | --- | --- | --- | --- |
| `JUBERSALEEM/ProGlassAutomationADV` | `arena/2611f8ff-proglassautomationadv` | `10f56b926430eaf784b33cfb917f31a25de81975` (2026-10-24 10:31:48 +0000) | one untracked file at inspection time (the Phase B report); no tracked change. This review document is added after the inspection and is also untracked | `git ls-remote` matches the local SHA |
| `JUBERSALEEM/ProglassAutomation3.0` | `arena/da841e06-proglassautomation3-0` | `812cf315180401db593d938e7c7f63d8664c4c22` (2026-10-24 09:07:57Z, "Center inbox delivery table content") | remote only, read through `gh api` | branch head equals the inspected SHA |

Earlier commits for context, **Verified fact**: `53881a6` "Redesign the inquiry
inbox and open a proforma entry point", `dc9f032` "Verify the desktop inbox fixes
against the live contract", `10f56b9` "Show every complete website submission to
the operator queue".

**Structure confirmed at these commits.**

- ADV is a WPF ERP with nine PI wizard steps plus a register, approval dialog and
  version chain, a SQLite document store, a specification catalogue and six export
  layouts. **Verified fact** by inventory of `ViewModels/ProformaInvoice/**`,
  `Services/ProformaInvoice/**`, `Models/ProformaInvoice/**`.
- V3.0 is inquiry-only: intake, register, editor, inbox client, ingest, lifecycle
  snapshot, SQLite migrations 1001 to 1005. No proforma module exists.
  **Verified fact** by the recursive tree at `812cf31`.
- The website holds the intake, the inbox, the estimator workspace and a proforma
  workspace bound to published inquiries. **Verified fact.**

**Existence check of every path cited by Phase B.** All 62 path-like spans in the
report resolve to files that exist, apart from obvious shorthands (`phase15.sql`),
the deliberately absent proposed file (`supabase-inquiry-proforma-phaseB.sql`) and
`InquiryInboxClient.cs`, which lives in the 3.0 repository, not ADV. **Verified
fact.**

**Line references re-checked one by one.** `SqmCalculator.cs:25` is
`MinimumSqmPerPanel = 0.5m`; `SpecGroup.Part2.cs:327` and `:952` and
`SpecGroup.Part3.cs:273` are the three 4 m2 switches; `GlassItemRow.cs:247` is the
suppression flag and `:504` its only pricing read; `ProformaInvoicesDbContext.cs:55`
is the unique `ProformaId` index; `StatusTransitions.cs:68-77` is the PI transition
table. **Verified fact.**

**Checks actually executed in this review.**

| Check | Result |
| --- | --- |
| `npx vitest run` in `website/` | 27 files, 282 tests, all passed, 34.25 s |
| `node --test scripts/*.test.mjs` in `website/` | 615 passed, 0 failed, 11.4 s |
| Read-only SQL on `afpbdyamedpanwijxghk` | counts, roles, source state, bucket flag (section 12 and 14) |
| Numeric reconstruction of both pricing implementations | section 11 |

**Not verified in this review.**

- The Windows application was not built or run, so wizard behaviour, print
  rendering and QuestPDF output are unverified.
- No customer SQLite file was opened, so no statement is made about real desktop
  data volumes, custom PI numbers in the field, or the exact set of statuses
  actually stored.
- The Node bridge (`scripts/inquiry-bridge.mjs`) was not executed against the
  project, and no storage object was downloaded.
- Supabase advisors, policies as applied in production, and function bodies in
  production were read from the repository SQL, not dumped from `pg_proc`
  (Phase A did dump some; this review did not repeat it).

---

## 3. Phase B findings confirmed by source

| # | Phase B conclusion | Evidence re-read | Status |
| --- | --- | --- | --- |
| 1 | Step 4 is a pricing estimator that writes a named rate into a catalogue, with no customer or invoice context | `PricingEstimatorViewModel.Part2.cs` `ApplyRateAsync`, `SaveOrUpdateAsync(..., proformaId: null)`, `ActiveInvoiceContext.cs` header | Confirmed |
| 2 | SGU rate is `(Base/Yield + Cutting + Tempering) * (1+Margin/100)` | `CalculateSguRate`, `SguBasis` | Confirmed |
| 3 | DGU rate is `((Outer+Inner)/Yield + Spacer) * (1+Margin/100)` | `CalculateDguRate`, `DguBasis` | Confirmed |
| 4 | LAM rate adds PVB and per-pane cut and temper before margin | `LamBasis`, `CalculateLamRate` | Confirmed |
| 5 | DGU+LAM delegates to one definition with its own warning channel | `CalculateDguLamRate` calling `DguLamVM.CalculateRate(out warning)` | Confirmed |
| 6 | All money uses 2 dp away from zero in AED | `MoneyRounding.cs:77,97` | Confirmed |
| 7 | Panel area floors at 0.5 m2, rounds to 3 dp, and invalid dimensions give 0 | `SqmCalculator.CalculatePanel` | Confirmed |
| 8 | Sheet area has no floor and 4 dp; physical area is unrounded | `CalculateSheetArea`, `CalculatePhysicalPanelArea` | Confirmed |
| 9 | Row value is `round(panel area * qty * rate, 2)` | `GlassItemRow.RecalculateAll`, `_value = MoneyRounding.Round(_sqm1 * _qty * _rate)` | Confirmed |
| 10 | The 4 m2 switch is evaluated per row on `Sqm1`, in three places, and also on the row change event | `SpecGroup.Part2.cs:327,952`, `Part3.cs:273`, `OnRowChanged` at `Part2.cs:948-956` | Confirmed |
| 11 | Step 6 is other charges with four surfaces and an optimisation feed | `OtherChargesViewModel`, `WastagesSectionViewModel` (`BindOptimizationFeedProvider`), `CustomChargesSectionViewModel`, `MetricDiscoveryHubViewModel` | Confirmed |
| 12 | Step 7 waterfall is net, discount capped at net, VAT on net less discount, grand total | `InvoiceMainSummaryViewModel.Recalculate` | Confirmed |
| 13 | VAT default 5 percent, applicable by default, clamped 0 to 100 | `_vatPercent = 5m`, `IsVatApplicable = true`, setter clamp | Confirmed |
| 14 | Numbering increments the trailing digits, reallocates `PI-YYYY-000`, retries on collision, allows custom ids | `SuggestNextIdAsync`, `InsertAsync`, `IsUnallocatedSeed` | Confirmed |
| 15 | Three save paths, one of which is a silent navigation save that refuses frozen documents | `ExecuteSaveInvoiceAsync`, `SilentDraftSaveAsync`, revision copy at `SavedInvoicesListViewModel.Part1.cs:1131,1210` | Confirmed |
| 16 | Reload restores from `FullSnapshotJson` inside a batch update | `ProformaInvoiceMapper.DeserializeSnapshot`, `RestoreWizardFromSnapshot`, `BeginBatchUpdate` | Confirmed |
| 17 | The PDF renderer is a deliberate parity port, and Excel and CSV share its metrics | `ProformaInvoicePdfLayout` header, `PiPreviewParityRenderer`, `ProformaChargeMetricsCalculator` | Confirmed |
| 18 | The desktop has three status vocabularies and two cancellation mechanisms | `ProformaInvoiceStatus`, `InvoiceLifecycleStateMachine`, `StatusTransitions`, `VoidAsync`, `MarkCancelledAsync` | Confirmed |
| 19 | The website API is role gated, idempotent, revision checked and server totals | `proforma-api.ts` `READ_ROLES`/`WRITE_ROLE`, `save_inquiry_proforma_v1` | Confirmed |
| 20 | The website PI workspace has no charges, discount, VAT, print or PDF | `ProformaBuilder.tsx`, `ProformaWorkspace.tsx`, `inquiry_proformas` columns | Confirmed |
| 21 | No Supabase code exists in the ADV desktop | repository-wide search for `supabase` in C# and project files returns nothing | Confirmed |

---

## 4. Findings that need correction or qualification

**4.1 The website proforma is attached to a published desktop inquiry, not to a
website submission. Verified fact, correction.**

`supabase-inquiry-intake.sql` opens with "Keeps website submissions separate from
desktop-published `public.inquiries`". `public.inquiries` requires `source_id`,
`source_inquiry_id` and an `INQ-` number, all of which only
`publish_inquiry_snapshot` supplies. `inquiry_proformas.inquiry_id` references it.
Phase B section 12 listed the website PI as attached to the website inquiry row and
described `WEB-YYYY-NNNNNN` as its inquiry number. It is the other table.

What this changes:

- A website submission cannot host a website proforma until the desktop has
  downloaded it, created an inquiry and published that inquiry. **Verified fact**
  by reading the two schemas and the bridge.
- The website cannot navigate from a submission to its inquiry, because
  `public.inquiries` has no `web_submission_id`. **Verified fact.** The link exists
  only in the desktop file (`InquiryInboxItems.SubmissionId` and
  `Inquiries.WebSubmissionId`).
- `inquiry_proformas_source_identity` looks even more arbitrary than Phase B said:
  a desktop-origin PI has a perfectly good `inquiry_id` available (the published
  inquiry), and the constraint forbids it.

**4.2 The website proforma workspace does not implement the surcharge.
Verified fact, material correction.**

`ProformaBuilder.tsx` and `ProformaSpecification.tsx` contain no surcharge field,
no group base rate and no glass type. `SURCHARGE_THRESHOLD_SQM` and
`STANDARD_SURCHARGE_PERCENT` are imported only by marketing pages and the P and L
surface. Phase B section 11 credited the website PI with the switch. It has none.

Consequence: the same typed rate produces different money on the two surfaces
whenever a panel reaches 4 m2 and the desktop group carries a surcharge percentage
(section 11 has the figures).

**4.3 The delivery hardcode is in the PDF renderer, not in the shared metrics
calculator. Verified fact, correction.**

`ProformaChargeMetricsCalculator.cs` contains no delivery or trip logic (search for
`Trip`, `TRIP`, `Delivery`, `DELIVERY` returns nothing). The hardcode is
`ProformaInvoicePdfLayout.cs:262-266`: `metricQty = 1m; unitLabel = "TRIP"; amount =
MoneyRounding.Round(metricQty * c.Rate);`, marked BUG-23. `OtherChargeRow.FormulaDisplay`
also renders "1 TRIP × rate". So the export and the print preview can disagree with
Step 6 for a delivery row whose metric is not 1 trip.

**4.4 The DGU+LAM basis uses the interlayer sell price. Qualification.**

`DguLamBasis()` uses `vm.InterlayerSellPrice`. Phase B wrote "Interlayer", which is
ambiguous. The cost provenance fields
(`Step4InterlayerCostPerSqm`) use the buy price, as the snapshot comment states.
Any publication of cost data must use the buy field, never the sell field.

**4.5 The proposed publication identity cannot work as written. Correction to
Phase B section 19.**

Phase B proposed `publish_desktop_proforma_v1(p_proforma uuid, ...)`. A desktop
client creating a PI offline cannot supply a server-minted uuid, and cannot look one
up while offline. The identity must be minted on the desktop (a `Guid` at PI
creation, stored locally) and sent as `p_client_public_id`, or the function must be
keyed only by the existing `(source_id, source_inquiry_id, source_proforma_number)`
triple. **Recommendation**: do both, `client_public_id` as the primary idempotency
key and the triple as the integrity check.

Phase B also proposed `superseded_by` on `inquiry_proformas` while keeping a unique
index on `(source_system, source_proforma_number)`. A superseding row that reuses the
desktop number would violate that index. Either the index becomes partial
(`... AND superseded_by IS NULL`, which requires dropping and recreating it) or
revisions are stored in a child table and the canonical row stays single. Phase B
did not notice the conflict.

Phase B also proposed one dedicated bridge identity with an `inquiry_admin` role.
The repository already has a narrower, better pattern: `inquiry_bridge` scoped to a
source dataset through `inquiry_sources.bridge_user_id`, with a `sync_run_id`
required by the function. **Recommendation**: reuse it.

**4.6 `Approved` is customer acceptance. Verified fact, resolves Phase B's open
question.**

`InvoiceApprovalViewModel.cs` exists to record how the customer confirmed the
quotation: mode 0 "Customer signed the Proforma Invoice", mode 1 "Customer provided
an LPO / PO", with the LPO number required in mode 1.
`SavedInvoicesListViewModel.Part2.cs:68-121` runs only after the signed PI or LPO is
uploaded in Step 8, persists the LPO into `ProjectSnapshot.LpoNumber`, sets status
`Approved`, writes an audit line naming signed PI or LPO, and auto-creates or
releases the job order. The saved-invoices workflow text says the same: "Confirmation
needs the signed PI or LPO. That approves the invoice and creates or releases the
job order."

So `Approved` is not internal sign-off. There is no internal approval step anywhere
in the PI lifecycle.

**4.7 `Sent` means dispatched, and the body stays editable after it. Verified fact.**

`Draft` shows the next action "Mark sent", and `ChangeStatusAsync(item, "Sent")`
sets it. The workflow hint reads "Sending records that the quotation left the
office." `DocumentLifecyclePolicy.BodyEditable` includes `Sent`, so a quotation can
be edited after it has gone to the customer. Combined with 4.6 this settles Phase
B's Decision B question and exposes a real risk: what the customer received is not
recorded anywhere (`SavedProformaInvoice` has no `SentAt`, `PrintedAt` or
`IssuedAt`, verified).

**4.8 Manual rates are not protected, because the suppression flag is never set.
Verified fact, new defect.**

`SuppressRateAutoAdjust` is declared at `GlassItemRow.cs:247` and read in four
places, and **assigned nowhere in the repository**. `OnRowChanged` therefore always
replaces a row's rate with base or surcharge when its `Sqm1` changes, and
`UpdateAllRowRates`, `FillDownAcrossRows` and the paste path always reprice every
row. A rate the operator typed by hand survives only until that row's area changes
or the group's base rate or surcharge percentage is edited.

This also means the comment in `website/src/data/pricingRules.ts` ("a row whose rate
a person has typed over is left alone") and the corresponding sentence in Phase B
section 6 describe an intention, not the behaviour. Label the flag as dead until it
is wired.

**4.9 Contradictions inside the Phase B report.**

| Section | Says | Contradicted by |
| --- | --- | --- |
| 11 | website applies the 4 m2 switch | 4.2 above; only marketing surfaces do |
| 12 | website PI is attached to the website inquiry (`WEB-`) | 4.1 above |
| 19 | revisions are new rows with `superseded_by` | the unique `(source_system, source_proforma_number)` index it keeps |
| 14 | `Sent` may equal `Issued`, unresolved | 4.7, plus `IssueDate` is the document date, not an issuance event |
| 6 | type over a rate and the switch stops | 4.8, the flag is never set |

**4.10 Qualification on test evidence.** Phase B recorded 615 node tests and 282
vitest tests without saying whether it had run them in that session. Both have now
been run in this review and pass at these commits. **Verified fact.** No Windows
test suite was executed.

---

## 5. Step 4 audit. Configuration that affects PI preparation

**Verified fact.** Step 4 is the only place a rate is manufactured. Everything else
selects, copies or overrides a rate.

| Setting | Stored where | Read by | Scope | Can diverge | Effect on an existing draft |
| --- | --- | --- | --- | --- | --- |
| Specification catalogue entry (glass type, name, rate) | `specifications.db` via `ISpecificationsRepository` | Step 4 list, Step 5 spec picker | Global, shared across every invoice on that workstation | No, single file, but it is mutated by any invoice (`proformaId: null`) | A later Apply on the same name overwrites the catalogue rate; drafts keep their own `BaseRate` copy |
| Step 4 section parameters (base, cut, temper, yield, margin, spacer, PVB) | PI snapshot (`Step4PricingSections`) | Step 4 recompute, Step 9 costing | Per PI, per section | Yes, two invoices can quote the same build at different rates | None until Step 4 is reopened and applied |
| Sheet colours and sheet prices | Sheet store database | Step 4 colour lists, Step 5 sheet P/L fields, `SheetStorePricingService` | Global inventory | Yes, inventory changes | Re-primed on load by `PrimeSyncSnapshot`; can change displayed material cost |
| Group base rate and surcharge percent | Step 5 group, in snapshot | Row pricing | Per specification group | Yes, per group, per invoice | Editable while the PI body is editable |
| Billable floor, perimeter factor, 4 m2 threshold | Code constants | Desktop only | Global, not configurable | Desktop and website differ (4 dp, no surcharge) | A code change would alter recomputation on reopen |
| Money rounding, currency | `MoneyRounding` | All money | Global, AED only | Website rounds independently | Stored totals do not change until resave |
| VAT percent and applicability | `InvoiceMainSummary` in snapshot | Step 7 | Per PI | Yes | Editable per invoice |
| Discount cap (never above net), maximum 50 percent | Code (`Recalculate`, `CrossStepValidator`) | Step 7 | Global policy | No | Blocks, does not rewrite |
| Quote validity dates | Step 1, snapshot | Step 7, print | Per PI | Yes | `CrossStepValidator` enforces at least 1 day |
| Terms and conditions | Step 7 terms VM, snapshot | Print and PDF | Per PI | Yes | Rendered verbatim |
| Company letterhead and profile | Company profile store | Step 1, print | Global | Yes, per workstation | Affects the next print, not stored totals |
| Currency | AED only, no field | All money | Global | Website has a `currency` column defaulting to AED | Website can hold a different currency string |

**What is not configurable and probably should be. Recommendation.** The 4 m2
threshold, the default surcharge percentage, the 0.5 m2 floor and the VAT default
are all code or per-invoice values. If the business wants one policy, they belong in
a single configuration object with an effective date and a version stamp on each PI.
Do not introduce configuration fields that do not exist today without that decision.

**Divergence summary.** The desktop and the website share no configuration source.
The catalogue, sheet store and company profile exist only on the desktop. The
website has no pricing configuration at all beyond the per-line rate. **Verified
fact.**

---

## 6. Step 5 audit. Groups, lines, validation and billable quantities

**Entry and grouping, verified.** A specification group exists per glass build
(`SpecGroup`) with `GroupNumber`, `GroupName`, `SelectedGlassType`,
`SelectedSpecName` and a per-group rate pair. Rows belong to a group and carry
`GlassRef`, `W1`, `H1`, `W2`, `H2`, `Qty`, `Rate`, `PerimeterFactor`.

**Validation actually performed.**

| Layer | Rule | Where |
| --- | --- | --- |
| Desktop grid | No type validation of dimensions at entry; arithmetic yields 0 for a zero side | `GlassItemRow.RecalculateAll` |
| Desktop cross step | At least one group, non negative totals, zero SQM with pieces warns, zero pieces with groups warns | `CrossStepValidator.Validate` |
| Desktop financial cross check | Step 7 glass subtotal must equal Step 5 total within 0.01 | `CrossStepValidator` |
| Desktop inquiry import | Glass type must be recognisable, dimensions and rates plausible, else the PI cannot start | `InquiryValidationRules.ValidateLineItems` via `InquiryIntegrationService` |
| Website client | Group 1 to 50, line 1 to 500, size 1 to 20000, qty 1 to 100000, rate 0 to 1000000, unique position | `validateLines` in `proforma-api.ts` |
| Website server | Same ranges re-validated in plpgsql, plus per line checks | `save_inquiry_proforma_v1` |
| Website schema | `w1` to `h2` 0 to 20000, `qty` 0 to 100000, `approved_rate` 0 to 1000000 | `inquiry_proforma_lines` checks |

**Two panel pairs.** `W2` and `H2` are stored, and
`SqmCalculator.CalculatePanel` is applied to them, but no money expression reads
`Sqm2` or `TotalSqm2`: `_value` uses `_sqm1` only. So an inner pane does not add
billed area; it only feeds linear metres and display. **Verified fact.** Any port
must preserve that, or prices change silently.

**Billable quantity chain, verified.**

```
Sqm1   = SqmCalculator.CalculatePanel(W1, H1)     floor 0.5, 3 dp, 0 if invalid
TotalSqm = round(Sqm1 * Qty, 3)                   display and footers only
Lm1    = round((W1 + H1)/1000 * 2 * Qty, 3)       feeds Step 6 linear charges
Value  = round(Sqm1 * Qty * Rate, 2)              the only money from area
```

Website, in `proforma-draft.ts` and in SQL:

```
sqm      = round4(max(W1*H1/1e6, 0.5))
totalSqm = round4(sqm * qty)
value    = round2(totalSqm * rate)                SQL and client agree
```

**Duplicate implementations of the same rule.**

| Rule | Implementations |
| --- | --- |
| 0.5 m2 floor | `SqmCalculator.CalculatePanel` (desktop), `greatest(...,0.5)` in `save_inquiry_proforma_v1`, `billablePanelSqm` in `pricingRules.ts`, plus the JS previews |
| 4 m2 surcharge switch | Three sites in `SpecGroup` plus `OnRowChanged`, none on the website PI |
| Surcharge rate | `SpecGroup.SurchargeRate` (`Part1.cs:133`), mirrored by `priceLight` in the marketing worked example |
| Area precision | 3 dp desktop, 4 dp website and SQL |
| Line value | `GlassItemRow`, `lineValue` in JS, plpgsql `v_line_value` |
| Charge metrics | `OtherChargesViewModel` and `MetricDiscoveryHubViewModel` inside the app, mirrored by `ProformaChargeMetricsCalculator` for export |
| Delivery quantity | Step 6 row logic versus `ProformaInvoicePdfLayout.cs:262` hardcode |

**Verdict.** Five of these are justified duplication with a documented single
source (`SqmCalculator`, `MoneyRounding`, the charge metrics calculator). The 4 m2
switch and the 4 dp versus 3 dp precision are not: they are independent
implementations that already disagree.

---

## 7. Step 6 audit. Calculation, validation and silent change

### 7.1 Desktop calculation sequence

**Verified fact**, `GlassItemRow` and `SpecGroup`:

1. Dimension or quantity setter fires `RecalculateAll()`, which recomputes area,
   totals, linear metres and value. It does **not** change the rate.
2. The row raises `PropertyChanged` for `Sqm1`. `SpecGroup.OnRowChanged` sees it and,
   when the row is not suppressed, replaces the rate with `Sqm1 >= 4 ? SurchargeRate :
   BaseRate`.
3. Editing the group `BaseRate` or `SurchargePercent` calls `UpdateAllRowRates`,
   which reprices every non suppressed row the same way.
4. Paste and fill down reprice the affected rows, with a per-row check for an
   incoming Rate column.
5. `Value` is recomputed from the current area, quantity and rate.
6. The footer sums values and areas.

Because the suppression flag is never set (4.8), every rate is effectively
"automatic" once the row's area or the group rate changes.

### 7.2 Desktop validation and error handling

- Invalid yield is caught and replaced by 1.0 with a visible warning
  (`YieldFactorWarning`), never a divide by zero.
- Negative or zero dimensions produce 0 area, not an exception.
- `ApplyRateAsync` refuses to save a blank name or a non positive rate.
- `CrossStepValidator` blocks a save when Step 7 and Step 5 disagree by more than a
  fils, or when any total is negative.
- Silent draft save swallows every exception by design, so a failure on navigation
  is invisible. **Verified fact.**

### 7.3 Website calculation sequence

**Verified fact.** Client (`priceLine`) and server (`save_inquiry_proforma_v1`)
compute the same formula, and the SQL stores `sqm numeric(12,4)`,
`total_sqm numeric(14,4)`, `line_value numeric(18,2)`. The rate is whatever the
client sends within 0 to 1,000,000; the server never derives a surcharge and never
checks the rate against the group base. There is no VAT, no discount and no charge
table on the website.

### 7.4 Can recalculation silently change a saved PI?

| Situation | Desktop | Website |
| --- | --- | --- |
| Open a saved PI | No. `SetAllSilent` writes the stored rate and dimensions, then recomputes area and value from the current code. Stored columns are untouched until a save. | No. Stored `sqm`, `total_sqm` and `line_value` are read, not recomputed. |
| Change a dimension | Area and value change. Rate is replaced if the row crosses the 4 m2 threshold, and a hand typed rate is lost. | Nothing until the operator saves; the preview recomputes from typed fields. |
| Change the group base or surcharge | Every row is repriced. Hand typed rates are lost. | Not applicable, no group rate exists. |
| Change a code constant (floor, threshold, rounding) | Every reopened PI displays recomputed areas and values, and the next save persists them. | Stored rows keep their values, but a new save recomputes from the same formula. |
| Restore from an older snapshot | `SchemaVersion` is 2 and additive fields default sensibly, so business values are preserved. | Not applicable. |

**Assessment.** The desktop's stored number is "the rate and the dimensions", not
the money. That is a deliberate design, and it means any future change to the
pricing policy retroactively changes what an old PI displays the next time it is
opened. **Recommendation**: stamp `pricing_version` on the PI and compute with the
version the PI was created under, so history is stable.

### 7.5 Boundary examples, computed from the source expressions

These are reconstructions of the verified code paths, not runs of the application.
**Not verified** against the UI.

| Case | Desktop (3 dp, floor, surcharge on `Sqm1`) | Website (4 dp, floor, no surcharge) |
| --- | --- | --- |
| 700 x 700 mm, qty 1, rate 900 | area 0.490 then floored to 0.500, value 450.00 | area 0.5000, value 450.00 |
| 708 x 708 mm, qty 1, rate 900 | area 0.501, value 450.90 | area 0.5013, value 451.17 |
| 1414 x 1414 mm, qty 1, rate 100 | area 1.999, value 199.90 | area 1.9994, value 199.94 |
| 1999 x 2001 mm (3.999999 m2), qty 1, rate 100, surcharge 20 percent | area rounds to 4.000, surcharge applies, value 480.00 | area 4.0000, base rate only, value 400.00 |
| 1990 x 2010 mm (3.9999 m2), same | area 4.000, value 480.00 | area 3.9999, value 399.99 |
| 2500 x 1600 mm, qty 2, rate 145.75, surcharge 20 percent | area 4.000, rate 174.90, value 1399.20 | area 4.0000, value 1166.00 |

The last three rows are the important ones: the threshold decision differs at the
boundary because the desktop rounds to 3 dp before comparing, and the money differs
by the surcharge because the website has none.

**VAT fixtures** (desktop only), recomputed from `Recalculate`: net 1000.00, 10
percent discount, 5 percent VAT gives discount 100.00, subtotal 900.00, VAT 45.00,
grand 945.00. Net 99.99, 7.5 percent discount gives 7.50, 92.49, 4.62, 97.11.
Across 200,000 nets in 0.001 steps, the displayed subtotal plus the displayed VAT
always equals the displayed grand total, because the VAT amount is rounded once and
the grand total is built from it. **Verified fact by reconstruction.**

Substituting VAT on the *rounded* subtotal for VAT on the unrounded one changes the
result only for bases below 0.10 AED. **Verified fact by reconstruction**, so this
is a documentation detail, not a commercial risk.

---

## 8. Step 7 audit. The thirteen lifecycle steps

| # | Step | ADV desktop | Website | Verdict |
| --- | --- | --- | --- | --- |
| 1 | Select an eligible inquiry | Implemented. `InquiryIntegrationService.ValidateInquiryEligibility` allows `Prepared` or `Confirmed`, rejects an inquiry that already has a PI, and requires a safe BOQ | Not implemented. The workspace takes an `inquiry_id` but has no picker, and no inquiry holds a PI today | Desktop yes, website no |
| 2 | Start a PI | Implemented, from inquiry or blank | Implemented as a workspace that creates a draft on first save | Both partial |
| 3 | Generate the number | Implemented, client side with unique index and retry | Implemented, server side from a sequence | Both, incompatible schemes |
| 4 | Add and edit lines | Implemented, groups plus rows plus catalogue sync | Implemented, lines with group and line numbers only | Both |
| 5 | Calculate totals | Implemented, with charges, discount and VAT | Implemented, glass lines only | Both, different scopes |
| 6 | Save a draft | Implemented, plus silent navigation save | Implemented, revision and idempotency | Both |
| 7 | Reload a draft | Implemented from the JSON snapshot | Implemented from normalised tables | Both |
| 8 | Edit an existing draft | Implemented while the body is editable | Implemented while `Draft` | Both |
| 9 | Issue or send | Partial: `Sent` is a manual status change with no stored dispatch date, no issued document, and the body stays editable | Absent: `Issued` exists in the check constraint and is never written | **Gap in both** |
| 10 | Print or generate a customer document | Implemented, QuestPDF parity renderer, Excel, CSV, dossier | Absent | Desktop only |
| 11 | Reject, cancel, expire, void | Implemented with two distinct mechanisms, expiry is a manual status | `Void` exists in the constraint and is never written | Desktop only |
| 12 | Publish or synchronise | Absent. No Supabase code in ADV; the Node bridge publishes inquiries, not PIs | Absent. It can read and write its own table only | **Gap in both** |
| 13 | Recover from failure | Partial: silent save, `RowVersion`, SQLite retry, attachment cleanup; no journal | Good inside a request, none across a failed publication | Gap |

**Step 9 deserves emphasis. Verified fact.** `SavedProformaInvoice` has no
`SentAt`, `PrintedAt`, `IssuedAt` or document hash, and `Sent` remains body
editable. The system therefore cannot answer "which document did the customer
receive, and was it changed afterwards". For a programme whose purpose is a
customer-facing pipeline, that is the most important missing piece after
publication itself.

**Step 11 semantics that must not be flattened. Verified fact.**
`VoidAsync` sets `IsVoid`, `VoidedAtUtc`, `VoidedBy`, `VoidReason` and status
`Cancelled`. `MarkCancelledAsync` sets status `Rejected` (and the snapshot status to
`Rejected`) when the downstream pipeline is dismantled, and the register displays
that as Cancelled. Two different business events, one displayed word.

---

## 9. PI ownership architecture comparison and recommendation

### 9.1 The five responsibilities, separated

| Responsibility | Candidate | Why |
| --- | --- | --- |
| Owns the canonical PI record | Supabase row per document | It is the only store both applications can reach, and it already has the origin, revision and identity columns |
| Authors the PI | Desktop module | It is the only application that can price, and the only one that carries the catalogue |
| Initiates an action | Either, per action | Creation from a website inquiry starts on the website (a button that opens the desktop flow); every pricing edit starts on the desktop |
| Calculates and validates pricing | Desktop pricing core, ported once into V3.0 | The rules are 46 defaults, a yield, a floor and a surcharge; duplicating them in SQL has already produced drift |
| Issues and publishes | Desktop | Issuance requires the customer document, and only the desktop can render it |

### 9.2 Options against ten criteria

| Criterion | A. Website owns | B. Desktop owns | C. Hybrid, explicit | C2. Recommended refinement |
| --- | --- | --- | --- | --- |
| Data consistency | One store, but no pricing context | One store per workstation, no shared view | Two stores, needs a contract | Two stores, one canonical read model, one writer per origin |
| Offline operation | None | Full | Full for authoring | Full for authoring, publication queued |
| User experience | Browser only, cannot price | Desktop only, no portal | Both, with a visible published state | Both, and the portal never pretends to edit |
| Duplicate PI risk | Low | High across workstations | Medium | Low, because identity is `(source, inquiry, client uuid)` |
| ADV compatibility | Breaks it | Preserves it | Preserves it | Preserves it, publication is additive |
| Implementation complexity | Low | Medium | High | High but bounded, and it reuses the existing bridge |
| Migration risk | High, would rewrite today's workflow | Low | Medium | Low, additive only, 0 existing rows |
| Security | Browser writes pricing, weakest | No shared audit | Needs a bridge | Bridge reuses an existing source-scoped role |
| Recovery and audit | Server only | Local only | Needs an outbox | Outbox plus immutable revisions |
| Long term maintenance | Two pricing models in one app | Two disconnected products | One contract, two apps | One contract, one pricing core, two apps |

**Recommendation: C2, desktop-authored hybrid with a server read model.**

Reasons, each traceable:

1. The desktop is the only place a rate can be produced (Step 4 plus the catalogue).
   Any website-authoring model would need that catalogue, which it does not have.
   **Verified fact.**
2. The website already has a role-gated, idempotent, revisioned save. That is the
   right pattern for the *shared read model* and for website-origin drafts.
   **Verified fact.**
3. A desktop-origin PI must be publishable while offline and reconcilable later, so
   the identity has to be minted locally and the transport has to be idempotent.
   **Verified fact** that offline authoring is the norm (SQLite, nine steps). 
4. The existing inquiry bridge already solves authentication, revisioning and
   conflict for inquiries. Reusing it for PIs removes a whole new credential and a
   whole new failure mode. **Verified fact.**

### 9.3 Hard boundaries that must be written into the contract

| Boundary | Rule |
| --- | --- |
| One editor per document | A desktop-origin PI is edited only on the desktop. The website renders it read only. A website-origin draft is edited only on the website, and it is not a priced commercial document. |
| No second source of truth | The canonical row is the published revision. The desktop's local row stays authoritative for its own authoring until it publishes, and a published revision is immutable. |
| No recomputation across the boundary | The website never recomputes a published total. The desktop never adopts a website total. |
| Issuance | Only the desktop issues. Issuance produces an immutable snapshot plus a rendered document with a hash. |
| Deletion | Nothing is deleted across the boundary. Void only, with a reason and an actor. |

---

## 10. Six business decisions and recommended defaults

### Decision A. PI numbering and dual-number display

**Question.** How is one PI identified internally and to the customer, given that
the desktop already prints `PI-2026-001` and accepts custom ids such as `INV-42`,
while the website generates `PI-WEB-YYYY-NNNNNN`?

**Current behaviour, verified.** Desktop: seed `PI-{year}-000`, increment, unique
index, five retries, hex fallback after a logged failure; custom ids are legal and
preserved (`IsUnallocatedSeed` only rewrites the seed). Website:
`'PI-WEB-' || year || '-' || lpad(nextval(seq), 6, '0')`, with a global sequence,
plus a check constraint that accepts only `^PI-(WEB|DSK)-[0-9]{4}-[0-9]{6}$`.
`inquiries.proforma_number` is free text copied by the publisher.

**Recommended default.**

- Internal identity is a uuid, always. For a desktop PI it is minted on the desktop
  at creation (`ClientPublicId`) and stored locally; for a website draft it is the
  row id the server already mints.
- **One customer-facing number.** It is the number of the system that authored the
  document. A desktop PI keeps `PI-2026-001`, `INV-42` or the hex fallback exactly
  as printed. A website draft keeps `PI-WEB-...`.
- Do **not** introduce `PI-DSK`. A second number for the same document is a support
  and reconciliation cost with no benefit.
- Store the desktop number in `source_proforma_number` **and** use it as the
  displayed `proforma_number` for desktop rows, which needs the check constraint
  relaxed per origin:

```sql
CHECK (
  (source_system = 'website' AND proforma_number ~ '^PI-WEB-[0-9]{4}-[0-9]{6}$')
  OR
  (source_system = 'desktop' AND proforma_number = source_proforma_number
     AND proforma_number ~ '^[A-Z][A-Z0-9-]{2,49}$')
)
```

**Algorithms that decide.** Legacy numbers are never rewritten. New website drafts
keep `PI-WEB`. New desktop PIs keep whatever the allocator produced. A collision
between a desktop number and a website number is impossible under the two prefixes,
and a hand typed desktop id that mimics `PI-WEB-...` is rejected by the desktop-side
guard added in Phase C.

**Alternatives and consequences.** (a) Keep `PI-DSK` for desktop rows: every
customer sees two numbers, and support must translate. (b) Move the desktop to
`PI-DSK` numbering prospectively: printed history and customer references diverge.
(c) Drop the website prefix and use one global sequence: any client can now collide
with a desktop number, and the desktop cannot allocate offline.

**Blocks implementation?** Yes, C1 binds the constraint. **Requires business
approval** from the owner who owns document control, on the exact regex and on the
statement that a desktop PI keeps its existing number on the portal.

### Decision B. Sent versus Issued, and the canonical lifecycle

**Question.** What distinguishes a document that has been generated, one that has
been delivered, and one the customer has accepted?

**Current behaviour, verified.** Desktop `Sent` is a manual status meaning the
quotation left the office; the body stays editable; nothing records when or what was
sent. `Approved` requires a signed PI or LPO attachment and drives inquiry
`Confirmed` plus job creation. Website `Issued` exists in the constraint and is
never written; there is no delivery or acceptance concept.

**Recommended canonical lifecycle.**

```
Draft --send--> Issued --customer confirms--> Accepted
  |                |                              |
  +--> Cancelled   +--> Expired                   +--> downstream job, delivery, invoice
Void is a separate terminal event with a reason, reachable from any state.
```

- `Issued` means a rendered document was sent. It stores `issued_at`,
  `issued_revision`, `document_sha256`, `sent_to` (optional).
- `Accepted` means the customer confirmed, with `acceptance_evidence` in
  (`signed_pi`, `lpo`) and `acceptance_reference` for the LPO number.
- `Expired` is either computed from `valid_until` or set manually; the decision is
  in the approval list.
- Do not model internal approval. It does not exist.

**Legacy mapping**, applied at read time, never by rewriting rows:

| Source status | Canonical | Note |
| --- | --- | --- |
| `Draft` | `Draft` | |
| `Sent` | `Issued` | no issued document exists for history, so `document_sha256` is null and the UI says "not archived" |
| `Approved` | `Accepted` | evidence unknown for historical rows, so `acceptance_evidence` is null |
| `Rejected` | `Rejected` | |
| `Expired` | `Expired` | |
| `Cancelled` | `Cancelled` | |
| `IsVoid = true` | `Void` | reason and actor are already stored |
| Website `Draft` | `Draft` | |
| Website `Issued` | `Issued` | no rows exist |
| Website `Void` | `Void` | no rows exist |

**Blocks implementation?** Yes for C3 and C4. **Requires business approval** on
whether a sent desktop PI may still be edited, and on whether expiry is automatic.

### Decision C. Meaning of Approved

**Question.** Does `Approved` mean internal sign-off, customer acceptance, or
production release?

**Current behaviour, verified.** Customer acceptance, evidenced by a signed PI or an
LPO number, entered after an attachment in Step 8, with an audit line, and followed
by automatic job order creation or release. There is no internal approver, no
approval role and no approval queue.

**Recommended default.** Rename the canonical state to `Accepted`, keep the desktop
label `Approved` unchanged for operators, and store the evidence explicitly:
`acceptance_evidence` and `acceptance_reference`. Do not merge acceptance with
production release; the job order is the production signal.

**Alternatives.** Keep one word for both and lose the distinction between "the
customer agreed" and "the job exists". Merge acceptance into the inquiry status and
leave the PI at `Issued`; that loses the PI-level audit trail the desktop already
writes.

**Blocks implementation?** Yes, C4. **Requires business approval** of the canonical
label and of whether the portal may display "Accepted".

### Decision D. VAT, charges and customer-facing PI content

**Question.** What must a customer-facing PI contain, and which application may
produce it?

**Current behaviour, verified.** Desktop: glass lines, other charges (targeted,
wastage, custom, delivery), subtotal, discount, VAT, grand total, validity, terms,
letterhead, PDF and Excel. Website: lines, area and value only, in AED, with no
charge, discount or VAT columns and no document output.

**Recommended default.**

1. The customer-facing document is produced by the desktop renderer only, and is
   published as an immutable revision with its hash. The portal displays it, never
   regenerates it.
2. The canonical summary carries the desktop's breakdown verbatim in structured
   form: glass subtotal, charge rows (name, unit, quantity, rate, amount), net,
   discount, VAT percent and amount, grand total, currency, validity, terms
   reference.
3. A website-origin draft remains a pre-desktop estimate. It is labelled as such and
   is never presented as a VAT document. If the business wants it to be a real PI,
   that requires the charge and VAT model on the website, which is a phase of its
   own.

**Alternatives.** Build VAT and charges on the website now (a second pricing
implementation, the exact thing this programme is trying to prevent). Or hide
charges and VAT on the portal (the customer sees a total that the PDF contradicts).

**Blocks implementation?** Yes, C1 for the summary shape and C4 for the document.
**Requires business approval** on the estimate label and on whether the portal must
show the full breakdown.

### Decision E. Operator attribution through a shared bridge

**Question.** When a shared integration identity performs the write, how is the real
operator recorded?

**Current behaviour, verified.** The desktop stamps `UpdatedBy`,
`PreparedByName` and audit lines with the Windows account
(`AuditService.CurrentUsername`, V3.0 `CurrentOperator`). The publication path
authenticates with the bridge user (`inquiry_sources.bridge_user_id` plus the
`inquiry_bridge` role), and `auth.uid()` therefore identifies the bridge, not the
person. Nothing on the desktop records a role.

**Recommended default.** Record both, labelled:

| Field | Source | Trust |
| --- | --- | --- |
| `actor_user_id` | `auth.uid()` at the database | Trusted, the bridge |
| `actor_kind` | `human` or `integration` | Trusted |
| `operator_name` | desktop-reported | **Untrusted data**, displayed as "reported by" |
| `operator_station` | desktop machine name | Untrusted data |
| `occurred_at` | server clock for the event, desktop clock in the payload | Both stored |
| `operation` | `create`, `update`, `issue`, `accept`, `cancel`, `void`, `publish` | Trusted |
| `revision`, `request_id` | publication envelope | Trusted |

If the business needs *trusted* per-person attribution, the only sound answer is one
credential per workstation mapped to a person, which is a later phase and a real
operational cost.

**Blocks implementation?** No for C3 if the untrusted label is accepted. **Requires
business approval** on whether self-reported attribution is sufficient.

### Decision F. The 4 m2 threshold and the surcharge percentage

**Question.** Is the 4 m2 switch with a 20 percent uplift a company policy, and how
is it applied?

**Current behaviour, verified.**

- The threshold literal is 4, in three places plus the row change handler.
- The comparison is `Sqm1 >= 4` where `Sqm1` is the 3 dp billable area. A panel at
  3.999999 m2 rounds to 4.000 and is surcharged.
- The percentage is a per group field, `SurchargePercent`, default 0. There is **no**
  20 percent default anywhere in the desktop.
- The rate is `round2(BaseRate * (1 + pct/100))`, and it replaces the row rate.
- Exceptions: rows whose rate arrives through a paste column are left alone; the
  suppression flag that would protect a typed rate is never set (4.8).
- The website proforma has no implementation. Only the marketing worked example
  shows a 20 percent standard.

**Recommended default.** Represent the rule as configuration, not as a literal:
`threshold_sqm`, `default_percent`, `evaluation_basis` (`billable_area`),
`effective_from`, and `pricing_version`. Keep the evaluation per row. Confirm 20
percent as policy or as a sales habit before it becomes a default. Add the boundary
test matrix in section 11.

**Alternatives.** Keep it hardcoded (a policy change becomes a code release), or
move it to the website too (a second implementation before the first is
consolidated).

**Blocks implementation?** Yes for the pricing core in C2, because the core must
know whether it reads a constant or configuration. **Requires business approval** on
the threshold, the percentage, and whether a typed rate may override the switch.

---

## 11. Pricing and rounding reconciliation

### 11.1 The two sequences, side by side

| Stage | Desktop ADV (verified source) | Website (verified source) |
| --- | --- | --- |
| Raw area | `W * H / 1e6` in decimal, unrounded | `(w/1000) * (h/1000)` in double |
| Area rounding | `round(..., 3)` before the floor | none before the floor |
| Minimum billable | applied to the 3 dp value, `0.5` if lower, `0` if a side is 0 | `max(area, 0.5)` then `round4` |
| Billable precision | 3 dp | 4 dp |
| Threshold basis | `Sqm1 >= 4` on the 3 dp value | no threshold in the workspace |
| Unit price | `round2(base * (1 + surcharge/100))`, replaced by the row rate on any area change | the typed rate, `round2` |
| Line total | `round2(area3 * qty * rate)` | `round2(round4(area4 * qty) * rate)` |
| Subtotal | sum of line values, `round2` at the summary boundary | sum of line values (`total_value`), `numeric(18,2)` |
| Charges | computed rows, each `round2`, then summed | none |
| Discount | `round2(net * pct/100)` or a fixed amount, capped at net | none |
| VAT | `round2((net - discount) * pct/100)` on the **unrounded** base | none |
| Final total | `round2((net - discount) + vat)` | none |
| Stored | rate and dimensions inside `FullSnapshotJson`, money columns on the row | `sqm numeric(12,4)`, `total_sqm numeric(14,4)`, `line_value numeric(18,2)`, `total_value numeric(18,2)` |
| Display | 3 dp area, 2 dp money | 4 dp area, 2 dp money |

### 11.2 What the difference actually costs

Three distinct causes, in order of size:

1. **No surcharge on the website.** Up to the group's percentage, 20 percent in the
   worked standard. Example in section 7.5: 480.00 against 400.00 on one 4 m2 panel.
2. **Threshold boundary.** The desktop rounds to 3 dp before comparing, so a panel
   between 3.9995 and 3.99999 m2 is surcharged while a 4 dp comparison is not.
3. **Fourth decimal of area.** Up to a few fils per line at ordinary rates (0.27 on
   the 708 x 708 fixture at 900 per m2), larger at high rates and quantities.

### 11.3 Recommended canonical policy (v2)

1. Dimensions are integer millimetres; quantity is an integer; rate is 2 dp; money is
   AED at 2 dp, half away from zero.
2. `raw = W * H / 1e6` exactly (decimal, no floating point).
3. `area4 = round(raw, 4)`; invalid dimensions give 0, never the floor.
4. `billable4 = area4 < 0.5 ? 0.5 : area4`.
5. `total4 = round(billable4 * qty, 4)`.
6. `line_value = round2(total4 * rate)`.
7. Surcharge test: `billable4 >= 4.0000`, applied per row, on the billable area, not
   on the physical area and not on the group total.
8. `surcharge_rate = round2(base_rate * (1 + pct/100))`; the row rate is the
   surcharge rate when the test passes and the base rate otherwise, unless a human
   has explicitly overridden that row.
9. Subtotal is the sum of stored line values; no re-rounding.
10. Each charge is `round2(quantity * rate)`; the charges total is their sum.
11. Discount is `round2(net * pct/100)` or a fixed amount, capped at net and at the
   50 percent policy limit.
12. VAT is `round2((net - discount) * pct/100)` computed on the unrounded base, and
    the grand total is `round2((net - discount) + vat)`. This preserves the desktop's
    current, internally consistent behaviour.
13. Store area at 4 dp and money at 2 dp. Display area at 3 dp on the desktop and
    4 dp on the website if that is what each screen already does; display is not the
    contract.

Rules 3 to 6 change desktop numbers in the fourth decimal, and rule 7 changes the
boundary between 3.9995 and 3.99999 m2. That is why every document carries
`pricing_version` and why historical documents are never recomputed (section 15).

### 11.4 Test matrix

| Case | Input | Expected under v2 | Guards |
| --- | --- | --- | --- |
| Just below the floor | 707 x 707 mm (0.4998) | billable 0.5000, value 450.00 at 900 | floor applied after rounding |
| Exactly the floor | 1000 x 500 mm (0.5000) | billable 0.5000 | no double floor |
| Just above the floor | 708 x 708 mm (0.5013) | billable 0.5013, value 451.17 at 900 | 4 dp retained |
| Zero side | 0 x 1200 mm | billable 0, value 0 | invalid is not floored |
| Just below the threshold | 1990 x 2010 mm (3.9999) | no surcharge | 4 dp comparison |
| Exactly 4.0000 | 2000 x 2000 mm | surcharge applies | inclusive boundary |
| Just above | 2001 x 2000 mm (4.002) | surcharge applies | |
| Rounding across the threshold | 1999 x 2001 mm (3.999999) | no surcharge under v2 | documents the v1 to v2 change |
| Fractional quantity | not supported, integer only | rejected at validation | no silent truncation |
| High unit price | rate 999,999.99, area 20 m2 | `round2` away from zero, no float drift | integer scaling in JS |
| Many fractional lines | 50 lines of 0.5013 m2 at 187.33 | subtotal equals the sum of stored values | no re-rounding |
| VAT rounding | net 0.10, 5 percent | VAT 0.01, grand 0.11 | half away from zero |
| VAT on odd base | net 92.49, 5 percent | 4.62, grand 97.11 | matches today |
| Legacy document | any stored v1 PI | totals unchanged, no recompute | version gate |

### 11.5 Migration and compatibility strategy

- No stored row is rewritten in this phase. **Recommendation.**
- Add `pricing_version` (smallint) to the canonical table and to the desktop entity,
  defaulting to 1 for existing rows and 2 for new ones. **Recommendation.**
- The desktop computes with the version on the document, so reopening an old PI
  reproduces its own arithmetic rather than today's.
- A reconciliation report (read only) lists documents whose v1 arithmetic differs
  from v2, with the delta, so the business can decide whether any need a correction
  revision. **Recommendation.**
- The parity fixture is the single artefact both implementations must satisfy:
  a JSON file of panels, rates and expected areas and values, executed by the
  desktop tests and the website tests. **Recommendation.**

---

## 12. Identity and synchronization contract

### 12.1 The real chain, with the keys that exist today

| Hop | From | To | Key that exists | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Customer submission `inquiry_customer_submissions` (`WEB-YYYY-NNNNNN`) | Desktop inbox ledger `InquiryInboxItems.SubmissionId` | submission uuid | 3.0 migration 1005 |
| 2 | Desktop ledger | Desktop inquiry `Inquiries` (`INQ-YYYY-NNNN`) | `InquiryInboxItems.LocalInquiryId`, and `Inquiries.WebSubmissionId` | `InquiryRecord.cs:79`, migration 1005 |
| 3 | Desktop inquiry | Website mirror `public.inquiries` | `(source_id, source_inquiry_id)`, `source_revision`, `source_fingerprint` | `publish_inquiry_snapshot` |
| 4 | Website mirror | Website proforma | `inquiry_proformas.inquiry_id` (uuid of the mirror row) | `phase15.sql:19`, `proforma-api.ts:119` |
| 5 | Desktop PI | Website proforma | nothing exists | no publisher |
| 6 | Desktop PI | Desktop inquiry | `ProformaInvoices.InquiryId` (int) | `SavedProformaInvoice.cs:36` |
| 7 | Desktop inquiry | Desktop PI | `Inquiries.ProformaInvoiceId`, `ProformaNumber` | `InquiryRecord.cs:69-70` |
| 8 | PI | its revisions | `RootProformaId`, `RevisionNumber` (desktop); `revision` (website, same row) | both |
| 9 | PI | issued document | nothing exists | no issuance record |

### 12.2 What the schema can and cannot represent

| Relationship | Representable today? | Why |
| --- | --- | --- |
| Submission to published inquiry | **No** | `public.inquiries` has no `web_submission_id`; the link lives only on the desktop |
| Inquiry to website draft PI | Yes | `inquiry_proformas.inquiry_id` plus the open draft unique index |
| Desktop PI to website record | **No** | the identity check forbids `inquiry_id` on `source_system = 'desktop'`, and the client has no uuid to supply |
| Two PIs for one inquiry, one per origin | Yes, silently | nothing prevents a website draft and a desktop publication coexisting with different numbers |
| PI revision content | **No** | `inquiry_proforma_saves` stores counts and totals, not lines; the desktop stores a whole new row |
| Issued customer document | **No** | no table, no hash, no timestamp |
| Operator behind a publication | **No** | only `auth.uid()` of the bridge exists |

### 12.3 Proposed additions (design only)

| Object | Change | Purpose |
| --- | --- | --- |
| `public.inquiries` | add `web_submission_id uuid REFERENCES inquiry_customer_submissions(id)`, unique where not null | makes submission to inquiry and therefore submission to PI navigable |
| `public.inquiry_proformas` | add `client_public_id uuid UNIQUE` | offline-minted identity for a desktop PI, the idempotency anchor |
| `public.inquiry_proformas` | relax `inquiry_proformas_source_identity` per Decision A | allow a desktop row to carry its inquiry and its own number |
| `public.inquiry_proformas` | add `source_revision int`, `source_content_hash text`, `source_status text`, `pricing_version smallint`, `published_at timestamptz`, `published_by uuid` | provenance, conflict detection, mapping without rewriting history |
| `public.inquiry_proformas` | add `summary jsonb` | charges, discount, VAT, validity, terms reference, for desktop origin |
| `public.inquiry_proforma_revisions` | new table: `proforma_id`, `revision`, `content_hash`, `snapshot jsonb`, `issued_at`, `document_object_key`, `document_sha256`, `published_by`, `created_at`, unique `(proforma_id, revision)` | immutable history and the issued document; avoids the Phase B `superseded_by` conflict |
| `public.inquiry_proforma_events` | new table: `proforma_id`, `revision`, `operation`, `actor_user_id`, `actor_kind`, `operator_name`, `operator_station`, `occurred_at`, `request_id`, `details jsonb` | audit and attribution (Decision E) |
| `public.inquiry_proforma_lines` | add `group_name text`, `glass_type text`, `spec_name text`, `item jsonb` | the current line table cannot carry what a desktop PI prints |

### 12.4 Synchronization contract

| Concern | Rule |
| --- | --- |
| Stable identifiers | `client_public_id` is minted on the desktop at creation and never changes. `inquiry_proformas.id` is the server row. Neither is derived from a customer name, project name or phone number. |
| Origin | `source_system` is immutable after insert. |
| Idempotency | `Idempotency-Key = uuidv5(client_public_id, source_revision, content_hash)`, or any uuid the desktop stores in its outbox. Replay returns the same revision. |
| Revision numbers | The desktop's `RevisionNumber` is the source revision, monotonic per PI. A publication may only advance it. |
| Conflict detection | Publish requires `expected_source_revision`. A lower or equal value is a no-op only when `content_hash` matches; otherwise `40001`. The website's own draft uses its own `revision` and is untouched by desktop publications. |
| Retry | Outbox with exponential backoff, retried on reconnect, removed only on 2xx or on a terminal 4xx that the operator has acknowledged. |
| Deletion and voiding | Never delete. Void writes a status, a reason, an actor and a revision; the revision snapshot is retained. |
| Partial failure | A publication is one transaction: header, lines, revision snapshot, event. If any part fails, none is stored. |
| Offline edits | Local edits continue; the outbox holds the last unpublished revision per PI. Publishing an older revision after a newer one is rejected by the revision guard. |
| Reconciliation | A read function reports, per published PI, the last source revision and content hash, so the desktop can compare without downloading everything. |
| Audit history | Every publication, void and status change writes an event row with the actor model from Decision E. |

**Explicitly rejected.** Matching records by number alone (a desktop custom id can be
anything), by customer name, by project name or by phone. Phase B already forbade
this; this section confirms the schema cannot support it either.

---

## 13. Inquiry, PI and website responsibility boundaries

### 13.1 Inquiry module (V3.0)

| Responsibility | Detail |
| --- | --- |
| Receive and review website inquiries | existing inbox, delivery ledger, ingest; unchanged |
| Preserve the submission link | `WebSubmissionId` already stored; expose it in the register and pass it on publication |
| Show PI status and summary | read `ProformaInvoiceId`, `ProformaNumber` plus the canonical state; show a chip, never a second editor |
| Start or open the PI | one action, gated on `Prepared` or `Confirmed`, a safe BOQ and no existing PI; opens the PI module with the inquiry id |
| Show synchronisation errors | surface outbox state ("published", "pending", "rejected: conflict") without blocking local work |
| Prevent duplicate PI creation | keep `ValidateInquiryEligibility` and add the published-state check before allowing a new PI |

### 13.2 PI module (V3.0)

| Responsibility | Detail |
| --- | --- |
| Draft creation and editing | groups, rows, dimensions, quantity, rate |
| Grouped specifications | glass type, spec name, colours, per pane sheet data |
| Calculation and validation | one pricing core, versioned, ported from `SqmCalculator` plus `MoneyRounding` plus the surcharge and discount and VAT rules |
| Persistence and revision control | SQLite, `RowVersion`, `RootProformaId`, `RevisionNumber`, plus a draft journal |
| Issuance and document generation | one renderer, an issued snapshot with a hash, `sent` event |
| Audit history | local events plus a published mirror |
| Publication and recovery | outbox, idempotency, conflict handling, retry |

### 13.3 Website PI workspace

**Recommendation: the workspace stops being an editor of commercial documents.**

| Origin | Role |
| --- | --- |
| Desktop-origin PI (published) | Read only. Shows the published revision, the breakdown from `summary`, the issued document link, and the event history. No edit control, no save, no void. |
| Website-origin draft | Remains editable, but is labelled a pre-desktop estimate: lines, area, value, currency, no VAT, no charges, no print. It cannot be issued. |

Why not keep the website as a full editor: it has no catalogue, no charges, no VAT,
no document renderer, and adding them would create the second pricing implementation
this programme exists to remove. Why not remove the website editor entirely: website
staff may need to capture a quick line estimate before the desktop sees it, and the
existing API, tests and UI already support that narrow case. If the business says the
estimate has no value, delete it in a later phase rather than leave two editors.

---

## 14. Security and operational requirements

| Requirement | Verified current state | Gap | Requirement for Phase C |
| --- | --- | --- | --- |
| Authentication | Website routes verify the bearer token with `auth.getUser`; desktop has no network identity | desktop cannot authenticate as a person | publication only through a bridge account, never a browser key |
| Authorization | `has_inquiry_role` with `inquiry_admin` read and write, `inquiry_auditor` read; bridge role exists | no auditor account exists in production | keep the matrix, add bridge scope, do not widen read |
| Document privacy | bucket `inquiry-intake-documents` is private (`public = false`, 30 MB limit, verified live); policies are role and ownership gated | none found | unchanged |
| Upload validation | size, extension, detected MIME, SHA-256, quarantine flag | none found in the read path | unchanged |
| Request validation | ranges and UUID checks in TS and plpgsql | rate is client supplied and not cross checked against a group base | server must accept the desktop's computed values only for desktop origin |
| Service credentials | bridge uses the anon key plus an operator session; no `service_role` in the desktop or the browser | none found | keep, and never put a service key in WPF |
| Secret management | bridge password encrypted at rest in the desktop settings | password reuse across workstations | per workstation credentials if trusted attribution is required |
| Audit logs | desktop audit lines and `inquiry_sync_runs`; `inquiry_proforma_events` does not exist | no shared PI event log | add the event table and write to it |
| Idempotent saves | website yes, desktop no (local only) | publication has no idempotency | outbox key, unique revision |
| Concurrent editing | desktop `RowVersion`, website `expected_revision` plus `FOR UPDATE` | two editors can work the same inquiry | refuse to open a desktop PI that has a published newer revision without a warning |
| Duplicate request protection | unique indexes and save ledger | no protection across the two systems | `client_public_id` unique |
| Failed publication retry | none | publication is new | outbox and terminal error handling |
| Backup and recovery | website database backups are platform managed; the desktop migration runner takes a SQLite file backup before every upgrade and refuses to migrate without one (`SchemaMigrationRunner`, verified) | no PI-level backup contract and no draft journal | draft journal plus a documented file backup, and keep the runner's backup behaviour |
| Migrations | website: numbered SQL files, `create_migration`; desktop: versioned `PRAGMA user_version` runner | none | additive only, one migration per increment |
| Deployment and rollback | Vercel for the website; desktop is a shipped binary | no rollback plan for schema | every migration paired with a documented reverse and no destructive change |

**Standing constraints that this plan keeps.** No RLS disabled, no inbox access for
anon, no `service_role` in the desktop or the browser, no public bucket, no trusted
client identity or role, no `auth.users` access from the desktop, no new live roles
without approval, and no broadening of the delivery RPCs or the ledger.

---

## 15. Data migration and backward-compatibility risks

| Risk | Present today | Impact | Mitigation | Rollback |
| --- | --- | --- | --- | --- |
| Relaxing the origin check | 0 rows in `inquiry_proformas` (verified) | none now, a backfill later if deferred | do it in C1 while empty, with the old and new check both documented | restore the old check, no rows to fix |
| Desktop custom numbers | unknown distribution in customer files | a number that is not `PI-YYYY-NNN` would be rejected by a naive parser | treat numbers as opaque, validate only per origin | none needed, no rewrite |
| Revision model mismatch | new rows on the desktop, in place on the website | a published correction could look like a new document | one canonical row, revisions in a child table | drop the child table, keep the row |
| Area precision change | v1 documents exist only in customer files | reopening could display different areas | `pricing_version` gate | recompute with the stored version |
| Surcharge boundary change | as above | a 3.999999 m2 panel stops being surcharged | version gate plus a reconciliation report | revert the version assignment |
| Snapshot schema | `SchemaVersion = 2`, additive fields | older readers ignore new fields | additive only, never reinterpret | none |
| Website status vocabulary | three values, two unused | mapping is untestable on real data | map at read time, keep `source_status` | remove the mapping |
| `inquiry_proforma_lines` shape | no spec or charge columns | a published desktop PI would lose its detail | add `item jsonb` in C1 | drop the columns |
| Catalogue mutation | Step 4 writes `proformaId: null` | one invoice can change every future quote | document it, do not change it in Phase C | none |
| Desktop journal | does not exist | crash loses a wizard session | add it in C3 behind a flag | remove the table |

**Backward compatibility statements.**

- No existing desktop SQLite file is migrated destructively. New tables and columns
  only. **Recommendation.**
- No existing website row changes meaning. New columns are nullable or defaulted.
- No existing RPC signature changes. New functions are additive; the delivery RPCs
  and the inbox ledger stay untouched. **Verified fact** that they are separate
  objects today.

---

## 16. Phase C scope

**Framing.** Phase C here is the whole first delivery of a working pipeline, in five
reviewable increments. This supersedes the four phase sketch at the end of the Phase
B report, which was indicative. Each increment is independently shippable and
independently revertible. Data integrity, identity, pricing correctness and lifecycle
semantics come before any UI work.

### 16.1 Increment map

| Inc | Objective | Repository and module | Depends on | Files or database objects | Migration | Exit criteria | Tests | Rollback | Approval |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| C1 | Freeze the contract and add the additive schema | `website/` (SQL, types, tests) | Decisions A, B, D, E | new `supabase-inquiry-proforma-phaseC.sql`; `inquiry_proformas` columns; `inquiry_proforma_revisions`, `inquiry_proforma_events`; `inquiries.web_submission_id`; types under `src/types/proforma.ts` | additive, applied once, no data change | contract document reviewed; migration applied; existing RPCs untouched; 0 rows changed | SQL probes as admin, auditor, bridge, anon; constraint tests | reverse SQL in the same file header | Yes, all four |
| C2 | One pricing core and a parity fixture | `ProGlassAutomation3.0` under `Inquiry/Services/Proforma/`, plus the fixture shared into `website/tests` | C1, Decision F | `ProformaPricing.cs`, `pricing-fixture.json`, website fixture test | desktop: none yet | the same fixture passes in C# and in TypeScript, including all boundary cases | unit tests listed in 11.4 | delete the new files | Yes, on threshold and percentage |
| C3 | Local V3.0 PI module | `ProGlassAutomation3.0` under `Inquiry/Models/Proforma`, `Inquiry/Services/Proforma`, `Inquiry/Views` | C2 | migration 1006 and up, `ProformaInvoice.cs`, `ProformaInvoiceLine.cs`, repository, lifecycle, draft journal, one renderer, Inquiry chip and navigation | desktop schema, versioned forward-only runner, additive | a PI can be created from an inquiry, priced, saved, reopened, revised and exported offline | xUnit plus a manual Windows pass | restore the pre-migration SQLite backup that the runner creates and refuses to migrate without (verified in `SchemaMigrationRunner`) | No, if C2 approved |
| C4 | Publication and the portal read model | `ProGlassAutomation3.0` publisher plus `website/` read view | C1, C3 | `publish_desktop_proforma_v1`, `get_proforma_publication_state_v1`, `void_published_proforma_v1`, outbox, read-only workspace rendering | additive functions, bridge role only | a desktop PI appears once, a correction appears as a new revision, a stale retry does not overwrite, the portal cannot edit it | idempotency, conflict, tombstone, anon read, auditor read | disable the functions with a revoke, keep the rows | Yes, on attribution and on the read-only portal |
| C5 | Issuance, documents, recovery, hardening | both repositories | C4 | issued snapshot and hash, document storage key, `sent` and `accept` events, journal recovery drill, parity report, deployment notes | additive column and table use only | the issued document is retrievable and hashed; the dispatch date exists; recovery from a killed session works; acceptance criteria in section 17 pass | end to end script plus manual drills | feature flags off, documents retained | Yes, on issuance semantics |

### 16.2 C1 detail

- **Objective**: every later increment writes against a fixed contract.
- **Scope**: write `supabase-inquiry-proforma-phaseC.sql` containing only additive
  changes and the new read helpers; no write function is enabled for desktop
  publication yet. Publish the contract as a versioned document next to
  `docs/PHASE15_PROFORMA_CONTRACT.md`, not inside the Phase B report.
- **Acceptance**: the migration applies cleanly on a copy; `\d` shows the new
  columns; the existing `save_inquiry_proforma_v1` behaviour is unchanged, proven by
  re-running its existing probe; anon and auditor still cannot write.
- **Tests**: constraint rejection matrix (desktop row with a website number, website
  row with a desktop number, duplicate `client_public_id`, revision gap), and a
  read-only grant check.
- **Rollback**: reverse statements in the file header, and the fact that no row was
  modified.
- **Approval note**: the `inquiries.web_submission_id` column needs question 10 in
  section 19 in addition to decisions A, B, D and E.

### 16.3 C2 detail

- **Objective**: one arithmetic definition, provable on both sides.
- **Scope**: `ProformaPricing` as a pure static class with no WPF dependency; a JSON
  fixture of at least the 14 cases in 11.4 plus 30 random panels; a TypeScript test
  that runs the same fixture through `proforma-draft.ts`; a documented statement of
  which rules are configuration and which are code.
- **Acceptance**: both sides produce identical area, total, value and thresholds for
  every fixture row; a deliberate mutation of the threshold makes both tests fail.
- **Rollback**: new files only, no existing behaviour changed.

### 16.4 C3 detail

- **Objective**: a PI that exists, prices and survives a crash, entirely offline.
- **Scope**: schema 1006 (invoice, line, charge, event, journal), `PublicId` minted at
  creation, `RowVersion` and revision chain, number allocator that never parses a
  custom id, draft journal with debounced writes, one export renderer, Inquiry page
  chip and navigation, and the eligibility gate reused verbatim.
- **Acceptance**: create, price, save, reload, revise, export and reopen with the same
  totals; kill the process mid edit and recover from the journal; a second PI for the
  same inquiry is refused.
- **Rollback**: drop the new tables; because nothing outside the module reads them in
  this increment, the app returns to inquiry-only behaviour.

### 16.5 C4 detail

- **Objective**: the desktop publishes, the website reads, nothing else changes.
- **Scope**: `publish_desktop_proforma_v1` gated on `has_inquiry_role(['inquiry_bridge'])`
  and an active `inquiry_sync_runs` row, with `client_public_id`, `source_revision`,
  `source_content_hash`, `source_status`, `summary`, `lines` and an idempotency key;
  `get_proforma_publication_state_v1` for the outbox; `void_published_proforma_v1`;
  the website workspace rendering a published revision read only, with the desktop
  number as the only number shown.
- **Acceptance**: one row per document; a replay creates nothing; a stale revision
  returns a conflict and changes nothing; the portal shows the published totals
  without recomputing them; an auditor can read and cannot write; anon reads nothing.
- **Rollback**: revoke the new functions, keep the rows, and the website falls back
  to the current draft view.

### 16.6 C5 detail

- **Objective**: the document the customer received is knowable, and failures are
  survivable.
- **Scope**: issued snapshot with `document_sha256`, `issued_at`, `issued_revision`,
  optional storage object; `sent` and `accept` as events, not just statuses; the
  existing `Approved` path keeps its attachment requirement; recovery drill; parity
  report for historical rows; deployment and rollback notes.
- **Acceptance**: every issued PI has a retrievable document and a hash; changing the
  body after issue is refused; the audit trail names the operator, the bridge, the
  time and the revision; the acceptance criteria in section 17 all pass.
- **Rollback**: stop issuing, retain documents, and the portal continues to show the
  last revision.

---

## 17. Phase C acceptance criteria

1. One inquiry has at most one canonical PI per origin, and a desktop PI created from
   a published inquiry is linked to it, not duplicated. **Testable** by attempting
   duplicate creation from both sides.
2. Every number is allocated by the authoring system, never by a client, and is never
   reused, including after a void or a failed publication.
3. The same fixture produces identical area, value, surcharge decision, subtotal, VAT
   and grand total in the desktop core and the website code.
4. A published revision renders the same total on the portal as the desktop PDF.
5. Publication is idempotent: the same key twice yields one row and one revision.
6. A stale or conflicting publication changes nothing and reports a conflict.
7. No published total is recomputed by the website. **Testable** by publishing a
   deliberately unusual summary and observing it verbatim.
8. Status mapping is explicit, legacy statuses are preserved in `source_status`, and
   void is distinct from cancel.
9. An issued PI has a stored document, a hash and a timestamp, and its body is frozen.
10. The audit trail names the bridge account, the reported operator, the operation,
    the revision and the time.
11. An auditor can read everything and write nothing; anon reads nothing; no client
    holds a privileged key.
12. A killed desktop session loses no priced work, and a failed publication retries
    without duplicating.
13. No existing desktop PI changes its number, totals, status or attachments.
14. The website suite, the node bridge suite and the new desktop tests pass, with the
    counts recorded in the delivery note.

---

## 18. Dependencies and unresolved blockers

| # | Dependency or blocker | Blocks | Owner | Status |
| --- | --- | --- | --- | --- |
| 1 | Decision A (numbering display and the origin check) | C1 | business owner, document control | open |
| 2 | Decision B (issuance and lifecycle vocabulary) | C1, C4, C5 | business owner | open |
| 3 | Decision D (portal content and estimate label) | C1, C4 | business owner, commercial | open |
| 4 | Decision E (attribution sufficiency) | C4 | business owner, operations | open |
| 5 | Decision F (threshold and percentage as policy) | C2 | business owner, pricing | open |
| 6 | Decision C (canonical label for acceptance) | C4 | business owner | open, low impact |
| 7 | Desktop publisher has never run in production (`inquiry_sync_runs` = 0, verified) | C4 acceptance | engineering | open risk, not an approval |
| 8 | No `inquiry_auditor` account exists in production (verified) | read-only portal proof | operations | open |
| 9 | Windows build and test execution cannot happen here | C3, C5 proof | engineering | environmental |
| 10 | Issued document storage location and retention policy | C5 | operations | open |

**Note on item 7.** It is not a reason to skip C4. It is a reason to prove the
publication path in a disposable project or a branched database before any real
customer record exists, and to state plainly in the release note that the inquiry
publication had never been exercised before this programme.

---

## 19. Questions requiring business-owner approval

Each question is written so it can be answered yes or no, or by naming a value.

1. **Numbering.** May a desktop PI keep its existing number (for example
   `PI-2026-014`, or `INV-42` if that is what was typed) as the only number shown to
   the customer and to the portal? (Recommended: yes. Rejected alternative: a second
   `PI-DSK` number.)
2. **Legacy numbers.** Confirm that no existing PI number, printed or filed, may be
   rewritten or reissued under a new scheme.
3. **Sending and editing.** May a sent (issued) quotation still be edited, or must a
   change after sending create a new revision? (Recommended: a new revision, and the
   issued document stays available.)
4. **Acceptance evidence.** Must every accepted PI carry either a signed PI or an LPO
   number, as the desktop requires today, or may acceptance be recorded without a
   document?
5. **Expiry.** Is `Expired` set by a person, or computed automatically from the
   valid-until date?
6. **Portal content.** Must the portal show the full breakdown (glass, charges,
   discount, VAT, grand total) and the issued PDF, or only the total and the status?
   (Recommended: the full breakdown from the published summary, and the PDF.)
7. **Website estimate.** Should the website keep its line-only draft as a
   pre-desktop estimate, or should that surface be removed once the desktop module
   exists?
8. **Attribution.** Is a self-reported operator name, stored as data and labelled as
   reported, sufficient for audit, or is a trusted per-workstation identity required?
9. **Surcharge policy.** Is the 4 m2 threshold and a 20 percent uplift company
   policy? If yes, is 20 percent the default for every group, and may a person type a
   rate that overrides the switch? (Current desktop behaviour: threshold yes,
   percentage per group with a default of 0, override possible only until the area
   changes.)
10. **Submission to inquiry link.** Approve adding `web_submission_id` to the
    published inquiry so the operator queue can show which submission produced
    which inquiry and PI.

---

## 20. Final go/no-go recommendation

**Go, for C1 only, conditional on decisions 1, 2, 3 and 6 in section 19.**

C1 is additive, touches no existing row (0 proformas, 0 inquiries, verified), changes
no existing function, and fixes the two structural defects that would otherwise
poison every later increment: the origin identity check and the missing submission
link.

**No-go for C2 to C5 until the pricing policy (decision 9), the numbering display
(decisions 1 and 2) and the portal content (decision 6) are approved.** C2 hard-codes
the arithmetic, C3 hard-codes the numbering and lifecycle, and C4 makes the portal
customer-visible. Starting them on assumptions would convert open business questions
into silent permanent behaviour, which the standing rules forbid.

**Conditions that must hold before C4.** The publication path must be exercised in a
non-production project first, because it has never run (0 sync runs, verified). The
desktop must authenticate as the bridge and never as a privileged key. The portal must
be read only for desktop-origin documents.

**What is solid today.** The pricing rules on the desktop are coherent and
internally consistent, the money rounding is single-sourced, the PDF parity renderer
is real, the website save path is genuinely idempotent and revision checked, the
intake bucket is private, and the bridge pattern already exists with the right role
and the right revision machinery. The programme is not starting from a broken
foundation. It is starting from two correct halves that have never been introduced to
each other, and the introduction needs the six decisions above plus one honest
publication proof.

**No implementation has begun. This document is the only artefact produced.**
