namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// Canonical module keys for per-module wallpapers (§9).
    /// One constant per module — no free strings.
    /// </summary>
    public static class WallpaperModules
    {
        public const string Dashboard = "Dashboard";
        public const string SheetStore = "SheetStore";
        public const string ProformaInvoice = "ProformaInvoice";
        public const string JobOrder = "JobOrder";
        public const string DeliveryNote = "DeliveryNote";
        public const string TaxInvoice = "TaxInvoice";
        public const string SalesReport = "SalesReport";
        public const string StatementGenerator = "StatementGenerator";
        public const string Optimization = "Optimization";
        public const string Logs = "Logs";
        public const string ActivitySessions = "ActivitySessions";
        public const string SystemCenter = "SystemCenter";
        public const string Settings = "Settings";
        public const string About = "About";

        /// <summary>Display order used by the Settings UI.</summary>
        public static readonly (string Key, string Label)[] All =
        {
            (Dashboard, "Dashboard"),
            (SheetStore, "Sheet Store"),
            (ProformaInvoice, "Proforma Invoice"),
            (JobOrder, "Job Order"),
            (DeliveryNote, "Delivery Note"),
            (TaxInvoice, "Tax Invoice"),
            (SalesReport, "Sales Report"),
            (StatementGenerator, "Statement Generator"),
            (Optimization, "Optimization"),
            (Logs, "Logs & Activity"),
            (ActivitySessions, "Activity Sessions"),
            (SystemCenter, "System Center"),
            (Settings, "Settings"),
            (About, "About")
        };
    }
}
