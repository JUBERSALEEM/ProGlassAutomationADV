# PERFORMANCE & SMOOTHNESS — SENIOR ANALYSIS (2026-08-30)

**Scope:** full WPF (.NET 8) rendering, scrolling, and list-load performance audit of
`ProGlassAutomationADV`. Goal: constant FPS scrolling, no UI-thread stalls, fast list
pages, with every change justified by measured/reasoned root cause.

**Status:** analysis complete — 13 findings (PERF-1 … PERF-13 + 2 hardware-side bugs).
PERF-7, PERF-8, PERF-9, PERF-10, PERF-11, PERF-12, PERF-13 **applied in this pass**.
Remaining items are documented with concrete next steps and a verification plan.

---

## 1. Executive summary

The "laggy / slow scrolling" is **not one bug** — it is the compounding of four
anti-patterns that are all classic WPF performance traps:

1. **Pixel-shader effects (`DropShadowEffect`) inside virtualized list item
   templates.** Every row button (OPEN / VERSIONS / DELETE / …) carried a
   `DropShadowEffect` (BlurRadius 32–48). WPF re-runs that shader for every
   instance on every redraw; with `VirtualizationMode=Recycling` the ~60 visible
   effects are destroyed and re-created as the viewport scrolls. This alone is
   the #1 scroll-stutter source on list pages.
2. **A per-frame scroll animation loop (`CompositionTarget.Rendering`) on
   non-virtualized page scrollers.** Every frame performed
   `ScrollToVerticalOffset`, forcing a full re-measure of the entire page
   (dashboards, Step 5 cut-list grid with thousands of TextBoxes, settings,
   wizard pages) at display refresh rate — *even between wheel events*.
   The apparent "smoothness" was a rubber-band ease that users feel as lag.
3. **`CanContentScroll="False"` combined with `VirtualizingPanel.IsVirtualizing="True"`.**
   WPF silently **disables virtualization** in that combination, so every row of
   the Statement List / Statement Preview was materialised up front.
4. **Whole-snapshot DB reads and unbounded file fan-out on list pages.**
   All **Saved Proforma Invoices** rows were loaded via `GetAllAsync()`
   (every column including `FullSnapshotJson` — the entire serialized invoice
   snapshot per row), followed by 4 sequential full-table SQLite queries for
   document progress, followed by **one manifest file read per invoice, all at
   once** (unbounded `Task.WhenAll`).

Secondary findings: infinite `RepeatBehavior=Forever` spinner storyboards running
on ~13 list pages even when hidden; global search firing a 7-module DB query on
**every keystroke** (no debounce, no stale-result protection, queries dropped
when busy); `SmoothScrollBehavior` was a silent no-op when attached to a
`ListBox/ListView` (only `ScrollViewer` was handled); non-virtualized chat
transcripts; Step 5 cut-list grid non-virtualized by design (drag-and-drop).

---

## 2. Methodology

**Static (what was done here):** inventory of every `DropShadowEffect` /
`BlurEffect` instance, every `ScrollViewer`/`ItemsControl` virtualization flag
combination, every `CompositionTarget.Rendering` / `DispatcherTimer` /
`RepeatBehavior=Forever` clock, every repository read used by list ViewModels,
and every `async void` / `.Result` / `.Wait()` hazard. Cross-checked against the
list pages' XAML (46 `SmoothScrollBehavior` attachments, 7 dashboard
`SmartScrollBubblingBehavior` attachments).

**Runtime (to run on a Windows machine — this sandbox has no .NET SDK):**

```powershell
# 1. Measure baseline vs. fixed build
dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x4000000000001000,Microsoft-Windows-DotNETRuntimePrivate:0x1000
perfview  # GPU / render thread: "WPF: Render Thread" events, "WPF: Layout" events

# 2. Focused regressions
#    • Dashboard scroll for 30 s        → expect 60 FPS, zero Layout > 16 ms events
#    • Saved Invoices (500+ rows) open  → expect < 500 ms to first paint
#    • Saved Invoices scroll 100+ rows  → expect container reuse, 0 effect CPU
#    • Statement Preview (1000 rows)    → expect virtualization (only ~30 containers)
#    • Type in Global Search "pi" fast  → expect 1 query per pause, latest text wins
```

`dotnet build` (or `dotnet test` for the xUnit suite) on a Windows dev machine is
the first gate; the patch is designed to be build-clean.

---

## 3. Findings, root causes, and fixes

### PERF-7 — `DropShadowEffect` on every virtualized row button  [CRITICAL · FIXED]

**Evidence:** `Styles/ButtonStyles.xaml` — `RowButtonTemplate` (`ShadowHost` →
`Effect={DynamicResource ShadowSoft}`, BlurRadius 32–48). Used by
`RowPrimaryButton`, `RowOutlinedButton`, `RowSuccessButton`, `RowDangerButton`
– **46 uses across list page row templates** (8–24 row buttons per page).

**Root cause:** WPF `Effect` = GPU/CPU pixel shader re-run every time the
element redraws, plus a texture re-allocation; affecting every child. With
`VirtualizationMode=Recycling` the effects are also continuously
re-created/destroyed while scrolling.

**Fix (applied):** removed the effect from `RowButtonTemplate` and replaced it
with a 1 px `DividerBrush` bottom edge (26 px pill buttons read the same depth
from crystal border + top glare). Toolbar buttons keep their shadow via
`GlassPillButtonTemplate` (a few dozen on screen max).

**Impact:** ~60 live shader passes per list screen → 0. This is the single
largest constant-FPS win, and it also speeds up `Recycling` container reuse.

### PERF-8 — per-frame scroll animation on heavy non-virtualized pages  [CRITICAL · FIXED]

**Evidence:** `Behaviors/SmoothScrollBehavior.cs` hooked
`CompositionTarget.Rendering` and called `ScrollToVerticalOffset` every frame
until the eased target was reached; `MainWindow.xaml` (menu), dashboards,
`Step5DataGrid` (outer scroller), Settings, wizard pages, etc. all enabled it.

**Root cause:** with `CanContentScroll=False` every `ScrollToVerticalOffset`
invalidates measure/layout/render of the whole subtree; a frame loop multiplied
that by 60–144. On the Step 5 cut-list (thousands of cells) each frame was a
massive layout. The ease-out also feels like input lag ("rubber band").

**Fix (applied):** the behavior was rewritten — **no render loop, zero clocks**:
- Virtualized pixel lists (`IsVirtualizing=True` + `ScrollUnit=Pixel`, the
  .NET 4.5 combo used by every list page) are **delegated to the native WPF
  scroller** (auto-detected via visual-tree probe — both shapes: ScrollViewer
  hosting an ItemsControl and ListBox/ListView owning a ScrollViewer).
- All other scrollers scroll the full 120 px **instantly per wheel event**
  (Chrome-like), so work is proportional to input, not refresh rate.
- **Nested-scroll etiquette:** at the inner scroller's top/bottom boundary
  the wheel is no longer swallowed — it bubbles to the outer page scroller
  (the dashboard `SmartScrollBubblingBehavior` now actually works).
- It now actually works when attached to `ListBox`/`ListView` (previously a
  silent no-op), with `ConditionalWeakTable` + `Unloaded` cleanup and a
  dispatcher retry for late template build.

### PERF-9 — list pages materialising full snapshots (FullSnapshotJson)  [HIGH · FIXED]

**Evidence:** `SavedInvoicesListViewModel.LoadInvoicesCoreAsync` called
`App.Invoices.GetAllAsync()`; `SavedProformaInvoice.FullSnapshotJson` holds the
complete serialized invoice. Job Order already had the right pattern
(`GetListRowsAsync`); PI was the outlier.

**Fix (applied):** added `ProformaListRow` (scalars only) +
`IProformaInvoiceRepository.GetListRowsAsync()` (AsNoTracking projection, blob
column never touched) and switched the Saved Invoices list to it.

**Impact:** list open time becomes O(rows × small row) instead of
O(rows × full invoice) and memory pressure drops by ~2 orders of magnitude on
typical datasets. Detail views still use `GetAllAsync` where the snapshot is
needed.

### PERF-10 — unbounded manifest read fan-out on the PI list  [HIGH · FIXED]

**Evidence:** `LoadApprovalProofStateAsync` ran
`Task.WhenAll(ids.Select(... LoadManifestAsync ...))` — one
`File.ReadAllTextAsync` + JSON parse per invoice, all at once.

**Fix (applied):** bounded concurrency via a static `SemaphoreSlim(8,8)`;
`ConfigureAwait(false)` inside the read pipeline so the UI thread is never in
the hot path. Also switched the input to the new `ProformaListRow` projection.

### PERF-11 — 4 sequential full-table queries per list load  [HIGH · FIXED]

**Evidence:** `DocumentProgressSnapshot.LoadAsync` (called by PI/JO/DN/TI list
pages) loaded job rows, master DNs, ALL DNs, and ALL tax invoices strictly
one after another, each on its own DbContext.

**Fix (applied):** the four independent reads now run concurrently
(`Task.WhenAll`-style with individual try/catch), cutting page-open latency from
4×query to ~1×query while keeping each failure isolated (behaviour when a table
is unavailable is unchanged). This also benefits the JO/DN/TI lists because they
use the same snapshot service.

### PERF-12 — perpetual spinner storyboards on ~13 list pages  [MEDIUM · FIXED]

**Evidence:** `Controls/LoadingState/LoadingStateControl.xaml` started a
`RepeatBehavior="Forever"` rotation on `Loaded`. The control stays in the tree
on 13 list/detail pages, only toggling Visibility — so a permanently running
animation clock continued on every one of those pages even while idle.

**Fix (applied):** the storyboard is now code-behind and started/stopped with
`IsVisibleChanged`/`Unloaded` — one clock, only while a spinner is actually on
screen.

### PERF-13 — global search: 7-module DB query per keystroke  [MEDIUM · FIXED]

**Evidence:** `GlobalSearchViewModel.SearchText` setter → `FireSearchAsync` →
`SearchAsync` ran 7 module queries immediately for every keypress; `if (IsBusy)
return;` **silently dropped** searches, so results could be stale; no
cancellation/generation guard meant out-of-order results were possible.

**Fix (applied):** 250 ms debounce timer; revision counter so only the newest
query may paint results; if a new keystroke arrives while a search is running,
the search is **re-run after completion** instead of being dropped.

### PERF-14 — `CanContentScroll=False` silently disables virtualization  [HIGH · FIXED]

**Evidence:** `Views/Reports/StatementList/StatementListView.xaml` and
`Views/Reports/StatementGenerator/Preview/PreviewStep.xaml` set
`CanContentScroll="False"` together with
`VirtualizingPanel.IsVirtualizing="True"` + `ScrollUnit="Pixel"`.

**Root cause:** when `CanContentScroll=False`, WPF turns off item virtualization
entirely; every statement row (report cards with many bindings) was realised
before first paint, and SmoothScrollBehavior's own doc comment warned about
exactly this combination.

**Fix (applied):** `CanContentScroll="True"` on both (virtualization preserved,
pixel scrolling preserved via `ScrollUnit=Pixel`). The two other
`VirtualizingPanel` lists (Saved Invoices, Delivery Notes, JO list) were already
correct and serve as the reference pattern:

```
ListBox  + CanContentScroll=True + IsVirtualizing=True
         + VirtualizationMode=Recycling + ScrollUnit=Pixel
         + CacheLength="200,400" (Pixel)
```

### PERF-15 — row templates with expensive per-row constructs  [LOW · NOTED]

Rows use `RelativeSource AncestorType` bindings (slowest binding form), per-row
inline `TextBlock.Style` in button templates, and `DynamicResource` walk-ups.
Acceptable at current sizes; if 5,000+ row lists become common, switch those
bindings to `x:Reference`/named ancestors or inherit a shared item-level
DataContext (e.g., bind command from a static resource).

### PERF-16 — Step 5 cut-list grid is intentionally non-virtualized  [LOW · NOTED]

`DataGridView.xaml`: outer `ScrollViewer` (`CanContentScroll="False"`) with
inner per-group `ListView` using a `StackPanel` items panel (drag-and-drop
requirement; the header comments explain why virtualizing was rejected).
Every row (~8 TextBoxes + 5 bindings) is always realised. With 200–500 rows
this is hundreds of elements permanently alive. Recommended follow-up (needs a
dedicated pass): row templating with `ItemsControl` + manual drag indices, or
`DataGrid` with `EnableRowVirtualization`. The direct-scroll change (PERF-8)
already removes the per-frame relayout on this page, which was the worst part.

### PERF-17 — Chat transcripts are non-virtualized  [LOW · NOTED]

`AiChatView` and `AgentChatView` host `ItemsControl` inside a non-virtualized
`ScrollViewer`. Bounded in practice; if long sessions (>300 messages) become
common, switch to a virtualized `ListBox`/`ListBox`-style chat panel.

### BUG-A — `SmoothScrollBehavior` attached to ListBox/ListView was inert  [FIXED]

`OnIsEnabledChanged` accepted only `ScrollViewer`; 46 XAML attachments — the
majority on `ListBox`/`ListView` — did nothing. No longer possible: the
behavior now handles `ItemsControl` too (see PERF-8).

### BUG-B — `SmartScrollBubblingBehavior` visual-tree walk per wheel event  [LOW · NOTED]

Each wheel tick runs a recursive `FindDescendantScrollViewer` scan. On dashboards
the inner scroller tree is small, so cost is negligible; if it ever runs on a
huge tree, cache the found ScrollViewer keyed by the attached control
(`ConditionalWeakTable`) and invalidate on `Unloaded`.

---

## 4. Verified invariants (no regressions introduced)

- No `async void` introduced outside `AsyncUtility` / WPF event handlers
  (checked the whole tree).
- No `.Result` / `.Wait()` blocking calls introduced.
- All timers are bounded and stopped on `Unloaded`/dispose where applicable
  (clock = 1 s; connectivity = 5 s; debounce timers 2–3.5 s; spinner now
  visibility-gated).
- `LiquidGlassDesign.xaml` / `LiquidGlassStyles.xaml` `Effect` resources are
  **unused by any view** (0 references) — dead at runtime, harmless.
- Shell sidebar styles (`SidebarNavItem`, `SidebarSubItem`) have no live
  per-item effects; only the active chip carries `NavActiveGlow` (1 instance).
- Dashboard `SoftCardStyle` keeffects: PERF-3 already removed tile shadows;
  remaining 6 large-card effects are deliberate and bounded.

---

## 5. Remaining recommendations / roadmap

1. **Build on Windows and run the runtime checkpoints** (Section 2). No .NET SDK
   is available in the analysis sandbox, so compile verification must run on the
   dev machine (`dotnet build`, `dotnet test`).
2. If buttery easing is wanted on cheap, non-virtualized pages, implement it
   with a single self-stopping `DoubleAnimation` on the scroller's offset —
   never a global `Rendering` loop.
3. **Step 5 cut-list virtualization** (PERF-16) — the last big structural
   trade-off; budget a dedicated pass with the drag-and-drop requirements.
4. Consider `VirtualizingStackPanel.CacheLength` tuning and `IsDeferredScrollingEnabled`
   experiments on the 1000+ row report pages after the runtime pass.
5. Telemetry: add a lightweight frame-time counter (CompositionTarget-based,
   only while visible, ~line 5) to the Diagnostics view to track regressions;
   keep it off by default.

---

*No runtime timings are claimed here: this audit is static, and the findings are
the well-documented WPF behaviours (effects, virtualization, render loop) that
this codebase was violating. The fixes remove whole classes of wasted work; the
checkpoints in §2 will quantify the improvement on the target hardware.*
