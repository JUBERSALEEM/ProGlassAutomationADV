# Phase 15 — Release Hardening

**Severity: Low · Estimate: 2-3 days · Prerequisite: all earlier phases**

The last phase deliberately. None of this is urgent, and doing it before the
critical phases would be polishing while the data layer is unsound.

## 15.1 — Activate CI

`.github/workflows/` does not exist. Four complete workflow files sit in
`docs/ci/` and `docs/CI/`, unused, because the GitHub App pushing this repo
lacks the `workflows` scope. **This needs a human with admin rights to copy
`docs/ci/build-test.yml` into `.github/workflows/`.** See `docs/CI.md`.

Nothing in this repository has ever been proven to build. Every other phase
assumes a green build; this is what produces one.

## 15.2 — Fix the build trap in documentation

The repo has `ProGlassAutomation.slnx` and **no `.sln`**. Older 8.0.x SDK CLIs
do not resolve `.slnx`, so a bare `dotnet build` fails with a confusing error
that has nothing to do with the code. `README.md` documents this; the CI file
already builds the `.csproj` explicitly. Consider adding a `global.json` to pin
the SDK so the failure cannot recur.

## 15.3 — Orphaned XML documentation

`App.xaml.cs` has a dangling `/// </summary>`. `App.StartupCore.cs` has two
consecutive `<summary>` blocks before `ShowLanguageOfferIfPendingAsync`, leaving
`BringMainWindowToForegroundAsync` undocumented.

Harmless today because `GenerateDocumentationFile` is off. It becomes **CS1587**
the moment documentation generation is enabled — and this project sets
`TreatWarningsAsErrors=true`, with a `WarningsNotAsErrors` list that does **not**
include CS1587. Fix the comments before enabling doc generation, not after.

## 15.4 — Obfuscation

Release binaries are readable in dnSpy. Acceptable for a desktop ERP; a raised
bar for a commercial product. The licensing design already assumes an attacker
can read the binary — the public key is embedded, and security rests on the
signature, not on secrecy. Obfuscation would raise the cost of tampering without
changing the threat model. Decide and record the decision either way.

## 15.5 — Installer and upgrade path

`installer/` exists. Verify:

- upgrade preserves the data folder, including a shared LAN data root
- a schema migration failure during upgrade is recoverable and does not leave a
  half-migrated database (this depends on Phase 1's migration work)
- uninstall does not delete customer data — the licensing subsystem already
  guarantees this for its own state

## 15.6 — Documentation reconciliation

`docs/ERP_UPGRADE_ROADMAP.md` rates the product **9.7**;
`docs/CODE_RATING_REPORT.md` rates it **9.0**. `README.md` recommends 9.0.

Neither figure came from a green build, and this analysis adds three critical
findings that neither report contains. Reconcile the two documents after Phase 1
lands, and state what the rating is measured against. A rating with no stated
method cannot be defended to a customer.

## Acceptance criteria

- [ ] CI runs on every push and every pull request.
- [ ] `global.json` pins the SDK, or the `.slnx` trap is documented at the point
      of failure.
- [ ] No orphaned XML doc comments.
- [ ] Obfuscation decision recorded.
- [ ] Upgrade and uninstall behaviour verified against a populated database.
- [ ] One rating, one method, stated.
