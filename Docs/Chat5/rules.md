# ProGlass Automation — Coding Rules

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

If any rule below conflicts with a "clever" solution, choose the
rule. Predictability beats brevity.

---

## 2. Language Rules

### RULE L01 — Language version
- C# 9+ features allowed
- Pattern matching allowed (**except** `dynamic` — see forbidden list)
- Records allowed for pure data
- Top-level statements NOT used
- File-scoped namespaces preferred (single top-level namespace per file)

### RULE L02 — Nullable
- Do not enable nullable reference types project-wide
- Use explicit null checks where needed
- Use `?.` and `??` operators
- Use `string?` on new code that legitimately accepts null

### RULE L03 — Async
- `async void` ONLY for:
    - Event handlers
    - `LoadFilteredSpecs()` style fire-and-forget loads
- All other async methods return `Task` or `Task<T>`
- Always try/catch inside `async void`
- All I/O operations use async/await

### RULE L04 — Access modifiers
- Explicit access modifiers always (never rely on defaults)
- `public class Foo` not `class Foo`
- One class per file, EXCEPT enum + tightly-coupled meta class
  (e.g., `PolishSubType.cs` contains both enum + `PolishSubTypeMeta`)

### RULE L05 — `var` usage
- OK when type is obvious from right-hand side:
    - `var list = new List<int>();`
    - `var count = items.Count;`
- Use explicit type when clarity helps:
    - `decimal totalValue = CalcTotal(rows);`

---

## 3. Naming Rules

### RULE N01 — Fields
- Private backing fields: `_camelCase`
- Private static fields: `_camelCase`
- Examples: `_baseRate`, `_isBulkAdding`, `_anchorColumn`

### RULE N02 — Properties
- All public properties: `PascalCase`
- Examples: `BaseRate`, `EffectiveRate`, `TotalRows`

### RULE N03 — Methods
- All methods: `PascalCase`
- Private helpers: `PascalCase`
- Examples: `RecalculateAll`, `FindRowDataContext`

### RULE N04 — Commands
- Pattern: `VerbNounCommand`
- Examples: `AddRowCommand`, `RemoveRowCommand`, `PasteExcelCommand`
- Type: always `ICommand`
- Initialized in constructor, never in field initializer

### RULE N05 — Interfaces
- Prefix with `I`
- Examples: `IExcelPasteable`, `ISpecsService`, `ISheetStoreRepository`

### RULE N06 — Event handlers
- Handler pattern: `OnNoun` or `OnNounChanged`
- Examples: `OnItemRowsChanged`, `OnRowChanged`, `OnGotFocus`

### RULE N07 — Booleans
- Prefix with: `is`, `has`, `can`, `should`, `suppress`
- Examples: `_isBulkAdding`, `IsEmpty`, `SuppressRateAutoAdjust`,
  `HasError`, `CanGoNext`

### RULE N08 — Constants
- `private const`: `PascalCase` OR `ALL_CAPS`
- Example: `MIN_SQM = 0.5m`

### RULE N09 — File naming
| Type              | Pattern                           | Example                          |
|-------------------|-----------------------------------|----------------------------------|
| ViewModel         | `{Feature}ViewModel.cs`           | `OverviewDashboardViewModel.cs`  |
| View XAML         | `{Feature}View.xaml`              | `OverviewDashboardView.xaml`     |
| View code-behind  | `{Feature}View.xaml.cs`           | `OverviewDashboardView.xaml.cs`  |
| Model             | `{ModulePrefix}{Concept}.cs`      | `DashboardActivityItem.cs`       |
| Repository iface  | `I{Feature}Repository.cs`         | `ISheetStoreRepository.cs`       |
| Repository impl   | `{Feature}Repository.cs`          | `SheetStoreRepository.cs`        |
| Service           | `{Feature}Service.cs`             | `BackupService.cs`               |
| Converter         | `{Purpose}Converter.cs`           | `BrushKeyConverter.cs`           |

### RULE N10 — Namespace mirrors folder
Folder: `ViewModels/Dashboard/Overview/`
Namespace: `ProGlassAutomation.ViewModels.Dashboard.Overview` OR
`ProGlassAutomation.ViewModels.Dashboard` (flat within module OK)

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

The `if (_field == value) return;` guard is REQUIRED. It prevents:
- Infinite loops in two-way bindings
- Unnecessary UI redraws
- Redundant event firing to subscribers

Exception: string properties use `string.Equals` or `==`.
No exceptions to this rule.

### RULE P02 — Calculated properties
- Must have `private set`
- Never set from outside the class
- Only updated inside `RecalculateAll()` or `SetAllSilent()`
- Examples: `Sqm1`, `TotalSqm`, `Value`, `Lm1`

### RULE P03 — No logic in getter
- Property getters return backing field only
- No calculations in getter

Wrong:
    get => Math.Round(_sqm1, 3)

Right:
    get => _sqm1   // rounded when set

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

### RULE P05 — XAML bindings to external data
- Use `TargetNullValue` and `FallbackValue` on all XAML bindings to
  external data
- Never use `{Binding}` inside `TargetNullValue` — it only accepts
  static values
- Use C# computed properties for fallback logic instead

---

## 5. MVVM Rules

### RULE M01 — No View references from ViewModels
- ViewModels never reference Views or UI types
- Exception: `PrintableInvoiceViewModel.PrintTarget` (documented)
  needs `FrameworkElement` for print rendering — this exception is
  called out in code comments

### RULE M02 — Commands
- All commands use `RelayCommand` from `Commands/RelayCommand.cs`
- Wire in constructor, never lazy-initialize
- CanExecute updates via `CommandManager.InvalidateRequerySuggested()`
  when needed

### RULE M03 — Collections
- `ObservableCollection<T>` for lists bound to UI
- `List<T>` for internal filtering/sorting caches
- Assign new collection to trigger `PropertyChanged` (forces
  ItemsControl to rebuild templates — needed for theme swap)
- Add/Remove items to trigger `CollectionChanged` (incremental update)

### RULE M04 — Bulk operation suppression
Use `_isBulkAdding` flag to suppress events during batch operations
(see Section 7 for full pattern).

### RULE M05 — No code-behind business logic
Views can have code-behind ONLY for:
- `InitializeComponent()`
- Event handlers that immediately delegate to VM commands
- UI-only concerns (scroll sync, focus management, drag-drop wiring)
- Right-click ContextMenu building (XAML bindings unreliable)

Views MUST NOT contain:
- Database access
- Business calculations
- File I/O
- Direct model manipulation

---

## 6. Constructor Rules

### RULE C01 — Command init in constructor
All `ICommand` properties initialized in constructor, not in field
initializer.

### RULE C02 — Collection event hooks in constructor
ObservableCollection event subscriptions in constructor.
Example: `ItemRows.CollectionChanged += OnItemRowsChanged;`

### RULE C03 — Constructor order
1. Field initializations
2. Collection event subscriptions
3. Command initializations
4. Any initial data load calls
5. Theme change subscriptions (for DashboardPageBase-derived classes)

---

## 7. Bulk Update Rules (CRITICAL FOR PERFORMANCE)

### RULE B01 — Bulk flag
`_isBulkAdding` flag used for ALL bulk operations:
- Set `true` before loop
- Reset in `finally` block (never in `catch`)

### RULE B02 — Mandatory bulk pattern

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
        _isBulkAdding = false;       // reset BEFORE batch notify
    }

    TotalRows = ItemRows.Count;
    RenumberRows();
    RecalculateFooter();
    foreach (var row in touchedRows)
        row.FireAllChanged();        // batch notify at end

**Purpose:** Prevents N×M PropertyChanged events during paste.
Keeps UI responsive during 100+ row paste.

### RULE B03 — Silent set
- Use `SetAllSilent()` when setting multiple fields on one row
- Never set individual properties in a paste loop
- `SetAllSilent()` sets all backing fields directly
- `SetAllSilent()` also recalculates derived backing fields

### RULE B04 — Fire all
- Call `FireAllChanged()` on each touched row AFTER `_isBulkAdding`
  is set back to `false`
- Never inside the bulk loop

### RULE B05 — Suppressed handlers
`_isBulkAdding` suppresses:
- `OnItemRowsChanged` logic
- `OnRowChanged` logic

Handlers must check the flag and early-return during bulk operations.

---

## 8. Calculation Rules

### RULE CA01 — Rounding per field type
| Field type    | Decimal places | Example format string |
|---------------|----------------|-----------------------|
| SQM values    | 3              | `"0.000"`             |
| LM values     | 3              | `"0.###"`             |
| Rate values   | 2              | `"N2"`                |
| Value (money) | 2              | `"N2"`                |

Never display unrounded decimals.

### RULE CA02 — Minimum SQM
- `MIN_SQM = 0.5m`
- Apply: `Math.Max(Math.Round(rawSqm, 3), MIN_SQM)`
- Only when `W > 0 AND H > 0`
- Return `0m` when `W = 0` OR `H = 0` (no MIN_SQM applied to
  non-existent glass)

### RULE CA03 — Division
- Always cast to `decimal` before division:
    - `(decimal)W1 * H1 / 1_000_000m`
- Never use integer division for SQM
- Never use `double` or `float` for money or SQM

### RULE CA04 — Footer calculations
- `RecalculateFooter` uses manual loop OR LINQ (both acceptable)
- Manual loop preferred for very large collections (100+ rows)
- Never call `RecalculateFooter` inside a loop

### RULE CA05 — Decimal type mandatory
- Money: always `decimal`
- SQM: always `decimal`
- LM: always `decimal`
- Never `double`, never `float` for these values

### RULE CA06 — Storage precision
- `decimal(18, 2)` for money
- `decimal(18, 3)` for SQM

### RULE CA07 — Rounding rule
- Never round intermediate results
- Only round on display OR at final storage step

---

## 9. Behavior Rules

### RULE BH01 — No VM type reference
- Behaviors must not reference ViewModel types directly
- Use interfaces only (e.g., `IExcelPasteable`)

### RULE BH02 — No static state
- No static fields that persist state across instances
- Exception: `_anchorColumn`, `_anchorRow` are acceptable as
  per-navigation-session state

### RULE BH03 — Double-subscribe guard
Always remove handler before adding:

    el.PreviewKeyDown -= OnKey;
    el.PreviewKeyDown += OnKey;

### RULE BH04 — Visual tree walk order
1. Walk visual tree to find row container (ListViewItem)
2. Walk visual tree to find `IExcelPasteable` DataContext
3. Fall back to logical tree as secondary option

Use `VisualTreeHelper.GetParent` first, then
`LogicalTreeHelper.GetParent` as fallback.

### RULE BH05 — Ctrl+V passthrough
In OnKey handler always return early for Ctrl+V:

    if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        return;

Let the `DataObjectPasting` event handler deal with paste.

---

## 10. Error Handling Rules

### RULE E01 — Async load
- Wrap all async data loads in try/catch
- Fail silently for background loads with WHY comment:

        catch
        {
            // sheet history unavailable — feed shows invoice
            // activity only
        }

### RULE E02 — Paste parse fail
If `parsed.Count == 0` after parsing show MessageBox:
- Title: `"Paste Failed"`
- Icon: `Warning`
- Message explains expected format

### RULE E03 — Paste success
Show success MessageBox via `Dispatcher.BeginInvoke`:
- Priority: `DispatcherPriority.Background`
- Title: `"Excel Paste"`
- Message: count of rows pasted

Never block UI thread with MessageBox during collection update.

### RULE E04 — No silent swallow (except async loads)
- Do not swallow exceptions in UI-affecting code paths
- Only silent catch in background async loads
- All silent catches must have comment explaining WHY

### RULE E05 — User-facing errors
Wrap risky operations in try/catch, show MessageBox:

    try
    {
        await someOperation();
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            $"Operation failed:\n\n{ex.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

### RULE E06 — Global handler
`App.xaml.cs` has a `DispatcherUnhandledException` handler for
crashes. Currently shows debug MessageBox — REMOVE before production
release.

---

## 11. Performance Rules

### RULE PF01 — Equality guard on every setter
Prevents unnecessary PropertyChanged chains. See RULE P01.

### RULE PF02 — Bulk flag suppression
`_isBulkAdding` suppresses `OnItemRowsChanged` + `OnRowChanged`.
Prevents N×M events during paste.

### RULE PF03 — Dispatcher for MessageBox after collection changes
Never block UI thread during collection update:

    Application.Current.Dispatcher.BeginInvoke(
        new Action(() =>
        {
            MessageBox.Show(...);
        }),
        DispatcherPriority.Background);

### RULE PF04 — DoEvents replacement for .NET Core+
Never use `DispatcherFrame.PushFrame` (CS0117 error).

Correct pattern:

    Application.Current.Dispatcher.Invoke(
        DispatcherPriority.Background,
        new Action(() => { }));

---

## 12. XAML Rules

### RULE X01 — No code-behind logic
Code-behind files contain ONLY:

    public partial class MyView : UserControl
    {
        public MyView()
        {
            InitializeComponent();
        }
    }

Nothing else. Business logic goes in ViewModels.

### RULE X02 — Behavior attachment
Attach behaviors in XAML, not code:

    behaviors:DataGridNavigationBehavior.EnableExcelNav="True"

### RULE X03 — Binding modes
| Property type              | Mode + UpdateTrigger                    |
|----------------------------|-----------------------------------------|
| Read-only calculated       | `Mode=OneWay`                           |
| Editable text/number       | `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` |
| Commands                   | bound to `Button.Command`               |

### RULE X04 — DataContext
- Set DataContext in XAML via DataTemplate or explicit binding
- Never set DataContext in code-behind (except InitializeComponent flow)

### RULE X05 — ClearType text rendering
For all major surfaces (Dashboard, Splash, print previews):

    TextOptions.TextFormattingMode="Display"
    TextOptions.TextRenderingMode="ClearType"
    UseLayoutRounding="True"
    SnapsToDevicePixels="True"

---

## 13. Theming Rules

### RULE T01 — Never hardcode colors in XAML
Use `DynamicResource` for theme-aware colors:

    ✅ Background="{DynamicResource ModuleBackgroundBrush}"
    ❌ Background="#FFFFFF"
    ❌ Background="White"

Exception: chart bars intentionally use hex for data-driven identity.

### RULE T02 — Chart/data-driven colors
Chart bars pass hex strings to `ColorStringConverter`. Never bind
to brush keys for chart data. These stay consistent across themes.

### RULE T03 — VM-driven card colors use brush keys
For colors set from ViewModels (badges, tints, status pills):

    ✅ kpi.BadgeColor = "AccentBrush";     // brush key
    ✅ kpi.TintColor  = "TintIndigoBrush"; // brush key
    ❌ kpi.BadgeColor = "#0F4C3A";         // hex (won't swap)

Use `BrushKeyConverter` in the binding.

### RULE T04 — Instant theme swap requires re-notify
VMs displaying brush-key-driven collections MUST override
`OnThemeChanged()` (or equivalent hook) to reassign the
ObservableCollection reference:

    protected override void OnThemeChanged()
    {
        KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
    }

Otherwise theme swap requires navigation round-trip to take effect.

### RULE T05 — Both themes must have same brush keys
`LightTheme.xaml` and `DarkTheme.xaml` must expose IDENTICAL brush
keys. Only values differ. Missing a key in one theme causes crashes
when swapping.

---

## 14. Database Rules

### RULE D01 — Separate SQLite files per domain
- `proglass.db` — Sheet Store
- `specifications.db` — Glass specs library
- `proformainvoices.db` — Saved invoices

### RULE D02 — DbContext lifecycle
Create per operation via `using`, never hold as field:

    ✅ using var db = new AppDbContext();
    ❌ private readonly AppDbContext _db = new AppDbContext();

### RULE D03 — Repository is the boundary
ViewModels access data through repositories. Repositories access
DbContext. Never skip layers.

    View → ViewModel → Repository → DbContext → SQLite

### RULE D04 — EnsureCreatedAsync in InitializeAsync
Auto-create schema on first run. When schema stabilizes post-Phase 21,
migrate to EF Core Migrations.

### RULE D05 — UNIQUE index on user-facing IDs
Example: `ProformaId` has UNIQUE constraint. Enables duplicate
detection.

### RULE D06 — JSON blob + indexed columns pattern
Complex nested data stored as JSON blob. Searchable fields surfaced
as real indexed columns alongside the blob.

    ProformaInvoices table:
        Id (PK)
        ProformaId (indexed, UNIQUE)
        CustomerName (indexed)
        GrandTotalVatInclusive (indexed)
        Status (indexed)
        CreatedAt (indexed)
        UpdatedAt (indexed)
        FullSnapshotJson (BLOB)

### RULE D07 — Async EF Core
Always use async EF methods when available:

    ✅ await db.GlassSheets.ToListAsync();
    ❌ db.GlassSheets.ToList();

---

## 15. QuestPDF Rules

### RULE Q01 — Padding methods
Use `.PaddingHorizontal()` + `.PaddingVertical()`, NOT
`.Padding(h, v)`.

The two-arg overload requires `Unit` parameter in QuestPDF 2024.12.2
and causes **CS1503 Unit error**.

### RULE Q02 — License
Set `QuestPDF.Settings.License = LicenseType.Community` before
generating any document.

### RULE Q03 — Font consistency
- Font family: Segoe UI
- Default size: 9pt
- Colors: match XAML preview hex values exactly

---

## 16. WPF Print Rules

### RULE WP01 — Two rendering paths
1. On-screen preview = XAML DataBinding
   (`PrintableInvoiceView.xaml`)
2. Printed output = code-built `FixedDocument`
   (`BuildFullContentStack` in VM)

These are TWO SEPARATE rendering paths — any layout change must be
mirrored in BOTH.

### RULE WP02 — Text rendering
Use `TextFormattingMode="Ideal"` + `TextRenderingMode="Auto"` for
crisp text.

### RULE WP03 — Never clone with XamlWriter.Save
Crashes on nested public types.
`PolishSubTypeMeta` was extracted from nested class specifically to
avoid this crash.

---

## 17. Export Rules

### RULE EX01 — Single source of truth
`PdfExportContext` is the single source of truth for all 3 export
formats (PDF, Excel, CSV).

### RULE EX02 — Builder from live VMs
`PdfExportContextBuilder.Build()` converts live wizard VMs → context.

### RULE EX03 — Builder from saved data
`BuildExportContextFromSnapshot()` converts saved JSON → context
(no VMs needed). Used when exporting directly from list without
loading wizard.

### RULE EX04 — File save locations
- PDFs: `%AppData%\ProGlassAutomation\PDFs\`
- Excel/CSV: `%AppData%\ProGlassAutomation\Exports\`

### RULE EX05 — Smart filenames
Pattern: `{ProformaId}_{IssueDate}.{ext}`
Example: `PI-2026-001_2026-01-15.pdf`

---

## 18. File Rules

### RULE F01 — One class per file
Exceptions (allowed):
- Enum + tightly-coupled meta class (e.g., `PolishSubType.cs`
  contains both enum + `PolishSubTypeMeta`)
- Enum + related DTO class (e.g., `TermClause.cs`)

### RULE F02 — Never nested public classes
WPF `XamlWriter.Save()` crashes on them.
Always extract to sibling class in same file or separate file.

### RULE F03 — README.md per feature
Every feature sub-folder in Views/ or Services/ layer should have a
README.md. Use 4-space indented code blocks, NOT triple backticks
(avoids markdown rendering breaks).

### RULE F04 — File header using order

    using System;                        // System first
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore; // Third-party second
    using ProGlassAutomation.Models.X;   // App types last
    using ProGlassAutomation.Services.Y;

---

## 19. Git Commit Rules

### RULE G01 — Commit format

First line:

    ModuleName: Area: Phase-X.XX: type(scope): short description

Then blank line, then:
- 2-4 sentence summary paragraph explaining WHY and WHAT
- Blank line
- Structured sections in ALL CAPS with colon:
  ROOT CAUSE, FIX APPROACH, VISUAL CHANGES, ARCHITECTURE CHANGES,
  MODELS UPDATED, VIEWMODELS UPDATED, VIEWS UPDATED, DESIGN TOKENS,
  BEHAVIOR AFTER FIX, BACKWARD COMPATIBILITY, etc.
- Each section: bullet list with dashes
- `Files modified:` section at bottom with sub-bullets using `*`
  for each change
- `Files added:` / `Files deleted:` if applicable
- End with `Phase X.X COMPLETE.` + `Next:` line

### RULE G02 — Type values

| Type       | Use for                                       |
|------------|-----------------------------------------------|
| feat       | New feature or capability                     |
| fix        | Bug fix                                       |
| refactor   | Code restructuring without behavior change    |
| style      | Cosmetic UI changes (colors, spacing, fonts)  |
| docs       | Documentation only                            |
| chore      | Build, tooling, config                        |
| perf       | Performance improvement                       |

### RULE G03 — Non-phase work
For work outside phase tracking (splash polish, minor fixes):

    Module: Area: type(scope): description

No `Phase-X.XX:` segment. Keep detailed body format.

### RULE G04 — Special sections
- `BUILD ITERATION NOTES` — for debugging history worth preserving
- `KNOWN OPEN ITEM` — for deferred issues explicitly not addressed

---

## 20. When Editing Files (AI + Human)

### RULE ED01 — Always deliver full file
When asked to "update" a file, return the COMPLETE file byte-for-byte.
Never use `// ... rest unchanged ...` or `// existing code here`
placeholders.

### RULE ED02 — Diff-check before delivering
Before returning an updated file:
1. Compare against user's shared version
2. Confirm NO properties/methods dropped
3. Confirm existing behavior preserved
4. Only additions + explicit modifications

### RULE ED03 — When memory uncertain
If unsure whether you have the latest version of a file:
- ASK the user to share the current version
- Do NOT guess from memory
- Do NOT reconstruct from what "should" be there

### RULE ED04 — Never remove existing code
Even legacy/backward-compat properties stay unless explicitly told
to remove them.

**Rationale:** Multiple sessions of AI-generated code have accumulated
backward-compat properties. Removing them silently causes regressions
in modules the AI cannot see.

---

## 21. AI Collaboration Rules

### RULE AI01 — Session bootstrap
Every new AI chat MUST start by reading `Docs/Chat5/prompts/
p01-master-prompt.md`. That prompt loads full project context.

### RULE AI02 — Ask before assuming
If any of these are ambiguous, ASK:
- Which folder should a new file go in
- Whether to remove or keep an existing property
- Which of two design options user prefers
- Whether current file matches AI's memory

### RULE AI03 — Recommend when told to
When user says "use your recommendation" or "use recommended":
- Just write the code
- Don't ask again
- State the choice made briefly in the response

### RULE AI04 — Session handoff
At end of long sessions, generate a handoff script so the next AI
session can continue without context loss.

### RULE AI05 — AI must NEVER
- Invent new folders outside the documented structure
- Modify architecture without explicit instruction
- Change formulas outside `data/d04-calculations.md`
- Add hidden defaults or silent fallbacks
- Silently drop existing properties or methods
- Use `dynamic` in pattern matching
- Use `DispatcherFrame.PushFrame`
- Hardcode `#FFFFFF` / `#000000` for theme-adaptive UI

---

## 22. Testing Rules (currently manual)

No unit test project exists yet. Manual QA checklist for each phase:

- Build succeeds (no warnings for new code)
- App launches without errors
- Feature works in Light + Dark theme
- Feature works after data reload (save + reopen)
- Export formats (PDF/Excel/CSV) open correctly in native apps
- Theme swap is instant on all touched pages
- No console errors during normal operation

### RULE TE01 — Post-Phase 21 requirement
Every calculation must have:
- Normal case
- Edge case (W=0, H=0)
- Boundary case (SQM exactly 0.5, exactly 4.0)
- Invalid input case (negative, non-numeric)

Introduce xUnit test project post-Phase 21 for repositories +
calculators.

---

## 23. Forbidden List (Quick Reference)

| FORBIDDEN                              | REASON                                    |
|----------------------------------------|-------------------------------------------|
| `dynamic` in pattern matching          | CS8208 compiler error                     |
| `is dynamic x` pattern                 | CS8208 compiler error                     |
| `DispatcherFrame.PushFrame`            | CS0117 not in .NET Core+                  |
| `static _container` in behaviors       | Multi-instance state conflict             |
| Code logic in code-behind              | Breaks MVVM                               |
| Direct VM reference in behavior        | Tight coupling                            |
| Reflection when interface exists       | Fragile, use interface                    |
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