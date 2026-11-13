# Nine-theme import: implementation plan and completion ledger

**Status:** In progress; integration phases remain open. Do not claim completion until all final acceptance boxes are checked.
**Source:** `JUBERSALEEM/ProGlassAutomation`, branch `arena/f1e0ac70-proglassautomation`, commit `2ce9856`.
**Goal:** Make all nine imported theme families a real, safe, reachable feature in this WPF application, including complete theme resources/engine, appearance modes, color plates, customization, icons, persistence, Studio UI, and verification. Preserve the existing ERP shell, data, and workflows.

## Phase sequence

| Phase | Scope | Completion evidence |
|---|---|---|
| 0 | Plan, inventory, provenance, baseline | This ledger and phase records |
| 1 | Import/build hygiene: nine themes, Light/Dark resources, engine, customization, plates, icons, analytics; dependency and test-source boundaries | Source inventory matches source branch; WPF project includes intended code/assets; Windows build passes |
| 2 | Runtime integration: safe initialization, application-resource adapter, atomic apply/rollback, mode/theme selection, legacy-key mapping, persistence | Existing app Settings controls the authoritative adapter; failure preserves last-known-good theme |
| 3 | UX: Theme Studio reachable from app; nine families, mode, isolated preview, color/plate/font/geometry/icon controls, apply/reset/import/export | UI tests exercise all controls, no unreachable imported views |
| 4 | Persistence/safety: schema validation, migration, recovery, dispatcher/event safety, rollback, safe import/export | Corrupt/invalid input and failed-activation tests pass |
| 5 | Full verification: key/type parity, contrast, icon/color coverage, visual smoke, app regressions | Windows build/test passes; all 18 family/mode combinations pass in the actual app |

## Hard requirements

- Exactly: Bento, Claymorphism, Fluent, Neumorphism, Liquid Glass, Glassmorphism, Spatial, Swiss, Holographic.
- Include both appearance packages, each family’s extras/components, engine/catalog/contract/validation, customization persistence/composition, shared geometry, font, color plate role, icon, and analytics services.
- Add a real user-facing entry point and connect it to the app’s actual theme runtime. Source copied but unreferenced is not implementation.
- Keep the current app's entry point and business workflows. Use a controlled adapter/resource slot; do not blindly replace global dictionaries.
- Accept only catalog-owned themes and validated settings; never load user-supplied XAML paths.
- Theme load/validation/activation failure must leave app running with prior known-good theme and show useful feedback.
- Record each phase's edits/tests/blockers. No 100% claim with any acceptance gate unchecked.

## Initial reality check

The remote branch is a standalone WPF theme demo; this repository is a different ERP application with its own `ThemeManager`, `VisualThemeEngine`, and Settings surface. The imported theme sources have been copied but were not initially connected to the app. Integration requires a compatibility adapter and actual UI routing, not replacing the shell.

## Current progress

- Phase 0 plan and per-phase checklists created.
- Phase 1 source inventory matches the pinned remote source (241 tree entries; local import notes are extra); imported engine/resources/Studio are present. LiveCharts dependency added and test code excluded from app compile.
- Portable source contract/contrast and checker mutation verification pass after scoping the source verifier to imported component resources.
- Windows build and runtime remain unverified (`dotnet` is absent in the current environment).
- Phase 2 is in progress: Fluent is added to the ERP painter/selector/config catalog; `ImportedThemeRuntime` loads the imported package slot, maps all nine families and modes, bridges typed tokens, and synchronizes Studio selections through `ThemePipeline`.
- Phase 3 is in progress: Settings opens the imported Studio; package overlays compose plates/fonts/geometry, and imported icon styles update ERP sidebar marks.
- Phase 4 has imported versioned-storage validation/recovery and failure rollback wired. Phases 2–5 remain open for Windows build, runtime, visual, persistence, and regression acceptance.

## Final acceptance checklist

- [ ] Nine families and Light/Dark load in the actual app.
- [ ] Actual ERP screens respond to theme changes (not just Studio preview).
- [ ] Color roles/plates and edits affect actual app resources and survive restart.
- [ ] Fonts, geometry, icons affect intended app surfaces and survive restart.
- [ ] Studio entry point and selection/preview/apply/reset/import/export work.
- [ ] Invalid/corrupt stored values and failed loads leave app running with previous theme.
- [ ] Full Windows build, imported tests, and app regression tests pass.
- [ ] Import inventory, adaptations, and dependency/license obligations documented.
