# ProGlass Automation — Architecture Guide

**Purpose:** Prevent AI and developers from inventing wrong project
structure. This document is the source of truth for folder layout,
naming conventions, layer responsibilities, and architectural patterns.

---

## 1. Architecture Philosophy

- **Functional core, thin orchestration layer**
- **Pure calculation layer** — no side effects in formulas
- **Explicit data contracts** — interfaces over reflection
- **No cross-module mutation** — modules isolated by database + folder
- **Deterministic outputs** — same input always produces same output
- **AI-safe architecture** — documented + strict conventions

---

## 2. Tech Stack

| Layer            | Technology                                   |
|------------------|----------------------------------------------|
| Platform         | Windows 10/11 desktop                        |
| UI framework     | WPF (.NET 8, C# 9+)                          |
| Pattern          | MVVM strict (ViewModelBase + RelayCommand)   |
| Database         | SQLite + Entity Framework Core 8             |
| PDF generation   | QuestPDF 2024.12.2 (Community license)       |
| Excel export     | ClosedXML 0.105.0                            |
| CSV export       | CsvHelper 33.1.0                             |
| AI Assistant     | Google Gemini (gemini-flash-latest)          |
| Themes           | Custom Light/Dark with 25-brush palette      |
| IDE              | Visual Studio 2022 / VS Code                 |

---

## 3. Solution Structure

Root project: `ProGlassAutomation` (single .csproj, no sub-projects
until scale demands otherwise).

**Rule:** Main/root files stay flat. Features get sub-folders.

    <Layer>/<Module>/<FeatureName>/<Files>

### 3.1 Complete Folder Tree

    ProGlassAutomation/
        App.xaml                      Global app resources + converters
        App.xaml.cs                   Startup orchestration + service init
        MainWindow.xaml               Shell window (hosts CurrentViewModel)
        MainWindow.xaml.cs

        Docs/                         Project documentation
            Chat1/                    (empty — early template attempt)
            Chat3/                    (user reference — Step 5 deep dive)
            Chat4/                    (user reference — module map)
            Chat5/                    (THIS folder — merged authoritative)
                prd.md
                architecture.md
                rules.md
                data/
                    d04-calculations.md
                prompts/
                    p01-master-prompt.md

        Themes/                       DynamicResource theme dictionaries
            LightTheme.xaml           25 brushes
            DarkTheme.xaml            25 brushes (same keys)

        Commands/                     Shared command infrastructure
            RelayCommand.cs

        Converters/                   IValueConverter implementations
            ColorStringConverter.cs
            DecimalIsPositiveConverter.cs
            ReferenceEqualityConverter.cs
            ScaleConverter.cs
            InverseBoolToVisibilityConverter.cs
            BrushKeyConverter.cs      Phase 8.2e — theme-adaptive VM colors

        Behaviors/                    Attached behaviors
            DataGridNavigationBehavior.cs
                Static attached behavior
                Excel navigation + paste interception
                Contains IExcelPasteable interface definition

        Services/                     Business logic + I/O
            ThemeManager.cs           Static + ThemeChanged event
            ConfigService.cs          Load/Save AppConfig JSON

            Database/
                AppDbContext.cs       DbSet<GlassSheet> + DbSet<SheetHistory>
                DatabaseInitializer.cs
                DatabaseBackup/
                    BackupService.cs  Keeps last 7 copies on exit

            SheetStore/
                ISheetStoreRepository.cs
                SheetStoreRepository.cs

            ProformaInvoice/
                SpecificationsRepository/
                    ISpecificationsRepository.cs
                    SpecificationsRepository.cs
                    SpecificationsDbContext.cs
                ProformaInvoiceRepository/
                    ProformaInvoicesDbContext.cs
                    IProformaInvoiceRepository.cs
                    ProformaInvoiceRepository.cs
                    ProformaInvoiceMapper.cs   (BuildEntityFromWizard,
                                                RestoreWizardFromSnapshot)
                PdfExport/
                    IProformaInvoicePdfService.cs
                    ProformaInvoicePdfService.cs
                    PdfExportContext.cs        (shared with Excel + CSV)
                    PdfExportContextBuilder.cs (from live VMs)
                InvoiceExportImport/
                    InvoiceExcelExportService.cs
                    InvoiceCsvExportService.cs

            AI/
                AIConfig/
                    AppConfig.cs
                    ConfigService.cs
                AIService/
                    IAiService.cs
                    GeminiAiService.cs
                AIConnectivity/
                    IConnectivityService.cs
                    ConnectivityService.cs

        Models/                       Plain data POCOs + EF entities
            Dashboard/
                KpiCardModel.cs
                ChartBarModel.cs
                ActivityFeedItem.cs

            SheetStore/
                GlassSheet.cs         (main entity — flat in module)
                SheetStoreHistory/
                    SheetHistory.cs   (feature — nested)

            ProformaInvoice/
                Specifications/
                    SavedSpecification.cs
                SavedInvoice/
                    SavedProformaInvoice.cs           EF entity
                    ProformaInvoiceSnapshot.cs        JSON DTO tree
                Step6OtherCharges/
                    OtherChargeUnit.cs
                    OtherChargeRow.cs
                    PolishSubType.cs  (enum + PolishSubTypeMeta same file)
                    Wastages/
                        WastageRow.cs
                        SpecGroupColorSide.cs
                    CustomCharges/
                        CustomChargeRow.cs
                Step7PrintPreview/
                    InvoiceMainSummary/
                        DiscountType.cs
                    TermsAndConditions/
                        TermClause.cs  (enum + class same file)

        ViewModels/                   Presentation logic
            ViewModelBase.cs          INotifyPropertyChanged base
            MainViewModel.cs          Navigation + all module VMs

            About/
                AboutViewModel.cs
            AI/
                AiChatViewModel.cs

            Dashboard/
                DashboardViewModel.cs         Shell — Pages + ActivePage
                DashboardPageBase.cs          Base with ThemeChanged hook
                Overview/
                    OverviewDashboardViewModel.cs
                SheetStore/
                    SheetStoreDashboardViewModel.cs
                ProformaInvoice/
                    PiDashboardViewModel.cs

            SheetStore/
                SheetStoreViewModel.cs

            ProformaInvoice/
                ProformaInvoiceViewModel.cs   Wizard controller +
                                              List/Wizard toggle +
                                              TriggerNewInvoice() public
                SavedInvoicesList/
                    SavedInvoicesListViewModel.cs
                Step1CompanySetup/
                Step2ProjectSpecs/
                Step3ClientProfile/
                Step4PricingEstimator/
                Step5DataGrid/
                    DataGridViewModel.cs      Contains 3 classes:
                                              GlassItemRow (row model)
                                              SpecGroup (IExcelPasteable)
                                              DataGridViewModel
                    GrandTotalSummary/
                        GrandTotalSummaryViewModel.cs
                Step6OtherCharges/
                    OtherChargesViewModel.cs
                    MetricDiscoveryHubViewModel.cs
                    Wastages/
                        WastagesSectionViewModel.cs
                    CustomCharges/
                        CustomChargesSectionViewModel.cs
                Step7PrintPreview/
                    PrintPreviewViewModel.cs
                    InvoiceMainSummary/
                        InvoiceMainSummaryViewModel.cs
                    TermsAndConditions/
                        TermsAndConditionsViewModel.cs
                    PrintableInvoice/
                        PrintableInvoiceViewModel.cs   ~800 lines
                                                       (Print/Save/Load/
                                                        PDF/Excel/CSV cmds)

            JobOrder/                 Phase 9 (placeholder)
            Optimization/
            Production/
            DeliveryNote/
            TaxInvoice/
            Reports/

        Views/                        XAML views + code-behind
                                      (mirrors ViewModels structure)

            Splash/
                SplashView.xaml
                SplashView.xaml.cs    FadeOutAndClose handles handoff

            About/
            AI/

            Dashboard/
                DashboardView.xaml
                DashboardView.xaml.cs
                Overview/
                    OverviewDashboardView.xaml
                    OverviewDashboardView.xaml.cs
                SheetStore/
                ProformaInvoice/
                README.md

            SheetStore/

            ProformaInvoice/
                ProformaInvoiceView.xaml      IsListView/IsWizardView switch
                SavedInvoicesList/
                    SavedInvoicesListView.xaml
                    SavedInvoicesListView.xaml.cs  OnRowRightClick menu
                Step1CompanySetup/
                Step2ProjectSpecs/
                Step3ClientProfile/
                Step4PricingEstimator/
                Step5DataGrid/
                    DataGridView.xaml
                Step6OtherCharges/
                Step7PrintPreview/
                    InvoiceMainSummary/
                    TermsAndConditions/
                    PrintableInvoice/
                        PrintableInvoiceView.xaml   On-screen A4 preview

---

## 4. Folder Rules (STRICT)

### 4.1 Per-feature sub-folder pattern

**RULE:** Every feature = its own folder. All related files live
inside that folder together.

    Views/Dashboard/Overview/                ← feature folder
        OverviewDashboardView.xaml           ← named after feature
        OverviewDashboardView.xaml.cs

    ViewModels/Dashboard/Overview/           ← mirror in ViewModels
        OverviewDashboardViewModel.cs

**FORBIDDEN:** dumping new files flat into an existing folder that
already has sub-folders established.

    ❌ Views/Dashboard/NewThingView.xaml         (flat, not allowed)
    ✅ Views/Dashboard/NewThing/NewThingView.xaml (correct)

### 4.2 Model naming

Models get module-prefixed names when in shared folders:

    ✅ Models/Dashboard/DashboardActivityItem.cs  (module-prefixed)
    ❌ Models/Dashboard/ActivityItem.cs           (too generic)

Exception: names already clear from context (`KpiCardModel`,
`ChartBarModel`) are acceptable.

### 4.3 Namespaces mirror folders

Folder `ViewModels/Dashboard/Overview/` →
namespace `ProGlassAutomation.ViewModels.Dashboard.Overview` OR
`ProGlassAutomation.ViewModels.Dashboard` (flat namespace OK for
grouped feature sub-VMs).

### 4.4 README.md per feature

Every feature folder in Views/ or Services/ layer should have a
README.md explaining:

- What the feature does
- Key files + responsibilities
- Any gotchas or non-obvious patterns

Use 4-space indented code blocks, NOT triple backticks (avoids
markdown rendering breaks).

---

## 5. Layer Responsibilities

### 5.1 Models

- Plain data objects
- No logic, no INotifyPropertyChanged
- Just properties and maybe `ToString()` or computed properties
- EF entities can have `[NotMapped]` computed helpers
- Never reference ViewModels or Views

### 5.2 ViewModels

- All business logic lives here
- Implement INotifyPropertyChanged via ViewModelBase
- Expose ICommand properties for all user actions
- **Never reference Views directly**
- **Never reference specific UI controls**
- Exception: `PrintableInvoiceViewModel.PrintTarget` (documented)
  needs FrameworkElement for print rendering

### 5.3 Views (XAML)

- Pure data binding to ViewModels
- No logic in code-behind (see 5.4 exceptions)
- Behaviors attached via XAML
- Converters for display formatting only

### 5.4 Code-behind exceptions (allowed)

Views can have code-behind ONLY for:
- `InitializeComponent()`
- Event handlers that immediately delegate to VM commands
- UI-only concerns: scroll sync, focus management, drag-drop wiring
- Right-click ContextMenu building (XAML bindings unreliable)

Views MUST NOT contain:
- Database access
- Business calculations
- File I/O
- Direct model manipulation

### 5.5 Behaviors

- Reusable UI logic not possible in pure XAML
- **No direct ViewModel type references** — use interfaces only
- Communicate with VM via interfaces (e.g., `IExcelPasteable`)
- No static state that persists across instances (exception:
  per-navigation-session state like `_anchorColumn`)

### 5.6 Services

- External I/O (database, HTTP, filesystem)
- Repositories inherit repository pattern
- Stateless — new instance per operation
- Return DTOs or entities, never ViewModels

---

## 6. Database Architecture

### 6.1 Three isolated databases

Each database has its own DbContext + repository. Modules do not
share tables across databases.

    %AppData%\ProGlassAutomation\
        proglass.db            (GlassSheets, SheetHistories)
        specifications.db      (SavedSpecifications)
        proformainvoices.db    (ProformaInvoices — indexed + JSON blob)

Future databases: `joborders.db` (Phase 9), `taxinvoices.db` (Phase 19).

### 6.2 Repository pattern

Every data access goes through a repository. ViewModels never touch
DbContext directly.

    View → ViewModel → Repository → DbContext → SQLite

Interface + implementation:

    public interface ISheetStoreRepository
    {
        Task<List<GlassSheet>> GetAllAsync();
        Task<GlassSheet> AddAsync(GlassSheet sheet);
        Task BuyStockAsync(int sheetId, int qty, decimal? price = null);
    }

    public class SheetStoreRepository : ISheetStoreRepository
    {
        public async Task<List<GlassSheet>> GetAllAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets.ToListAsync();
        }
    }

### 6.3 DbContext-per-operation

Do NOT hold DbContext instances as fields. Create a new one per
operation via `using`:

    ✅ using var db = new AppDbContext();
    ❌ private readonly AppDbContext _db = new AppDbContext();

This avoids threading issues and connection pool exhaustion.

### 6.4 JSON snapshot pattern

Complex entities (like Proforma Invoices) are stored as JSON
snapshots in a single column, with indexed columns for search:

    ProformaInvoices table:
        Id (PK)
        ProformaId (indexed, UNIQUE, e.g., "PI-2026-001")
        CustomerName (indexed)
        GrandTotalVatInclusive (indexed)
        Status (indexed)
        CreatedAt (indexed)
        UpdatedAt (indexed)
        FullSnapshotJson (BLOB — complete wizard state)

Search + list use indexed columns.
Load reconstructs full wizard from JSON via
`ProformaInvoiceMapper.DeserializeSnapshot`.

### 6.5 Migrations

For rapid development: use `EnsureCreatedAsync()` in
`DatabaseInitializer`. When schema stabilizes post-Phase 21, migrate
to EF Core Migrations.

---

## 7. Dependency Injection

Simple service locator via static App properties (no DI container yet):

    App.Config       — AppConfig (persisted to config.json)
    App.Ai           — IAiService (GeminiAiService)
    App.Connectivity — IConnectivityService
    App.Specs        — ISpecificationsRepository
    App.Invoices     — IProformaInvoiceRepository

Initialization order in `App.xaml.cs > Application_Startup`:

    1. Config = ConfigService.Load()
    2. DatabaseInitializer.Initialize()          (proglass.db)
    3. Specs = new SpecificationsRepository()    + InitializeAsync()
    4. Invoices = new ProformaInvoiceRepository() + InitializeAsync()
    5. Ai = new GeminiAiService(Config.Ai)
    6. Connectivity.StartMonitoring()
    7. Show splash
    8. Create MainWindow
    9. Splash.FadeOutAndClose(() => Show + Activate + Focus main)

---

## 8. Navigation Architecture

MainViewModel owns module VMs as public properties. MainWindow uses
ContentControl + DataTemplate binding on `CurrentViewModel`.

    <ContentControl Content="{Binding CurrentViewModel}"/>

    <UserControl.Resources>
        <DataTemplate DataType="{x:Type vm:DashboardViewModel}">
            <views:DashboardView/>
        </DataTemplate>
        <!-- ... more DataTemplates ... -->
    </UserControl.Resources>

### 8.1 Navigate() method

    private void Navigate(object parameter)
    {
        switch (parameter?.ToString())
        {
            case "Dashboard":       CurrentViewModel = DashboardVM;       break;
            case "SheetStore":      CurrentViewModel = SheetStoreVM;      break;
            case "ProformaInvoice": CurrentViewModel = ProformaInvoiceVM; break;
            // ...
        }
    }

### 8.2 Cross-module callbacks

Sub-modules trigger navigation via Action callbacks wired in
MainViewModel constructor:

    DashboardVM.NavigateTo = target => Navigate(target);
    DashboardVM.TriggerNewInvoice = () => ProformaInvoiceVM.TriggerNewInvoice();

Sub-modules never reference MainViewModel directly.

### 8.3 List ↔ Wizard toggle (Proforma Invoice)

ProformaInvoiceViewModel has two view states:

- `IsListView = true` → shows SavedInvoicesList
- `IsWizardView = true` → shows 7-step wizard with sidebar

Same shell view (ProformaInvoiceView.xaml) uses DataTrigger to swap
content between list and wizard.

---

## 9. Theming System

### 9.1 Brush palette (25 brushes per theme)

Themes defined in `Themes/LightTheme.xaml` + `DarkTheme.xaml`.
Both files expose **IDENTICAL brush keys** — only values differ.

**Categories:**

- **Surfaces (6):** WindowBackgroundBrush, ModuleBackgroundBrush,
  ElevatedSurfaceBrush, SideMenuBackgroundBrush, MenuHoverBrush,
  OverlayButtonBackgroundBrush
- **Text (5):** PrimaryForegroundBrush, SecondaryForegroundBrush,
  MutedForegroundBrush, OnAccentForegroundBrush,
  OverlayButtonForegroundBrush
- **Accents (4):** AccentBrush, AccentHoverBrush, AccentDarkBrush,
  AccentLightBrush
- **Semantic (5):** SuccessBrush, WarningBrush, DangerBrush,
  InfoBrush, NeutralBrush
- **Tints (7):** TintIndigoBrush, TintGreenBrush, TintAmberBrush,
  TintBlueBrush, TintRedBrush, TintGrayBrush, TintPurpleBrush
- **Borders (2):** BorderBrush, DividerBrush

### 9.2 Light theme values

- Surfaces: white cards on `#F9FAFB` gray-50 background
- Text: `#0F172A` primary, `#6B7280` secondary, `#9CA3AF` muted
- Accent: `#0F4C3A` deep green (matches splash logo)
- AccentHover: `#22C55E` bright green
- Semantic: Tailwind-500 palette
- Tints: Tailwind-50 pastels (`#EEF2FF`, `#F0FDF4`, etc.)
- Border: `#E5E7EB` gray-200

### 9.3 Dark theme values

- Surfaces: `#1E293B` cards on `#0B1220` slate background
- ElevatedSurface: `#273449` (lighter than module bg)
- Text: `#F1F5F9` primary, `#94A3B8` secondary, `#64748B` muted
- Accent: `#22C55E` bright green (better contrast on dark)
- AccentHover: `#4ADE80` (even brighter)
- Semantic: Tailwind-400 palette (slightly desaturated)
- Tints: dark-tinted surfaces (`#252B48` indigo, `#1E3A2F` green, etc.)
- Border: `#334155` slate-700

### 9.4 ThemeManager

Static class with `ApplyTheme(AppTheme)` + `ThemeChanged` event.
Swaps merged resource dictionary at runtime.

    public static void ApplyTheme(AppTheme theme)
    {
        // ... swap merged dictionary ...
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

### 9.5 Two binding patterns

**Pattern A — DynamicResource (auto-swaps with theme):**

    Background="{DynamicResource ModuleBackgroundBrush}"

Use for direct XAML color references.

**Pattern B — BrushKeyConverter (for VM-provided colors):**

VM sets property to a brush key string (not hex):

    kpi.BadgeColor = "AccentBrush";     // brush key
    kpi.TintColor  = "TintIndigoBrush"; // brush key

XAML binds via converter:

    Background="{Binding BadgeColor,
                 Converter={StaticResource BrushKeyConverter}}"

Converter resolves the key against `Application.Current.Resources`
at render time.

### 9.6 Instant theme swap on VM-driven cards

For VM-driven brush-key properties, Binding + Converter does NOT
re-evaluate when a DynamicResource changes. Solution:

VMs displaying brush-key-driven collections must override
`OnThemeChanged()` in `DashboardPageBase` (or equivalent) to reassign
the ObservableCollection reference:

    protected override void OnThemeChanged()
    {
        KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
    }

This forces WPF to teardown+rebuild the DataTemplates, which re-runs
the BrushKeyConverter with fresh brush lookups.

### 9.7 Chart colors stay hex

Chart bars keep hex color strings (`#6366F1`) intentionally — data-
driven visual identity stays consistent across themes. Only card
surfaces adapt.

---

## 10. Key Design Patterns

### Pattern 01 — Repository (interface + implementation)

Every data access through repository. Interface for testability,
implementation uses DbContext.

### Pattern 02 — Async everywhere

All I/O uses async/await. Long-running operations use reentrancy
guards:

    private int _loadingFlag = 0;

    public async Task LoadAsync()
    {
        if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
            return;
        try
        {
            // work here
        }
        finally
        {
            Interlocked.Exchange(ref _loadingFlag, 0);
        }
    }

### Pattern 03 — In-memory filtering/sorting

Load once, cache in `List<T>`, filter/sort in `ApplyFilters()`.
Bound collection is separate `ObservableCollection<T>`.

### Pattern 04 — Constructor injection

Cross-step dependencies passed via constructor:

    Step6VM = new OtherChargesViewModel(Step5VM);
    Step7VM = new PrintPreviewViewModel(
        Step1VM, Step2VM, Step3VM, Step5VM, Step6VM);

### Pattern 05 — JSON snapshot for complex entities

Whole-wizard state serialized to JSON blob. Indexed columns for
search. `ProformaInvoiceMapper` builds entity + restores wizard.

### Pattern 06 — Shared export context

`PdfExportContext` is the single source of truth for PDF, Excel,
CSV exports:

    var ctx = PdfExportContextBuilder.Build(...live VMs...);
    pdfService.GeneratePdf(ctx, path);
    excelService.ExportToExcel(ctx, path);
    csvService.ExportToCsv(ctx, path);

`BuildExportContextFromSnapshot()` reconstructs context from saved
JSON without live VMs.

### Pattern 07 — Bulk update (SetAllSilent + FireAllChanged)

For operations touching many rows at once (e.g., 150-row paste):

    _isBulkAdding = true;
    try
    {
        foreach (var item in parsed)
        {
            var row = new GlassItemRow();
            row.SetAllSilent(...);   // no PropertyChanged fires
            ItemRows.Add(row);
        }
    }
    finally
    {
        _isBulkAdding = false;       // reset before batch notify
    }

    TotalRows = ItemRows.Count;
    RenumberRows();
    RecalculateFooter();
    foreach (var row in touchedRows)
        row.FireAllChanged();        // batch notify at end

Purpose: prevents N×M PropertyChanged events during bulk operations.
Keeps UI responsive during 100+ row paste.

### Pattern 08 — Interface contract (IExcelPasteable)

Behavior communicates with ViewModel via interface, never Reflection
or `dynamic`:

    public interface IExcelPasteable
    {
        void PasteFromText(string clipboardText, object startRowContext);
        void AddRows(int count);
    }

`SpecGroup` implements `IExcelPasteable`.
`DataGridNavigationBehavior` finds it via visual tree walk.

### Pattern 09 — Visual tree walk

Behavior walks visual tree to find:

    1. ListViewItem       → row boundary for column count
    2. IExcelPasteable    → ViewModel for paste target

Walk order:

    1. Check element itself
    2. Walk up visual tree via VisualTreeHelper.GetParent
    3. Fall back to logical tree via LogicalTreeHelper.GetParent

### Pattern 10 — Silent set + batch notify

`GlassItemRow.SetAllSilent()`:
- Sets all backing fields directly
- No PropertyChanged fires
- Recalculates all derived backing fields silently

`GlassItemRow.FireAllChanged()`:
- Fires PropertyChanged for every property
- Called once after all silent sets done

### Pattern 11 — BrushKeyConverter (theme-adaptive VM colors)

VM properties hold brush key names (`"AccentBrush"`). Converter
resolves at render time against `Application.Current.Resources`.
Adapts automatically to theme swap.

### Pattern 12 — Reference equality (MultiBinding for tab active state)

`ReferenceEqualityConverter` (IMultiValueConverter) compares
ActivePage reference to each item in tab list. Drives underline
highlight via DataTrigger.

---

## 11. Data Flow Diagrams

### 11.1 Paste flow (Excel → DataGrid)

    User presses Ctrl+V in TextBox
        ↓
    DataGridNavigationBehavior.OnDataObjectPasting()
        ↓
    Checks: hasTab OR hasNewline → multi-cell paste
        ↓
    e.CancelCommand()  ← stop default TextBox paste
        ↓
    FindRowDataContext(focusedBox)
        Walks up to ListViewItem.DataContext
        Returns GlassItemRow (or null)
        ↓
    FindViewModelInTree(sender)
        Walks up visual tree
        Returns first IExcelPasteable DataContext
        ↓
    pasteable.PasteFromText(clipText, rowDataContext)
        ↓
    SpecGroup.PasteFromText()
        Parse lines into (glassRef, w1, h1, w2, h2, qty) tuples
        Determine startIdx (empty row focused OR end of list)
        _isBulkAdding = true
        For each parsed row:
            Overwrite existing empty rows first
            Then append new rows
            Use SetAllSilent() + apply correct rate
        _isBulkAdding = false (finally)
        RenumberRows()
        RecalculateFooter()
        FireAllChanged() on each touched row
        Show success MessageBox (Dispatcher Background)

### 11.2 Keyboard nav flow

    User presses Tab / Arrow / Enter in TextBox
        ↓
    DataGridNavigationBehavior.OnKey()
        ↓
    FindAllTextBoxes(container) → flat list
        ↓
    GetColumnsPerRow(current)
        FindRowContainer → walk up to ListViewItem
        Count TextBoxes in ListViewItem
        ↓
    Calculate target index based on key
        ↓
    FocusAndSelect(targetTextBox)

### 11.3 Rate auto-adjust flow

    User enters W1 and H1
        ↓
    GlassItemRow.W1.set → RecalculateAll()
        ↓
    Sqm1 calculated
        ↓
    GlassItemRow.PropertyChanged(Sqm1) fires
        ↓
    SpecGroup.OnRowChanged()
        SuppressRateAutoAdjust == false?
        Sqm1 >= 4 → targetRate = EffectiveRate
        Sqm1 <  4 → targetRate = BaseRate
        ↓
    row.Rate = targetRate (if different)

### 11.4 Theme swap flow (instant, no navigation)

    User toggles theme in Settings
        ↓
    ThemeManager.ApplyTheme(newTheme)
        Resource dictionary swap
        ThemeChanged event fires
        ↓
    DashboardPageBase.OnThemeManagerChanged
        (UI-thread marshalled via Dispatcher.BeginInvoke)
        ↓
    Virtual OnThemeChanged() override
        KpiCards = new ObservableCollection<>(_kpiCards)
        OnPropertyChanged fires
        ↓
    WPF ItemsControl rebuilds DataTemplates
        BrushKeyConverter re-runs with fresh brush lookups
        ↓
    Cards render with new theme colors instantly

### 11.5 Splash-to-main handoff flow

    App startup completes
        ↓
    Show splash (Topmost=True)
        ↓
    await Task.Delay(2800)  ← splash animation
        ↓
    Create MainWindow (not shown yet)
        Application.MainWindow = mainWindow
        ↓
    splash.FadeOutAndClose(callback)
        splash.Topmost = false  ← release BEFORE fade
        Fade-out animation (400ms)
        On Completed:
            callback() ← invoked here
                mainWindow.Show()
                mainWindow.Activate()
                mainWindow.Focus()
                mainWindow.Topmost = true then false  ← yank trick
            splash.Close()

---

## 12. Extension Rule (5-step protocol for new features)

When adding new feature:

1. **Update PRD** — add feature description to `prd.md`
2. **Add formulas** — any new calculations to
   `data/d04-calculations.md`
3. **Update architecture** — add folder structure + patterns to
   `architecture.md` if new pattern introduced
4. **Follow folder convention** — create per-feature sub-folder:
   `<Layer>/<Module>/<FeatureName>/<Files>`
5. **Update AI prompt** — add to completed modules section in
   `prompts/p01-master-prompt.md`

---

## 13. What NEVER Changes

- Currency: AED (never INR, never USD)
- Country conventions: UAE (5% VAT)
- .NET 8 + WPF (no Blazor, no MAUI, no Uno)
- SQLite (no PostgreSQL, no SQL Server unless explicitly migrated)
- MVVM pattern (no code-behind business logic)
- Per-feature sub-folder structure
- Repository pattern for data access
- Three isolated databases (unless explicitly consolidated)

---

## 14. What NOT to Do

**DO NOT:**

- Reference Views from ViewModels
- Reference ViewModels directly from Behaviors (use interfaces)
- Put business logic in code-behind
- Use `dynamic` in pattern matching (CS8208 error)
- Use `DispatcherFrame.PushFrame` (CS0117 in .NET Core+)
- Use static container fields in behaviors (multi-instance conflict)
- Use Reflection when interface is available
- Call `RecalculateFooter()` inside a loop (performance)
- Fire `PropertyChanged` inside `_isBulkAdding` block (performance)
- Use integer division for SQM (precision loss)
- Omit `Math.Round()` on display values (wrong display)
- Silently swallow exceptions in UI-affecting code paths
- Hardcode hex colors in XAML (use DynamicResource brushes)
- Reassign hex to `TintColor`/`BadgeColor` in VMs (use brush keys)
- Skip the `if (_field == value) return;` setter guard
- Remove existing properties when updating a file (backward compat)

---

## 15. External Dependencies

- **App.Specs** — `ISpecificationsRepository`, method
  `GetAllAsync(string glassType)`, called by `SpecGroup.LoadFilteredSpecs()`
- **App.Invoices** — `IProformaInvoiceRepository`, CRUD +
  ExistsByProformaIdAsync + SuggestNextIdAsync
- **App.Ai** — `IAiService.SendMessageAsync()` for chat
- **App.Connectivity** — `IConnectivityService.StartMonitoring()`

Error handling on all external calls: try/catch with user-facing
MessageBox for critical operations, silent catch only for background
async loads.