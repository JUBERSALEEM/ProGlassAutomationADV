// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/WorkflowWarningItem.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Statement Generator Redesign
//
// PURPOSE:
//   Immutable display model for one warning/alert card in Section 4
//   of the redesigned Statement Generator.
//   Built from WorkflowResult.AllWarnings + WorkflowStepState data
//   after a workflow run completes.
//
// DESIGN:
//   Each warning card shows:
//     • Colored icon (severity-based emoji)
//     • Bold title (what happened)
//     • Detail text (context + specifics)
//     • Amount display (AED value if applicable)
//     • Action label (suggested next step)
//     • Optional quantity breakdown (Ordered / Delivered / Difference)
//
//   Background colors use theme-aware BrushKey strings resolved via
//   BrushKeyConverter — adapts to Light/Dark theme automatically.
//   Accent colors (title, amount, action) are fixed hex — they are
//   high-contrast accent colors that work on both tinted surfaces.
//
// CONSUMERS:
//   • StatementGeneratorViewModel.WorkflowWarnings
//   • StatementGeneratorView.xaml Section 4 warnings ItemsControl
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>
    /// What the "action" button on a warning/alert card should do when
    /// clicked. Resolved by StatementGeneratorViewModel.OpenWarningActionCommand
    /// via callbacks wired by MainViewModel (navigate + open the related
    /// document), so the action is real — not a dead label.
    /// </summary>
    public enum WarningActionKind
    {
        /// <summary>No action — hide the button.</summary>
        None = 0,

        /// <summary>Open the related Tax Invoice (target = invoice number).</summary>
        OpenInvoice = 1,

        /// <summary>Create a Tax Invoice from the related Delivery Note (target = DN id).</summary>
        CreateInvoiceFromDn = 2,

        /// <summary>Open the related Proforma Invoice (target = proforma id).</summary>
        OpenProforma = 3,

        /// <summary>Open the generated statement PDF.</summary>
        OpenPdf = 4,

        /// <summary>Jump to the Review step.</summary>
        GoToReview = 5
    }

    /// <summary>
    /// One warning/alert card displayed in the processing results section.
    /// Immutable — built from WorkflowResult after workflow completes.
    /// </summary>
    public sealed class WorkflowWarningItem
    {
        // ── Content ───────────────────────────────────────────────────────

        /// <summary>
        /// Severity level — determines card color scheme.
        /// Uses LogSeverity for consistency (Error, Warning, Info).
        /// </summary>
        public LogSeverity Severity { get; init; } = LogSeverity.Warning;

        /// <summary>
        /// Icon emoji displayed on the left side of the card.
        /// Example: "💔" for overdue, "🟡" for partial delivery.
        /// </summary>
        public string Icon { get; init; } = "⚠";

        /// <summary>
        /// Bold title — what happened.
        /// Example: "Partial Delivery", "Missing DN", "Overdue Invoice"
        /// </summary>
        public string Title { get; init; } = "";

        /// <summary>
        /// Description text with context and specifics.
        /// Example: "DN-048 delivered 289 of 310 units (21 units short)"
        /// </summary>
        public string Detail { get; init; } = "";

        /// <summary>
        /// AED amount display (if applicable). Empty string if N/A.
        /// Example: "AED 45,200", "AED 15,180", "N/A"
        /// </summary>
        public string AmountDisplay { get; init; } = "";

        /// <summary>
        /// Suggested action label with arrow prefix.
        /// Example: "→ Send reminder", "→ Follow up", "→ Upload DN"
        /// </summary>
        public string ActionLabel { get; init; } = "";

        /// <summary>
        /// What the action button does when clicked (see WarningActionKind).
        /// Drives whether the action is rendered as a clickable button.
        /// </summary>
        public WarningActionKind ActionKind { get; init; } = WarningActionKind.None;

        /// <summary>
        /// Target identifier for the action — an invoice number, delivery-note
        /// id, or proforma id depending on ActionKind. Empty when None.
        /// </summary>
        public string TargetId { get; init; } = "";

        /// <summary>True when the card has a clickable action button.</summary>
        public bool HasAction =>
            ActionKind != WarningActionKind.None &&
            !string.IsNullOrWhiteSpace(ActionLabel);

        /// <summary>
        /// Which workflow step generated this warning.
        /// Example: "Step 3 · Delivery Notes", "Step 6 · Coverage"
        /// </summary>
        public string StepLabel { get; init; } = "";

        // ── Optional quantity breakdown ───────────────────────────────────

        /// <summary>True when the card should show Ordered/Delivered/Difference row.</summary>
        public bool HasQuantities { get; init; }

        /// <summary>Ordered quantity text. Example: "310"</summary>
        public string OrderedText { get; init; } = "";

        /// <summary>Delivered quantity text. Example: "289"</summary>
        public string DeliveredText { get; init; } = "";

        /// <summary>Difference text. Example: "-21"</summary>
        public string DifferenceText { get; init; } = "";

        // ── Computed colors ───────────────────────────────────────────────

        /// <summary>
        /// Card background — theme-aware brush key string.
        /// Resolved via BrushKeyConverter in XAML:
        ///   Background="{Binding BackgroundBrushKey,
        ///       Converter={StaticResource BrushKeyConverter}}"
        /// Adapts to Light/Dark theme automatically.
        /// </summary>
        public string BackgroundBrushKey => Severity switch
        {
            LogSeverity.Error => "TintRedBrush",
            LogSeverity.Warning => "TintAmberBrush",
            LogSeverity.Info => "TintBlueBrush",
            _ => "TintGrayBrush"
        };

        /// <summary>
        /// Title text color — theme brush key resolved via BrushKeyConverter.
        /// Semantic + theme-aware (readable on the light/dark tinted surfaces).
        /// </summary>
        public string TitleBrushKey => Severity switch
        {
            LogSeverity.Error => "DangerBrush",
            LogSeverity.Warning => "WarningBrush",
            LogSeverity.Info => "AccentBrush",
            _ => "MutedForegroundBrush"
        };

        /// <summary>
        /// Amount text color — theme brush key (matches title semantics).
        /// Bold weight applied in XAML for emphasis.
        /// </summary>
        public string AmountBrushKey => Severity switch
        {
            LogSeverity.Error => "DangerBrush",
            LogSeverity.Warning => "WarningBrush",
            LogSeverity.Info => "AccentBrush",
            _ => "MutedForegroundBrush"
        };

        /// <summary>
        /// Action link color — theme brush key.
        /// Green follow-up for warnings, danger for errors.
        /// </summary>
        public string ActionBrushKey => Severity switch
        {
            LogSeverity.Error => "DangerBrush",
            LogSeverity.Warning => "SuccessBrush",
            LogSeverity.Info => "AccentDarkBrush",
            _ => "MutedForegroundBrush"
        };

        /// <summary>
        /// Severity label for the badge pill (if shown).
        /// Example: "ERROR", "WARNING", "INFO"
        /// </summary>
        public string SeverityLabel => Severity switch
        {
            LogSeverity.Error => "ERROR",
            LogSeverity.Warning => "WARNING",
            LogSeverity.Info => "INFO",
            _ => ""
        };

        /// <summary>
        /// Severity badge background color hex.
        /// </summary>
        public string SeverityBadgeColorHex => Severity switch
        {
            LogSeverity.Error => "#B91C1C",
            LogSeverity.Warning => "#B45309",
            LogSeverity.Info => "#3B82F6",
            _ => "#94A3B8"
        };
    }
}