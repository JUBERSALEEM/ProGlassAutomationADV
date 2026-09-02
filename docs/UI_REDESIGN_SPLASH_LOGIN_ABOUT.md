# Splash · Login · About — Industrial Redesign (v5)

**Date:** 2026-08-21
**Branch:** `arena/01a0245f-proglassautomationadv`

Senior-developer analysis and implementation for the three-screen redesign,
guided by the `ui-ux-pro-max` skill (recommended direction: *Minimalism &
Swiss* — navy `#0F172A` stage, restrained blue accent, 12px cards, no
decorative blur).

---

## 1. Pill radius (999) removed globally

`PillCornerRadius` was defined as `999` in both themes but — after the
earlier button migration — **no style actually used it**; it only lived on
as a definition and three stale comments. Removed completely:

| File | Change |
|---|---|
| `Themes/DarkTheme.xaml` | deleted `<CornerRadius x:Key="PillCornerRadius">999</CornerRadius>` |
| `Themes/LightTheme.xaml` | deleted the same key |
| `Themes/Styles/DesignTokens.xaml` | removed the radius-scale line + stale "full pill radius (999)" toolbar comment |
| `Services/ThemeManager.cs` | removed the "PillCornerRadius stays 999" sentence |

Every card / input / button now binds `CardCornerRadius` /
`ControlCornerRadius` / `CardCornerRadiusLarge` (all **12px**).

---

## 2. Splash screen — industrial redesign

The glassmorphism "aurora orbs + frosted glass" stage read as consumer
software. It is now **factory software**:

- deep steel-navy stage with a faint **blueprint grid** + corner brackets
  + a 1px accent hairline
- flat **12px cards** (trust chips, logo tile, status, footer) — no glass,
  no orbs
- **Consolas (monospace)** accents for version / status / footer measures
- restrained steel-blue accent (`#38BDF8`)

**Zero regression:** every `x:Name`, the `FadeOutAndClose / RunLoginAsync /
ShowTermsGate` API, the terms gate (UAE legal copy unchanged), the login
gate, and the animation choreography in `SplashView.xaml.cs` are untouched.

---

## 3. Login — industrial redesign

`AuthGlassStyles.xaml` and `LoginPanel.xaml` are restyled to match the
splash:

- flat steel card (`#101A2E`) with a 1px `#2A3A55` border and a top accent
  hairline (12px corners)
- steel inputs (`#0C1626` fill), square-tick checkbox, monospace version +
  status lines
- the frosted "glass" look is gone; the single SIGN IN action and all
  keyboard/focus behaviour are unchanged

All resource **keys** are preserved verbatim, so
`ResetPasswordWindow` and `ForcePasswordChangeWindow` pick up the new look
with no edits (their `OrbLeft/OrbRight` references now resolve to a faint
vignette instead of decorative orbs).

---

## 4. About page — split into tabs with one XAML per section

The single 699-line About view is now a **shell + 3 section views**:

```
Views/About/
  AboutView.xaml                  ← shell: hero + tab strip + content host
  AboutStyles.xaml                ← shared cards / typography / templates
  Sections/
    AboutOverviewSection.xaml     ← "About"   (overview, principles,
                                                compliance, capabilities, privacy)
    ModulesSection.xaml           ← "Modules" (active modules + workflow)
    DeveloperSection.xaml         ← "Developer" (identity, email, tech stack,
                                                version/build, copyright)
```

- **Top segmented tab strip** (`About · Modules · Developer`) bound to the
  new `AboutViewModel.SelectedTabIndex`; the content grid switches sections
  with `DataTrigger`s.
- Each section is a standalone `UserControl` — edit one section without
  touching the others.
- The email contact handler moved into `DeveloperSection.xaml.cs`, so that
  section is fully self-contained.
- `AboutViewModel` gained `Tabs` + `SelectedTabIndex` (now raises
  `PropertyChanged`); all existing bindings are unchanged.

---

## Files changed

| Area | Files |
|---|---|
| Pill removal | `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`, `Themes/Styles/DesignTokens.xaml`, `Services/ThemeManager.cs` |
| Splash | `Views/Splash/SplashView.xaml` |
| Login | `Views/Login/AuthGlassStyles.xaml`, `Views/Login/LoginPanel.xaml` |
| About | `Views/About/AboutView.xaml(.cs)`, `Views/About/AboutStyles.xaml` (new), `Views/About/Sections/*` (3 new), `ViewModels/About/AboutViewModel.cs` |

## Verify

```bash
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release
```

Manual check: launch → splash is a flat industrial stage (no orbs); sign in
→ steel card; open About → hero + three tabs switch between the sections.
