using System.Collections.Generic;

namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// Data passed to the shared letterhead. Modules provide this so
    /// the active layout can compose company + document identity.
    /// Extra <see cref="Meta"/> rows are placed by layouts that have
    /// room (split console, workshop KPI, editorial caption row).
    /// </summary>
    public class LetterheadInfo
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLocation { get; set; } = string.Empty;
        public string CompanyTrn { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string? SubtitleLine { get; set; }

        public List<(string Label, string Value)> Meta { get; } = new();
    }
}
