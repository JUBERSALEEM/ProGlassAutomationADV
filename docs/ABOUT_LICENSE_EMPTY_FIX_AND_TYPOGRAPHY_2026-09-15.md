# About → License: the blank tab (root cause) + the About typography pass — 2026-09-15

Owner report: **"License page is still empty."** It was, and the previous
attempt could not have fixed it — the tab was never being shown at all.

---

## 1 · Symptom

* The About page opens fine (hero, rail, tabs all render).
* Clicking **License** highlights the rail item, the page title changes to
  `About → License`, and the content area stays **completely blank**.
* The other five tabs render.

## 2 · Root cause — a hijacked DataContext broke the shell's tab switch

**`Views/About/Sections/AboutLicenseCard.xaml.cs` (before this change)**

```csharp
public AboutLicenseCard()
{
    InitializeComponent();
    DataContext = LicenseStatusViewModel.Instance;   // ← on the UserControl ROOT
}
```

**`Views/About/AboutView.xaml` (before this change)**

```xml
<aboutSections:AboutLicenseCard x:Name="LicenseSectionHost">
    <aboutSections:AboutLicenseCard.Style>
        <Style TargetType="UserControl">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding SelectedTabIndex}" Value="1">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </aboutSections:AboutLicenseCard.Style>
</aboutSections:AboutLicenseCard>
```

A binding declared on an element resolves against **that element's own
DataContext**. The card's constructor had already replaced it with
`LicenseStatusViewModel`, so WPF asked the *license* view model for
`SelectedTabIndex`:

1. the property does not exist → the binding fails,
2. the DataTrigger never activates → the `Collapsed` base Setter stays,
3. the section is never laid out → **an empty page under a tab strip that
   still works perfectly** (the strip is owned by the shell).

The other five sections inherit the shell's `AboutViewModel`, which *does*
expose `SelectedTabIndex` — which is exactly why only License was blank.

### Why the three previous "License looks empty" fixes did nothing

| Previous fix | Why it could not help |
|---|---|
| Auto-trial now renders "Evaluation user / ProGlass Automation" | Data inside a card that was never made visible. |
| `VmUnavailableNote` when the singleton fails | Same — a visible card was never the problem. |
| The `Run.Text` OneWay fixes (ERR-38B0CA) | That crash class was real, but a crash would have shown the About failure panel, not an empty content area. |

### Proof (tooling)

The repo already had `scripts/layout-verify/datacontext_scan.py`, written for
this exact bug class — but it only inspected bindings written as *attributes*
on the element, and this binding lived inside a **property element**
(`<AboutLicenseCard.Style>`), so it stayed silent. The scanner now walks the
whole element block (property elements included) and requires anchors to be
recognised. Run against the pre-fix files it reports:

```
Views/About/AboutView.xaml:478
   HIJACKED DATACONTEXT: <AboutLicenseCard> is given an unanchored {Binding}
   by its parent, but AboutLicenseCard.xaml.cs sets the ROOT DataContext =
   LicenseStatusViewModel.Instance.
   Bound here: ['SelectedTabIndex']
   1 problem(s)
```

`0 problem(s)` for the fixed files.

## 3 · The fix

1. **The switch is anchored to the shell** (`Views/About/AboutView.xaml`).
   Each section host now declares:

   ```xml
   <aboutSections:AboutLicenseCard x:Name="LicenseSectionHost"
       Visibility="{Binding DataContext.SelectedTabIndex,
                    ElementName=AboutShell,
                    Converter={StaticResource EqualityToVisibilityConverter},
                    ConverterParameter=1}"
       RenderTransformOrigin="0.5,0.5">
   ```

   `x:Name="AboutShell"` on the root UserControl is the anchor, so **no
   section — present or future — can break the tab switch again** by
   re-pointing its own DataContext. The inline `<X.Style>` + DataTrigger
   blocks are gone (fewer Freezables in a template-loaded tree, ERR-D71810).

2. **The card keeps its own data source on an inner element.**
   `Views/About/Sections/AboutLicenseCard.xaml` gained
   `<StackPanel x:Name="CardRoot">`; the code-behind assigns
   `CardRoot.DataContext = vm;` (host-supplied `LicenseStatusViewModel` wins,
   otherwise the singleton) and re-resolves on `Loaded` for late assignments.
   The root DataContext is never touched.

3. **`AboutCrashReportsCard` had the same anti-pattern** (root
   `DataContext = new AboutCrashReportsViewModel();`) — same treatment, since
   it is a card inside a section that the shell switches.

4. **Never blank as a last resort.** `AboutView.EnsureDataContext()` creates a
   fallback `AboutViewModel` (and logs the incident) if the view is ever hosted
   with a null DataContext, so the page cannot render as an empty scaffold.

5. **The gate now actually runs.**
   `scripts/layout-verify/verify_all.sh` pointed at `tools/layout-verify/`,
   which does not exist — every check failed to start, so nobody had run the
   suite. Paths fixed and a new **Bindings** stage added:

   ```
   run "datacontext hijack" python3 scripts/layout-verify/datacontext_scan.py --strict \
       --exclude "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml"
   ```

   (The Step-9 baseline is benign: its host passes the same view model
   explicitly with `DataContext="{Binding}"`.)

## 4 · Regression locks (`Tests/About/AboutModuleAuditTests.cs`)

| Test | What it forbids |
|---|---|
| `About_section_switches_read_the_shell_datacontext` | A missing `ElementName=AboutShell` anchor, a wrong `ConverterParameter`, or any return of `Visibility="{Binding SelectedTabIndex…}"` on a section host. |
| `About_views_do_not_hijack_a_hosted_section_datacontext` | Any `DataContext = …` root assignment in the seven About section code-behinds; requires the `CardRoot` pattern. |
| `About_views_use_the_about_type_scale_and_the_global_weights` | Hard-coded sizes / weights and all fractional font sizes in the About views. |
| `About_weight_hierarchy_is_disciplined` | Bold below 12 px, SemiBold on a 16 px+ title. |
| `About_theme_defines_the_type_scale_and_whole_pixel_sizes` | A missing type token or role style, a fractional type token. |
| `About_license_card_field_rows_follow_the_label_value_roles` | The detail rows drifting off the label/value roles. |

## 5 · Typography pass — "proper bold fonts, proper styling"

The module carried **20 hard-coded font sizes, half of them fractional**
(13.5 / 12.5 / 11.5 / 10.5) and **97 hand-set weights (68 of them Bold)**.
Under `TextOptions.TextFormattingMode="Display"`, a fractional size is hinted
at a half-pixel grid: stems land between pixels, so the bold cuts render
muddy and the semi-bold cuts read almost like regular — and when everything
is bold, nothing is emphasised.

* **One scale, 15 role tokens, whole pixels only**
  (`Views/About/AboutIos26Theme.xaml`): Caption 10 · Label 11 · Small 12 ·
  Body 13 · Value 14 · Subtitle 15 · CardTitleSm 16 · CardTitle 17 ·
  SectionTitle 18 · Glyph 19 · H2 20 · H1 22 · Metric 24 · Stat 28 ·
  Display 30.
  Fractional sizes snap **down** to the whole pixel below, so a 13.5 px label
  keeps its 1 px distance from a 14 px value instead of colliding with it.
  Nothing moved more than 0.5 px, so the approved mockups still hold.
* **Weights come from the ONE global scale** (`Weight.Regular` /
  `Weight.SemiBold` / `Weight.Bold` in `GlobalDesignSystem.xaml`), applied
  through the documented three-level hierarchy:
  Regular = body + muted labels · SemiBold = micro labels, chips, buttons,
  compact (≤13 px) values · **Bold = titles, metrics and field values at
  ≥14 px**.
* **288 literals replaced** across the nine About view files; the audit test
  fails the build if a literal comes back.
* **Role styles** for the repeating patterns: `Ios26CardTitleStyle`,
  `Ios26FieldLabelStyle`, `Ios26FieldValueStyle`. The License detail grid now
  reads as quiet 13 px Regular labels over 14 px **Bold** values — the
  "proper bold" the screen was missing.
* **The ACTIVE rail tab carries the Bold cut** while idle tabs stay SemiBold:
  one weight step of hierarchy instead of everything shouting.
* MDL2 glyph sizes reuse the same scale, so there is exactly one place in the
  module where a font size is written down.

## 6 · Other senior improvements in this pass

* **Accessibility** — the two icon-only copy buttons (license ID, machine
  fingerprint) now expose `AutomationProperties.Name` +
  `AutomationProperties.HelpText`, so screen readers announce "Copy license
  ID" instead of an unnamed button.
* **Ellipsised values stay reachable** — the shared value style trims with
  `CharacterEllipsis`, so every value row carries the full text as a tooltip.
* **Documented contracts** — `AboutView.xaml`, `AboutLicenseCard.xaml`,
  `AboutIos26Theme.xaml` and the crash-reports card each explain the
  DataContext rule and the type scale at the point of use.

## 7 · Verification

| Check | Result |
|---|---|
| `python3 scripts/xaml-guard.py` | clean — 274 XAML files |
| `python3 scripts/layout-verify/xaml_wellformed.py` | 274 files, 0 failing |
| `python3 scripts/layout-verify/datacontext_scan.py --strict` | 0 problems for the About module |
| `python3 scripts/layout-verify/brace_scan.py` (About code-behind) | 0 failing |
| `python3 scripts/layout-verify/using_check.py` (About code-behind) | 0 missing usings |
| `python3 scripts/xaml-resource-lint.py` | clean (107 pre-existing L2 warnings) |
| `python3 scripts/xaml-xref-audit.py` | forward=0 duplicate=0 |
| DynamicResource key audit (About module) | no unresolved keys |
| Audit-test logic mirrored over the real files | all six new locks pass |

> No .NET SDK is available in this sandbox, so `dotnet build` / `dotnet test`
> could not be executed here. Every check above is static (XAML parse, resource
> resolution, source contracts).

### Pre-existing findings the repaired gate now surfaces (untouched, not ours)

* `property el. order` — 20 findings in unrelated dialogs / `InquiryListView`
  / `SpecificationsStep` (property element after a *property* element, which
  WPF permits — checker noise).
* `brace balance` — `AppSchemaMigrations.cs` "unterminated string": C# raw
  string literals (`"""`) confuse the scanner.
* `missing usings` — `SalesReportView.xaml.cs` uses the fully qualified
  `System.Windows.Media.Animation.EasingMode`, so the heuristic flags a false
  positive.

All three are outside the About module and were failing before this change;
they should be handled in a separate, deliberate pass.
