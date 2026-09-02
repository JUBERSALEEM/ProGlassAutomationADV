# ProGlass Automation — Architecture Guide

**Version:** 3.0 (Chat 10 update — through Phase 10.9a)

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
- **Shared infrastructure** — export engine + design tokens reused
  across modules (never duplicated)

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
| Shared exports   | IPdfLayout / IExcelLayout / ICsvLayout       |
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
            Chat5/                    (legacy — earlier merged version)
            Chat5-9/                  (THIS folder — current authoritative)
                prd.md
                architecture.md
                rules.md
                delivery.md           DN module deep-dive reference
                data/
                    d04-calculations.md
                    d05-design-system.md
                prompts/
                    p01-master-prompt.md

        Themes/                       DynamicResource theme dictionaries
            LightTheme.xaml           25 brushes
            DarkTheme.xaml            25 brushes (same keys)
            Styles/                   Shared style dictionaries
                TypographyStyles.xaml
                IconBadgeStyles.xaml
                PillStyles.xaml
                CardStyles.xaml

        Commands/                     Shared command infrastructure
            RelayCommand.cs

        Controls/                     Reusable UserControls
            GradientHeroCard/
                GradientHeroCard.xaml + .cs
            Sparkline/
                SparklineControl.xaml + .cs
            MiniBarChart/
                MiniBarChartControl.xaml + .cs

        Converters/                   IValueConverter implementations
            ColorStringConverter.cs
            DecimalIsPositiveConverter.cs
            ReferenceEqualityConverter.cs
            ScaleConverter.cs
            InverseBoolToVisibilityConverter.cs
            BrushKeyConverter.cs
            PercentToWidthConverter.cs

        Behaviors/                    Attached behaviors
            DataGridNavigationBehavior.cs
                (contains IExcelPasteable interface)

        Services/                     Business logic + I/O
            ThemeManager.cs           Static + ThemeChanged event
            ConfigService.cs          Load/Save AppConfig JSON

            Database/
                AppDbContext.cs       Sheet Store DbSets
                DatabaseInitializer.cs
                DatabaseBackup/
                    BackupService.cs

            Export/                   ← SHARED EXPORT ENGINE (Phase 9.5a)
                Core/
                    IDocumentLayout.cs
                    ExportRequest.cs
                    ExportResult.cs
                    ExportFormat.cs
                FileSystem/
                    SaveFileDialogService.cs
                    ExportFileNamer.cs
                Pdf/
                    IPdfLayout.cs
                    PdfExportEngine.cs
                    DocumentStyle.cs
                    LetterheadInfo.cs
                    PageLayout.cs
                    Renderers/
                        LetterheadRenderer.cs
                        FooterRenderer.cs
                        SectionHeaderRenderer.cs
                Excel/
                    IExcelLayout.cs
                    ExcelExportEngine.cs
                    WorkbookStyle.cs
                Csv/
                    ICsvLayout.cs
                    CsvExportEngine.cs

            SheetStore/
                ISheetStoreRepository.cs
                SheetStoreRepository.cs

            ProformaInvoice/
                SpecificationsRepository/
                ProformaInvoiceRepository/
                Export/               ← migrated Phase 9.5b
                    ProformaInvoicePdfLayout.cs
                    ProformaInvoiceExcelLayout.cs
                    ProformaInvoiceCsvLayout.cs
                    PdfExportContext.cs (retained for context building)
                    PdfExportContextBuilder.cs

            JobOrder/
                Database/
                    JobOrdersDbContext.cs
                Repository/
                    IJobOrderRepository.cs
                    JobOrderRepository.cs
                Mapper/
                    JobOrderMapper.cs
                Export/               ← Phase 9.5a
                    JobOrderPdfLayout.cs
                    JobOrderExcelLayout.cs
                    JobOrderCsvLayout.cs

            DeliveryNote/            ← Phase 10.1–10.9a (COMPLETE)
                Database/
                    DeliveryNotesDbContext.cs
                Repository/
                    IDeliveryNoteRepository.cs
                    DeliveryNoteRepository.cs
                Mapper/
                    DeliveryNoteMapper.cs
                Attachments/
                    AttachmentService.cs
                    AttachmentLimits.cs
                    AttachmentType.cs
                Export/               ← Phase 10.8
                    DeliveryNotePdfLayout.cs
                    DeliveryNoteExcelLayout.cs
                    DeliveryNoteCsvLayout.cs

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
                KpiCardModel.cs       (legacy)
                KpiTileModel.cs       (Phase 10.9a — new tile model)
                ChartBarModel.cs
                ActivityFeedItem.cs

            SheetStore/
                GlassSheet.cs
                SheetStoreHistory/
                    SheetHistory.cs

            ProformaInvoice/
                Specifications/
                SavedInvoice/
                    SavedProformaInvoice.cs
                    ProformaInvoiceSnapshot.cs
                Step6OtherCharges/
                Step7PrintPreview/

            JobOrder/
                Common/
                    JobOrderStatus/
                    JobPriority/
                SavedJobOrder/
                    SavedJobOrder.cs
                    JobOrderSnapshot.cs
                ProductionDetails/
                    ProductionDetails.cs

            DeliveryNote/            ← Phase 10.1
                Common/
                    DeliveryNoteStatus/
                        DeliveryNoteStatus.cs
                        DeliveryNoteStatusMeta.cs
                    DeliveryType/
                        DeliveryType.cs
                        DeliveryTypeMeta.cs
                SavedDeliveryNote/
                    SavedDeliveryNote.cs
                    DeliveryNoteSnapshot.cs
                    DeliveryNoteListItem.cs
                    DeliveryNoteItem.cs
                    DeliveryNoteAttachment.cs

        ViewModels/                   Presentation logic
            ViewModelBase.cs
            MainViewModel.cs

            About/
            AI/

            Dashboard/
                DashboardViewModel.cs         Shell
                DashboardPageBase.cs          Base with ThemeChanged hook
                Overview/
                    OverviewDashboardViewModel.cs
                SheetStore/
                    SheetStoreDashboardViewModel.cs
                ProformaInvoice/
                    PiDashboardViewModel.cs
                JobOrder/                     ← Phase 9.4
                    JoDashboard/
                        JoDashboardViewModel.cs

            SheetStore/
            ProformaInvoice/                  (7 wizard steps + list VM)

            JobOrder/
                JobOrderViewModel.cs
                SavedJobOrdersList/
                    SavedJobOrdersListViewModel.cs
                JobOrderDetail/
                    JobOrderDetailViewModel.cs

            DeliveryNote/                     ← Phase 10.1–10.9a
                DeliveryNoteViewModel.cs      shell
                DeliveryNoteList/
                    DeliveryNoteListViewModel.cs
                    DeliveryNoteRowViewModel.cs
                DeliveryNoteDetail/
                    DeliveryNoteDetailViewModel.cs
                    DnAttachmentRowViewModel.cs

            (placeholders)
                Optimization/
                Production/
                TaxInvoice/
                Reports/

        Views/                        XAML views + code-behind
                                      (mirrors ViewModels structure)

            Splash/
                SplashView.xaml + .cs

            About/
            AI/

            Dashboard/
                DashboardView.xaml + .cs
                Overview/
                    OverviewDashboardView.xaml + .cs
                SheetStore/
                ProformaInvoice/
                JobOrder/

            SheetStore/
            ProformaInvoice/                  (7 wizard steps + list)

            JobOrder/
                JobOrderView.xaml
                SavedJobOrdersList/
                JobOrderDetail/

            DeliveryNote/                     ← Phase 10.1–10.9a
                DeliveryNoteView.xaml         shell
                DeliveryNoteList/
                    DeliveryNoteListView.xaml + .cs
                    JobOrderPicker/
                        JobOrderPickerDialog.xaml + .cs
                DeliveryNoteDetail/
                    DeliveryNoteDetailView.xaml + .cs
                    Attachments/Rename/
                        RenameAttachmentDialog.xaml + .cs

---

## 4. Folder Rules (STRICT)

### 4.1 Per-feature sub-folder pattern

**RULE:** Every feature = its own folder. All related files live
inside that folder together.

    Views/DeliveryNote/DeliveryNoteList/               ← feature folder
        DeliveryNoteListView.xaml                       ← named after feature
        DeliveryNoteListView.xaml.cs

    ViewModels/DeliveryNote/DeliveryNoteList/          ← mirror in ViewModels
        DeliveryNoteListViewModel.cs

**FORBIDDEN:** dumping new files flat into an existing folder that
already has sub-folders established.

    ❌ Views/DeliveryNote/NewThingView.xaml         (flat, not allowed)
    ✅ Views/DeliveryNote/NewThing/NewThingView.xaml (correct)

### 4.2 Model naming

Models get module-prefixed names when in shared folders:

    ✅ Models/Dashboard/KpiTileModel.cs               (module-prefixed)
    ✅ Models/DeliveryNote/SavedDeliveryNote/          (in module folder)
    ❌ Models/Dashboard/TileModel.cs                  (too generic)

### 4.3 Namespaces mirror folders

Folder `ViewModels/DeliveryNote/DeliveryNoteDetail/` →
namespace `ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail`

### 4.4 README.md per feature

Every feature folder in Views/ or Services/ layer should have a
README.md explaining:

- What the feature does
- Key files + responsibilities
- Any gotchas or non-obvious patterns

Use 4-space indented code blocks, NOT triple backticks.

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
- Row-level click routing (visual tree walks)

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

### 5.7 Shared Export Layouts

- Located at `Services/{Module}/Export/`
- Implement `IPdfLayout`, `IExcelLayout`, or `ICsvLayout`
- Constructor takes entity + snapshot
- Engine handles page structure, file dialog, save/open
- Layouts only write content

---

## 6. Database Architecture

### 6.1 Five isolated databases

Each database has its own DbContext + repository. Modules do not
share tables across databases.

    %AppData%\ProGlassAutomation\
        proglass.db            (GlassSheets, SheetHistories)
        specifications.db      (SavedSpecifications)
        proformainvoices.db    (ProformaInvoices)
        joborders.db           (JobOrders)              ← Phase 9
        deliverynotes.db       (DeliveryNotes +         ← Phase 10.1
                                DeliveryNoteItems +
                                DeliveryNoteAttachments)

    Attachment files (not DB):
        %AppData%\ProGlassAutomation\DeliveryNotes\Attachments\
            Images\{DeliveryNoteId}\
            PDFs\{DeliveryNoteId}\

Future databases: `taxinvoices.db` (Phase 19).

### 6.2 Repository pattern

Every data access goes through a repository. ViewModels never touch
DbContext directly.

    View → ViewModel → Repository → DbContext → SQLite

### 6.3 DbContext-per-operation

Do NOT hold DbContext instances as fields. Create a new one per
operation via `using`:

    ✅ using var db = new DeliveryNotesDbContext();
    ❌ private readonly DeliveryNotesDbContext _db = new ...();

### 6.4 JSON snapshot pattern

Complex entities are stored as JSON snapshots in `FullSnapshotJson`
column, with indexed columns for search:

    DeliveryNotes table:
        Id (PK)
        DeliveryNoteId (indexed, UNIQUE)
        JobOrderId (indexed)
        ProformaId (indexed)                ← for Tax Invoice link
        CustomerName (indexed)
        ProjectName (indexed)
        Status (indexed)
        DeliveryType (indexed)
        DeliveryDate (indexed)
        TaxInvoiceId (Phase 10.7 placeholder)
        HasTaxInvoice (Phase 10.7 placeholder)
        FullSnapshotJson (BLOB — DeliveryNoteSnapshot POCO tree)

Search + list use indexed columns.
Detail load uses `DeliveryNoteMapper.DeserializeSnapshot()`.

### 6.5 Migrations

For rapid development: use `EnsureCreatedAsync()` in each
repository's `InitializeAsync`. When schema stabilizes post-Phase 21,
migrate to EF Core Migrations.

**Schema change protocol:** delete old .db file before first run.

---

## 7. Dependency Injection

Simple service locator via static App properties (no DI container):

    App.Config          — AppConfig (persisted to config.json)
    App.Ai              — IAiService (GeminiAiService)
    App.Connectivity    — IConnectivityService
    App.Specs           — ISpecificationsRepository
    App.Invoices        — IProformaInvoiceRepository
    App.Jobs            — IJobOrderRepository
    App.DeliveryNotes   — IDeliveryNoteRepository       ← Phase 10.1

Initialization order in `App.xaml.cs > Application_Startup`:

    1. Config = ConfigService.Load()
    2. DatabaseInitializer.Initialize()          (proglass.db)
    3. Specs = new SpecificationsRepository()    + InitializeAsync()
    4. Invoices = new ProformaInvoiceRepository() + InitializeAsync()
    5. Jobs = new JobOrderRepository()            + InitializeAsync()
    6. DeliveryNotes = new DeliveryNoteRepository() + InitializeAsync()
    7. Ai = new GeminiAiService(Config.Ai)
    8. Connectivity.StartMonitoring()
    9. Show splash
    10. Create MainWindow
    11. Splash.FadeOutAndClose(() => Show + Activate + Focus main)

---

## 8. Navigation Architecture

MainViewModel owns module VMs as public properties. MainWindow uses
ContentControl + DataTemplate binding on `CurrentViewModel`.

    <ContentControl Content="{Binding CurrentViewModel}"/>

    <UserControl.Resources>
        <DataTemplate DataType="{x:Type vm:DashboardViewModel}">
            <views:DashboardView/>
        </DataTemplate>
        <DataTemplate DataType="{x:Type vm:DeliveryNoteViewModel}">
            <views:DeliveryNoteView/>
        </DataTemplate>
    </UserControl.Resources>

### 8.1 Navigate() method

    private void Navigate(object parameter)
    {
        switch (parameter?.ToString())
        {
            case "Dashboard":       CurrentViewModel = DashboardVM;       break;
            case "SheetStore":      CurrentViewModel = SheetStoreVM;      break;
            case "ProformaInvoice": CurrentViewModel = ProformaInvoiceVM; break;
            case "JobOrder":        CurrentViewModel = JobOrderVM;        break;
            case "DeliveryNote":    CurrentViewModel = DeliveryNoteVM;    break;
            // ...
        }
    }

### 8.2 Cross-module callbacks

Sub-modules trigger navigation via Action callbacks wired in
MainViewModel constructor:

    DashboardVM.NavigateTo = target => Navigate(target);
    DashboardVM.TriggerNewInvoice = () => ProformaInvoiceVM.TriggerNewInvoice();

    JobOrderVM.OnRequestOpenDeliveryNote = dnId =>
    {
        Navigate("DeliveryNote");
        _ = DeliveryNoteVM.OpenDeliveryNoteAsync(dnId);
    };

Sub-modules never reference MainViewModel directly.

### 8.3 List ↔ Detail toggle pattern

Most modules use a shell VM with `IsListView` / `IsDetailView`
booleans. The same shell view (e.g. `DeliveryNoteView.xaml`) uses
DataTrigger to swap content between list and detail child views.

---

## 9. Theming System

### 9.1 Brush palette (25 brushes per theme)

Themes defined in `Themes/LightTheme.xaml` + `DarkTheme.xaml`.
Both files expose **IDENTICAL brush keys** — only values differ.

Categories:
- 6 Surfaces
- 5 Text
- 4 Green Accents
- 5 Semantic (Success, Warning, Danger, Info, Neutral)
- 7 Tints (Indigo, Green, Amber, Blue, Red, Gray, Purple)
- 2 Borders

Full palette details in `data/d05-design-system.md`.

### 9.2 Two binding patterns

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

### 9.3 Instant theme swap on VM-driven cards

Binding + Converter does NOT re-evaluate when a DynamicResource
changes. VMs displaying brush-key-driven collections must override
`OnThemeChanged()` in `DashboardPageBase` to reassign the
ObservableCollection reference:

    protected override void OnThemeChanged()
    {
        SalesTiles       = new ObservableCollection<KpiTileModel>(_salesTiles);
        ProductionTiles  = new ObservableCollection<KpiTileModel>(_productionTiles);
        DeliveryTiles    = new ObservableCollection<KpiTileModel>(_deliveryTiles);
        InventoryTiles   = new ObservableCollection<KpiTileModel>(_inventoryTiles);
    }

### 9.4 Chart colors stay hex

Chart bars keep hex color strings intentionally — data-driven visual
identity stays consistent across themes.

---

## 10. Shared Export Engine (Phase 9.5a — Locked Pattern)

### 10.1 Three contracts

Located at `Services/Export/`:

- `IPdfLayout` — implemented by module PDF layouts
- `IExcelLayout` — implemented by module Excel layouts
- `ICsvLayout` — implemented by module CSV layouts

All extend `IDocumentLayout` which provides identity fields for
filename building:

    interface IDocumentLayout
    {
        string DocumentPrefix   // "DN", "JO", "PI"
        string DocumentId       // "DN-2026-001-01"
        string DocumentTitle    // "Delivery Note"
        string CompanyName      // filename part
        string Reference        // project or contract
        DateTime DocumentDate   // filename date
    }

### 10.2 Engine responsibilities

- QuestPDF/ClosedXML/CsvHelper setup
- SaveFileDialog + filename suggestion
- Directory creation + file write
- Open-after-save via shell
- Error wrapping via ExportResult
- PDF: letterhead (ShowOnce) + footer (every page)

### 10.3 Layout responsibilities

- Provide `Letterhead` info (PDF)
- Provide `PageLayout` (A4 portrait/landscape)
- Render content only — no file dialog, no I/O

### 10.4 Consumer pattern (from VM)

    var layout = new DeliveryNotePdfLayout(_entity!, _snapshot!);
    var engine = new PdfExportEngine();
    var request = new ExportRequest
    {
        PromptForLocation = true,
        OpenAfterSave     = true
    };
    var result = await engine.ExportAsync(layout, request);
    ShowExportResult(result, "PDF");

Consistent across every module (PI, JO, DN).

---

## 11. Attachment File Storage (Phase 10.6)

Located at:
    %AppData%\ProGlassAutomation\DeliveryNotes\Attachments\
        Images\{DeliveryNoteId}\
        PDFs\{DeliveryNoteId}\

Managed by `AttachmentService`:

- Files stored on disk, paths in DB
- Max 10 MB per file
- Allowed: JPG, PNG, BMP, GIF, WEBP for Image; PDF only for PDF slot
- Rename dialog before save (frameless glassy modal)
- Delete DN → best-effort delete all attachment files first

Future modules (Job Order attachments, Tax Invoice supporting docs)
should follow the same pattern:
    %AppData%\ProGlassAutomation\{Module}\Attachments\{Type}\{EntityId}\

---

## 12. Key Design Patterns (12 named patterns)

### Pattern 01 — Repository (interface + implementation)

Every data access through repository. Interface for testability.

### Pattern 02 — Async everywhere

All I/O uses async/await. Long-running operations use reentrancy
guards:

    private int _loadingFlag = 0;

    public async Task LoadAsync()
    {
        if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
            return;
        try { /* work */ }
        finally { Interlocked.Exchange(ref _loadingFlag, 0); }
    }

### Pattern 03 — In-memory filtering/sorting

Load once, cache in `List<T>`, filter/sort in `ApplyFilters()`.
Bound collection is separate `ObservableCollection<T>`.

### Pattern 04 — Constructor injection

Cross-step dependencies passed via constructor.

### Pattern 05 — JSON snapshot for complex entities

Whole entity state serialized to JSON blob. Indexed columns for
search. Mapper builds entity + restores state.

### Pattern 06 — Shared export contracts

`IPdfLayout` / `IExcelLayout` / `ICsvLayout` — every module
implements these; engine owns file I/O.

### Pattern 07 — Bulk update (SetAllSilent + FireAllChanged)

For operations touching many rows at once (e.g., 150-row paste):

    _isBulkAdding = true;
    try
    {
        foreach (var item in parsed)
        {
            var row = new GlassItemRow();
            row.SetAllSilent(...);
            ItemRows.Add(row);
        }
    }
    finally { _isBulkAdding = false; }

    TotalRows = ItemRows.Count;
    RenumberRows();
    RecalculateFooter();
    foreach (var row in touchedRows)
        row.FireAllChanged();

### Pattern 08 — Interface contract (IExcelPasteable)

Behavior communicates with ViewModel via interface, never Reflection.

### Pattern 09 — Visual tree walk

Behavior walks visual tree to find `IExcelPasteable` DataContext.

### Pattern 10 — Silent set + batch notify

`SetAllSilent()` + `FireAllChanged()` for bulk performance.

### Pattern 11 — BrushKeyConverter (theme-adaptive VM colors)

VM properties hold brush key names, converter resolves at render time.

### Pattern 12 — Reference equality (MultiBinding for tab active state)

`ReferenceEqualityConverter` compares ActivePage reference to each
tab item. Drives underline highlight via DataTrigger.

### Pattern 13 — Self-healing snapshots (Phase 10.7)

DB-stored derived values that depend on sibling records must be
recomputed on load, never trusted from cache:

    // On LoadAsync:
    var prevMap = await App.DeliveryNotes.GetPreviouslyDeliveredMapAsync(
                            entity.JobOrderId, entity.DeliveryNoteId);
    foreach (var item in entity.Items)
    {
        var key = $"{item.SpecGroupNumber}|{item.SerialNumber}";
        item.PreviouslyDeliveredQty =
            prevMap.TryGetValue(key, out var prev) ? prev : 0;
    }

### Pattern 14 — Grouped ICollectionView (Phase 10.7)

For grid rows that should visually group by a property:

    var view = CollectionViewSource.GetDefaultView(_itemRows);
    view.GroupDescriptions.Add(
        new PropertyGroupDescription(
            nameof(DnItemRowViewModel.SpecGroupName)));
    GroupedItemsView = view;

Then in XAML use `<ItemsControl.GroupStyle>` with `HeaderTemplate`.

### Pattern 15 — Parent-card + nested tiles (Phase 10.9a)

Dashboard layout: parent Border cards contain UniformGrid of nested
tile Borders. Colors driven by `Accent` string on tile model
(`"Green"|"Blue"|"Amber"|"Red"`).

### Pattern 16 — Chat-style scrollable feed (Phase 10.9a)

Activity feed inside a parent card with `MaxHeight` + inner
ScrollViewer with `CanContentScroll="False"` + `PanningMode="VerticalOnly"`
for smooth pixel scrolling.

---

## 13. Data Flow Diagrams

### 13.1 DN Create from JO (cross-module)

    User in JO detail
        ↓
    "Create Delivery Note" button
        ↓
    JobOrderDetailViewModel.CreateDeliveryNoteAsync
        Check history balance
        Confirm dialog
        Get next sequence
        Build DN via DeliveryNoteMapper.FromJobOrder
        Adjust PrevQty if seq > 1
        Insert to DB
        ↓
    OnRequestOpenDeliveryNote(newDnId) callback
        ↓
    MainViewModel.Navigate("DeliveryNote")
        ↓
    DeliveryNoteVM.OpenDeliveryNoteAsync(newDnId)
        Toggle IsDetailView
        DetailVM.LoadAsync(newDnId)

### 13.2 DN Dispatch (Make DN)

    User edits DeliveredQty
        ↓
    Clicks "🚚 Make DN"
        ↓
    ConfirmDispatchAsync
        Validate: has any delivery > 0
        Confirm dialog
        Status = "Dispatched"
        SaveAsync (writes + self-heals + reloads)
        Check totalBalance
        ↓ If balance > 0:
            Create Dummy DN via FromBalance
            Insert to DB
        ↓
    OnRequestBackToList → return to list

### 13.3 Theme swap flow (unchanged from Phase 8.2f)

    User toggles theme
        ↓
    ThemeManager.ApplyTheme(newTheme)
        Resource dictionary swap
        ThemeChanged event fires
        ↓
    DashboardPageBase.OnThemeManagerChanged (Dispatcher.BeginInvoke)
        ↓
    Virtual OnThemeChanged() override
        Reassign all tile ObservableCollections
        ↓
    WPF rebuilds DataTemplates
        BrushKeyConverter re-runs with fresh brush lookups

---

## 14. Extension Rule (5-step protocol for new features)

When adding new feature:

1. **Update PRD** — add feature description to `prd.md`
2. **Add formulas** — any new calculations to `data/d04-calculations.md`
3. **Update architecture** — add folder structure + patterns here
   if new pattern introduced
4. **Follow folder convention** — create per-feature sub-folder:
   `<Layer>/<Module>/<FeatureName>/<Files>`
5. **Update AI prompt** — add to completed modules in
   `prompts/p01-master-prompt.md`

For DN module specifically, also update `delivery.md`.

---

## 15. What NEVER Changes

- Currency: AED (never INR, never USD)
- Country conventions: UAE (5% VAT)
- .NET 8 + WPF (no Blazor, no MAUI, no Uno)
- SQLite (no PostgreSQL, no SQL Server unless explicitly migrated)
- MVVM pattern (no code-behind business logic)
- Per-feature sub-folder structure
- Repository pattern for data access
- Five (soon six) isolated databases
- Balance formula: `Ordered − Delivered` (never sum snapshots)
- Shared export engine — no per-module engine duplication

---

## 16. What NOT to Do

**DO NOT:**

- Reference Views from ViewModels
- Reference ViewModels directly from Behaviors (use interfaces)
- Put business logic in code-behind
- Use `dynamic` in pattern matching (CS8208 error)
- Use `DispatcherFrame.PushFrame` (CS0117 in .NET Core+)
- Use static container fields in behaviors
- Use Reflection when interface is available
- Call `RecalculateFooter()` inside a loop
- Fire `PropertyChanged` inside `_isBulkAdding` block
- Use integer division for SQM
- Omit `Math.Round()` on display values
- Silently swallow exceptions in UI-affecting paths
- Hardcode hex colors in themeable XAML
- Reassign hex to `TintColor`/`BadgeColor` in VMs (use brush keys)
- Skip the `if (_field == value) return;` setter guard
- Remove existing properties when updating a file
- Remove/rename interface methods that other modules depend on
- Sum DN Balance across siblings (double-counts every undelivered unit)
- Trust stored PreviouslyDeliveredQty without self-healing

---

## 17. External Dependencies

- **App.Specs** — `ISpecificationsRepository`
- **App.Invoices** — `IProformaInvoiceRepository`
- **App.Jobs** — `IJobOrderRepository`
- **App.DeliveryNotes** — `IDeliveryNoteRepository`
- **App.Ai** — `IAiService.SendMessageAsync()`
- **App.Connectivity** — `IConnectivityService.StartMonitoring()`

Error handling on all external calls: try/catch with user-facing
MessageBox for critical operations, silent catch only for background
async loads.