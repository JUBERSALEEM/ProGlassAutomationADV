using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Agent.Nlu
{
    /// <summary>
    /// Small, curated ERP vocabulary. Not a dictionary of English —
    /// only tokens that actually appear in this factory's speech.
    /// </summary>
    public static class ErpLexicon
    {
        private static readonly Dictionary<string, string> ExpandMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["glas"] = "glass",
                ["glss"] = "glass",
                ["stok"] = "stock",
                ["stk"] = "stock",
                ["clr"] = "clear",
                ["gry"] = "grey",
                ["gray"] = "grey",
                ["brnz"] = "bronze",
                ["mm"] = "mm",
                ["sqm"] = "sqm",
                ["purches"] = "purchase",
                ["purchas"] = "purchase",
                ["kharida"] = "purchase",
                ["khareeda"] = "purchase",
                ["kitna"] = "howmuch",
                ["kitni"] = "howmuch",
                ["bacha"] = "left",
                ["bachi"] = "left",
                ["hai"] = "is",
                ["qty"] = "quantity",
                ["pcs"] = "pieces",
                ["pi"] = "proforma",
                ["jo"] = "joborder",
                ["dn"] = "delivery",
                ["ti"] = "invoice",
                ["inv"] = "invoice",
            };

        private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            "glass", "stock", "clear", "grey", "bronze", "frosted", "mirror",
            "purchase", "purchased", "buy", "bought", "left", "available",
            "used", "consumed", "delivered", "delivery", "wastage", "waste",
            "supplier", "customer", "proforma", "joborder", "invoice",
            "statement", "inventory", "sheet", "thickness", "colour", "color",
            "howmuch", "quantity", "pieces", "demo", "record", "video"
        };

        public static IEnumerable<string> Vocabulary
        {
            get
            {
                foreach (var k in Known) yield return k;
                foreach (var v in ExpandMap.Values) yield return v;
            }
        }

        public static string ExpandToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            return ExpandMap.TryGetValue(token, out var mapped) ? mapped : token;
        }

        public static bool IsKnown(string token) =>
            !string.IsNullOrEmpty(token) && Known.Contains(token);

        public static string? ClosestUnique(string token, int maxDistance)
        {
            string? best = null;
            int bestDist = int.MaxValue;
            int ties = 0;

            foreach (var word in Known)
            {
                int d = Levenshtein(token, word);
                if (d > maxDistance) continue;
                if (d < bestDist)
                {
                    best = word;
                    bestDist = d;
                    ties = 1;
                }
                else if (d == bestDist)
                {
                    ties++;
                    best = null;
                }
            }

            return ties == 1 ? best : null;
        }

        public static int Levenshtein(string a, string b)
        {
            if (a == b) return 0;
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            int n = a.Length;
            int m = b.Length;
            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[m];
        }
    }
}
