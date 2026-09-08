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
| 2 | No pagination | 🟠 Planned | Phase 1 |
| 3 | Dashboards load whole tables | 🟠 Planned | Phase 1 |
| 4 | `MetricQty` hardcodes delivery qty | 🟠 Planned | Phase 1 |
| 5 | Test ratio 1 : 7.6 | 🟠 Planned | Phase 4 |
| 6 | 13 files > 990 lines | ⚪ Deferred | Phase 1 |
| 7 | No obfuscation / anti-debug | ⚪ Deferred | Phase 0 |
| — | Root `.md` clutter | ✅ **Done 2026-09-08** | — |

---

## Phase 0 — Activate CI

**Why it is P0.** Nothing else on this list can be *proven* fixed until a
machine builds the project and runs 1,148 tests. Every other phase below ends
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
   knows it — the 1,148 figure is a count of `[Fact]`/`[Theory]` attributes,
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

**The problem, precisely.** 28 real `GetAllAsync()` call sites across 20
ViewModels. Each one materialises every column of a table into memory to
display or count it. Fine at 500 records; at 50,000 it is a multi-second UI
freeze on a machine that is also driving a cutting saw.

```
SheetStore 5 · TaxInvoice 4 · DeliveryNote 4 · Dashboard 4
Shared 3 · Reports 3 · ProformaInvoice 2 · JobOrder 2 · Specifications 1
```

**The good news.** This is not a design problem, it is an adoption problem.
A working pattern already exists:

- `Services/Database/GlobalQueryEngine.cs` — shared paging engine
- `Services/Inquiry/InquiryRepository.cs:62` — `Math.Clamp(query.PageSize, 10, 200)`
- `ViewModels/Inquiry/InquiryListViewModel.Part1.cs:705` — the UI side, with
  `TotalPages` and a "Showing 1–50 of 1,204" caption

**Steps**
1. Add `GetPagedAsync(query, page, pageSize, ct)` to each repository that
   lacks it, delegating to `GlobalQueryEngine`. Do **not** invent a second
   paging abstraction.
2. Migrate one list screen end-to-end as the reference (SheetStore — highest
   call count). Get it reviewed before touching the rest.
3. Roll out module by module, one commit per module.
4. Add a test asserting no ViewModel calls `GetAllAsync()` outside an
   explicitly allow-listed aggregate path — otherwise this regresses.

**Acceptance:** zero `GetAllAsync()` in `ViewModels/` except allow-listed
aggregate reads (which Phase 3 removes); every list screen shows a page
caption; the guard test from step 4 exists and passes.

**Risk: medium.** Touches every list screen. Do not batch it — one module per
commit, CI green between each.
**Estimate:** 3–4 days.

---

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

This is the only acknowledged `HACK` marker in 223,866 lines, and it sits on
a money path — which is precisely why it has survived: everyone is
(reasonably) afraid to touch it.

**Steps**
1. Write the characterisation tests **first**: capture current
   `MetricQty` output for every row type across a set of real invoices.
   These must pass before any change.
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

**The problem.** 29,578 test lines against 223,866 production lines — 1 : 7.6.
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
