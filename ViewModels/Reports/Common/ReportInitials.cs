// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/Common/ReportInitials.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — shared avatar-initials helper
//
// One implementation for the 2-letter monogram shown on customer / company /
// salesman / project rows. Previously copy-pasted into each row DTO; extracted
// so the rendering is identical everywhere and there is a single place to fix.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.ViewModels.Reports.Common
{
    public static class ReportInitials
    {
        /// <summary>
        /// Two-letter uppercase monogram from a name. "Al Noor Glass" → "AG",
        /// "Metro" → "ME", blank → "—". Never throws.
        /// </summary>
        public static string From(string? name)
        {
            var n = (name ?? "").Trim();
            if (n.Length == 0) return "—";
            var parts = n.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "—";
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
        }
    }
}
