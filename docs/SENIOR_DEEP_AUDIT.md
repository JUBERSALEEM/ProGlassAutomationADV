# Senior Industrial Developer Deep Code Audit
## ProGlassAutomationADV — 2026-09-02

---

## METHODOLOGY
Read every core file line-by-line: App.xaml, App.xaml.cs, App.StartupCore.cs, MainWindow.xaml, 
MainWindow.xaml.cs, MainViewModel (3 parts), ViewModelBase, AsyncUtility, PerformanceProfileService,
VisualThemeEngine, DashboardViewModel, OverviewDashboardViewModel, ProformaInvoiceViewModel,
SpecificationsCenterViewModel, and scanned ALL .cs and .xaml files for anti-patterns.

---

## CRITICAL FINDINGS (Severity 1 — UI Freeze / Crash)

### C-1: Blocking Dispatcher.Invoke from Background Threads (8+ sites)
**Files:** StatementGeneratorViewModel.Generate.cs, .Prepare.cs, .Preview.cs, .Helpers.cs,
.LiveSubscriptions.cs, .WizardState.cs, StatementListViewModel.cs, StDashboardViewModel.cs,
SettingsViewModel.Typography.cs, CurrencyService.cs, GeoService.cs

**Problem:** `Application.Current?.Dispatcher.Invoke(() => ...)` blocks the calling thread
until the UI thread processes the delegate. When called from Task.Run, this creates a
synchronous dependency on the UI thread — if the UI thread is busy (rendering, processing
input), the background thread stalls, and if the UI thread is waiting for the background
task, you get a DEADLOCK. This is the #1 cause of WPF UI freezes.

**Fix:** Replace ALL `Dispatcher.Invoke()` with `Dispatcher.BeginInvoke()` or
`Dispatcher.InvokeAsync()` for fire-and-forget UI updates from background threads.

### C-2: Excessive CommandManager.InvalidateRequerySuggested() (20+ sites)
**Files:** AgentChatViewModel, DeliveryNoteDetailViewModel, InquiryEditorViewModel,
InquiryListViewModel, DemoCenterViewModel, RelayCommand.cs

**Problem:** Each call forces WPF to re-evaluate EVERY CanExecute handler in the entire
visual tree. With 20+ call sites, this can fire dozens of times per user action, forcing
a full command tree requery that blocks the UI thread.

**Fix:** Create a throttled version that coalesces calls within a 50ms window.

### C-3: async void OpenAttachment (DeliveryNoteDetailViewModel.Part3.cs:463)
**Problem:** `private async void OpenAttachment(...)` is an unguarded async void that can
throw unobserved exceptions and crash the process.

**Fix:** Wrap in SafeFireAndForget or make it return Task.

### C-4: ObservableCollection REPLACEMENT on Dashboard Load
**File:** OverviewDashboardViewModel.cs (LoadCoreAsync)

**Problem:** Every dashboard load creates 5 NEW ObservableCollection objects (SalesTiles,
ProductionTiles, DeliveryTiles, InventoryTiles, RecentActivity), replacing the old ones.
This forces complete UI teardown + rebuild — every binding, every DataTemplate, every
container is destroyed and recreated. On a dashboard with complex KPI cards, this is
visibly slow.

**Fix:** Clear + Add to the EXISTING collection instead of replacing.

---

## HIGH SEVERITY FINDINGS (Severity 2 — Performance / Responsiveness)

### H-1: No Search/Filter Debouncing
**Files:** SpecificationsCenterViewModel.cs, and likely other list VMs

**Problem:** Every keystroke in SearchText triggers ApplyFilters(), which rebuilds the
entire filtered collection. For fast typists, this means 5-10 rebuilds per second.

**Fix:** Add a 200ms debounce timer on SearchText changes.

### H-2: Eager Construction of 12+ ViewModels in MainViewModel
**File:** MainViewModel.Part1.cs

**Problem:** Despite claiming "lazy" design, the constructor eagerly creates:
DashboardVM, InquiryVM, ProformaInvoiceVM, SettingsVM, JobOrderVM, DeliveryNoteVM,
TaxInvoiceVM, SalesReportVM, StatementModuleVM, GlobalSearchVM, AiChatVM, AgentChatVM.
Each constructor may trigger DB queries, adding 2-5 seconds to startup.

**Fix:** Make ALL non-wired VMs truly lazy via `??= ResolveOrNew<T>()` pattern.

### H-3: Clock Timer Updates Every Second
**File:** MainViewModel.Part1.cs

**Problem:** The clock timer fires every 1 second, updating CurrentDateText and
CurrentTimeText. This triggers PropertyChanged events that propagate through the
entire top bar, forcing WPF to re-evaluate bindings on every visible element.

**Fix:** Only update time when it actually changes (seconds), and date only on day change.

### H-4: 138 DropShadowEffect/BlurEffect in XAML
**Problem:** GPU-expensive effects applied globally. On lower-end hardware, each shadow
forces an offscreen render pass.

**Fix:** Already partially handled by PerformanceProfileService.RichEffectsEnabled —
ensure all heavy effects respect this flag.

### H-5: Double Dashboard Load
**File:** DashboardViewModel.cs

**Problem:** The constructor sets `_activePage = OverviewVM` then calls `_ = OverviewVM.LoadAsync()`.
But the ActivePage setter ALSO calls `_ = _activePage.LoadAsync()`. So if ActivePage is set
during construction (even to the same value), LoadAsync runs TWICE.

**Fix:** Guard against double-load.

---

## MEDIUM SEVERITY FINDINGS (Severity 3 — Code Quality / Maintainability)

### M-1: 2060 OnPropertyChanged calls without batching
**Problem:** Many VMs fire 10+ property changes during a single operation, each triggering
a separate UI update cycle.

**Fix:** Implement BeginBatchUpdate/EndBatchUpdate pattern (already exists in
InvoiceMainSummary but not used elsewhere).

### M-2: Missing Virtualization on some DataGrids/ListBoxes
**Problem:** Only DeliveryNoteListView and ListPageStyles explicitly enable virtualization.
Other list views may render all items at once.

### M-3: No Freeze() on SolidColorBrush/GradientBrush resources
**Problem:** Only 177 Freeze() calls found. WPF Freezable objects that aren't frozen
create change-listener overhead on every instance.

### M-4: Module navigation creates full view each time
**Problem:** ContentControl with DataTemplate re-creates the entire view on every
navigation. For heavy views like PI wizard, this is slow.

---

## IMPLEMENTATION PHASES

### Phase 1: Critical UI Freeze Fixes
- C-1: Replace all Dispatcher.Invoke → InvokeAsync/BeginInvoke
- C-2: Throttled CommandManager
- C-3: Fix async void
- C-4: ObservableCollection update instead of replace

### Phase 2: Responsiveness
- H-1: Search debouncing
- H-3: Clock optimization  
- H-5: Double-load prevention

### Phase 3: Performance
- H-2: True lazy VM loading
- H-4: Effect optimization
- M-1: Property change batching
- M-3: Brush freezing

### Phase 4: Visual Polish & Smoothness
- Smooth navigation transitions
- Consistent animation timing
- Loading state indicators
- Module-specific optimizations
