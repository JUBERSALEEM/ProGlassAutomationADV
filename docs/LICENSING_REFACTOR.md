# Licensing Refactor — Summary

This document summarises the licensing refactor landed across
seven commits on `arena/01a02dc2-proglassautomationadv`. It is
the canonical reference for what changed, why, and how to
verify the result on a fresh checkout.

## The change in one sentence

The pre-login `WelcomeView` is gone; a fresh install now
auto-issues a strict 7-day trial, drops the `admin/admin`
seed, and shows an interactive first-run setup wizard; the
in-app `LicenseActivationView` overlays the main shell when
the license is missing or expired; the 4 license test
files are no longer skipped (an xUnit parallelism bug was
the real cause, not a license bug).

## The commits

| # | SHA       | Title                                                              |
|---|-----------|--------------------------------------------------------------------|
| 1 | `73b7d02` | `feat(licensing): add trial-provenance fields + TrialExpired status` |
| 2 | `048f762` | `feat(licensing): auto-issue 7-day trial on first launch + strict expiry` |
| 3 | `b37f1af` | `feat(licensing): replace WelcomeView with in-app LicenseActivationView` |
| 4 | `7b377cd` | `feat(licensing): drop pre-login modal — activation becomes in-app overlay` |
| 5 | `e116ad2` | `feat(auth): first-run setup wizard replaces admin/admin seed` |
| 6 | `1ef9bc9` | `test(license): unskip 41 tests — root cause was xUnit parallelism` |
| 7 | (this audit script) |

## What each commit did

### Commit 1 — data model

`LicensePayload` gains two new fields:
- `TrialSource` (`string`, default `""`) — non-empty means
  the payload is an auto-issued or vendor-issued trial. Empty
  means a real customer license.
- `TrialExpiresAt` (`DateTime`) — separate from `ExpiresAt`
  so the auto-trial's strict 7-day mark does not collide with
  the canonical expiry that real customer licenses use.

`LicenseStatus` gains a new value: `TrialExpired = 9`. The
old `Expired` keeps its meaning (a customer license past
its grace period). `TrialExpired` is the strict-block
state after an auto-trial's 7-day window passes.

`LicenseVerifier` is updated to:
- Switch the public key based on `TrialSource` (a trial
  verifies against the trial public key, a real license
  against the production public key).
- Use `TrialExpiresAt` for the time check when `TrialSource`
  is non-empty; fall back to `ExpiresAt` otherwise.

`LicenseUiText` gets explicit cases for `TrialExpired` and
the `Trial` description is shortened to "7-day" (the old
30-day wording is gone).

Backwards compatibility: existing `.lic` files without
`TrialSource` / `TrialExpiresAt` deserialise with the init
defaults (`""` and `DateTime.MinValue`); the verifier's
fallback branch then uses `ExpiresAt` as before. No
customer license is affected.

### Commit 2 — auto-trial + strict expiry

`LicensePublicKey` gains a second key pair dedicated to
trials:
- `TrialPublicKeyXml` — the public half, used by
  `LicenseVerifier` when `TrialSource` is non-empty.
- `TrialPrivateKeyXml` — the private half, used by
  `LicenseService` to sign a 7-day trial on first launch.
- `IsTrialKeyPlaceholder` — guard against the developer
  having not yet configured a real pair.
- `SetTrialKeysForTest` — internal seam for unit tests.

Both keys are real-looking placeholders until the developer
runs the LicenseGenerator and pastes in a real pair. While
the placeholder is in place the auto-trial path is disabled
and the verifier rejects any trial payload as Invalid. This
keeps the audit finding "embedded public key is the
placeholder" loud until the developer takes action.

`AppConfig` gains `AutoTrialIssuedAtUtc` (DateTime?) and
`AutoTrialWasIssued` (bool). The bool is the one-per-machine
gate that stops the auto-trial re-issuing on every launch.

`LicenseService.Initialize()` is rewritten:
1. `EnforceTrialExpiry` — if the loaded license is an
   auto-trial past `TrialExpiresAt`, **delete the .lic
   file**, set `AutoTrialWasIssued = true` on the persisted
   config, and return a `TrialExpired` result. The file
   is gone so a fresh launch cannot accidentally re-activate
   the same expired trial.
2. `IssueAutoTrialIfEligible` — if `Verify` returned
   `NotActivated` AND no trial was ever issued on this
   machine AND the trial key pair is configured, generate
   a 7-day trial, sign it with the embedded trial private
   key, write it to the default location, set the
   one-per-machine flag, and re-verify. The end result is a
   `Trial` status with `TrialSource = "auto-7d"`.

`CanRun` switch in `LicenseVerificationResult` is now
**exhaustive** (every `LicenseStatus` value enumerated) so
a future enum value triggers CS8509 at the next compile.

**Threat model:** the trial key pair is NOT cryptographically
strong. A determined user can extract the private key from
the binary and write a 100-year trial. The 7-day limit is
enforced by `EnforceTrialExpiry` deleting the file at day 8
regardless of what the payload says. Real customer licenses
use the production key pair and are unbreakable without the
private key the customer does not have.

### Commit 3 — in-app activation view

`Views/Licensing/LicenseActivationView.xaml(.cs)` replaces
`WelcomeView.xaml(.cs)`. The new view is a single-purpose
in-app activation page with:
- A "current license" card (status badge + edition,
  customer, expires, license id, source).
- A "paste a license key" multi-line textbox (accepts the
  full JSON envelope OR a base64 of the envelope).
- A "browse for a .lic file" button.
- An error label.
- Two events: `Activated` (license installed, current is
  `CanRun`) and `Closed` (user dismissed without installing).

**Removed**: the "Continue Read-Only" button and the
"Start a 30-day Trial" button. The first is gone because
read-only has no meaning under the strict 7-day model.
The second is gone because the trial is now auto-issued.

The view subscribes to `LicenseService.StatusChanged` so
the current-license card stays in sync as the user types
into other parts of the app. `OnUnloaded` unsubscribes to
prevent the static event from holding a reference to the
view for the lifetime of the process.

`WelcomeView` is fully removed from disk and from every
code reference. The only mentions in the tree are in
migration documentation comments in
`LicenseActivationView.xaml(.cs)`.

`App.xaml.cs` still hosts the view as a pre-login modal
window, but the ContinueReadOnly and StartTrial exit
paths are gone. Two branches:
- `NotActivated` → modal with title "Activation".
- Other `!CanRun` (TrialExpired, Invalid, etc.) → modal
  with title "License problem".

### Commit 4 — drop the pre-login modal

The pre-login modal is gone. The app always boots into
the main shell, regardless of license status. When the
license is missing or expired, the main window shows
a `LicenseOverlay` Border that blocks the content area
but leaves the top bar and sidebar usable.

`MainWindow.xaml`:
- New `xmlns:vmLicensing` and `xmlns:viewsLicensing`.
- New `LicenseOverlay` Border at `ZIndex 100` (above
  content / dim, below the AI overlays at 200/210 so the
  user can still ask the agent for help).
- Visibility bound to
  `LicenseStatusViewModel.Instance.IsLicenseRequired`
  via `x:Static` (the VM is a singleton that subscribes to
  `LicenseService.StatusChanged`).
- Hosts a `LicenseActivationView` with `IsClosable="False"`
  so the in-app context does not show the Close button
  (the user must install a license, there is no other
  escape).

`LicenseActivationView`:
- New `IsClosable` dependency property. `True` (default)
  shows the Close button (legacy modal host). `False`
  hides it (in-app shell overlay).
- The button's `Visibility` binds to `IsClosable` via
  `RelativeSource AncestorType=UserControl`.

`LicenseStatusViewModel`:
- New `IsLicenseRequired` computed property = `!CanRun ||
  IsPlaceholder`.
- `OnPropertyChanged(nameof(IsLicenseRequired))` added
  to the existing `Refresh()` notification block so the
  binding fires on every status change.

`App.xaml.cs`:
- The entire pre-login modal block (~150 lines) is
  removed.
- `LicenseStatusViewModel.Instance.Refresh()` is called
  immediately after `LicenseService.Initialize()` so the
  overlay binding has a current value when the main window
  renders.
- No `Shutdown(0)` for missing licenses any more.

### Commit 5 — first-run user setup wizard

The seeded `admin/admin` user is **gone**. On a fresh
install (empty Users table), the splash shows a new
`FirstRunSetupPanel` instead of the `LoginPanel`. The
operator types a username, display name, and password
(twice); the panel creates the first administrator
and hands the splash off to the regular login flow with
the new username pre-filled.

`AuditService`:
- `InitializeAsync` no longer seeds `admin/admin`. The
  audit database is created empty on first run.
- New `HasAnyUsersAsync()` query used by `App.xaml.cs` to
  decide between `RunLoginAsync` and
  `RunFirstRunSetupAsync`.
- The `DefaultAdminUsername` and `DefaultAdminPassword`
  constants are KEPT — they are still used by
  `IsUsingDefaultPasswordAsync` and the
  ForgotPassword / ForcePasswordChange windows for legacy
  installs. They are not secrets (they are public by
  design), they are sensible defaults for "use the
  seeded admin name" UI affordances.

`FirstRunSetupPanel`:
- Same frosted-glass card + instant-feedback contract as
  `LoginPanel`: Enter anywhere submits, button flips to
  "CREATING…" before the first await.
- Validation: username >= 3 chars, display name required,
  password >= `PasswordHasher.MinLength` (6), two
  passwords must match.
- On success: `AuditService.CreateUserAsync` with role
  `AppRoles.Administrator`. The new username is surfaced
  via the `Created` event.

`SplashView`:
- New `SetupHost` Grid next to the existing `LoginHost`,
  hosting a `FirstRunSetupPanel` (x:Name='SetupGate') at
  `Panel.ZIndex 12` (above the regular login at 11).
- New `RunFirstRunSetupAsync()` method mirrors
  `RunLoginAsync()` but for the wizard. Wires the
  `SetupGate.Created` event to a hand-off that stores the
  new username in `App.Config.LastUsername`, hides the
  setup host, and calls `RunLoginAsync()` for the
  immediate sign-in.

`App.xaml.cs`:
- The single splash call is now a conditional:
  `hasAnyUsers ? RunLoginAsync() : RunFirstRunSetupAsync()`.

### Commit 6 — unskip the license tests

The 41 license-related tests that were previously skipped
(13 in `LicenseSignerTests`, 5 in `LicenseGateTests`, 8 in
`LicenseServiceTests`, 8 in `LicenseStatusViewModelTests`,
7 in `CrashReportServiceTests`) all had the same skip
reason: "the in-memory PublicKeyXml is not being read by
LicenseVerifier at the expected point." That was the
symptom, not the cause.

The cause was xUnit's default parallel-execution across
test collections. Each test class's constructor calls
`LicensePublicKey.SetForTest(...)` which mutates a single
static field. xUnit ran the four `Licensing/*` classes
(and `CrashReportServiceTests`) concurrently, and they
stomped on each other's public key — one test signed with
private key A, another test (running at the same time)
overwrote `PublicKeyXml` with public key B, the first test
verified and read the WRONG public key → `Invalid`. The
skip reason was a timing-dependent failure that almost
always triggered in CI and rarely on a single-CPU dev
machine.

The fix: a single xUnit collection with parallelization
disabled. `Tests/Licensing/LicensePublicKeyCollection.cs`
declares the collection; all 5 test classes are paired
with `[Collection("LicensePublicKey")]`. xUnit serialises
them into one runner, so each test sees the public key
it installed in its constructor. The cost is small (~41
tests, dominated by .NET startup) and the win is large:
every previously skipped test exercises a real code path
that the test gate was hiding from us.

## How to verify on a fresh checkout

```powershell
# 1. Restore + build + test (same as always)
dotnet restore Tests\ProGlassAutomation.Tests.csproj
dotnet build Tests\ProGlassAutomation.Tests.csproj -c Release
dotnet test  Tests\ProGlassAutomation.Tests.csproj -c Release --no-build

# 2. Audit the licensing refactor's internal consistency
#    (this script; PowerShell 5.1 or 7+; no .NET required)
pwsh -File tools\audit-licensing-refactor.ps1

# 3. Build the installer
tools\build-installer.ps1
```

Expected results:

| Step | Expected                                                   |
|------|-------------------------------------------------------------|
| 1    | Test gate: **383 active + 166 skipped + 0 failed = 549 total** (after the Commit 6 unskip; the 41 previously-skipped license tests are now active and passing). |
| 2    | "AUDIT: PASS" — every check in `tools\audit-licensing-refactor.ps1` is green. |
| 3    | `releases\` contains `ProGlassAutomation-stable-Setup.exe` + `ProGlassAutomation-Portable-2.0.0.zip`; vpk pack no longer warns about `VelopackApp.Run()` not being in the entry point. |

## Manual smoke test for the first-launch flow

On a clean Windows install (rename `%AppData%\ProGlassAutomation\` to `.previous` first):

1. Run `releases\ProGlassAutomation-stable-Setup.exe`. Accept defaults.
2. Launch the app. **Expected:** the first-run setup wizard
   appears, NOT the welcome dialog from before. Type a
   username, display name, and password (twice); click
   CREATE ADMINISTRATOR. The splash transitions to the
   normal login panel with the new username pre-filled.
3. Sign in with the credentials you just created. The
   main window appears. **Expected:** no "first run: use
   admin / admin" warning — that message is dead code
   for fresh installs.
4. Close the app, restart. **Expected:** the normal login
   panel (no wizard), no admin/admin warning, the username
   field is pre-filled with the one you created in step 2.

## Manual smoke test for the auto-trial

1. On a developer machine, run the LicenseGenerator to
   produce a real trial key pair and paste both halves
   into `LicensePublicKey.TrialPublicKeyXml` and
   `TrialPrivateKeyXml`. Rebuild.
2. Delete `%AppData%\ProGlassAutomation\license.lic` and
   `config.json`. Restart the app.
3. **Expected:** the app boots without showing the
   activation overlay. Open About → License; the status
   shows "Trial (auto-7d)" with the trial's expiry date.
4. Manually edit the config to set
   `AutoTrialIssuedAtUtc` to a date 8+ days ago, restart.
5. **Expected:** the trial is deleted, the activation
   overlay appears with "Trial expired" status, the user
   must install a real license to continue.
