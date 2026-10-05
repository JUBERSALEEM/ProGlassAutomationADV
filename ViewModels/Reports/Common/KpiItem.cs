// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/Common/KpiItem.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared KPI Card Model for Reports
// PURPOSE: Reusable KPI card data structure for all report ViewModels
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.ViewModels.Reports.Common
{
    /// <summary>
    /// Represents a KPI (Key Performance Indicator) card for report dashboards.
    /// Used across multiple report ViewModels for consistent KPI display.
    /// </summary>
    public class KpiItem
    {
        /// <summary>
        /// Title of the KPI (e.g., "TOTAL VAT COLLECTED")
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Main value to display (e.g., "AED 1,234.56")
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Subtitle or additional context (e.g., "15 invoices")
        /// </summary>
        public string Subtitle { get; }

        /// <summary>
        /// Brush key for theming (e.g., "AccentBrush", "SuccessBrush")
        /// </summary>
        public string BrushKey { get; }

        /// <summary>
        /// Creates a new KPI card item
        /// </summary>
        /// <param name="title">KPI title</param>
        /// <param name="value">Main value to display</param>
        /// <param name="subtitle">Additional context</param>
        /// <param name="brushKey">Theme brush key</param>
        public KpiItem(string title, string value, string subtitle, string brushKey)
        {
            Title = title;
            Value = value;
            Subtitle = subtitle;
            BrushKey = brushKey;
        }
    }
}
