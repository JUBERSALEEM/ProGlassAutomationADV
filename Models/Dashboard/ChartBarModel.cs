namespace ProGlassAutomation.Models.Dashboard
{
    /// <summary>
    /// Single bar entry for any bar chart on a dashboard page.
    /// NormalizedHeight (0.0–1.0) is computed by the parent ViewModel
    /// against the max value in the set.
    /// ScaleConverter in XAML multiplies NormalizedHeight × ConverterParameter
    /// to get the actual pixel height.
    /// </summary>
    public class ChartBarModel
    {
        /// <summary>X-axis label. e.g. "Jan", "Clear", "Approved"</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Raw numeric value used for normalization and tooltip.</summary>
        public decimal RawValue { get; set; }

        /// <summary>
        /// Normalized height 0.0–1.0 relative to the tallest bar.
        /// Computed by ViewModel after all bars are known.
        /// </summary>
        public double NormalizedHeight { get; set; }

        /// <summary>Formatted string shown above the bar. e.g. "AED 45,200"</summary>
        public string ValueDisplay { get; set; } = string.Empty;

        /// <summary>Hex color for this bar. e.g. "#6366F1"</summary>
        public string BarColor { get; set; } = "#6366F1";

        /// <summary>True when this bar is the tallest — gets glow shadow.</summary>
        public bool IsHighlight { get; set; } = false;
    }
}