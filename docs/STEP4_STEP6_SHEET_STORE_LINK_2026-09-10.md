# Step 4/5/6 ↔ Sheet Store linkage (2026-09-10)

Phases applied in one run, each verified (XAML ET-parse, brace/paren
balance, semantic chain checks, EF span-Contains sweep) and pushed
separately. **No dotnet SDK in this environment — "green" means the static
battery; runtime verification happens on the user's next build.**

## P1 — `bbf7eb5` Shared link service
- NEW `Services/ProformaInvoice/SheetStockLinkService.cs`:
  `ResolveSheetAsync(color, w, h[, thickness])` (4-tier match: exact+stock →
  exact → same thickness largest balance → colour largest balance) and
  `CommitConsumptionAsync` (balance-checked `UseStockAsync` + history +
  audit, origin-tagged, never throws) + `GetBalanceAsync`.
- `SheetStorePricingService.GetAllSheetsAsync()` added (same never-throw
  loader).
- `SheetStoreViewModel.CommitUseAsync` now routes through the SAME core —
  the manual Use dialog and the wizard cannot drift.

## P2 — `a2c4fbe` Step 4
- Built-in colour defaults REMOVED in all 4 panels (SGU/DGU/LAM/DGU+LAM);
  combos refill exclusively via `LoadColorsIntoAsync` (store-only).
  DGU+LAM previously had NO store wiring — now loads store colours too.
- Footer hint line → proper warning banner (repo TintAmber pattern, ⚠ icon,
  bold title, per-panel hint text) + "Open Sheet Store — Add Sheet" button.
- `PricingEstimatorViewModel.OpenSheetStoreCommand` →
  `NavigateToSheetStoreRequested` → MainViewModel →
  `NavigateFromAgent("SheetStore")` (license-gated, dispatcher-safe).
- Localization keys `ProformaInvoice.NotInSheetStore` /
  `.OpenSheetStoreAddSheet` added ×15 languages (EN canonical).

## P3 — `9c2e5ca` Step 5 suggest-only
- `SpecGroup.Part1.SpecTypedText`: typing an exact spec name NO LONGER
  auto-commits/auto-switches glass type/auto-fills rate. Suggestions only;
  commit exclusively on explicit dropdown pick (SelectedSpecItem) — glass-type
  auto-switch + rate autofill preserved on that explicit path. Free-typed
  spec names still fully supported.

## P4 — `564376a` Step 6 auto-use (+ `fix` commit: concurrency)
- `WastageRow`: `SheetQuantityCommittedByUser` raised ONLY on a real
  operator edit of SHEET QTY (programmatic assigns — restore, suggestions —
  never pass through the text setter → saved invoices can never
  double-consume). New `InventoryStatusText` / `IsInventoryWarning` /
  `HasInventoryStatus`.
- `WastagesSectionViewModel`: `AutoUseFromInventory` toggle (default ON,
  header, ×15 localized), `BindJobContext` (customer from Step 2),
  per-row consumption engine via the shared service (resolve colour+size →
  balance check → consume + history "auto from Step 6 Wastage"), inline
  status line (accent = consumed / amber warning = missing or insufficient).
  NEVER opens Sheet Store. Utilization untouched (Step 9 owns it).
- Race fix: in-flight guard is a ConcurrentDictionary (Add on UI thread,
  Remove on threadpool continuation).

## Known limits (honest)
- Static-only verification (no SDK sandbox): compile-level confirmation
  pending the user's build. Highest-confidence risks checked manually:
  XAML resource keys exist, binding paths exist on VMs, event wiring chain
  complete, no `T[].Contains` in EF expression trees introduced.
- Step 6 resolves inventory by colour + sheet size (thickness not passed —
  service falls back to best balance match of that colour when sizes drift);
  Step 4 remains the source of thickness quoting.
- Localization: new keys shipped in English across all 15 dictionaries —
  translation sweep is the carried task.
