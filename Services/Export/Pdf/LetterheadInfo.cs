namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// Data passed to LetterheadRenderer. Modules provide this so the
    /// shared letterhead block can render the company header.
    ///
    /// If a module returns null Letterhead from IPdfLayout, the engine
    /// falls back to defaults from AppConfig (future — Phase 9.5b).
    /// </summary>
    public class LetterheadInfo
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLocation { get; set; } = string.Empty;
        public string CompanyTrn { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string? SubtitleLine { get; set; }
    }
}