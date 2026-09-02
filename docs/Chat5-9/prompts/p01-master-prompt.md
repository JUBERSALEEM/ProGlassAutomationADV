# ProGlass Automation — Master AI Bootstrap Prompt

**Version:** 3.0 (Chat 10 update — through Phase 10.9a)

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
- "Build Phase 19 — Tax Invoice module — design data model first"
- "Fix bug in DN detail history navigation"
- "Add sales trend chart to Overview dashboard"
- "Review my code against rules.md"

---

## AI Behavior Contract (READ FIRST)

You are working on ProGlassAutomation, a WPF (.NET 8) desktop
application for UAE-based glass manufacturing companies.

Before writing any code:
1. Read `Docs/Chat5-9/prd.md` — Product requirements + module map
2. Read `Docs/Chat5-9/architecture.md` — Folder structure + patterns
3. Read `Docs/Chat5-9/rules.md` — Coding standards + gotchas
4. Read `Docs/Chat5-9/data/d04-calculations.md` — All formulas
5. Read `Docs/Chat5-9/data/d05-design-system.md` — Colors + styles
6. Read `Docs/Chat5-9/delivery.md` — DN module reference (for any
   downstream module like Tax Invoice)

You MUST:
- Follow folder structure exactly (per-feature sub-folders)
- Place formulas only in `data/d04-calculations.md`
- Use MVVM strict pattern (ViewModelBase + RelayCommand)
- Add validation where needed
- Avoid hidden state
- Guard all property setters: `if (_field == value) return;`
- Give full file when asked — never skip lines with `...`
- Never remove existing code when adding new features
- Never remove/rename interface methods (other modules depend)
- Change ONE file at a time — verify build before next

You MUST NOT:
- Invent new folders outside the documented structure
- Modify architecture without explicit instruction
- Change formulas outside `data/d04-calculations.md`
- Add hidden defaults or silent fallbacks
- Use `dynamic` in pattern matching (CS8208 error)
- Use `DispatcherFrame.PushFrame` (CS0117 in .NET Core+)
- Hardcode `#FFFFFF` / `#000000` for theme-adaptive UI
- Silently drop existing properties or methods
- Sum DN Balance across siblings (Gotcha #40)
- Trust stored PreviouslyDeliveredQty without self-healing (Gotcha #41)
- Batch 15+ file changes in one shot (Gotcha #45)

When generating code:
- Be deterministic
- No pseudo-code
- No missing types
- No assumptions
- No structural changes without asking

If something is unclear:
**ASK** before implementing.

Return:
1. Updated file path
2. Full file contents
3. Short explanation

---

# PART 1 — Project + Conventions

    ═══════════════════════════════════════════════════════════════
    PROGLASS AUTOMATION — SESSION HANDOFF (v3.0)
    ═══════════════════════════════════════════════════════════════

    Developer: Juber Saleem (India, jubersaleem@gmail.com)
    App: ProGlass Automation — Glass Production Workflow Software
    Stack: WPF (.NET 8, C# 9+), MVVM (ViewModelBase + RelayCommand)
    Database: SQLite + EF Core 8 (5 separate .db files)
    NuGet: CsvHelper, ClosedXML, QuestPDF 2024.12.2, Google Gemini AI
    Features: Light/Dark theme, AI Chat, shared export engine

    Full docs available in Docs/Chat5-9/ folder:
        Docs/Chat5-9/prd.md                       Product requirements
        Docs/Chat5-9/architecture.md              Folder structure + patterns
        Docs/Chat5-9/rules.md                     Development rules
        Docs/Chat5-9/delivery.md                  DN module deep-dive
        Docs/Chat5-9/data/d04-calculations.md     All formulas
        Docs/Chat5-9/data/d05-design-system.md    Colors + styles

    ═══════════════════════════════════════════════════════════════
    STRICT CONVENTIONS (ALWAYS FOLLOW)
    ═══════════════════════════════════════════════════════════════

    1. FOLDER STRUCTURE — Every feature = its own sub-folder.
       All related files (View + VM + Models) live inside that folder.
       Never dump new files flat into an existing folder that already
       has sub-folders established.

       Example pattern:
         Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml
         Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml.cs
         ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.cs

    2. ALWAYS confirm folder structure before writing code for new
       features.

    3. Full file updates when asked — never skip lines with "..."
       comments. Give the complete file byte-for-byte.

    4. NEVER remove existing code when adding new features.
       Even legacy/backward-compat properties stay unless explicitly
       told to remove them.

    5. NEVER remove/rename interface methods that other modules
       depend on. ADD new methods alongside old ones (Gotcha #46).

    6. Guard ALL property setters:
         if (_field == value) return;

    7. Async everywhere with reentrancy guards
       (Interlocked.CompareExchange).

    8. README.md per feature (in Views or Services layer).

    9. Change ONE file at a time. Rebuild + verify before next
       (Gotcha #45).

    10. When user says "use your recommendation" → just write the code.

    11. When user shares current file + says "update" → give back
        complete updated file. NEVER assume you have latest from
        memory — always check what user shared vs what you have.

    12. Before writing files check chat history for existing versions.
        If unsure whether you have the latest, ASK the user to share.
        Never silently drop properties or methods.

    13. When user is in "analysis phase" (sharing files for review),
        DO NOT write code. Just analyze and acknowledge. Wait for
        explicit "start coding" or "give updated file" request.

    ═══════════════════════════════════════════════════════════════
    COMMIT MESSAGE FORMAT (STRICT — DETAILED FORMAT)
    ═══════════════════════════════════════════════════════════════

    First line:
      Module: Area: Phase-X.XX: type(scope): short description

    For combined sub-phases:
      Module: Area: Phase-X.XX+Y.YY: type(scope): description

    Then blank line, then:
    - 2-4 sentence summary paragraph explaining WHY and WHAT
    - Blank line
    - Structured sections in ALL CAPS with colon
      (SUMMARY, VISUAL CHANGES, ARCHITECTURE CHANGES, ROOT CAUSE,
       FIX APPROACH, MODELS UPDATED, VIEWMODELS UPDATED, VIEWS UPDATED,
       DESIGN TOKENS, BEHAVIOR, BACKWARD COMPATIBILITY, etc.)
    - Each section: bullet list with dashes
    - FILES ADDED: / FILES MODIFIED: / FILES DELETED: sections
      with sub-bullets
    - COMPLETED PHASES: full list for tracking
    - UPCOMING PHASES: what's next

    Types:
      feat       New feature or capability
      fix        Bug fix
      refactor   Code restructuring without behavior change
      style      Cosmetic UI changes
      docs       Documentation only
      chore      Build, tooling, config
      perf       Performance improvement
      ux         UX polish (Phase 10.7a redesign, 10.8b, 10.9a)

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

    DELIVERY NOTE FORMULAS (CRITICAL):
    - Balance = Ordered − Delivered (NEVER sum sibling snapshots)
    - PrevQty must self-heal on load via GetPreviouslyDeliveredMapAsync
    - Full details in Docs/Chat5-9/delivery.md

    ═══════════════════════════════════════════════════════════════
    DATABASE ARCHITECTURE (5 SEPARATE SQLITE FILES)
    ═══════════════════════════════════════════════════════════════

    1. proglass.db          — Sheet Store (GlassSheets, SheetHistories)
    2. specifications.db    — Saved glass specs
    3. proformainvoices.db  — Saved invoices (JSON blob + indexed cols)
    4. joborders.db         — Job orders (JSON blob + indexed cols)
    5. deliverynotes.db     — Delivery notes + items + attachments

    All at %AppData%\ProGlassAutomation\

    Attachment files (not DB):
        %AppData%\ProGlassAutomation\DeliveryNotes\Attachments\
            Images\{DeliveryNoteId}\
            PDFs\{DeliveryNoteId}\

    All initialized in App.xaml.cs:
      App.Specs = new SpecificationsRepository();
      await App.Specs.InitializeAsync();
      App.Invoices = new ProformaInvoiceRepository();
      await App.Invoices.InitializeAsync();
      App.Jobs = new JobOrderRepository();
      await App.Jobs.InitializeAsync();
      App.DeliveryNotes = new DeliveryNoteRepository();
      await App.DeliveryNotes.InitializeAsync();

    Service locator (static App properties):
      App.Config          — AppConfig
      App.Ai              — IAiService (GeminiAiService)
      App.Connectivity    — IConnectivityService
      App.Specs           — ISpecificationsRepository
      App.Invoices        — IProformaInvoiceRepository
      App.Jobs            — IJobOrderRepository
      App.DeliveryNotes   — IDeliveryNoteRepository

---

# PART 2 — Completed Modules Summary

    ═══════════════════════════════════════════════════════════════
    COMPLETED MODULES (8 total)
    ═══════════════════════════════════════════════════════════════

    MODULE 1 — App Shell:
    - Splash window (light theme, green accent, custom glass-stack
      logo, feature preview row, rotating loading messages)
    - Hamburger menu navigation (5 groups)
    - AI chat panel (floating, Gemini-powered)
    - Light/Dark theme toggle (ThemeManager static class)
    - About panel

    MODULE 2 — Sheet Store (Phase 2-3):
    - Full CRUD on GlassSheet entity
    - Buy/Use stock operations
    - CSV/Excel/PDF export
    - History tracking (SheetHistory table)
    - Backup service (last 7 database copies on exit)

    MODULE 3 — AI Infrastructure (Phase 4-6):
    - GeminiAiService with API key config
    - ConnectivityService (online/offline monitoring)
    - AiChatViewModel + floating chat panel

    MODULE 4 — Proforma Invoice (Phase 7, 22 sub-phases, COMPLETE):
    - 7-step wizard hosted by ProformaInvoiceViewModel
    - List-first landing page (SavedInvoicesList)
    - Company Setup → Project Specs → Client Profile → Pricing
      Estimator → DataGrid → Other Charges → Print Preview
    - JSON snapshot save/load
    - PDF/Excel/CSV export via shared engine (migrated Phase 9.5b)
    - Right-click: Open, Duplicate, Export, Change Status, Convert
      to Job Order (functional Phase 9.3), Delete

    MODULE 5 — Dashboard (Phase 8.1-10.9a, COMPLETE):
    - Multi-page shell with underline tab navigation
    - DashboardPageBase abstract class with ThemeChanged hook
    - Sub-pages: Overview, Sheet Store, Proforma Invoice, Job Order
    - Overview REDESIGNED in Phase 10.9a:
        2-column parent-card grid (Sales · Production · Delivery ·
        Inventory · Activity · Chart)
        Nested tiles inside each parent card (Accent-driven colors)
        Recent Activity as chat-style feed INSIDE grid
        Smooth pixel-based scrolling
        Quick actions inline in header (5 pills)
    - BrushKeyConverter for theme-adaptive VM colors
    - Instant theme swap via reassign-ObservableCollection pattern

    MODULE 6 — Job Order (Phase 9.1-9.5, COMPLETE):
    - Convert-from-PI architecture (Path B, no wizard duplication)
    - Numbering: JOB-YYYY-NNN derived from PI-YYYY-NNN
    - Status: Draft → Released → InProduction → Completed → Delivered
    - Priority: High / Medium / Low
    - 4th SQLite database: joborders.db
    - Embedded PI snapshot (immutable) + editable ProductionDetails
    - Two-column detail view: read-only PI (left) + editable panel (right)
    - PDF/Excel/CSV export via shared engine (production sheet, no pricing)
    - Job Order sub-dashboard (Phase 9.4)

    MODULE 7 — Shared Export Engine (Phase 9.5a-b, COMPLETE):
    - Services/Export/ folder with 3 contracts:
        IPdfLayout, IExcelLayout, ICsvLayout
    - 3 engines: PdfExportEngine, ExcelExportEngine, CsvExportEngine
    - Modules provide layouts, engine handles page structure + file I/O
    - Style tokens: DocumentStyle.* (PDF) + WorkbookStyle.* (Excel)
    - PdfExportEngine.PrintAsync for physical printer support
    - Letterhead ShowOnce (page 1 only)
    - Footer every page with DocumentId + page X of Y
    - ExportFileNamer builds smart filenames

    MODULE 8 — Delivery Note (Phase 10.1-10.9a, FULLY COMPLETE):

    The most sophisticated module built. See Docs/Chat5-9/delivery.md
    for complete reference.

    HIGHLIGHTS:
    - 5th SQLite database: deliverynotes.db
    - DN ID format: DN-YYYY-NNN-NN (JO seq + delivery seq)
    - 4 delivery types: Original | Dummy | Rework | Return
    - 5 statuses: Draft | Dispatched | Delivered | Signed | Received
    - Sibling DN chain with auto-created Dummy DNs for balance
    - Self-healing PREV column (never trusts stale snapshots)
    - Balance formula lock-in: Ordered − Delivered (never sum)
    - Attachments: disk-based storage with rename dialog
    - Clickable history rows navigate between sibling DNs
    - Items grouped by SpecGroupName in detail grid
    - Zero-qty save warning + Delete DN command + JO Picker dialog
    - Draft-DN-exists check prevents duplicates
    - Invoice button per row (placeholder for Phase 19)
    - Tax Invoice link fields on entity (TaxInvoiceId, HasTaxInvoice)
    - PDF/Excel/CSV export via shared engine
    - PDF 5-column totals strip: Ordered · Prev · This DN · Balance · Seq
    - Premium detail page (Phase 10.8b):
        Action bar in card with logical grouping
        [Save] [Make DN] | [PDF] [Excel] [CSV] | divider | [Delete]
        Hero card with gradient header + 4 stat tiles
        Gradient section headers (green + indigo)
        Bolder spec group headers with accent bar
    - Dashboard integration (Phase 10.9):
        Delivery Snapshot tile group
        DN events in activity feed
        Deliveries quick action

    ═══════════════════════════════════════════════════════════════
    PHASE HISTORY (chronological)
    ═══════════════════════════════════════════════════════════════

    ✅ Phase 1-3    : App shell + Sheet Store + Splash
    ✅ Phase 4-6    : AI infrastructure + Gemini + Config
    ✅ Phase 7      : Proforma Invoice module (7 wizard steps)
    ✅ Phase 8      : Dashboard multi-page (4 sub-pages + design system)
    ✅ Phase 8.2a-f : Progressive UI polish
    ✅ Phase 8.3    : Design system foundation
    ✅ Phase 9.1-4  : Job Order module + sub-dashboard
    ✅ Phase 9.5a   : Shared Export Engine (PDF + Excel + CSV)
    ✅ Phase 9.5b   : PI migrated to shared engine + legacy deleted
    ✅ Phase 10.1   : DN foundation (models + 5th SQLite)
    ✅ Phase 10.2   : DN repository + mapper
    ✅ Phase 10.3   : DN list view (6 tabs + KPI + filters)
    ✅ Phase 10.3b  : Premium card tab headers
    ✅ Phase 10.4   : DN detail view (two-column)
    ✅ Phase 10.5   : Create DN from JO + cross-module navigation
    ✅ Phase 10.5a  : Removed debug MessageBoxes
    ✅ Phase 10.5b  : Cleanup of DN detail load flow
    ✅ Phase 10.5c  : 6 tabs + 5 hero cards + click-to-preview
    ✅ Phase 10.6   : Attachments — disk-based file storage
    ✅ Phase 10.6a  : Rename dialog on upload
    ✅ Phase 10.6b  : Clickable history rows → per-sibling attachments
    ✅ Phase 10.6c  : CommandManager.InvalidateRequerySuggested
    ✅ Phase 10.7   : 7 improvements (balance fix, PREV heal, grouping,
                      wider columns, Make DN rename, Tax Invoice fields,
                      TotalPreviouslyDeliveredQty)
    ✅ Phase 10.7a  : Create DN button + JO picker + Draft check +
                      Delete + zero-qty warning + navigation fix +
                      Invoice placeholder + row card redesign +
                      Invoice button per row
    ✅ Phase 10.8   : DN Export — PDF + Excel + CSV via shared engine
    ✅ Phase 10.8a  : Prev Delivered — PDF totals + detail chips
    ✅ Phase 10.8b  : Premium DN detail page redesign
    ✅ Phase 10.9   : Dashboard integration — DN stats + activity feed
    ✅ Phase 10.9a  : Overview dashboard full redesign — 2-col parent
                      card grid + chat activity + smooth scrolling

---

# PART 3 — File Locations + Patterns + Gotchas

    ═══════════════════════════════════════════════════════════════
    KEY FILE LOCATIONS
    ═══════════════════════════════════════════════════════════════

    APP STARTUP:
    - App.xaml (converter + theme dictionary registrations)
    - App.xaml.cs (startup: Config → 5 DBs → AI → Splash)
    - MainWindow.xaml + .cs
    - ViewModels/MainViewModel.cs (navigation + all module VMs +
      cross-module callbacks including OnRequestOpenDeliveryNote)

    THEMES:
    - Themes/LightTheme.xaml (25 brushes)
    - Themes/DarkTheme.xaml (25 brushes)
    - Themes/Styles/{Typography,IconBadge,Pill,Card}Styles.xaml
    - Services/ThemeManager.cs (ApplyTheme + ThemeChanged event)

    SPLASH:
    - Views/Splash/SplashView.xaml (+ .cs with FadeOutAndClose)

    DASHBOARD:
    - ViewModels/Dashboard/DashboardViewModel.cs (shell)
    - ViewModels/Dashboard/DashboardPageBase.cs
    - ViewModels/Dashboard/Overview/OverviewDashboardViewModel.cs
      (Phase 10.9a — 4 tile collections + KpiTileModel + smooth scroll VM)
    - ViewModels/Dashboard/SheetStore/SheetStoreDashboardViewModel.cs
    - ViewModels/Dashboard/ProformaInvoice/PiDashboardViewModel.cs
    - ViewModels/Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs
    - Views/Dashboard/*/*View.xaml (mirror ViewModels structure)
    - Models/Dashboard/KpiCardModel.cs (legacy)
    - Models/Dashboard/KpiTileModel.cs (Phase 10.9a — NEW)
    - Models/Dashboard/ChartBarModel.cs
    - Models/Dashboard/ActivityFeedItem.cs

    PROFORMA INVOICE:
    - ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs
    - Views/ProformaInvoice/ProformaInvoiceView.xaml
    - Step1-7 folders under ViewModels + Views
    - Services/ProformaInvoice/PdfExport/ (legacy — being migrated)
    - Services/ProformaInvoice/Export/ (shared engine layouts —
      Phase 9.5b)

    JOB ORDER:
    - Models/JobOrder/Common/{JobOrderStatus,JobPriority}/
    - Models/JobOrder/SavedJobOrder/
    - Models/JobOrder/ProductionDetails/
    - Services/JobOrder/Database/JobOrdersDbContext.cs
    - Services/JobOrder/Repository/IJobOrderRepository.cs + impl
    - Services/JobOrder/Mapper/JobOrderMapper.cs
    - Services/JobOrder/Export/{Pdf,Excel,Csv}Layout.cs
    - ViewModels/JobOrder/JobOrderViewModel.cs (shell)
    - ViewModels/JobOrder/SavedJobOrdersList/
    - ViewModels/JobOrder/JobOrderDetail/
    - Views/JobOrder/{shell + list + detail}

    DELIVERY NOTE (Phase 10.1-10.9a — COMPLETE):
    - Models/DeliveryNote/Common/{DeliveryNoteStatus,DeliveryType}/
    - Models/DeliveryNote/SavedDeliveryNote/
        SavedDeliveryNote.cs (EF entity)
        DeliveryNoteSnapshot.cs (POCO tree)
        DeliveryNoteListItem.cs (lightweight list DTO)
        DeliveryNoteItem.cs (EF child)
        DeliveryNoteAttachment.cs (EF child)
    - Services/DeliveryNote/Database/DeliveryNotesDbContext.cs
    - Services/DeliveryNote/Repository/IDeliveryNoteRepository.cs + impl
    - Services/DeliveryNote/Mapper/DeliveryNoteMapper.cs
    - Services/DeliveryNote/Attachments/
        AttachmentService.cs
        AttachmentLimits.cs
        AttachmentType.cs
    - Services/DeliveryNote/Export/
        DeliveryNotePdfLayout.cs
        DeliveryNoteExcelLayout.cs
        DeliveryNoteCsvLayout.cs
    - ViewModels/DeliveryNote/DeliveryNoteViewModel.cs (shell)
    - ViewModels/DeliveryNote/DeliveryNoteList/
        DeliveryNoteListViewModel.cs
        DeliveryNoteRowViewModel.cs
    - ViewModels/DeliveryNote/DeliveryNoteDetail/
        DeliveryNoteDetailViewModel.cs
        DnAttachmentRowViewModel.cs
    - Views/DeliveryNote/DeliveryNoteView.xaml (shell)
    - Views/DeliveryNote/DeliveryNoteList/
        DeliveryNoteListView.xaml + .cs
        JobOrderPicker/JobOrderPickerDialog.xaml + .cs
    - Views/DeliveryNote/DeliveryNoteDetail/
        DeliveryNoteDetailView.xaml + .cs
        Attachments/Rename/RenameAttachmentDialog.xaml + .cs

    SHARED EXPORT (Phase 9.5a):
    - Services/Export/Core/
        IDocumentLayout.cs
        ExportRequest.cs
        ExportResult.cs
        ExportFormat.cs
    - Services/Export/FileSystem/
        SaveFileDialogService.cs
        ExportFileNamer.cs
    - Services/Export/Pdf/
        IPdfLayout.cs
        PdfExportEngine.cs
        DocumentStyle.cs
        LetterheadInfo.cs
        PageLayout.cs
        Renderers/{Letterhead,Footer,SectionHeader}Renderer.cs
    - Services/Export/Excel/
        IExcelLayout.cs
        ExcelExportEngine.cs
        WorkbookStyle.cs (STATIC — Gotcha #32)
    - Services/Export/Csv/
        ICsvLayout.cs
        CsvExportEngine.cs

    SHEET STORE:
    - Services/SheetStore/ISheetStoreRepository.cs + impl
    - Models/SheetStore/GlassSheet.cs
    - Models/SheetStore/SheetStoreHistory/SheetHistory.cs

    DATABASE:
    - Services/Database/AppDbContext.cs (Sheet Store)
    - Services/Database/DatabaseBackup/BackupService.cs

    GLOBAL SERVICES:
    - Commands/RelayCommand.cs
    - ViewModels/ViewModelBase.cs
    - Services/ThemeManager.cs
    - Services/ConfigService.cs

    CONVERTERS (all in App.xaml):
    - BoolToVisibilityConverter (built-in)
    - ColorStringConverter (hex → SolidColorBrush)
    - DecimalIsPositiveConverter
    - ReferenceEqualityConverter (MultiBinding)
    - ScaleConverter (normalized × parameter → pixel)
    - InverseBoolToVisibilityConverter
    - BrushKeyConverter (string brush key → theme brush)
    - PercentToWidthConverter (Phase 10.3 — DN pending bar)

    ═══════════════════════════════════════════════════════════════
    IMPORTANT PATTERNS & GOTCHAS (52 rules)
    ═══════════════════════════════════════════════════════════════

    1-14. (Legacy — see Chat5 docs for historical context)

    15. BrushKeyConverter registered in BOTH theme files (not local)

    16. UserControl code-behind must NOT specify `: UserControl`
        (CS0263 conflict)

    17. Same for ResourceDictionary code-behind — no base class

    18. DynamicResource CANNOT be Converter value in Binding markup

    19. `x:Name` on `GradientStop` inside Freezable is unreliable

    20. `;assembly=ProGlassAutomation` on same-assembly xmlns breaks

    21. When snapshot doesn't store computed field, pass value
        separately from indexed column

    22. Delete old placeholder VMs when replacing with real ones

    23-27. (Chat 6-7 gotchas — see rules.md)

    28. StackPanel has no Padding — use Border wrapper

    29. Property cannot be set twice on same element (XLS0501)
        Watch VerticalScrollBarVisibility (direct + attached)

    30. Namespace MUST match folder path exactly

    31. `\#` in XAML StringFormat is INVALID

    32. WorkbookStyle is STATIC — never `new WorkbookStyle`

    33. CsvConfiguration.LeaveOpen removed in newer CsvHelper

    34. New UserControl auto-creates `: UserControl` — remove it

    35. XAML rendering silent failures — invalid XAML = blank view

    36. ContentControl+DataTemplate reliable for list↔detail

    37. UserControl MUST keep constructor + InitializeComponent()

    38. Nested namespace collision — use string literals

    39. WPF Commands don't auto-refresh — InvalidateRequerySuggested

    40. Balances NEVER summed — always Ordered − Delivered (DN critical)

    41. PREV values are snapshots — self-heal on load + save (DN)

    42. Save must refresh BOTH panels (history + items grid)

    43. Hot Reload cannot apply interface changes — full rebuild

    44. DB schema changes → delete old .db before first run

    45. NEVER batch 15+ file changes — one file at a time

    46. NEVER remove/rename interface/shell VM methods

    47. XAML converters — verify file+namespace+registration first

    48. Reused VMs must reset ALL editable fields on load

    49. One DN on screen at a time — clicking history navigates

    50. Verify "same file" claims before trusting pasted files

    51. BrushKeyConverter tints = dark text (PrimaryForegroundBrush)

    52. CanExecute predicates: call InvalidateRequerySuggested after load

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
    Silent removal of interface methods    Breaks other modules
    Sum snapshot Balance across siblings   Double-counts (#40)
    Setting same property twice in XAML    XLS0501 (#29)
    Trust stored PreviouslyDeliveredQty    Stale snapshots (#41)
    Batching 15+ file changes              Cascading errors (#45)

---

# PART 4 — Current State + Roadmap

    ═══════════════════════════════════════════════════════════════
    CURRENT STATE — THROUGH PHASE 10.9a
    ═══════════════════════════════════════════════════════════════

    Completed (see PART 2 for detailed history):
    - Phase 1-8.3   : Foundation + PI + Dashboard + Design System
    - Phase 9       : Job Order module (all sub-phases)
    - Phase 9.5     : Shared Export Engine
    - Phase 10      : Delivery Note module (all sub-phases through 10.9a)

    Foundational documentation in Docs/Chat5-9/ folder:
    - prd.md, architecture.md, rules.md, delivery.md
    - data/d04-calculations.md, data/d05-design-system.md
    - prompts/p01-master-prompt.md (this file)

    ═══════════════════════════════════════════════════════════════
    NEW INFRASTRUCTURE (Chat 6-9)
    ═══════════════════════════════════════════════════════════════

    SHARED EXPORT ENGINE (Phase 9.5a):
      Services/Export/ folder
      3 contracts: IPdfLayout, IExcelLayout, ICsvLayout
      3 engines: PdfExportEngine, ExcelExportEngine, CsvExportEngine
      Modules provide layouts, engine handles I/O + file dialog
      PdfExportEngine.PrintAsync for physical printer

    DELIVERY NOTE MODULE (Phase 10):
      5th SQLite database: deliverynotes.db
      Full CRUD + workflow (Draft → Dispatched → Delivered → Signed → Received)
      4 delivery types with auto-Dummy for balance
      Attachment system (disk-based, Rename dialog)
      Self-healing PREV formula
      Sibling DN chain navigation
      PDF/Excel/CSV export
      Dashboard integration
      Premium detail page redesign

    DASHBOARD OVERVIEW REDESIGN (Phase 10.9a):
      2-column parent-card grid layout
      Nested tiles inside each parent card
      Chat-style Recent Activity inside grid
      Smooth pixel-based scrolling
      Compact color system with dark shade text on soft tints
      KpiTileModel (replaces old KpiCardModel for tiles)

    ═══════════════════════════════════════════════════════════════
    ROADMAP — WHAT'S NEXT
    ═══════════════════════════════════════════════════════════════

    IMMEDIATE NEXT: Phase 10.10 — Tax Invoice link preparation
    OR jump to: Phase 11 — Cutting Module (production floor)

    PRODUCTION MODULES (Phase 11-18):
    - Phase 11: Cutting Module
    - Phase 12: Holes & Cutouts
    - Phase 13: Polish
    - Phase 14: Edge Deletion
    - Phase 15: Mitring
    - Phase 16: Tempering
    - Phase 17: Pairing
    - Phase 18: Doubling/Lamination

    TAX INVOICE (Phase 19):
    - Wire up TaxInvoiceId + HasTaxInvoice placeholders on DN
    - Convert DN → Tax Invoice with pricing from PI snapshot
    - 6th SQLite database: taxinvoices.db
    - Use shared export engine
    - See Docs/Chat5-9/delivery.md Section 13 for integration plan

    REPORTING (Phase 20-21):
    - Phase 20: Sales Report
    - Phase 21: Optimization

    ═══════════════════════════════════════════════════════════════
    KEY DESIGN DECISIONS LOCKED IN (23 items)
    ═══════════════════════════════════════════════════════════════

    1.  5 SQLite databases — separate files for clean isolation
    2.  JSON blob + indexed columns for all entities
    3.  Shared export engine — IPdfLayout/IExcelLayout/ICsvLayout
    4.  Cross-module navigation via callback chain
    5.  Namespace = folder path (Gotcha #30)
    6.  Card tab headers with gradient active states
    7.  Attachment file storage on disk (not DB blob)
    8.  DN types: Original | Dummy | Rework | Return
    9.  IsFoc at DN level (not item level)
    10. Balance = Ordered − Delivered (never sum snapshots)
    11. Sibling DN chain: JO → DN-01 → DN-02(Dummy) → DN-03...
    12. Rename dialog = frameless glassy card style
    13. Save must refresh BOTH panels (Gotcha #42)
    14. Interface methods: ADD alongside, never remove (Gotcha #46)
    15. Clicking history row = navigate to that DN (Gotcha #49)
    16. Pill text on tint = dark, not white (Gotcha #51)
    17. Draft-DN-exists check before creating new DN
    18. Export: inline engine creation, PromptForLocation=true
    19. Export CanExecute: _entity != null && !IsSaving && !IsLoading
    20. DN detail hero card: 4 stat tiles always visible
    21. Action bar: Save+MakeDN | Export group | divider | Delete
    22. Dashboard: 2-col parent-card grid + chat activity inside
    23. Smooth scrolling: CanContentScroll=False + PanningMode

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

    Correct DN self-heal PREV on load:

        var prevMap = await App.DeliveryNotes
            .GetPreviouslyDeliveredMapAsync(
                entity.JobOrderId,
                entity.DeliveryNoteId);
        foreach (var item in entity.Items)
        {
            var key = $"{item.SpecGroupNumber}|{item.SerialNumber}";
            item.PreviouslyDeliveredQty =
                prevMap.TryGetValue(key, out var prev) ? prev : 0;
        }

    Correct DN Balance calculation (item-level):

        public int BalanceQty =>
            Math.Max(0, OrderedQty - PreviouslyDeliveredQty - DeliveredQty);

    Correct DN Balance calculation (JO-level):

        // In GetDeliveryHistoryAsync:
        TotalBalance = Math.Max(0, totalOrdered - totalDelivered);
        // NEVER sum sibling BalanceQty snapshots!

    Correct export command in VM:

        private async Task ExportPdfAsync()
        {
            if (!EnsureLoaded()) return;
            var layout  = new DeliveryNotePdfLayout(_entity!, _snapshot!);
            var engine  = new PdfExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave     = true
            };
            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "PDF");
        }

    Correct KpiTileModel (Phase 10.9a):

        var tile = new KpiTileModel
        {
            Icon         = "💰",
            Label        = "Total Sales",
            DisplayValue = $"AED {totalSales:N0}",
            Accent       = "Green"  // "Green"|"Blue"|"Amber"|"Red"
        };

    Correct theme swap re-notify (Phase 8.2f + 10.9a):

        protected override void OnThemeChanged()
        {
            SalesTiles       = new ObservableCollection<KpiTileModel>(_salesTiles);
            ProductionTiles  = new ObservableCollection<KpiTileModel>(_productionTiles);
            DeliveryTiles    = new ObservableCollection<KpiTileModel>(_deliveryTiles);
            InventoryTiles   = new ObservableCollection<KpiTileModel>(_inventoryTiles);
            RecentActivity   = new ObservableCollection<ActivityFeedItem>(_recentActivity);
        }

    Correct smooth-scrolling ScrollViewer (Phase 10.9a):

        <ScrollViewer VerticalScrollBarVisibility="Auto"
                      CanContentScroll="False"
                      PanningMode="VerticalOnly">
            <!-- content -->
        </ScrollViewer>

    Correct destructive action confirmation (Phase 10.7a):

        var result = MessageBox.Show(
            $"Delete delivery note '{DeliveryNoteId}'?\n\n" +
            $"Customer : {CustomerName}\n" +
            $"Project  : {ProjectName}\n" +
            $"Status   : {Status}\n\n" +
            "This will permanently delete...",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

    ═══════════════════════════════════════════════════════════════
    COMMUNICATION PREFERENCES
    ═══════════════════════════════════════════════════════════════

    - When user says "use your recommendation" → just write the code
    - Always give FULL file when asked — never skip lines
    - When user shares file + says "update" → give complete updated file
    - Follow per-feature sub-folder pattern STRICTLY
    - Include README.md for every new feature
    - Ask before architectural decisions ONLY if truly ambiguous
    - Give commit messages matching the detailed format
    - When giving find-and-replace, show exact text to find and replace
    - Before writing files, verify chat history — ask user to share
      current version if unsure
    - NEVER remove existing code when adding new features
    - NEVER remove/rename interface methods (Gotcha #46)
    - NEVER specify base class on UserControl/ResourceDictionary
      code-behind partial classes
    - Change ONE file at a time, verify build, then next
    - In analysis phase: brief acknowledgment only, no code

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

    For Tax Invoice work specifically:
    - Read Docs/Chat5-9/delivery.md Section 13 first
    - DN entity already has TaxInvoiceId + HasTaxInvoice placeholder fields
    - DN row has Invoice button + CanGenerateInvoice property
    - Consider whether 1 DN = 1 Tax Invoice OR many DNs = 1 Tax Invoice

    ═══════════════════════════════════════════════════════════════
    CONTEXT LOADED. READY TO CONTINUE.
    ═══════════════════════════════════════════════════════════════

    I have full ProGlass Automation context through Phase 10.9a.
    All 8 completed modules + shared export engine + full DN module
    + Dashboard redesign. 52 gotchas + 23 locked design decisions.

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
  module. Added gotchas 15-22.
- v4.0 (2026-Q2): FULL REFRESH through Phase 10.9a — added:
  - Shared Export Engine (Phase 9.5a-b)
  - Full Delivery Note module (Phase 10.1-10.9a)
  - Dashboard Overview redesign (Phase 10.9a)
  - KpiTileModel + 4-tile grouping pattern
  - Self-healing PREV pattern (Gotcha #41)
  - Balance never-sum rule (Gotcha #40)
  - Interface preservation rule (Gotcha #46)
  - Gotchas #23-52 (52 total)
  - New folder: Docs/Chat5-9/ (replaces Chat5/)
  - New file: delivery.md (DN module deep-dive reference)
  - 23 locked design decisions