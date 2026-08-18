using System;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Agent.Nlu
{
    /// <summary>
    /// Pulls thickness, colour, metric and demo intent out of prepared text.
    /// Ambiguous colours stay unset so the tool can ask.
    /// </summary>
    public static class ErpQueryParser
    {
        private static readonly Regex Thickness = new(
            @"\b(\d{1,2})\s*(?:mm)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] FollowUps =
        {
            "what about", "and the", "how about", "same for", "uska", "uski"
        };

        public static ErpQuery Parse(string? raw)
        {
            string prepared = QueryNormalizer.Prepare(raw);
            var q = new ErpQuery { Raw = raw ?? "", Prepared = prepared };

            if (prepared.Length == 0) return q;

            foreach (var f in FollowUps)
            {
                if (prepared.Contains(f))
                {
                    q.IsFollowUp = true;
                    break;
                }
            }

            var tm = Thickness.Match(prepared);
            if (tm.Success && int.TryParse(tm.Groups[1].Value, out int mm)
                && mm is >= 3 and <= 25)
                q.ThicknessMm = mm;

            q.Color = DetectColor(prepared);
            q.Metric = DetectMetric(prepared);
            q.WantsDemo = DetectDemo(prepared, out var module);
            q.DemoModule = module;

            if (q.IsFollowUp)
                ConversationSlots.ApplyMissing(q);

            return q;
        }

        private static string? DetectColor(string text)
        {
            if (ContainsAny(text, "clear", "clr")) return "Clear";
            if (ContainsAny(text, "grey", "gray", "gry")) return "Grey";
            if (ContainsAny(text, "bronze", "brnz")) return "Bronze";
            if (ContainsAny(text, "frosted")) return "Frosted";
            if (ContainsAny(text, "mirror", "silver")) return "Silver";
            if (ContainsAny(text, "green")) return "Green";
            if (ContainsAny(text, "blue")) return "Blue";
            return null;
        }

        private static ErpMetric DetectMetric(string text)
        {
            if (ContainsAny(text, "wastage", "waste", "breakage")) return ErpMetric.Wastage;
            if (ContainsAny(text, "deliver", "delivered", "delivery")) return ErpMetric.Delivered;
            if (ContainsAny(text, "purchase", "purchased", "buy", "bought", "kharida"))
                return ErpMetric.Purchased;
            if (ContainsAny(text, "used", "consumed", "consume")) return ErpMetric.Consumed;
            if (ContainsAny(text, "supplier", "who supplied", "which supplier"))
                return ErpMetric.Supplier;
            if (ContainsAny(text, "last purchase", "when did we buy"))
                return ErpMetric.LastPurchase;
            if (ContainsAny(text, "low stock", "running low", "almost finished"))
                return ErpMetric.LowStock;
            if (ContainsAny(text, "stock", "left", "available", "bacha", "howmuch"))
                return ErpMetric.Available;
            return ErpMetric.Unknown;
        }

        private static bool DetectDemo(string text, out string? module)
        {
            module = null;
            bool demo = ContainsAny(text,
                "demo", "record this", "make a video", "create demo",
                "workflow demo", "product demo", "screen recording");
            if (!demo) return false;

            if (ContainsAny(text, "proforma", "quotation")) module = "ProformaInvoice";
            else if (ContainsAny(text, "job order", "joborder", "production order")) module = "JobOrder";
            else if (ContainsAny(text, "delivery")) module = "DeliveryNote";
            else if (ContainsAny(text, "tax invoice", "invoice")) module = "TaxInvoice";
            else if (ContainsAny(text, "stock", "inventory", "sheet")) module = "SheetStore";
            else if (ContainsAny(text, "statement", "report")) module = "SalesReport";
            else if (ContainsAny(text, "dashboard", "overview")) module = "Dashboard";
            else if (ContainsAny(text, "all module", "every module", "complete erp"))
                module = "All";
            return true;
        }

        private static bool ContainsAny(string text, params string[] keys)
        {
            foreach (var k in keys)
                if (text.Contains(k, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
