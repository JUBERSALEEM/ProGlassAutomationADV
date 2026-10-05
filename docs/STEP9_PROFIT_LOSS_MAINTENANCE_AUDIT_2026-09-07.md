# Step 9 Profit & Loss Maintenance Audit — 2026-09-07

## Rating before this pass

**A- / 88% — well maintained, but with two reputation-risk gaps.**

Step 9 is already strongly structured: calculation is split into partials, uses shared target-pricing services, parses multi-pane builds centrally, watches Step 4/5/6 sources with disposable subscriptions, respects the 0.5 m² billable panel floor, and keeps target profitability separate from actual-cost rows.

## Full senior findings

### Strengths already present

- Clear separation between actual P&L rows and target profitability.
- Exact pane parsing through `PnLSpecParser`, including SGU/DGU/LAM/DGU+LAM support.
- Shared Step 6 sheet allocation avoids duplicating mixed-utilization sheet quantities.
- Target model uses Step 4 pane-rate boxes and Step 9 wastage yield instead of allocated actual-cost rates.
- Live recalculation is coalesced and rebinding-safe.
- Warnings are exposed and persisted in the P&L snapshot.
- Sheet, service and glass-type analysis sub-tabs auto-refresh from the main calculation.

### Gaps found

1. **Revenue basis was still hardcoded to 5% VAT stripping.** Step 9 used `GrandTotalVatInclusive / 1.05`. That is correct only when VAT is exactly 5% and enabled. If VAT is disabled, zero-rated, exempt, or manually set to another percentage, Step 9 margin becomes wrong.
2. **Step 9 did not directly watch Step 7 totals.** Step 4/5/6 changes were watched, but Step 7 discount, VAT and round-to-grand-total changes could leave P&L margin stale until another event recalculated it.
3. **Row-rate edits were not a direct recalculation trigger.** Quantity/dimension changes were watched; rate changes affect revenue/target judgement and should queue Step 9 too.
4. **Status wording was outdated.** It said Step 6 wastage rows were excluded until optimization/utilization was implemented, but the current architecture already uses Step 6 wastage setup for sheet/detail allocation and excludes it only to avoid duplicate cost rows.
5. **No executive rating was visible.** Operators saw warnings, but there was no immediate “is Step 9 healthy?” rating and first improvement action.

## Improvements implemented

- Added a **net-revenue provider** to Step 9 binding. Step 9 now uses Step 7 `SubtotalBeforeVat` as the revenue basis when available, with the old 5% divisor only as legacy fallback.
- Bound Step 9 to the Step 7 summary as an `INotifyPropertyChanged` source, so discount/VAT/round-to-total edits queue live recalculation.
- Added Step 5 item `Rate` as a recalculation trigger.
- Corrected the status text: Step 6 wastage setup drives shared sheet allocation/sheet detail and is not duplicated as P&L production cost.
- Added Step 9 health/rating outputs:
  - `Step9MaintenanceScore`
  - `Step9MaintenanceRating`
  - `Step9ImprovementSummary`
- Added rating and first-improvement text to the Step 9 header.

## Rating after this pass

**A / 94% — well maintained and safer for approval/output.**

The remaining gap to A+ is not a specific bug found in this pass; it is the absence of a full runtime test run in a machine with the .NET SDK and a real WPF session. The sandbox cannot compile or execute the WPF app because `dotnet` is unavailable.

## Files changed

- `ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Binding.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.LiveRecalc.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CostBasis.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs`
- `ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Health.cs`
- `Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml`

## Validation

- XAML parse: pass.
- Edited C# brace balance: pass.
- `git diff --check`: pass.
- Full `dotnet build`: unavailable in sandbox (`dotnet: command not found`).
