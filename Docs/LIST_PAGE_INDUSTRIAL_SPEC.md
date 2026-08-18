# Document-list chrome — industrial target (2026-08-18)

Mockups (approve before any view rewrite):

| Module | File |
|---|---|
| Proforma Invoice | `Docs/list-target-proforma.png` |
| Job Order | `Docs/list-target-joborder.png` |
| Delivery Note | `Docs/list-target-delivery.png` |
| Tax Invoice | `Docs/list-target-taxinvoice.png` |

One chrome language. Four modules. Same 12 px tokens already on the Appearance slider.

## Status — APPLIED (2026-08-18)

Chrome only. Bindings, commands, tab `x:Name`s, and right-click handlers
were not rewritten. `tools/xaml-guard.py` clean.

## What changed

Content of the four list `UserControl`s only. **Not** MainWindow / sidebar / splash / printable invoice.

| Now | Target |
|---|---|
| Solid-fill / gradient hero KPI cards | One compact KPI strip inside the list card |
| Fat “Search & Filter” card + indigo hint banner (PI) | One filter row |
| DN/TI fat tab cards | Underline tabs with counts |
| Mixed card-rows vs table | One table: pale header, 48–56 px rows, 3 px left accent on the active row |
| Saturated status blocks | Small tinted pills (`ControlCornerRadius`) |
| Accent-filled table header | Pale `#F1F5F9` / `MenuHoverBrush` header, 10 px bold uppercase |

Bindings, commands, context menus, virtualization stay. No pagination (TI mock footer is visual only). No VM split in this pass.

## Shared primitives to add (once)

In `Themes/Styles/DesignTokens.xaml` (or a new `ListPageStyles.xaml`):

- `ModuleListKpiStripStyle` / `ModuleListKpiItemStyle`
- `ModuleListFilterBarStyle`
- `ModuleListUnderlineTabStyle` (active = 2 px `AccentBrush` underline)
- `ModuleListTableHeaderStyle` (one-sided `12,12,0,0`)
- `ModuleListRowStyle` (hover `MenuHoverBrush`, selected left bar `AccentBrush`)

Then consume from PI → JO → DN → TI, one file per commit. Pilot PI first (furthest from target).

## Module data that must survive

- **PI** — Total / Showing / Value; Search, Status, From, To, Sort; REF, Company, Customer, Project, Status, Grand Total, Date; OPEN + versions; right-click.
- **JO** — Total / In production / Completed / Overdue; Create; Search, Status, Priority, Sort; JO + From-PI chip + overdue chip; Value, Status, Priority; OPEN + HISTORY.
- **DN** — 5 KPIs; Create DN; Search, Status; tabs All / Prepared / Delivered / Pending / Rework / Returned; progress + qty + sqm; OPEN + Master Invoice; FOC / Rework / Return / Invoiced chips.
- **TI** — 5 KPIs; New Invoice; Search, Status; tabs All / Draft / Issued / Paid; % invoiced + invoiced + balance; OPEN + DEL.

## Out of scope

Printable invoice, Splash aurora, Settings accent swatches, Production/Optimization ComingSoon, God-VM splits.
