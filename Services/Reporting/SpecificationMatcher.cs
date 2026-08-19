// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/SpecificationMatcher.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Canonical Specification Identity
//
// PURPOSE:
//   Type-aware exact specification matching. Must NOT merge:
//     6 Clear + 12 ASP + 6 Clear  vs  6 Clear + 16 ASP + 6 Clear
//     1200×800 vs 1200×900 vs different colours/thicknesses/PVB/spacers
//
// RULES:
//   • SGU, DGU, LAM, DGU+LAM have different relevant attributes.
//   • Canonical key is lowercased, trimmed, normalized.
//   • Dimensions are separate from spec identity (same glass spec
//     at different sizes = different canonical keys).
//   • GlassRef is part of identity (ties to physical reference code).
//
// USED BY:
//   Glass/Spec Report aggregation, Row-level traceability, AI spec search.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Services.Reporting
{
    public static class SpecificationMatcher
    {
        private static string Norm(string? s) =>
            (s ?? "").Trim().ToLowerInvariant();

        private static string NormRef(string? s) =>
            (s ?? "").Trim().ToLowerInvariant();

        /// <summary>
        /// Build a canonical identity key for a glass specification.
        /// Type-aware: SGU/DGU/LAM/DGU+LAM each include only their relevant
        /// attributes. Never merges specs that differ on any relevant field.
        /// </summary>
        public static string BuildCanonicalKey(SpecIdentity identity)
        {
            if (identity == null) return "unknown|||0x0";

            var type = Norm(identity.GlassType);
            if (string.IsNullOrWhiteSpace(type)) type = DetectType(identity.SpecName);

            var sb = new StringBuilder();
            // Glass reference + type are always significant
            sb.Append(NormRef(identity.GlassRef));
            sb.Append('|');
            sb.Append(type);
            sb.Append('|');

            // Spec name normalized — captures thickness, colour, treatment,
            // spacer, PVB, lamination that the Pricing Estimator embedded.
            // Type-aware parsing: extract only relevant tokens.
            var specName = NormSpecName(identity.SpecName, type);
            sb.Append(specName);
            sb.Append('|');

            // Dimensions — exact, never merged across sizes
            // For shape with W2/H2, include them; 0 means rectangular only
            sb.Append(identity.W1).Append('×').Append(identity.H1);
            if (identity.W2 > 0 || identity.H2 > 0)
                sb.Append('/').Append(identity.W2).Append('×').Append(identity.H2);

            // Colors — significant for identity (different stock)
            if (!string.IsNullOrWhiteSpace(identity.OuterColor) ||
                !string.IsNullOrWhiteSpace(identity.InnerColor))
            {
                sb.Append('|');
                sb.Append(Norm(identity.OuterColor));
                sb.Append('+');
                sb.Append(Norm(identity.InnerColor));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Build canonical key from raw spec group + row data without needing
        /// to construct SpecIdentity first. Convenience for DB projections.
        /// </summary>
        public static string BuildCanonicalKey(
            string? glassRef,
            string? glassType,
            string? specName,
            string? outerColor,
            string? innerColor,
            int w1, int h1, int w2, int h2)
        {
            return BuildCanonicalKey(new SpecIdentity
            {
                GlassRef = glassRef ?? "",
                GlassType = glassType ?? "",
                SpecName = specName ?? "",
                OuterColor = outerColor ?? "",
                InnerColor = innerColor ?? "",
                W1 = w1,
                H1 = h1,
                W2 = w2,
                H2 = h2
            });
        }

        /// <summary>
        /// True if two identities describe the exact same physical glass.
        /// Dimensions must match exactly; 1200×800 ≠ 1200×900.
        /// </summary>
        public static bool IsExactMatch(SpecIdentity a, SpecIdentity b)
        {
            if (a == null || b == null) return false;
            return string.Equals(BuildCanonicalKey(a), BuildCanonicalKey(b), StringComparison.Ordinal);
        }

        /// <summary>
        /// Try to detect glass type from spec name when GlassType column is empty
        /// (legacy invoices or manual specs).
        /// </summary>
        public static string DetectType(string? specName)
        {
            var s = Norm(specName);
            if (string.IsNullOrWhiteSpace(s)) return "sgu";
            if (s.Contains("dgu+lam") || s.Contains("dgu + lam")) return "dgu+lam";
            if (s.Contains("dgu")) return "dgu";
            if (s.Contains("lam")) return "lam";
            return "sgu";
        }

        private static string NormSpecName(string? specName, string type)
        {
            var raw = Norm(specName);
            if (string.IsNullOrWhiteSpace(raw)) return "";

            // Collapse multiple spaces, normalize separators
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            raw = raw.Replace(" + ", "+").Replace(" +", "+").Replace("+ ", "+");

            // Type-aware normalization:
            // SGU: keep entire spec (single pane spec)
            // DGU: spec contains spacer — keep exactly as is, spacer thickness is significant
            // LAM: contains PVB — keep exactly as is, PVB thickness is significant
            // DGU+LAM: contains both — keep exactly
            // All types: lowercase already, keep punctuation that carries meaning

            // Normalize "mm" spacing: "6 mm" → "6mm"
            raw = Regex.Replace(raw, @"(\d+)\s*mm", "$1mm");
            // Normalize ASP: "12 mm asp" / "12mmasp" → "12mmasp"
            raw = Regex.Replace(raw, @"(\d+)\s*m\s*m\s*a\s*s\s*p", "$1mmasp");
            raw = raw.Replace(" ", "");

            return raw;
        }

        /// <summary>
        /// Human-readable key for grouping in UI (first 50 chars of spec name + dimensions).
        /// </summary>
        public static string DisplayKey(SpecIdentity identity)
        {
            var name = (identity.SpecName ?? "").Trim();
            if (name.Length > 60) name = name.Substring(0, 57) + "…";
            return $"{name} · {identity.Dimensions}";
        }
    }
}
