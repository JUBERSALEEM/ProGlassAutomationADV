# Sheet Detail — Step 9 Corrective Rework: Final Report (2026-09-06)

## Scope

Modernize the Step 9 **SHEET DETAIL** tab to the proforma reality and
cross-check it against the **Step 6 WASTAGE CHARGE** setup — not a blind
number readout.

This report closes the corrective cycle that began when the wastage-sheets
(stock-based) model in commit `11d4136` was rejected. It documents what
shipped, how it was verified, and the single remaining action (a Windows
build/test run).

## Delivered model — BUY = USED + WASTAGE (leftover)

A job buys whole glass sheets and cuts them. The bought sheets split into
exactly two parts:

```
BUY SHEET  =  USED SHEET  +  WASTAGE (leftover)
```

| Term        | Meaning                                                            |
|-------------|--------------------------------------------------------------------|
| BUY SHEET   | Sheets bought, priced at the authoritative Step 9 glass row amount |
| USED SHEET  | Whole sheets required to cover cut SQM + wastage % (calculated)    |
| WASTAGE     | The LEFTOVER of cutting = BUY − USED (sheets, area, material value)|

- Wastage is **derived**, never purchased. There is no separate "wastage
  sheet" line, no offcut, and no scrap/remnant *value* for the leftover.
- The **wastage PRICE** is the Step 6 wastage charge
  (`WastageRow.ResultAmount`), read from the matching Step 6 row's SOURCE
  (outer / inner / lam outer / lam inner / middle), its SQM, SHEET QTY and
  rate — split across a row's checked sources by SQM share
  (`src.SqmValue / row.TotalSqm`), the same allocation Step 9's
  `AppendWastageCostRows` uses, so the two screens reconcile to the penny.

## Cross-check (not blind results)

| Condition                        | Badge      | Meaning                                    |
|----------------------------------|------------|--------------------------------------------|
| bought sheets < used sheets      | SHORTFALL  | Step 6 SHEET QTY below what is needed      |
| charge > 0, no leftover          | REVIEW     | charged for wastage that does not exist    |
| leftover > 0, no charge          | NO CHARGE  | leftover glass has no Step 6 charge        |
| otherwise                        | OK         | wastage charge matches the leftover        |

A shortfall can only arise from a Step 6 SHEET QTY override, because every
other buy path (SGU priced-by-area, DGU/LAM whole-sheet rounding) buys
exactly what is needed. The shortfall message names the Step 6 quantity so
the operator can correct the wastage setup.

## Formulas

```
requiredSqm    = AreaWithWastage(cutSqm, wastagePercent)
usedSheets     = WholeSheetsRequired(requiredSqm, sheetSqm)
buySheets      = priced-by-area ? usedSheets : glassRow.Quantity
wastageSheets  = buySheets − usedSheets          (negative ⇒ shortfall)
buyPrice       = glassRow.Amount                 (authoritative)
usedPrice      = requiredSqm × glassRow.PricePerSqm
wastageValue   = max(0, buyPrice − usedPrice)    (material leftover)
step6Charge    = Σ Step 6 source shares for this pane
```

The "priced by required area" basis is read from the glass row description
(the phrase `priced by required area`) — never guessed.

## Pane-side mapping (Step 9 → Step 6 source label)

| Step 9 side      | Step 6 source label |
|------------------|---------------------|
| Outer            | `Outer (W1/H1)`     |
| Inner            | `Inner (W2/H2)`     |
| Lam Outer        | `Lam Outer (W2/H2)` |
| Lam Inner        | `Lam Inner (W2/H2)` |
| Middle / Middle N| `{side} (W2/H2)`   |
| any other        | (none — no charge)  |

Only **active** wastage rows contribute a charge; a disabled row still owns
its SHEET QTY in Step 9 but carries no charge.

## UI contract

- Report **closed by default** (list + KPI strip); clicking a row opens a
  **full-screen detail** with `← BACK TO SHEET LIST`.
- GLASS TYPE column shows only the build family **SGU / DGU / LAM / DGU+LAM**
  — no outer/inner pane identity.
- Professional KPI cards; label/value rows use a fixed label column + fixed
  right-aligned value column (no far-left/far-right stretching).
- SHORTFALL (red) and REVIEW (amber) banners; per-row STATUS badge column;
  detail includes a STEP 6 WASTAGE CROSS-CHECK card and a job-level
  OVERALL REPORT.

## Changed files / commits

| File | Change |
|------|--------|
| `ViewModels/…/SheetProfitabilityViewModel.cs` | buy/used/wastage model, per-pane Step 6 source readout, cross-check states |
| `Views/…/SheetProfitabilityView.xaml` | KPI strip, banners, STATUS badges, full-screen detail, cross-check + overall report |
| `Tests/Agent/Step9SheetDetailTests.cs` | pins the model, Step 6 wiring, and the absence of any offcut surface |
| `docs/SHEET_DETAIL_STEP6_INTEGRATION_2026-09-06.md` | architecture doc for the model + cross-check |

Commit history (pushed to `arena/01a0733b-proglassautomationadv`):
`a1cb70c` → `7943f10` → `11d4136` (superseded) → **`1993019`** (current HEAD).

## Verification performed (Linux sandbox — no .NET SDK)

1. XAML well-formed (XML DOM parse) and **zero duplicate-`Style`** attributes.
2. ViewModel brace/paren/bracket balance correct.
3. All 52 `{Binding}` tokens resolve to real public properties on the
   ViewModel / row model; no missing bindings.
4. All `StaticResource` / `DynamicResource` keys resolve in the merged
   dictionaries.
5. Every `Assert` in `Step9SheetDetailTests` simulated against the committed
   sources — all pass, including `DoesNotContain("offcut")`.
6. `offcut` appears **0** times in production VM/view (6 intentional
   mentions remain in the test file only).
7. No stale rejected identifiers repo-wide (`WastageSheetPrice`,
   `OffcutPrice`, `TotalOffcutPrice`, `NetWastageVsOffcut`, `.UsedPrice`).
8. Build-risk audit of the project config:
   - `.editorconfig` sets `dotnet_analyzer_diagnostic.severity = suggestion`
     → CA####/IDE#### analyzer rules can never fail the build.
   - `Directory.Build.props` keeps `AnalysisMode` out of
     `AllEnabledByDefault`; `WarningsNotAsErrors` downgrades CS86xx/CS876x,
     CS0618, CS0414, CS0649, CS4014 → nullable/obsolete warnings are
     non-blocking.
   - Manual sweep found no escalatable warnings in the new code (no unused
     locals CS0168/CS0219, no member hiding, no CS0612 obsolete-without-
     message usage).

## Remaining action — Windows build/test (cannot run here)

Not executable in this sandbox: no .NET SDK, NuGet/`dot.net` network
endpoints blocked, and `net8.0-windows` + WPF cannot compile on Linux.

### Option A — CI (recommended)

Push the Windows gate (already staged in the workspace as
`.github/workflows/dotnet-desktop.yml`, identical to `docs/ci/dotnet-desktop.yml`)
from an account with `workflows` permission:

```
git add .github/workflows/dotnet-desktop.yml
git commit -m "CI: activate Windows build+test gate"
git push
```

The `build-test` job then runs on `windows-latest`:
restore Tests → `dotnet build -c Release` → `dotnet test`.

> Note: this session's GitHub App token lacks the `workflows` scope — a
> push of the workflow file is rejected with *"refusing to allow a GitHub
> App to create or update workflow"*. An admin/owner must push it.

### Option B — local Windows (with .NET 8 SDK)

```
dotnet restore Tests/ProGlassAutomation.Tests.csproj
dotnet build Tests/ProGlassAutomation.Tests.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --no-build
```

Any failure will be fixed as a new corrective commit on
`arena/01a0733b-proglassautomationadv` (no force-push, no reintroduction of
the rejected stock-based wastage-sheet model).
