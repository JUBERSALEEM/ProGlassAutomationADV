# Remediation Plan

Every open issue from the root README's *Engineering health* section, broken
into phases with entry criteria, concrete steps, and an acceptance test that
proves the phase is done.

**Sequencing rule:** Phase 0 gates everything else. Until CI runs, no other
phase can be *verified* — only written. Do not start Phase 2 or later without
a green build, because each of them changes code that computes money.

```
Phase 0  CI activation            ← BLOCKED on a permission you control
   │
Phase 1  Verification baseline    ← needs Phase 0
   │
   ├── Phase 2  Pagination                  (28 call sites, 20 ViewModels)
   ├── Phase 3  Dashboard aggregates        (4 ViewModels)
   └── Phase 4  Money-path hack             (OtherChargeRow.MetricQty)
   │
Phase 5  Test depth — pricing engine        ← needs Phase 4
   │
   ├── Phase 6  Decomposition               (13 files > 990 lines)
   └── Phase 7  Release hardening           (obfuscation, anti-debug)
```

---

## Status board

| Phase | Issue | Status | Blocked by |
|---|---|---|---|
| 0 | No CI | 🔴 **Blocked — needs your action** | GitHub App `workflows` scope |
| 1 | Build/test unverified | 🟠 Waiting | Phase 0 |
| 2 | Whole-table loads | 🟡 20 sites / 15 files left (was 25 / 18); 5 lookups fixed, 2 reclassified as non-defects | Phase 0 (to enforce) |
| 3 | Dashboards load whole tables | 🟡 1 of 4 migrated (Statement); splits fixed | Phase 0 |
| 4 | `MetricQty` hardcodes delivery qty | 🟡 Step 1 done — characterisation net landed | Phase 1, for steps 2–5 |
| 5 | Test ratio 1 : 7.4 | 🟡 Target-tolerance split **fixed** | Phase 0 |
| 6 | 13 files > 990 lines | ⚪ Deferred | Phase 1 |
| 7 | No obfuscation / anti-debug | ⚪ Deferred | Phase 0 |
| — | Root `.md` clutter | ✅ **Done 2026-09-08** | — |

> **What "fixed" means here.** Three correctness defects were found and
> repaired while working Phases 2, 3 and 5. None of them was on the original
> issue list — all three surfaced by asking whether a symptom was isolated
> when it was not. See [Done](#done) for the detail.
>
> **Every one is verified by simulation only.** There is still no .NET SDK in
> this environment, so no commit since `b86a3bc` has been compiled. The logic
> was re-implemented faithfully in Python and every assertion re-run against
> it, but that is not a green build. Treat these as *reviewed*, not *proven*.

### The one defect class behind all three

Each bug is the same mistake: a status or a threshold declared more than
once, in two spellings or two values, with no single home.

| | Where it split | Effect |
|---|---|---|
| Job Order | `"In Production"` vs `"InProduction"` | Overview under-counted In Production; disagreed with the Job Order dashboard |
| Delivery Note | `"Draft"` vs `"Dispatching"` | `Preparing + Dispatched ≠ TotalNotes` |
| Target pricing | `0.005m` vs `0.05m` | Same invoice read ABOVE TARGET on the quotation, AT TARGET in Job Profitability |
| VAT | `0.05m` / `1.05m` in three money paths | Nothing wrong *today* — all three said 5%. It would have gone wrong the day the UAE rate changed |

The structural fix is the same in each case — one canonical home
(`JobOrderStatusValues`, `DeliveryNoteStatusValues`, `TargetMarginTolerance`)
plus a source-scan ratchet that fails the build if a second declaration
appears.

### Two ratchets now hold the line

| Test | Counts | Baseline | Fails when |
|---|---|---|---|
| `PaginationRatchetTests` | `GetAllAsync()` call sites | 20 files / 28 sites | an unlisted file calls it, or a listed one stops offending |
| `StatusLiteralRatchetTests` | `.Status` compared to a bare literal | 12 files / 60 comparisons | a new file offends, a listed file grows, a ceiling goes stale, or a module has no canonical home |
| `TargetMarginToleranceTests` | target-status band literals | 0 outside the canonical file | any file classifies a target margin with its own number |
| `VatRateSingleSourceTests` | VAT literals next to VAT vocabulary | 0 outside `VatCalculator.cs` | any file applies VAT with its own `0.05m` / `1.05m` |

The status ratchet is the one that generalises: it does not know about
"In Production" or "Dispatching" specifically. It flags the *shape* — a
persisted status compared against an inline literal — which is the shape all
three bugs had. Grandfathered files are capped per file, so cleaning one up
and then adding a new comparison back to it still fails.

The largest remaining offender is
`Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs` at 17; it is
the natural first migration.

### VAT — the same class, found by generalising

Having found one duplicated threshold, I swept for the whole category: named
numeric constants whose value appears in more than one file. Most of the 32
groups were coincidental collisions (UI sizes, row limits). One was not.

The UAE VAT rate was declared canonically in `VatCalculator` and then
hardcoded independently in three money paths:

| Site | Was |
|---|---|
| `TaxInvoiceLineService.CalculateFinancials` | default parameter `vatRate = 0.05m` |
| `TaxInvoiceDetailViewModel.PrivateHelpers` | `Math.Round(afterDiscount * 0.05m, 2)` |
| `ProfitLossStepViewModel.VatDivisor` | its own `1.05m` — named *divisor* while the canonical one is named *multiplier* |

Unlike the other three defects this one was **not producing a wrong number
today** — all three said 5%. It was a latent fault: the day the rate changes,
the tax invoice detail screen and the financials overload would have kept
computing at the old rate while everything else moved. All three now reference
`VatCalculator`, whose constants are const and therefore valid as a default
parameter value.

`ProfitLossStepViewModel.EffectiveYieldFactor` also uses `0.05m`, but as a
wastage-yield floor, not VAT. It is deliberately left independent, and a guard
test stops the ratchet being widened into something that forces an unrelated
constant to track the tax rate.

---

## Phase 0 — Activate CI

**Why it is P0.** Nothing else on this list can be *proven* fixed until a
machine builds the project and runs 1,306 tests. Every other phase below ends
with "CI is green" as its acceptance criterion, so Phase 0 is not one item
among eight — it is the instrument the other seven are measured with.

**Why I cannot do it.** Verified empirically on 2026-09-08. Committing
`.github/workflows/build-test.yml` and pushing produces:

```
! [remote rejected] (refusing to allow a GitHub App to create or update
  workflow `.github/workflows/build-test.yml` without `workflows` permission)
```

The commit was reverted; the branch is unaffected. This is a GitHub
permission boundary, not a code problem.

**Your action — one of two:**

```powershell
# Option A: activate it yourself (30 seconds)
mkdir .github\workflows
copy docs\ci\build-test.yml .github\workflows\build-test.yml
git add .github ; git commit -m "ci: activate build-test" ; git push
```

Option B: grant the Arena GitHub App the **Workflows: Read & write**
permission on this repository (Settings → Integrations → GitHub Apps →
configure). After that I can do it, and can also maintain the workflow.

**Acceptance:** a `build-test` run appears under the Actions tab on the next
push, and either passes or fails with a real compiler error. A *failure* is a
successful Phase 0 — it means the instrument works.

**Then delete the duplicates.** `docs/ci/` and `docs/CI/` hold four workflow
files between them (`build-test.yml`, `dotnet-desktop.yml`, `build.yml`,
`release.yml`) with overlapping purposes and two different casings of the
same folder name. Keep one, delete the rest, and update `docs/CI.md`.

---

## Phase 1 — Verification baseline

**Entry:** Phase 0 green.

**Steps**
1. Run the full suite locally: `dotnet test Tests -c Release`.
2. Record the real pass/fail count in `RELEASE_GATE.md`. Nobody currently
   knows it — the 1,306 figure is a count of `[Fact]`/`[Theory]` attributes,
   not of passing tests.
3. Build with `TreatWarningsAsErrors=true` (already set in the csproj) and
   clear whatever surfaces.
4. Commit the dashboard work from `041a231` as *verified* or fix what the
   build rejects. It is currently shipped on static checks alone.

**Acceptance:** a documented baseline — "N tests, M passing, K failing" —
plus a green CI run on `main`.

**Risk:** low. Nothing changes; this phase only measures.
**Estimate:** 0.5 day, most of it waiting on runners.

---

## Phase 2 — Pagination

**Correction, 2026-09-08 — read this before migrating anything.**

The original framing was "28 `GetAllAsync()` call sites; paginate them." That
is wrong, and following it literally would break working features. I read every
one of the 25 remaining call sites. Most are not lists.

| | What it actually is | Sites | Right fix |
|---|---|---|---|
| **A** | Dashboard aggregate | 3 | **Phase 3** — metrics service, not pagination |
| **B** | Single-row lookup / existence check | 7 | A targeted repository method |
| **C** | Picker or dialog data source | 3 | Searchable and bounded |
| **D** | Full-text search | 3 | Push the filter into SQL |
| **E** | **A real list screen** | **9** | **Actual pagination** |

Paginating category B would break it outright. These are not lists:

```
DeliveryNoteListViewModel:590        allJobs.FirstOrDefault(j => j.JobOrderId == id)
SavedJobOrdersListViewModel:659      .FirstOrDefault(p => p.ProformaId == id)
TaxInvoiceViewModel:309, 474         siblings.Any(d => d.ProformaId == id && qty > 0)
TaxInvoiceDetailViewModel.InitNew:132  allDns.Where(d => d.ProformaId == id).Select(...)
AddEditSheetViewModel:293            standards fallback list
StatementGenerator.Helpers:103       dedupe scan by company name
```

Each wants a one-line repository query — `GetByIdAsync`, `AnyDeliveredByProformaAsync`,
`GetDeliveryNoteIdsByProformaAsync` — which is both faster *and* correct,
whereas a page of 50 would silently miss the row being looked up.

Category D does not want pagination either. `GlobalSearchViewModel` loads three
whole tables and runs an in-memory `.Where(Hit(...))` over them. Paging that
would search one page at a time. The fix is a SQL `LIKE` per module, returning
the top N matches.

Category A belongs to Phase 3 and is being handled there.

### What real pagination requires, per screen

`InquiryList` is the reference implementation, and copying it is not a
one-line change. There is **no shared pager control** — the pager is
hand-written XAML at `Views/Inquiry/InquiryListView.xaml:1409-1423`. Each of
the 9 category-E screens needs all four of:

1. A `PagedResult<TProjection>` repository method built on
   `GlobalQueryEngine.Instance.ExecutePagedQueryAsync(source, selector, page,
   Math.Clamp(pageSize, 10, 200), ct)`.
2. ViewModel state and commands — `CurrentPage`, `TotalPages`, `PageStatus`,
   `NextPageCommand`, `PreviousPageCommand`.
3. Pager XAML.
4. **KPI separation.** This is the part that is easy to miss. Several of these
   screens compute KPI cards from the *full* set —
   `OrderReportListViewModel.UpdateKpis(rows)` is one. Paginate the list and
   the KPIs silently start describing one page. Each needs its own aggregate
   query, which is the Phase 3 pattern again.

That is why this phase is estimated in days rather than hours, and why it
should be done one screen at a time with a build between each.

### Status

- **Ratchet landed** (`ebe9dfd`), tightened 2026-09-08 to flag only
  *unfiltered* `GetAllAsync()`. `SpecGroup.Part2.cs` calls
  `GetAllAsync(selectedType)` — already scoped to one spec type — and was
  wrongly on the list. Baseline is now **25 sites across 18 files**.
- **1 of the 4 dashboards migrated** (Statement) — see Phase 3.
- **0 of the 9 list screens paginated.**
- **5 of the 7 category-B lookups fixed** (2026-09-08). Two of them needed no
  new code at all — `IJobOrderRepository.GetByJobOrderIdAsync` and
  `IProformaInvoiceRepository.GetByProformaIdAsync` **already existed** and
  were simply not being called; the ViewModels loaded the whole table and
  filtered in memory instead. The other two
  `TaxInvoiceViewModel` sites loaded every delivery note in the database to
  answer a question about one proforma. They now call
  `DeliveryNoteRepository.GetByProformaIdAsync`, added for the purpose.

  Two details had to be preserved or the behaviour would have changed:
  `GetAllAsync` excludes Master DNs, and the call sites compared ProformaId
  with `OrdinalIgnoreCase` while SQLite `=` is case-sensitive — so the new
  query keeps the `!IsMasterDn` filter and lower-cases both sides. The
  Draft/Dispatching pair at one site also became
  `DeliveryNoteStatusValues.IsPreparing`, which is what those two comparisons
  meant. `TaxInvoiceDetailViewModel.InitNew` — which loaded the whole table to
  collect sibling IDs for one proforma — now uses the same method.

- **2 of the 7 were reclassified: they are not defects.** I had these wrong in
  the first pass and am correcting the record:

  - `AddEditSheetViewModel:293` loads every `GlassSheet` to build a size
    cascade filtered on category + thickness + colour. `GlassSheet` is
    **master data** — "each row = unique combination of Category + Thickness +
    Color + Size + Supplier" — so the table is bounded by the number of sheet
    types the business stocks, not by transaction volume. Loading it whole is
    correct.
  - `StatementGeneratorViewModel.Helpers:103` is `FindDuplicateStatementAsync`,
    which compares six filter fields using `SameText` (trim + ignore-case) and
    `SameDate` (nullable, compared on `.Date`). Pushing that into EF/SQLite
    cannot be verified without a build, and a wrong translation makes
    duplicate detection silently wrong in one direction or the other. The
    statement table is monthly customer aggregates, not transactions. **Left
    alone deliberately** — revisit with a compiler.

### The worst offender, found while verifying the above

`DeliveryNoteListViewModel.Part1.cs:834` loads **six full tables in
parallel**:

```csharp
var allTask      = _deliveryNotes.GetAllAsync();
var prepTask     = _deliveryNotes.GetPreparedAsync();
var delTask      = _deliveryNotes.GetDeliveredAsync();
var pendingTask  = _deliveryNotes.GetPendingQtyJobsAsync();
var reworkTask   = _deliveryNotes.GetReworkAsync();
var returnedTask = _deliveryNotes.GetReturnedAsync();
```

Five of those are **subsets of the first**. Every delivery note in the
database is materialised up to six times to populate six tabs, then filtered
in memory. **Partly addressed 2026-09-08.** Prepared and Delivered are now derived from
the single `GetAllAsync` result instead of being fetched again — same
`!IsMasterDn` exclusion, same projection, verified to produce identical sets
and identical ordering (Delivered sorts by `DeliveryDate` DESC, not `CreatedAt`,
so the sort is reproduced explicitly). Six round trips became four.

Rework and Returned are deliberately still fetched separately: unlike
`GetAllAsync` they do **not** exclude Master DNs, so deriving them would
silently change those two tabs. The tab model still needs rethinking for real
pagination.

## Phase 3 — Dashboard aggregates

**The problem.** Four dashboard ViewModels load entire tables to compute four
numbers:

| File | Line | Loads |
|---|---|---|
| `DnDashboardViewModel.cs` | 214 | all delivery notes + pending-qty jobs |
| `JoDashboardViewModel.cs` | 183 | all job orders |
| `SheetStoreDashboardViewModel.cs` | 103 | all sheet stock |
| `StDashboardViewModel.cs` | 152 | all statements |

**The fix already exists.** `OverviewDashboardViewModel` was migrated to
`IDashboardMetricsService.GetExecutiveMetricsAsync()` — one service call
returning aggregate DTOs via `AsNoTracking` projections that read only the
columns each figure needs. The comment at `OverviewDashboardViewModel.cs:303`
documents exactly why. The other four simply were not converted.

**Steps**
1. Extend `IDashboardMetricsService` with the aggregates those four need
   (delivery status counts, job status counts, stock totals, statement
   balances) if not already present.
2. Convert each ViewModel, following the Overview pattern verbatim.
3. Assert the displayed figures are unchanged — snapshot them before and
   after. A dashboard that quietly changes its numbers is worse than a slow
   one.

**Acceptance:** no `GetAllAsync()` in `ViewModels/Dashboard/`; figures
identical to pre-change; four service methods covered by tests.

**Risk: low-medium.** Contained to four files, and there is a working
reference implementation.
**Estimate:** 1–2 days.

---

## Phase 4 — The money-path hack

**The problem.** `Models/ProformaInvoice/Step6OtherCharges/OtherChargeRow.cs`
line ~308, `MetricQty`:

```csharp
if (IsDeliveryRow) return 1m;   // ← ignores TargetSelection entirely
```

The comment is honest about it: *"DELIVERY is per-invoice, not per-piece —
always = 1 trip… Proper fix (PER_ROUND source + editable qty) planned for
Chat 8."* Chat 8 happened. The fix did not.

This is the only acknowledged `HACK` marker in 224,583 lines, and it sits on
a money path — which is precisely why it has survived: everyone is
(reasonably) afraid to touch it.

**Progress, 2026-09-08.** Step 1 is done.
`Tests/ProformaInvoice/OtherChargeRowMetricQtyCharacterisationTests.cs` pins
current `MetricQty` behaviour for every branch — delivery, both polish keys,
custom polish, selection-driven rows, nothing-checked, no-selection, blank
key — plus branch precedence and the fact that the delivery predicate is an
exact case-sensitive match. Two tests are marked **WILL CHANGE**: they assert
the hack itself, and they are the ones Phase 4 is expected to break. A third
group asserts that non-delivery rows must *not* move, which is step 5's
acceptance criterion written as a test.

**Steps**
1. ~~Write the characterisation tests **first**: capture current
   `MetricQty` output for every row type across a set of real invoices.
   These must pass before any change.~~ **Done 2026-09-08** — see above.
2. Add a `PER_ROUND` charge source with an editable quantity, defaulting to
   `1` so existing documents do not change value.
3. Replace the hardcoded `1m` with the resolved quantity.
4. Verify PDF, Excel and on-screen all agree — the comment notes the current
   behaviour "matches PDF/Excel export behavior", so all three move together
   or none of them do.
5. Re-run the characterisation tests; the only differences permitted are on
   documents that explicitly opt into `PER_ROUND`.

**Acceptance:** no `HACK` marker in the codebase; characterisation tests
green; a multi-trip delivery can be billed without a manual Custom Charge.

**Risk: HIGH.** Money path, three output surfaces, and the current behaviour
is load-bearing for existing documents. Do not start this without Phase 1.
**Estimate:** 2 days.

---

## Phase 5 — Test depth on the pricing engine

**The problem.** 30,272 test lines against 224,583 production lines — 1 : 7.4.
(An earlier revision said 1 : 7.6; that divided by a production figure which
had counted the test files too.)
The suite is well *aimed* (workflow invariants, not getters) but thin where it
matters most: the Step 9 pricing engine.

**Steps**
1. Property-based tests over `Step9ProfitLoss`: for any valid input set,
   `cost ≤ price` implies non-negative margin; VAT rounding never drifts
   more than one fils; wastage never double-counts.
2. Golden-file tests: pin the full computed output for a handful of real
   invoices and diff on every change.
3. Table-driven VAT tests at the rounding boundaries (`.005`, `.015`,
   multi-line discounts).
4. Report the coverage number CI already collects (Cobertura) and set a
   floor that can only ratchet up.

**Acceptance:** pricing engine covered by property + golden tests; coverage
floor committed and enforced in CI.

**Risk: low** — tests only, no production change.
**Estimate:** 3 days. Highest value-per-risk on this list.

---

## Phase 6 — Decomposition

**The problem.** 13 files exceed 990 lines; `JobProfitabilityViewModel.cs` is
1,930. Partial-class splitting has contained the *file* size without reducing
the *responsibility* count.

**Steps**
1. Rank by change frequency (`git log --format= --name-only | sort | uniq -c`),
   not by size. A 1,900-line file nobody edits is not a problem.
2. Extract collaborator services — pricing, formatting, navigation — behind
   interfaces, starting with the top three.
3. One extraction per commit; behaviour unchanged; tests green.

**Acceptance:** no file over 990 lines in the top three most-changed files.

**Risk: medium.** Refactoring without a build is reckless — Phase 1 required.
**Estimate:** 4–5 days. Lowest urgency; do not do this before Phases 2–5.

---

## Phase 7 — Release hardening

| Item | Action | Note |
|---|---|---|
| Obfuscation | Add ConfuserEx or Dotfuscator to the publish pipeline | Release binaries are currently readable in dnSpy. Raises the tamper bar; does not make it safe. |
| Anti-debug | Detect attached debuggers at license-check time | Acceptable to skip for a desktop ERP. Decide explicitly rather than by omission. |
| Installer smoke test | Automate the `docs/INSTALLER.md` flow | `RELEASE_GATE.md` lists this as open. |

**Risk: medium** — obfuscation regularly breaks reflection and WPF binding,
which this codebase uses heavily. Test the obfuscated build end-to-end before
shipping it.
**Estimate:** 2–3 days.

---

## Done

**Phase 3, first migration: Statement dashboard — 2026-09-08.** The Statement
dashboard was the only one of the four with no metrics coverage, so it loaded
every statement into its ViewModel and summed the table there. It now calls
`IDashboardMetricsService.GetStatementMetricsAsync(5)`, added for this purpose
along with a `StatementMetrics` model.

Two findings worth recording, because both changed the design:

- **The money columns are TEXT.** `SavedStatement.TotalInvoiced`,
  `TotalOutstanding` and `OverdueAmount` are mapped
  `[Column(TypeName = "TEXT")]`, so a server-side `SUM` would not be a decimal
  sum. The aggregation therefore stays in memory inside the service. Pushing it
  into SQL needs a column-type migration first — do not "optimise" this with a
  `GroupBy`/`Sum` translation, it will silently produce the wrong total.
- **The constructor could not change.** `DashboardViewModel` calls
  `new StDashboardViewModel()` and DI is registered transient, selecting the
  greediest constructor. `IStatementRepository` stays injected and is still
  read in the startup guard — with `TreatWarningsAsErrors` on, an
  assigned-but-unread private field is a build error, not a warning.

`StatementMetrics.Recent` carries `StatementListItem` rather than a parallel
DTO. That type is already a blob-free SQL projection whose display strings are
computed from its own fields; re-declaring them would recreate the
duplicated-definition drift this codebase has spent the day removing.

The pagination ratchet's stale-entry test fired on this file, and
`StDashboardViewModel.cs` was removed from its allow-list — the ratchet working
as designed rather than being edited to stay green. Baseline is now
**27 call sites across 19 ViewModels**.

**Pagination ratchet — 2026-09-08 (`ebe9dfd`).** `Tests/Architecture/
PaginationRatchetTests.cs` scans every non-test `.cs` file for `GetAllAsync()`
call sites and fails if one appears in a file that is not on the allow-list —
and fails again if an allow-listed file stops offending, so the list can only
shrink. Baseline locked at 20 files / 28 call sites. This does not paginate
anything; it stops the number growing while the migration in Phase 2 happens.

**Job Order status split — 2026-09-08 (`122a32b`).** `SavedJobOrder.Status` is
a `string`, but `JobOrderStatus` is an enum whose member is `InProduction`, so
rows written through the enum store `"InProduction"` while older rows store
`"In Production"`. `DashboardMetricsService` matched only the spaced form, so
every enum-written job — i.e. every recent one — was excluded from
`InProduction`. The Overview dashboard reported fewer jobs in production than
the Job Order dashboard, which matched `"InProduction"`. Fixed by adding
`Models/JobOrder/Common/JobOrderStatus/JobOrderStatusValues.cs` (Delivery Note
already had the equivalent; Job Order did not, which is how three consumers
each grew their own literals) and routing the metrics service through it.
`StatusBreakdown` now groups on `Canonical()`, so one state can no longer
render as two bars. `ReportingDataService` keeps its own both-spelling checks
— correct today, worth migrating so the rule has one home.

**Delivery Note status split — 2026-09-08 (`fdfea80`).** Same shape, found by
asking whether the Job Order bug was isolated. `DeliveryNoteStatus` has no
`Dispatching` member, yet `DeliveryNoteMapper.cs` writes
`Status = "Dispatching"` at lines 65, 189 and 272, and `DeliveryNoteStatusMeta`
already renders both `"Draft"` and `"Dispatching"` as "Dispatching".
`DashboardMetricsService` counted `Preparing` as `"Draft"` only, so every
mapper-written note landed in *neither* bucket — `Preparing + Dispatched` did
not equal `TotalNotes`. `DeliveryNoteStatusValues` gained `Is`, `IsPreparing`,
`HasLeftTheBuilding` and `Canonical`; the metrics service delegates all five
status checks to it.

**Dashboard status splits — 2026-09-08.** Having fixed `DashboardMetricsService`
to match both job-status spellings, the two module dashboards were checked and
were wrong in the *opposite* direction. `JoDashboardViewModel` compared
`j.Status == "InProduction"`, silently dropping every legacy `"In Production"`
row from the KPI count, the status chart and the feed. `DnDashboardViewModel`
had no `"Dispatching"` constant at all, so mapper-written notes fell out of
every bar and their feed pills took the default colour.

Both now key on `Canonical()` rather than the raw stored string. A consequence
worth recording: the first DN bar changed label from **"Draft" to
"Dispatching"**, because that is what `DeliveryNoteStatusMeta` already displays
for both spellings and what `Canonical()` returns. The bars now sum to the
total; previously they did not. Each page also gained an invariant that the bar
label set equals the `Canonical()` output set, verified for both.

**Target-margin tolerance split — 2026-09-08.** The ABOVE / AT / BELOW TARGET
band was declared eight times with two values: `0.005m` in
`SpecificationTargetCalculator` and `TargetPriceModels`, `0.05m` in
`TargetSnapshotRecompute`, `JobProfitabilityRow`, `JobDetailViewModel` and
twice in `JobProfitabilityViewModel`. An invoice whose margin against target
fell between the two read **ABOVE TARGET on the quotation and AT TARGET in Job
Profitability** — the same symptom as the dashboard splits, in a different
module. All eight sites now call `TargetMarginTolerance`, and
`TargetMarginToleranceTests.No_Other_File_Declares_Its_Own_Target_Tolerance`
is a source-scan ratchet that fails if a ninth appears.

> **Decision needing your sign-off.** The unified value is **0.005m**, taken
> from the two Proforma sites because they classify first and persist the
> result, and `TargetSnapshotRecompute` exists specifically to *reproduce* a
> stored snapshot. The consequence is visible: margins between 0.005 and 0.05
> that previously read AT TARGET now read ABOVE or BELOW. If the business rule
> is really ±0.05, change `TargetMarginTolerance.AtTargetPercent` — one line,
> one place — and update its golden test. Do not reintroduce a local band.
>
> `JobMarginBands.TolerancePercent = 0.05m` was deliberately **left alone**:
> it measures achieved margin against the *base* margin, not the target, and
> `ProfitMarginKpiTests` pins its literal. A guard test asserts the two bands
> stay separate.

**Root document clutter — 2026-09-08.** 24 audit/analysis documents moved from
the repository root into `docs/` with `git mv` (history preserved). All 30
inbound references rewritten with correct relative paths per referrer, one
`../`-collapse bug found and fixed, and links re-verified: 231 markdown files,
0 broken. `README.md` is now the only document at the root. The documentation
index regenerated from 140 to 164 entries.

---

## How to use this document

Each phase ends with an acceptance criterion that a machine can check. If you
cannot state how you would know a phase is finished, it is not a phase — it is
a worry.

Update the status board as phases land. When Phase 0 is green, come back and
re-measure everything in the root README's health table: several of those
numbers (test pass count, warning count, coverage) are currently unknown
rather than bad, and CI is what turns them into facts.
