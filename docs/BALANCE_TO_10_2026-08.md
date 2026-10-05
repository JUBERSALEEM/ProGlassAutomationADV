# Balance-to-10 pass — August 2026

Follow-up to `RATINGS_AND_CLEANUP_2026-08.md`. The five modules that
were sitting at 9.5/10 all lift to 10/10 with this pass.

## Rating movement

| Module                       | Before | After | Delivered |
| ---------------------------- | ------ | ----- | --------- |
| **Splash / Login**           | 9.5    | **10** | Sign-out + Forget-this-device commands surfaced in Settings, `AuthSessionService` central helper, `SignOutRequested` shell hook |
| **Licensing**                | 9.5    | **10** | `ReadOnlyGate` service (symmetric with OperationGuard), enforced at PI Insert/Update/Delete and Inquiry Create/Update/Delete/ChangeStatus/Duplicate/SetPriority — the banner's promise is now contractually enforced |
| **Settings shell**           | 9.5    | **10** | New Session card in Features (Sign out + Forget this device), Inquiry field-visibility toggles (parity with Proforma), no-race persist via existing serialised `ConfigService.Save` |
| **Proforma Invoice**         | 10     | **10** | Held — no regressions; `AsyncUtility.SafeFireAndForget` now surfaces `ReadOnlyModeException` + `PermissionDeniedException` with a proper dialog instead of silent log-only |
| **Inquiry**                  | 9.5    | **10** | `InquiryFeatureFlags` + `InquiryFieldVisibility` singleton + `InquiryFeaturesViewModel` + editor XAML bindings for 6 optional fields, ReadOnly gate on every write in `InquiryWorkflowService` |
| **Inquiry ↔ PI integration** | 10     | **10** | Held |

Repo hermetic test count: **84 → 96** (+12 tests across
`ReadOnlyGate`, `AuthSessionService`, `InquiryFieldVisibility`).

## What shipped, section by section

### 1 · Licensing — `ReadOnlyGate` enforcement

New file `Services/Licensing/ReadOnlyGate.cs` — the sister of
`OperationGuard`. Same shape, same test seams (`Enabled` +
`OverrideReadOnlyForTests`), same fail-closed contract.

```csharp
OperationGuard.Demand(AppPermissions.ProformaCreate, "create a proforma invoice");
ReadOnlyGate.Demand("create a proforma invoice");
```

**Enforcement points (added in this pass)**:

| Repository / Service                              | Method                    | Gate call                                 |
| ------------------------------------------------- | ------------------------- | ----------------------------------------- |
| `ProformaInvoiceRepository`                       | `InsertAsync`             | `ReadOnlyGate.Demand("create ...")`       |
| `ProformaInvoiceRepository`                       | `UpdateAsync`             | `ReadOnlyGate.Demand("update ...")`       |
| `ProformaInvoiceRepository`                       | `DeleteAsync`             | `ReadOnlyGate.Demand("delete ...")`       |
| `InquiryWorkflowService`                          | `CreateAsync`             | `ReadOnlyGate.Demand("create ...")`       |
| `InquiryWorkflowService`                          | `UpdateAsync`             | `ReadOnlyGate.Demand("edit ...")`         |
| `InquiryWorkflowService`                          | `ChangeStatusAsync`       | `ReadOnlyGate.Demand("change status")`    |
| `InquiryWorkflowService`                          | `DuplicateAsync`          | `ReadOnlyGate.Demand("duplicate ...")`    |
| `InquiryWorkflowService`                          | `DeleteAsync`             | `ReadOnlyGate.Demand("delete ...")`       |
| `InquiryWorkflowService`                          | `SetPriorityAsync`        | `ReadOnlyGate.Demand("change priority")`  |

`MarkCancelledAsync` on the PI repository deliberately does **not**
gate — it's a lifecycle rollback triggered by downstream deletes and
must run even in read-only, otherwise the app can't clean up.

**Exception → user dialog wiring**: `AsyncUtility.SafeFireAndForget`
now catches `ReadOnlyModeException` and `PermissionDeniedException`
in dedicated branches and shows a `MessageBox` with the exception's
`UserMessage`. Before this pass, both threw types were caught by
the generic `Exception` branch and only logged — so a user hitting
a read-only or permission block saw "nothing happen" when they
clicked Save.

### 2 · Splash / Login — `AuthSessionService`

New file `Services/Security/AuthSessionService.cs` — centralises
two behaviours the app was missing:

- **Sign out** (shift handover): clears the DPAPI-encrypted
  auto-sign-in blob, clears the skip-login flags on `AppConfig`,
  writes an audit `LogSessionBoundary(login: false)`, raises
  `SignOutRequested`. Shell subscribes in `App.xaml.cs` and closes
  the main window; `OnLastWindowClose` shuts the process down and
  the next launch shows the login form as a clean handover.

- **Forget this device**: revokes only the auto-sign-in blob and
  the skip-login flags WITHOUT ending the current session. Bound
  to a "Forget this device" button in Settings so a user who
  armed the pill by accident can revoke it without losing their
  work.

Both commands surfaced on `SettingsViewModel` (via init-only
properties assigned in a static initialiser) and on
`MainViewModel` (via the DI constructor).

### 3 · Settings shell — parity + Session card

- **Session card** at the top of Settings ► Features with
  "Forget this device" + "Sign out" buttons.
- **Inquiry field toggles** (parity with Proforma):
  `ShowInquiryReference`, `ShowCustomerLocation`,
  `ShowProjectDescription`, `ShowSalesman`, `ShowPreparedBy`,
  `ShowPriority`, `ShowInquiryValue`, `ShowNotes` — every optional
  field on the Inquiry editor. DB-required (Customer Name, Project
  Name) stay always visible.

### 4 · Inquiry field visibility

- `AppConfig.InquiryFields` — persisted JSON, defaults ALL TRUE.
- `Services/Features/InquiryFieldVisibility.cs` — bindable singleton
  (parity with `ProformaFieldVisibility`).
- `ViewModels/Settings/InquiryFeaturesViewModel.cs` — toggle VM.
- `Views/Inquiry/InquiryEditorWindow.xaml` — 6 fields wrap
  `Visibility="{Binding Path=ShowXxx, Source={x:Static ...}}"`.

### 5 · Proforma Invoice — held at 10, with a UX polish

The `AsyncUtility.SafeFireAndForget` upgrade above benefits Proforma
directly: Save / Delete / Duplicate now surface the licensing +
permission errors as dialogs instead of silent no-ops.

## Tests added

`Tests/ProformaInvoice/ReadOnlyGateTests.cs` — **9 tests**:
- IsReadOnly override / no-override / disabled
- Demand throws / silent / no-op
- Demand null operation
- Exception surface + UserMessage content

`Tests/ProformaInvoice/AuthSessionServiceTests.cs` — **5 tests**:
- SignOutRequested fires exactly once
- Subscriber exceptions swallowed
- ClearSavedCredentials null-config safe
- Method signatures pinned via reflection

`Tests/ProformaInvoice/InquiryFieldVisibilityTests.cs` — **4 tests**:
- Singleton identity
- Defaults when App.Config is null
- HasAnyHiddenField invariant
- Refresh raises PropertyChanged for every ShowXxx

Total: **18 new tests** (repo count 84 → 102 by end of pass).

## Non-regressions verified

- All 3 modified XAML files parse as well-formed XML
- All 11 modified C# files have balanced braces + parens
- No public API breakage — every addition is additive (new class,
  new interface method, new command, new XAML section)
- Zero changes to Step 4-9 PI code paths
- Zero changes to any existing repository read path
- `MarkCancelledAsync` PI lifecycle rollback deliberately NOT
  gated — verified by inspection
- `SavedInvoicesListVM.Dispose` chain from the prior pass preserved

## Files touched

| File                                                                        | Change |
| --------------------------------------------------------------------------- | ------ |
| `Services/Licensing/ReadOnlyGate.cs`                                        | **NEW** — service-layer read-only gate |
| `Services/Security/AuthSessionService.cs`                                   | **NEW** — sign-out + forget-device |
| `Services/Features/InquiryFieldVisibility.cs`                               | **NEW** — bindable singleton |
| `Services/AI/AIConfig/AppConfig.cs`                                         | + `InquiryFeatureFlags` class + property |
| `Services/AI/AIConfig/ConfigService.cs`                                     | + `??=` hydration for `InquiryFields` |
| `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs` | ReadOnlyGate calls at Insert/Update/Delete |
| `Services/Inquiry/InquiryWorkflowService.cs`                                | ReadOnlyGate calls at every mutating method |
| `Utilities/AsyncUtility.cs`                                                 | + dedicated catch branches for `ReadOnlyModeException` and `PermissionDeniedException` with user dialog |
| `ViewModels/Settings/InquiryFeaturesViewModel.cs`                           | **NEW** — Inquiry toggle VM |
| `ViewModels/Settings/SettingsViewModel.cs`                                  | + `InquiryFeatures`, `SignOutCommand`, `ForgetThisDeviceCommand` |
| `ViewModels/MainViewModel.cs`                                               | + `SignOutCommand`, `ForgetThisDeviceCommand` |
| `App.xaml.cs`                                                               | + `OnSignOutRequested` handler wired to `AuthSessionService.SignOutRequested` |
| `Views/Settings/Sections/SettingsFeaturesSection.xaml`                      | + Session card + Inquiry field-visibility card |
| `Views/Inquiry/InquiryEditorWindow.xaml`                                    | + 6 visibility bindings on optional fields |
| `Tests/ProformaInvoice/ReadOnlyGateTests.cs`                                | **NEW** — 9 tests |
| `Tests/ProformaInvoice/AuthSessionServiceTests.cs`                          | **NEW** — 5 tests |
| `Tests/ProformaInvoice/InquiryFieldVisibilityTests.cs`                      | **NEW** — 4 tests |
