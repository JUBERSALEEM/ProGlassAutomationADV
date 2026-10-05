# ProGlass Automation — Master AI Bootstrap Prompt

**Purpose:** Bootstrap any new AI chat session with full ProGlass
Automation context. Copy the 4 PARTS below into a new chat in order.

**When to use:** Any time you start a new AI session and want the
assistant to have complete project context without re-explaining
architecture, conventions, or completed work.

**How to use:**

Option A (single message, faster):
1. Copy all 4 PARTS below into a single message
2. Paste as first message in new Claude/ChatGPT/Gemini chat
3. Replace `[TELL THE AI WHAT TO BUILD NEXT]` with your actual request

Option B (multi-message, safer for very long context):
1. Paste PART 1 as first message → wait for acknowledgment
2. Paste PART 2 → wait for acknowledgment
3. Paste PART 3 → wait for acknowledgment
4. Paste PART 4 → assistant is fully loaded
5. Begin your actual work request

**Example requests after loading:**
- "Build Phase 9 — Job Order Module — design data model first"
- "Fix print crash in PrintableInvoiceViewModel"
- "Add sales trend chart to Overview dashboard"
- "Review my code against rules.md"

---

## AI Behavior Contract (READ FIRST)

You are working on ProGlassAutomation, a WPF (.NET 8) desktop
application for UAE-based glass manufacturing companies.

Before writing any code:
1. Read prd.md — Full product requirements + module map
2. Read architecture.md — Folder structure + patterns
3. Read rules.md — Coding standards + gotchas
4. Read data/d04-calculations.md — All formulas with examples

You MUST:
- Follow folder structure exactly (per-feature sub-folders)
- Place formulas only in `data/d04-calculations.md`
- Use MVVM strict pattern (ViewModelBase + RelayCommand)
- Add validation where needed
- Avoid hidden state
- Guard all property setters: `if (_field == value) return;`
- Give full file when asked — never skip lines with `...`
- Never remove existing code when adding new features

You MUST NOT:
- Invent new folders outside the documented structure
- Modify architecture without explicit instruction
- Change formulas outside `data/d04-calculations.md`
- Add hidden defaults or silent fallbacks
- Use `dynamic` in pattern matching (CS8208 error)
- Use `DispatcherFrame.PushFrame` (CS0117 in .NET Core+)
- Hardcode `#FFFFFF` / `#000000` for theme-adaptive UI
- Silently drop existing properties or methods

When generating code:
- Be deterministic
- No pseudo-code
- No missing types
- No assumptions
- No structural changes

If something is unclear:
**ASK** before implementing.

Return:
1. Updated file path
2. Full file contents
3. Short explanation

---

# PART 1 — Project + Conventions

    ═══════════════════════════════════════════════════════════════
    PROGLASS AUTOMATION — SESSION HANDOFF
    ═══════════════════════════════════════════════════════════════

    Developer: Juber Saleem (India, jubersaleem@gmail.com)
    App: ProGlass Automation — Glass Production Workflow Software
    Stack: WPF (.NET 8, C# 9+), MVVM (ViewModelBase + RelayCommand)
    Database: SQLite + EF Core 8 (3 separate .db files)
    NuGet: CsvHelper, ClosedXML, QuestPDF 2024.12.2, Google Gemini AI
    Features: Light/Dark theme, AI Chat, 15 module placeholders

    Full docs available in Docs/Chat5/ folder:
        Docs/Chat5/prd.md                       Product requirements
        Docs/Chat5/architecture.md              Folder structure + patterns
        Docs/Chat5/rules.md                     Development rules
        Docs/Chat5/data/d04-calculations.md     All formulas

    ═══════════════════════════════════════════════════════════════
    STRICT CONVENTIONS (ALWAYS FOLLOW)
    ═══════════════════════════════════════════════════════════════

    1. FOLDER STRUCTURE — Every feature = its own sub-folder.
       All related files (View + VM + Models) live inside that folder.
       Never dump new files flat into an existing folder that already
       has sub-folders established.

       Example pattern:
         Views/Dashboard/Overview/OverviewDashboardView.xaml
         Views/Dashboard/Overview/OverviewDashboardView.xaml.cs
         ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs
         Models/Dashboard/KpiCardModel.cs  (module-prefixed names)

    2. ALWAYS confirm folder structure before writing code for new
       features.

    3. Full file updates when asked — never skip lines with "..."
       comments. Give the complete file byte-for-byte.

    4. NEVER remove existing code when adding new features.
       Even legacy/backward-compat properties stay unless explicitly
       told to remove them.

    5. Guard ALL property setters:
         if (_field == value) return;

    6. Async everywhere with reentrancy guards
       (Interlocked.CompareExchange).

    7. README.md per feature (in Views or Services layer).

    8. Mini-commit approach: build one feature, test, then next.

    9. When user says "use your recommendation" → just write the code.

    10. When user shares current file + says "update" → give back
        complete updated file. NEVER assume you have latest from
        memory — always check what user shared vs what you have.

    11. Before writing files check chat history for existing versions.
        If unsure whether you have the latest, ASK the user to share.
        Never silently drop properties or methods.

    ═══════════════════════════════════════════════════════════════
    COMMIT MESSAGE FORMAT (STRICT — DETAILED FORMAT)
    ═══════════════════════════════════════════════════════════════

    First line:
      Module: Area: Phase-X.XX: type(scope): short description

    Then blank line, then:
    - 2-4 sentence summary paragraph explaining WHY and WHAT
    - Blank line
    - Structured sections in ALL CAPS with colon
      (VISUAL CHANGES, ARCHITECTURE CHANGES, ROOT CAUSE, FIX APPROACH,
       MODELS UPDATED, VIEWMODELS UPDATED, VIEWS UPDATED, DESIGN
       TOKENS, BEHAVIOR AFTER FIX, BACKWARD COMPATIBILITY, etc.)
    - Each section: bullet list with dashes
    - Files modified: section at bottom with sub-bullets using * for
      each change
    - Files added: / Files deleted: if applicable
    - End with: Phase X.X COMPLETE. + Next: line

    Types:
      feat       New feature or capability
      fix        Bug fix
      refactor   Code restructuring without behavior change
      style      Cosmetic UI changes (colors, spacing, fonts)
      docs       Documentation only
      chore      Build, tooling, config
      perf       Performance improvement

    For non-phase work use normal commit without Phase-X.XX segment.

    ═══════════════════════════════════════════════════════════════
    BUSINESS DOMAIN — GLASS PRODUCTION
    ═══════════════════════════════════════════════════════════════

    Workflow: Raw sheets → Cut → Process → Temper → Pair → Deliver → Invoice
    Pricing: Per SQM = (W×H/1,000,000) × Qty × PricePerSqm
    Min SQM: 0.5 sqm per piece
    Currency: AED (UAE Dirham), VAT: 5% (toggleable)

    LM Formulas:
    - SGU/DGU: (W+H)/1000 × 2 × Qty
    - LAM: (W+H)/1000 × 4 × Qty

    Polish Sub-Types: All Side, Both Height, Both Width, As Per DWG, Custom
    Rate Auto-Adjust: SQM >= 4 → EffectiveRate, SQM < 4 → BaseRate

    ═══════════════════════════════════════════════════════════════
    DATABASE ARCHITECTURE (3 SEPARATE SQLITE FILES)
    ═══════════════════════════════════════════════════════════════

    1. proglass.db — Main (GlassSheets, SheetHistories)
       Location: %AppData%\ProGlassAutomation\proglass.db
    2. specifications.db — Saved glass specs (SavedSpecifications)
    3. proformainvoices.db — Saved invoices (ProformaInvoices with
       indexed columns + FullSnapshotJson blob)

    All initialized in App.xaml.cs:
      App.Specs = new SpecificationsRepository();
      await App.Specs.InitializeAsync();
      App.Invoices = new ProformaInvoiceRepository();
      await App.Invoices.InitializeAsync();

    Service locator (static App properties):
      App.Config       — AppConfig (persisted to config.json)
      App.Ai           — IAiService (GeminiAiService)
      App.Connectivity — IConnectivityService
      App.Specs        — ISpecificationsRepository
      App.Invoices     — IProformaInvoiceRepository

---

# PART 2 — Completed Modules Summary

    ═══════════════════════════════════════════════════════════════
    COMPLETED MODULES
    ═══════════════════════════════════════════════════════════════

    MODULE 1 — App Shell:
    - Splash window (light theme, green accent, custom glass-stack
      logo, feature preview row, rotating loading messages, shimmer
      animation, glass-style footer info cards)
    - Hamburger menu navigation (5 groups: MAIN, INVENTORY,
      SALES & REPORTS, PRODUCTION, DISPATCH)
    - AI chat panel (floating, Gemini-powered)
    - Light/Dark theme toggle (ThemeManager static class with
      ThemeChanged event)
    - About panel
    - Splash-to-main handoff fix (Topmost released before fade,
      Show + Activate + Focus + Topmost yank trick)

    MODULE 2 — Sheet Store (Phase 2-3):
    - Full CRUD on GlassSheet entity
    - Buy/Use stock operations
    - CSV/Excel/PDF export
    - History tracking (SheetHistory table with SheetId + Action +
      Details)
    - Backup service (last 7 database copies on exit)
    - 4 filter dropdowns, 10 sort presets, live search
    - Stat cards: Buying Value, Selling Value, Profit

    MODULE 3 — AI Infrastructure (Phase 4-6):
    - GeminiAiService with API key config
    - ConnectivityService (online/offline monitoring)
    - ConfigService persists to %AppData%\ProGlassAutomation\
      config.json
    - AiChatViewModel + AiChatView (floating panel)

    MODULE 4 — Proforma Invoice (Phase 7 — 22 sub-phases, COMPLETE):

      LIST-FIRST ARCHITECTURE (Phase 7.19):
      - Module landing page = SavedInvoicesList (not wizard)
      - IsListView / IsWizardView toggle on ProformaInvoiceViewModel
      - "+ NEW INVOICE" button → resets all steps, opens wizard
      - "Invoice List" button in sidebar TOP + bottom nav for
        returning to list (Phase 7.19c — prominent accent button)
      - Right-click context menu: Open, Duplicate, Export PDF,
        Export Excel, Change Status (sub-menu), Convert to Job
        Order (placeholder), Delete
      - Right-click menu built in code-behind (OnRowRightClick)

      WIZARD (Steps 1-7):
      - Step 1: Company Setup (CompanyName, TRN, Location,
        ProformaId, Dates, Status)
      - Step 2: Project Specs (ProjectName, ContractNo, SiteDelivery,
        LPO)
      - Step 3: Client Profile (Customer, TRN, Sales Rep, PreparedBy,
        Address, Contact)
      - Step 4: Pricing Estimator (SGU/DGU/LAM, Guardian/AGC catalog,
        auto-spec names)
      - Step 5: DataGrid (SpecGroups + GlassItemRows, W1/H1/W2/H2
        split columns per Phase 7.19d, VirtualizingStackPanel)
      - Step 6: Other Charges (MetricHub + 8 predefined + Polish +
        Wastages + Custom)
      - Step 7: Print Preview (A4, T&C editor, Summary, Print/Save/
        PDF/Excel/CSV)

      KEY FEATURES:
      - SpecDisplayName computed property (shows spec name, not
        "Spec Group N")
      - PreparedByName/Status auto-populate from Step 3/1
        (overridable)
      - PdfExportContext shared across PDF/Excel/CSV exports
      - BuildExportContextFromSnapshot for direct export from list
      - 6 UAE default Terms & Conditions clauses (editable,
        reorderable)
      - Smart save with duplicate detection (Update / Auto-increment
        / Cancel)
      - Full restore from JSON snapshot (all 7 steps + T&C +
        Prepared By)

    MODULE 5 — Dashboard (Phase 8.1-8.2f, COMPLETE):

      Multi-page dashboard shell with underline tab navigation.
      Each module gets its own dashboard sub-page.

      ARCHITECTURE:
      - DashboardViewModel (shell) owns List<DashboardPageBase>
        Pages + ActivePage property
      - DashboardPageBase abstract class: IsLoading, HasError,
        TabTitle, TabIcon, LoadAsync with reentrancy guard,
        LoadCoreAsync abstract, OnThemeChanged virtual hook
        (Phase 8.2f)
      - Lazy loading: sub-page LoadAsync fires only when tab is
        selected
      - ContentPresenter with DataTemplate auto-selects the right
        View
      - ReferenceEqualityConverter drives active tab underline
        highlight

      SUB-PAGES (all extend DashboardPageBase):
      - OverviewDashboardViewModel + View — Total Sales hero card
        (dark green gradient), 3 KPI cards (Total Invoices,
        Inventory Value, Low Stock Alerts), Monthly Sales bar chart
        last 6 months, Recent Activity Feed merged from invoices +
        sheet history, Quick Actions (New Invoice, Saved Invoices,
        View Stock, Export)
      - SheetStoreDashboardViewModel + View — 4 KPI cards (Sheet
        Types, Inventory Value, Available SQM, Low/Out Stock),
        Category SQM chart
      - PiDashboardViewModel + View — 4 KPI cards by status,
        Status Distribution bars, Top 5 Customers bars

      NAVIGATION CALLBACKS (Phase 8.2):
      - DashboardVM.NavigateTo = target => MainViewModel.Navigate
        (target)
      - DashboardVM.TriggerNewInvoice = () =>
        ProformaInvoiceVM.TriggerNewInvoice()
      - ProformaInvoiceViewModel.TriggerNewInvoice() public method
        wraps private StartNewInvoice() so quick action can open
        wizard

      RECENT ACTIVITY FEED:
      - Merges last 10 invoices (by CreatedAt/UpdatedAt) + last 10
        sheet history rows (from SheetHistories table via
        AppDbContext)
      - ActivityFeedItem model: Icon, Title, SubTitle, ValueDisplay,
        StatusLabel, StatusColor, TintColor, ModuleTag, SortDate
      - Ordered by SortDate descending, taken 10

      DESIGN SYSTEM (Phase 8.2c-8.2f — completely rebuilt):
      - Complete 25-brush theme palette (was 8)
      - Green accent (was blue) matching splash logo
      - Light theme: white cards on #F9FAFB, deep green #0F4C3A
        accent
      - Dark theme: #1E293B cards on #0B1220, bright green #22C55E
        accent
      - BrushKeyConverter resolves VM string properties like
        "AccentBrush" to actual theme brushes at render time —
        swaps with theme
      - Chart bars keep hex colors intentionally (data-driven
        identity)
            - Instant theme swap via ThemeManager.ThemeChanged event
        subscription in DashboardPageBase — sub-pages reassign
        ObservableCollections to force DataTemplate rebuild

    MODULE 6 — Job Order (Phase 9.1-9.3, COMPLETE):

      Convert-from-PI architecture (Path B — no wizard duplication):
      - 1 PI = 1 Job Order (no splitting)
      - Numbering: JOB-YYYY-NNN (derived from PI-YYYY-NNN)
      - Status: Draft → Released → InProduction → Completed → Delivered
      - Priority: High / Medium / Low
      - Embedded PI snapshot (immutable) + Production details (mutable)
      - 4th SQLite database: joborders.db
      - App.Jobs = IJobOrderRepository (initialized in App.xaml.cs)

      Foundation (Phase 9.1):
      - Models/JobOrder/ — enums (JobOrderStatus + JobPriority) with
        meta helpers, SavedJobOrder EF entity, JobOrderSnapshot JSON
        DTO, ProductionDetails POCO
      - Services/JobOrder/ — JobOrdersDbContext, IJobOrderRepository +
        implementation, JobOrderMapper (BuildFromProformaInvoice +
        deep-clone via JSON round-trip)

      List-first landing page (Phase 9.2):
      - 4 KPI cards (Total, In Production, Completed, Overdue)
      - Purple + blue GradientHeroCard controls for top 2 KPIs
      - Priority-styled row cards (4px left accent bar via
        BrushKeyConverter)
      - Status + priority pills (theme-adaptive via brush keys)
      - Filter bar: search + status + priority + sort + clear
      - Right-click: Open, Change Status (sub-menu), Change Priority
        (sub-menu), Delete

      Detail view (Phase 9.3 — two-column layout):
      - Left (2*): read-only PI snapshot — Company, Client, Project
        cards + glass items spec group tables (8-column layout
        matching Phase 7.19d) + Summary card with grand total
      - Right (*): editable production panel — Status/Priority
        dropdowns, Delivery Date picker, Site Delivery contact/phone/
        address, Assigned To, Cutting Instructions, Drawing Reference,
        Internal Notes, Save/Back buttons, audit trail footer
      - Purple GradientHeroCard header with JOB ID + grand total

      PI conversion (Phase 9.3):
      - Right-click PI → "Convert to Job Order" (was grayed out,
        now functional in SavedInvoicesListView.xaml.cs)
      - Deep-clones PI snapshot via JSON round-trip (immutable record)
      - Duplicate detection + auto-increment JOB ID
      - Passes GrandTotalVatInclusive from PI entity as override
      - Success message with new JOB ID

---

# PART 3 — File Locations + Patterns + Gotchas

    ═══════════════════════════════════════════════════════════════
    KEY FILE LOCATIONS
    ═══════════════════════════════════════════════════════════════

    APP STARTUP:
    - App.xaml (converter + theme dictionary registrations)
    - App.xaml.cs (startup: Config → DBs → Specs → Invoices → AI →
      Splash)
    - MainWindow.xaml + .cs (shell hosting CurrentViewModel)
    - ViewModels/MainViewModel.cs (navigation + all module VMs +
      DashboardVM.NavigateTo/TriggerNewInvoice wiring)

    THEMES:
    - Themes/LightTheme.xaml (25 brushes)
    - Themes/DarkTheme.xaml (25 brushes, same keys)
    - Services/ThemeManager.cs (ApplyTheme + ThemeChanged event)

    SPLASH:
    - Views/Splash/SplashView.xaml (light theme, glass logo,
      features row, rotating messages, footer info cards)
    - Views/Splash/SplashView.xaml.cs (FadeOutAndClose releases
      Topmost first, then invokes callback, then closes)

    DASHBOARD:
    - ViewModels/Dashboard/DashboardViewModel.cs (shell)
    - ViewModels/Dashboard/DashboardPageBase.cs (base with
      ThemeChanged subscription + OnThemeChanged virtual hook)
    - ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs
    - ViewModels/Dashboard/SheetStore/SheetStoreDashboardViewModel.cs
    - ViewModels/Dashboard/ProformaInvoice/PiDashboardViewModel.cs
    - Views/Dashboard/DashboardView.xaml (+ .cs)
    - Views/Dashboard/Overview/OverviewDashboardView.xaml (+ .cs)
    - Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml (+ .cs)
    - Views/Dashboard/ProformaInvoice/PiDashboardView.xaml (+ .cs)
    - Models/Dashboard/KpiCardModel.cs
    - Models/Dashboard/ChartBarModel.cs
    - Models/Dashboard/ActivityFeedItem.cs

    PROFORMA INVOICE:
    - ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs
      (7 step VMs + IsListView/IsWizardView + SavedInvoicesListVM +
       TriggerNewInvoice public method)
    - Views/ProformaInvoice/ProformaInvoiceView.xaml (list ↔ wizard
      toggle, sidebar with prominent Invoice List button)
    - ViewModels/ProformaInvoice/SavedInvoicesList/
      SavedInvoicesListViewModel.cs
    - Views/ProformaInvoice/SavedInvoicesList/
      SavedInvoicesListView.xaml (+ .xaml.cs with OnRowRightClick
      code-behind context menu)
    - Step1-7 folders under both ViewModels + Views

    STEP 5 — DATAGRID:
    - ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs
      Contains three classes:
        GlassItemRow (row model with W1/H1/W2/H2 ints,
                      SetAllSilent + FireAllChanged pattern)
        SpecGroup (implements IExcelPasteable, has _isBulkAdding,
                   OnRowChanged for rate auto-adjust)
        DataGridViewModel (owns ObservableCollection<SpecGroup>)
    - Behaviors/DataGridNavigationBehavior.cs (IExcelPasteable
      interface + Excel-like nav + smart paste)

    STEP 6 — OTHER CHARGES:
    - ViewModels/ProformaInvoice/Step6OtherCharges/
      OtherChargesViewModel.cs
    - MetricDiscoveryHubViewModel.cs (9 GridSourceOptions)
    - Wastages/WastagesSectionViewModel.cs
    - CustomCharges/CustomChargesSectionViewModel.cs
    - Models/ProformaInvoice/Step6OtherCharges/PolishSubType.cs
      (enum + PolishSubTypeMeta in same file — extracted from
      nested class due to XamlWriter crash)

    STEP 7 — PRINT PREVIEW:
    - ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/
      PrintableInvoiceViewModel.cs
      (BIG FILE ~800 lines — Print/Save/Load/ExportPdf/Excel/CSV
       commands + code-built FixedDocument BuildSpecTable has
       10-column layout per Phase 7.19d)
    - Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/
      PrintableInvoiceView.xaml (on-screen A4 preview with
      10-column spec table)

    SAVE/LOAD (Proforma):
    - Services/ProformaInvoice/ProformaInvoiceRepository/
      (DbContext, IRepo, Repo, Mapper with
      RestoreWizardFromSnapshot)
    - Models/ProformaInvoice/SavedInvoice/
      (SavedProformaInvoice entity + ProformaInvoiceSnapshot POCO
      tree)

    EXPORT (Proforma):
    - Services/ProformaInvoice/PdfExport/
      (ProformaInvoicePdfService + PdfExportContext +
      PdfExportContextBuilder)
      PdfGlassRow has W1/H1/W2/H2 ints + SizeW1H1/W2H2 strings for
      backward compat
    - Services/ProformaInvoice/InvoiceExportImport/
      (InvoiceExcelExportService + InvoiceCsvExportService)

    SHEET STORE:
    - Services/SheetStore/ISheetStoreRepository.cs +
      SheetStoreRepository.cs
    - Models/SheetStore/GlassSheet.cs (Category, ThicknessMm, Color,
      WidthMm, HeightMm, Supplier, Stock, Used, prices; computed:
      SqmPerSheet, Balance, TotalSqmBalance, BalanceValue, etc.)
    - Models/SheetStore/SheetStoreHistory/SheetHistory.cs (SheetId,
      Action, Details, QuantityChanged, ValueChanged, UserName,
      Timestamp, ActionColor computed)

    DATABASE:
    - Services/Database/AppDbContext.cs (DbSet<GlassSheet> +
      DbSet<SheetHistory>)
    - Services/Database/DatabaseBackup/BackupService.cs

    GLOBAL SERVICES:
    - Commands/RelayCommand.cs
    - ViewModels/ViewModelBase.cs (INotifyPropertyChanged base)
    - Services/ThemeManager.cs (static, ThemeChanged event)
    - Services/ConfigService.cs (Load/Save AppConfig JSON)

    CONVERTERS (all in App.xaml):
    - BooleanToVisibilityConverter (built-in) key:
      BoolToVisibilityConverter
    - ColorStringConverter (hex → SolidColorBrush)
    - DecimalIsPositiveConverter
    - ReferenceEqualityConverter (MultiBinding, object reference
      equality)
    - ScaleConverter (normalized double × parameter → pixel height)
    - InverseBoolToVisibilityConverter (true → Collapsed)
    - BrushKeyConverter (Phase 8.2e — string brush key → theme brush)

    ═══════════════════════════════════════════════════════════════
    IMPORTANT PATTERNS & GOTCHAS
    ═══════════════════════════════════════════════════════════════

    1. PolishSubTypeMeta extracted from nested class (XamlWriter
       crash otherwise).

    2. QuestPDF 2024.12.2: use .PaddingHorizontal() +
       .PaddingVertical() NOT .Padding(h, v) — causes CS1503 Unit
       error.

    3. PrintableInvoiceView.xaml (on-screen A4 preview) and
       BuildFullContentStack() in ViewModel (code-built print) are
       TWO separate rendering paths — BOTH need updating for any
       spec table layout changes.

    4. PreparedByName/Status use "override pattern" — getter falls
       back to wizard VM data unless user typed a local override.

    5. ContextMenu bindings don't work reliably in XAML — use code-
       behind OnRowRightClick for reliable command binding.

    6. SpecDisplayName on SpecGroup handles spec name fallback in C#
       (not XAML MultiBinding which crashes with TargetNullValue).

    7. BuildExportContextFromSnapshot reconstructs PdfExportContext
       from JSON snapshot without needing live wizard VMs.

    8. README.md uses 4-space indented code blocks (not triple
       backticks).

    9. ColorStringConverter must return SolidColorBrush not Color —
       XAML Background expects Brush.

    10. WPF Binding + IValueConverter does NOT re-evaluate when a
        DynamicResource changes. For theme-adaptive card colors from
        VM strings: reassign the ObservableCollection to force
        DataTemplate rebuild, OR use BrushKeyConverter that reads
        from Application.Current.Resources. Dashboard uses BOTH.

    11. Splash Topmost=True must be released BEFORE fade-out
        animation otherwise main window opens behind splash.

    12. Main window handoff sequence:
        a) MainWindow = mainWindow (before Show)
        b) mainWindow.Show()
        c) mainWindow.Activate()
        d) mainWindow.Focus()
        e) mainWindow.Topmost = true; mainWindow.Topmost = false

    13. All ViewModel property setters MUST have
        "if (_field == value) return" guard as first line.

    14. Models/ can be flat inside module folder, but ViewModels/ +
        Views/ should have per-feature sub-folders.

    ═══════════════════════════════════════════════════════════════
    FORBIDDEN LIST (QUICK REFERENCE)
    ═══════════════════════════════════════════════════════════════

    FORBIDDEN                              REASON
    dynamic in pattern (is dynamic x)      CS8208 compiler error
    DispatcherFrame.PushFrame              CS0117 not in .NET Core+
    static _container in behaviors         Multi-instance state conflict
    Code logic in code-behind              Breaks MVVM
    Direct VM reference in behavior        Tight coupling
    Reflection when interface exists       Fragile, use interface
    RecalculateFooter inside a loop        Performance
    PropertyChanged inside bulk loop       Performance
    Integer division for SQM               Precision loss
    Math.Round omitted on display          Wrong display values
    Missing setter equality guard          Infinite bindings, perf
    Hardcoded #FFFFFF in themeable XAML    Breaks theme swap
    Hex string in TintColor/BadgeColor     Breaks theme swap (use keys)
    .Padding(h, v) in QuestPDF 2024.12.2   CS1503 Unit error
    XamlWriter.Save() on nested types      Runtime crash
    Nested public classes                  XamlWriter crash
    double/float for money or SQM          Precision loss
    Silent removal of existing properties  Breaks other modules

---

# PART 4 — Current State + Roadmap

    ═══════════════════════════════════════════════════════════════
    CURRENT STATE — PHASE COMPLETE
    ═══════════════════════════════════════════════════════════════

    Completed:
    - Phase 1-3: App shell + Sheet Store
    - Phase 4-6: AI infrastructure + Gemini integration
    - Phase 7 (all 22 sub-phases): Proforma Invoice module complete
      including 7.19a-e polish rounds
    - Phase 8.1: Dashboard multi-page shell + 3 sub-dashboards
    - Phase 8.2: Quick actions + Recent activity feed
    - Phase 8.2a-f: Progressive UI polish
        8.2a: Modern UI (ClearType, borders, hero card)
        8.2b: Tinted card backgrounds + gradient hero
        8.2c: Full DynamicResource migration + 25-brush palette +
              splash light theme + splash-to-main handoff fix
        8.2d: Splash custom glass logo + feature row + rotating
              messages + shimmer + footer info cards
        8.2e: BrushKeyConverter + brush key indirection
        8.2f: Instant theme swap via ThemeManager.ThemeChanged
    - Phase 8.3: Design system foundation
        Themes/Styles/ — 4 shared style dictionaries
          TypographyStyles.xaml (14 text styles)
          IconBadgeStyles.xaml (10 icon badge styles)
          PillStyles.xaml (16 pill/tag styles)
          CardStyles.xaml (13 card styles + 5 hero gradients)
        Controls/ — 3 reusable custom UserControls
          Sparkline/SparklineControl (mini line chart)
          MiniBarChart/MiniBarChartControl (mini bar chart)
          GradientHeroCard/GradientHeroCard (hero card with gradient)
        BrushKeyConverter registered in BOTH theme files
          (LightTheme.xaml + DarkTheme.xaml) so CardStyles.xaml can
          reference it via StaticResource during merged dictionary
          load phase
        Overview Dashboard updated to demonstrate patterns
          (GradientHeroCard + SparklineControl)
    - Phase 9.1: Job Order foundation (models + services + mapper)
    - Phase 9.2: Job Order list view + shell controller
    - Phase 9.3: Job Order detail view + PI conversion wiring
        Full two-column detail view: read-only PI snapshot with
          glass items tables (left) + editable production panel (right)
        "Convert to Job Order" on PI right-click menu now functional
        Deep-clones PI snapshot via JSON round-trip (immutable record)
        Glass items table shows 8 columns matching Phase 7.19d layout

    Foundational documentation in Docs/Chat5/ folder:
    - prd.md, architecture.md, rules.md, data/d04-calculations.md,
      data/d05-design-system.md, prompts/p01-master-prompt.md

    ═══════════════════════════════════════════════════════════════
    NEW IN LAST SESSION (Chat 5 — Phase 8.3 + Phase 9)
    ═══════════════════════════════════════════════════════════════

    PHASE 8.3 — DESIGN SYSTEM FOUNDATION:

    New folders:
      Themes/Styles/         4 ResourceDictionary XAML files + .cs stubs
      Controls/Sparkline/    SparklineControl UserControl
      Controls/MiniBarChart/ MiniBarChartControl UserControl
      Controls/GradientHeroCard/ GradientHeroCard UserControl

    Key architectural decisions:
    - BrushKeyConverter must be registered in BOTH theme files
      (Light + Dark), NOT in CardStyles.xaml locally — causes
      "incompatible type" resource conflict if registered in both
      App.xaml body AND merged dictionary
    - UserControl code-behind files must NOT specify base class
      (e.g. `: UserControl`) — let XAML-generated partial class
      own the base class declaration (prevents CS0263)
    - ResourceDictionary code-behind stubs use `public partial class`
      with no explicit base class
    - Controls use DependencyProperties for all bindable state
    - SparklineControl accepts comma-separated string OR
      IEnumerable<double/decimal/int> for Values
    - GradientHeroCard uses FindName("RootBorder") + GradientStops[0/1]
      to update gradient colors at runtime (x:Name on GradientStop
      inside Freezable is unreliable)

    PHASE 9 — JOB ORDER MODULE (3 sessions):

    New database: joborders.db at %AppData%\ProGlassAutomation/
    New service: App.Jobs (IJobOrderRepository)

    Folder structure:
      Models/JobOrder/
          Common/JobOrderStatus/    enum + meta helper
          Common/JobPriority/       enum + meta helper
          SavedJobOrder/            EF entity + JSON snapshot
          ProductionDetails/        delivery + priority + notes

      Services/JobOrder/
          Database/                 JobOrdersDbContext
          Repository/               IJobOrderRepository + implementation
          Mapper/                   JobOrderMapper (PI → JO conversion)

      ViewModels/JobOrder/
          JobOrderViewModel.cs      shell (list ↔ detail toggle)
          SavedJobOrdersList/       list VM + row VM
          JobOrderDetail/           detail VM

      Views/JobOrder/
          JobOrderView.xaml         shell
          SavedJobOrdersList/       list view + code-behind
          JobOrderDetail/           detail view

    ═══════════════════════════════════════════════════════════════
    IMPORTANT GOTCHAS DISCOVERED IN LAST SESSION
    ═══════════════════════════════════════════════════════════════

    15. BrushKeyConverter must be registered in BOTH theme files
        (LightTheme.xaml + DarkTheme.xaml) — NOT in CardStyles.xaml
        locally. Local registration causes "incompatible type"
        resource conflict (XDG-0001 error).

    16. UserControl code-behind must NOT specify `: UserControl`
        base class — XAML-generated partial class already declares
        it. Specifying it again causes CS0263.

    17. Same rule applies to ResourceDictionary code-behind stubs —
        do NOT write `: ResourceDictionary`. Just `public partial class`.

    18. DynamicResource CANNOT be used as Converter value in Binding
        markup extension. Converter requires StaticResource. Fix:
        register the converter in the theme dictionary that loads
        BEFORE the style dictionary that uses it.

    19. `x:Name` on `GradientStop` inside `LinearGradientBrush`
        does NOT reliably generate backing fields in .NET WPF
        (Freezable quirk). Use `FindName("RootBorder")` on parent
        + `lgb.GradientStops[0]` / `[1]` indexer instead.

    20. `;assembly=ProGlassAutomation` on xmlns for same-assembly
        UserControls makes WPF look in wrong assembly. Omit
        `;assembly=` for controls in the same project.

    21. When ProformaInvoiceSnapshot doesn't store a computed field
        (like GrandTotalVatInclusive), pass the value as a separate
        parameter from the SavedProformaInvoice entity's indexed
        column — don't try to recalculate from snapshot inputs.

    22. Old placeholder ViewModels (empty stubs like
        `public class JobOrderViewModel : ViewModelBase { }`) must
        be DELETED when replaced with real implementation — leaving
        both causes namespace/class conflicts.

    ═══════════════════════════════════════════════════════════════
    KEY FILE LOCATIONS — NEW IN LAST SESSION
    ═══════════════════════════════════════════════════════════════

    DESIGN SYSTEM:
    - Themes/Styles/TypographyStyles.xaml + .cs
    - Themes/Styles/IconBadgeStyles.xaml + .cs
    - Themes/Styles/PillStyles.xaml + .cs
    - Themes/Styles/CardStyles.xaml + .cs  (NO local BrushKeyConverter)
    - Themes/Styles/README.md
    - Controls/Sparkline/SparklineControl.xaml + .cs
    - Controls/MiniBarChart/MiniBarChartControl.xaml + .cs
    - Controls/GradientHeroCard/GradientHeroCard.xaml + .cs
    - Controls/README.md
    - Docs/Chat5/data/d05-design-system.md

    JOB ORDER:
    - Models/JobOrder/Common/JobOrderStatus/JobOrderStatus.cs
    - Models/JobOrder/Common/JobOrderStatus/JobOrderStatusMeta.cs
    - Models/JobOrder/Common/JobPriority/JobPriority.cs
    - Models/JobOrder/Common/JobPriority/JobPriorityMeta.cs
    - Models/JobOrder/SavedJobOrder/SavedJobOrder.cs
    - Models/JobOrder/SavedJobOrder/JobOrderSnapshot.cs
    - Models/JobOrder/ProductionDetails/ProductionDetails.cs
    - Services/JobOrder/Database/JobOrdersDbContext.cs
    - Services/JobOrder/Repository/IJobOrderRepository.cs
    - Services/JobOrder/Repository/JobOrderRepository.cs
    - Services/JobOrder/Mapper/JobOrderMapper.cs
    - ViewModels/JobOrder/JobOrderViewModel.cs
    - ViewModels/JobOrder/SavedJobOrdersList/
        SavedJobOrdersListViewModel.cs (includes JobOrderRowViewModel)
    - ViewModels/JobOrder/JobOrderDetail/JobOrderDetailViewModel.cs
    - Views/JobOrder/JobOrderView.xaml + .cs
    - Views/JobOrder/SavedJobOrdersList/
        SavedJobOrdersListView.xaml + .cs (OnRowRightClick menu)
    - Views/JobOrder/JobOrderDetail/JobOrderDetailView.xaml + .cs

    ═══════════════════════════════════════════════════════════════
    ROADMAP — WHAT'S NEXT
    ═══════════════════════════════════════════════════════════════

    IMMEDIATE NEXT: Phase 9.4 — Job Order Dashboard sub-page

    Add 4th tab to Dashboard (Overview | Sheet Store | PI | Job Order)
    with KPI cards, status distribution chart, priority breakdown,
    recent job orders feed. Pattern established by existing 3 sub-pages.

    After Phase 9.4:
    - Phase 9.5: Job Order PDF/Excel export (factory floor printout)
    - Phase 10: Cutting Module
    - Phase 11-15: Production Modules (Holes, Polish, Edge, Mitring,
      Tempering)
    - Phase 16-17: Dispatch (Pairing, Doubling/Lamination)
    - Phase 18: Delivery Note
    - Phase 19: Tax Invoice
    - Phase 20: Sales Report
    - Phase 21: Optimization

    ═══════════════════════════════════════════════════════════════
    KNOWN WORKING PATTERNS (COPY THESE EXACTLY)
    ═══════════════════════════════════════════════════════════════

    Correct property pattern (equality guard MANDATORY):

        private int _w1;
        public int W1
        {
            get => _w1;
            set
            {
                if (_w1 == value) return;
                _w1 = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

    Correct bulk paste pattern:

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
        finally
        {
            _isBulkAdding = false;
        }
        RenumberRows();
        RecalculateFooter();
        foreach (var row in touchedRows)
            row.FireAllChanged();

    Correct async load with reentrancy guard:

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

    Correct theme brush key on VM (Phase 8.2e):

        var kpi = new KpiCardModel
        {
            Icon        = "📄",
            Title       = "Total Invoices",
            BadgeColor  = "AccentBrush",        // brush KEY not hex
            TintColor   = "TintIndigoBrush",    // brush KEY not hex
            DisplayValue = totalInvoices.ToString("N0")
        };

    Correct theme swap re-notify (Phase 8.2f):

        protected override void OnThemeChanged()
        {
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
        }

    ═══════════════════════════════════════════════════════════════
    COMMUNICATION PREFERENCES
    ═══════════════════════════════════════════════════════════════

    - When user says "use your recommendation" → just write the code
    - Always give FULL file when asked — never skip lines
    - When user shares file + says "update" → give complete updated
      file
    - Follow per-feature sub-folder pattern STRICTLY
    - Include README.md for every new feature
    - Ask before architectural decisions ONLY if truly ambiguous
    - Give commit messages matching the detailed format
    - When giving find-and-replace, show exact text to find and
      replace
    - Before writing files, verify chat history — ask user to share
      current version if unsure
    - NEVER remove existing code when adding new features
    - NEVER specify base class on UserControl/ResourceDictionary
      code-behind partial classes

    ═══════════════════════════════════════════════════════════════
    CONTEXT LOADED. READY TO CONTINUE.
    ═══════════════════════════════════════════════════════════════

    I have full ProGlass Automation context including Phase 8.3
    design system + Phase 9 Job Order module.

    CURRENT STATUS: [TELL THE AI WHAT TO BUILD NEXT]

---

## Maintenance

**When to update this prompt:**
- After completing any phase (add to PART 2 completed modules list)
- When adding new gotchas discovered (add to PART 3 patterns)
- When new file locations become important (add to PART 3)
- When architectural patterns change (update PART 3 working patterns)
- Never delete history — append only

**When to bump version:**
- Every major phase completion (Phase 9, Phase 10, etc.)
- Bump minor version for polish rounds (v1.1, v1.2)

**Version log:**
- v1.0 (2026-Q1): Initial 4-part bootstrap covering Phases 1-8.2f
- v2.0 (2026-Q1): Merged authoritative version combining Chat 1,
  Chat 3, Chat 4, and Chat 5 into single source of truth
- v3.0 (2026-Q2): Added Phase 8.3 design system + Phase 9 Job Order
  module (6 completed modules). Added gotchas 15-22 discovered during
  WPF style dictionary + custom control development.
    - Phase 4-6: AI infrastructure + Gemini integration
    - Phase 7 (all 22 sub-phases): Proforma Invoice module complete
      including 7.19a-e polish rounds
    - Phase 8.1: Dashboard multi-page shell + 3 sub-dashboards
    - Phase 8.2: Quick actions + Recent activity feed
    - Phase 8.2a-f: Progressive UI polish
        8.2a: Modern UI (ClearType, borders, hero card)
        8.2b: Tinted card backgrounds + gradient hero
        8.2c: Full DynamicResource migration + 25-brush palette +
              splash light theme + splash-to-main handoff fix
        8.2d: Splash custom glass logo + feature row + rotating
              messages + shimmer + footer info cards
        8.2e: BrushKeyConverter + brush key indirection
        8.2f: Instant theme swap via ThemeManager.ThemeChanged

    Foundational documentation in Docs/Chat5/ folder:
    - prd.md, architecture.md, rules.md, data/d04-calculations.md,
      prompts/p01-master-prompt.md (this file)

    ═══════════════════════════════════════════════════════════════
    ROADMAP — WHAT'S NEXT
    ═══════════════════════════════════════════════════════════════

    IMMEDIATE NEXT: Phase 9 — Job Order Module

    Before Phase 9 starts, USER needs to answer 4 design questions:
    1. What is a Job Order in the business? (signed-off PI converted
       to production, or has its own fields like delivery date,
       priority, assigned team, cutting instructions?)
    2. Does Job Order have its own numbering? (e.g., JO-2026-001
       separate from PI-2026-001?)
    3. Can one Proforma Invoice split into multiple Job Orders?
       (partial production?)
    4. What are the Job Order status states? (e.g., Draft → Released
       → In Production → Completed?)

    After Phase 9:
    - Phase 10: Cutting Module
    - Phase 11-15: Production Modules (Holes, Polish, Edge, Mitring,
      Tempering)
    - Phase 16-17: Dispatch (Pairing, Doubling/Lamination)
    - Phase 18: Delivery Note
    - Phase 19: Tax Invoice
    - Phase 20: Sales Report
    - Phase 21: Optimization

    ═══════════════════════════════════════════════════════════════
    JOB ORDER MODULE — 40% REUSABLE FROM PROFORMA INVOICE
    ═══════════════════════════════════════════════════════════════

    Patterns to reuse when building Phase 9:
    - List-first architecture (SavedInvoicesList pattern →
      SavedJobOrdersList)
    - 7-step wizard pattern (or custom step count)
    - Save/Load JSON snapshot pattern
    - Repository + DbContext pattern (create JobOrderRepository with
      separate joborders.db)
    - Export services (PDF/Excel/CSV — reuse PdfExportContext
      pattern)
    - Dashboard sub-page (add JobOrdersDashboardViewModel + View
      under Views/Dashboard/JobOrder/ and ViewModels/Dashboard/
      JobOrder/)

    ═══════════════════════════════════════════════════════════════
    KNOWN WORKING PATTERNS (COPY THESE EXACTLY)
    ═══════════════════════════════════════════════════════════════

    Correct property pattern (equality guard MANDATORY):

        private int _w1;
        public int W1
        {
            get => _w1;
            set
            {
                if (_w1 == value) return;
                _w1 = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

    Correct bulk paste pattern:

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
        finally
        {
            _isBulkAdding = false;
        }
        RenumberRows();
        RecalculateFooter();
        foreach (var row in touchedRows)
            row.FireAllChanged();

    Correct async load with reentrancy guard:

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

    Correct DoEvents for .NET Core+:

        Application.Current.Dispatcher.Invoke(
            DispatcherPriority.Background,
            new Action(() => { }));

    Correct MessageBox after collection update:

        Application.Current.Dispatcher.BeginInvoke(
            new Action(() =>
            {
                MessageBox.Show("Success", "Done");
            }),
            DispatcherPriority.Background);

    Correct interface-based behavior communication:

        public interface IExcelPasteable
        {
            void PasteFromText(string clipboardText,
                               object startRowContext);
            void AddRows(int count);
        }

        // In behavior:
        var vm = FindViewModelInTree(sender) as IExcelPasteable;
        vm?.PasteFromText(text, rowContext);

    Correct theme brush key on VM (Phase 8.2e):

        var kpi = new KpiCardModel
        {
            Icon        = "📄",
            Title       = "Total Invoices",
            BadgeColor  = "AccentBrush",        // brush KEY not hex
            TintColor   = "TintIndigoBrush",    // brush KEY not hex
            DisplayValue = totalInvoices.ToString("N0")
        };

    Correct theme swap re-notify (Phase 8.2f):

        protected override void OnThemeChanged()
        {
            // reassign to force DataTemplate rebuild
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
        }

    ═══════════════════════════════════════════════════════════════
    COMMUNICATION PREFERENCES
    ═══════════════════════════════════════════════════════════════

    - When user says "use your recommendation" → just write the code
    - Always give FULL file when asked — never skip lines
    - When user shares file + says "update" → give complete updated
      file
    - Follow per-feature sub-folder pattern STRICTLY
    - Include README.md for every new feature
    - Ask before architectural decisions ONLY if truly ambiguous
    - Give commit messages matching the detailed format
    - When giving find-and-replace, show exact text to find and
      replace
    - Before writing files, verify chat history — ask user to share
      current version if unsure

    ═══════════════════════════════════════════════════════════════
    HOW TO CONTINUE WORK
    ═══════════════════════════════════════════════════════════════

    Say ONE of:
        "Continue building [FileName]"
        "Fix bug: [describe bug]"
        "Build XAML for [screen name]"
        "Build [ClassName] following architecture.md"
        "Review [code] against rules.md"
        "Design [module] before building"

    Always paste the current file content when asking for fixes.
    Always specify which file you are working on.

    ═══════════════════════════════════════════════════════════════
    CONTEXT LOADED. READY TO CONTINUE.
    ═══════════════════════════════════════════════════════════════

    I have full ProGlass Automation context. Ready to work on any
    module.

    CURRENT STATUS: [TELL THE AI WHAT TO BUILD NEXT]

---

## Maintenance

**When to update this prompt:**
- After completing any phase (add to PART 2 completed modules list)
- When adding new gotchas discovered (add to PART 3 patterns)
- When new file locations become important (add to PART 3)
- When architectural patterns change (update PART 3 working patterns)
- Never delete history — append only

**When to bump version:**
- Every major phase completion (Phase 9, Phase 10, etc.)
- Bump minor version for polish rounds (v1.1, v1.2)

**Version log:**
- v1.0 (2026-Q1): Initial 4-part bootstrap covering Phases 1-8.2f
- v2.0 (2026-Q1): Merged authoritative version combining Chat 1,Instant theme swap via ThemeManager.ThemeChanged event
  Chat 3, Chat 4, and my Chat 5 into single source of truth