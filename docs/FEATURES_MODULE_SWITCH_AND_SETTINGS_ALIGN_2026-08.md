# Features Module Switch + Settings Button Alignment (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "Revert this. I said to do settings section not job order and read
> properly i said to full width of in setting all option right now they
> are left right center etc align which is looking ugly. And features
> section that in tab switch as per module. Do you understand now"

Clarified intent:
1. **Revert the Job Order tab-switch change** — that was a misread; the
   "tab switch" belonged in Settings, not Job Order.
2. **Settings ► Features** must be a **per-module tab switch** (show one
   module's toggles at a time), not all five modules stacked.
3. **All settings option buttons** were left/right/center aligned and
   looked ugly — make them fill full width consistently.

---

## 2. Reverted the Job Order change

The Job Order detail is back to the **unified page** (editable production
form at the top, then the source-invoice identity strip, then the GLASS
PANELS grid). No tab strip is rendered, and the Demo narration again
describes the production section as the first thing on the sheet.

---

## 3. Settings ► Features — per-module tab switch

The Features page now has a **module tab strip** (Proforma Invoice /
Inquiry / Job Order / Delivery Note / Tax Invoice) above the content. Only
the selected module's field-toggles are shown:

- `SettingsViewModel` gained `FeaturesModule` (default `"Proforma"`),
  `SwitchFeaturesModuleCommand`, and one `IsXFeaturesModule` flag per
  module.
- Each module's content panel has a `Visibility` binding keyed to the
  matching flag (anchored via `SettingsFeaturesRoot` element-name), so
  switching tabs swaps the content live with no restart.
- The five inter-module separators now live inside each module panel so a
  hidden module's divider doesn't leave a stray line.
- The Session Security card stays always visible at the top.

This mirrors the Settings shell navigation pattern already used by the
left nav rail.

---

## 4. All settings buttons fill full width

Converted every horizontally-aligned button row (that was left-cramped or
right/center-scattered) to a **full-width row**:

- **Features** — all six bulk-action rows now stretch evenly.
- **AI & Agent** — privacy TOGGLE buttons, STORE/REMOVE KEY, EXPORT/IMPORT,
  CLEAR MEMORY/KNOWLEDGE now stretch.
- **Demo Center** — record-session, workflow-generate and isolation action
  rows now fill the card (the record row uses a star/auto Grid so a hidden
  button collapses cleanly).
- **Data & Backup / Updates / Voice / AI** — already stretched (kept).
- Added reusable `SettingsActionRowStyle` (UniformGrid, 1 row) and
  `SettingsActionButtonStyle` (stretch) to `SettingsSectionStyles.xaml`.

Toggle switches and numeric readouts (which are intentionally
left/right-aligned) were left as-is; only action buttons were changed.

---

## 5. Verified (no `dotnet` build here)

- All modified XAML parses; no element sets the same property twice.
- Features still exposes all 5 module DataContext scopes + 47 toggles.
- Added `Features_section_uses_a_per_module_tab_switch` (SettingsRedesignTests)
  and `Tests/Settings/SettingsModuleSwitchTests.cs` (type-shape + XAML
  wiring lock) — both avoid constructing the heavy VM (which pulls the WPF
  dispatcher + config file) to stay hermetic.
- All command/feature-flag bindings preserved; C# balanced.

---

## 6. Notes for you (non-coder summary)

- Open **Settings ► Features** and you'll see a **tab strip** for each
  module. Click a module to see only its field toggles — no more one long
  page of every module.
- Every **Settings** action button now stretches to fill its card width, so
  nothing clusters on the left with empty space on the right.
