# ProGlassAutomation — Commercial Release Audit

**Date:** 2026-08-23
**Branch:** arena/01a02dc2-proglassautomationadv
**HEAD:** a892ade

## 1. Production-readiness verdict

**The application is approximately 35–40 % production-ready.**

A real customer **cannot** install, activate, and run their business on it
today. The ERP scaffolding is real and impressive, but the commercial
"shell" (installer, licensing, first-run, signed updates, crash
reporting) is mostly missing. Shipping it as-is would be a support
nightmare.

### 1.1 Feature matrix (verified from source — not guessed)

| Module / Capability              | Exists | Functional | Production-ready | Critical issues |
|----------------------------------|:------:|:----------:|:----------------:|-----------------|
| Dashboard                        |   ✓    |  partial   |        ✗         | Several KPIs are placeholders, some sources still return empty lists. |
| Master Data (companies/clients/projects/salesmen) | ✓ | yes | partial | Schema drift between EF model and live DBs; no CRUD for salesmen. |
| Sheet Store (inventory)          |   ✓    |  yes       |       partial    | No reorder / supplier workflows. |
| Inquiry                          |   ✓    |  yes       |       partial    | Attachment-EF-mirror drift (the `cf14864` fix chain is the most-recent work); edit/delete/Permissions added but not e2e tested. |
| Proforma Invoice                 |   ✓    |  yes       |       partial    | Snapshot JSON is `{}` default — known BUG-06 in code. |
| Job Order                        |   ✓    |  yes       |       partial    | Production tracking surface exists; not e2e walked. |
| Delivery Note                    |   ✓    |  yes       |       partial    | Master-DN logic; balance/prev/this math not verified. |
| Tax Invoice                      |   ✓    |  yes       |       partial    | Status transitions not all gated; aging reports not verified. |
| Statement                        |   ✓    |  yes       |       partial    | Reconciliation has known open TODO. |
| Reports (PI/JO/DN/TI/STMT)       |   ✓    |  yes       |       partial    | Excel/CSV/PDF wired but not stress-tested. |
| Settings                         |   ✓    |  yes       |       partial    | Database health card is the most recent work (commit 92d523d); not yet clicked-through. |
| Users / Roles / Permissions      |   ✓    |  yes       |       partial    | `OperationGuard` at service layer; coverage is good, but no UI for managing users/roles from the app. |
| AI Assistant (Gemini/Groq/Cohere) |  ✓   |  yes       |       partial    | Three providers wired; no rate-limit/backoff. |
| Logs & Audit                     |   ✓    |  yes       |       partial    | Excellent `AppLogService`; activity log UI exists; no log rotation config. |
| Backup / Restore                 |   ✓    |  yes       |       partial    | `BackupService`/`RestoreDrillService`; schedule UI is implicit. |
| Import / Export (Excel/CSV/PDF)  |   ✓    |  yes       |       partial    | ClosedXML + QuestPDF; PDF is good, Excel not stressed. |
| Search / filter / sort           |   ✓    |  yes       |       partial    | Local; no server-side pagination. |
| Update system                    |   ✓    |  partial   |        ✗         | Velopack wired but no CI release pipeline; no update signature verification in production (only tests). |
| **First-run / onboarding**       |   ✗    |    no      |        ✗         | **No Welcome / company-setup / admin-create flow.** `Login` view exists but no first-run detection. |
| **Licensing system**             |   ✗    |    no      |        ✗         | **No license code at all.** Only a third-party Google-font license JSON file. |
| **Trial mode**                   |   ✗    |    no      |        ✗         | Not implemented. |
| **License key generator**        |   ✗    |    no      |        ✗         | Not implemented. |
| **Cryptographic license signing**|   ✗    |    no      |        ✗         | No RSA / Ed25519 / ECDsa for license payloads. |
| **Online activation**            |   ✗    |    no      |        ✗         | No server; no activation endpoint. |
| **Offline activation**           |   ✗    |    no      |        ✗         | No request-file / response-file flow. |
| **Installer (MSI / Inno / WiX)** |   ✗    |    no      |        ✗         | No installer project, no `.iss`, no `.wxs`, no Velopack bundle configured. |
| **Uninstaller**                  |   ✗    |    no      |        ✗         | (Implicit via installer when built) |
| **CI/CD release pipeline**       |   ✗    |    no      |        ✗         | Only `dependabot.yml` and a PR template. No `build.yml`, no `release.yml`. |
| **Crash reporter**               |   partial | partial |        ✗         | `AppLogService` records crashes; no in-app "send report" UI, no remote sink. |
| **User documentation**           |   ✗    |    no      |        ✗         | None. |
| **Customer-facing About page**   |   ✓    |  partial   |       partial    | `Views/About` exists; license status field is empty because there is no license. |
| **Data integrity at scale**      |   ?    |   ?        |        ✗         | No load-test data. |

### 1.2 What is actually solid

- **Architecture.** Clean MVVM, well-named services, explicit dependency
  injection. ~780 C# files / 162 XAML files / 11 distinct DB contexts.
- **Security primitives.** `OperationGuard` is in place at the service
  layer; `PermissionService` covers 8 roles; `PasswordHasher` uses PBKDF2;
  `SecretVault` uses DPAPI for API keys; `AgentIntegrity` has
  cryptographic checks.
- **Async safety.** `AsyncUtility.SafeFireAndForget` etc. (after the
  `5735cd1` fix) — one of the cleanest fire-and-forget patterns I've
  seen in a WPF codebase.
- **Migration framework.** `SchemaMigrationRunner` + per-DB registries +
  backup-before-migrate is the right shape.
- **Update framework.** Velopack is referenced and `UpdateService` has a
  state machine; the package is referenced from the csproj.

### 1.3 What is **blocking** the release gate

In rough order of how much work each item is:

1. **First-run / onboarding flow.** No Welcome → License → Company → Admin
   → DB-init → Backup-location → Preferences → AI-config flow. App
   currently boots straight into Login with hardcoded users.
2. **Licensing system.** None at all. The Master Prompt requires a
   proper public-key-signed payload, trial/expiry/grace-period, online
   + offline activation, and a separate key generator. This is several
   days of work.
3. **Installer.** No `setup.exe`, no MSI, no Inno Setup, no WiX. The
   Velopack bundle is the right answer (it does install + auto-update
   in one), but the `publish` profile and a release build target are
   not configured.
4. **CI/CD release pipeline.** Only `dependabot.yml` exists. A real
   commercial release needs a `release.yml` that builds Release,
   produces the Velopack bundle, signs it, computes checksums, attaches
   to a GitHub Release.
5. **Crash reporting.** Logs are excellent. There is no "send crash
   report" UI and no remote sink.
6. **Documentation.** Zero customer-facing docs.
7. **Load testing.** No 1k / 10k / 100k-row tests, even synthetic.
8. **End-to-end workflow walk-through.** The Inquiry → PI → JO → DN → TI
   → STMT chain is partially built; I have not clicked through it
   myself and would not claim it works without doing so.

## 2. My plan (real, not aspirational)

I am **not** going to:
- Pretend the application is more ready than it is.
- Push cosmetic fixes while the installer and licensing are missing.
- Suggest a "do everything in one big bang" commit.
- Replace working code.

I **am** going to:
- Start with the **smallest durable** change that unblocks the next
  one (release-branch hygiene, then crash report plumbing, then
  licensing primitives, then first-run, then installer, then CI).
- Stop after each phase to verify the previous one is intact.
- Tell you the truth about progress at every commit.

## 3. Order of work (concrete)

1. **Release-branch hygiene** (1 commit)
   - Confirm the existing `Master` / `arena/*` branching and
     release flow. Document the actual release process in
     `RELEASE.md`. No code change.
2. **Crash-report sink (offline-first)** (2–3 commits)
   - Add a local crash dump writer + ZIP.
   - Add a "Send crash report" UI that produces a redactable
     payload and (optionally) POSTs to a placeholder endpoint.
3. **Licensing primitives** (5–8 commits)
   - `LicensePayload` (record struct).
   - `LicenseSigner` + `LicenseVerifier` (Ed25519 via
     `System.Security.Cryptography`, public key embedded in app,
     private key only in the separate tool).
   - `LicenseService` (load, validate expiry, machine-bound hash,
     grace period, trial mode, edition checks).
   - Separate `Tools/LicenseGenerator` console project.
4. **First-run flow** (3–5 commits)
   - `AppFirstRunService` detects no license.
   - Welcome → Activate / Trial / License file load → Company setup
     → Admin account → Backup location → Done.
5. **Installer (Velopack)** (2–3 commits)
   - Velopack `pack` command in `publish` profile.
   - Bundle produced from CI.
6. **CI/CD release pipeline** (1–2 commits)
   - `.github/workflows/release.yml` — build Release, run tests,
     produce Velopack bundle, sign, checksum, attach to GitHub
     Release.
7. **Documentation** (1 commit)
   - `docs/INSTALL.md`, `docs/FIRST_RUN.md`, `docs/LICENSE.md`,
     `docs/BACKUP.md`, `docs/UPDATE.md`, `docs/TROUBLESHOOTING.md`.
8. **Release-gate checklist** (1 commit)
   - Re-run every item from PHASE 23 of the Master Prompt and
     produce a `RELEASE_GATE.md` with the actual status.

## 4. What I will not do in this session

- The 100k-row load test. There is no test data, no CI to run it in,
  and it would take a full day to set up honestly.
- Replace any working module.
- Touch Inquiry / PI / JO / DN / TI / Statement business logic unless
  required for licensing or first-run.
- Add a placeholder "Licensing ✓" check in the About page that does
  nothing real. If the licensing system is not done, the About page
  says "Not licensed".

## 5. What I need from you

- A go/no-go on the order above (or a re-prioritisation).
- If you have a license-issuing server / domain name / activation
  endpoint, tell me now so I do not have to retrofit. Otherwise I
  will build it as **offline-first with optional online activation
  pointing at `https://licensing.proglass.example/` (placeholder
  hostname — easy to swap later)**.
- Confirm I should keep pushing to
  `arena/01a02dc2-proglassautomationadv` and not a release branch
  named e.g. `release/v2.0.0`.
