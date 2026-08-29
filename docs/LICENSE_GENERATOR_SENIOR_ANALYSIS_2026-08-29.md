# License Generator — Senior Analysis: Startup Failure "Cannot find resource named …"
**Date:** 2026-08-29 · **Branch:** `arena/01a04c12-proglassautomationadv`

---

## 1. Symptom

The standalone tool `Tools/LicenseGenerator` builds but crashes at startup:

```
Startup failed:

Cannot find resource named TTextitle", Resourcenames ar case sensitive.

Details: System.Exception
```

(The message box is the tool's own `App.OnStartup` failure handler, which
unwraps the deepest exception — a `XamlParseException` from WPF resource
resolution. The exact key name in the message is `TextTitle`; the report's
"TTextitle" is the same first unresolved lookup — the `◆` logo `TextBlock`
in `MainWindow.xaml` — with a transcription slip / stale local copy.)

## 2. Root cause

The global typography migration (commit `a36a2cc`) remapped every numeric
`FontSize` below 30 DIPs to canonical `{StaticResource Text…}` tokens.
That sweep was repo-wide and it correctly reached the tool's XAML:

- `Tools/LicenseGenerator/MainWindow.xaml` — now references `TextTitle`,
  `TextBody`.
- `Tools/LicenseGenerator/Theme.xaml` — now references `TextBody`,
  `TextBodyLarge`, `TextHeadline`.

**But the License Generator is a separate WPF assembly with its own
resource scope:**

| | Main app | License Generator |
|---|---|---|
| Theme dictionaries | ~20 merged in `App.xaml`, incl. `Themes/Styles/TypeScaleTokens.xaml` | **one** dictionary: `Theme.xaml`, loaded manually in `App.OnStartup` |
| `App.xaml` resources | rich | **empty** `<ResourceDictionary/>` |
| `Text*` size tokens | defined (TypeScaleTokens.xaml) | **defined nowhere** |

`App.OnStartup` loads `Theme.xaml` into `Application.Resources`, then
constructs `MainWindow`. The first unresolved `{StaticResource TextTitle}`
throws during XAML parse — WPF aborts the window load, the catch in
`OnStartup` shows the "Startup failed" dialog, and the tool exits.

The tool cannot reference the main app's dictionary (different assembly,
no project reference — by design: the tool is standalone and depends only
on `ProGlass.Licensing`), so the tokens must exist **inside the tool**.

## 3. Fix applied

`Tools/LicenseGenerator/Theme.xaml` now declares the same canonical
type-scale tokens as the main app's `Themes/Styles/TypeScaleTokens.xaml`
(kept in sync, values identical):

```xml
<sys:Double x:Key="TextXSmall">11</sys:Double>
<sys:Double x:Key="TextCaption">12</sys:Double>
<sys:Double x:Key="TextBody">13</sys:Double>
<sys:Double x:Key="TextBodyLarge">14</sys:Double>
<sys:Double x:Key="TextSection">15</sys:Double>
<sys:Double x:Key="TextSubhead">16</sys:Double>
<sys:Double x:Key="TextTitle">18</sys:Double>
<sys:Double x:Key="TextTitleLarge">20</sys:Double>
<sys:Double x:Key="TextHeadline">22</sys:Double>
<sys:Double x:Key="TextDisplay">24</sys:Double>
<sys:Double x:Key="TextDisplayLarge">28</sys:Double>
```

This keeps the tool's typography aligned with the app-wide scale (and the
sizes the sweep already mapped to), and it is self-contained — no
cross-assembly resource dependency, exactly matching the tool's
standalone design.

## 4. Full cross-check performed (all passed)

1. **Resource resolution** — extracted every `{StaticResource …}` from
   `MainWindow.xaml` (21) and `Theme.xaml` (16); after the fix **0 missing
   keys** (before: `TextTitle`, `TextBody`, `TextBodyLarge`, `TextHeadline`).
2. **XAML well-formedness** — `App.xaml`, `MainWindow.xaml`, `Theme.xaml`
   all parse.
3. **No leftover literals / mangled tokens** — no numeric `FontSize`
   remains and no `TText*`/`TexteTitle`-style corruption exists anywhere.
4. **Event-handler wiring** — all 11 handlers declared in
   `MainWindow.xaml` exist in the code-behind (Click/SelectionChanged).
5. **x:Name wiring** — all 29 named elements are referenced by the
   code-behind; no orphan or missing names (BAML-level error source).
6. **Code-behind resource access** — the only runtime brush lookup is
   `TryFindResource(key) as Brush ?? Brushes.DimGray` (null-safe); the
   four keys it uses (`SuccessBrush`, `DangerBrush`, `WarningBrush`,
   `TextDimBrush`) are all defined in `Theme.xaml`.
7. **Type surface** — `OfflineActivationRequestDocument` /
   `OfflineActivationResponseDocument` are public in `ProGlass.Licensing`
   (net8.0, portable); `IssuerKeyMaterial`, `AppCompatibleIssuerKeys`,
   `LicenseGeneratorCore`, `AppLogServiceStub` all live in the tool.
   Project reference `..\..\Licensing\ProGlass.Licensing.csproj` exists.
8. **Usings** — the tool's C# carries explicit `System.IO` /
   `System.Windows.Media` / `Microsoft.Win32` usings; no file anywhere in
   the repo uses `File`/`Path`/`Directory` without `System.IO` (the
   CS0103 class of failure fixed in `8d02e16`).
9. **Source-inspection tests** (`Tests/Agent/LicenseGeneratorSourceTests.cs`)
   — every asserted invariant still holds: no `StartupUri`, theme loaded
   explicitly before the window, `ResolveBrush(`, `result.OutputPath`,
   `IssuerKeyMaterial.LoadFromFile`, `AppFont`, no `DropShadowEffect` in
   `Theme.xaml`.
10. **Scope audit for the same class of bug elsewhere** — the only
    assembly-relative XAML outside the main app is this tool. About view
    sections merge dictionaries locally but resolve inside the main app's
    `Application.Resources` (which contains the tokens), so they are safe.
11. **Repo-wide validation** — all 174 XAML files parse; lexical scan of
    every `.cs` file reports 0 issues.

## 5. Commit

- `«hash»` — *Fix License Generator startup: define canonical type-scale
  tokens in the tool's own Theme.xaml* — plus this document.

## 6. Operator notes

- If the error text still shows after pulling, the local working copy is
  stale (the message in the report predates this fix) — re-pull the branch
  and rebuild the `ProGlassAutomation.LicenseGenerator` project.
- The tool is included in `ProGlassAutomation.slnx`; building the solution
  builds it. `TreatWarningsAsErrors` is on for the tool with the same
  non-blocking warning carve-outs as the rest of the repo.
