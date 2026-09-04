// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs
// PROJECT: ProGlassAutomation
// MODULE: Workflow enforcement (Phase 4, audit item 4.2)
//
// Canonical Proforma Invoice status strings — replaces magic literals
// ("Approved", "Rejected", ...). The transition table in
// Services/Workflow/StatusTransitions is the AUTHORITY for which changes
// are legal; these constants only name the states.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.ProformaInvoice.Common
{
    public static class ProformaInvoiceStatus
    {
        public const string Draft = "Draft";
        public const string Sent = "Sent";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All =
            [Draft, Sent, Approved, Rejected, Expired, Cancelled];

        /// <summary>
        /// PHASE 5 — single normalize home. Canonicalizes any case/spacing
        /// variant of a KNOWN status; an UNRECOGNIZED value passes through
        /// as-typed (never silently re-mapped to Draft — an unexpected
        /// status is something to surface, not to hide).
        /// </summary>
        public static string Normalize(string? value)
        {
            string text = value?.Trim() ?? "";
            foreach (var known in All)
                if (string.Equals(known, text, StringComparison.OrdinalIgnoreCase))
                    return known;
            return text;
        }
    }
}
