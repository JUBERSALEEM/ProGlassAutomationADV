# Pull Request — ProGlass Automation

## What
<!-- Describe the change, link to audit phase if applicable -->

## Why
<!-- Business reason, bug, audit finding ID -->

## How Tested
- [ ] `dotnet build -c Release` passes with `TreatWarningsAsErrors=true`
- [ ] `dotnet test` — 40 tests pass
- [ ] `dotnet format --verify-no-changes` — no drift
- [ ] Manual WPF smoke test (open Dashboard, PI wizard, ScreenRecord ComboBox)

## Phase Checklist (if applicable)
- [ ] Phase 1: CI wired, analyzers enabled, no new warnings
- [ ] No new `async void` (except event handlers)
- [ ] No new static event `+=` without `-=`
- [ ] No `Debug.WriteLine` (use `AppLogService`)
- [ ] No `Process.Start` without validation

## Screenshots / Recording
<!-- For UI changes, attach screenshot or screen recording -->

## Risk
<!-- Low/Medium/High + rollback plan -->
