# Phase 3 scoped preparation report

## Scope

This turn prepared CI, test-skip triage, and dependency/SBOM guidance only. It did **not** change the target framework, publish profiles, installer signing, runtime code, or release gates. CI was not run in this Linux environment.

## Windows CI

The workflow YAML is **deferred and is not included in this commit**, at the user's request; it will be added later. The planned definition is manual-dispatch on `windows-2022` (expected Windows Server 2022 image), installs the .NET 8 SDK, then restores, builds, and tests the application and all three test projects. Each `dotnet test` step must fail the job on a non-zero result. Push/pull-request triggers are also left for later approval. There is no solution file in the current checkout, so a future workflow should list projects explicitly.

## Skipped-test triage

The repository-wide scan initially found **88 skipped xUnit declarations** across the three test projects. After source review, **67 were re-enabled** where the skip reason was clearly stale and the current contract/test was deterministic or could be ported to the current production API. This includes permission gates, Delivery Note validation/conversion guards, P&L integrity checks, statement projection, correlation scopes, deterministic process-guard tests, and current recording-setting checks. Affected expected values were updated where they encoded superseded recording defaults. No tests were executed.

**21 declarations remain skipped**, listed individually in [`docs/Phase3-TestTriage.md`](Phase3-TestTriage.md):

- 6 Inquiry attachment manifest tests — 5 marked **HIGH PRIORITY** for attachment persistence/data integrity and legacy migration/data preservation; they need isolated file/audit fixtures.
- 9 provider-fallback tests — deferred because they persist settings to real `%AppData%` and can consult the real credential vault.
- 4 P&L layer tests — deferred until Sheet Store pricing/database access is isolated.
- 1 screen-record capture-default test — its `Supersample` assertion does not represent the current physical capture contract.
- 1 process-start test — can launch an installed `ffmpeg` executable.

No skipped tests were found for auth/bootstrap/last-admin, backup/restore/restore-drill, or generic schema migration. Twelve permission-gate tests were re-enabled; three Delivery Note-to-Tax Invoice conversion guards were also re-enabled, though they do not replace a dedicated TI/DN reconciliation test. The triage note calls out missing/needed Windows coverage in these areas rather than treating the lack of skipped tests as proof of coverage.

## Dependency list and SBOM

Added [`docs/Phase3-Dependencies.md`](Phase3-Dependencies.md) with Windows PowerShell commands for `dotnet restore` and `dotnet list <project> package --include-transitive`. Since no `.sln` is present, the instructions enumerate the app and test projects. The guide also documents optional CycloneDX SBOM generation, the EF Core SQLite / Microsoft.Data.Sqlite / SQLitePCLRaw package chain, and how to query `SELECT sqlite_version()` through the application's provider.

The CVE-2025-6965 GitHub advisory is linked. The guide distinguishes the NuGet package range from the bundled native engine version and instructs release reviewers to verify the loaded SQLite engine is at least 3.50.2, re-check the live advisory, and rebuild/retest/regenerate the SBOM after any package update.

## Verification and next steps

- `git diff --check` passed. Static source checks confirmed 21 remaining skipped declarations are indexed in the triage document. No CI workflow file is included in this commit, as requested.
- No restore, build, test, workflow dispatch, Windows/WPF run, package install, or signing operation was performed; the .NET SDK is unavailable here.
- **Known first-run CI risk:** a prior source scan matching `SqliteResilienceCoverageTests.No_repository_calls_raw_SaveChangesAsync` found 69 direct `SaveChangesAsync` calls. That broad source guard remains in the test project and may fail the first Windows test run; review its scope/expectations before treating CI red as a new Phase 3 regression.
- Before routine CI activation, add the deferred Windows workflow, choose when to add push/pull-request triggers, then run it on Windows and triage any failures.
- Before release, make the remaining high-priority attachment tests hermetic, verify auth/bootstrap/last-admin and TI/DN reconciliation coverage, complete backup/restore-drill acceptance, and perform the dependency/native-SQLite check described in `Phase3-Dependencies.md`.
