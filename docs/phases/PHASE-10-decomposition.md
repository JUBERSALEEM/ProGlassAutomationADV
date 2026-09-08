# Phase 10 — Decompose the God Files

**Severity: Moderate · Estimate: 5-7 days · Prerequisite: green build**

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
