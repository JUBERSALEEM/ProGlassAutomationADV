# Job Order Tab Switch Restore + Settings Button Full-Width (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "I said to keep tab in separate button tab switch. Also setting all
> button are not properly align make them to cover full width. Left side
> hald align looking ugly. Do a cross check and properly fix all section
> filling properly width in all setting pages do a check and update
> properly."

Two things:

1. **Keep the Job Order detail as separate button tabs** (Overview /
   Production). A previous pass had merged them into one continuous page;
   the user wanted the tab switch back.
2. **Settings buttons were left-aligned in full-width cards** and left a
   big empty gap on the right. Make them stretch to cover the full width
   consistently across all settings pages.

---

## 2. Job Order — restored the Overview / Production tab switch

`JobOrderDetailView` now has a **separate button tab strip**:

- **OVERVIEW** tab → source-invoice identity strip (company / client /
  project / value) + the **GLASS PANELS** production-ticket grid.
- **PRODUCTION** tab → the editable, auto-compact production panel.

The **header action bar** stays on top and carries all document actions
(BACK, CREATE DN, MASTER DN, PDF, EXCEL, CSV, SAVE) plus the invoice value.
The tabs are the two-button pattern (active = filled accent, inactive =
outlined), switched by `IsOverviewTab` / `IsProductionTab`.

- `JoOverviewSection` no longer embeds the production panel (the two
  sections are now distinct tabs).
- The Demo Hub still calls `SwitchDetailTabCommand("Production")`, so the
  narration timing is unchanged; the workflow comment and the production
  narration were updated to describe the tab again.

---

## 3. Settings — buttons now fill full width

All multi-button "action rows" inside full-width cards previously used a
horizontal `StackPanel`, which left-aligned the buttons and left an empty
gap on the right. Fixed across every settings page:

- **SettingsFeaturesSection** — all six bulk-action rows (Session Security,
  Proforma / Inquiry / Job Order / Delivery Note / Tax Invoice) now use a
  `UniformGrid` so the "Enable all / Hide all optional / Reset defaults"
  buttons stretch evenly across the whole card.
- **SettingsDataSection** — "Load sample data / Clear all data" and
  "Refresh / Rebuild databases" rows now stretch full width.
- **SettingsUpdatesSection** — "Check for updates / Download update" now
  filled so the primary button spans the card (`*` column that collapses
  cleanly when Download is hidden).
- **SettingsVoiceSection** — the model action bar (Download / Cancel /
  Delete) now spans the card with the primary button in a `*` column.
- **SettingsAiSection** — the provider "SAVE & TEST" buttons and the
  "Remove keys / Clear memory" buttons now stretch to fill their tiles.
- Added reusable `SettingsActionRowStyle` (UniformGrid, 1 row) and
  `SettingsActionButtonStyle` (stretch) to `SettingsSectionStyles.xaml`.

State-conditional rows (Voice, Updates) use a star/auto `Grid` so hiding a
button collapses its auto column to zero and the remaining button fills the
width — no leftover gap.

---

## 4. Verified (no `dotnet` build here)

- All modified XAML parses cleanly; no element sets the same property
  twice (the XLS0501/MC3024 guard).
- All `EnableAllCommand` / `DisableAllOptionalCommand` /
  `ResetToDefaultsCommand` bindings preserved in the Features page.
- All Data / Updates / Voice command bindings preserved.
- All five DataContext scopes + 47 feature toggles intact.
- The Job Order tab bindings (`SwitchDetailTabCommand`, `IsOverviewTab`,
  `IsProductionTab`) and all 7 header action commands preserved.
- C# (workflow + tests) balanced; the production narration beat is 645
  chars (> the 200 minimum).
- Updated `Tests/JobOrder/JobOrderProductionRedesignTests.cs` to lock the
  restored separate-tab layout.

---

## 5. Notes for you (non-coder summary)

- Open a **Job Order** and you'll see two tab buttons — **Overview** and
  **Production** — exactly as a proper module tab switch. All the action
  buttons (Create DN / PDF / Excel / CSV / Save) are in the top bar.
- Every **Settings** page now has its buttons stretching to fill the card
  width, so nothing clusters on the left with empty space on the right.
