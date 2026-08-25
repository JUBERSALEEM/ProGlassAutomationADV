// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/LicenseGate.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — feature gating helper
//
// Maps a navigation key (the same string the rail sends
// to MainViewModel.Navigate) to a license feature that
// must be granted for navigation to proceed. The single
// public entry point is LicenseGate.CanNavigate which
// returns true or an "Upgrade required" message that the
// caller (the nav switch) surfaces in a MessageBox.
//
// HOW THE MAP IS READ
//   • Empty map entry  →  no gating. The module is open
//     to every edition.
//   • "Cutting.*"      →  the licence must grant the
//     Cutting feature. The wildcards / prefix matching
//     in LicenseService.FeatureAllowed apply.
//   • "*" entry        →  EVERY feature listed in the
//     licence is required. Useful for modules that
//     should only be in Enterprise.
//
// WHY THIS IS A SEPARATE CLASS
//   • The MainViewModel switch is already long; adding
//     a gate per case is a maintenance hazard.
//   • Feature flags change as the product evolves; a
//     separate file is easier to grep, easier to audit,
//     and easier to unit test.
//   • The Gate's "can navigate?" decision is a pure
//     function of (key, current license state). It
//     has no UI dependencies, so it is trivially
//     testable.
//
// OUT OF SCOPE
//   • The Gate does not block the user from OPENING a
//     view. It blocks the NAVIGATION. If the module is
//     already loaded in the ViewModel, the user can
//     still scroll to it. Future work: per-command
//     gating in the per-module ViewModels.
//   • The Gate does not know about editions. It only
//     knows about features. A Basic-edition user who
//     lacks "Optimization.*" is told "this feature
//     requires Factory" or similar; the exact text
//     lives in LicenseUiText so the call site does not
//     need a switch on edition.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// Result of a <see cref="LicenseGate.CanNavigate"/>
    /// check. <see cref="Allowed"/> is true when the
    /// user has the required feature(s); <see cref="Reason"/>
    /// is a user-friendly explanation when it is not.
    /// </summary>
    public readonly record struct LicenseGateResult(
        bool Allowed, string Reason, string? RequiredEdition)
    {
        public static LicenseGateResult Allow() =>
            new(true, "", null);

        public static LicenseGateResult Deny(string reason, string? requiredEdition) =>
            new(false, reason, requiredEdition);
    }

    /// <summary>
    /// Module-key → required-feature map. The keys are the
    /// same string constants the navigation rail sends to
    /// <c>MainViewModel.Navigate</c>.
    /// </summary>
    public static class LicenseGate
    {
        // The map. Add a new entry when a new module
        // ships. Empty-string ("") means "no gate".
        // The single value in the list is the feature
        // pattern; multiple values are AND-ed (the user
        // must have ALL of them).
        private static readonly Dictionary<string, string[]> Map = new(
            StringComparer.Ordinal)
        {
            // Production-stage modules — Factory and above.
            ["Cutting"] = new[] { "Cutting.*" },
            ["HolesCutouts"] = new[] { "Cutting.*" },
            ["Polish"] = new[] { "Cutting.*" },
            ["EdgeDeletion"] = new[] { "Cutting.*" },
            ["Mitring"] = new[] { "Cutting.*" },
            ["Tempering"] = new[] { "Cutting.*" },
            ["Pairing"] = new[] { "Cutting.*" },
            ["DoublingLamination"] = new[] { "Cutting.*" },

            // Cutting-optimisation engine — Factory and above.
            ["Optimization"] = new[] { "Cutting.*" },
            ["JobProfitability"] = new[] { "Reports.*" },

            // The remaining modules (Dashboard, Inquiry,
            // SheetStore, ProformaInvoice, JobOrder,
            // DeliveryNote, TaxInvoice, StatementGenerator,
            // SalesReport, About, Settings, ...) are
            // available in every edition. No gate.
        };

        /// <summary>
        /// True if the current license permits opening
        /// the module at <paramref name="moduleKey"/>.
        /// Returns an explanatory reason when it does
        /// not, so the caller can show a MessageBox
        /// without inventing copy.
        /// </summary>
        public static LicenseGateResult CanNavigate(string moduleKey)
        {
            // Unknown module keys are NOT gated. The
            // navigation switch will simply not find a
            // match and the current view stays put; that
            // is its own failure mode, not a licensing
            // one.
            if (string.IsNullOrEmpty(moduleKey)) return LicenseGateResult.Allow();

            // 2026-08-25: an ACTIVE trial behaves exactly like premium —
            // nothing is gated. The trial payload carries no feature list
            // (FeatureAllowed would deny it), which used to lock Cutting /
            // Optimization / Job Profitability behind "Upgrade required"
            // even during the free trial. Once the trial ends the status
            // becomes TrialExpired/ReadOnly and the normal restrictions
            // (read-only mode + feature denials) apply automatically.
            if (LicenseService.Current.Status == LicenseStatus.Trial)
                return LicenseGateResult.Allow();

            if (!Map.TryGetValue(moduleKey, out var required)) return LicenseGateResult.Allow();
            if (required.Length == 0) return LicenseGateResult.Allow();

            // Empty list entry means no gate (see above).
            foreach (var p in required)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (!LicenseService.FeatureAllowed(p))
                {
                    return LicenseGateResult.Deny(
                        reason:
                            "This module is part of a higher " +
                            "edition of ProGlass Automation. " +
                            "Your current license does not grant " +
                            "the '" + p + "' feature set. " +
                            "Contact your vendor to upgrade, or " +
                            "ask them to re-issue a license with " +
                            "this feature enabled.",
                        requiredEdition: SuggestEdition(p));
                }
            }

            return LicenseGateResult.Allow();
        }

        /// <summary>
        /// Maps a feature pattern to the minimum
        /// edition that includes it. The mapping is
        /// loose by design — a Basic-edition user told
        /// "this needs Business" is directionally
        /// correct even if the actual upgrade is
        /// Business → Factory.
        /// </summary>
        private static string? SuggestEdition(string featurePattern)
        {
            if (featurePattern.StartsWith("Cutting.", StringComparison.Ordinal))
                return "Factory";
            if (featurePattern.StartsWith("Reports.", StringComparison.Ordinal))
                return "Business";
            if (featurePattern.StartsWith("AI.", StringComparison.Ordinal))
                return "Business";
            return null;
        }
    }
}
