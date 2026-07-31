// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/LogEntry.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Statement Generator Redesign
//
// PURPOSE:
//   Immutable model for one line in the terminal-style processing log
//   displayed in Section 4 of the redesigned Statement Generator.
//   Each entry has a severity level that determines its display color
//   in the monospace terminal console.
//
// DESIGN:
//   Init-only properties — entries are created once and never modified.
//   Safe to create from any thread and add to ObservableCollection
//   via Dispatcher.Invoke (existing pattern from WorkflowStepState).
//
//   SeverityColor returns hex strings optimized for high contrast on
//   dark terminal background (#0F172A). These are NOT theme brushes —
//   they are terminal-specific accent colors that work identically in
//   both Light and Dark themes because the terminal surface itself is
//   always dark.
//
// CONSUMERS:
//   • StatementGeneratorViewModel.ProcessingLog
//   • StatementGeneratorView.xaml Section 4 terminal ItemsControl
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>
    /// One line in the processing log terminal.
    /// Immutable — created once, bound directly in XAML ItemsControl.
    /// </summary>
    public sealed class LogEntry
    {
        /// <summary>Severity level — determines foreground color.</summary>
        public LogSeverity Severity { get; init; }

        /// <summary>
        /// Full text of the log line including any inline markers.
        /// Example: "› ✓ Found 8 matching invoices"
        /// Example: "› ✗ DN_2024_048.pdf not found in repository"
        /// Example: "═══ DELIVERY NOTE MATCHING ═══"
        /// </summary>
        public string Text { get; init; } = "";

        // ── Computed display properties (all pure, no side effects) ───────

        /// <summary>
        /// Hex color for XAML Foreground binding via ColorStringConverter.
        /// Terminal-optimized: high contrast on dark background (#0F172A).
        /// Same in both Light and Dark themes (terminal is always dark).
        /// </summary>
        public string SeverityColor => Severity switch
        {
            LogSeverity.Info => "#60A5FA",  // blue-400
            LogSeverity.Success => "#34D399",  // green-400
            LogSeverity.Warning => "#FBBF24",  // amber-400
            LogSeverity.Error => "#F87171",  // red-400
            LogSeverity.Section => "#A78BFA",  // purple-400
            _ => "#94A3B8"   // slate-400 fallback
        };

        /// <summary>
        /// Font weight for XAML binding.
        /// Section separators and errors are bold for visual scanning.
        /// </summary>
        public string FontWeightDisplay => Severity switch
        {
            LogSeverity.Section => "Bold",
            LogSeverity.Error => "Bold",
            _ => "Normal"
        };
    }
}