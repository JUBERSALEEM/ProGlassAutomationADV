namespace ProGlassAutomation.Models.Dashboard
{
    /// <summary>
    /// One slice of a shared dashboard donut / ring chart.
    /// Used by all 7 module dashboards — do not invent a second model.
    /// </summary>
    public class ChartSliceModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal RawValue { get; set; }
        public string ValueDisplay { get; set; } = string.Empty;

        /// <summary>Green | Blue | Amber | Red | (default accent).</summary>
        public string Accent { get; set; } = "";

        /// <summary>Optional hex override. Prefer Accent so Dark theme works.</summary>
        public string SliceColor { get; set; } = "";
    }
}
