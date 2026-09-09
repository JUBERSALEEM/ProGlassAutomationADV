# Phase 11 — Theme and Design-System Consolidation

**Severity: Moderate · Estimate: 3-4 days · Prerequisite: green build**

## Status — parity locked down 2026-09-09; consolidation not attempted

| | |
|---|---|
| Dark/light key parity verified and enforced | **done** — `Tests/Theme/ThemeKeyParityTests.cs` (3 tests) |
| Intra-file duplicate keys scanned | **done** — zero across all 43 theme XAML files |
| Consolidating 43 dictionaries / 4.6 MB | **not attempted — see below** |

### What the scan found: the themes are not broken

Scanned all 43 XAML files under `Themes/` — 3,257 distinct `x:Key` values across
38,869 declarations. Two things worth recording, both of them good news that
nothing was enforcing:

- **Zero keys declared twice inside the same file.** No silent shadowing
  anywhere in the theme tree. A duplicate `x:Key` means the later declaration
  wins and the earlier one is dead, and the symptom is a colour rather than an
  error, so this is worth knowing.
- **`DarkTheme.xaml` and `LightTheme.xaml` declare exactly the same 225 keys.**
  Not approximately — the two sets are identical, with nothing in either that is
  missing from the other.

The 2,751 keys that appear in more than one file are almost entirely the
Dark/Light pair, which is correct by design: the same token must exist in both,
and only one dictionary is merged at a time.

### Why that needed a test

WPF resolves resource keys at **runtime**. Add a brush to `DarkTheme.xaml`,
forget `LightTheme.xaml`, and nothing fails to build, nothing fails to parse,
the app starts normally — the light theme falls through to whatever dictionary
was merged before it, and one control on one screen renders in the dark palette,
only for operators on light.

That had not happened, but it was true only because somebody had been careful.
`ThemeKeyParityTests` now makes it enforced: parity between the two
dictionaries, the token count pinned at 225, and a scan for intra-file
duplicates across the whole theme tree. Both key-set extractions assert they
found more than 100 keys, so a restructured file cannot make the parity test
pass on two empty sets.

Proven to fail: removing `AccentColor` from `LightTheme.xaml` makes the parity
assertion name it, and the file was restored afterwards.

### Why consolidation was not attempted

Merging 43 dictionaries into a smaller set is real work, and its failure mode is
invisible: a shadowed key changes a colour on one screen and fails no build, no
parse and no test that does not already know the expected value. This
repository has already had to retract one claim that colours were preserved
through a migration. Doing that at 4.6 MB of XAML with no compiler and no
running app is how a working product acquires a dozen wrong colours nobody can
trace.

The parity test is the right first step: it locks down the property that
silently breaks, so the consolidation can be done later against a net.


## Current state

- `Themes/` holds `LightTheme.xaml`, `DarkTheme.xaml`, `Themes/Styles/`,
  `Themes/CrystalFluid/`, `Themes/Languages/` — 48 XAML files, 4.6 MB.
- `Styles/` holds a further set, 152 KB.
- `design-system/` holds `proglass-industrial` and `proglass-pi`.
- `Services/Theme/VisualThemeEngine.Part1.cs` is 1,157 lines and drives four
  visual families: Normal, Normal (Industrial), Monet, and Ios-17pm.

## Two claims to verify before doing anything

Earlier notes for this repo record:

1. **~51 self-referencing resource keys in `DashboardStyles.xaml`.** Two
   independent regex passes over all 229 XAML files on 2026-09-09 returned
   **zero**. Either the original count used a different definition or it was
   wrong. **Re-measure with a stated definition before acting.**
2. **A fourth design system (`Crystal*` / `Liquid*` / `Glass*`) alongside three.**
   `Themes/CrystalFluid/` exists, but whether it is a parallel system or a
   layered family was not established.

Both are marked **UNVERIFIED** in the analysis. Establish the facts first — a
consolidation built on a wrong premise deletes working code.

## Steps

### 11.1 — Inventory

Produce `docs/THEME_INVENTORY.md`: every resource dictionary, what it defines,
what merges it, and which visual family can reach it. The existing
`scripts/verify-dashboard.py` already resolves 3,867 keys across 229 files with
zero unresolved references — extend that tooling rather than writing a new
scanner.

### 11.2 — Establish the layering

A design system needs one answer to "where does a colour come from". The
expected layering is:

```
palette tokens  →  semantic tokens  →  component styles  →  views
```

Record which of the four visual families is the product default
(`VisualThemeEngine.NormalIndustrial`, per `App.StartupCore.cs`), and make every
other family a **variation** expressed as overrides, not a parallel dictionary.

### 11.3 — Eliminate duplication

Where two dictionaries define the same semantic token, one wins and the other is
deleted. The failure mode to avoid is a token defined in both `LightTheme.xaml`
and a family dictionary with subtly different values — the merge order then
decides the colour, which is exactly how the `AmberBrush` → `WarningBrush`
missing-key bugs arose elsewhere in this codebase.

### 11.4 — Ratchet

Add the invariants to CI:
- every `DynamicResource` key resolves in every theme
- no key defined twice with different values
- no hex colour literal in a view (already enforced for the seven dashboard
  pages by `verify-dashboard.py` — extend to all views)

## Acceptance criteria

- [ ] `docs/THEME_INVENTORY.md` exists and is accurate.
- [ ] One documented layering, with the default family named.
- [ ] No semantic token defined in two places with different values.
- [ ] The two UNVERIFIED claims are resolved with evidence, in whichever
      direction the evidence points.

## Must not change

- The theme **swap** semantics. Theme switching is a dictionary swap, not a
  merge — merging was tried and produces stale keys.
- `PerformanceProfileService`'s motion tiers, which publish `MotionDuration*`
  and `TargetFps` resources the XAML binds to.
