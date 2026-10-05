// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/PdfSection.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Statement Generator Redesign
//
// PURPOSE:
//   Immutable model for one row in the Merged PDF Report browser
//   (Section 6 of the redesigned Statement Generator).
//   Each entry represents a section/chunk in the final merged PDF:
//   statement cover page, financial summary, invoice PDFs,
//   DN attachments, etc.
//
// DESIGN:
//   Init-only properties — built once from WorkflowResult after
//   PDF generation completes. Never modified after construction.
//   All computed properties are pure and side-effect-free.
//
//   StatusLabel/StatusBrushKey drive a small colored badge on the
//   right side of each row (e.g. "PLACEHOLDER", "CONVERTED").
//   HasStatus controls badge visibility via BoolToVisibilityConverter.
//
// CONSUMERS:
//   • StatementGeneratorViewModel.MergedPdfSections
//   • StatementGeneratorViewModel.BalancePdfSections
//   • StatementGeneratorViewModel.AlertsPdfSections
//   • StatementGeneratorView.xaml Section 6 tab content ItemsControls
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>
    /// One section/chunk in the merged PDF report browser.
    /// Immutable — built from WorkflowResult post-generation.
    /// </summary>
    public sealed class PdfSection
    {
        // ── Identity ──────────────────────────────────────────────────────

        /// <summary>
        /// Icon displayed in the section list.
        /// Example: "📄" for statement pages, "🧾" for invoice PDFs.
        /// </summary>
        public string Icon { get; init; } = "📄";

        /// <summary>
        /// Theme brush key for the icon badge background (resolved via
        /// BrushKeyConverter). Replaces the old hardcoded IconColorHex so the
        /// tiles follow Light/Dark + accent presets.
        /// </summary>
        public string IconBrushKey { get; init; } = "TintIndigoBrush";

        /// <summary>
        /// Section title displayed in the browser list.
        /// Example: "Statement Cover Page", "TAX-INV-2024-001.pdf"
        /// </summary>
        public string Title { get; init; } = "";

        /// <summary>
        /// Page count info displayed below the title.
        /// Example: "1 page", "3 pages", "2 pages"
        /// </summary>
        public string PageInfo { get; init; } = "";

        /// <summary>
        /// Status badge — drives visual badge on the right side.
        /// Normal = no badge. Others show colored label.
        /// </summary>
        public PdfSectionStatus Status { get; init; } = PdfSectionStatus.Normal;

        // ── Computed display properties ───────────────────────────────────

        /// <summary>
        /// Badge label text. Empty for Normal status (badge hidden).
        /// </summary>
        public string StatusLabel => Status switch
        {
            PdfSectionStatus.Placeholder => "PLACEHOLDER",
            PdfSectionStatus.Converted => "CONVERTED",
            PdfSectionStatus.Warning => "WARNING",
            _ => ""
        };

        /// <summary>
        /// Badge background — theme tint brush key (resolved via BrushKeyConverter).
        /// Neutral/semantic tints; amber is avoided so badges never look yellow.
        /// </summary>
        public string StatusBrushKey => Status switch
        {
            PdfSectionStatus.Placeholder => "TintGrayBrush",
            PdfSectionStatus.Converted => "TintBlueBrush",
            PdfSectionStatus.Warning => "TintRedBrush",
            _ => "TintGrayBrush"
        };

        /// <summary>
        /// Badge foreground — semantic brush key (readable on the tint in both
        /// themes; no hardcoded white-on-amber).
        /// </summary>
        public string StatusForegroundBrushKey => Status switch
        {
            PdfSectionStatus.Placeholder => "MutedForegroundBrush",
            PdfSectionStatus.Converted => "AccentBrush",
            PdfSectionStatus.Warning => "DangerBrush",
            _ => "MutedForegroundBrush"
        };

        /// <summary>
        /// True when a status badge should be displayed.
        /// Bound via BoolToVisibilityConverter in XAML.
        /// </summary>
        public bool HasStatus => Status != PdfSectionStatus.Normal;
    }
}