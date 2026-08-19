// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/DataBoundary/AgentDataBoundary.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE T)
//
// THE DATA BOUNDARY — one file that answers one question:
//     "May this piece of ERP data leave the machine?"
//
// WHY IT IS ITS OWN MODULE
//   The master prompt names DataBoundary as a distinct security component
//   and requires it to be FAIL-CLOSED. The behaviour previously lived
//   inside AgentSecurityService.AllowExternalAsync mixed with sanitizing
//   and redaction. Splitting it out means a reviewer can point at exactly
//   one file per guarantee instead of reading a 250-line service.
//
// FAIL-CLOSED BY CONSTRUCTION
//   Every method returns "blocked" on any doubt: unknown classification,
//   null input, an exception while deciding — all deny. There is no code
//   path that returns Allowed as a fallback.
//
// WHAT THIS DELIBERATELY DOES NOT CLAIM
//   This is a policy gate, not an exfiltration guarantee. It governs the
//   agent's own egress calls. It cannot stop a compromised process, and
//   nothing here should be described as making the app "secure".
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Security.Redaction;
using ProGlassAutomation.Services.Logging;
using System;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Security.DataBoundary
{
    /// <summary>How sensitive a piece of data is.</summary>
    public enum DataClass
    {
        /// <summary>Unknown provenance. Treated as the most sensitive.</summary>
        Unclassified = 0,

        /// <summary>Product facts: glass types, VAT rate, thicknesses.</summary>
        PublicKnowledge = 1,

        /// <summary>Customer names, prices, invoice numbers, stock levels.</summary>
        BusinessData = 2,

        /// <summary>API keys, passwords, tokens. Never leaves, ever.</summary>
        Secret = 3
    }

    /// <summary>Outcome of a boundary check.</summary>
    public sealed class BoundaryVerdict
    {
        public bool Allowed { get; init; }

        /// <summary>Truthful, user-facing reason. Never empty when denied.</summary>
        public string Reason { get; init; } = "";

        public static BoundaryVerdict Allow() => new() { Allowed = true };

        public static BoundaryVerdict Block(string reason) =>
            new()
            {
                Allowed = false,
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? "Blocked by the data boundary."
                    : reason
            };
    }

    /// <summary>
    /// Decides whether data of a given class may cross to an external
    /// service. Pure policy — it performs no I/O and holds no state, so
    /// it is trivially testable and cannot be tricked by ordering.
    /// </summary>
    public static class AgentDataBoundary
    {
        /// <summary>
        /// The single rule. Only PublicKnowledge may ever leave, and only
        /// when the caller has already established consent.
        ///
        /// BusinessData is blocked even WITH consent: consenting to use a
        /// cloud model is not the same as consenting to upload the
        /// customer list, and conflating the two is how ERP data leaks.
        /// </summary>
        public static BoundaryVerdict MayLeaveDevice(
            DataClass classification, bool consentGiven)
        {
            try
            {
                switch (classification)
                {
                    case DataClass.Secret:
                        return BoundaryVerdict.Block(
                            "That contains a credential, so it never leaves this " +
                            "computer under any setting.");

                    case DataClass.BusinessData:
                        return BoundaryVerdict.Block(
                            "That is business data — customer, pricing or stock " +
                            "information — so I keep it on this computer. Only " +
                            "general product knowledge can be sent to an online " +
                            "provider.");

                    case DataClass.PublicKnowledge:
                        return consentGiven
                            ? BoundaryVerdict.Allow()
                            : BoundaryVerdict.Block(
                                "Cloud use has not been consented to, so I did not " +
                                "send anything.");

                    case DataClass.Unclassified:
                    default:
                        // Fail closed: unknown means sensitive.
                        return BoundaryVerdict.Block(
                            "I could not establish what kind of data that is, so I " +
                            "did not send it.");
                }
            }
            catch (Exception ex)
            {
                // A boundary that throws must still refuse.
                AppLogService.Debug($"[AgentDataBoundary] {ex.Message}");
                return BoundaryVerdict.Block(
                    "The data boundary could not complete its check, so I did " +
                    "not send anything.");
            }
        }

        /// <summary>
        /// Conservative classifier for free text. Errs upward: anything
        /// that smells like a credential is Secret, anything containing
        /// business markers is BusinessData, and empty/unknown text is
        /// Unclassified rather than Public.
        /// </summary>
        public static DataClass Classify(string? text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return DataClass.Unclassified;

                if (AgentRedaction.ContainsSecret(text)) return DataClass.Secret;

                // Markers that only appear alongside real customer or
                // financial records.
                string lower = text.ToLowerInvariant();

                string[] businessMarkers =
                {
                    "pi-", "jo-", "dn-", "ti-", "invoice no", "customer",
                    "aed ", "purchase price", "selling price", "stock",
                    "profit", "margin", "salesman", "project"
                };

                foreach (var marker in businessMarkers)
                    if (lower.Contains(marker)) return DataClass.BusinessData;

                return DataClass.PublicKnowledge;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentDataBoundary] classify: {ex.Message}");
                return DataClass.Unclassified;   // fail closed
            }
        }
    }
}
