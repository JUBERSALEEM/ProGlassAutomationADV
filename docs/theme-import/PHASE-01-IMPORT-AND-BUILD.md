# Phase 1 — Import and build hygiene

**Status:** In progress.

## Checklist
- [x] Fetch and pin source provenance: remote branch `arena/f1e0ac70-proglassautomation`, commit `2ce9856`.
- [x] Copy theme engine, catalog, contract, validation, nine theme families, all Light/Dark dictionaries, extras/components, customization/color-plate/font/icon services, analytics, Studio sources, and source verification assets.
- [x] Add the remote LiveCharts WPF dependency used by imported Studio/analytics sources.
- [x] Inventory all imported files against the source tree and verify no expected assets were omitted (241 entries; only local import notes are extra; see `REMOTE-SOURCE-MANIFEST.txt`).
- [x] Ensure imported verification/test source is excluded from application compile and all imported production source/XAML is included intentionally (SDK defaults; test `.cs` removed in the app project).
- [ ] Build Windows-targeted project and resolve all C#, XAML, resource URI, and dependency conflicts. **Blocked in this sandbox:** `dotnet` is not installed.
- [x] Add a portable source/contract verification run; adapted the remote verifier to validate the imported component boundary instead of incorrectly treating host ERP tokens as imported contract tokens.

## Verification executed
- `python3 Theme/step1.d.tests/step1.d.3.verify_sources.py` — PASS (104 typed tokens × two variants, 1,514 contrast checks, 125 XML documents).
- `python3 Theme/step1.d.tests/step1.d.4.CheckerRegression.py` — PASS (14 mutation checks).
- `python3 Theme/step1.d.tests/step1.d.5.verify_isolation.py` — PASS (nine packages, 47 contracts/styles in both modes).
- `python3 Theme/step1.d.tests/step1.d.6.verify_foundation.py` — adapted from standalone-demo cleanup assumptions to verify current ERP startup/pipeline/Studio/icon wiring; PASS.
- `Theme/step1.d.tests/step1.d.1.RuntimeChecks.cs` — adapted to exercise the ERP package adapter, 18 combinations, color plate overlay, and icon styles; isolated from the user customization file.
- Windows build is still required; no `dotnet` binary is present.

## Current notes
- `Theme/` and `step1.ThemeStudio/` were copied from the pinned source revision.
- `ProGlassAutomation.csproj` currently uses WPF SDK default item discovery; explicit inclusion/exclusion still needs audit.
- Added `LiveChartsCore.SkiaSharpView.WPF` 2.0.4. Existing SkiaSharp 2.88.8 compatibility must be confirmed on Windows restore/build.
- The sandbox lacks `dotnet`, so no compile result is available yet.
