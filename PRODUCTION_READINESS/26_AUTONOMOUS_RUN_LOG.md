# Autonomous run log

Start: 2026-09-24T20:51:11Z
Branch: `arena/01a0ce09-proglassautomationadv`
Start HEAD: `98622ca`

## Cycle 1

- Finding: environment cannot compile or run the WPF app. `dotnet` missing. `dot.net` TLS failed.
- Severity: P0 environment blocker for verification, not an application defect.
- Fix: none possible locally. Recorded BLOCKED. Continued with static work.

## Cycle 2

- Finding: `Print.*` resources used by the quotation print preview were undefined. Paper background, padding, and font sizes would not resolve.
- Severity: P1
- Root cause: `Themes/Styles/PrintTokens.xaml` defined a different key family (`PrintSurfaceBrush`) than the view (`Print.Paper`, `Print.Font.*`, `Print.Margin.*`, `Print.Thickness.*`).
- Fix: defined the keys the view already requests, using the existing paper palette.
- Build/test/runtime: UNVERIFIED (no SDK). Static scan no longer reports those keys missing.

## Cycle 3

- Finding: other live bindings pointed at keys that do not exist (`Settings.Reset`, `CardBorderBrush`, `Ios26WarningBrush`, `Ios26TileBadgeBrush`, `Radius.Top.Large`, broken `Chart.*Alpha*` keys, `SurfaceBrush`, `Ov.AccentSoftBorder`, `SurfaceGlassOverlayBrush`).
- Severity: P2
- Fix: added the missing theme keys or pointed the view at an existing token.
- Shell keys missing from 14 language files were added so the key set matches. Runtime already fell back to en-US.

## Cycle 4

- Finding: release evidence must not ship.
- Fix: csproj item removal plus publish-folder delete in `scripts/build-installer.ps1`.

## Commit

`3e3aeff` fix: restore print and shell resources that the live UI already binds

Push: SUCCESS to `origin/arena/01a0ce09-proglassautomationadv`.

End HEAD after that push: `3e3aeff`.
Worktree after push: clean.

This log update is a follow-up so the evidence matches the commit that already shipped.

## Cycle 5 — money rounding and schema-upgrade warning

Date: 2026-09-25

- Finding: customer money still used `Math.Round(x, 2)`, which is banker's `ToEven`. Product policy in `MoneyRounding` is `AwayFromZero`. A half-fil such as VAT on 200.10 (10.005) became 10.00 instead of 10.01, so the tax invoice, statement, and quotation could disagree by one fil.
- Severity: P0 wrong money.
- Fix: line values, rates, VAT, discounts, statement aging, Step 7 grand total, PDF/Excel/CSV charge amounts, and specification target rates now call `MoneyRounding.Round`. Square metres, linear metres, and percentages were left alone. The LAM half-fil vector in `SpecificationTargetBuildTypeTests` moved from 77.62 / 776.20 to 77.63 / 776.30 on purpose. The rounding ratchet allow-list was lowered to the remaining unclassified calls (228).
- Finding: `SchemaMigrationRunner.Upgrade()` returning false was ignored, and startup still logged the database as ready.
- Severity: P1. Not a proven corruption case. On failure the schema is unchanged and a backup exists when one could be made.
- Fix: failures are recorded on the runner. Startup logs an error and shows a warning telling the operator not to enter new documents until it is fixed.
- Build / `dotnet test` / WPF runtime: BLOCKED. No .NET SDK. Do not treat this as production-ready.
- Still open: `ProformaLifecycleSync` logs a failed status sync and returns success to the delete caller. Profit-and-loss `ProfitLossStepViewModel.Calculation.cs` still mixes money `Math.Round` with quantities and was not converted. Optimizer silent-drop was not re-proven.
- Local commit: `f1b4143` on `f2249ea`. Push to `origin/arena/01a0ce09-proglassautomationadv` was rejected (remote is at `e78ee00`, which is ahead). Not force-pushed. Not merged, because the worktree already holds a large uncommitted rewrite.
