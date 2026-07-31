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
        /// Title text color — fixed hex, high contrast on tinted surfaces.
        /// Works on both Light and Dark theme tinted backgrounds.
        /// </summary>
        public string TitleColorHex => Severity switch
        {
            LogSeverity.Error => "#B91C1C",  // red-800
            LogSeverity.Warning => "#B45309",  // amber-700
            LogSeverity.Info => "#1D4ED8",  // blue-700
            _ => "#475569"   // slate-600
        };

        /// <summary>
        /// Amount text color — matches title for visual consistency.
        /// Bold weight applied in XAML for emphasis.
        /// </summary>
        public string AmountColorHex => Severity switch
        {
            LogSeverity.Error => "#B91C1C",
            LogSeverity.Warning => "#92400E",  // amber-800 (darker for readability)
            LogSeverity.Info => "#1E40AF",  // blue-800
            _ => "#334155"   // slate-700
        };

        /// <summary>
        /// Action link color — green for actionable, muted for informational.
        /// </summary>
        public string ActionColorHex => Severity switch
        {
            LogSeverity.Error => "#B91C1C",  // red — urgent action
            LogSeverity.Warning => "#047857",  // green — follow-up action
            LogSeverity.Info => "#4338CA",  // indigo — informational
            _ => "#475569"   // slate
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