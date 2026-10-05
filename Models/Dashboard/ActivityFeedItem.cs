using System;

namespace ProGlassAutomation.Models.Dashboard
{
    /// <summary>
    /// Single row in the Recent Activity Feed on the Overview dashboard.
    /// Merges invoice activity and sheet store history into one unified feed.
    ///
    /// Phase 8.2a: Added TintColor for subtle row background tinting.
    /// Phase 8.2e: TintColor and StatusColor now hold THEME BRUSH KEY names
    ///             instead of hex strings, so rows adapt automatically to
    ///             Light/Dark theme swap via BrushKeyConverter.
    ///             Backward-compat: hex strings still resolve correctly.
    /// </summary>
    public class ActivityFeedItem
    {
        /// <summary>Emoji icon. "📄" for invoices, "📦" for sheet store.</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Primary line. e.g. "INV-2024-001 · Al Noor Glass"</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Secondary line. e.g. "Proforma Invoice · Updated 2 Jan 2025"</summary>
        public string SubTitle { get; set; } = string.Empty;

        /// <summary>Right-side value. e.g. "AED 45,200" or "20 sheets"</summary>
        public string ValueDisplay { get; set; } = string.Empty;

        /// <summary>Status/action label. e.g. "Approved", "Bought", "Used"</summary>
        public string StatusLabel { get; set; } = string.Empty;

        /// <summary>
        /// Theme brush KEY name for the status dot / pill / badge.
        /// e.g. "SuccessBrush", "InfoBrush", "DangerBrush", "NeutralBrush",
        ///      "WarningBrush".
        /// Resolved at render time by BrushKeyConverter.
        /// Backward-compat: also accepts hex strings like "#10B981".
        /// </summary>
        public string StatusColor { get; set; } = "NeutralBrush";

        /// <summary>
        /// Theme brush KEY name for the subtle row background tint.
        /// e.g. "TintGreenBrush", "TintBlueBrush", "TintRedBrush",
        ///      "TintGrayBrush", "TintAmberBrush", "TintPurpleBrush".
        /// Resolved at render time by BrushKeyConverter — swaps with theme.
        /// Backward-compat: also accepts hex strings.
        /// </summary>
        public string TintColor { get; set; } = "TintGrayBrush";

        /// <summary>
        /// Navigation target string matching MainViewModel Navigate switch.
        /// "ProformaInvoice" or "SheetStore"
        /// </summary>
        public string ModuleTag { get; set; } = string.Empty;

        /// <summary>Used for sorting the unified feed newest-first.</summary>
        public DateTime SortDate { get; set; }
    }
}