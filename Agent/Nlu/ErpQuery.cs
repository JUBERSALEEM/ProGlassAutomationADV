namespace ProGlassAutomation.Agent.Nlu
{
    public enum ErpMetric
    {
        Unknown = 0,
        Available,
        Purchased,
        Consumed,
        Delivered,
        Wastage,
        LowStock,
        LastPurchase,
        Supplier
    }

    /// <summary>Structured reading of one user utterance. Never a guess of numbers.</summary>
    public sealed class ErpQuery
    {
        public string Raw { get; init; } = "";
        public string Prepared { get; init; } = "";
        public int? ThicknessMm { get; set; }
        public string? Color { get; set; }
        public ErpMetric Metric { get; set; } = ErpMetric.Unknown;
        public bool IsFollowUp { get; set; }
        public bool WantsDemo { get; set; }
        public string? DemoModule { get; set; }

        public bool HasMaterialHint =>
            ThicknessMm.HasValue || !string.IsNullOrWhiteSpace(Color);

        public string FilterText()
        {
            var bits = new System.Collections.Generic.List<string>();
            if (ThicknessMm.HasValue) bits.Add(ThicknessMm.Value + "mm");
            if (!string.IsNullOrWhiteSpace(Color)) bits.Add(Color);
            return string.Join(" ", bits);
        }
    }
}
