using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Agent.Nlu
{
    /// <summary>
    /// Layer 1 of NLU: collapse noise so later matching sees one shape.
    /// Does not invent meaning — it only cleans text.
    /// </summary>
    public static class QueryNormalizer
    {
        private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string s = text.Trim().ToLowerInvariant();
            s = s.Replace('’', '\'');
            s = s.Replace('“', '"').Replace('”', '"');
            s = s.Replace('_', ' ');
            s = MultiSpace.Replace(s, " ");
            return s.Trim();
        }

        /// <summary>
        /// Expands known abbreviations and Hinglish tokens using the lexicon.
        /// Unknown words are left as-is so we never invent a term.
        /// </summary>
        public static string Expand(string normalized)
        {
            if (string.IsNullOrEmpty(normalized)) return "";

            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(normalized.Length + 16);
            foreach (var part in parts)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ErpLexicon.ExpandToken(part));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Soft typo fix: only rewrite a token when it is within 1 edit of
        /// exactly one lexicon word. Ambiguous or far tokens stay raw.
        /// </summary>
        public static string SoftCorrect(string expanded)
        {
            if (string.IsNullOrEmpty(expanded)) return "";

            var parts = expanded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i];
                if (token.Length < 3) continue;
                if (ErpLexicon.IsKnown(token)) continue;

                string? hit = ErpLexicon.ClosestUnique(token, maxDistance: 1);
                if (hit != null) parts[i] = hit;
            }
            return string.Join(' ', parts);
        }

        public static string Prepare(string? text) =>
            SoftCorrect(Expand(Normalize(text)));
    }
}
