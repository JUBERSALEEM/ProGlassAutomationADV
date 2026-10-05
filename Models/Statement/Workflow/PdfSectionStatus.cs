// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/PdfSectionStatus.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Statement Generator Redesign
//
// PURPOSE:
//   Status badges displayed on individual sections in the
//   Merged PDF Report browser (Section 6 of redesigned generator).
//   Each value maps to a colored badge label shown on the right
//   side of each PDF section row.
//
// CONSUMERS:
//   • PdfSection.cs             — Status property type
//   • StatementGeneratorVM      — BuildPdfSections() builder
//   • StatementGeneratorView    — section list DataTrigger visibility
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>
    /// Status of an individual section within the merged PDF report.
    /// Drives the badge label and color shown in the PDF browser list.
    /// </summary>
    public enum PdfSectionStatus
    {
        /// <summary>Normal section — no badge displayed.</summary>
        Normal = 0,

        /// <summary>
        /// Invoice PDF regeneration failed — placeholder page inserted.
        /// Badge: "PLACEHOLDER" in amber (#B45309).
        /// </summary>
        Placeholder = 1,

        /// <summary>
        /// Image file (PNG/JPEG) was auto-converted to PDF page.
        /// Badge: "CONVERTED" in blue (#3B82F6).
        /// </summary>
        Converted = 2,

        /// <summary>
        /// Section generated with warnings (e.g. partial data).
        /// Badge: "WARNING" in amber (#B45309).
        /// </summary>
        Warning = 3
    }
}