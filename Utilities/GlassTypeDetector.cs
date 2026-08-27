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
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.Utilities
{
    /// <summary>
    /// Pattern-based detector that infers a specification's glass type
    /// from its human-readable name. Case-insensitive.
    /// </summary>
    public static class GlassTypeDetector
    {
        /// <summary>
        /// Returns "SGU" | "DGU" | "LAM" | "DGU+LAM" | "Unknown"
        /// based on the same grammar Step 9 already trusts.
        ///
        /// Detection rules:
        ///   spacer + interlayer → DGU+LAM
        ///   spacer only         → DGU
        ///   interlayer only     → LAM
        ///   glass only          → SGU
        ///   empty / unparsable  → Unknown
        ///
        /// Using the shared parser matters: the older implementation only
        /// looked for "PVB", so SGP/EVA hybrids were mis-detected as DGU and
        /// the Step 5 glass-type dropdown drifted away from the actual spec.
        /// </summary>
        public static string Detect(string specName)
        {
            if (string.IsNullOrWhiteSpace(specName))
                return "Unknown";

            bool hasSpacer = PnLSpecParser.Spacer(specName).Success;
            bool hasInterlayer = PnLSpecParser.Interlayer(specName).Success;
            bool hasGlass = PnLSpecParser.GlassMatches(specName).Count > 0;

            if (hasSpacer && hasInterlayer) return "DGU+LAM";
            if (hasSpacer) return "DGU";
            if (hasInterlayer) return "LAM";
            if (hasGlass) return "SGU";

            return "Unknown";
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