# Build Verification Guide

**Written 2026-09-08 for the 16 commits between `b86a3bc` and `84d087b`.**

None of that work has been compiled. It was written in an environment with no
.NET SDK, no `mono`, no `csc` and no reachable NuGet feed. Every change was
verified by reading, by simulating the logic in Python, and by a
string-aware brace scanner — none of which is a compiler.

This guide tells you what to run, in what order, what "good" looks like, and
how to capture failures so they can be fixed in one pass instead of ten.

**Budget about 20 minutes.** Most of it is NuGet restore.

---

## 1. Prerequisites

| Need | Why | Check |
|---|---|---|
| Windows | `net8.0-windows` + WPF | — |
| **.NET 8 SDK** (8.0.x) | target framework | `dotnet --list-sdks` |
| Visual Studio 2022 **17.13+** *only if* you want to open the solution | `.slnx` is the newer XML format | — |

You do **not** need Visual Studio to do any of this. The CLI is enough.

### The `.slnx` trap — read this before you type anything

The repo has `ProGlassAutomation.slnx` and **no `.sln`**. The `.slnx` format is
not resolved by older 8.0.x SDK CLIs. If you run:

```
dotnet build          # ← will fail: cannot resolve ProGlassAutomation.slnx
```

you will get a confusing "project file does not exist" style error that has
nothing to do with my changes. **Always name the project explicitly**, which is
also what `docs/ci/build-test.yml` does:

```
dotnet build ProGlassAutomation.csproj
```

If you prefer the solution, install the .NET 9 SDK *alongside* 8 — it can build
`net8.0-windows` targets and it understands `.slnx`.

---

## 2. The commands, in order

Run these from the repository root. **Stop at the first failure** — later steps
depend on earlier ones.

```powershell
# 0. Confirm the toolchain
dotnet --info

# 1. Restore (slowest step; needs internet)
dotnet restore ProGlassAutomation.csproj --verbosity minimal

# 2. Build the app — Release, because that is what CI uses.
#    TreatWarningsAsErrors is TRUE in ProGlassAutomation.csproj, so a warning
#    here is a build failure. That is intentional and it is what we want to see.
dotnet build ProGlassAutomation.csproj -c Release --no-restore --verbosity minimal

# 3. Build the tests (TreatWarningsAsErrors is FALSE for the test project)
dotnet build Tests/ProGlassAutomation.Tests.csproj -c Release --verbosity minimal

# 4. Run the suite
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --no-build `
  --logger "console;verbosity=normal"
```

### What success looks like

- Step 2: `Build succeeded. 0 Warning(s) 0 Error(s)`
- Step 4: a summary line like `Passed! - Failed: 0, Passed: 1306, Skipped: 0`

`1306` is the count of `[Fact]`/`[Theory]` attributes in the tree. The real
passing number will differ slightly — `[Theory]` cases expand, and some tests
are skipped. **Record the actual number**; it is the Phase 1 baseline nobody has
ever measured.

---

## 3. If it fails — capture it like this

Please do not screenshot. Paste text. The fastest thing you can send me is:

```powershell
# full build log to a file
dotnet build ProGlassAutomation.csproj -c Release --no-restore `
  --verbosity normal > build.log 2>&1

# just the errors, deduplicated
Select-String -Path build.log -Pattern "error [A-Z]+[0-9]+" |
  ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique > errors.txt
```

Then send me `errors.txt`. For tests:

```powershell
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --no-build `
  --logger "trx;LogFileName=results.trx" --results-directory ./TestResults
```

and tell me the failed test names, or send `TestResults/results.trx`.

**What I need per error:** the code (`CS0103`, `CS1061`, …), the file, the line,
and the message. One error usually explains ten — send them all, I will group
them.

---

## 4. Where errors are most likely, in order

These are the things I could not verify. If the build fails, it is probably
here.

### Tier 1 — new types referenced across namespaces

| File | Risk |
|---|---|
| `Services/Dashboard/Core/DashboardMetricModels.cs` | Added `using ProGlassAutomation.Services.Statement.Repository;` so `StatementMetrics.Recent` can be `List<StatementListItem>`. A models file now depends on a repository type — deliberate, but it is a new edge. |
| `Services/Dashboard/Core/IDashboardMetricsService.cs` | New member `GetStatementMetricsAsync`. If `DashboardMetricsService` does not satisfy it you get `CS0535`. |
| `Services/Dashboard/Core/DashboardMetricsService.cs` | The implementation, plus `new StatementRepository()` and `using ...Statement.Repository`. |
| `Models/TaxInvoice/Common/TaxInvoiceStatus/TaxInvoiceStatus.cs` | New file. Needs `using System;` for `StringComparison` — I added it, but it is unverified. |
| `Models/JobOrder/Common/JobOrderStatus/JobOrderStatusValues.cs` | New file, same shape. |
| `Services/ProformaInvoice/TargetPricing/TargetMarginTolerance.cs` | New file. Its const is consumed by three other consts — legal in C#, but unverified. |

### Tier 2 — the const-to-const chains

A `const` may only be initialised from another `const`. All three of these are
const-to-const, which is legal, but none has been compiled:

- `SpecificationTargetCalculator.AtTargetTolerancePercent = TargetMarginTolerance.AtTargetPercent`
- `ProfitLossStepViewModel.VatDivisor = ProGlassAutomation.Services.Reporting.VatCalculator.VAT_MULTIPLIER`
- `TaxInvoiceLineService.CalculateFinancials(..., decimal vatRate = VatCalculator.VAT_RATE_DECIMAL)` — a **default parameter value**, which must be a compile-time constant.

### Tier 3 — the ViewModel rewrites

| File | What to look at |
|---|---|
| `ViewModels/Dashboard/Statement/StDashboardViewModel.cs` | `App.DashboardMetrics` static access; new `RecentStatementCount` const; `_statements` still read in the guard (it must be, or `CS0414` fires under `TreatWarningsAsErrors`). |
| `ViewModels/Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs` | `Canonical()` keying; `sd.Key` was removed from the `statusDefs` tuple. |
| `ViewModels/Dashboard/DeliveryNote/DnDashboard/DnDashboardViewModel.cs` | Same, plus the bar label changed `Draft` → `Dispatching`. |
| `ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.Part1.cs` | `prepTask`/`delTask` were removed; two tabs are now derived in memory. New `using`. |

### Tier 4 — the new tests

`Tests/` has `TreatWarningsAsErrors=false`, so warnings there will not block
you. But these make runtime assertions about source text and will fail loudly
if a path or literal is wrong:

- `Tests/Architecture/PaginationRatchetTests.cs` — allow-list must be exactly 15 files
- `Tests/Architecture/StatusLiteralRatchetTests.cs` — allow-list must be exactly 12 files / 60 comparisons
- `Tests/Agent/StatusVocabularySourceTests.cs` — asserts six literals exist verbatim

If a ratchet fails, read the message: it names the offending file and the
canonical home to use. **Do not silence it by editing the list** — that is the
whole point of it.

---

## 5. Runtime smoke test (only if the build is green)

Compile-clean does not mean behaviour-correct. These are the screens whose
numbers I changed. Check each against what you know to be true.

| Screen | What to check | Why |
|---|---|---|
| **Overview dashboard** | In Production count | Was under-counting enum-written jobs (`"InProduction"` vs `"In Production"`) |
| **Job Order dashboard** | In Production count + status bars | Was dropping legacy rows; should now **agree with Overview** |
| **Delivery Note dashboard** | Does `Preparing + Dispatched = Total`? First bar is now labelled **"Dispatching"**, not "Draft" | The two buckets did not sum before |
| **Statement dashboard** | All four KPIs and the recent-5 list | Now served by `GetStatementMetricsAsync` instead of summing in the ViewModel |
| **Job Profitability** | Rows with margin between 0.005 and 0.05 now read **ABOVE/BELOW TARGET** instead of AT TARGET | This is the deliberate tolerance change — confirm it is what you want |
| **Tax Invoice detail** | VAT amount on any invoice | Now reads `VatCalculator` instead of a local `0.05m`; the value must be unchanged |
| **Delivery Note list** | Prepared and Delivered tabs | Now derived in memory from one load — same rows, Delivered still sorted by delivery date |
| **Any picker / search** | Proforma picker, global search | These were deliberately *not* paginated; they must be unchanged |

The one to look at hardest is **Job Profitability**. It is the only place where
I changed a number users see on purpose, and it is a business rule I inferred
rather than one you confirmed. If ±0.05 is the real rule, change one constant:
`TargetMarginTolerance.AtTargetPercent`.

---

## 6. What is *not* a problem

So you do not spend time on these:

- **`.slnx` not resolving** — see §1. Use the `.csproj`.
- **`docs/ci/` files are not active CI.** `.github/workflows/` does not exist
  because the pushing App lacks the `workflows` scope. That is Phase 0 and it
  is yours to do.
- **The `HACK` marker in `OtherChargeRow.cs` is still there.** That is correct.
  Phase 4 step 1 (characterisation tests) landed; the fix itself did not, and
  the plan says not to start it without a green build.
- **`GetAllAsync()` still appears 20 times.** Deliberate. Only 9 of those are
  paginatable lists; the rest are lookups, pickers and searches. See
  `docs/REMEDIATION_PLAN.md` § Phase 2 before changing any of them.

---

## 7. After the build

Send me:

1. `errors.txt` (or "clean")
2. The test summary line — actual passed/failed/skipped
3. Anything from §5 that looks wrong on screen

With that I can finish Phase 2 properly — the nine list screens, one at a
time, with a build between each instead of nine guesses in one commit.
