# Proforma Invoice module — senior-dev audit (August 2026)

Full 9-step review of the Proforma Invoice wizard. Bugs are numbered
by severity (`P1` = must-fix, `P2` = should-fix, `P3` = nice-to-have)
and grouped by area. Every `P1` in this pass was fixed in the
accompanying commit; `P2` / `P3` items are documented for a
follow-up ticket.

## Scope

| Step | Screen                    | LOC (XAML + VM) | Findings |
| ---- | ------------------------- | --------------- | -------- |
| 1    | Company setup             | 823             | 1 UX, 0 bug |
| 2    | Client profile            | 764             | 0 UX, 0 bug |
| 3    | Project specs             | 579             | 1 UX, 0 bug |
| 4    | Pricing estimator         | 296 + VM        | 0 bug found in this pass |
| 5    | Data grid                 | 473 + big VM    | 0 bug found; already hardened via `DispatcherPriority` batching |
| 6    | Other charges             | 564             | 0 bug found |
| 7    | Print preview             | 126 + code-beh. | 0 bug found; popup dismissal is proper |
| 8    | Attachments               | 168             | path-traversal already hardened (`S-8`) |
| 9    | Profit & loss             | 317             | 0 bug found; pure MVVM |

Wizard shell (`ProformaInvoiceViewModel`, 530 LOC) reviewed
separately — see §"Cross-cutting" below.

---

## Feature: user-toggleable fields (Steps 1-3)

**Why.** The user asked for a way to hide fields they never fill in
without a code change. Small glass shops don't invoice with LPO
numbers or attention lines; a busy hourly operator wants a compact
form. The wizard is now:

```
Settings ► Features ► Proforma Invoice
       │
       ├── Enable / disable each optional field
       │       (iPhone-pill PiToggleSwitch)
       │
       └── Enable all / Hide all optional / Reset defaults
```

Wiring:

1. `AppConfig.ProformaInvoiceFields` — persisted JSON, one bool per
   field. Defaults ALL true (upgrade is byte-identical).
2. `Services.Features.ProformaFieldVisibility.Instance` — WPF-
   bindable singleton mirror. Raises `PropertyChanged` on every
   `Refresh()` so wizard fields collapse / re-appear live.
3. `ViewModels.Settings.ProformaInvoiceFeaturesViewModel` — the
   toggle-row viewmodel. Each setter writes the flag, persists via
   `ConfigService.Save`, and calls `ProformaFieldVisibility
   .Instance.Refresh()`.
4. Every optional field in `CompanySetupView.xaml`,
   `ClientProfileView.xaml`, `ProjectSpecsView.xaml` wraps its
   `StackPanel` in a `Visibility` binding to
   `ProformaFieldVisibility.Instance.ShowXxx` through the app-wide
   `BoolToVisibilityConverter` (Collapsed = zero layout space).

Design constraints honoured:

- **DB-required fields never appear as toggles.** Company Name,
  Customer Name and Proforma ID stay always visible; hiding them
  would leave the DB with an empty `NOT NULL` column.
- **Hiding a field never drops its value.** The underlying VM
  property still binds to the DB serializer; a `BillingAddress`
  hidden and re-enabled shows the same text.
- **No app restart.** `ProformaFieldVisibility.Refresh()` marshals
  onto the UI dispatcher and fires per-property notifications so
  the wizard reacts on the same frame the pill flips.

## Bugs & fixes

### `P1` — none discovered in this pass ✅

The nine steps have been through multiple prior audit rounds
(`BUG-33 → BUG-81`, `C-01 → C-11`, `V-13`, `S-8`) so the low-hanging
`P1` fruit was gone. Everything I looked at compiled to safe patterns:
`Task.Run` around EF calls, `RaiseOnUi` on cross-thread events,
`SanitizeName` blocking path traversal, `AsyncUtility.SafeFireAndForget`
around every fire-and-forget lambda.

### `P2` — event-handler subscription lifetime

`ProformaInvoiceViewModel` subscribes to `Step8VM.AttachmentUploaded`
and `Step2VM.PropertyChanged` in its constructor and never
unsubscribes. Not a GC leak *today* because the parent and children
share the app lifetime, but a `ProformaInvoiceViewModel` re-created
in tests (or in a future multi-instance / multi-window scenario)
would leak both closures.

**Recommendation** — implement `IDisposable` on the wizard shell,
unhook the two event handlers in `Dispose()`, and call it from
`MainViewModel` when the tab is closed. Deferred; not yet a leak
in the shipping app.

### `P2` — `MainViewModel` OpenLicenseSettingsCommand style used elsewhere

Not related to Proforma — flagged in previous audits.

### `P3` — inline validation on hidden fields

If a user hides a field that was populated with an invalid value
(e.g. TRN "1234"), the validation message stays inside the collapsed
`StackPanel` — the operator can't see it. The validators still allow
save because they only reject non-empty invalid values (looked at
`TrnValidationMessage` and `CustomerTrnValidationMessage`; both
short-circuit on `IsNullOrWhiteSpace`).

**Recommendation** — when a field is hidden, treat any inline
warning it owns as suppressed for step-advance gates. No code change
needed today because step-advance in this wizard is not gated on
field validity (`CanGoNext => CurrentStepNumber < TotalSteps`).

### `P3` — 3-column grid rows leave whitespace when one field is hidden

Step 1's identity row and rep contact row use fixed
3-column `Grid` layouts. Hiding one column collapses the field but
leaves the column definition, so you get an empty gap. Cosmetic
only — content flows correctly.

**Recommendation** — a follow-up can adopt a `WrapPanel` or a
dynamic-column-count layout so hidden fields reflow the row. Not
worth the code churn until a user complains.

## Security review

| Area                             | Verdict | Notes |
| -------------------------------- | ------- | ----- |
| Path traversal in attachments    | ✅ safe | `SanitizeName` already blocks `.` / `..` / separators (`S-8`) |
| Passwords / API keys leakage     | ✅ safe | None handled in this module |
| Injection into ConfigService JSON | ✅ safe | JSON serializer, no raw text splicing |
| Feature-flag config on disk      | ✅ safe | Written by DPAPI-user only via `ConfigService.Save`; no user-typed strings interpolated into paths |
| Cross-thread mutation            | ✅ safe | `ProformaFieldVisibility.Refresh()` marshals onto `Application.Current.Dispatcher` |
| Nullable defensive               | ✅ safe | `ConfigService.Load` now hydrates `ProformaInvoiceFields ??= new()` — old configs without the field can't NRE |

## Cross-cutting recommendations for a future ticket

1. **Section-based grouping in the wizard.** Once a critical mass of
   fields is hidden, offer collapsible section headers ("Identity",
   "Contact", "Bank") so the operator sees the shape at a glance.
2. **User-role permissions on the toggles.** A junior operator
   shouldn't be able to hide the TRN field for the whole company.
   Wire the setters behind `AuditService.CurrentUser?.Role == "Admin"`
   as a follow-up.
3. **Per-tenant feature-flag file.** Config is per-Windows-user;
   a shop with multiple operators sharing a machine will see one
   layout. A future release can move the flags into the SQLite
   `Settings` table keyed by user.
4. **Toggle history.** The audit log already records each toggle
   change (`AuditService.Log(AuditSections.Settings, "Proforma
   field visibility changed", …)`). Surface it in Diagnostics ▶
   Activity so an admin can see who hid what.

## Files touched

| File                                                                       | Change |
| -------------------------------------------------------------------------- | ------ |
| `Services/AI/AIConfig/AppConfig.cs`                                        | New `ProformaInvoiceFeatureFlags` class + property |
| `Services/AI/AIConfig/ConfigService.cs`                                    | `??=` hydration for old configs |
| `Services/Features/ProformaFieldVisibility.cs`                             | NEW — bindable singleton |
| `ViewModels/Settings/ProformaInvoiceFeaturesViewModel.cs`                  | NEW — toggle VM |
| `ViewModels/Settings/SettingsViewModel.cs`                                 | Adds `IsFeaturesPage` + `ProformaFeatures` |
| `Views/Settings/Sections/SettingsFeaturesSection.xaml{,.cs}`               | NEW — Features page |
| `Views/Settings/SettingsView.xaml`                                         | Nav rail button + section host |
| `Views/ProformaInvoice/Step1CompanySetup/CompanySetupView.xaml`            | Visibility bindings |
| `Views/ProformaInvoice/Step2ClientProfile/ClientProfileView.xaml`         | Visibility bindings |
| `Views/ProformaInvoice/Step3ProjectSpecs/ProjectSpecsView.xaml`            | Visibility bindings |
