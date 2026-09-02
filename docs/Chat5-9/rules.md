# ProGlass Automation — Coding Rules

**Version:** 3.0 (Chat 10 update — through Phase 10.9a)

**Purpose:** Enforce consistent code quality across all human +
AI-generated contributions. Every rule here is MANDATORY, not a
suggestion.

**Priority:** All generated code must follow every rule below. No
exceptions without explicit override instruction from the user.

---

## 1. Core Principles

- **Deterministic > Clever**
- **Explicit > Implicit**
- **Simple > Smart**
- **Testable > Fast**
- **Never remove existing code when adding new features**
- **Never remove/rename interface methods other modules depend on**

If any rule below conflicts with a "clever" solution, choose the
rule. Predictability beats brevity.

---

## 2. Language Rules

### RULE L01 — Language version
- C# 9+ features allowed
- Pattern matching allowed (**except** `dynamic` — see forbidden list)
- Records allowed for pure data
- Top-level statements NOT used
- File-scoped namespaces preferred

### RULE L02 — Nullable
- Do not enable nullable reference types project-wide
- Use explicit null checks where needed
- Use `?.` and `??` operators
- Use `string?` on new code that legitimately accepts null

### RULE L03 — Async
- `async void` ONLY for event handlers + fire-and-forget loads
- All other async methods return `Task` or `Task<T>`
- Always try/catch inside `async void`
- All I/O operations use async/await

### RULE L04 — Access modifiers
- Explicit access modifiers always
- One class per file, EXCEPT enum + tightly-coupled meta class
  (e.g., `PolishSubType.cs`, `DeliveryNoteStatusMeta.cs`)

### RULE L05 — `var` usage
- OK when type is obvious from right-hand side
- Use explicit type when clarity helps for money/SQM/LM

---

## 3. Naming Rules

### RULE N01 — Fields
- Private backing fields: `_camelCase`
- Private static fields: `_camelCase`

### RULE N02 — Properties
- All public properties: `PascalCase`

### RULE N03 — Methods
- All methods: `PascalCase`

### RULE N04 — Commands
- Pattern: `VerbNounCommand`
- Type: always `ICommand`
- Initialized in constructor, never in field initializer
- Examples: `MakeDnCommand`, `DeleteDnCommand`, `ExportPdfCommand`,
  `SelectHistoryEntryCommand`

### RULE N05 — Interfaces
- Prefix with `I`
- Examples: `IExcelPasteable`, `IDeliveryNoteRepository`,
  `IPdfLayout`, `IExcelLayout`, `ICsvLayout`

### RULE N06 — Event handlers
- Handler pattern: `OnNoun` or `OnNounChanged`
- Examples: `OnRowLeftClick`, `OnItemDeliveredQtyChanged`

### RULE N07 — Booleans
- Prefix with: `is`, `has`, `can`, `should`, `suppress`
- Examples: `IsFoc`, `HasError`, `CanGenerateInvoice`,
  `_isNavigatingHistory`, `_isBulkAdding`

### RULE N08 — Constants
- `private const`: `PascalCase` OR `ALL_CAPS`

### RULE N09 — File naming
| Type              | Pattern                           | Example                          |
|-------------------|-----------------------------------|----------------------------------|
| ViewModel         | `{Feature}ViewModel.cs`           | `DeliveryNoteDetailViewModel.cs` |
| View XAML         | `{Feature}View.xaml`              | `DeliveryNoteDetailView.xaml`    |
| View code-behind  | `{Feature}View.xaml.cs`           | `DeliveryNoteDetailView.xaml.cs` |
| Model             | `{ModulePrefix}{Concept}.cs`      | `KpiTileModel.cs`                |
| Repository iface  | `I{Feature}Repository.cs`         | `IDeliveryNoteRepository.cs`     |
| Repository impl   | `{Feature}Repository.cs`          | `DeliveryNoteRepository.cs`      |
| Service           | `{Feature}Service.cs`             | `AttachmentService.cs`           |
| Converter         | `{Purpose}Converter.cs`           | `BrushKeyConverter.cs`           |
| PDF Layout        | `{Module}PdfLayout.cs`            | `DeliveryNotePdfLayout.cs`       |
| Excel Layout      | `{Module}ExcelLayout.cs`          | `DeliveryNoteExcelLayout.cs`     |
| CSV Layout        | `{Module}CsvLayout.cs`            | `DeliveryNoteCsvLayout.cs`       |

### RULE N10 — Namespace mirrors folder
Folder: `ViewModels/DeliveryNote/DeliveryNoteDetail/`
Namespace: `ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail`

---

## 4. Property Rules (CRITICAL)

### RULE P01 — Equality guard (MANDATORY)

Every property setter with a backing field MUST have this exact
pattern:

    private string _myField = string.Empty;
    public string MyField
    {
        get => _myField;
        set
        {
            if (_myField == value) return;
            _myField = value;
            OnPropertyChanged();
        }
    }

The `if (_field == value) return;` guard is REQUIRED. Prevents
infinite loops, unnecessary redraws, redundant events.

### RULE P02 — Calculated properties
- Must have `private set`
- Never set from outside the class
- Only updated inside `RecalculateAll()`, `RecalculateTotals()`,
  or `SetAllSilent()`
- Examples: `Sqm1`, `TotalSqm`, `Value`, `BalanceQty`

### RULE P03 — No logic in getter
- Property getters return backing field only
- No calculations in getter

### RULE P04 — Setters that trigger recalculation

Properties whose changes require recalculating derived values:

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

### RULE P05 — XAML bindings
- Use `TargetNullValue` and `FallbackValue` on bindings to external data
- Never use `{Binding}` inside `TargetNullValue` — only static values
- Use C# computed properties for fallback logic

---

## 5. MVVM Rules

### RULE M01 — No View references from ViewModels
- ViewModels never reference Views or UI types
- Exception: `PrintableInvoiceViewModel.PrintTarget` (documented)

### RULE M02 — Commands
- All commands use `RelayCommand` from `Commands/RelayCommand.cs`
- Wire in constructor, never lazy-initialize
- CanExecute updates via `CommandManager.InvalidateRequerySuggested()`
  when needed (Gotcha #52)

### RULE M03 — Collections
- `ObservableCollection<T>` for lists bound to UI
- `List<T>` for internal filtering/sorting caches
- Assign new collection to trigger `PropertyChanged` (forces
  ItemsControl to rebuild templates — needed for theme swap)
- Add/Remove items to trigger `CollectionChanged`

### RULE M04 — Bulk operation suppression
Use `_isBulkAdding` flag to suppress events during batch operations
(see Section 7).

### RULE M05 — No code-behind business logic
Views can have code-behind ONLY for:
- `InitializeComponent()`
- Event handlers that immediately delegate to VM commands
- UI-only concerns (scroll sync, focus, drag-drop)
- Right-click ContextMenu building
- Row-level click routing (visual tree walks for `OnRowLeftClick`,
  `OnRowRightClick` in DN and PI list views)

Views MUST NOT contain:
- Database access
- Business calculations
- File I/O
- Direct model manipulation

---

## 6. Constructor Rules

### RULE C01 — Command init in constructor
All `ICommand` properties initialized in constructor.

### RULE C02 — Collection event hooks in constructor
ObservableCollection event subscriptions in constructor.

### RULE C03 — Constructor order
1. Field initializations
2. Collection event subscriptions
3. Command initializations
4. Any initial data load calls
5. Theme change subscriptions (for DashboardPageBase-derived)

---

## 7. Bulk Update Rules (CRITICAL FOR PERFORMANCE)

### RULE B01 — Bulk flag
`_isBulkAdding` flag used for ALL bulk operations. Set `true`
before loop, reset in `finally` block (never in `catch`).

### RULE B02 — Mandatory bulk pattern

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

    TotalRows = ItemRows.Count;
    RenumberRows();
    RecalculateFooter();
    foreach (var row in touchedRows)
        row.FireAllChanged();

### RULE B03 — Silent set
- Use `SetAllSilent()` when setting multiple fields on one row
- Never set individual properties in a paste loop

### RULE B04 — Fire all
- Call `FireAllChanged()` on each touched row AFTER `_isBulkAdding`
  is set back to `false`

### RULE B05 — Suppressed handlers
`_isBulkAdding` suppresses `OnItemRowsChanged` + `OnRowChanged`.

---

## 8. Calculation Rules

### RULE CA01 — Rounding per field type
| Field type    | Decimal places | Format          |
|---------------|----------------|-----------------|
| SQM values    | 3              | `"0.000"`       |
| LM values     | 3              | `"0.###"`       |
| Rate values   | 2              | `"N2"`          |
| Value (money) | 2              | `"N2"`          |

### RULE CA02 — Minimum SQM
- `MIN_SQM = 0.5m`
- Apply: `Math.Max(Math.Round(rawSqm, 3), MIN_SQM)`
- Only when `W > 0 AND H > 0`

### RULE CA03 — Division
- Always cast to `decimal` before division
- Never use integer division for SQM
- Never use `double` or `float` for money or SQM

### RULE CA04 — Footer calculations
- Never call `RecalculateFooter` inside a loop

### RULE CA05 — Decimal type mandatory
- Money: always `decimal`
- SQM: always `decimal`
- LM: always `decimal`

### RULE CA06 — Storage precision
- `decimal(18, 2)` for money
- `decimal(18, 3)` for SQM

### RULE CA07 — Rounding rule
- Never round intermediate results
- Only round on display OR at final storage step

### RULE CA08 — Balance formula (Phase 10.1+)
- **NEVER sum snapshot BalanceQty across siblings** (Gotcha #40)
- Always compute `Balance = Ordered − Delivered` at query time
- Applied per item AND per DN AND per JO

### RULE CA09 — Self-healing derived values (Phase 10.7)
- Any DB-stored derived value that depends on sibling records
  MUST be self-healed on load
- Never trust cached snapshots for cross-record derived values
- Example: DN `PreviouslyDeliveredQty` — self-heal via
  `GetPreviouslyDeliveredMapAsync`

---

## 9. Behavior Rules

### RULE BH01 — No VM type reference
- Behaviors must not reference ViewModel types directly
- Use interfaces only

### RULE BH02 — No static state
- No static fields that persist state across instances
- Exception: per-navigation-session state like `_anchorColumn`

### RULE BH03 — Double-subscribe guard
Always remove handler before adding:

    el.PreviewKeyDown -= OnKey;
    el.PreviewKeyDown += OnKey;

### RULE BH04 — Visual tree walk order
1. Walk visual tree to find row container (ListViewItem)
2. Walk visual tree to find `IExcelPasteable` DataContext
3. Fall back to logical tree as secondary

### RULE BH05 — Ctrl+V passthrough
In OnKey handler always return early for Ctrl+V; let
`DataObjectPasting` handle paste.

---

## 10. Error Handling Rules

### RULE E01 — Async load
- Wrap all async data loads in try/catch
- Fail silently for background loads with WHY comment

### RULE E02 — Paste parse fail
If `parsed.Count == 0` show MessageBox warning.

### RULE E03 — Paste success
Show success MessageBox via `Dispatcher.BeginInvoke` with
`DispatcherPriority.Background`.

### RULE E04 — No silent swallow (except async loads)
All silent catches must have comment explaining WHY.

### RULE E05 — User-facing errors
Wrap risky operations in try/catch, show MessageBox with clear message.

### RULE E06 — Global handler
`App.xaml.cs` has `DispatcherUnhandledException` handler.
Remove debug MessageBox before production.

### RULE E07 — Confirm destructive actions (Phase 10.7a)
DELETE / DISPATCH / CONVERT operations must show a MessageBox with
YesNo confirmation before executing. Include entity details in the
message so user can verify:

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

### RULE E08 — Zero-qty save warning (Phase 10.7a)
When user saves a form with no meaningful data (all quantities = 0),
show a YesNo warning before saving. Give user path to fix or override.

---

## 11. Performance Rules

### RULE PF01 — Equality guard on every setter (see RULE P01)

### RULE PF02 — Bulk flag suppression

### RULE PF03 — Dispatcher for MessageBox after collection changes

    Application.Current.Dispatcher.BeginInvoke(
        new Action(() => { MessageBox.Show(...); }),
        DispatcherPriority.Background);

### RULE PF04 — DoEvents replacement for .NET Core+
Never use `DispatcherFrame.PushFrame`.

Correct:

    Application.Current.Dispatcher.Invoke(
        DispatcherPriority.Background,
        new Action(() => { }));

### RULE PF05 — Smooth scrolling (Phase 10.9a)
For dashboards + long scrollable views, use pixel-based scrolling:

    <ScrollViewer VerticalScrollBarVisibility="Auto"
                  CanContentScroll="False"
                  PanningMode="VerticalOnly">

Do NOT combine with `ScrollViewer.VerticalScrollBarVisibility`
attached property — set VerticalScrollBarVisibility only once
(Gotcha #29).

---

## 12. XAML Rules

### RULE X01 — No code-behind logic (except allowed exceptions)

### RULE X02 — Behavior attachment
Attach behaviors in XAML.

### RULE X03 — Binding modes
| Property type              | Mode + UpdateTrigger                    |
|----------------------------|-----------------------------------------|
| Read-only calculated       | `Mode=OneWay`                           |
| Editable text/number       | `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` |
| Commands                   | bound to `Button.Command`               |

### RULE X04 — DataContext
- Set DataContext in XAML via DataTemplate or explicit binding
- Never set DataContext in code-behind

### RULE X05 — ClearType text rendering
For all major surfaces:

    TextOptions.TextFormattingMode="Display"
    TextOptions.TextRenderingMode="ClearType"
    UseLayoutRounding="True"
    SnapsToDevicePixels="True"

### RULE X06 — Property set only once (Gotcha #29)
Never set the same property on the same element twice. Especially
watch for `VerticalScrollBarVisibility` (both direct property AND
attached `ScrollViewer.VerticalScrollBarVisibility` — pick one).

### RULE X07 — Shared DataTemplates for repeated content (Phase 10.7a)
When 4+ list views use the same row layout, extract to a shared
DataTemplate resource:

    <DataTemplate x:Key="DnRowTemplate">...</DataTemplate>

    <ItemsControl ItemsSource="{Binding AllItems}"
                  ItemTemplate="{StaticResource DnRowTemplate}"/>

Avoids duplicate maintenance across tabs.

---

## 13. Theming Rules

### RULE T01 — Never hardcode colors in themeable XAML
Use `DynamicResource` for theme-aware colors.

Exception: chart bars use hex for data-driven identity.
Exception: brand-locked accent hex values (like `#059669` for Sales
parent card in Phase 10.9a) are acceptable when the intent is
"brand color that stays across themes."

### RULE T02 — Chart/data-driven colors
Chart bars pass hex strings to `ColorStringConverter`.

### RULE T03 — VM-driven card colors use brush keys
For colors set from ViewModels:

    ✅ tile.Accent = "Green";     // token string
    ✅ kpi.BadgeColor = "AccentBrush";
    ❌ kpi.BadgeColor = "#0F4C3A"; // hex (won't swap)

### RULE T04 — Instant theme swap requires re-notify
VMs displaying brush-key-driven collections MUST override
`OnThemeChanged()` to reassign ObservableCollection reference.

Phase 10.9a example:

    protected override void OnThemeChanged()
    {
        SalesTiles       = new ObservableCollection<KpiTileModel>(_salesTiles);
        ProductionTiles  = new ObservableCollection<KpiTileModel>(_productionTiles);
        DeliveryTiles    = new ObservableCollection<KpiTileModel>(_deliveryTiles);
        InventoryTiles   = new ObservableCollection<KpiTileModel>(_inventoryTiles);
        RecentActivity   = new ObservableCollection<ActivityFeedItem>(_recentActivity);
    }

### RULE T05 — Both themes must have same brush keys
Missing a key in one theme causes crashes when swapping.

---

## 14. Database Rules

### RULE D01 — Separate SQLite files per domain
- `proglass.db` — Sheet Store
- `specifications.db` — Glass specs library
- `proformainvoices.db` — Saved invoices
- `joborders.db` — Job orders
- `deliverynotes.db` — Delivery notes + attachments metadata

### RULE D02 — DbContext lifecycle
Create per operation via `using`:

    ✅ using var db = new DeliveryNotesDbContext();
    ❌ private readonly DeliveryNotesDbContext _db = new ...();

### RULE D03 — Repository is the boundary
    View → ViewModel → Repository → DbContext → SQLite

### RULE D04 — EnsureCreatedAsync in InitializeAsync
Auto-create schema on first run.

### RULE D05 — UNIQUE index on user-facing IDs
Examples: `ProformaId`, `JobOrderId`, `DeliveryNoteId`.

### RULE D06 — JSON blob + indexed columns pattern
Complex data as JSON. Searchable fields as indexed columns.

### RULE D07 — Async EF Core

    ✅ await db.DeliveryNotes.ToListAsync();
    ❌ db.DeliveryNotes.ToList();

### RULE D08 — Schema change protocol (Phase 10.7)
When adding/removing columns:
- User must delete old .db file before first run
- `EnsureCreatedAsync` doesn't migrate existing tables
- Add a note in the entity file header: `⚠️ DB schema change`

### RULE D09 — Interface preservation (Gotcha #46)
When adding new repository methods:
- ADD new methods alongside old ones
- NEVER remove or rename existing methods that other modules call
- Old methods can internally delegate to new implementations

Protected repository methods list is documented in `delivery.md`.

---

## 15. Attachment Rules (Phase 10.6)

### RULE ATT01 — File storage on disk, paths in DB
Don't blob-store files in SQLite. Use disk with GUID-safe subfolders:

    %AppData%\ProGlassAutomation\{Module}\Attachments\{Type}\{EntityId}\

### RULE ATT02 — Size limits
Max 10 MB per file. Enforce in `AttachmentLimits`.

### RULE ATT03 — Extension validation
Whitelist allowed extensions per slot:
- Image slot: JPG, PNG, BMP, GIF, WEBP
- PDF slot: PDF only

### RULE ATT04 — Rename before save
Show rename dialog with locked extension pill. User edits filename only.

### RULE ATT05 — Delete entity → delete files
Before deleting parent entity, delete all attachment files (best-effort
try/catch — files may already be gone).

---

## 16. QuestPDF Rules

### RULE Q01 — Padding methods
Use `.PaddingHorizontal()` + `.PaddingVertical()`, NOT `.Padding(h, v)`.

### RULE Q02 — License
Set `QuestPDF.Settings.License = LicenseType.Community` before
generating any document.

### RULE Q03 — Font consistency
- Font family: Segoe UI
- Default size: 9pt for content
- Colors from `DocumentStyle.*` constants

### RULE Q04 — Letterhead ShowOnce (Phase 9.5a-r4)
Letterhead on page 1 only via `.ShowOnce()`:

    page.Header().ShowOnce().Element(h =>
        LetterheadRenderer.Render(h, layout.Letterhead));

### RULE Q05 — Footer every page with DocumentId
Footer includes DocumentId so multi-page docs self-identify:

    page.Footer().Element(f =>
        FooterRenderer.Render(f, documentId));

---

## 17. WPF Print Rules

### RULE WP01 — Two rendering paths
1. On-screen preview = XAML DataBinding
2. Printed output = code-built `FixedDocument`

Any layout change must be mirrored in BOTH.

### RULE WP02 — Text rendering
Use `TextFormattingMode="Ideal"` + `TextRenderingMode="Auto"`.

### RULE WP03 — Never clone with XamlWriter.Save
Crashes on nested public types.

---

## 18. Shared Export Rules (Phase 9.5a locked pattern)

### RULE EX01 — Every module uses shared engine
Modules must implement `IPdfLayout` / `IExcelLayout` / `ICsvLayout`
from `Services/Export/`. Never write custom PDF/Excel generation.

### RULE EX02 — Layout constructor signature
`(SavedEntity entity, EntitySnapshot snapshot)` — consistent across
all modules.

### RULE EX03 — Engine instantiation inline in VM
Create `new PdfExportEngine()` in the VM method that triggers export.
No DI needed.

### RULE EX04 — Request options
Standard:

    var request = new ExportRequest
    {
        PromptForLocation = true,
        OpenAfterSave     = true
    };

### RULE EX05 — Result handling
Silent on success + silent on cancel. MessageBox only on failure:

    private static void ShowExportResult(ExportResult result, string formatLabel)
    {
        if (result.Success)   return;
        if (result.Cancelled) return;
        MessageBox.Show(
            $"{formatLabel} export failed:\n\n{result.ErrorMessage}",
            "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
    }

### RULE EX06 — EnsureLoaded guard
Every export command must check entity is loaded:

    private bool EnsureLoaded()
    {
        if (_entity != null && _snapshot != null) return true;
        MessageBox.Show(
            "Load a delivery note before exporting.",
            "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

### RULE EX07 — CanExecute for export commands
Standard predicate:

    _ => _entity != null && !IsSaving && !IsLoading

### RULE EX08 — Style tokens
- PDF: `DocumentStyle.Color*`, `DocumentStyle.FontSize*`, etc.
- Excel: `WorkbookStyle.Color*`, `WorkbookStyle.FontSize*` (STATIC — never instantiate)
- Never hardcode hex/font in layout files

### RULE EX09 — Filename convention
Auto-built by `ExportFileNamer` from `IDocumentLayout` properties:

    DN-2026-001-01_EMAAR-PROPERTIES_TOWER-A_22-07-2026.pdf
    {DocumentId}_{CompanyName}_{Reference}_{DocumentDate:dd-MM-yyyy}.{ext}

---

## 19. File Rules

### RULE F01 — One class per file
Exceptions:
- Enum + tightly-coupled meta class
- Enum + related DTO class

### RULE F02 — Never nested public classes
WPF `XamlWriter.Save()` crashes on them.

### RULE F03 — README.md per feature
Every feature sub-folder in Views/ or Services/ should have README.md.
Use 4-space indented code blocks.

### RULE F04 — File header using order

    using System;                        // System first
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore; // Third-party second
    using ProGlassAutomation.Models.X;   // App types last
    using ProGlassAutomation.Services.Y;

---

## 20. Git Commit Rules

### RULE G01 — Commit format

First line:

    ModuleName: Area: Phase-X.XX: type(scope): short description

Then blank line, then:
- 2-4 sentence summary paragraph
- Blank line
- Structured sections in ALL CAPS with colon
- `FILES ADDED:` / `FILES MODIFIED:` / `FILES DELETED:` sections
- `COMPLETED PHASES:` + `UPCOMING PHASES:` for phase-tracked commits
- End with `Phase X.X COMPLETE.` for phase-tracked commits

### RULE G02 — Type values

| Type       | Use for                                       |
|------------|-----------------------------------------------|
| feat       | New feature or capability                     |
| fix        | Bug fix                                       |
| refactor   | Code restructuring without behavior change    |
| style      | Cosmetic UI changes                           |
| docs       | Documentation only                            |
| chore      | Build, tooling, config                        |
| perf       | Performance improvement                       |
| ux         | UX polish (used in Phase 10.7a redesign)      |

### RULE G03 — Non-phase work
For work outside phase tracking:

    Module: Area: type(scope): description

No `Phase-X.XX:` segment.

### RULE G04 — Combined phase commits (Phase 10.8 + 10.8a)
When two closely-related sub-phases are completed in one session,
combine them in one commit with `Phase-X.XX+Y.YY` prefix:

    DeliveryNote: Export: Phase-10.8+10.8a: feat(export): ...

---

## 21. When Editing Files (AI + Human) — CRITICAL

### RULE ED01 — Always deliver full file
When asked to "update" a file, return the COMPLETE file byte-for-byte.
Never use `// ... rest unchanged ...` or placeholders.

### RULE ED02 — Diff-check before delivering
Before returning an updated file:
1. Compare against user's shared version
2. Confirm NO properties/methods dropped
3. Confirm existing behavior preserved
4. Only additions + explicit modifications

### RULE ED03 — When memory uncertain
If unsure whether you have the latest version:
- ASK the user to share the current version
- Do NOT guess from memory
- Do NOT reconstruct from what "should" be there

### RULE ED04 — Never remove existing code
Even legacy/backward-compat properties stay unless explicitly told
to remove them.

### RULE ED05 — Never remove interface methods (Gotcha #46)
When updating a repository interface:
- ADD new methods alongside old ones
- Old methods can delegate to new internally
- Other modules depend on the exact signatures

---

## 22. AI Collaboration Rules

### RULE AI01 — Session bootstrap
Every new AI chat MUST start by reading `Docs/Chat5-9/prompts/
p01-master-prompt.md`.

### RULE AI02 — Ask before assuming
If ambiguous, ASK:
- Which folder should a new file go in
- Whether to remove or keep an existing property
- Which of two design options user prefers
- Whether current file matches AI's memory

### RULE AI03 — Recommend when told to
When user says "use your recommendation":
- Just write the code
- Don't ask again
- State the choice made briefly

### RULE AI04 — Session handoff
At end of long sessions, generate a master prompt so next AI can
continue without context loss.

### RULE AI05 — Analysis phase (Chat 10 pattern)
When user is sharing files for analysis (before writing code):
- Read carefully
- Give short 2-4 line analysis
- Do NOT write code
- Wait for explicit "start coding" or "give updated file" request

### RULE AI06 — AI must NEVER
- Invent new folders outside the documented structure
- Modify architecture without explicit instruction
- Change formulas outside `data/d04-calculations.md`
- Add hidden defaults or silent fallbacks
- Silently drop existing properties or methods
- Use `dynamic` in pattern matching
- Use `DispatcherFrame.PushFrame`
- Hardcode `#FFFFFF` / `#000000` for theme-adaptive UI
- Remove/rename interface methods
- Batch 15+ file changes in one shot (Gotcha #45)
- Trust "same file" claims without verifying (Gotcha #50)

---

## 23. Testing Rules (currently manual)

Manual QA checklist for each phase:
- Build succeeds (no warnings for new code)
- App launches without errors
- Feature works in Light + Dark theme
- Feature works after data reload (save + reopen)
- Export formats open correctly in native apps
- Theme swap is instant on all touched pages
- No console errors during normal operation

### RULE TE01 — Post-Phase 21 requirement
Every calculation must have:
- Normal case
- Edge case (W=0, H=0)
- Boundary case (SQM exactly 0.5, exactly 4.0)
- Invalid input case
- Balance boundary (partial delivery, dispatch → Dummy)

Introduce xUnit test project post-Phase 21.

---

## 24. Gotchas Discovered (52 rules across all sessions)

Quick reference (full details in commit history + master prompt):

| # | Gotcha |
|---|--------|
| #16 | UserControl code-behind must NOT specify `: UserControl` |
| #28 | StackPanel has no Padding — use Border wrapper |
| #29 | Property cannot be set twice on same element (XLS0501) |
| #30 | Namespace MUST match folder path exactly |
| #31 | `\#` in XAML StringFormat is INVALID |
| #32 | WorkbookStyle is STATIC — never instantiate with `new` |
| #33 | CsvConfiguration.LeaveOpen removed in newer CsvHelper |
| #34 | New UserControl auto-creates `: UserControl` — remove it |
| #35 | XAML rendering silent failures — invalid XAML = blank view |
| #36 | ContentControl+DataTemplate reliable for list↔detail |
| #37 | UserControl MUST keep constructor + InitializeComponent() |
| #38 | Nested namespace collision — use string literals |
| #39 | WPF Commands don't auto-refresh — InvalidateRequerySuggested |
| #40 | Balances NEVER summed — always Ordered − Delivered |
| #41 | PREV values are snapshots — self-heal on load + save |
| #42 | Save must refresh BOTH panels (history + items grid) |
| #43 | Hot Reload cannot apply interface changes — full rebuild |
| #44 | DB schema changes → delete old .db before first run |
| #45 | NEVER batch 15+ file changes — one file at a time |
| #46 | NEVER remove/rename interface/shell VM methods |
| #47 | XAML converters — verify file+namespace+registration first |
| #48 | Reused VMs must reset ALL editable fields on load |
| #49 | One DN on screen at a time — clicking history navigates |
| #50 | Verify "same" claims before trusting pasted files |
| #51 | BrushKeyConverter tints = dark text (PrimaryForegroundBrush) |
| #52 | CanExecute predicates: call InvalidateRequerySuggested after load |

---

## 25. Forbidden List (Quick Reference)

| FORBIDDEN                              | REASON                                    |
|----------------------------------------|-------------------------------------------|
| `dynamic` in pattern matching          | CS8208 compiler error                     |
| `DispatcherFrame.PushFrame`            | CS0117 not in .NET Core+                  |
| `static _container` in behaviors       | Multi-instance state conflict             |
| Code logic in code-behind              | Breaks MVVM                               |
| Direct VM reference in behavior        | Tight coupling                            |
| Reflection when interface exists       | Fragile                                   |
| `RecalculateFooter` inside a loop      | Performance                               |
| `PropertyChanged` inside bulk loop     | Performance                               |
| Integer division for SQM               | Precision loss                            |
| `Math.Round` omitted on display        | Wrong display values                      |
| Missing setter equality guard          | Infinite bindings, perf                   |
| Hardcoded `#FFFFFF` in themeable XAML  | Breaks theme swap                         |
| Hex string in `TintColor`/`BadgeColor` | Breaks theme swap (use brush keys)        |
| `.Padding(h, v)` in QuestPDF 2024.12.2 | CS1503 Unit error                         |
| `XamlWriter.Save()` on nested types    | Runtime crash                             |
| Nested public classes                  | XamlWriter crash                          |
| `double`/`float` for money or SQM      | Precision loss                            |
| Silent exception catch (except bg load)| Debugging hell                            |
| Silent removal of existing properties  | Breaks other modules                      |
| Silent removal of interface methods    | Breaks other modules (Gotcha #46)         |
| Sum snapshot Balance across siblings   | Double-counts undelivered units (#40)     |
| Setting same property twice in XAML    | XLS0501 (Gotcha #29)                      |
| Trusting stored PreviouslyDeliveredQty | Stale snapshots (Gotcha #41)              |
| Batching 15+ file changes              | Cascading errors (Gotcha #45)             |