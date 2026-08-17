// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/StatementAnalyticsRow.cs
// PROJECT: ProGlassAutomation
//
// PERF. A narrow, read-only projection for analytics aggregation (the AI
// context builder and dashboards). SavedStatement carries FullSnapshotJson
// and FilterJson; loading every blob just to sum the three money columns is
// waste. This type holds only those scalars. Intentionally NOT an entity —
// no key, never tracked, never saved.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement
{
    /// <summary>
    /// Read-only projection of <see cref="SavedStatement"/> for analytics.
    /// Excludes FullSnapshotJson / FilterJson by design.
    /// </summary>
    public sealed class StatementAnalyticsRow
    {
        public decimal TotalInvoiced { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
    }
}
