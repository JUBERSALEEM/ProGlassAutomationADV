# Licensing & Login UX — August 2026 rework

This note records the intentional behaviour changes to the app's
splash → login → licensing flow made in August 2026, so a future
reviewer does not have to reverse-engineer the reasoning from the
diff.

## Motivation

The pre-change flow had three UX problems that a professional
industrial app should not ship with:

1. **License activation was the default main screen.** On any fresh
   launch (and on any state where `CanRun` was false — including
   the auto-trial being unavailable on a placeholder key) the app
   opened straight into the licensing UI. Nobody wants their first
   sight of an industrial suite to be a licence form.
2. **The auto-trial was strict and destructive.** It ran for 7 days
   and deleted the `.lic` file on expiry, forcing the user into a
   `CanRun=false` state that blocked every module.
3. **Login had no "remember me" contract.** Every launch showed the
   login form. There was no way to say "trust this device until the
   next update".

## New behaviour

### Splash

- Unchanged animation.
- After the terms gate, the splash decides between three paths:
  1. **First-run** (no users in DB) → `FirstRunSetupPanel` creates
     the first administrator and **auto-arms the skip-login pill**
     so the user is not asked to re-type their brand-new password
     on the very next launch.
  2. **Returning user with skip-login armed** → `TryAutoLoginAsync`
     verifies the DPAPI-encrypted password from `SecretVault` and,
     on success, signs in without ever mounting the login form.
  3. **Returning user without skip-login (or after an update)** →
     the classic `LoginPanel` is shown.

### Login

- New **iPhone-style pill** labelled "Skip login until update"
  under the Remember-username row. Style: `PiToggleSwitch`.
- When the pill is ON at sign-in:
  - The password is stored DPAPI-encrypted in `SecretVault` under
    `LoginPanel.SkipLoginSecretKey` ("auth.skiploginpassword").
  - `AppConfig.SkipLoginUntilUpdate = true` and
    `AppConfig.SkipLoginBoundVersion = AppInfo.Version` are
    persisted.
- On the next launch, `SplashView.TryAutoLoginAsync` verifies:
  - `SkipLoginUntilUpdate` is still true, AND
  - `SkipLoginBoundVersion` still equals the running `AppInfo.Version`.
    An OTA update changes the version → the pill deliberately
    stops working and the user re-verifies on the new binary.
- If the saved password fails verification (e.g. the operator
  changed it since), the blob and the flags are cleared and the
  regular login form is shown.

### Licensing

- **Auto-trial is now 30 days**, not 7. Constant:
  `LicenseService.AutoTrialDays`.
- **All modules are unlocked during the trial** — the trial
  payload's `Features = new[] { "*" }`. Paid Business / Factory /
  Enterprise licences still narrow this by SKU.
- **Trial expiry no longer deletes the `.lic` file.** Customer data
  is never touched by the licensing subsystem. Instead, the trial
  simply transitions to `LicenseStatus.TrialExpired` which now maps
  to `CanRun=true, ReadOnly=true`. The app remains navigable; a
  yellow banner at the top of the shell says
  _"Read-only mode — your data is safe"_ with an
  **Activate license** button that jumps straight to
  Settings → License.
- **The full-screen `LicenseActivationView` overlay is now hidden
  for Trial and TrialExpired.** It is shown only for hard-broken
  states (invalid signature, machine-mismatch, exceeded
  activations, expired paid licence, or a fresh install where the
  auto-trial could not be issued because the trial key pair is the
  placeholder).
- **Settings → License** is the new home for licence management.
  The nav rail has a new "License" entry (glyph  `E192`) that
  hosts `SettingsLicenseSection`, which wraps the shared
  `LicenseActivationView` plus a compact status card.

## File map

| Layer            | File                                                                |
|------------------|---------------------------------------------------------------------|
| Config           | `Services/AI/AIConfig/AppConfig.cs` (SkipLoginUntilUpdate, SkipLoginBoundVersion) |
| Trial length     | `Services/Licensing/LicenseService.cs` (`AutoTrialDays = 30`)       |
| Trial features   | `Services/Licensing/LicenseService.cs` (`Features = ["*"]`)         |
| Trial expiry     | `Services/Licensing/LicenseService.cs` (`EnforceTrialExpiry` non-destructive) |
| CanRun / ReadOnly| `Services/Licensing/LicenseVerifier.cs`                             |
| Overlay gating   | `ViewModels/Licensing/LicenseStatusViewModel.cs` (`IsLicenseRequired`, `IsReadOnly`, `IsTrialExpiredReadOnly`) |
| Read-only banner | `MainWindow.xaml` (`ReadOnlyBanner` element)                        |
| Settings page    | `Views/Settings/Sections/SettingsLicenseSection.xaml{,.cs}`         |
| Settings nav     | `Views/Settings/SettingsView.xaml` + `IsLicensePage` in `SettingsViewModel` |
| Login pill       | `Views/Login/LoginPanel.xaml{,.cs}` (`SkipLoginPill`)               |
| Auto-login       | `Views/Splash/SplashView.xaml.cs` (`TryAutoLoginAsync`)             |
| First-run arm    | `Views/Login/FirstRunSetupPanel.xaml.cs` (`TryArmAutoLogin`)        |

## Non-goals / follow-ups

- `LicenseService.IsReadOnlyMode` is exposed but not yet consumed
  by write commands. A follow-up should gate `SaveCommand` /
  `PrintCommand` / `ExportCommand` on the ReadOnly flag so the
  banner's promise ("no new edits until you activate") is
  enforced at the command layer, not just messaging.
- The disk marker `LicenseService.AutoTrialSourceMarker` is kept
  as the string `"auto-7d"` for backwards compatibility with
  installs that already carry a trial file. Do NOT rename it
  unless you also write a migration path.
- Documentation in `docs/LICENSING_REFACTOR.md` still references
  the older 7-day flow. Leave it as historical context or update
  it in a dedicated docs pass — it is not part of runtime
  behaviour.
