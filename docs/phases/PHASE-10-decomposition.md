# Phase 10 — Decompose the God Files

**Severity: Moderate · Estimate: 5-7 days · Prerequisite: green build**

## Status — premise corrected, decomposition mapped 2026-09-09; nothing moved

| | |
|---|---|
| "12,162-line MainViewModel" | **that file does not exist** — see below |
| Current largest files measured | **done** |
| Worst methods mapped to exact line ranges | **done** |
| Code moved | **NOT done — deliberately** |

### The premise was wrong

`MainViewModel` is **1,467 lines across three partials** — `MainViewModel.Part1.cs`
(1,031), `MainViewModel.Part2.cs` (369) and `MainViewModel.cs` (67). It has
already been decomposed. There is no 12,162-line file in this repository; the
largest source file is 1,933 lines.

Acting on the phase as written would have meant looking for a decomposition
target that is not there.

### The real targets, measured 2026-09-09

| lines | file |
|---|---|
| 1,933 | `ViewModels/JobProfitability/JobProfitabilityViewModel.cs` |
| 1,417 | `ViewModels/Reports/OrderReport/OrderReportGeneratorViewModel.cs` |
| 1,300 | `ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs` |
| 1,272 | `Services/Export/Pdf/Renderers/PdfChrome.Part1.cs` |
| 1,246 | `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs` |
| 1,157 | `Services/Theme/VisualThemeEngine.Part1.cs` |
| 1,120 | `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs` |
| 1,105 | `App.StartupCore.cs` |

Note that four of these are already partials — `Part1` in the name means the
split has been started and not finished, which is a different problem from a
file nobody has touched.

### `JobProfitabilityViewModel` — the decomposition map

1,934 lines, **no `#region` markers at all**, 11 methods. Only 850 of those
lines are method bodies; the remaining ~1,080 are fields, properties and
constructors. The three that matter:

| lines | span | method |
|---|---|---|
| **350** | 1187–1536 | `LoadAsync` |
| 155 | 1687–1841 | `RecalculateTotals` |
| 91 | 1595–1685 | `ApplyFilters` |

`LoadAsync` at 350 lines is the single worst method in the codebase — larger
than every method in most files. It is also the entry point, so it is the first
thing anyone reads and the place where every data dependency is visible at once.
Extracting its stages into named private methods would do more for this file
than splitting it across partials, because it addresses comprehension rather
than file length.

Six methods are 40 lines or more; three are 80 or more.

### Why nothing was moved

Splitting a file is the one refactor where "behaviour unchanged" depends
entirely on the compiler: a moved method that loses an `using`, a field that
becomes ambiguous across two partials, an accessibility change. There is no
compiler in this environment, and `EnforceCodeStyleInBuild` with
`TreatWarningsAsErrors` means a single newly-unused `using` is a hard build
error, not a warning.

Extracting `LoadAsync` into stages is the right first move and it is a
mechanical, testable one — but it should be the first thing done on a machine
that can build, not the last thing done on one that cannot.


## Evidence

Thirteen production files exceed 990 lines:

| File | Lines |
|---|---|
| `ViewModels/JobProfitability/JobProfitabilityViewModel.cs` | 1,933 |
| `ViewModels/Reports/OrderReport/OrderReportGeneratorViewModel.cs` | 1,417 |
| `ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs` | 1,300 |
| `Services/Export/Pdf/Renderers/PdfChrome.Part1.cs` | 1,272 |
| `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs` | 1,246 |
| `Services/Theme/VisualThemeEngine.Part1.cs` | 1,157 |
| `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs` | 1,120 |
| `ViewModels/ProformaInvoice/Step9ProfitLoss/Step9ReportExport.cs` | 1,099 |
| `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs` | 1,059 |
| `App.StartupCore.cs` | 1,043 |
| `ViewModels/MainViewModel.Part1.cs` | 1,031 |
| `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs` | 1,009 |
| `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs` | 993 |

## The real problem is not line count

Most of these are already partial classes. Splitting a partial class further
reduces file length without reducing responsibility count — the `.Part1` /
`.Part2` suffixes in this list are evidence that this has already been tried.

The measure that matters is **how many reasons a class has to change**. Extract
collaborators, not file boundaries.

## Order of work

Do the ones where extraction also improves testability first.

1. **`App.StartupCore.cs` (1,043).** Startup is a sequence of independent,
   individually-failable steps crammed into one 900-line method. Extract a
   `StartupSequence` of named steps, each returning a result. This directly
   enables Phase 7.4 (reporting every database initialisation failure instead of
   only the first) and makes startup order a data structure that can be tested
   rather than a code path that can only be run.

2. **`Step9ReportExport.cs` (1,099).** Export logic with no WPF dependency in
   principle. Extract to a service so it can be tested without a dispatcher.

3. **`JobProfitabilityViewModel.cs` (1,933).** The largest. Separate the
   profitability *calculation* from the *presentation*. The calculation belongs
   next to `SpecificationTargetCalculator`, which is already pure and already
   tested — that is the pattern to follow, and the codebase already contains its
   own best example.

4. The rest, opportunistically, when a phase touches them anyway.

## Rules

- **No behaviour changes during extraction.** A decomposition phase that also
  fixes bugs cannot be reviewed — if the tests fail you will not know whether
  the move or the fix caused it.
- Extract to a **new collaborator with an interface**, not to another partial.
- Keep the public surface stable. `DashboardViewModel.cs` constructs several
  ViewModels with `new X()` *and* DI registers them; `AddTransient` selects the
  greediest constructor. Changing a constructor signature breaks both paths, and
  under `TreatWarningsAsErrors=true` an orphaned field becomes CS0414.
- One file per commit, so a mistake is cheap to revert.

## Acceptance criteria

- [ ] No production file exceeds 990 lines, **or** each remaining exception has
      a recorded reason.
- [ ] `App.StartupCore.cs` is a sequence of testable steps.
- [ ] Profitability calculation is pure and unit-tested independently of WPF.
- [ ] Zero behaviour change — proven by the existing test suite passing
      unchanged.
