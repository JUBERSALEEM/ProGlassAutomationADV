# step1.c.3 — Theme isolation correction and Windows CI evidence

**Historical checkpoint:** the subsequent [clean-foundation report](step1.c.5.Clean-Foundation-Reference.md) supersedes current ownership, counts and workflow placement. Historical results below are not new Windows execution evidence.

Date: **2026-10-20**. This is the current Phase 01 follow-up report; the earlier checkpoint is retained in [step1.c.2](step1.c.2.Phase01-Verification.md). Scope: Bento engine/Studio architecture and verification only. **Windows compilation, runtime and visual approval remain unverified.**

## Final isolation architecture and exact responsibilities

```text
Theme/
├── step1.a.ThemeEngine/          non-visual infrastructure only; no XAML
│   ├── step1.a.1.ThemeContract.cs
│   ├── step1.a.2.ThemeService.cs
│   ├── step1.a.3.ResourceValidation.cs
│   ├── step1.a.5.ThemeContract-README.md
│   └── step1.a.6.ThemeCatalog.cs
├── ThemeBento/                  only implemented visual theme
│   ├── ThemeBento.Light.xaml
│   ├── ThemeBento.Dark.xaml
│   ├── ThemeBento.Readme.md
│   ├── step1.e.7.BentoResources.cs
│   └── step1.e.Components/
│       ├── step1.e.1.Components.xaml
│       ├── step1.e.2.Foundation.xaml
│       ├── step1.e.3.Buttons.xaml
│       ├── step1.e.4.Inputs.xaml
│       ├── step1.e.5.Selections.xaml
│       └── step1.e.6.PopupsAndScroll.xaml
└── step1.d.tests/               compiled and portable verification
```

| File or family | Responsibility and correction |
| --- | --- |
| `ThemeContract.cs` | Only semantic key/type roles and 27 required style/target contracts. No concrete visual defaults or template implementation. Existing 76 public tokens retained because consumers use this contract; no new hypothetical control roles. |
| `ThemeCatalog.cs` | Identity, stable folder metadata and a complete package factory per real registration. Bento only. No plugin scanner, fake registrations or theme-name switch statements. |
| `ThemeService.cs` | Calls the registered factory, validates independently, then installs/replaces one complete package slot. Does not load/retain any component dictionary separately. Contextual load diagnostics and synchronous activation rollback attempt; unrelated resources retained. |
| `ResourceValidation.cs` | Missing/invalid token checks, required Style/TargetType checks and duplicate keys across the entire merged package. Uses candidate dictionaries, never Application.Current as fallback. |
| `BentoResources.cs` | Bento alone owns visual composition/lifetime. Loads fresh Light/Dark values and fresh component key dictionaries. Reuses only Bento-owned sealed style identities so mode changes do not unnecessarily rebuild templates/editing/popups. No mutable dictionary or concrete-brush cache shared between loads/themes. |
| `ThemeBento.Light/Dark.xaml` | Complete Bento appearance mappings/values; a complete installed package combines either with Bento’s own components through its factory. The value files are not advertised as standalone visual packages. |
| `step1.e.1.Components.xaml` | Composes only Bento’s six-part visual implementation; never global infrastructure. |
| `step1.e.2.Foundation.xaml` | Bento window/text roles, card/plate/elevation/typography/focus, separator and progress treatments. |
| `step1.e.3.Buttons.xaml` | Bento buttons and interaction/disabled states. |
| `step1.e.4.Inputs.xaml` | Bento inputs, selection/caret/validation, ComboBox/item/popup visuals. |
| `step1.e.5.Selections.xaml` | Bento checkbox/radio/list selection visuals and states. |
| `step1.e.6.PopupsAndScroll.xaml` | Bento tooltips, context menu, thumb and vertical/horizontal scrollbar visuals. |
| `ThemeBento.Readme.md` | Bento research/design/validation expectations, not a global aesthetic. |
| `step1.d.1.RuntimeChecks.cs` | Separate compiled `--contracts-only` route; STA software-rendered smoke additionally tests existing controls/dialog/popup, template identity and unsaved text/caret during switching, bindings/commands and reflow. |
| `step1.d.2.ContractChecks.cs` | Adds missing style despite active fallback, wrong target type, duplicate merged keys and independently mutable candidate checks. Existing token/contrast/invalid selection cases remain. |
| `step1.d.3.verify_sources.py` | Original portable checks plus isolation checker invocation; does not confuse required style contracts with semantic tokens. |
| `step1.d.4.CheckerRegression.py` | 10 mutation regressions, including visual contract/cross-theme/duplicate-package cases. Each must fail for the intended reason, after a passing baseline. |
| `step1.d.5.verify_isolation.py` | Portable ownership/composition/required target checks, duplicate keys, no cross-theme borrowing or engine visuals, and no sibling-order StaticResource dependencies. |
| `ProGlassAutomation.csproj` | Explicitly includes Bento composition code and nested Bento XAML; removes engine visual-XAML inclusion. Target framework unchanged. |
| `docs/ci/windows-wpf.yml` | Real Windows build, contract and runtime verification configuration; not a release pipeline. |

Removed the old engine `step1.a.4.SharedStyles/` directory after moving/renumbering its six files into Bento’s step1.e branch. No unrelated user file was removed. Studio views/view model did not need a rewrite. Additional permanent rules are in [AGENTS.md](../../AGENTS.md), [architecture](../../docs/01.Architecture-Principles.md), [naming](../../docs/00.Project-Rules.md) and the [typed contract](../../Theme/step1.a.ThemeEngine/step1.a.5.ThemeContract-README.md).

## Why future themes stay independent

A future authorized theme owns its complete appearances, semantic mappings, component styles/templates, typography/shape/depth/states, composition factory, documentation and tests. Registering its factory supplies the existing public consumer roles; it need not borrow or override Bento. The orchestrator removes/replaces the complete prior package, so Bento’s helpers/styles are not left in application lookup scope. Theme-local implementation choices such as Bento’s stable style identity are not mandatory visual architecture for other themes. No extra application executable or duplicated views are needed.

No future theme was implemented or simulated to claim this. Semantic compatibility is specified and validated; visual/runtime extensibility cannot be called proven before actual tests of an authorized future theme.

## Windows workflow

Path: **[docs/ci/windows-wpf.yml](../../cols/ci/windows-wpf.yml)**.

- Triggered by pull requests, pushes to master/arena branches and manual dispatch once present on GitHub. Read-only repository permissions; checkout does not persist credentials.
- Runs on **windows-2022**. Official `actions/setup-dotnet@v4` installs **8.0.x** for the existing `net8.0-windows7.0` projects. A CI-only generated global.json selects the installed 8.0 SDK instead of an unrelated newer preinstalled SDK. No repository target-framework or release-policy change.
- Restores/builds actual `ProGlassAutomation.csproj` and `Theme/step1.d.tests/step1.d.ThemeTests.csproj`. Project paths deliberately avoid the SDK 8 / .slnx format incompatibility.
- Uses official Python setup, then portable source/isolation and 10-mutation checks.
- Runs the compiled test executable with `--contracts-only`, then its real-WPF STA software-rendered runtime smoke in a separate timed step. No mocked renderer or fake screenshots.
- Native command errors fail the step/job; multi-command restore/environment steps check exit codes explicitly. No continue-on-error or failure-masking pipeline. Runtime timeout also fails. The always-run summary reports status/limitations without overriding failure.
- No artifact/executable upload, release, package publishing or deployment. A future visual/desktop limitation or assertion failure must remain a visible failed result, not be silently skipped.

## Actual results — no Windows run claimed

| Evidence category | Actual result |
| --- | --- |
| Portable source checks | **PASS:** both 76-token variants, 74 contrast pairings, 18 XAML documents, content/class/handler/resource checks and screen hardcoding restrictions. |
| Portable isolation checks | **PASS:** engine contains no visual XAML; visuals stay under ThemeBento; 27 style/target roles in both packages; no duplicate package keys, cross-theme borrowing or sibling merge-order dependency. |
| Regression mutations | **PASS:** all 10 deliberately broken temporary copies rejected for intended reasons. Original source untouched by mutations. |
| Final integration/preservation audit | **PASS:** 19 explicit Compile items and 17 Page items, existing paths/no duplicates; 14 Markdown documents/83 local links; 50 unrelated original files byte-for-byte unchanged; no tracked deletions or empty theme folders; `git diff --check` clean. |
| Workflow YAML | **PASS:** parsed with unique-key checking; required triggers, official setup action/8.0.x, Windows runner, actual paths, read-only permissions and no continue-on-error/upload/release actions checked locally. This is not execution or exhaustive Actions/PowerShell validation. |
| actionlint | **NOT RUN:** optional small linter download failed at GitHub release-asset connection. A small YAML parser was used in an external cache; no repository/product dependency was added. |
| GitHub Actions run access | **BLOCKED:** `gh run list --limit 5` returned HTTP 403, `Resource not accessible by integration`. No run ID/result is available. The GitHub connection needs Actions-capable access before listing/dispatching runs. |
| Workflow execution for current changes | **NOT RUN:** The original push was rejected for missing workflow-write permission. At the user’s request the workflow is stored inactive under docs/ci for pushing without that permission; it cannot execute there. No unrelated remote run is claimed as evidence. |
| Actual Windows compilation / compiled tests / STA runtime | **NOT RUN:** no local SDK/runtime installation attempted again; remote execution unavailable for the reasons above. Build/runtime success is not inferred from portable checks. |
| Human rendered visual inspection | **NOT PERFORMED:** no native WPF rendering service or remote Windows desktop was available. No source screenshot, browser mock-up or generated approximation was used. |

The runtime harness edits TextBox text through `SetCurrentValue` and explicitly checks that its binding survives, rather than replacing the binding with a local assignment. This correction has been source-reviewed only, not executed.

No SDK, Visual Studio, Windows runtime or large build-tool installation was attempted in this follow-up. Target frameworks in both projects remain `net8.0-windows7.0`.

## Remaining verification

After separately authorized synchronization and Actions-capable GitHub access, run this exact workflow for the resulting commit and retain the real run URL/conclusion/logs. Fix any compiler, contract or runtime failures; do not convert them into warnings or continue-on-error. The configured STA harness is unproven on the hosted runner until executed; it intentionally fails if popup/desktop behavior is unavailable or assertions/timeouts fail.

Even a future green result will not establish visual correctness. Still inspect the real WPF screen in both modes: hierarchy/component states, keyboard focus/access, open dialogs/popups, editing/selection state, resizing/clipping at supported sizes, actual 100/150/200% DPI/scaling, high contrast/screen reader behavior and measured performance. Compare to the Bento specification, correct Bento resources/components, rerun regressions, then seek user acceptance. **Do not begin Claymorphism or another theme.**

Workflow activation status: `docs/ci/windows-wpf.yml` is an **inactive template**, moved at the user’s request after GitHub rejected the push for missing workflow-write permission. Restore it to `cols/ci/windows-wpf.yml` using the commands in its header before expecting CI runs; restoration requires workflow-write permission.
