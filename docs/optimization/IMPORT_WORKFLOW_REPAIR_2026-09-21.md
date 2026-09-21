# Jobs → pane import → Sizes repair (2.0.7)

This follow-up addresses the reported failure after port `7191a81` and result-contract fix `615885d`. It supersedes confidence in the earlier import-workflow review. The previous static checks did not establish a functioning data path.

## Actual data-path defect

The retained `PieceEntryVm.ParsePositiveInt` initialized its output to zero, parsed integer text into a different local (`direct`), and returned success without assigning the output. Therefore `1000 × 800, Qty 4` parsed successfully as `0 × 0, Qty 0`. The imported textual rows could exist while the derived sizes, expanded cutting plan and engine input quantities were wrong. This was not merely a view/binding issue.

The integer path now writes directly to `out value`. The source branch's parser assigned its outputs correctly; the integrated adapter had retained the defective helper. Whole-valued decimal support (`1000.0`, `4.0`) remains, without accepting fractional millimetres or quantities.

## Implemented workflow repairs

1. **Job card selection:** bind `ListBox.SelectedItem` to `SelectedJob`, attach the existing checkbox-change handler and select the job. Other checked jobs remain selected. Unchecking clears the selected-card pointer so that clicking the same card can select it again.
2. **Real saved sizes:** extract `BuildJobOption` from the loader and call it from both the production loader and regression tests. The path remains saved JO JSON → embedded invoice → production distribution → spec/position panes → merged W/H and quantities. Missing snapshots remain visible with warnings, never synthetic sizes.
3. **Add selected panes:** the button count/enabled state follows pane selection. Both bulk import and the pane gesture use `ImportPaneLayers`; per-job/spec/position identity prevents repeat-import duplication without dropping matching rows from distinct jobs. Material metadata follows the rows.
4. **Pane double-click:** import that actual pane, then open its glass group in Sizes. **This is a newly added requested action, not an upstream handler:** no double-click implementation was found in the optimization views/VMs/behaviors/styles at source `b43782d`.
5. **Cutting-row double-click:** navigate to that already-imported row's glass group in Sizes. Do not copy it again or multiply quantities. A separate **View imported sizes** button opens ALL groups. Handled editor/checkbox/button gestures are left alone.
6. **Sizes refresh:** a group change rebuilds size summaries and the cut plan immediately. Rebuild available groups *before* filtering rows; reset obsolete filters and reset to ALL after bulk import. Guard ComboBox reentrancy while rebuilding its collection.
7. **Tab boundary:** collapse each inactive `UserControl` root rather than only its inner Grid. This is a defensive input/hit-testing change, **not a reproduced diagnosis** of blocked clicks.

The engine, algorithm and pattern files were not replaced or modified. Protected service/model hashes still match the pre-port manifest. The 2M expanded-piece admission guard remains; there is no new million-scale performance claim.

## Focused comparison

| Concern | Previous integrated `615885d` | Source `b43782d` | Current 2.0.7 changes |
|---|---|---|---|
| Ordinary integer dimensions/quantity | Success returned with zero outputs | Outputs assigned correctly | Correct output assignment; whole-decimal adapter retained |
| Job card click | No SelectedItem binding | Checkbox-driven selection | Card/keyboard selection wired to existing checked-job flow |
| Explicit Add selected panes | Text rows imported; downstream parser broken | Explicit source import path | Shared import path, real positive values, count/enabled state |
| Double-click action | Absent | No implementation found in searched workflow | Pane imports; cutting row navigates without copying |
| Group selection | Setter did not refresh Sizes | Setter also did not refresh Sizes | Immediate refresh and stale-filter recovery |
| Advanced engine | Retained | Older engine, not imported | Retained, protected hashes unchanged |
| End-to-end working status | User reported failure; parser defect confirmed by inspection | Not run here | **Windows build/runtime acceptance pending** |

**Revised code-review ratings only:** import-path completeness: previous integrated **2/10**, source **7/10**, current **8/10 provisional**. Retained advanced-engine architecture remains **8/10**, not a throughput result. The earlier integrated import/data-contract **8/10** should not be read as reliability evidence; the parser defect invalidated that confidence. No verified-working rating is assigned to the repaired application before Windows acceptance.

## Evidence and limits

Executed in this sandbox:

- `python3 scripts/verify-optimization-port.py`: **11/11 portable source/contract checks pass**, including engine hashes, integer output assignment, selection/command/event wiring, refresh order, and root tab visibility.
- Negative control: the new integer-parser guard **fails against `615885d` as expected**. This is a source-level regression guard, not execution of the C# parser.
- Tree-sitter C# syntax parsing of seven changed/new C# files: no parse errors; **not compilation or type checking**.
- `python3 scripts/xaml-resource-lint.py`: clean across 337 XAML files; 116 warnings.
- `git diff --check`: clean.

Added `Tests/Optimization/OptimizationImportWorkflowTests.cs` for Windows execution:

- Parser returns actual W/H/Qty, accepts whole decimals, rejects invalid values.
- Serialized saved JO fixture → mapper → production pane projection → job-card selection → Add 4 selected panes → **4 rows / 10 physical pieces** in Sizes/cut plan and engine input expansion.
- Pane subsets, job deselection/reselection, pane and cutting-row commands, repeat-import idempotency, group switching/removal, inline quantity edits and missing-snapshot warnings.

**These C# tests have not run.** There is no .NET SDK here and WPF needs Windows runtime validation. Existing quantity-conservation and adapter tests also need execution; neither syntax checks nor portable source checks substitute for them.

## Windows acceptance

```powershell
dotnet restore ProGlassAutomation.csproj
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release
```

1. Open a confirmed job with two DGU specs. Click the card, not just its checkbox. Verify four pane checkboxes and **Add 4 selected panes**.
2. Add them. Check real W/H/Qty in the cutting list, then open Sizes. Totals must equal the approved Step 5 physical-pane totals (not just the count of row records).
3. Double-click a pane to import/open it, and an imported cutting row to view its group. Repeat each: quantities must not increase. Double-click Qty text: editing must remain usable without navigation.
4. Switch ALL/specific glass groups; remove the last row of the selected group; import more panes; edit Qty. Verify summaries and expanded rows refresh immediately. The display cap is 500 rows, not an engine-input cap.
5. Untick/reselect the card; combine two jobs with matching references/dimensions; repeat imports. Test missing-snapshot warnings, search/refresh and all eight tab transitions.
6. Run QUICK and OPTIMIZE, then complete the existing handoff's Step 4/5, feed/session, inventory, export and large-job acceptance scenarios before release.
