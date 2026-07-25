// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Utilities/GlassTypeDetector.cs                                          ║
// ║                                                                          ║
// ║  CHAT 13 HOTFIX-4 — NEW FILE                                             ║
// ║                                                                          ║
// ║  Detects glass type (SGU / DGU / LAM / DGU+LAM / Unknown) from a         ║
// ║  specification name by pattern matching against known industry markers:  ║
// ║                                                                          ║
// ║    ASP  → spacer present  → DGU family                                   ║
// ║    PVB  → interlayer      → LAM family                                   ║
// ║    Both → DGU+LAM hybrid                                                 ║
// ║    Neither → SGU (monolithic)                                            ║
// ║                                                                          ║
// ║  Used by DataGridViewModel to auto-switch Glass Type dropdown when the   ║
// ║  user picks or types a specification whose glass type differs from the   ║
// ║  current selection.                                                      ║
// ╚══════════════════════════════════════════════════════════════════════════╝

using System;

namespace ProGlassAutomation.Utilities
{
    /// <summary>
    /// Pattern-based detector that infers a specification's glass type
    /// from its human-readable name. Case-insensitive.
    /// </summary>
    public static class GlassTypeDetector
    {
        private const string ASP_MARKER = "ASP";   // Aluminum SPacer (DGU)
        private const string PVB_MARKER = "PVB";   // PolyVinyl Butyral (LAM)

        /// <summary>
        /// Returns "SGU" | "DGU" | "LAM" | "DGU+LAM" | "Unknown"
        /// based on markers found in the spec name.
        ///
        /// Detection rules:
        ///   ASP + PVB → DGU+LAM
        ///   ASP only  → DGU
        ///   PVB only  → LAM
        ///   Neither   → SGU
        ///   Empty     → Unknown
        /// </summary>
        public static string Detect(string specName)
        {
            if (string.IsNullOrWhiteSpace(specName))
                return "Unknown";

            bool hasAsp = specName.IndexOf(ASP_MARKER, StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasPvb = specName.IndexOf(PVB_MARKER, StringComparison.OrdinalIgnoreCase) >= 0;

            if (hasAsp && hasPvb) return "DGU+LAM";
            if (hasAsp) return "DGU";
            if (hasPvb) return "LAM";

            return "SGU";
        }

        /// <summary>
        /// True if the detected glass type differs from the currently
        /// selected glass type. Returns false when either value is empty
        /// or "Unknown" (nothing to compare).
        /// </summary>
        public static bool IsMismatch(string specName, string currentGlassType)
        {
            if (string.IsNullOrWhiteSpace(currentGlassType)) return false;

            var detected = Detect(specName);
            if (detected == "Unknown") return false;

            return !string.Equals(detected, currentGlassType, StringComparison.OrdinalIgnoreCase);
        }
    }
}