// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Dashboard/WorkflowStageModel.cs
// PROJECT: ProGlassAutomation
//
// One stage of the order-to-cash pipeline shown on the Overview
// dashboard: Quote → Production → Delivery → Invoice → Payment.
//
// WHY IT IS A SEPARATE MODEL
//   The pipeline is the one dashboard visual that crosses module
//   boundaries — every stage is owned by a different module, but the
//   point of the rail is that they are one flow. Giving it its own small
//   POCO keeps that cross-module shape out of the per-module KPI models
//   (KpiCardModel / KpiTileModel), which describe a single module's
//   metrics and must stay module-local.
//
//   AccentKey holds a theme brush KEY (not a colour), resolved by
//   BrushKeyConverter, so stage colour follows a runtime Light/Dark
//   swap exactly like every other dashboard surface.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Dashboard
{
    public class WorkflowStageModel
    {
        /// <summary>Segoe MDL2 Assets glyph for the stage.</summary>
        public string Glyph { get; set; } = string.Empty;

        /// <summary>Short stage name, e.g. "Production".</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The headline figure for the stage — how much work is sitting
        /// in it right now. Pre-formatted, so the view never formats.
        /// </summary>
        public string ValueDisplay { get; set; } = "0";

        /// <summary>One line of context under the figure.</summary>
        public string Foot { get; set; } = string.Empty;

        /// <summary>Theme brush key for the stage glyph chip.</summary>
        public string AccentKey { get; set; } = "AccentBrush";

        /// <summary>
        /// True when this stage is carrying attention-worthy work (an
        /// overdue job, an overdue invoice). Rendered as a filled glyph
        /// chip rather than a tinted one so the alert reads without
        /// relying on colour alone.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Module key handed to the dashboard's NavigateTo callback when
        /// the stage is clicked, so the rail doubles as navigation into
        /// the module that owns the work.
        /// </summary>
        public string ModuleKey { get; set; } = string.Empty;

        /// <summary>
        /// True for the first stage only. The rail draws a connector into
        /// the left edge of every stage after the first; the first stage
        /// has nothing upstream of it, so its connector is collapsed.
        /// Driving this from the model keeps the ItemsControl template
        /// free of position hacks.
        /// </summary>
        public bool IsFirst { get; set; }
    }
}
