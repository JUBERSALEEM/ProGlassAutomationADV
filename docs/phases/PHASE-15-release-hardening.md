# Phase 15 — Release Hardening

**Severity: Low · Estimate: 2-3 days · Prerequisite: all earlier phases**

The last phase deliberately. None of this is urgent, and doing it before the
critical phases would be polishing while the data layer is unsound.

## Status — 15.3 measured 2026-09-09, the rest open

| | |
|---|---|
| 15.1 activate CI | **blocked** — Phase 0 is out of scope at the user's request, and the GitHub App lacks the `workflows` scope |
| 15.2 build trap in documentation | **already done** — `docs/BUILD_VERIFICATION_GUIDE.md` |
| 15.3 orphaned XML documentation | **measured** — 13 blocks in 12 files, each verified individually |
| 15.4 obfuscation | **open** — external tooling, cannot be done here |
| 15.5 installer and upgrade path | **open** — Velopack 1.2.0 is referenced; no packaging change attempted blind |
| 15.6 documentation reconciliation | **ongoing** — `verify-readmes.py` is green: 251 md, 0 broken links |

### 15.3 — the number, and why it took three attempts to get one worth quoting

`ProGlassAutomation.csproj` does not set `<GenerateDocumentationFile>`. Enabling
it is the usual first step toward API docs or IntelliSense on a published
package. The risk is CS1587 ("XML comment is not placed on a valid language
element") — and CS1587 is **not** in `WarningsNotAsErrors`, while
`TreatWarningsAsErrors` is `true`. So every orphaned comment becomes a hard build
error, in one step.

`scripts/report-orphaned-xml-docs.py` now measures that. Its first two versions
were wrong, and the corrections are the useful part:

- **145 blocks in 41 files** — the first version enumerated declaration forms
  with a regex and flagged `/// <summary>` above enum members such as
  `Unknown = 0,`. That is valid documentation and raises no CS1587. Roughly 130
  of the 145 were invented.
- **15 blocks** — inverting the test (orphaned only when followed by a brace, a
  comment, or end of file) cleared those, but a second class remained: this
  codebase writes `[MaxLength(30)] public string GlassType { get; set; }` on one
  line, and skipping "lines that start with an attribute" walked straight past
  the declaration.
- **13 blocks in 12 files** — after fixing the attribute rule. **Every one of the
  13 was then read by hand**, and each is followed by `}` or a `//` comment and
  never by a declaration.

A checker that only ever passes proves nothing, so it was also run against a
synthetic file containing one valid enum comment and one genuine orphan: it
reported the orphan, ignored the valid comment, and exited 1.

The script is documented as a **lower bound**. It will not catch a doc comment
sitting above ordinary code inside a method body. The authoritative count is
`dotnet build /p:GenerateDocumentationFile=true` with CS1587 counted — which
needs the SDK this environment does not have.

Locations:

```
App.StartupCore.cs:1095
Agent/UI/AgentChatView.xaml.cs:79
Services/ThemeManager.Part1.cs:767
Services/ThemeManager.Part1.cs:908
Services/AI/AIService/GeminiAiService.cs:331
Services/Audit/AuditService.Part1.cs:764
Services/Licensing/LicenseService.Part1.cs:821
Services/Licensing/MachineFingerprint.cs:429
Services/Statement/Workflow/StatementWorkflowEngine.Part1.cs:825
ViewModels/AI/AiChatViewModel.Part1.cs:872
ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs:881
ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Helpers.cs:41
Views/AI/AiChatView.xaml.cs:102
```

Thirteen is small enough to fix in one sitting, which is the point of measuring
first: the decision to enable doc generation is now a known half-hour, not an
unknown.


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
