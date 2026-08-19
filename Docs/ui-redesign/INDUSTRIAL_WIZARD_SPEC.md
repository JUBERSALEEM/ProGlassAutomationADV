# Proforma Invoice — industrial redesign

**Status:** Approved PNG boards recut into live XAML (presentation layer). Bindings, commands, AutomationIds, snapshot JSON and repositories were not rewritten.

The PNG mockups in this folder are the visual source of truth for **placement**, field height (34 px), grid row height (40 px), chips (34 px), apply button (40 px), and 3D glass composition. Garbled sample text in the PNGs is image-model noise (`LEFT 40%`, `40px`, `48px`, factory drawing). Theme colors stay `DynamicResource` so Light / Dark / Monet still work.

Theme rule: **zero hardcoded brand hex in views** except Step 7 paper (`#FFFFFF`). Canvas / surface / outline / text / accent / success / danger all come from `DynamicResource`.

Honest ceiling: layout can match the boards (~75–80%). Pixel-perfect PNG / photo-real 3D is not possible in WPF.

---

## Shared chrome (every step)

```
[ LIST ]  1 Company  2 Client  3 Project  4 Glass  5 Grid  6 Charges  7 Preview  8 Files  9 P&L
────────────────────────────────────────────────────────────────────────────────────────────
                              ONE workbench (the step)
────────────────────────────────────────────────────────────────────────────────────────────
[ Back ]                         STEP  n  OF  9                         [ Continue ]
```

- Rail height **40 px**. Outlined 32 px pills. Active = `AccentBrush` fill + `OnAccentForegroundBrush`. Completed = success tick. Inactive = outline only.
- Steps 1–2 = **two** work-surface cards. Steps 3–9 = one workbench (Step 7 splits steel | paper).
- Footer = 48 px bar. Text-only Back / Continue.
- Input height **34 px**. Labels 11 px SemiBold `SecondaryForegroundBrush`.
- Corner `CardCornerRadius`. Border `OutlineBrush`.

### Theme mapping

| Role | Light | Dark | Monet |
|---|---|---|---|
| Canvas | `WindowBackgroundBrush` | dark steel | wallpaper shows through |
| Surface | `ModuleBackgroundBrush` | raised steel | translucent surface |
| Line | `OutlineBrush` / `DividerBrush` | muted | same tokens |
| Accent | `AccentBrush` (user preset) | same key, brighter | Monet-derived accent |
| Paper (Step 7 only) | `#FFFFFF` | `#FFFFFF` | `#FFFFFF` — never themed |

---

## Per-step structure

### 1 Company
Two cards: **form 2/3** | **saved companies 1/3**. LETTERHEAD zone, then DOCUMENT IDENTITY with Skip to grid. Saved list is a slim table (name · TRN · city · badge).

### 2 Client
Two cards. 3-column identity grid + tall billing address (120 px). Saved clients table (name · TRN · city · badge).

### 3 Project
One workbench. **Stacked full-width fields** (identifier, contract, reference, LPO, site). Slim project list filtered by Step 2. Ignore PNG vertical-stepper leak.

### 4 Glass (SGU / DGU / LAM / DGU+LAM)
- Type switch: one 34 px chip row — SGU | DGU | LAM | DGU+LAM.
- Live `GlassAssemblyShowcase` inside a framed well.
- Sticky apply bar: spec groups + rate AED /sqm + APPLY RATE.

| Type | Composition |
|---|---|
| SGU | Wide hero pane + two metric strips |
| DGU | 40% 3D IGU · 60% Outer / Air spacer / Inner / costing |
| LAM | Identity bands · exploded stack · labeled editors · six costing tiles |
| DGU+LAM | Cutaway · numbered five-layer sandwich · yield/margin/cut/temper rail |

### 5 Grid
Spec tabs. Toolbar: glass type / spec / base / surcharge. Table: `# REF W1 H1 W2 H2 QTY SQM LM RATE VALUE`. Footer pcs · SQM · AED. GENERATE / + ADD ROW stay as quiet chips (not on the PNG).

### 6 Charges
Three table bands on one surface: Predefined · Wastage · Custom. KPI strip. Combined footer. Ignore PNG factory drawing.

### 7 Preview
Split 1.15 / 0.85: left steel (summary, discount, VAT, terms checklist, **SAVE / EXPORT at bottom**). Right paper-white print proof.

### 8 Files
Centered card. TYPE | NAME | SIZE | ADDED | ACTIONS. `+ IMAGE` `+ PDF`. Ignore PNG `40px` / `48px` labels.

### 9 P&L
One workbench. KPI strip (Gross | Net | Glass | ASP | Profit). Spec card with layers in **build order**. Rate-check row.

---

## What was not changed

Print paper ink, snapshot JSON, commands, repositories, calculations, navigation, validation.
