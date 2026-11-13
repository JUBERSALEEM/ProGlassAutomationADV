# Phase 5 — Verification and completion gate

**Status:** In progress — portable source/contract/mutation checks pass; WPF build and runtime acceptance are blocked on a Windows-capable environment with `dotnet`.

## Checklist
- [x] Run portable token/contrast/source verification (104 typed tokens × two variants; 1,514 contrast pairs; 125 XML documents).
- [x] Run source-checker mutation suite (14 mutations) and theme package isolation checks (nine packages, both modes).
- [x] Parse host XAML and project XML; `git diff --check` passes.
- [x] Confirm no new GitHub Actions workflow is included, per owner request. Windows build/runtime acceptance remains available to run manually; the existing workflow files are untouched.
- [ ] `dotnet restore` and `dotnet build ProGlassAutomation.csproj` on Windows with warnings/errors reviewed (`dotnet` is absent in current sandbox).
- [ ] Run imported theme runtime tests and existing application test suite; fix regressions, do not suppress failures broadly.
- [ ] Exercise all 18 family/appearance combinations in runtime tests with contract/type/resource validation.
- [ ] Verify colors/plates, icon resources, fonts, geometry, customization persistence, and active app screens in smoke tests.
- [ ] Verify keyboard/accessibility and startup recovery from invalid stored data.
- [ ] Compare final source file inventory to pinned remote revision and document deliberate omissions/adaptations.
- [ ] Update this checklist and `PHASE-00-IMPLEMENTATION-PLAN.md` only after every acceptance gate passes.

## Environment limitation
This sandbox currently has no `dotnet` executable. Source-level work can continue, but a 100% completion claim is gated on a Windows WPF build/test run.
