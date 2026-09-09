// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Sharing/Contacts/PhoneNumbers.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — phone normalization + validation (Stage 1)
//
// Produces wa.me-safe digit strings from the free-text phones the ERP
// stores (ClientContactPhone is operator-typed: "+971 50 123 4567",
// "050-1234567", "00971…", "(+971)…").
//
// RULES (prompt §27 — never mangle a legitimate number)
// ──────────────
//   • Strip formatting (spaces, dashes, brackets, dots) — never digits.
//   • Leading "+" or "00" = explicit international: keep the digits after
//     it VERBATIM. A number that already carries a country code is never
//     rewritten, whatever country it belongs to.
//   • Leading single "0" (trunk) = local number WITHOUT a country code:
//     replace the trunk 0 with defaultCountryCode ("971" unless Settings
//     says otherwise). This is the ONLY rewrite, and it only fires on the
//     trunk pattern — "050…" → "97150…", never "971971…".
//   • Anything else (no prefix at all): if it already looks international
//     (length ≥ 10 and does not start with 0) it is kept verbatim; a short
//     local number gets the default country code prepended.
//   • Valid = 7–15 digits after normalization (E.164 range).
//
// The default country code lives in SharingSettings — the UAE default is
// a setting, not an assumption baked into code.
// ═══════════════════════════════════════════════════════════════════════

using System.Text;

namespace ProGlassAutomation.Services.Sharing.Contacts
{
    public static class PhoneNumbers
    {
        /// <summary>
        /// Normalizes to a wa.me-safe digit string (no +, no spaces), or
        /// "" when no usable number exists. Never throws, never invents
        /// digits beyond the documented trunk rule.
        /// </summary>
        public static string Normalize(
            string? raw, string defaultCountryCode = "971")
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            string cc = (defaultCountryCode ?? "").Trim().TrimStart('+');
            var digitsOnly = new StringBuilder(raw.Length);
            foreach (char c in raw)
                if (char.IsDigit(c)) digitsOnly.Append(c);
            string digits = digitsOnly.ToString();
            if (digits.Length == 0) return "";

            string compact = "";
            foreach (char c in raw)
                if (!char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')' && c != '.')
                    compact += c;

            // Explicit international: "+" or "00" prefix — keep verbatim.
            if (compact.StartsWith("+") || compact.StartsWith("00"))
            {
                string rest = compact.StartsWith("+")
                    ? compact.Substring(1)
                    : compact.Substring(2);
                var intl = new StringBuilder(rest.Length);
                foreach (char c in rest)
                    if (char.IsDigit(c)) intl.Append(c);
                return intl.ToString();
            }

            // Trunk 0: local number — swap trunk for the country code.
            // Guard: never double-prefix an already-correct number.
            if (digits.StartsWith("0"))
            {
                string withoutTrunk = digits.TrimStart('0');
                if (withoutTrunk.Length == 0) return "";
                if (!string.IsNullOrEmpty(cc)
                    && withoutTrunk.StartsWith(cc))
                    return withoutTrunk; // already carried the code
                return string.IsNullOrEmpty(cc)
                    ? withoutTrunk
                    : cc + withoutTrunk;
            }

            // No prefix: long enough to be international → verbatim; short
            // local → prepend the default country code.
            if (digits.Length >= 10 || string.IsNullOrEmpty(cc))
                return digits;
            if (digits.StartsWith(cc))
                return digits;
            return cc + digits;
        }

        /// <summary>E.164-plausible: 7–15 digits.</summary>
        public static bool IsValidNormalized(string? normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized)) return false;
            foreach (char c in normalized)
                if (!char.IsDigit(c)) return false;
            return normalized.Length >= 7 && normalized.Length <= 15;
        }

        /// <summary>
        /// Normalize + validate in one call. Returns the wa.me digits, or
        /// "" when the input cannot produce a callable number.
        /// </summary>
        public static string NormalizeValidated(
            string? raw, string defaultCountryCode = "971")
        {
            string digits = Normalize(raw, defaultCountryCode);
            return IsValidNormalized(digits) ? digits : "";
        }
    }
}
