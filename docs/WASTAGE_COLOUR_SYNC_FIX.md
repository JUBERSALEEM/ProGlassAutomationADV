# Step 6 Wastage Charges — Colour Import & Spec-Side Sync Fix

**Date:** 2026-09-04 · **Scope:** Steps 4 → 5 → 6 (wastage section) · **Severity:** data-loss + wrong-charge class

## Reported symptoms

1. Wastage charge section has syncing issues across Steps 4/5/6.
2. Colour names are never imported automatically — the COLOUR box stays blank even when Steps 4/5 already know the pane colours.
3. With two specs, the operator needs: **select colour → then pick Spec 1 W1/H1 or W2/H2, Spec 2 W1/H1 or W2/H2 as the SQM source**. No such colour-driven targeting existed.

## Root-cause analysis (senior audit)

| # | Finding | Root cause | File |
|---|---------|-----------|------|
| F1 | Row colour never auto-imports | `WastageRow.ColorName` was write-only from the UI. `SyncSourcesForRow` resolved pane colours into the sources but never propagated them to the row. Exports printed `Wastage — ` (blank). | `Models/…/Wastages/WastageRow.cs` |
| F2 | No colour pick-list despite known colours | Detected colours lived only as popup text; nothing aggregated them per row. | `WastageRow.cs`, `WastagesSectionViewModel.cs` |
| F3 | Garbage row names in advanced mode | Row identity was scraped from the human display string via `ExtractColorKey(DisplayName)`. Unresolved colours produced rows literally named **"Outer glass wastage"**. | `WastagesSectionViewModel.cs`, `OtherChargeAutomationRules.cs` |
| F4 | **Silent data loss** — operator rows destroyed | `ApplyAdvancedSuggestions` ran `ClearWastageRows()` on **every Step 5 keystroke** (W1/H1/W2/H2/Qty edits via `RefreshWastageSelectionSurface`). A row added with **+ ADD ROW** vanished seconds later. | `WastagesSectionViewModel.cs` |
| F5 | Wrong-charge trap with 2+ specs | Flat source list: no per-spec headers, no match/mismatch indication of pane colour vs row colour. | `SpecGroupColorSide.cs`, `WastagesSectionView.xaml` |

## Implementation

### 1. Structured colour transport (kills string scraping)
- `AutoWastageSuggestion.ColorName` — the rules engine now carries colour as **data** for every pane suggestion (SGU/DGU/LAM/DGU+LAM incl. all three hybrid panes).
- `WastagesSectionViewModel.SuggestionColour()` prefers the structured colour; `ExtractColorKey` is hardened with a sentinel set so "Outer/Inner/Lam … glass wastage" labels can **never** become colour names again.

### 2. Row colour auto-import (F1)
- `WastageRow.SyncColourStateFromSources()` — when the checked panes share exactly one detected colour, a **blank or auto-assigned** row inherits it immediately. Works on first ADD ROW, on reticks, and on spec re-syncs.
- Ownership model: typing a colour, picking from the list, restoring from snapshot, or advanced assignment marks the colour operator-owned (`_colorManuallyEdited`) — later source churn **never stomps** it. Clearing the box hands the row back to auto-import.
- An auto label that becomes ambiguous (mixed-colour selection) is **withdrawn** rather than left accusing one colour while charging another; operator labels are never withdrawn.

### 3. Detected-colour picker with per-spec pane targeting (F2, the requested workflow)
- `WastageRow.AvailableColors` — every distinct pane colour across all specs, in spec/side order.
- `SelectedAvailableColor` — picking a colour (a) names the row, (b) ticks every pane of that colour across **all specs** (Spec 1 W1/H1 or W2/H2, Spec 2 W1/H1 or W2/H2, …), and (c) unticks panes detected in a different colour. Colourless panes stay operator-controlled.
- The picker doubles as an indicator: it always shows the row colour when that colour is a detected one (`ResyncColourPickerSelection`).

### 4. Manual rows survive advanced refresh (F4, data loss)
- `WastageRow.IsManuallyAdded` set by ADD ROW; **persisted** in `WastageRowSnapshot` + mapper (legacy invoices default false → historical behaviour).
- `ApplyAdvancedSuggestions` now partitions rows: generated rows are rebuilt (rates/SHEET QTY preserved by colour+side key as before); **manual rows are never removed** — only their source lists re-sync against the current spec set.

### 5. Wrong-charge defenses in the source picker (F5)
- Per-spec headers (`SPEC 1`, `SPEC 2`, …) via `IsFirstOfSpec`/`SpecHeaderText` stamped by the section VM.
- Row colour is pushed into every pane (`SpecGroupColorSide.RowColorName`): **✓** badge when the pane colour matches the row, **⚠** when it contradicts it, *"colour not detected"* note when detection failed.
- `WastageRow.HasColorConflict` shows an inline warning under the colour box whenever the checked panes mix colours.

## Files changed

- `Models/ProformaInvoice/Step6OtherCharges/Wastages/WastageRow.cs` — colour identity pipeline, pick-list, conflict flag, `IsManuallyAdded`.
- `Models/ProformaInvoice/Step6OtherCharges/Wastages/SpecGroupColorSide.cs` — row-colour badges, spec headers, `HasColorIdentity`.
- `ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs` — manual-row preservation, structured colour rows, header stamping, sentinel-hardened `ExtractColorKey`.
- `Services/ProformaInvoice/OtherChargeAutomationRules.cs` — `AutoWastageSuggestion.ColorName` populated for all five suggestion sites.
- `Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs` + `Services/…/ProformaInvoiceMapper.Part1.cs` — `IsManuallyAdded` persistence.
- `Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml` — detected-colour combo, conflict warning, spec headers + colour badges in the source popup.
- `Themes/Languages/en-US.xaml` — 5 new strings (fallback locale covers all 15 dictionaries).
- `Tests/Agent/WastageColorSyncTests.cs` — 13 regression tests (model-level + end-to-end wiring locks).

## Behaviour now

1. **ADD ROW** → colours detected upstream fill the box automatically (single-colour selection) — nothing to type.
2. Two specs on screen → the row offers a **DETECTED COLOUR** dropdown listing e.g. *HD Grey / Clear / Bronze / Green*; picking one instantly ticks exactly that colour's panes per spec side and names the row.
3. The source popup shows `SPEC 1` / `SPEC 2` sections, each pane with side (W1/H1 vs W2/H2), colour, SQM — plus ✓/⚠ badges against the row colour.
4. Mixed-colour selections raise a visible warning instead of silently mis-charging.
5. Manual rows, typed colours, rates and SHEET QTY survive Step 5 edits, advanced refreshes, and save/reload cycles.
