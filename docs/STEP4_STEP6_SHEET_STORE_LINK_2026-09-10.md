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

---

# CROSS-CHECK HARDENING (2026-09-10, second pass)

Senior self-audit of the five phases above. Findings → fixes:

| # | Finding | Severity | Fix |
|---|---|---|---|
| 1 | **Double-consumption on QTY correction** — typing 5 then correcting to 3 consumed 5+3=8; keystroke commits compounded it | HIGH | Per-row net-consumption ledger; each commit takes only the DELTA. Zero-delta = "already consumed" notice; negative delta = explicit warning, never silent auto-return |
| 2 | **TOCTOU race** — balance checked against a snapshot that could be minutes old (another window/use dialog in between) | HIGH | `CommitConsumptionAsync` re-fetches the CURRENT row (`GetByIdAsync`) and validates against it immediately before the write |
| 3 | **Cross-thread `ObservableCollection` mutation** — store loads run on thread-pool continuations (`ConfigureAwait(true)` ≠ UI thread); `ColorOptions.Clear/Add` + size-list swaps could throw `InvalidOperationException` under WPF CollectionView (pre-existing latent hazard, amplified by the new ledger-refresh wiring) | HIGH | `SheetStorePricingService.PostToUi` marshaller; colour lists + SGU size list + DGU/LAM `ReplaceSizes` all land on the dispatcher |
| 4 | **DGU+LAM had no not-in-store warning** although its colours became store-only | MED | `HasSheetStoreHint`/`SheetStoreHint` on the hybrid VM (outer-pane rule, same contract as other panels); banner triggers + text case added — all four glass families now covered |
| 5 | **Interlayer combo fed glass-inventory colours** — a PVB interlayer is a film, not a stocked sheet | MED | Dedicated `InterlayerColorOptions` (classic palette); glass pane combos stay store-only |
| 6 | **Step 4 went stale after editing inventory** — add a sheet, return to wizard, colours/prices unchanged until reopen | MED | `SheetStoreViewModel.StockLedgerChanged` raised on all 5 ledger write paths (add/edit/buy/use/CSV import) → MainViewModel (lazily, idempotent — preserves deferred module creation) → `Step4VM.RefreshSheetStoreData()` |
| 7 | Misleading history wording — manual dialog entries said "auto from Sheet Store" | LOW | Neutral "via Sheet Store (manual)" / "via Step 6 Wastage (automatic)" |
| 8 | Thickness not used in Step 6 resolution — could match wrong thickness when sizes drift | LOW | Primary checked source's Step 4 section → spec-name leading mm → 4-tier resolver now thickness-aware (0 = graceful wildcard) |
| 9 | `GetBalanceAsync` dead public API | LOW | Removed (tight surface) |

**Verified:** XAML ET-parse 42/42 · C# brace/paren balance 104/104 · semantic
chain checks GREEN. Static-only (no SDK in sandbox) — runtime confirmation
on next user build.

**Carried recommendations (not implemented, by design):**
- Persisted per-invoice consumption ledger (DB table) so RE-OPENED invoices
  know historical consumption; today a session ledger covers the common
  new-invoice flow and the status line makes consumption visible.
- Negative-delta auto-return via `SheetHistoryRepository.ReverseMovementAsync`.
- Translation of the new EN strings (×15 dictionaries carry EN for now).
