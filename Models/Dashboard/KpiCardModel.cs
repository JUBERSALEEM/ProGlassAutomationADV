namespace ProGlassAutomation.Models.Dashboard
{
    /// <summary>
    /// Plain data object for a single KPI stat card on any dashboard page.
    /// No INotifyPropertyChanged — the parent ViewModel owns reactivity.
    /// Phase 8.2a: Added TintColor for subtle card background tinting.
    /// </summary>
    public class KpiCardModel
    {
        /// <summary>Emoji shown in the icon badge. e.g. "📄"</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Label under the value. e.g. "Total Invoices"</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Formatted string shown as the big number. e.g. "AED 124,500"</summary>
        public string DisplayValue { get; set; } = string.Empty;

        /// <summary>Hex color string for the icon badge background. e.g. "#6366F1"</summary>
        public string BadgeColor { get; set; } = "#6366F1";

        /// <summary>
        /// Hex color string for the subtle card background tint.
        /// Should be a very light version of BadgeColor.
        /// e.g. BadgeColor="#6366F1" → TintColor="#EEF2FF"
        /// </summary>
        public string TintColor { get; set; } = "#F9FAFB";

        /// <summary>Optional sub-label shown below title in smaller text.</summary>
        public string SubLabel { get; set; } = string.Empty;

        /// <summary>When true the card gets a red accent ring (used for alerts).</summary>
        public bool IsAlert { get; set; } = false;
    }
}