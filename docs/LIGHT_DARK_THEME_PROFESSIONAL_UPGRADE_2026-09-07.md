# Light/Dark Professional Industrial Theme Upgrade

Date: 2026-09-07
Branch: arena/01a07b84-proglassautomationadv
Instruction: deep audit, list phases, start implementation, no commit/push.

## Senior audit findings

1. Light and Dark theme palettes were technically dynamic, but several colors were from mixed visual eras: warm ivory industrial, liquid glass cyan, old slate, and high-alpha white glass all coexisted. This made module switching feel inconsistent.
2. Dark theme shadow tokens were heavy (`BlurRadius` up to 80 and `ShadowDepth` up to 30). Many modules use `ShadowSoft`/`ShadowElevated`; large blur effects are WPF software-rendering hotspots and contribute to perceived lag.
3. `ThemePipeline.Rebuild()` called `ThemeManager.ApplyTheme()` and then `RebuildFamily()`. `ApplyTheme()` already did accent/outline/card/surface work, while `RebuildFamily()` repeated family/accent/outline/surface work. This created duplicate repaint work on Light/Dark switching.
4. Some theme DropShadowEffect resources were not frozen even though they are shared resources.
5. After the previous dynamic-XAML pass, the remaining risk was not static literals in module XAML but source theme quality, surface consistency and switching pipeline churn.

## Phases

### Phase 1 — Audit palette and runtime switching
Status: Complete

- Inspected Light/Dark color tokens, surface brushes, glass brushes, borders, shadows.
- Inspected ThemeManager and ThemePipeline order.
- Confirmed non-print XAML dynamic audit remains clean.

### Phase 2 — Professional palette normalization
Status: Complete

- Light theme moved to a cleaner professional slate/blue industrial palette:
  - ink: near-slate instead of warm brown-black
  - secondary/muted: consistent slate ladder
  - borders/dividers: blue-slate neutral rhythm
  - surfaces: cleaner white/snow base
  - accent: stable industrial blue
- Dark theme moved to a calmer dark-slate palette:
  - ink: softer off-white
  - surface: deep navy/slate
  - borders: less brown/olive, more steel-slate
  - tints: quieter, less glowing

### Phase 3 — Smooth consistent surface/shadow tokens
Status: Complete

- Reduced oversized shadow blur/depth values.
- Froze shared DropShadowEffect resources for better reuse.
- Kept shadows present but less expensive and more professional.
- Preserved existing semantic surface system and Appearance controls.

### Phase 4 — Theme-switching lag reduction
Status: Complete

- Added a deferred post-processing path to `ThemeManager.ApplyTheme(...)`.
- `ThemePipeline.Rebuild()` now swaps Light/Dark dictionaries without doing duplicate accent/outline/surface processing.
- The full pipeline now applies family/accent/outline/surfaces once, then raises ThemeChanged once.
- Direct callers of `ThemeManager.ApplyTheme(theme)` keep the existing behavior.

### Phase 5 — Validation
Status: Complete except build runtime, blocked by environment

- XAML parse all 225 files: PASS
- non-print dynamic audit: PASS
- `git diff --check`: PASS
- edited C# brace balance: PASS
- `dotnet build`: blocked, SDK missing

## Files changed in this upgrade

- `Themes/LightTheme.xaml`
- `Themes/DarkTheme.xaml`
- `Services/ThemeManager.Part1.cs`
- `Services/ThemeManager.Part2.cs`
- `Services/Theme/ThemePipeline.cs`
- `docs/LIGHT_DARK_THEME_PROFESSIONAL_UPGRADE_2026-09-07.md`

Other already-modified design-system files remain from the prior UI/theme work in this same active task.

## Build result

Attempted:

```bash
dotnet build ProGlassAutomation.csproj --no-restore
```

Result:

```text
/bin/bash: line 1: dotnet: command not found
```

Build status: FAILED/UNAVAILABLE due missing .NET SDK in sandbox.

## Addendum — error fix + second senior audit pass

The owner reported missed areas and errors after the prior pass. I re-audited the generated changes and found concrete issues:

1. Some dynamic color conversions had left `po:Freeze="True"` on Freezable resources (`SolidColorBrush` / `DropShadowEffect`) that now contain `DynamicResource` values. WPF cannot safely freeze resources with dynamic expressions.
2. Several local and foundation shadows were still too heavy for smooth ERP module switching.
3. DropShadowEffect color usage still contained named/static colors in non-theme XAML.
4. Full Light/Dark switching still needed a cleaner post-processing split so ThemePipeline can avoid duplicate work while direct ThemeManager callers keep compatibility.

### Fixes applied

- Removed `po:Freeze="True"` from every Freezable resource block that contains `DynamicResource`.
- Verified the count is now zero:

```text
FREEZE_DYNAMIC: 0
```

- Capped heavy shadows across application/foundation XAML:
  - `BlurRadius <= 24`
  - `ShadowDepth <= 8`
- Replaced non-theme `DropShadowEffect Color="Black"` / static splash shadow colors with dynamic theme resources.
- Revalidated that no heavy shadow remains by the audit threshold:

```text
HEAVY_EFFECTS: 0
```

- Kept Light/Dark source theme resources frozen where they do not contain dynamic expressions.
- Preserved the compatibility behavior of direct `ThemeManager.ApplyTheme(theme)` while allowing `ThemePipeline.Rebuild(...)` to use deferred post-processing and reduce duplicate repaint work.

### Final static validation after error fix pass

```text
XAML_PARSE: PASS
FREEZE_DYNAMIC: 0
HEAVY_EFFECTS: 0
git diff --check: PASS
C# edited-file brace balance: PASS
```

Build remains blocked by missing .NET SDK:

```text
/bin/bash: line 1: dotnet: command not found
```
