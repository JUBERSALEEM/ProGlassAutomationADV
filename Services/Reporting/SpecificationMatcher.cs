// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/SpecificationMatcher.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Canonical Specification Intelligence Engine
//
// PURPOSE:
//   Type-aware deterministic specification matching & fingerprinting.
//   Guarantees that distinct physical configurations are NEVER merged:
//     • 6 Clear + 12 ASP + 6 Clear  ≠  6 Clear + 16 ASP + 6 Clear
//     • 1200×800  ≠  1200×900
//     • 6mm Clear Temp + 1.52 PVB + 6mm Clear  ≠  6mm Clear + 0.76 PVB + 6mm Clear
//
// SUPPORTED SPECIFICATION PARAMETERS:
//   • Glass Reference (physical barcode/tag)
//   • Glass Type (SGU / DGU / LAM / DGU+LAM)
//   • Glass Layer Thicknesses (4mm, 5mm, 6mm, 8mm, 10mm, 12mm, 15mm, 19mm)
//   • Colours (Clear, Extra Clear/Low Iron, Euro Grey, Dark Grey, Bronze, Green, Blue)
//   • Treatments (Annealed, Tempered/Toughened, Heat Strengthened)
//   • Coatings (Low-E, Solar Control, Reflective, Double Silver, Sunergy, Solarban)
//   • Interlayer / PVB (Clear PVB, Acoustic PVB, SentryGlas/SGP, 0.38mm, 0.76mm, 1.14mm, 1.52mm)
//   • Air Space / Spacer (6mm, 8mm, 9mm, 10mm, 12mm, 14mm, 15mm, 16mm, 18mm, 20mm, Warm Edge, Aluminum, Argon gas)
//   • Dimensions (W1 × H1, W2 × H2 for shaped panes)
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
            (s ?? "").Trim().ToUpperInvariant();

        /// <summary>
        /// Build a deterministic, type-aware canonical identity key for a glass specification.
        /// </summary>
        public static string BuildCanonicalKey(SpecIdentity identity)
        {
            if (identity == null) return "UNKNOWN|||0x0";

            var type = Norm(identity.GlassType);
            if (string.IsNullOrWhiteSpace(type)) type = DetectType(identity.SpecName);

            var sb = new StringBuilder();

            // 1. Glass Reference
            sb.Append(NormRef(identity.GlassRef));
            sb.Append('|');

            // 2. Glass Build Type
            sb.Append(type.ToUpperInvariant());
            sb.Append('|');

            // 3. Normalized Composition (thicknesses, treatments, coatings, spacer, PVB)
            var specFingerprint = NormalizeSpecComposition(identity.SpecName, type);
            sb.Append(specFingerprint);
            sb.Append('|');

            // 4. Exact Dimensions (never merged across sizes)
            sb.Append(identity.W1).Append('×').Append(identity.H1);
            if (identity.W2 > 0 || identity.H2 > 0)
                sb.Append('/').Append(identity.W2).Append('×').Append(identity.H2);

            // 5. Colours (Outer + Inner stock colors)
            if (!string.IsNullOrWhiteSpace(identity.OuterColor) || !string.IsNullOrWhiteSpace(identity.InnerColor))
            {
                sb.Append('|');
                sb.Append(Norm(identity.OuterColor));
                sb.Append('+');
                sb.Append(Norm(identity.InnerColor));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Build canonical key directly from row attributes.
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
        /// True if two specifications represent the exact same physical glass configuration.
        /// </summary>
        public static bool IsExactMatch(SpecIdentity a, SpecIdentity b)
        {
            if (a == null || b == null) return false;
            return string.Equals(BuildCanonicalKey(a), BuildCanonicalKey(b), StringComparison.Ordinal);
        }

        /// <summary>
        /// Detect glass type from specification name when explicit type is missing.
        /// </summary>
        public static string DetectType(string? specName)
        {
            var s = Norm(specName);
            if (string.IsNullOrWhiteSpace(s)) return "sgu";
            if (s.Contains("dgu+lam") || s.Contains("dgu + lam") || s.Contains("dgu/lam")) return "dgu+lam";
            if (s.Contains("dgu") || s.Contains("asp") || s.Contains("spacer") || s.Contains("air")) return "dgu";
            if (s.Contains("lam") || s.Contains("pvb") || s.Contains("sgp") || s.Contains("sentry")) return "lam";
            return "sgu";
        }

        /// <summary>
        /// Normalize specification composition preserving all critical engineering tokens.
        /// </summary>
        private static string NormalizeSpecComposition(string? specName, string type)
        {
            var raw = Norm(specName);
            if (string.IsNullOrWhiteSpace(raw)) return "";

            // Normalize spaces and operators
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            raw = raw.Replace(" + ", "+").Replace(" +", "+").Replace("+ ", "+");
            raw = raw.Replace(" / ", "/").Replace(" /", "/").Replace("/ ", "/");

            // Normalize unit notations
            raw = Regex.Replace(raw, @"(\d+)\s*mm", "$1mm");
            raw = Regex.Replace(raw, @"(\d+)\s*m\s*m\s*a\s*s\s*p", "$1asp");
            raw = Regex.Replace(raw, @"(\d+)\s*a\s*s\s*p", "$1asp");
            raw = Regex.Replace(raw, @"(\d+)\s*a\b", "$1asp"); // 12A -> 12asp

            // Normalize PVB notations
            raw = Regex.Replace(raw, @"(\d+\.\d+)\s*pvb", "$1pvb");
            raw = Regex.Replace(raw, @"(\d+\.\d+)\s*sgp", "$1sgp");

            // Normalize Treatments
            raw = raw.Replace("tempered", "temp").Replace("toughened", "temp");
            raw = raw.Replace("heat strengthened", "hs").Replace("heat-strengthened", "hs");
            raw = raw.Replace("annealed", "ann");

            // Strip redundant filler spaces
            raw = raw.Replace(" ", "");

            return raw;
        }

        /// <summary>
        /// Formats a human-readable title for UI grouping.
        /// </summary>
        public static string DisplayKey(SpecIdentity identity)
        {
            var name = (identity.SpecName ?? "").Trim();
            if (name.Length > 60) name = name.Substring(0, 57) + "…";
            return $"{name} · {identity.Dimensions}";
        }
    }
}
