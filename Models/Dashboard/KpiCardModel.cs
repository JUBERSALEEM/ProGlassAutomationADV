namespace ProGlassAutomation.Models.Dashboard
{
    /// <summary>
    /// Plain data object for a single KPI stat card on any dashboard page.
    /// No INotifyPropertyChanged — the parent ViewModel owns reactivity.
    ///
    /// Phase 8.2a: Added TintColor for subtle card background tinting.
    /// Phase 8.2e: TintColor and BadgeColor now hold THEME BRUSH KEY names
    ///             (e.g. "TintIndigoBrush", "AccentBrush") instead of hex
    ///             strings, so cards adapt automatically to Light/Dark theme
    ///             swap via BrushKeyConverter. Backward-compat: hex strings
    ///             starting with '#' still resolve correctly.
    /// Phase 8.3 : Added SparklineValues for optional mini-chart under
    ///             the card value. Empty string = no sparkline shown.
    /// </summary>
    public class KpiCardModel
    {
        /// <summary>Emoji shown in the icon badge. e.g. "📄"</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Label under the value. e.g. "Total Invoices"</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Formatted string shown as the big number. e.g. "AED 124,500"</summary>
        public string DisplayValue { get; set; } = string.Empty;

        /// <summary>
        /// Theme brush KEY name for the icon badge background.
        /// e.g. "AccentBrush", "SuccessBrush", "WarningBrush", "DangerBrush",
        ///      "InfoBrush", "NeutralBrush".
        /// Resolved at render time by BrushKeyConverter — swaps with theme.
        /// Backward-compat: also accepts hex strings like "#6366F1".
        /// </summary>
        public string BadgeColor { get; set; } = "AccentBrush";

        /// <summary>
        /// Theme brush KEY name for the subtle card background tint.
        /// e.g. "TintIndigoBrush", "TintGreenBrush", "TintAmberBrush",
        ///      "TintBlueBrush", "TintRedBrush", "TintGrayBrush",
        ///      "TintPurpleBrush".
        /// Resolved at render time by BrushKeyConverter — swaps with theme:
        ///   Light theme → light pastel tint (readable dark text)
        ///   Dark theme  → dark-tinted surface (readable light text)
        /// Backward-compat: also accepts hex strings like "#EEF2FF".
        /// </summary>
        public string TintColor { get; set; } = "TintGrayBrush";

        /// <summary>Optional sub-label shown below title in smaller text.</summary>
        public string SubLabel { get; set; } = string.Empty;

        /// <summary>When true the card gets a red accent ring (used for alerts).</summary>
        public bool IsAlert { get; set; } = false;

        /// <summary>
        /// Phase 8.3 — Optional sparkline data as comma-separated numeric
        /// string (e.g. "12,15,18,14,22,25,20").
        /// Empty string = no sparkline rendered.
        /// Consumed by SparklineControl in the card template.
        /// Recommended: 7-14 data points for weekly/bi-weekly trends.
        /// </summary>
        public string SparklineValues { get; set; } = string.Empty;
    }
}