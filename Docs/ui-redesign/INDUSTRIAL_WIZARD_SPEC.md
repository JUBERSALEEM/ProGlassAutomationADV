# Proforma Invoice — industrial redesign

**Status:** Steps 1–9 rebuilt to the approved mockup compositions (presentation layer). Bindings, commands, AutomationIds, snapshot JSON and repositories were not rewritten.

The PNG mockups in this folder are the visual source of truth for placement, field height (34 px), grid row height (40 px), chips (34 px), apply button (40 px), and 3D glass drawings. Garbled sample text in the PNGs is image-model noise. Theme colors stay `DynamicResource` so Light / Dark / Monet still work.

Theme rule: **zero hardcoded brand hex in views** except Step 7 paper (`#FFFFFF`). Canvas / surface / outline / text / accent / success / danger all come from `DynamicResource`. Light, Dark and Monet re-skin the same layout.

---

## Shared chrome (every step)

```
[ LIST ]  1 Company  2 Client  3 Project  4 Glass  5 Grid  6 Charges  7 Preview  8 Files  9 P&L
────────────────────────────────────────────────────────────────────────────────────────────
                              ONE workbench (the step)
────────────────────────────────────────────────────────────────────────────────────────────
[ Back ]                         STEP  n  OF  9                         [ Continue → ]
```

- Rail height **32 px**. Number box 16×16. Label 10 px SemiBold. Active = `AccentBrush` fill + `OnAccentForegroundBrush`. Completed = `SuccessBrush` tick. Inactive = outline only.
- Content = **one** `PiWorkSurfaceStyle` surface. No nested stripe cards.
- Footer = 48 px bar, not a second hero card.
- Input height `DensityInputHeight`. Labels 10 px uppercase `SecondaryForegroundBrush`.
- Corner `CardCornerRadius`. Border `OutlineBrush`.
- Accent is a **keyline or one primary button**, never a purple page.

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
Two columns on **one** workbench: **form 2/3** | **saved companies 1/3**. LETTERHEAD then DOCUMENT IDENTITY. Slim rows, not cards. Skip-to-grid is a text link in the letterhead header.

### 2 Client
Same two-column pattern. 3-column identity grid + tall billing address.

### 3 Project
Same pattern. Slim project list **filtered by Step 2 client**. Filter is an inline line, not a banner card.

### 4 Glass (SGU / DGU / LAM / DGU+LAM)
- Type switch: **one 32 px chip row** — SGU | DGU | LAM | DGU+LAM.
- Live `GlassAssemblyShowcase` (manufacturing drawing, not a placeholder image).
- Product is a **layer strip, always expanded**:

| Type | Composition |
|---|---|
| SGU | Wide hero pane + two metric strips |
| DGU | Outer · Air spacer · Inner · costing (full-width rows) |
| LAM | Exploded stack centre · layer identity left · editors right · six costing tiles |
| DGU+LAM | Cutaway left · numbered five-layer sandwich · costing rail |

- Live spec name = one monospace line.
- Sticky apply bar: target-group chips + rate AED /sqm + APPLY RATE.

### 5 Grid
Spec tabs on top. Toolbar: glass type / spec / base / surcharge. One table: `# REF W1 H1 W2 H2 QTY SQM LM RATE VALUE`. Sticky subtotal. Slim grand-total strip. No imported-price card stack.

### 6 Charges
Three **table bands** on one surface: Predefined · Wastage · Custom. One KPI strip. Combined footer.

### 7 Preview
Split: left steel controls (summary form, discount, VAT, terms checklist, save/export). Right **paper-white** print proof.

### 8 Files
One file table: TYPE | NAME | SIZE | ADDED | ACTIONS. `+ IMAGE` `+ PDF`.

### 9 P&L
One workbench. KPI strip (Gross | Net | Glass | ASP | Profit). Layers in **build order**. Rate-check row. Unpriced panes stay visible.

---

## What was not changed

Print paper ink, snapshot JSON, commands, repositories, calculations, navigation, validation.
