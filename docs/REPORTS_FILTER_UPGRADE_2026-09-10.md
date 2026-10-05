# Reports Hub — Inventory-Grade Filter Panels (2026-09-10)

## Senior analysis
The inventory (Sheet Store) page defines the app's best filter UX: a
padded rounded card, labeled 40px fields, accent-highlighted SORT,
an `OutlinedPillButton` clear, and a full-width search band with an
accent magnifier and borderless input. The report hub pages used a
flat 12px divider strip with 34px inputs and a small search box buried
among the fields — visually inconsistent and cramped.

## Implementation (style system, not per-view patches)
1. **New scoped shared styles** in `Themes/Styles/ListPageStyles.xaml`:
   `ReportFilterCardStyle` (card: ModuleBackground + Outline border +
   CardCornerRadius + 20,16 padding), `ReportSearchBandStyle` (Outline
   border + MenuHover background + rounded), `ReportSearchInputStyle`
   (borderless transparent 40px input). The shared `ModuleList*` styles
   are untouched — other modules keep their look.
2. **All 12 report views** restructured to the inventory vocabulary:
   filter card style; fields on row 0; full-width search band on row 1
   (in the 9 views whose service honors SearchText — Delivery,
   Profitability and VAT deliberately don't get a fake search band);
   all combos → `ModernComboBoxStyle` at 40px (46 combos across the
   hub); date pickers at 40px; Clear → `OutlinedPillButton` with an
   E711 glyph; SORT label accent-highlighted.
3. **Buttons**: `ReportToolbarOutlinedButton`, `ReportExportOutlinedButton`
   and `ReportExportPrimaryButton` raised 32→40px so CSV / Excel / PDF /
   Share / Refresh align with the fields on one line (report-scoped keys —
   no blast radius outside the hub).

## Verification
238-file XAML semantic sweep clean; all changed XAML XML-parsed;
asserted battery (card ×12, single RowDefinitions per card, search band
exactly in the 9 wired views, zero legacy combo/datepick styles, Clear
pill ×12, button heights, inventory page untouched).
