// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/LogSeverity.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Statement Generator Redesign
//
// PURPOSE:
//   Severity levels for processing log entries displayed in the
//   terminal-style console of the redesigned Statement Generator.
//   Each value maps to a fixed display color for consistent
//   terminal aesthetics across Light and Dark themes.
//
// CONSUMERS:
//   • LogEntry.cs                — Severity property type
//   • WorkflowWarningItem.cs    — Severity property type
//   • StatementGeneratorVM      — ProcessingLog + WorkflowWarnings
//   • StatementGeneratorView    — terminal ItemsControl foreground
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>
    /// Severity levels for Statement Generator processing log entries.
    /// Colors are terminal-optimized (high contrast on dark background).
    /// </summary>
    public enum LogSeverity
    {
        /// <summary>Informational message — blue (#60A5FA).</summary>
        Info = 0,

        /// <summary>Step/action succeeded — green (#34D399).</summary>
        Success = 1,

        /// <summary>Non-fatal issue detected — amber (#FBBF24).</summary>
        Warning = 2,

        /// <summary>Step/action failed — red (#F87171).</summary>
        Error = 3,

        /// <summary>Section separator line — purple (#A78BFA).</summary>
        Section = 4
    }
}