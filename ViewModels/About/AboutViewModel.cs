// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/About/AboutViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: About (Chat 23 v3.0)
// VERSION: Chat 23 v3.0 — Remove Stats duplication with hero pillars
//
// CHANGES vs v2.9:
//   ✓ REMOVED `Stats` IReadOnlyList collection
//     Reason: it duplicated the hero LeftPillars/RightPillars metrics
//     (both showed "8 MODULES", "5 DATABASES", "AI"). Two consecutive
//     sections with the same numbers = amateur ERP design.
//     Hero pillars are the single source of at-a-glance metrics now.
//   ✓ REMOVED `StatTile` record (no longer consumed by any view)
//     Removing dead code prevents future confusion and lint warnings.
//   ✓ ALL other v2.9 properties/collections preserved verbatim
//     (LeftPillars, RightPillars, SalesCycle, ProductionStages,
//     DispatchBilling, Modules, TechStack, CapabilitiesTop,
//     CapabilitiesBottom, all 7 core string properties)
//
// PRESERVED (zero regression):
//   • ALL 7 core string properties (AppName, AppTagline, Version,
//     DeveloperName, Country, Email, Copyright)
//   • Version v1.1.0
//   • Dynamic copyright year (2026 floor)
//   • LeftPillars / RightPillars (hero triptych)
//   • SalesCycle / ProductionStages / DispatchBilling (workflow)
//   • Modules (8 module chips)
//   • TechStack (8 tech items)
//   • CapabilitiesTop / CapabilitiesBottom (split capability rows)
//   • All records except StatTile: HeroPillar, WorkflowStage,
//     ModuleInfo, TechItem, CapabilityInfo
//   • ViewModelBase inheritance
//   • sealed class + file-scoped namespace + XML docs
//
// PERFORMANCE:
//   Same as v2.9. Removing 1 collection + 1 record = marginal memory
//   reduction. Zero PropertyChanged, zero LINQ, zero async — VM is
//   still static get-only initializers.
//
// REGRESSION RISK: ZERO
//   Only unused/duplicating code removed. All active bindings preserved.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.Licensing;

namespace ProGlassAutomation.ViewModels.About;

/// <summary>
/// View-model for the About screen.
///
/// Exposes read-only metadata about the application: brand identity,
/// version, developer attribution, legal copyright, hero decorative
/// pillars, and typed collections for workflow stages, modules, tech
/// stack, and capabilities.
///
/// All properties are static get-only initializers — this VM never
/// raises <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// events. Safe for use as a page-level singleton if desired.
///
/// Copyright year is computed dynamically at construction time so the
/// footer stays current across multi-year deployment lifetimes, with a
/// minimum floor of 2026 to prevent regression if the host system clock
/// is misconfigured.
/// </summary>
public sealed class AboutViewModel : ViewModelBase
{
    /// <summary>
    /// Minimum year floor for the copyright line.
    /// Prevents display of "© 2024" or earlier if the host machine's
    /// system clock is set incorrectly (common on offline factory PCs).
    /// </summary>
    private const int CopyrightYearFloor = 2026;

    // ══════════════════════════════════════════════════════════════════
    //  BRAND IDENTITY
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Application name shown in the hero header.</summary>
    public string AppName { get; } = "ProGlass Automation";

    /// <summary>One-line tagline shown under the app name.</summary>
    public string AppTagline { get; } = "Made in India · for the UAE glass industry";

    /// <summary>
    /// Semantic version string shown in the version pill.
    /// v2 — single source of truth lives in Services/Updates/AppInfo.
    /// </summary>
    public string Version { get; } =
        "v" + Services.Updates.AppInfo.Version;

    /// <summary>
    /// The current license status as a bindable view
    /// model. Singleton — the same instance is shared
    /// with the Settings → Data → Database Health card
    /// and any future license surface. Bound by the
    /// hero-card "Edition" badge and the License section
    /// in the About tab.
    /// </summary>
    public LicenseStatusViewModel LicenseStatus =>
        LicenseStatusViewModel.Instance;

    /// <summary>
    /// v3 OTA §25 — build label derived from the build artifact date.
    /// Never hard-coded; comes from AppInfo (assembly identity).
    /// </summary>
    public string Build { get; } =
        "Build " + Services.Updates.AppInfo.BuildLabel;

    // ══════════════════════════════════════════════════════════════════
    //  TABBED SECTIONS (Chat 24 — About split into switchable sections)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Top tab labels. Each tab maps to a separate section UserControl in
    /// Views/About/Sections, so a section can be edited on its own.
    /// </summary>
    public IReadOnlyList<string> Tabs { get; } = new[]
    {
        "About",
        "Modules",
        "Developer"
    };

    /// <summary>
    /// Index of the visible tab (0 = About, 1 = Modules, 2 = Developer).
    /// Raises PropertyChanged so the section views can switch visibility.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex == value) return;
            _selectedTabIndex = value;
            OnPropertyChanged();
        }
    }
    private int _selectedTabIndex;

    // ══════════════════════════════════════════════════════════════════
    //  PRIVACY (v2) — plain-language data handling summary
    // ══════════════════════════════════════════════════════════════════

    public IReadOnlyList<string> PrivacyLines { get; } = new[]
    {
        "All business data stays on this computer — local SQLite files in your user profile.",
        "API keys are encrypted with Windows user-level protection (DPAPI); never plain text, never sent anywhere except the AI provider you configure.",
        "The activity log records meaningful actions only (create, edit, delete, print, login) — never keystrokes or mouse movement.",
        "Technical logs contain error details and context, not invoice contents or personal data.",
        "Backups are written to your local Backups folder; nothing is uploaded automatically.",
        "The AI assistant sends only your questions plus the business summary you allow (Settings → Data Access)."
    };

    // ══════════════════════════════════════════════════════════════════
    //  DEVELOPER ATTRIBUTION
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Developer's display name.</summary>
    public string DeveloperName { get; } = "Juber Saleem";

    /// <summary>Developer's city + state + country.</summary>
    public string Country { get; } = "Baraut, Uttar Pradesh, India";

    /// <summary>
    /// Developer contact email.
    /// Bound to a clickable pill in AboutView — click launches
    /// the OS default mailto handler via AboutView.xaml.cs.
    /// </summary>
    public string Email { get; } = "Jubersaleem@gmail.com";

    // ══════════════════════════════════════════════════════════════════
    //  LEGAL
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dynamically-computed copyright line.
    /// Format: "© {year} ProGlass Automation. Developed by Juber Saleem. All rights reserved."
    /// </summary>
    public string Copyright { get; } = BuildCopyright();

    // ══════════════════════════════════════════════════════════════════
    //  HERO TRIPTYCH PILLARS (Chat 23 v2.9 — v3.0 kept, now sole source
    //  of at-a-glance metrics after Stats duplicate was removed)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Left-side vertical pillars in the hero card.
    /// Emphasis on QUANTITY / IDENTITY:
    ///   Modules count · Made in India · Theme flexibility.
    /// </summary>
    public IReadOnlyList<HeroPillar> LeftPillars { get; } = new[]
    {
        new HeroPillar("\uE9F9", "8",      "MODULES"),
        // E909 (Globe), NOT the India flag emoji. HeroPillarTemplate renders
        // this through FontFamily="Segoe MDL2 Assets", which contains no
        // emoji glyphs, so a flag would fall back to .notdef boxes on
        // screen. Every pillar glyph must therefore be an MDL2 codepoint.
        new HeroPillar("\uE909", "INDIA", "MADE IN"),
        new HeroPillar("\uE790", "DARK",  "+ LIGHT"),
    };

    /// <summary>
    /// Right-side vertical pillars in the hero card.
    /// Emphasis on ARCHITECTURE / CAPABILITY:
    ///   💾 Isolated databases · 🔒 Offline-first · 🤖 AI-ready.
    /// </summary>
    public IReadOnlyList<HeroPillar> RightPillars { get; } = new[]
    {
        new HeroPillar("\uE74E", "5",       "DATABASES"),
        new HeroPillar("\uE72E", "OFFLINE", "FIRST"),
        new HeroPillar("\uE8F2", "AI",      "READY"),
    };

    // ══════════════════════════════════════════════════════════════════
    //  END-TO-END WORKFLOW STAGES (Chat 23 v2.8)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sales cycle: from raw inventory to a released production order.
    /// Rendered as 3 separate cards in a centered row.
    /// </summary>
    public IReadOnlyList<WorkflowStage> SalesCycle { get; } = new[]
    {
        new WorkflowStage("\uE7B8", "Sheet Store",
            "Raw glass inventory · Buy/Use tracking · SQM pricing"),
        new WorkflowStage("\uE8A5", "Proforma Invoice",
            "7-step wizard · Quotation · Customer approval"),
        new WorkflowStage("\uE822", "Job Order",
            "Convert PI · Priority · Factory sheet · Status tracking"),
    };

    /// <summary>
    /// Production stages (upcoming release wave).
    /// Rendered as 8 separate cards in a 2×4 grid.
    /// </summary>
    public IReadOnlyList<WorkflowStage> ProductionStages { get; } = new[]
    {
        new WorkflowStage("\uE8C6",  "Cutting",
            "CNC-guided cuts · Waste optimization · Piece labels"),
        new WorkflowStage("\uE71D",  "Holes & Cutouts",
            "Circular / rectangular · Position tracking · QC gate"),
        new WorkflowStage("\uE895",  "Polish",
            "Edge finishing · Grit selection · Batch grouping"),
        new WorkflowStage("\uE774",  "Edge Deletion",
            "Coating removal for tempered/insulated units"),
        new WorkflowStage("\uE7E8",  "Mitring",
            "Angled edges for corner assemblies"),
        new WorkflowStage("\uE728",  "Tempering",
            "Heat treatment · Batch logging · Fragmentation test"),
        new WorkflowStage("\uE71B",  "Pairing",
            "Match panes for laminated / insulated units"),
        new WorkflowStage("\uE81C",  "Doubling / DGU",
            "Double-glazed unit assembly · Spacer selection · Seal"),
    };

    /// <summary>
    /// Dispatch and billing stages: from packed delivery to sales analytics.
    /// Rendered as 3 separate cards in a centered row.
    /// </summary>
    public IReadOnlyList<WorkflowStage> DispatchBilling { get; } = new[]
    {
        new WorkflowStage("\uE7F4", "Delivery Note",
            "Partial deliveries · Sibling chain · Photo proof"),
        new WorkflowStage("\uE8C8", "Tax Invoice",
            "FTA-compliant · UAE 5% VAT · Zero-rated export support"),
        new WorkflowStage("\uE9F9", "Sales Report",
            "Account statements · Aging · Top customers · Trend analytics"),
    };

    // ══════════════════════════════════════════════════════════════════
    //  ACTIVE MODULES (Chat 23 v2.6)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Live modules shipped in the current release.
    /// Each entry renders as a chip in the "Active Modules" card.
    /// Ordered by user-facing importance (Sales → Production → Dispatch).
    /// </summary>
    public ObservableCollection<ModuleInfo> Modules { get; } = new()
    {
        new ModuleInfo("\uE7B8", "Sheet Store",
            "Raw glass inventory · SQM pricing · Buy/Use tracking"),
        new ModuleInfo("\uE8A5", "Proforma Invoice",
            "7-step wizard · SGU/DGU/LAM · Excel-like paste"),
        new ModuleInfo("\uE822", "Job Order",
            "Convert PI · Priority · Status tracking · Factory sheet"),
        new ModuleInfo("\uE7F4", "Delivery Note",
            "Partial deliveries · Sibling chain · Photo proof"),
        new ModuleInfo("\uE8C8", "Tax Invoice",
            "VAT-compliant · Partial invoices · Family tracking"),
        new ModuleInfo("\uE81C", "Statements & Reports",
            "Account statements · Sales report · PDF export"),
        new ModuleInfo("\uE72E", "Users · Audit · Activity",
            "Roles & permissions · Before/after diffs · Live log"),
        new ModuleInfo("\uE9F9", "Multi-page Dashboard",
            "Real-time KPIs · Activity feed · Cross-module stats"),
        new ModuleInfo("\uE72D", "Shared Export Engine",
            "PDF (QuestPDF) · Excel (ClosedXML) · CSV · Print"),
        new ModuleInfo("\uE8F2", "AI Assistant",
            "Gemini-powered · Glass industry expertise · Chat"),
        new ModuleInfo("\uE790", "Light + Dark Themes",
            "Instant swap · 25 brushes · Design system foundation"),
    };

    // ══════════════════════════════════════════════════════════════════
    //  TECHNOLOGY STACK (Chat 23 v2.6)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runtime/library stack shown in the Technology Stack card.
    /// Ordered platform → data → pattern → language → libs → AI.
    /// </summary>
    public ObservableCollection<TechItem> TechStack { get; } = new()
    {
        new TechItem("\uE770",  "PLATFORM",     ".NET 8 · WPF"),
        new TechItem("\uE74E",  "DATABASE",     "SQLite · EF Core 8"),
        new TechItem("\uE9F9",  "PATTERN",      "MVVM Strict"),
        new TechItem("\uE765",  "LANGUAGE",     "C# 12"),
        new TechItem("\uE8A5",  "PDF ENGINE",   "QuestPDF 2024.12"),
        new TechItem("\uE9F9",  "EXCEL ENGINE", "ClosedXML 0.105"),
        new TechItem("\uE8C8",  "CSV ENGINE",   "CsvHelper 33.1"),
        new TechItem("\uE8F2",  "AI PROVIDER",  "Google Gemini"),
    };

    // ══════════════════════════════════════════════════════════════════
    //  KEY CAPABILITIES — split into two groups of three
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Top group of 3 capabilities.
    /// Rendered in a centered row above <see cref="CapabilitiesBottom"/>.
    /// </summary>
    public IReadOnlyList<CapabilityInfo> CapabilitiesTop { get; } = new[]
    {
        new CapabilityInfo("\uE8EF", "Deterministic Calculations",
            "Every formula produces the same result every time. No hidden state."),
        new CapabilityInfo("\uE8C8", "Excel-Like Data Entry",
            "Paste 150+ rows from Excel · Tab/Enter navigation · Auto-numbering"),
        new CapabilityInfo("\uE71B", "Sibling Delivery Chain",
            "Track partial deliveries · Auto-create Dummy DNs for balance qty"),
    };

    /// <summary>
    /// Bottom group of 3 capabilities.
    /// Rendered in a centered row below <see cref="CapabilitiesTop"/>.
    /// </summary>
    public IReadOnlyList<CapabilityInfo> CapabilitiesBottom { get; } = new[]
    {
        new CapabilityInfo("\uE722", "Photo Proof of Delivery",
            "Attach signed copies + site photos to every delivery note"),
        new CapabilityInfo("\uE790", "Instant Theme Swap",
            "Light + Dark modes with 25-brush palette — swap without reload"),
        new CapabilityInfo("\uE74E", "Auto Backup on Exit",
            "Keeps last 7 database copies · Never lose a day of work"),
    };

    // ══════════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Computes the copyright string at construction time.
    /// Uses <see cref="Math.Max(int, int)"/> to enforce the 2026 floor.
    /// </summary>
    private static string BuildCopyright()
    {
        int year = Math.Max(DateTime.Now.Year, CopyrightYearFloor);
        return $"© {year} ProGlass Automation. Made in India by Juber Saleem. All rights reserved.";
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  TYPED DATA CARRIERS
//  Sealed for JIT inlining. Immutable via init-only setters.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Single module chip rendered in the "Active Modules" card.
/// Contains display metadata only — no behavior.
/// </summary>
public sealed record ModuleInfo(string Icon, string Name, string Description);

/// <summary>
/// Single row in the Technology Stack card.
/// Icon + uppercase label + value string.
/// </summary>
public sealed record TechItem(string Icon, string Label, string Value);

/// <summary>
/// Single capability tile in the Key Capabilities section.
/// Icon is a topic-specific emoji, not a generic checkmark.
/// </summary>
public sealed record CapabilityInfo(string Icon, string Title, string Description);

/// <summary>
/// Single workflow stage rendered as a separate card in the
/// End-to-End Production Workflow section.
/// </summary>
public sealed record WorkflowStage(string Icon, string Name, string Description);

/// <summary>
/// Vertical decorative pillar shown on the LEFT or RIGHT side of the
/// hero card. Small icon + short value + tiny uppercase label.
/// Sole source of at-a-glance metrics on the About page (v3.0).
/// </summary>
public sealed record HeroPillar(string Icon, string Value, string Label);