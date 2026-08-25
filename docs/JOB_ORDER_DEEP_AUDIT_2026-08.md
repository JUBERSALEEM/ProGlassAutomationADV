# Job Order Module — Deep Senior Audit (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## What was done

A full senior audit of the Job Order module (repository, mapper, models,
all ViewModels, and the XAML/code-behind) for bugs, security issues,
memory leaks and performance problems. Fixes were applied and are backed
by hermetic tests.

---

## 1. Memory leaks — static-event subscribers never detached

The module's child ViewModels subscribed to static events that were never
detached, so they stayed alive for the whole session and kept re-running:

- `SavedJobOrdersListViewModel` subscribed `DocumentLifecycle` (a static
  cross-module signal) and `SampleDataService.DataSeeded`. Its `Dispose()`
  only detached `DataSeeded`.
- `SectionActivityViewModel` subscribed `AuditService.ActivityLogged` (it
  is `IDisposable`, but nothing disposed it).
- `JobOrderViewModel` (the shell) was never disposed by `MainViewModel`.

**Fix:**
- `SavedJobOrdersListViewModel.Dispose()` now also calls
  `DocumentLifecycle.Unsubscribe(LoadJobOrdersAsync)`.
- `JobOrderViewModel` now implements `IDisposable` and disposes
  `ListVM` and `ActivityVM`.
- `MainViewModel.Dispose()` now calls `JobOrderVM?.Dispose()`.

Without these, switching modules or closing the window leaked the Job
Order ViewModels and their static handlers.

## 2. Performance — list & God View loaded every snapshot blob

`GetAllAsync()` returns the full `SavedJobOrder` entity, which carries the
entire serialized `FullSnapshotJson` snapshot blob per row. The saved
list and the God View only render indexed columns (JobOrderId, customer,
project, value, status, priority, dates) — none of which live in the blob.

**Fix:**
- Added a narrow `JobOrderListRow` projection model (no `FullSnapshotJson`).
- Added `IJobOrderRepository.GetListRowsAsync()` — `AsNoTracking`, projects
  only the indexed columns.
- `SavedJobOrdersListViewModel` and `JobOrderGodViewViewModel` now build
  their rows from `GetListRowsAsync()` instead of `GetAllAsync()`.
- `JobOrderRowViewModel` gained a lightweight `JobOrderListRow`
  constructor (kept the full-entity one as a backward-compatible overload),
  sharing one private `ResolveDisplays()` core so display fields never
  drift between the two.

This avoids pulling (and materialising) the snapshot blob for every row
on every refresh.

## 3. Consistency — emoji replaced with Segoe MDL2 glyphs

The right-click context menu in the saved list used emoji (`📂 📤 📄 📊 🏷️
🗑️`). Every other surface in the app uses Segoe MDL2 glyphs. Replaced
them with the proper glyph codes so the menus match the rest of the UI.

## 4. What was checked and found already sound

- **Security**: `OperationGuard.Demand(...)`/`PermissionService.Require(...)`
  at the repository and VM boundaries (cannot be bypassed). Search uses
  `EF.Functions.Like` with escaped wildcards (no SQL injection). Snapshot
  deserialisation catches and returns null (no crash on corrupt data).
  Delete has a `DependencyGuard` + attachments gate.
- **Auth**: create/update/delete all gate on `JobOrderEdit`/`JobOrderDelete`.
- **DB**: WAL + busy_timeout + foreign_keys=ON via `SqliteResilience`;
  every repo method uses `using` (no DbContext held); save path wrapped in
  `SqliteResilience.SaveWithRetryAsync`.
- **Reentrancy**: load methods use an `Interlocked` guard.
- **Race on ID**: `SuggestNextIdAsync` loops with a 99999 cap + GUID fallback.
- **EF indexes**: unique ID index + search/filter/sort indexes present.

## 5. Tests added

`Tests/JobOrder/JobOrderModuleAuditTests.cs`:
- `JobOrder_shell_disposes_child_viewmodels`
- `List_viewmodel_detaches_the_static_lifecycle_subscription`
- `Repository_exposes_a_narrow_list_projection`
- `List_and_god_view_build_rows_from_the_projection`

## 6. Verified

All modified `.cs` + model files balance; all Job Order XAML parses; no
property-set-twice; 0 broken bindings.
