# ProGlass UI/UX — index (2026-09-13)

This folder is the **full UI/UX roadmap**. It does **not** add shop-floor MES gadgets (racks, hold/scrap, pairing boards, Andon kiosks). It upgrades **what already exists**: one card language, one button language, one type scale, one colour system, and a **shared frosted module skin**.

| File | What it covers |
|---|---|
| `01-PRINCIPLES.md` | Estimator product rules. What “industrial quality” means here. |
| `02-FROST-CARDS.md` | White-on-white bug, blur 1–100, frost colour plates, WPF constraint. |
| `03-MODULE-INVENTORY.md` | Every module surface that must use the shared skin. |
| `04-BUTTONS-TYPE-COLOR.md` | Global pills, fonts, semantic colours. No per-module clones. |
| `05-PHASES.md` | Ordered work. Landed vs next. Honest stop conditions. |
| `06-FULL-XAML-CENSUS.md` | Every XAML on disk (264). |

**Engine (authoritative):** `Themes/ProGlassDesignSystem/`, `Themes/Styles/DesignTokens.xaml` (`SurfacePrimaryStyle` / Secondary / Detail), `ThemeManager.ApplySemanticSurfaces`, Appearance in Settings.

**Not in scope unless the owner asks:** Crystal/Liquid live chrome as a fourth family, extra production MES UI, formula/binding/schema changes.
