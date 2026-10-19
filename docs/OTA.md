# 🔄 ProGlass Automation — OTA Update System

**How updates work · release hosting · versioning · tagging · database safety**

---

## Architecture (what exists)

```
Settings UI (Updates card)          About (version + build pill)
        │                                    │
        ▼                                    ▼
SettingsViewModel ──────────────► Services/Updates/AppInfo  ◄── ONE version
        │                         (reads assembly identity;
        ▼                          .csproj <Version> is the
Services/Updates/UpdateService      single edit point)
  ├─ CheckNowAsync      → GitHub Releases API (public, NO token in app)
  ├─ DownloadNowAsync   → release asset → Downloads folder (progress)
  ├─ InstallAndRestart  → launches installer, app exits, installer applies
  ├─ StartupCheckAsync  → once/day, async, never blocks startup
  └─ state machine      → Idle/Checking/UpToDate/UpdateAvailable/
                          Downloading/DownloadCompleted/Installing/
                          Failed/Offline/Cancelled
        │
        ├─► AuditService (section "Updates"): UPDATE_CHECK,
        │   UPDATE_AVAILABLE, UPDATE_DOWNLOAD_COMPLETED,
        │   UPDATE_DOWNLOAD_FAILED, UPDATE_INSTALL, …
        ├─► AppLogService (technical failures, module "Updates")
        └─► UpdateHistoryService (deduplicated on-screen ledger)
```

**Next step (after first green on-device build):** the Velopack provider
plugs into `UpdateService` and replaces "download + manual installer"
with full in-app **download → apply → restart**, and packages the app
for first-time installation. No UI change will be needed — the states
above already cover it.

## Where releases are hosted

GitHub Releases on `JUBERSALEEM/ProGlassAutomationADV`.
The desktop app calls the **public** releases API — it contains **no
GitHub token or secret anywhere** (spec §9/§23). CI secrets live only
in GitHub Actions.

## Versioning

Semantic versioning: `MAJOR.MINOR.PATCH` (2.0.0 → 2.0.1 → 2.1.0).

**ONE edit point:** the `<Version>` property in
`ProGlassAutomation.csproj`. The assembly identity carries it into:
About · Settings · update checker · installer · release. `AppInfo`
resolves it at runtime; the hard-coded fallback only protects unusual
host scenarios.

## Release tagging & GitHub Actions

- Tags use the `v` prefix and must match `<Version>` in
  `ProGlassAutomation.csproj` exactly (for example `2.0.12` ↔ `v2.0.12`).
- Normal pushes **never** publish a release. The active workflow is
  [`.github/workflows/release.yml`](../.github/workflows/release.yml);
  pushing a matching version tag triggers it.
- The pipeline restores, builds, runs regression tests, and publishes a
  **self-contained single-file win-x64 EXE**. It also builds the License
  Generator as a separate single-file EXE and stores that output only as
  a short-lived private GitHub Actions artifact. The License Generator is
  never attached to GitHub Release assets and is never selected by OTA.
- The application EXE is Authenticode-signed before release. Configure
  repository Actions secrets `CODESIGN_PFX_BASE64` and
  `CODESIGN_PFX_PASSWORD`; publishing intentionally fails if trusted
  signing credentials are missing because the updater refuses unsigned
  downloads.
- Velopack packages remain available for installed-client OTA. The release
  also contains the versioned standalone app EXE and SHA-256 checksum.
  GitHub-generated release notes are attached to the release, and the
  OTA Updates page displays the full changelog.

## How users receive updates

1. App starts → `StartupCheckAsync` (after ~8 s, max once per 20 h)
2. Or Settings → Updates → **Check for updates**
3. Update found → release name, date, notes shown in the card
4. **Download update** → live progress bar
5. **Install & restart** → installer launches, app closes, new version
   installs over application files only
6. Everything is recorded in the Activity log (section **Updates**)

## Database & user-data safety (spec §19–§21)

- User data: `%AppData%/ProGlassAutomation/` — SQLite databases,
  settings, themes, wallpapers, config.
- Application files: installation folder.
- The updater **only replaces application files**. Nothing under
  AppData is ever touched, overwritten or migrated implicitly.
- Database schema changes use the existing EF Core migration path with
  the existing backup-first strategy — an update does NOT force a
  migration unless the new version needs one.

## Offline mode (spec §17)

No internet ⇒ clean "No internet connection. Update check unavailable."
state. Startup is never delayed, the UI never freezes, manual retry is
always available.

## Exact commands — FIRST OTA release

```bash
# 1) finalize code + bump version in ProGlassAutomation.csproj (<Version>)
# 2) commit
git add -A
git commit -m "release: v2.0.1"

# 3) tag with the SAME version
git tag v2.0.1
git push origin <branch>
git push origin v2.0.1        # ← this triggers the release workflow
```

Then watch **Actions** on GitHub: the workflow builds, packages and
publishes the GitHub Release. Users pick it up on their next check.

## Troubleshooting

| Symptom | Where to look |
|---|---|
| Check fails | Activity log → Updates section; `logs/app-*.log` (module Updates) |
| "No releases published yet" | publish a tag first (see commands above) |
| Download fails | network/proxy; the error text is in the card + app log |
| Version looks wrong | confirm `<Version>` in the .csproj — it is the only source |
| Update history empty | Settings → Updates card populates after the first check |
