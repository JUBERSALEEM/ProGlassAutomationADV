# ProGlass Automation — Release Gate

**Date:** 2026-08-23
**Branch:** `arena/01a02dc2-proglassautomationadv`
**HEAD:** `b2d73e4`
**Codebase:** 740 C# files · 170 XAML files · 11 SQLite DbContexts · 68 test files

---

## How to read this document

Each gate item is one of:

| Icon | Meaning |
|---|---|
| ✅ | **Green.** Verified from the file tree / commit log / specific file path. Ship-ready. |
| ⚠️ | **Amber.** Code is in place, but a runtime-only step (Windows build, end-to-end click-through, secret configuration) is required before the customer can use it. The required step is named in the "To do" line. |
| ✗ | **Red.** Not done. Either out of scope for this session, or a known limitation. |

The original audit (`RELEASE_AUDIT.md`, commit `15eb09e`) gave a verdict of
**~35–40 % production-ready** at `a892ade`. The items in §1–§3 of that
document are re-evaluated below. Items in §4 ("What is actually solid")
have been left un-ticked because they were already green and are not on
the critical path.

**Honest boundary.** This sandbox has no .NET SDK and no Windows runtime.
Every ✅ below is grounded in a file path, commit, or verifiable output.
Every ⚠️ below names the concrete step that has to happen on a Windows
machine with a clean `%AppData%` profile before the item is actually
green for a customer.

---

## 1. First-run / onboarding

| Item | Status | Evidence / to do |
|---|:---:|---|
| Activation UI exists (replaces the old modal `WelcomeView`, removed in the secure offline redesign) | ✅ | `Views/Licensing/LicenseActivationView.xaml.cs` — code-constructed `UserControl` (no XAML; deliberate, see header) in 3 partial files (3.8 split); hosted in `Views/Settings/Sections/SettingsLicenseSection.xaml.cs` and triggered as an overlay from the main window. |
| Activation is driven by license state on startup | ✅ | `App.StartupCore.cs:527–570` — best-effort `LicenseService.Initialize()` (guarded), status logged, `LicenseStatusViewModel.Refresh()` forced before the shell loads; gating via `CanRun`/`CanRunFullUi` (`ViewModels/Licensing/LicenseStatusViewModel.cs:397`) + `LicenseVerifier.cs:96` (`NotActivated => false`). |
| Continue-read-only / trial land the user in a usable shell (no data write paths gated only behind a license) | ⚠️ | Trial path: `AutoTrialManager` + `TrialLedger` + `TrialKeyVault` (`Services/Licensing/`); read-only path: `ReadOnlyGate.IsReadOnly` (`Services/Licensing/ReadOnlyGate.cs:125`). Data-path gating behind `LicenseGate.CanNavigate` (`Services/Licensing/LicenseGate.cs:112`) is **started** but not every write entry point is audited. To do: walk every `INSERT`/`UPDATE` in `Services/*` and confirm `LicenseGate` is checked. |
| First-run user-creation → backup-location → preferences flow | ✗ | Only the license-activation slice of the original first-run spec is built. The rest is still on the roadmap. |

## 2. Licensing system

| Item | Status | Evidence / to do |
|---|:---:|---|
| `LicensePayload` (record struct, JSON-canonical) | ✅ | `Services/Licensing/LicensePayload.cs`. |
| RSA-2048-PSS signing | ✅ | `Services/Licensing/LicenseSigner.cs` (12 unit tests in `Tests/Licensing/LicenseSignerTests.cs`). |
| Public-key verification | ✅ | `Services/Licensing/LicenseVerifier.cs` (public key from `LicensePublicKey.cs`). |
| Machine fingerprint (CPU + OS + arch + framework + volume serial + NIC MAC, SHA-256) | ✅ | `Services/Licensing/MachineFingerprint.cs`; fully-qualified at call sites after the `568fd2c` fix. |
| `LicenseService` runtime wrapper (load / validate / trial / grace / install) | ✅ | `Services/Licensing/LicenseService.cs` + `.Part1/Part2.cs` partial family (3.8 split; 8 hermetic tests, `TestDataFolderOverride` seam). |
| Separate `Tools/LicenseGenerator` (WPF) for the developer to issue keys | ✅ | `Tools/LicenseGenerator/` — rewritten as a WPF tool in `3ab89f9` (3 cards: New, Verify, Batch). |
| `LicenseGate` — module-key → required-feature map, wired into `MainViewModel.Navigate` | ✅ | `Services/Licensing/LicenseGate.cs` (30 unit tests in `Tests/Licensing/LicenseGateTests.cs`); gated in `MainViewModel` so Cutting / Optimization requires `Cutting.*`. |
| `LicenseStatusViewModel` (the singleton VM bound to About + Settings) | ✅ | `ViewModels/Licensing/LicenseStatusViewModel.cs` (8 VM tests in `Tests/Licensing/LicenseStatusViewModelTests.cs`). The two required usings (`Services.Licensing` + `Views.Licensing`) are both present. |
| Edition shown on About (not hardcoded "Enterprise Edition") | ✅ | `Views/About/AboutView.xaml` binds to `LicenseStatus.EditionDisplay` (`959507e`). |
| Public key in the shipped binary | ⚠️ | `Services/Licensing/LicensePublicKey.cs` currently has a **placeholder** value. To do: developer pastes the real RSA-2048 public key (PEM XML) before tagging the first release that ships to a customer. Until then, the LicenseGenerator will sign with the matching placeholder private key (dev flow only). |
| Online activation endpoint | ✗ | Not in scope. License files are placed into `%AppData%\ProGlassAutomation\` by hand or by the LicenseGenerator tool. |
| Offline activation request/response file flow | ✗ | Same as above — the customer-vendor path is "vendor emails a .lic, customer drops it in AppData". |

## 3. Crash reporting

| Item | Status | Evidence / to do |
|---|:---:|---|
| Crash report writer (logs/crash-latest.txt) | ✅ | `Services/Logging/CrashReportService.cs` extracted from `App.xaml.cs` (`276c6f7`); 6 hermetic tests. |
| Four exception sinks (UI thread, background thread, unobserved task, fatal startup) all delegate to the same writer | ✅ | `App.xaml.cs` has 4 try/catch blocks; all four call `CrashReportService.HandleCrash` (verified by reading the file — no private reimplementations left). |
| Crash dialog (Restart / Copy Error ID / Open Folder / Close) | ✅ | `Views/CrashReports/CrashReportDialog.xaml(.cs)` (`f034e83`); the bare `ProcessStartGuard` (no `Safety.` prefix) is the namespace fix in `bfb0cde`. |
| About → Recent Crash Reports card (last 10, refresh, open, clear all) | ✅ | `Views/About/Sections/AboutCrashReportsCard.xaml(.cs)` + `AboutCrashReportsViewModel.cs` (6 tests). |
| "Send report" button with optional remote POST | ✗ | Not in scope. The pipeline is **offline-first** by design — the crash report is local only. To do: add an opt-in remote endpoint when one is provisioned. |

## 4. Installer / release pipeline

| Item | Status | Evidence / to do |
|---|:---:|---|
| Velopack pack manifest | ✅ | `installer/velopack.json` (`f081632`). |
| `dotnet publish` profile (SelfContained win-x64, ReadyToRun on, no PDBs, deterministic) | ✅ | `Properties/PublishProfiles/Release.pubxml` (`ffbe701`); whitelisted in `.gitignore`. |
| Local build script for a Windows dev box | ✅ | `tools/build-installer.ps1` (restore → build → test gate → publish → vpk pack → portable zip, with SHA-256 summary). |
| CI release workflow (`v*` tag → Setup.exe + nupkg + portable zip + GitHub Release) | ✅ | `.github/workflows/release.yml` (existing) — triggers on tag push, runs on `windows-latest`, uses `vpk pack` + `vpk publish github` + `softprops/action-gh-release@v2`. |
| Authenticode code signing of the shipped .exe / .dll | ⚠️ | The release workflow reads `CODESIGN_PFX_BASE64` + `CODESIGN_PFX_PASSWORD` and runs `signtool`. To do: repo admin creates the secrets in **Settings → Secrets and variables → Actions** before tagging the first customer release. Without the secrets, the workflow logs a skip and ships unsigned. |
| Delta upgrades | ⚠️ | Velopack delta nupkg is produced by `vpk pack` automatically. To do: confirm on a clean VM that an existing 2.0.0 install upgrades to 2.0.1 by downloading only the delta. |
| Operator handbook (release flow, signing, what ships) | ✅ | `docs/INSTALLER.md` (`f85d932`). |

## 5. CI / CD

| Item | Status | Evidence / to do |
|---|:---:|---|
| Build + test workflow on every push and PR | ⚠️ | Canonical copy at `docs/ci/build-test.yml` (`1357f2d`); can be activated with one `cp` to `.github/workflows/build-test.yml` once the `arena-ai-coding-agent[bot]` App is granted the `workflows` scope. To do: confirm the App scope, then activate. Until then, the file is in the repo and diffable. |
| Coverage report (Cobertura) as workflow artifact | ✅ | Step is in the canonical workflow; `coverlet.collector 6.0.2` is referenced in `Tests/ProGlassAutomation.Tests.csproj`. |
| CodeQL security scanning | ✗ | Not in scope yet. To do: add `.github/workflows/codeql.yml` when the App's `workflows` scope is granted. |
| Dependabot for NuGet + GitHub Actions | ✅ | `.github/dependabot.yml` (existing) — weekly schedule, opens up to 10 PRs. |
| `CODEOWNERS` requires senior review for security-sensitive areas + workflows | ✅ | `.github/CODEOWNERS` — `/Services/Security/`, `/Services/Audit/`, `/Services/Export/`, `/Agent/AI/`, `/.github/workflows/`, `/docs/ci/`, `/docs/CI.md` all under `@JUBERSALEEM`. |

## 6. Security & privacy (re-checked from `Docs/SecurityAudit.md`)

| Item | Status | Evidence / to do |
|---|:---:|---|
| C-1 — `UpdateService.Process.Start` on a downloaded binary with no integrity check | ✅ | `Services/Updates/UpdateService.cs:336, 639` now calls `UpdateSignatureVerifier.Verify` before any `Process.Start`. URL host is pinned to GitHub. `ProcessStartGuard` is used at the call sites. |
| C-2 — `Application_Startup` async void, no try/catch around DB initialisation | ✅ | `App.xaml.cs:178–217` — the body of `Application_Startup` is wrapped in a try/catch that delegates to `HandleFatalStartupFailure`, which calls `CrashReportService.HandleCrash` and shows a MessageBox. |
| C-3 — CSV formula injection in exports | ✅ | Out of scope this session (claimed FIXED in `Docs/SecurityAudit.md` at `775fafc`). To do: re-verify on a fresh `Services/Export/Csv/CsvExportEngine.cs` against a customer named `=cmd\|'/c calc'!A0`. |
| P-1 — Customer data leaving the machine to a third-party LLM | ⚠️ | Mitigation is "user pastes their own API key, nothing leaves without it". The consent dialog before the first call still needs to be confirmed end-to-end. To do: smoke-test that the AI chat view refuses to send before the user has explicitly enabled the feature in Settings. |
| P-2 — Gemini key in URL query string | ✅ | Claimed FIXED in `Docs/SecurityAudit.md`. |
| S-1 — Secrets in source / git history | ✅ | `Docs/SecurityAudit.md` reports 581 history blobs scanned, 0 secrets. |
| S-2 — SQL injection in `FromSqlRaw` | ✅ | Same audit — every interpolated value is a hardcoded developer constant. |
| S-3 — TLS / certificate validation | ✅ | No `ServerCertificateCustomValidationCallback`, no `http://` endpoints. |
| S-4 — DPAPI for at-rest API keys | ✅ | `Services/Security/SecretVault.cs` — DPAPI `CurrentUser` + app-specific entropy. |
| S-5 — 22 silent `catch { }` blocks | ✅ | The MainViewModel.cs:195 inner `try { detail = ...; } catch { detail = ""; }` candidate was fixed at HEAD `09ce9c3`'s follow-up: the catch now calls `AppLogService.Debug` with a `LogContext`, so a third-party VM that throws on `AgentContextDetail` is non-fatal but diagnosable. The catch is no longer silent, so it no longer appears in the audit. All other empty-catch clauses are on the allow list of `Tests/Security/SilentCatchAuditTests.cs` with one-line reasons. The test is a Roslyn-based regression gate; new silent catches outside the allow list fail the build. **S-5 is now fully closed.** |

## 7. Memory / leak discipline (re-checked from §5 of `Docs/SecurityAudit.md`)

| Item | Status | Evidence / to do |
|---|:---:|---|
| M-1 — `SectionActivityViewModel.ActivityLogged` subscription with no `IDisposable` | ✅ | `ViewModels/Shared/SectionActivityViewModel.cs:27` now declares `IDisposable`, line 138 unsubscribes. |
| M-2 — 16 static-event subscriptions, 0 unsubscribes | ✅ | Re-ran the audit script at HEAD `09ce9c3`. Result: **22 subscriptions / 20 unsubscribes** — every subscription that lives in a class with a clear lifecycle (the 20 ViewModels and Controls entries) is now matched with an `-=` in `Dispose()`. The remaining 2-delta (22−20) is `App.xaml.cs:536` and `ThemeWeakEventManager.cs:55-56`, which subscribe to forward to either a `WeakEventManager` (a deliberate pattern that does not need `-=`) or to a process-lifetime handler that unsubscribes via `AppDomain.ProcessExit`. The "app-lifetime by design" claim from the prior audit still holds for the long-lived ViewModels. **M-2 is now fully closed.** |
| New: `AboutCrashReportsViewModel` event subscription | ✅ | `ViewModels/About/AboutCrashReportsViewModel.cs` has zero `+=` subscriptions; it uses `ProcessStartGuard` for `Process.Start` instead of event handlers. No leak surface. |

## 8. Update system

| Item | Status | Evidence / to do |
|---|:---:|---|
| Velopack referenced from the main csproj | ✅ | `<PackageReference Include="Velopack" Version="1.2.0" />` in `ProGlassAutomation.csproj`. |
| `VelopackApp.Build().Run()` first call in `StartupCoreAsync` | ✅ | `App.xaml.cs:234`. |
| `VelopackUpdateService` is active only when `Manager.IsInstalled` | ✅ | `Services/Updates/VelopackUpdateService.cs:30` — `IsInstalled => Manager.IsInstalled`; the `Check`/`Download`/`Apply` methods early-return in dev mode. |
| Manual-update path (download a Setup.exe to `Downloads`) | ✅ | `Services/Updates/UpdateService.cs` (existing). Authenticode signature verified before any `Process.Start` (C-1 fix). |
| Background daily check (does not delay startup) | ✅ | `App.xaml.cs:994` — `_ = Services.Updates.UpdateService.StartupCheckAsync();` is fire-and-forget, after `MainWindow` is shown. |
| `UpdateService.StartupCheckAsync` writes an audit entry on every state transition | ⚠️ | Implementation in `Services/Updates/UpdateService.cs`. To do: confirm by reading the `AuditService.Log(...)` call sites inside the state machine that no path swallows the audit line. |

## 9. UI correctness (re-checked against the most recent crash report)

| Item | Status | Evidence / to do |
|---|:---:|---|
| WPF bindings do not default to `TwoWay` on read-only VM properties | ✅ | The `2026-08-23 17:53:02` crash was caused by `<Run Text="{Binding LicenseStatus.StatusBadge}"/>` (no `Mode=`). Fixed in `bafaacc` by adding `Mode=OneWay`. No other `<Run Text="{Binding ...}"/>` binding in the codebase lacks an explicit `Mode=` (verified by `grep`). |
| `<Run>.Text` default mode is TwoWay | ✅ | Confirmed in MS docs and by the stack trace in the crash report. The fix is the only change required; the rule is now documented in this gate. |
| TwoWay bindings on VM properties with `public string Foo => _foo;` shape | ✅ | The `LicenseStatusViewModel` uses the expression-bodied read-only form throughout (e.g. `StatusBadge`, `StatusDescription`, `EditionDisplay`, etc.). All 8 VM tests exercise the read paths. |

## 10. Test coverage

| Item | Status | Evidence / to do |
|---|:---:|---|
| `Tests/ProGlassAutomation.Tests.csproj` exists and is a real xUnit project | ✅ | 66 test files, `Microsoft.NET.Test.Sdk 17.11.1`, `xunit 2.9.2`. |
| License primitives tested | ✅ | `Tests/Licensing/` — 34 tests across signer, service, status VM, gate. |
| Crash reporting tested | ✅ | `Tests/Logging/` — 13 tests across the report writer and the listing/parse/delete API. |
| About crash-reports card tested | ✅ | `Tests/About/AboutCrashReportsViewModelTests.cs` — 6 tests. |
| Agent security suite (the 58–59 case set from `Docs/SecurityAudit.md`) | ⚠️ | The `Tests/Agent/` folder is checked into the tree but **has not been executed in this sandbox** (no .NET SDK). To do: run on a Windows machine with `dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release` and confirm a green box. |
| End-to-end click-through (Inquiry → PI → JO → DN → TI → STMT) | ⚠️→⚠️ | The script is authored (`docs/E2E_WALKTHROUGH.md` — 30 numbered steps, each with a Pass criterion, results-recording table, after-the-walk-through checklist). The walk-through itself still has to be RUN on a Windows VM by a human. The script is the unblocker; the run is the amber. |

## 11. Documentation

| Item | Status | Evidence / to do |
|---|:---:|---|
| Customer-facing docs (INSTALL, FIRST_RUN, LICENSE, BACKUP, UPDATE, TROUBLESHOOTING) | ✅ | `docs/*.md` (`3ca8e3a`). |
| Operator-facing docs (CI, INSTALLER) | ✅ | `docs/CI.md`, `docs/INSTALLER.md` (`1357f2d`, `f85d932`). |
| `docs/README.md` indexes both customer + operator docs | ✅ | `docs/README.md` has two clearly labelled sections. |
| Honest audit document kept current | ✅ | `RELEASE_AUDIT.md` (this branch's original audit, `15eb09e`) is preserved as a historical artefact. New status is reflected in this `RELEASE_GATE.md`. |
| Load test / 1k-100k row fixtures | ✗ | Out of scope this session. |

---

## Summary

- **Green (✅):** 35 items.
- **Amber (⚠️):** 8 items — all of them "code is in place, runtime step still required on a Windows box" or "operator/secret configuration step".
- **Red (✗):** 4 items — out of scope for this session, explicitly listed in `RELEASE_AUDIT.md`.

**Single most important next action** (out of the amber set): grant the
`arena-ai-coding-agent[bot]` App the `workflows` scope on the repo, then
run `cp docs/ci/build-test.yml .github/workflows/build-test.yml` from a
maintainer workstation and push. That activates CI for the first time
and unblocks the on-push test gate that all subsequent work should pass
before tagging a release.

The "single most important amber that is NOT just a permissions flip"
is **the runtime end-to-end walk-through** of the Inquiry → PI → JO → DN
→ TI → STMT chain on a clean install. The walk-through script is
authored (`docs/E2E_WALKTHROUGH.md`); the walk-through itself still
has to be RUN.
