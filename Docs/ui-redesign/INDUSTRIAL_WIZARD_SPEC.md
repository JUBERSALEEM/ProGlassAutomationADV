# Proforma Invoice — industrial station deck

**Status:** Full XAML recut. Bindings, commands, AutomationIds, snapshot JSON and repositories were not rewritten.

Pattern: **MES station rail + work deck**. Theme tokens stay `DynamicResource` so Light / Dark / Monet still work. Step 4 uses photoreal 3D product plates plus the live `GlassAssemblyShowcase`.

Theme rule: **zero hardcoded brand hex in views** except Step 7 paper (`#FFFFFF`) and the Step 4 product viewport well (`#0B1220` mill floor under the 3D plates). Canvas / surface / outline / text / accent / success / danger all come from `DynamicResource`.

---

## Shared chrome (every station)

```
┌──────────┬──────────────────────────────────────────────┐
│ LIST     │  04          Glass type & specs              │
│ 01 COM   │              Estimators, materials           │
│ 02 CLI   ├──────────────────────────────────────────────┤
│ 03 PRJ   │                                              │
│ 04 GLS ● │           WORK DECK                          │
│ 05 GRD   │                                              │
│ 06 CHG   ├──────────────────────────────────────────────┤
│ 07 PRV   │ [ BACK ]     STATION 04 / 09    [ CONTINUE ] │
│ 08 FIL   │                                              │
│ 09 P&L   │                                              │
└──────────┴──────────────────────────────────────────────┘
```

- Left rail **176 px**. Active station = `AccentBrush` plate. Completed = success tick. Inactive = outline only.
- Work header shows station code + title + subtitle.
- Footer = 52 px command bar. Text-only BACK / CONTINUE.
- Input height **34 px**. Labels 11 px SemiBold `SecondaryForegroundBrush`.
- Corner `CardCornerRadius`. Border `OutlineBrush`.

### Theme mapping

| Role | Light | Dark | Monet |
|---|---|---|---|
| Canvas | `WindowBackgroundBrush` (#F1F5F9 steel) | mill black | wallpaper shows through |
| Surface | `ModuleBackgroundBrush` | raised steel | translucent surface |
| Line | `OutlineBrush` / `DividerBrush` | muted | same tokens |
| Accent | `AccentBrush` (industrial cyan #0369A1) | #38BDF8 | Monet-derived accent |
| Paper (Step 7 only) | `#FFFFFF` | `#FFFFFF` | `#FFFFFF` — never themed |
| 3D well (Step 4) | `#0B1220` | `#0B1220` | mill floor under the plate |

---

## Per-station structure

### 1 Company
Two plates: **identity 2/3** | **vault 1/3**. LETTERHEAD zone, then DOCUMENT IDENTITY with Skip to grid. Saved list is a slim table (name · TRN · city · badge).

### 2 Client
Two plates. 3-column identity grid + tall billing address (120 px). Saved clients table (name · TRN · city · badge).

### 3 Project
One plate. **Stacked full-width fields** (identifier, contract, reference, LPO, site). Slim project vault filtered by Step 2.

### 4 Glass (SGU / DGU / LAM / DGU+LAM)
- Type switch: one 34 px chip row — SGU | DGU | LAM | DGU+LAM.
- **3D product viewport** (photoreal factory plate) + live `GlassAssemblyShowcase` overlay that follows colour / thickness.
- Sticky apply bar: spec groups + rate AED /sqm + APPLY RATE.

| Type | Composition |
|---|---|
| SGU | 3D single-lite plate · live build · two metric rails |
| DGU | 3D IGU cutaway · Outer / Air spacer / Inner / costing |
| LAM | 3D exploded stack · numbered editors · six costing tiles |
| DGU+LAM | 3D hybrid cutaway · numbered five-layer sandwich · yield/margin/cut/temper rail |

Assets: `Resources/Glass3D/{sgu,dgu,lam,dgulam}-3d.png`.

### 5 Grid
Spec tabs. Toolbar: glass type / spec / base / surcharge. Table: `# REF W1 H1 W2 H2 QTY SQM LM RATE VALUE`. Footer pcs · SQM · AED.

### 6 Charges
Three table bands on one plate: Predefined · Wastage · Custom. KPI strip. Combined footer.

### 7 Preview
Split 1.15 / 0.85: left steel (summary, discount, VAT, terms checklist, **SAVE / EXPORT at bottom**). Right paper-white print proof.

### 8 Files
Full-bleed vault plate. TYPE | NAME | SIZE | ADDED | ACTIONS. `+ IMAGE` `+ PDF`.

### 9 P&L
One plate. KPI strip (Gross | Net | Glass | ASP | Profit). Spec card with layers in **build order**. Rate-check row.

---

## What was not changed

Print paper ink, snapshot JSON, commands, repositories, calculations, navigation, validation.
