# Optimization engine source audit — 7 November 2026

## Scope and reference

Audited the WPF optimization shell, tab views, `GuillotinePacker`, instrumentation, run orchestration, production bridge, cutting workflow and CNC export against GitHub `main` at `d862436c125c5a83ac86bdef9ccef34c587408b3` (fetched 7 November 2026). This is a source audit, not a runtime or production acceptance test.

## WPF workflow reflected on the website

The Optimization module is an eight-tab workflow, in this order:

1. Jobs
2. Sizes
3. Cut List
4. Stocks
5. Settings
6. Summary
7. 2D View
8. Report

Settings include thickness-based LM/RM/TM/BM margins, kerf, minimum breakout, cutting complexity, rotation, search effort/stage, plate pattern and offcut reuse. The report includes CSV, Excel, print PDF ticket, glass stickers, generic DXF and cut-list CSV. The inspected view labels Bottero `.OPT`, Lisec `.DAT` and Intermac `.ISO` as not implemented.

The new localized `/optimization` route reconstructs these eight surfaces with sample data and interactions. Its on-page notice states that the browser uses a separate illustrative packer; it does not run the WPF optimizer, WPF validation, or machine exporters.

## Findings before remediation

### Critical — production calls did not require guillotine feasibility

`PackOptimize` accepted `requireGuillotine = false` by default. The WPF run path, `ProductionOptimizationBridge`, and `CuttingWorkflowService` called it without enabling the policy. Thus a non-guillotine candidate could pass selection in normal application/production paths. The existing policy tests covered explicit-on behavior and explicitly pinned the default to off.

### Critical — audit information was not a release gate

`AuditCutReachability` recorded a note/count, but that did not make a result invalid. `VerifyComprehensive` covered geometry, material and piece conservation, but did not require a reachable cut sequence or replay-check its consistency. The bridge warned on `Verify` findings and continued toward allocation; the cutting workflow also used geometry-only `Verify` before continuing.

### Critical — CNC export could proceed after derivation failed

The export path attempted `TryDeriveGuillotineCuts` when a sheet had no cuts, but ignored `false` and proceeded to its writer. That allowed an export path to carry a layout with no derivable sequence.

### Audit ordering defect

`OptimizationInstrumentation.Instrument` audited cut reachability before repairing stale cut lists, despite the code comment requiring repair first. The count could describe an intermediate cut list rather than the repaired final plan.

## Changes made

- Made guillotine feasibility default-on for `PackOptimize`; an explicitly non-glass consumer can still opt out.
- Added `VerifyProductionReady`, which combines the comprehensive geometry/conservation checks with cut reachability and cut-sequence replay consistency.
- Applied the production gate to Quick/Optimize final results and the partitioned/industrial result paths, and made the WPF run display a failed gate as a validation issue rather than “Verified.”
- Made the production bridge stop before creating a session or allocating material on a failed production gate. Made the cutting workflow use the same gate before allocation.
- Added an all-groups CNC export preflight before opening the save dialog or writing files; a failed gate blocks the export.
- Reordered instrumentation so stale cut lists are repaired before reachability and consistency audits.
- Updated regression coverage for the default-on policy and production gate rejection of uncuttable or inconsistent layouts.
- Corrected the website tab order and export availability copy; added the localized workspace route and tests. The illustrative browser packer now honors a no-rotation input and does not count an empty/unplaceable batch as consuming a stock sheet.

## Verification and limitations

- Website: `npm run typecheck`, `npm run lint`, `npm run build`, and `npm run test:navbar` passed. The full Vitest run passed **51 files / 426 tests**, including the new workspace and browser-packer tests.
- WPF: .NET SDK is absent in this environment (`dotnet` is not installed). The C# changes and new xUnit regression tests therefore **have not been compiled or executed here**. Source-level correctness is not a substitute for a .NET build, regression suite, hardware/CNC replay, or large-job runtime verification.
- `npm run i18n:verify` reports eight locales marked complete below the verifier threshold (about 75%); the locale dictionaries already have incomplete coverage. `npm run i18n:keys` also stops on an existing route-overlay anchor assertion (`__chrome` missing `Mobile`). Generated key/coverage artifacts from that failed attempt were restored. The new route serializes only the selected locale's available translations; strings missing from a locale continue to fall back to English under the site's existing translation policy.
