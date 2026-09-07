using ProGlassAutomation.Models.MasterData;
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

        // ── Company branding (logo + placement) ──────────────────────────
        // Resolved from the saved company record by each module. When both
        // are present LogoPath (a live file) is preferred over LogoBase64.

        /// <summary>File path to the company logo image (empty = none).</summary>
        public string LogoPath { get; set; } = string.Empty;

        /// <summary>Base64-encoded logo image (empty = none).</summary>
        public string LogoBase64 { get; set; } = string.Empty;

        /// <summary>Logo layout: None (text), LeftPortrait or TopPanel.</summary>
        public CompanyLogoLayout LogoLayout { get; set; } = CompanyLogoLayout.None;

        /// <summary>Printed logo width in millimetres (clamped 8–60).</summary>
        public double LogoWidthMm { get; set; } = 22;

        /// <summary>Printed logo height in millimetres (clamped 8–60).</summary>
        public double LogoHeightMm { get; set; } = 26;

        public List<(string Label, string Value)> Meta { get; } = new();
    }
}
