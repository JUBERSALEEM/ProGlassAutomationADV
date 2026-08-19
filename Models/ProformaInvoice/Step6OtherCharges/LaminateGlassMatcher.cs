using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Detects whether a Step 5 spec is a same-glass laminate / double
    /// (Clear + Clear) versus mixed (HD Grey + Clear).
    ///
    /// Same-glass LAM / DGU / DGU+LAM is charged on the dedicated
    /// POLISH_MATCHED_LAM row at 2× because both panes get polished
    /// even when the grid only has W1/H1 filled.
    /// Mixed glass stays on the regular Polish row (one charge per pane).
    /// </summary>
    public static class LaminateGlassMatcher
    {
        private static readonly string[] InterlayerTokens =
        {
            "PVB", "EVA", "SGP", "ASP", "AIR", "SPACER", "ARGON", "KRYPTON"
        };

        public static bool IsDoublePaneType(string? glassType)
        {
            if (string.IsNullOrWhiteSpace(glassType)) return false;
            return glassType.Equals("LAM", StringComparison.OrdinalIgnoreCase)
                || glassType.Equals("DGU", StringComparison.OrdinalIgnoreCase)
                || glassType.Equals("DGU+LAM", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when the spec name has two (or more) glass layers that
        /// match after stripping PVB/ASP interlayers.
        /// Example match: "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass"
        /// Example miss:  "6mm HD Grey FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass"
        /// </summary>
        public static bool IsMatchedSameGlass(string? specName, string? glassType)
        {
            if (!IsDoublePaneType(glassType)) return false;
            var glasses = ExtractGlassLayers(specName);
            if (glasses.Count < 2) return false;

            string first = NormalizeGlass(glasses[0]);
            string last = NormalizeGlass(glasses[glasses.Count - 1]);
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last))
                return false;

            return string.Equals(first, last, StringComparison.Ordinal);
        }

        public static List<string> ExtractGlassLayers(string? specName)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(specName)) return result;

            foreach (var raw in specName.Split('+'))
            {
                var layer = raw.Trim();
                if (layer.Length == 0) continue;
                if (IsInterlayer(layer)) continue;
                result.Add(layer);
            }

            return result;
        }

        private static bool IsInterlayer(string layer)
        {
            foreach (var token in InterlayerTokens)
            {
                if (layer.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string NormalizeGlass(string layer)
        {
            string s = layer.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"\bglass\b", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }
    }
}
