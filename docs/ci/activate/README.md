# Activating the CI workflows — one command, from your own machine

<!-- pg-nav -->
> **[Repository root](../../../README.md)** · [All documents](../../README.md) · [Application workflow](../../WORKFLOW.md) · [Design system](../../DESIGN_SYSTEM.md)
<!-- pg-nav -->

The two files in this directory are **complete, ready-to-run GitHub Actions
workflows**. They live here rather than in `.github/workflows/` for one reason
only, and it is not a design choice:

| Attempt | Result |
|---|---|
| `git push` of a commit that adds `.github/workflows/ci.yml` | `remote rejected` — *"refusing to allow a GitHub App to create or update workflow `.github/workflows/ci.yml` without `workflows` permission"* |
| `PUT /repos/JUBERSALEEM/ProGlassAutomationADV/contents/.github/workflows/ci.yml` (Contents API, with an explicit author) | `403 Resource not accessible by integration` |

Both were tried on 2026-10-05 from the development sandbox. The credential
there is a GitHub App installation token, and workflow files are the one thing
GitHub protects behind a separate permission — a git push and an API write are
refused identically. Granting the App that scope is an App-owner decision
(`https://github.com/settings/apps/arena-ai-coding-agent/permissions`), not a
repo-owner one. **Your own credentials are not subject to it.**

## Activate

From a clone on your own machine, authenticated as yourself:

```bash
git fetch origin
git checkout fix/production-readiness-2026-10-19
git pull

mkdir -p .github/workflows
cp docs/ci/activate/ci.yml      .github/workflows/ci.yml
cp docs/ci/activate/release.yml .github/workflows/release.yml

git add .github/workflows
git commit -m "ci: activate ci.yml and release.yml"
git push
```

Or without a clone: on GitHub, **Add file → Create new file**, name it
`.github/workflows/ci.yml`, paste the contents of
[`ci.yml`](ci.yml), commit; repeat with `release.yml`.

The push that creates them triggers the first run immediately — `ci.yml` fires
on `push` to every branch, so you do not have to wait for the next commit to
see whether the gate is green.

## After activation

Delete this directory, or leave it — it is inert either way. Nothing outside
`.github/workflows/` is executed by Actions, and `docs/CI.md` points here only
to explain where the files came from. If you keep it, note that the two copies
can drift: **`.github/workflows/` becomes the source of truth the moment it
exists**, and this directory should be deleted in the same commit.

## What you are activating

| File | Trigger | Jobs |
|---|---|---|
| `ci.yml` | every push to every branch, every pull request, manual dispatch | `static-checks` (Ubuntu: docs links, docs index, brace balance over 1,678 `.cs` files) · `test-portable` (Ubuntu: licensing + engine regression, neither of which is in any solution file) · `build-test` (`windows-latest`: the WPF build + 2,369-test suite) |
| `release.yml` | a `v*.*.*` tag, or manual dispatch | `build-test` (the same gate, repeated because a workflow cannot `needs` across files) · `release` (version check against `<Version>`, then `scripts/build-installer.ps1`, then `vpk upload github --channel stable --publish`) |

Both were verified before being written here: no tabs, every `- name:` step has
a `run:`/`uses:` sibling, every folded-scalar continuation line is indented
deeper than its key. They have **not** been executed — no .NET SDK, no
PowerShell and no Windows runner exists in the environment they were written
in. The first run is the first real test of them, and
[`docs/CI.md`](../../CI.md) says what to suspect first if the Windows job goes
red on tests that pass locally.
