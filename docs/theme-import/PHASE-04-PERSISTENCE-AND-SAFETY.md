# Phase 4 — Persistence, validation, and failure safety

**Status:** In progress — imported versioned customization storage and host/runtime synchronization are wired; failure/restart behavior needs Windows testing.

## Checklist
- [x] Audit imported serialization paths, storage location, versioning, and exception handling (`CustomizationStore`, version 2, bounded JSON).
- [x] Use catalog membership and imported state validation for theme names, appearance, colors, fonts, dimensions, icons, and plate stops.
- [x] Load corrupt/missing customization through the imported store's default-recovery path; host startup catches engine initialization failure and keeps the ERP theme active.
- [x] Only the fixed catalog loads WPF package URIs; no user-selected XAML paths are accepted.
- [x] Enforce dispatcher access in engine operations; sidebar icon handlers detach on Unloaded; failed imported activation schedules host rollback.
- [ ] Verify atomic persistence, preview/cancel semantics, restart restoration, corrupt-file recovery, and activation rollback on Windows.
- [ ] Add restart, corruption, unsupported-version, bad-value, and failed-activation tests.
