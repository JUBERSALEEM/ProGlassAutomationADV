using System;

namespace ProGlassAutomation.Agent.Nlu
{
    /// <summary>
    /// Bounded follow-up memory. RAM only. Cleared with the conversation.
    /// Holds the last material the user talked about — never prices or names.
    /// </summary>
    public static class ConversationSlots
    {
        private static readonly object Gate = new();
        public static int? ThicknessMm { get; private set; }
        public static string? Color { get; private set; }
        public static ErpMetric Metric { get; private set; }
        public static string? LastTool { get; private set; }

        public static bool HasSubject
        {
            get
            {
                lock (Gate)
                    return ThicknessMm.HasValue || !string.IsNullOrWhiteSpace(Color);
            }
        }

        public static void Remember(ErpQuery query, string? tool = null)
        {
            if (query == null) return;
            lock (Gate)
            {
                if (query.ThicknessMm.HasValue) ThicknessMm = query.ThicknessMm;
                if (!string.IsNullOrWhiteSpace(query.Color)) Color = query.Color;
                if (query.Metric != ErpMetric.Unknown) Metric = query.Metric;
                if (!string.IsNullOrWhiteSpace(tool)) LastTool = tool;
            }
        }

        public static void ApplyMissing(ErpQuery query)
        {
            if (query == null) return;
            lock (Gate)
            {
                if (!query.ThicknessMm.HasValue && ThicknessMm.HasValue)
                    query.ThicknessMm = ThicknessMm;
                if (string.IsNullOrWhiteSpace(query.Color) && !string.IsNullOrWhiteSpace(Color))
                    query.Color = Color;
                if (query.Metric == ErpMetric.Unknown && Metric != ErpMetric.Unknown)
                    query.Metric = Metric;
            }
        }

        /// <summary>
        /// Fill gaps from the last subject when this turn is a continuation
        /// (follow-up phrase, pronoun, metric-only, or only colour/thickness changed).
        /// A fresh "show stock" / "inventory" does not inherit the last glass.
        /// </summary>
        public static void ApplyIfContinuing(ErpQuery query)
        {
            if (query == null || !HasSubject) return;

            bool colour = !string.IsNullOrWhiteSpace(query.Color);
            bool thick = query.ThicknessMm.HasValue;
            bool partialMaterial = colour ^ thick;
            bool metricOnly = query.Metric is ErpMetric.Purchased
                or ErpMetric.Consumed
                or ErpMetric.Delivered
                or ErpMetric.Wastage
                or ErpMetric.LowStock
                or ErpMetric.LastPurchase
                or ErpMetric.Supplier;

            if (query.IsFollowUp || partialMaterial || metricOnly)
                ApplyMissing(query);
        }

        public static void Clear()
        {
            lock (Gate)
            {
                ThicknessMm = null;
                Color = null;
                Metric = ErpMetric.Unknown;
                LastTool = null;
            }
        }
    }
}
