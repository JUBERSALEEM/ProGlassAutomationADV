# ProGlass Automation — Installer Pipeline

This document is the **operator handbook** for the local + CI installer
pipeline. It complements `docs/CI.md` (the build-and-test pipeline). The two
share the same `Release.pubxml` publish profile and the same `installer/
velopack.json` pack manifest, so a change to one place applies to both.

---

## What ships

For each tagged release (`v2.0.1`, `v2.0.2`, …) the pipeline produces:

| Artefact | Purpose |
|---|---|
| `Setup.exe` | One-click Velopack installer. Shows the Prism splash, installs for the current user, then launches the app. |
| `ProGlassAutomation-2.0.1-full.nupkg` | The full payload, in Velopack's nupkg format. Future deltas diff against the most recent full. |
| `ProGlassAutomation-2.0.1-delta.nupkg` | Binary delta against the previous release. Only produced when an older release exists in the release feed. |
| `ProGlassAutomation-Portable-2.0.1.zip` | Same binaries as `Setup.exe`, just zip-wrapped. For sysadmins who block Setup.exe or for kiosks where the app runs from a thumb drive. |
| `release.yml` (in CI) | A GitHub Release entry with the assets above, generated release notes, and a stable download URL. |

The SHA-256 of every artefact is printed at the end of a local build, so the
operator can attach them to a GitHub Release (or to a manual download
mirror) with confidence the bytes match what was tested.

---

## Where the pipeline lives

| File | Role | Used by |
|---|---|---|
| `installer/velopack.json` | Velopack pack manifest: packId, title, authors, copyright, icon, readme, target rid, framework, include/exclude globs. Version is overridden on the command line. | Local build script + CI release workflow |
| `installer/content/` | Welcome, readme, licence, and finish text for the MSI wizard. | `vpk pack` when the installed vpk supports those flags |
| `scripts/generate-installer-art.ps1` | Paints the Prism splash and the two MSI bitmaps into `installer/generated/`. Those images are not committed. | Local build script |
| `Properties/PublishProfiles/Release.pubxml` | `dotnet publish` profile: `SelfContained=true`, `win-x64`, `ReadyToRun` on, deterministic build on, PDBs out. No secrets. | Local build script + CI release workflow |
| `scripts/build-installer.ps1` | Local mirror of the CI workflow. Runs on a Windows dev box with no GitHub involvement. | Operator (you) |
| `.github/workflows/release.yml` | The CI mirror of the local build script. Runs on `windows-latest` on every `v*` tag push. | GitHub Actions |

`docs/ci/build-test.yml` is the **sibling** workflow that runs the
build-and-test gate on every push and PR; it must be green before
`release.yml` will produce a usable installer.

---

## Cutting a release — the short version

1. **Bump the version** in `ProGlassAutomation.csproj`:
   ```xml
   <Version>2.0.1</Version>
   <AssemblyVersion>2.0.1.0</AssemblyVersion>
   <FileVersion>2.0.1.0</FileVersion>
   ```
   The csproj comment block reminds you that this is the **single
   authoritative source** for the version string. About, Settings, the
   updater, the installer, and the release tag all read from this.

2. **Run the local build to sanity-check the bits on your own box**:
   ```powershell
   pwsh ./scripts/build-installer.ps1 -Version 2.0.1 -Clean
   ```
   Read the SHA-256 summary, then smoke-test `Setup.exe` by double-
   clicking it on a clean Windows VM. The portable zip can be smoke-
   tested by extracting it and running `ProGlassAutomation.exe` directly.

3. **Commit + tag + push**:
   ```bash
   git add ProGlassAutomation.csproj
   git commit -m "release: 2.0.1 — <one-line summary>"
   git tag v2.0.1
   git push origin arena/01a02dc2-proglassautomationadv --follow-tags
   ```

4. **The `release.yml` workflow does the rest** — it picks up the
   `v2.0.1` tag, re-builds deterministically, signs the binaries if
   `CODESIGN_PFX_BASE64` is configured, runs `vpk pack`, and creates
   the GitHub Release with the assets attached. The whole job takes
   about 6 minutes on `windows-latest`.

---

## Authenticode signing

The CI release workflow signs every shipped `.exe` and `.dll` with
Authenticode when the following repo secrets are configured
(Settings → Secrets and variables → Actions):

| Secret | Content |
|---|---|
| `CODESIGN_PFX_BASE64` | The base64 of the .pfx code-signing certificate. |
| `CODESIGN_PFX_PASSWORD` | The password for that .pfx. |

If the secrets are absent, the signing step logs "No code-signing
certificate configured — skipping Authenticode signing." and proceeds.
The release is published unsigned. The desktop app's updater
(`Services/Updates/UpdateSignatureVerifier`) will then refuse to apply
the update — **expected and safe** — and the user will be steered
back to a manual installer download. So unsigned builds are useful
for the developer's own testing but should never be sent to a
customer.

To rotate the certificate, push a new base64 blob to the
`CODESIGN_PFX_BASE64` secret; the next `v*` tag will pick it up.

---

## Why the install folder is `Program Files` and the data is in `%AppData%`

This is the Windows convention and it is also what Velopack's
`Setup.exe` does by default. Two reasons:

- **`Program Files` is the installer scope** — only an admin can
  write to it, and the installer runs as admin. The user never has
  to be one to run the app.
- **`%AppData%` is the user scope** — it is the only folder the
  regular user can write to without a UAC prompt. All 11 SQLite
  databases, the logs, the licence, the crash reports, and the
  config live there. A backup includes the entire `%AppData%\
  ProGlassAutomation\` folder; an uninstall leaves it in place so
  the next install can pick it up. The customer can copy the
  folder to a new machine and re-install the app to migrate.

`INSTALL.md` (the customer doc) covers this in plain language.

---

## Verifying a release after publish

After a tag push, the `release.yml` workflow uploads the assets to
GitHub Releases. Three quick sanity checks:

1. Open the release page, confirm the assets are present
   (Setup.exe, full nupkg, delta nupkg if applicable, portable zip).
2. On a clean Windows VM, download `Setup.exe`, install it, launch
   it, confirm the About page shows `2.0.1`.
3. With the install still on `2.0.0`, drop a fresh `.lic` into
   `%AppData%\ProGlassAutomation\`, restart the app, confirm
   the licence is picked up. (Reinstalling preserves `%AppData%`,
   so the licence survives.)
