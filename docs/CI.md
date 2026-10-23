# ProGlass Automation — Continuous Integration

<!-- pg-nav -->
> **[Repository root](../README.md)** · [All documents](README.md) · [Application workflow](WORKFLOW.md) · [Design system](DESIGN_SYSTEM.md)
<!-- pg-nav -->

Two workflows are written, verified and waiting in
[`docs/ci/activate/`](ci/activate/README.md) — complete files, not templates.
They are not yet in `.github/workflows/`, and the reason is a permission, not a
decision:

| Attempt to place them (2026-10-05) | Result |
|---|---|
| `git push` of a commit adding `.github/workflows/ci.yml` | `remote rejected` — *"refusing to allow a GitHub App to create or update workflow … without `workflows` permission"* |
| `PUT …/contents/.github/workflows/ci.yml` (Contents API, explicit author) | `403 Resource not accessible by integration` |

The sandbox credential is a GitHub App installation token, and workflow files
sit behind a permission that token does not hold; a git push and an API write
are refused identically. Your own credentials are not subject to it, so
activation is one command — see
**[`docs/ci/activate/README.md`](ci/activate/README.md)**.

| File | Runs on | Purpose |
|---|---|---|
| [`ci.yml`](ci/activate/ci.yml) | every push to every branch, every pull request, manual dispatch | Proves the tree builds, the suites pass and the docs are consistent |
| [`release.yml`](ci/activate/release.yml) | a `v*.*.*` tag, or manual dispatch | Cuts the installer and publishes the Velopack OTA feed |

A tag push runs `release.yml` only — `ci.yml` filters on `branches`, which
does not match tags — so no commit is built twice, and a release still cannot
be cut from a red tree because `release.yml` carries its own copy of the same
gate.

> **History.** Four workflow templates used to sit in `docs/ci/` and
> `docs/CI/`, disagreeing with each other about the publish verb, about where
> signing happens and about the portable zip's name. They are retired to
> comment-only pointers — each one explains what was wrong with it — because
> roughly a dozen documents link to them, including the September 2026 audits
> that describe CI as it was then. What replaces them is the two files in
> `docs/ci/activate/`, which are one pipeline each rather than four opinions
> about one pipeline.

---

## `ci.yml` — the verification gate

Three jobs, cheapest first, so a docs typo fails in seconds instead of after a
Windows restore.

### 1. `static-checks` — Ubuntu, no SDK

| Step | Command | Fails when |
|---|---|---|
| Documentation | `python3 scripts/verify-readmes.py` | a relative link is broken, `docs/README.md` is stale, or a folder README lost its navigation |
| Index freshness | `python3 scripts/gen-docs-index.py --check` | the indexed document count disagrees with the tree |
| Brace balance | `git ls-files '*.cs' \| xargs -r python3 scripts/check-csharp-balance.py` | a `.cs` file ends mid-string, mid-comment or with unbalanced braces |

The brace scan is **not** a compile check — it is a string- and comment-aware
scanner that catches the one class of mistake a scripted find-and-replace
makes: a truncated or duplicated block. It is the cheapest net in the repo and
it runs over all 1,678 tracked `.cs` files in about three seconds.

### 2. `test-portable` — Ubuntu, .NET 8

`Licensing/ProGlass.Licensing.Tests` and `Tools/EngineRegression`. Both target
portable `net8.0`, both are fast, and **neither is in any solution file**
(`ProGlassAutomation.slnx` omits `Tools/EngineRegression` entirely), so before
this workflow nothing compiled either of them. They are separated from the
Windows job so a licensing-contract regression is reported in a minute rather
than after a WPF build.

### 3. `build-test` — `windows-latest`, .NET 8

The real gate, and the only evidence in this repository that the 1,370
production `.cs` files compile:

```
dotnet restore Tests/ProGlassAutomation.Tests.csproj
dotnet build   Tests/ProGlassAutomation.Tests.csproj -c Release --no-restore
dotnet test    Tests/ProGlassAutomation.Tests.csproj -c Release --no-build
```

**Why the test project and not the app project.** `ProGlassAutomation.csproj`
carries `<Compile Remove="Tests/**" />`, so building the app never compiles the
test project, and a later `dotnet test --no-build` fails with *test source file
not found*. Building the test project compiles the app first through its
`ProjectReference`, so one step produces both DLLs and `--no-build` is honest.
This is the same trap documented at the top of `scripts/build-installer.ps1`
and the exact bug `docs/ci/build-test.yml` used to ship.

`windows-latest` is not a preference: `net8.0-windows` + `UseWPF`
(PresentationFramework, WindowsBase) only resolve on a Windows MSBuild. A
Linux runner fails at restore.

Because the app sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
any new analyzer warning fails this job. That is deliberate; see the
`global.json` note below.

---

## `release.yml` — the OTA release

Two jobs: the same `build-test` gate (a workflow cannot `needs` across files,
and a release must never be cut from a tree that does not build green), then
`release`.

The release job **does not re-implement packaging**. It calls
`scripts/build-installer.ps1` — the same script a developer runs — so there is
exactly one declaration of what a release contains: publish through
`Release.pubxml`, strip `PRODUCTION_READINESS`, bundle FFmpeg, paint the splash
and MSI art, `vpk pack` (Setup.exe + full nupkg + portable zip), SHA-256
summary. The workflow adds only what a local box cannot do:

1. **Resolve and check the version.** From the tag, or from
   `workflow_dispatch`, or from `<Version>` in `ProGlassAutomation.csproj`.
   The job fails unless it is `MAJOR.MINOR.PATCH` **and** equal to the csproj
   value. A mislabelled package is either unreachable by OTA —
   `UpdateService.IsNewer` is a strict greater-than compare — or silently
   skips a release.
2. **Publish the feed.** `vpk upload github --channel stable --publish`, which
   creates the GitHub Release and uploads the `RELEASES` file that
   `VelopackUpdateService`'s `GithubSource` reads on launch. The `RELEASES`
   file is also uploaded as a workflow artifact, because it is the first thing
   to read when an installed app reports "no update available": if the channel
   or the version line is wrong there, the client cannot see the release.

The CLI is pinned to `vpk` **1.2.0** to match the Velopack NuGet library the
app references, and the channel is pinned to **stable** on both sides
(`installer/velopack.json` declares it, the pack step passes it). A feed
written to one channel and read from another reports "no update available"
forever.

### Release runbook

```bash
# 1. Bump the ONE version source
#    ProGlassAutomation.csproj  ->  <Version>2.0.12</Version>
git add ProGlassAutomation.csproj
git commit -m "chore(release): 2.0.12"
git push

# 2. Tag the commit you just pushed. The workflow asserts tag == csproj.
git tag v2.0.12
git push origin v2.0.12
```

Then watch the **Release** workflow. On success the GitHub Release carries the
`RELEASES` feed and the installed apps pick it up on their next check.

### Known limitations — stated, not hidden

| Limitation | Consequence | What it would take |
|---|---|---|
| **Packages are unsigned.** Neither the workflow nor the script signs anything, so a CI release is unsigned exactly like a local `build-installer.ps1` run. | Fine for internal testing. Do not ship unsigned builds to customers. | Give `build-installer.ps1` a `--signParams` passthrough and sign **during** pack. A post-pack `signtool` pass misses the `Setup.exe`/`Update.exe` that vpk generates and invalidates the feed checksums. Secrets would be `CODESIGN_PFX_BASE64` + `CODESIGN_PFX_PASSWORD`. |
| **Full packages only, no deltas.** The script clears `releases\` immediately before packing, and deltas are computed from packages already in the output directory. | Every update downloads the full package. Correct, just not minimal. | A `vpk download github` step that survives into the pack — i.e. pack into a directory the script does not wipe. |
| **`CollectCoverage` is inert.** Only `coverlet.collector` 6.0.2 is referenced, so no Cobertura file is produced. | No coverage number in CI. | Add `coverlet.msbuild`, or pass `--collect:"XPlat Code Coverage"` and upload the artifact. |

### If `build-test` goes red on tests that pass locally

Suspect a desktop session first. The suite is a WPF project; a runner's
session is not the one a workstation provides, and any test that touches a
`Window`, a `Dispatcher` or `Application.Current` behaves differently there.
Triage the failing test — do not add a filter to the workflow to make the box
green. A filtered suite is a suite that has stopped testing the thing you
filtered.

---

## Secrets and permissions

| Secret | Used by | Required? |
|---|---|---|
| `GITHUB_TOKEN` | `release.yml` — `vpk upload github` creates the Release | Automatic. `release.yml` declares `permissions: contents: write`; `ci.yml` declares `contents: read` and nothing more. |
| `CODESIGN_PFX_BASE64`, `CODESIGN_PFX_PASSWORD` | nothing yet | Not wired — see the limitations table. The release job prints a warning either way, so an unsigned release is never a surprise. |

`global.json` pins the SDK to `8.0.100` with `"rollForward": "latestFeature"`,
so `8.0.x` in `setup-dotnet` resolves to whatever 8.0 patch the runner has,
and a 9.x SDK is refused. That is deliberate: with
`<AnalysisLevel>latest</AnalysisLevel>` and `TreatWarningsAsErrors`, a newer
SDK turns a brand-new analyzer rule into a build error on code nobody touched.

---

## Pausing CI

Comment out the `push:` block in `.github/workflows/ci.yml`, or disable the
workflow from the repository's **Actions** tab. Do not delete the file: the
next person to look for CI will conclude there is none, which is the state
this repository was in for its first two years of audits.

## Adding more workflows later

| File | Purpose | Trigger |
|---|---|---|
| `.github/workflows/codeql.yml` | GitHub-native security analysis | weekly + push to the default branch |
| `.github/workflows/dependabot-review.yml` | Auto-approve Dependabot patch bumps that pass `ci.yml` | `pull_request` from Dependabot |
