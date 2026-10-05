# INDUSTRIAL REFORM — strict industrial pattern app-wide (2026-09-13)

**Owner ask:** "complete professional industrial redesign upgrade … reform
strictness industrial pattern. Industrial workflow chain, all UI/UX redesign."

This supersedes the 2026-09-12 "4-family consolidation" (which had retired
`Normal (Industrial)` and made the cream SaaS `Normal` the default). The
industrial look is now the app-wide default again, and the token system is
enforced strictly across every functional view.

## 1. The four visual families (unchanged count, industrial default)

| Order | Family | Engine identity | Display |
|---|---|---|---|
| 1 (default) | **Industrial** | `Normal (Industrial)` | Industrial |
| 2 | Monet | `Monet` | Monet |
| 3 | iPhone 17 Pro Max | `Crystal Fluid` | iPhone 17 Pro Max |
| 4 | iOS 26 | `iOS 26` | iOS 26 |

The cream SaaS `Normal` is folded into Industrial: `Normalize("Normal")` →
`NormalIndustrial`, and every legacy industrial name (`Professional`,
`Industrial`, `Industrial White/Grey/Dark`) resolves back to it. Unknown
saved values also reset to Industrial. The switcher stays at exactly **4**.

The industrial family itself (`ApplyIndustrial`, `VisualThemeEngine.Part3`)
was already fully authored and is unchanged in behaviour:

- flat slate stage `#F8FAFC` light / `#0A1326` dark (no gradient),
- solid opaque white cards, defined slate card edge `#CBD5E1` /
  `#475569`, navy ink `#0F172A`, blue CTA `#0369A1`,
- dense radii — cards 8 / large 12 / control 6,
- crisp navy-tinted shadows.

Because every view consumes the shared `DynamicResource` token surface,
restoring this family as the default re-skins the whole app (list pages,
dashboards, reports, production chain) without per-file edits.

## 2. Strictness sweep — what changed

Two mechanical sweeps (functional views only; decorative/brand surfaces
excluded per `DESIGN_SYSTEM.md`):

| Violation | Count fixed | Mapping |
|---|---|---|
| hard-coded `CornerRadius` | 38 | chips/buttons/tiles → `ControlCornerRadius`; badges → `Radius.XSmall` / `Radius.Small` |
| hard-coded `FontSize` (< 30 DIP) | 35 | `9/10/11→TextXSmall`, `15→TextSection`, `16→TextSubhead`, `18→TextTitle`, `22→TextHeadline`, `26→TextDisplay` |

Files touched (26): Production (8 stage views + `ProductionPipelineBar`,
`ProductionUnitDetailControl`, `ProductionUnitDetailTemplates`),
Optimization (`OptimizationView` + 5 tabs), Reports
(Company/Customer/Executive/GlassSpec/Pi/Profitability/Project/Salesman +
OrderReport preview), and the PI Step 7 preview badge.

Result: zero hard-coded `FontSize` < 30 remains in functional views, and the
only remaining literal radii are the two one-sided `12,12,0,0` table-header
corners in the Statement Generator preview (allowed by the design system:
"one-sided corners use the matching literal").

## 3. Decisions (documented as permitted)

1. **Industrial replaces cream `Normal` as the default** — the user's
   "strictness industrial pattern" is the shipped look. The cream SaaS
   canvas is no longer selectable and folds into Industrial.
2. **Companions kept:** Monet and the two iPhone families (iPhone 17 Pro
   Max, iOS 26) — unchanged from the consolidation.
3. **Decorative/brand surfaces left as designed:** Splash aurora, Login
   art, About hero glass, and the printable-invoice paper surface keep
   their authored values (they are brand art, not ERP chrome, and are
   rendered before/differently from the theme family).
4. **Semantic data-viz hex left as-is:** the Step 9 profit/loss green/red
   and utilisation-legend tints are universal profit/loss semantics, not
   UI chrome.

## 4. Files changed

| Area | Change |
|---|---|
| `Services/Theme/VisualThemeEngine.Part3.cs` | `Normalize` folds `Normal` + legacy industrial names → `NormalIndustrial`; unknown → `NormalIndustrial` |
| `Services/Theme/VisualThemeEngine.Part1.cs` | `FamilyDisplayName(NormalIndustrial)` → `"Industrial"` |
| `Services/Theme/BuiltinWallpaperScenes.cs` | catalogue → Industrial ("Studio"), Monet, Crystal Fluid, iOS 26; default scene family → `NormalIndustrial` |
| `Services/Theme/WallpaperOrchestrator.cs` | theme-linked scene fallback → `NormalIndustrial` |
| `ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs` | `VisualThemeOptions` → 4 rows (Industrial first); fallback → `NormalIndustrial` |
| `ViewModels/Settings/SettingsViewModel.Helpers.cs` | startup re-apply guard default → `NormalIndustrial` |
| `Services/AI/AIConfig/AppConfig.cs` | default `VisualTheme` → `"Normal (Industrial)"` |
| `Services/AI/AIConfig/ConfigService.cs` | one-time migration folds saved `"Normal"` → `"Normal (Industrial)"` |
| `App.StartupCore.cs` | startup fallback → `NormalIndustrial` |
| `ViewModels/Settings/SettingsViewModel.Typography.cs` | factory reset → `NormalIndustrial` |
| `Tests/Theme/ThemeRegistryParityTests.cs` | `AllFamilies` + mode-pair theory → industrial catalogue |
| `Tests/Agent/LiquidGlassThemeSourceTests.cs` | source assertions → industrial default + 4-family catalogue |
| `Themes/Languages/*.xaml` (15) | helper strings → "Industrial, Monet, iOS 26 or iPhone 17 Pro Max" |
| 26 functional `Views/**/*.xaml` | radii + font-size tokenization (see §2) |

## 5. Verification (static — no .NET SDK in this sandbox)

- Family catalogue consistent across switcher / scene catalogue / parity test.
- All 252 XAML files re-parse cleanly.
- Every referenced token (`ControlCornerRadius`, `Radius.XSmall`,
  `Radius.Small`, `TextXSmall..TextDisplay`) is defined in the merged
  dictionaries.
- Windows build + visual smoke still required on the Windows box.

## 6. Not done / deferred

- iOS 26 token-coverage extension (the `Tint*` / `Glass*` / `Liquid*`
  control set it does not yet publish) — separate workstream, not part of
  this industrial pass.
- QuestPDF `conflicting size constraints` export repro (see
  `PdfExportEngine` `EnableDebugging` flag).
