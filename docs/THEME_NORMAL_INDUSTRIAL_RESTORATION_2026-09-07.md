# Theme — "Normal (Industrial)" restoration + card edge lines (2026-09-07)

**Request:** bring back the August-26 "Normal" (full white + blue tone,
professional industrial layout) as a selectable family **beside** the current
cream SaaS Normal, and give cards a real edge/corner border line (they
currently have none).

---

## 1. Senior analysis — what the August-26 theme actually was

The repo has **no commits dated around 2026-08-26**, so the old look cannot be
recovered from git history. It is reconstructed from two authoritative
sources that both describe the pre-cream industrial look:

| Source | What it proves |
|---|---|
| `design-system/proglass-industrial/MASTER.md` | The industrial token set: background `#F8FAFC`, card `#FFFFFF`, **border `#E2E8F0`** (the card edge line), primary ink `#0F172A` (navy), accent/CTA `#0369A1` (blue), secondary `#334155`, muted `#475569`, density 9/10 (dense dashboard). |
| `docs/THEME_SECTION_ADOPTION_UHD_2026-09-01.md` | The 2026-09-01 pass **replaced** the industrial Normal with the cream canvas (`#F6F1E6→#FEFDFA→#F1EBDE`, hairline `#D8D0C2`) and documented the removed **"Professional (industrial)"** family = steel-blue `#ECF2F9→#D9E5F2` light / `#0A1326→#16233E` dark, "higher contrast + saturation". |

So "the August-26 Normal" = the industrial white/blue surface system, later
deleted on 2026-09-01. Restoring it means reintroducing a **runtime family**
that paints solid white cards, a white-blue stage, navy ink, a blue CTA, and
— its signature — a **defined slate card edge**.

## 2. Why cards currently have no border line

Every card style binds one of two border tokens:

- `BaseCardStyle` → `GlassBorderGradientCrystal` (white, 60–90 % alpha)
- `TaskCardStyle` → `GlassBorderSoft` (white, 18 % alpha)

On a white/cream card fill (`GlassUltraClearBrush`, white 36–80 % alpha) a
white border is invisible. The cream Normal deliberately keeps this soft,
borderless SaaS look (2026-09-01 decision). The industrial family therefore
**overrides those exact tokens** with the slate `#E2E8F0` hairline — no
`CardStyles.xaml` edits, every card (Base/Task/Stat/KPI…) inherits the edge.

## 3. Design decision — a NEW family, never renamed in place

`VisualThemeEngine.Normal = "Normal"` is a **persisted identity**: configs,
profiles and window state store it verbatim. It can never be renamed, and the
cream Normal must stay the default. So the industrial look is a **new
persisted identity**:

```
public const string NormalIndustrial = "Normal (Industrial)";
```

- Display name: `FamilyDisplayName` returns it verbatim (view-only label).
- Mode coupling: `ModePairFor → null` (follows the user's Light/Dark toggle).
- Migration: legacy industrial names (`Professional`, `Industrial`,
  `Industrial White/Grey/Dark`) now `Normalize` back onto `Normal
  (Industrial)` instead of silently falling through to the cream Normal.
  Everything else still resolves to `Normal`.

## 4. Implementation

| File | Change |
|---|---|
| `Services/Theme/VisualThemeEngine.cs` | Header now documents the four families. |
| `VisualThemeEngine.Part1.cs` | `NormalIndustrial` const; `case NormalIndustrial` in both `Apply` and `ApplyModule`; `GlassBorderPrimary`/`GlassBorderBright` added to `ManagedKeys` (they were written by `PublishSectionGlass` but never cleaned up on switch — a latent edge-leak the new family would otherwise trigger). |
| `VisualThemeEngine.Part3.cs` | `ApplyIndustrial` painter + `IndustrialShadow` helper; `Normalize` maps `NormalIndustrial` and the legacy industrial names. |
| `Services/Theme/BuiltinWallpaperScenes.cs` | `NormalIndustrial → "Studio"` scene row (painter already falls through to `Studio`). |
| `ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs` | New selectable option row. |
| `Services/AI/AIConfig/AppConfig.cs` | `VisualTheme` doc updated. |
| `Tests/Theme/ThemeRegistryParityTests.cs` | `AllFamilies` + `ModePairFor` inline data include the new family. |
| `Tests/Agent/LiquidGlassThemeSourceTests.cs` | Pins the new option row. |

### `ApplyIndustrial` palette (Light / Dark)

| Role | Light | Dark |
|---|---|---|
| Window stage | `#F8FAFC → #F1F5F9 → #E7EEF6` | `#0A1326 → #111D38 → #16233E` |
| Card / surface | `#FFFFFF` | `#16233E` |
| **Card edge (corner border line)** | **`#E2E8F0`** (slate-200) | **`#334155`** (slate-700) |
| Edge strong (buttons/inputs) | `#CBD5E1` | `#475569` |
| Ink / secondary / muted | `#0F172A / #475569 / #64748B` | `#F8FAFC / #94A3B8 / #64748B` |
| Hover | `#F1F5F9` | `#1E293B` |
| Radii (dense) | cards 8 / large 12 / control 6 | same |
| Shadows | navy `#0F172A`, crisp | near-black navy |

Surfaces are **solid and opaque** (industrial is not frosted glass). The
family is painted entirely at runtime; **no base theme dictionary changed**,
so the cream Normal and its source-level pins stay byte-identical.

## 5. Verification (static — no .NET SDK in this sandbox)

- `git diff --check` clean.
- All eight touched files brace/paren/bracket balanced.
- Simulated the repo's own source invariants (0 failures):
  - engine still lacks `public const string Ocean/Professional/Industrial/Glass/Glassy`;
  - `ManagedKeys` covers the crash-critical set and still excludes the
    accent-owned outline keys;
  - options list contains the four families, none of the removed identities;
  - scene catalogue covers the new family; `Normalize` and both `Apply`
    switches carry the new case.
- Windows build + `dotnet test` + visual smoke (Settings → Appearance →
  Visual Theme → "Normal (Industrial)") still required on the Windows box.

## 6. What did NOT change

- Cream Normal (default) — palette, hairline, shadows, fallbacks: untouched.
- `LightTheme.xaml` / `DarkTheme.xaml` — untouched (all industrial values are
  runtime overrides, removed again by `ClearOverrides` on family switch).
- Monet, Crystal Fluid (Ios-17pm), wallpaper pipeline, accent system — untouched.
