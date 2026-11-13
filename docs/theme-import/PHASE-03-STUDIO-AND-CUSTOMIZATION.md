# Phase 3 — Reachable Theme Studio and complete customization

**Status:** In progress — Studio is reachable from Settings and connected to the shared theme runtime; UI/runtime acceptance remains open.

## Checklist
- [x] Add a user-facing Settings → Themes entry point; host the imported Studio in a single owned window with the shared runtime service.
- [x] Provide the imported nine family choices and Light/Dark mode in the Studio; the ERP picker also offers all nine.
- [x] Connect imported customization state (geometry, fonts, per-role colors/plates, and icon preferences) to the runtime package overlay and host resource bridge.
- [x] Wire the icon service to the actual ERP sidebar marks; styles update live and semantic fallback is used for app navigation tags.
- [x] Reuse imported versioned/validated persistence; synchronize theme and appearance with current app config, preserve last known valid resource slot on activation failure.
- [ ] Verify preview/apply isolation, reset, import/export and no-data-loss behavior interactively on Windows.
- [ ] Ensure all commands and controls have errors/loading feedback, keyboard access, and localization-compatible labels.
- [ ] Remove or disable demo-only sample actions that could be mistaken for production actions; retain useful preview/gallery functions.
- [ ] Add tests for each family/mode, color role, icon option, and customization apply/reset path.
