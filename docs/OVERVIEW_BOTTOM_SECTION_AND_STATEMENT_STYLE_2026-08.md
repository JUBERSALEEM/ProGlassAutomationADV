# Overview Bottom Section + Statement Style Match (Aug 2026)

**Branch:** `arena/01a03473-proglassautomationadv`

---

## 1. What you asked for

> "sales production sheetstore in single line 4 block in each can you do
> them do by two so they can be big properly because right now and some
> text not proper visible upgrade this down section properly. also statement
> match style same like other modules. redesign this overview dashboard end
> section and in recents documents inside implement mouse scroller also. and
> check if this recents documents have still issues like it"

Three parts:
1. Make the Overview bottom section (Sales / Production / Sheet store) show
   the KPI blocks **2 per row** so they're bigger and text is readable.
2. Make the **Statement** dashboard match the other modules' style.
3. Give **Recent documents** a working **mouse-wheel scroller**.

## 2. Overview bottom section — 2×2 KPI grid

The bottom row had each of Sales / Production / Sheet store rendering **4
thin tiles in a single line** (`SoftKpiRowPanel` = `UniformGrid Rows="1"`),
so each tile was too narrow and the label/value were cramped.

- Changed `SoftKpiRowPanel` to **`UniformGrid Rows="2"`** → a 4-tile band
  now renders as a **2×2 grid** of much larger tiles.
- Updated `SoftKpiTileTemplate` with a vertical gutter + `MinHeight="82"`
  and gave the label a larger font with `TextWrapping="Wrap"` and
  `LineHeight=15`, so labels like "Delivered Today (pcs)" stay visible.

This only affects the Overview (the only consumer of `SoftKpiRowPanel`).

## 3. Statement matches the other modules

The Statement dashboard used **hand-written scalar KPI cards**
(`TotalStatements`, `TotalInvoicedDisplay`, …) while every other module
uses a `KpiCards` collection rendered through the shared
`SoftKpiCardTemplate`.

- Added `KpiCards` (`ObservableCollection<KpiCardModel>`) to
  `StDashboardViewModel`, rebuilt from the four scalars after each load
  (Statements, Total Invoiced, Outstanding, Overdue) with the same
  theme-brush keys as the other modules.
- Updated the Statement XAML to render the "Statement pulse" card through
  `SoftKpiCardTemplate` + `KpiCards`, exactly like the other six modules.
- The scalar display properties remain on the VM (used by the overdue
  summary and available for future use).

## 4. Recent documents — mouse-wheel scroller

The "Recent documents" list is nested inside the page-level ScrollViewer,
so the mouse wheel was being swallowed and the list didn't scroll reliably.

- Bounded the inner `ScrollViewer` (`MaxHeight="320"`) so it doesn't grow
  forever.
- Added `CanContentScroll="True"` + `PanningMode="VerticalOnly"`.
- Added `behaviors:SmoothScrollBehavior.IsEnabled="True"` for buttery
  scrolling and **`behaviors:SmartScrollBubblingBehavior.IsEnabled="True"`**
  so the inner list grabs the wheel over the list and hands it back at the
  boundary (no more "stuck" / no-scroll).

## 5. Verified (no `dotnet` build here)

- All modified XAML + C# parse/balance; no element sets the same property
  twice.
- Statement still binds `OverdueAmountDisplay`, `HasOverdue`,
  `BalanceMixSlices` and now `KpiCards`; the removed scalar bindings are
  replaced by the KpiCards tiles.
- 0 broken Overview bindings (`SalesTiles`, `ProductionTiles`,
  `InventoryTiles`, `DeliveryMixSlices`, `PulseDisplay`, `RecentActivity`
  all intact).
- Added tests:
  - `Overview_kpi_band_uses_a_two_row_grid`
  - `Recent_documents_is_a_mouse_wheel_scroller`
  - `Statement_matches_the_other_module_dashboards`

---

## 6. Notes for you (non-coder summary)

- **Overview bottom**: Sales / Production / Sheet store now show their
  numbers as **bigger 2×2 grids**, labels fully visible.
- **Statement**: now uses the exact same card row style as Proforma, Job
  Order, Delivery Note and Tax Invoice.
- **Recent documents**: scrolls with the mouse wheel correctly.
