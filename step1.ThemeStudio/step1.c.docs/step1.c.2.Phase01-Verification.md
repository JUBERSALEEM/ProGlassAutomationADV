# step1.c.2 — Original Phase 01 verification checkpoint (historical)

**Historical checkpoint:** the subsequent [clean-foundation report](step1.c.5.Clean-Foundation-Reference.md) supersedes current ownership, counts and workflow placement. Historical results below are not new Windows execution evidence.

Date: **2026-10-20**. This records the original implementation checkpoint. The subsequent [theme-isolation and Windows CI report](step1.c.3.Theme-Isolation-and-CI.md) supersedes its architecture/file-count/mutation-count details; original unperformed Windows checks remain unperformed. Branch: `arena/bd154b4b-proglassautomation`. Phase 01 explicitly authorized by the user to use the existing root Visual Studio WPF starter. **Implementation is supplied, but build/runtime/visual acceptance remains incomplete.** No further theme/feature is started.

## Architecture and functionality supplied

The existing root project now compiles explicit fresh App/MainWindow, shared engine/styles, ThemeBento dictionaries and Studio source. Unrelated source is preserved on disk and excluded from Compile/Page items; all previous package references were removed from this application's project. Startup no longer invokes SheetStore services/SQLite. There is no parallel application.

ThemeCatalog contains Bento only. ThemeService loads a candidate dictionary, validates the 76 mandatory semantic resources, then replaces its dedicated application palette slot and emits selection notifications. At this historical checkpoint shared styles stayed installed; that design has now been removed in favor of a complete isolated theme package. Both values and screen style references are dynamic; one modeless preview window, existing controls and application-level popups are intended to update together. Invalid selections/resources do not deliberately replace the active set. Switching errors are reported near the selector, not silently swallowed.

Studio includes six focused modules, functional preview name validation and apply/reset actions, progress advance/reset, helper toggle, installed theme and Light/Dark selectors, list selection, tooltip/context actions, status/surface/type examples and a filtered live typed token reference. Only one theme is installed; the theme selector truthfully shows Bento only. It cannot demonstrate switching between different theme identities until another theme is authorized. Light/Dark must visibly update the full resource-backed client UI.

Complete file purposes and styling/platform exceptions: [feature README](step1.c.1.ThemeStudio-README.md). Concrete values/research: [Bento specification](../../Theme/ThemeBento/ThemeBento.Readme.md). Executable contract/extension procedure: [contract README](../../Theme/step1.a.ThemeEngine/step1.a.5.ThemeContract-README.md).

## Checks actually executed in this environment

| Check | Actual result |
| --- | --- |
| `python3 Theme/step1.d.tests/step1.d.3.verify_sources.py` | PASS: 76 typed tokens in each of 2 variants; 74 foreground/background contrast pairs; 18 XAML XML documents; content/property assignment checks; local dictionary/resource references; no view-local theme literals; no product PackageReference. |
| `python3 Theme/step1.d.tests/step1.d.4.CheckerRegression.py` | PASS: all 6 mutations rejected—missing token, wrong type, contrast regression, undefined resource, screen-local palette, duplicate property. Mutations used temporary copies, not original files. |
| `dotnet build ProGlassAutomation.csproj --configuration Release --no-restore` | NOT EXECUTED by a compiler: shell exit 127, `dotnet: command not found`. |
| `dotnet build ProGlassAutomation.slnx --configuration Release` | NOT EXECUTED by a compiler: shell exit 127, `dotnet: command not found`. |
| Official .NET 8 Linux SDK download connectivity | Failed TLS connection to builds.dotnet.microsoft.com (curl exit 35); no SDK installed. A Linux SDK would still not provide Windows WPF runtime execution. |
| Windows STA runtime executable | Added, but **not run or compiled**. Its source assertions are not reported as passing. |
| Actual WPF visual/interaction inspection, screenshots, physical DPI, performance | **Not performed**: Linux container, no Windows WPF runtime or display session. No fabricated screen/browser substitute. |

Portable checks are source checks, not a C#/XAML compiler, full WPF property checker, runtime test or accessibility certification. Measured contrast is for the concrete opaque resource pairings, not every possible state/composition or OS high contrast.

## Review → correction → regression performed at source level

- Found and removed a duplicate Style assignment on a TextBlock.
- Replaced a scalar spacing reference used for a Thickness property with a dedicated typed token.
- Added proper selection foreground/opacity tokens, a separate ComboBox content inset, rounded radio marker and a resource-backed keyboard-focus adorner.
- Implemented both scrollbar orientations instead of leaving horizontal page commands incorrect.
- At the original checkpoint, made shared focus style lookup dynamic to avoid cross-dictionary eager lookup dependency; made screen style consumers dynamic and placed active variants after shared styles so compatible future overrides can replace them.
- Generalized semantic color roles to WPF Brush, not SolidColorBrush-only, so future supported gradients do not require view changes.
- Added a small concrete installed-theme registry; future registration does not require rewriting screens or the loading service.
- Expanded contrast coverage to text on hover/pressed/selected/accent plates. This caught Light TextMuted/StateHover at 4.40:1 and Dark TextMuted/StatePressed at 4.39:1. Corrected the shared muted-text tokens to #555F74 / #B0BAD0 and reran all 74 resource-pair checks. Mutation checks now require a passing baseline before testing deliberate failures.
- Reran portable checks and mutation regressions after these corrections. No rendered comparison/correction has been claimed; that part of the loop is blocked.

## Windows verification commands (must still be executed)

Use supported Windows 10/11 with the .NET 8 SDK and desktop runtime/tooling, or compatible later SDK. Project commands avoid .slnx format requirements. No third-party test framework or product packages are needed.

```powershell
dotnet build ProGlassAutomation.csproj --configuration Release
dotnet run --project Theme/step1.d.tests/step1.d.ThemeTests.csproj --configuration Release
dotnet run --project ProGlassAutomation.csproj --configuration Release
```

The STA executable exits nonzero on failure and records assertions/binding diagnostics. It exercises both sets and negative token cases, invalid selection, actual main/dialog/popup resources, notifications, validation/commands, live filtering, repeated replacement and basic responsive layout. Fix any build/runtime failures it reveals; it has not been validated by running here.

## Remaining manual review checklist — all unchecked

- [ ] Compare actual Light and Dark against the researched specification: visual hierarchy, exact palettes, card/plate variants, text roles and restrained edges.
- [ ] Select each appearance repeatedly; verify all existing elements, selected rows, live values and an already-open preview window update together. Check no stale templates/resources or binding errors.
- [ ] Inspect buttons in normal/hover/pressed/disabled/keyboard-focus states; valid/invalid input with caret/selection; radio/checkbox/list states; statuses and progress; tooltip, popup, context menu and scrolling.
- [ ] Operate with keyboard only: Tab, Shift+Tab, Space, Enter, radio arrows, ComboBox Alt+Down/Escape and context-menu keyboard access. Confirm automation names and readable validation.
- [ ] Resize at 720/1024/1440 window widths and 600/900 heights; inspect actual reflow, wrapping, scrolling and no clipping/overlap/lost content. Repeat with longer input/text.
- [ ] Verify supported Windows display scaling at 100/150/200%, multi-monitor DPI changes and font rendering. Manifest opt-in alone proves none of these.
- [ ] Verify high-contrast/readability behavior, required contrast pairings in actual states/compositions and screen reader semantics. Full conformance/high-contrast integration remains unverified.
- [ ] Measure switching/resizing responsiveness on agreed hardware; no performance budget measurements exist here.
- [ ] Record real screenshots/issues, correct shared resources/templates and rerun build/tests/runtime checks before approval. Only then seek Bento acceptance; do not start Claymorphism automatically.

## Safety and remaining boundaries

No later theme folder, fake selector, placeholder implementation, new integration/database, product dependency, commit or push was introduced. Existing unrelated module files were not cleaned up or modified. Root App/MainWindow/project integration intentionally replaces starter behavior under the user's explicit authorization. OS title chrome/native startup-error MessageBox and structural layout/geometry boundaries are documented in the feature README; there are no intentional screen-local palette exceptions.

**Next review is Phase 01 acceptance after resolving the Windows verification gap, not permission to implement further themes.**

## Final repository checks

- 13 project Markdown documents checked; all 66 local links resolved at verification time.
- Project XML enumerates 18 explicit Compile and 17 Page items (App.xaml remains the conventional ApplicationDefinition); no duplicate item matches or unrelated module source included.
- Project, solution, test-project and manifest XML parsed successfully. XAML class/code-behind pairing and named handlers checked at source level.
- All 50 original files outside the six authorized integration/configuration files and root README remain byte-for-byte unchanged from the starting commit. No tracked files were deleted.
- This phase created 40 files: 19 under Theme (engine/styles/variants/specification/contract/tests), 19 Studio view/view-model/helper files, one manifest, and this verification record. It updated the six root integration/project files plus the ten existing documentation files. The feature README lists each responsibility.
- `git diff --check` and whitespace checks on new files passed. Branch remains unchanged; no commit or push performed.
