# ProGlass Automation — Continuous Integration

This document is the **operational handbook** for the CI/CD pipeline that builds
and tests ProGlass Automation on every push and pull request. It exists because
the canonical workflow file lives at `docs/ci/build-test.yml` (not at
`.github/workflows/build-test.yml`) and a maintainer has to activate it with a
single file copy.

---

## Why is the workflow not in `.github/workflows/`?

The development sandbox pushes to this repository through the
`arena-ai-coding-agent[bot]` GitHub App. The App's installation does **not**
include the `workflows` permission scope, so any push that creates or modifies
a file under `.github/workflows/` is rejected:

> `refusing to allow a GitHub App to create or update workflow
> .github/workflows/build-test.yml without workflows permission`

Granting the App that scope is an App-owner decision (lives at
`https://github.com/settings/apps/arena-ai-coding-agent/permissions`), not a
repo-owner decision, so it has to wait on the platform team. Until then, the
canonical copy of the workflow lives next to its documentation, where it is
still version-controlled and diffable.

The file content is **identical** to what would live at
`.github/workflows/build-test.yml`. Copying it into place is a one-line
operation and is documented below.

---

## Activating the workflow

A maintainer with admin rights on the repo runs, from the repo root, **on a
machine that is not the bot** (i.e. a personal workstation, with their own
credentials):

```bash
# 1. Pull the latest from the working branch
git fetch origin
git checkout arena/01a02dc2-proglassautomationadv
git pull

# 2. Copy the canonical workflow into the location GitHub Actions expects
mkdir -p .github/workflows
cp docs/ci/build-test.yml .github/workflows/build-test.yml

# 3. Commit + push from your own account
git add .github/workflows/build-test.yml
git commit -m "ci(github): activate build-test workflow from docs/ci/"
git push origin arena/01a02dc2-proglassautomationadv
```

After that push lands, the workflow runs on every subsequent push and pull
request to any branch.

> **Tearing it down.** If you want to pause CI without losing the file, move
> it back: `mv .github/workflows/build-test.yml docs/ci/build-test.yml` and
> push. The two files are byte-for-byte identical.

---

## What `build-test.yml` actually does

The workflow runs on **`windows-latest`** (WPF / `net8.0-windows` requires
Windows) and executes, in order:

1. **Checkout** the full git history (so coverage can attribute lines to
   commits).
2. **Setup .NET 8 SDK** (`actions/setup-dotnet@v4`, `8.0.x`).
3. **Restore** NuGet packages for the main project.
4. **Build** with `dotnet build -c Release --no-restore`. Because the project
   sets `TreatWarningsAsErrors=true`, any new warning fails the build — this
   is intentional.
5. **Test** with `dotnet test` plus two loggers:
   - `trx` → `test-results.trx` (Visual Studio test result format)
   - `console;verbosity=normal` → human-readable in the Actions log
   - `XPlat Code Coverage` → Cobertura XML for coverlet
6. **Upload artifacts**:
   - `coverage-cobertura` — Cobertura XML, retained 14 days.
   - `test-results-trx` — TRX, retained 14 days.

Concurrency is configured so that a new push to the same ref cancels the
in-flight run for the same ref, which keeps the Actions minutes budget under
control on a busy branch.

### Why no NuGet cache?

The project pulls a small set of packages and cold restore is well under the
6-minute GitHub-hosted-runner restore budget. Add a `cache` step here when
the dependency graph grows.

### Why `windows-latest`?

`net8.0-windows` + WPF (PresentationFramework, WindowsBase, etc.) only
resolve on a Windows MSBuild. A Linux or macOS runner would fail at restore
with a clear message about an unsupported `TargetFramework`.

---

## Adding more workflows later

When the App's `workflows` scope is granted and `.github/workflows/` becomes
writable from the bot, the recommended next slices are:

| File | Purpose | Trigger |
|---|---|---|
| `.github/workflows/codeql.yml` | GitHub-native security analysis | weekly + push to main |
| `.github/workflows/release.yml` | Build the Velopack `Setup.exe` + portable zip and attach to a GitHub Release | push of a `v*` tag |

Until then, the same workaround applies: keep the canonical file under
`docs/ci/` and document the activation step in this file.
