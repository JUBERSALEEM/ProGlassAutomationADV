# Studio Bento — five production list pages

**2026-09-22 · Approved direction: Proforma concept 07 (Studio Bento)**
**Baseline:** `7663b7d1277266680e73a6dc49a0bb27b8ccfae9`
**Branch:** `arena/01a0c412-proglassautomationadv`

## 1. Scope and publication

Implemented the approved Indigo/lavender bento presentation in the actual WPF list views for:

1. Proforma Invoice saved list.
2. Job Order saved list.
3. Delivery Note list, including all six tabs and pending-item rows.
4. Tax Invoice list, including all four family tabs.
5. Statement Generator's saved-statement list (`StatementListView`), not the read-only Statement Report or generation wizard.

The user's standing instruction is to push completed source work while excluding mockups/images. This change is prepared for publication on the session branch with the native-verification limitation disclosed. **No mockup, PNG/JPG, illustrative business record, rendering script or new dependency is included.** The approved images remain outside the repository.

**Native build / xUnit / WPF rendering / DPI / performance: NOT EXECUTED — TOOLCHAIN UNAVAILABLE.** Source contracts and syntax checks do not replace compilation or runtime binding tracing. This is not a release-certification or full-regression claim.

## 2. Source analysis before implementation

The five XAML views, their resource/template scopes and code-behinds were inspected against the committed baseline. The review also traced the relevant VM partials, row models, parent hosts, loading/filtering/selection paths and commands. The prior Proforma review was continued rather than using mockup labels as an implementation specification.

| Module | Bound summary values | List controls / sources | Key contracts retained |
|---|---:|---|---|
| Proforma | 3 | `FilteredItems` | Search delay, computed-status filter, inclusive created-date bounds, sort presets, ShowVoided, Today/Clear behavior, filtered Excel export; original row fields, versions, duplicate/revision, approval, individual exports, four dossier variants, sharing and dependency-aware delete/void |
| Job Order | 4 | `DisplayedJobs` | Search, delivery-derived status filter, independent priority filter, eight existing sort options, ShowVoided; production status, delivery status and priority remain separate; date/overdue/value/project/customer/source PI fields; open, history, status/priority menus, export/share/delete and create-from-PI workflow |
| Delivery Note | 5 | `AllItemsGrouped`, `PreparedItemsGrouped`, `DeliveredItemsGrouped`, `PendingItems`, `ReworkItemsGrouped`, `ReturnedItemsGrouped` | Five grouped collections plus distinct pending rows; selected-note metadata/actions; driver/latest DN/date; piece and SQM ordered/delivered/balance; real progress, FOC/rework/return/void flags; group/master opening, remaining-balance DN creation, master invoice generation and existing dispatch/delete guards |
| Tax Invoice | 5 | `AllFamilies`, `DraftFamilies`, `IssuedFamilies`, `PaidFamilies` | One row per PI family; selected-family summaries; search across family/member identity, PO/DN references; existing family-status filter; latest invoice/date/status, invoice count, real invoiced progress, invoiced/balance amounts, overdue flag; select/open/double-click/delete family/create actions |
| Statements | 4 | `Rows` | Existing search over number/title/customer/project/company/salesman; `SelectedRow`; invoice count/date/subtitle/attachments/invoiced/outstanding/overdue status; new/edit/delete/PDF/share actions and both HasPdf gates |

### Metrics are not interchangeable

- **Proforma:** `TotalCount` is the loaded saved-record count; `FilteredCount` and `TotalValueSum` follow filters. Quote value is VAT inclusive and **not collected revenue**. Cancelled records can contribute when included by the current filters. Search/status/date/sort formulas were not changed.
- **Jobs:** totals and production/completed/overdue counts retain the original whole-loaded-dataset scope. A row's production state and delivery-derived state remain independent. `GrandTotalDisplay` retains its existing formatting, not a new financial calculation.
- **Deliveries:** hero labels/values follow the existing selected note or dataset summary. Piece quantities, SQM and delivery-note counts are distinct units. Pending-item view intentionally has different filter/header visibility. Rework/Returned loading is not replaced by an All-tab subset.
- **Tax:** dataset or selected-family hero semantics remain dynamic. Draft/Issued/Paid tabs are family predicates, not mutually exclusive invoice-count partitions. The existing real invoiced-progress calculation is retained; no fictional progress bar or growth metric is introduced.
- **Statements:** KPI totals are across all loaded saved statement snapshots, not just search matches. Snapshots can overlap; overdue is included in outstanding. They are **not a deduplicated receivables ledger**. No PI date/status filters, revenue chart or fabricated statement export capability was added.

## 3. Binding preservation evidence

`Tests/Lists/Contracts/studio-list-bindings.json` was captured from the pre-edit source. It contains source metadata only, no customer records.

| View | Original binding-valued attributes | Object-element Bindings | Original XAML command endpoints |
|---|---:|---:|---:|
| Proforma | 50 | 1 | 12 |
| Jobs | 48 | 0 | 5 |
| Deliveries | 98 | 4 | 8 |
| Tax Invoices | 70 | 2 | 6 |
| Statements | 43 | 0 | 8 |
| **Five-list total** | **309** | **7** | **39** |

Also retained: **all 36 Proforma parent-shell binding attributes** and its creation automation IDs. The list resource scope does not theme or rewrite the wizard branch.

The portable checker validates:

- Original binding multiplicity **and page/data-template/control-template scope**.
- All seven object-element bindings, including the dossier's parent-VM Tag bridge and original progress-width MultiBindings / named tracks.
- Same control-level command, parameter, label, Tag, tooltip and enabled gate—not merely matching command names elsewhere.
- Original collection/template connections, page classes and inherited DataContexts.
- Original mouse/loaded event endpoints; Statement selection and PDF/share HasPdf gates.
- All six Delivery and four Tax tab indices, with original collections.
- All 21 original summary values.
- All four complete original row-menu bodies, normalized only for the menu-entry adapter, placement mode and scoped resource lookup. Hashes were independently compared with `git show` of the baseline before recording them.
- A negative control demonstrates that swapping page/row contexts preserves flat binding counts but fails the scoped contract.

Authorized binding presentation mapping is limited to `BrushKeyConverter` → `Sl.StatusBrush` and explicit `soft` / `ink` color parameters. The underlying property paths and business meanings remain unchanged. This mapping also preserves `StatusForegroundKey` independently of `StatusTintKey` in Delivery rows.

## 4. Shared implementation

### New production resources

- `Views/Shared/Lists/StudioListResources.xaml`: one scoped list system, merging the approved Overview palette. Solid indigo hero, warm-white 18-DIP cards, lilac canvas, plum text, small semantic pills, compact buttons, tabs, row hover/focus, table header, error treatment and native-input styling.
- `Views/Shared/Lists/StudioListConverters.cs`: presentation-only palette resolution, safe string-only Tag glyph handling, scrollbar gutter, finite viewport extents and bounded toolbar-height calculation.
- Existing `IndigoDashboardPanel` is reused for responsive summary rows; its source is unchanged.

### Layout choices

- Original KPI elements and their triggers were recomposed into a hero + supporting-summary + actions bento. No fabricated KPI values or new queries.
- Filters have explicit labels and wrap; selection strips and tab controls also reflow.
- A scrollable toolbar region has a viewport-relative maximum height. **All controls remain reachable by scrolling** if the host is short or narrow; the register retains its own bounded space.
- One horizontal scroller contains both table header and data region. Header and row column definitions match, including explicit action-column widths. A system-scrollbar-width gutter reserves header alignment.
- All **13 ListBox/ListView controls** now delegate content scrolling with pixel units and recycling. An outer horizontal-only viewport has explicitly finite content height; it does not vertically measure the entire list to infinity.
- Fixed minimum register widths preserve business fields; narrow hosts scroll horizontally instead of cutting off data. Action groups wrap inside their column. Financial text wraps and has full-value tooltips.
- Four visible More buttons invoke the **same original menu builders** as right-click. Delivery also gets a keyboard-reachable Open button invoking its existing group-open command. No new mutation logic is in the view.
- Statement keeps its own PDF/share/edit/delete actions; it does not receive imaginary Proforma history/dossier commands.
- Proforma's creation actions remain in their correct parent VM scope. The embedded list's action card contains its actual Refresh / list-export commands, not broken bindings to parent commands.
- The dossier button's Tag remains a parent-VM bridge. The shared glyph converter renders only string Tags, preventing an object/type name from appearing as an icon.
- Existing native context-menu templates are reused with scoped styles; local lookup replaces application-only lookup for PI helper menu items.

No App.xaml merge, global theme replacement, model/schema/repository change, optimizer change, project dependency or version bump.

## 5. Narrow debugging changes

| Finding | Implemented correction |
|---|---|
| Recycling/pixel flags were combined with `CanContentScroll=False` | Delegate content scrolling in all 13 bounded lists; add native realized-container/layout tests, still unexecuted |
| Fixed filter columns and disabled horizontal data scrolling could cut off controls/fields | Wrapping filters/tabs/actions, bounded scrollable toolbar, shared horizontal register viewport, matching header/row column widths |
| Export result properties existed but PI did not display them | Bind `LastExportStatus` with `HasExportStatus` in its action card; retain actual success/failure text without forcing a green success state |
| Missing PI repository returned as apparent empty success | Add `HasError`/`ErrorMessage`; shared data area does not display failure as successful empty data |
| Optional/null coordinator could crash subscription during initialization | Null-safe subscription/unsubscription; explicit load failure for missing required repository/coordinator rather than exposing unsafe loaded actions |
| Statement used fire-and-forget Dispatcher publication then ended loading before rows were installed | Await UI publication, coalesce reloads, publish explicit error state and keep failures out of IsEmpty / IsListVisible |
| Tax set `_reloadQueued` while loading but never consumed it | Drain the queued refresh after releasing the loading flag, following the existing module pattern |
| Tax AllCount fell back to a stale All-tab collection when the correct filtered count was zero | Return the actual `_allCount` (zero is a valid result) |
| Selected Tax family could refer to an obsolete object after refresh | Rebind by existing PI family reference to refreshed data, or clear if removed |
| PI standalone picker retained document subscriptions after close | Dispose the picker-owned VM from `OnClosed` |
| Picker exposed restoration without a restore callback and could claim restoration without navigation | History remains viewable; restore is explicitly disabled in this host. `VersionsDialog.Show` has an optional `canRestore=true`, preserving normal existing callers |
| Picker approval callback is absent | Show an explanation directing approval through the main module's Step 8; do not silently no-op or bypass approval |
| Tax family brush keys already contain `Tint`; using the same key for text could make it pale-on-pale | Explicit `ink` override for foreground and `soft` fill; semantic palette-pair contrast source tests pass |

Repositories, permission gates, duplicate/revision construction, approval persistence, delete/void decisions, attachment handling, financial formulas, report generation and sharing implementations are not rewritten.

## 6. Verification actually executed

| Check | Result |
|---|---|
| `python3 scripts/verify-studio-lists.py` | **PASS — 24 source tests** |
| `python3 scripts/verify-indigo-modules.py` | **PASS — 18 source tests**, existing six-dashboard contracts |
| `python3 scripts/verify-indigo-overview.py` | **PASS — 12 source tests**, approved Overview/shell |
| **Portable total** | **54 passing tests**; not native execution |
| `verify-dashboard.py` | **340/340 XAML well-formed**; zero unresolved dashboard resources/binding heads/style-target/KPI-host issues. Dashboard checks are not a claim of native list binding resolution |
| `xaml-resource-lint.py` | **Clean — 340 XAML files**, 121 non-fatal typed-resource warnings across the repository |
| `xaml-xref-audit.py` | **0 forward references / 0 reported duplicate keys** under this tool's checks |
| Changed/new C# delimiter checks | **16 files balanced**; not compilation |
| `git diff --check` | PASS |
| Services, Models, App.xaml, project file, dashboards and Statement Generator wizard compared with baseline | Unchanged |
| Native build / full xUnit / rendering / DPI / performance | **NOT EXECUTED — TOOLCHAIN UNAVAILABLE** |

### Repository-wide hygiene is still failing

The separate `audit-xaml.py` reports **FAIL: 229 violations**, versus **234 at the committed baseline**. Baseline content was read from Git without resetting the working tree; failing key/category identities were compared. **No new failing key/category pairs** were found.

The apparent decrease of five is **not five fixed global defects**: local resource overrides cause that audit to classify five formerly app-only duplicate keys as mixed-scope informational duplicates (`CardCornerRadius`, `ControlCornerRadius`, `Surface.Primary.BackgroundBrush`, `Surface.Secondary.BackgroundBrush`, `SurfaceInputBrush`). Existing application-level duplication debt remains. Do not call this a repository-wide clean audit.

### Native tests added, not executed

`Tests/Lists/StudioListPresentationTests.cs`: four Facts plus six Theory cases (10 cases total): Tag safety; finite width/height conversion; toolbar-height bounds; scrollbar gutter; strong/soft semantic brushes; and a real WPF ListBox in a bounded horizontal viewport at narrow/wide widths with all 500 synthetic test items retained and container recycling assertions. These integer-only test items are not mockup/customer data and never enter the application.

## 7. Remaining validation and known boundaries

Before release on Windows:

```powershell
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --filter "FullyQualifiedName~Lists|FullyQualifiedName~Dashboard"
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release
```

1. Open all five lists in the main shell and PI in its Step 7 picker. Inspect WPF binding traces, loaded dictionaries, popup colors, title scope, keyboard focus and all row actions.
2. Check 100/125/150/200% DPI, narrow/wide and short hosts, PI picker minimum size, long names/translations, large amounts and long selected driver/status values. Confirm header/row alignment and nested scroll behavior.
3. Verify all six Delivery tabs (including Pending, Rework, Returned) and four Tax family tabs. Verify real quantity/SQM/progress values, permissions and selection-dependent summaries.
4. Exercise initial load, queued refresh, rapid filter/tab changes, empty results, missing services and repository failure. Zero-match Tax AllCount must remain zero. A deleted selected family must not remain in the hero.
5. Confirm PI New/Inquiry choices, open/unsaved-work handling, duplicate/revision, approval through Step 8, four dossier variants/P&L permission, invoice PDF/Excel/CSV, filtered-register Excel, version history and sharing. Picker history must remain readable without allowing unsupported restore.
6. Confirm Job status vs delivery status vs priority, PDF/Excel/share/history and delete/void dependency guards; Delivery master/latest/dispatch restrictions and invoice generation; Tax latest-family navigation and family delete/void; Statement PDF/share availability, edit/new/delete and list/wizard toggle.
7. Measure realized containers and scroll/input performance on large real lists. No performance improvement is certified from the source configuration alone.

Previously identified business-flow concerns are not disguised as styling fixes: PI conversion-success messaging after a caught job-conversion failure, Step 7 save-outcome propagation before loading, duplicate-snapshot sanitization fallback, and older source tests that assume monolithic VM files require separate focused investigation/validation. No blanket claim that all legacy business defects or all native tests are resolved is made.

**Result:** one implemented shared Studio Bento list system, with original module-specific sources/actions retained and documented list-state/layout corrections. Publish source only; native acceptance remains pending.
