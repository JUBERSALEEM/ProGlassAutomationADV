// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Audit/AuditSections.cs
// PROJECT: ProGlassAutomation
// MODULE: Audit / Activity Logging
//
// Canonical section names used across the audit system. Always use these
// constants (never free strings) so the Activity view can group/filter
// reliably and typos cannot split one section into two.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>Section names for audit entries.</summary>
    public static class AuditSections
    {
        public const string App          = "Application";
        public const string Users        = "Users";
        public const string Dashboard    = "Dashboard";
        public const string SheetStore   = "Sheet Store";
        public const string Proforma     = "Proforma Invoice";
        public const string JobOrder     = "Job Order";
        public const string DeliveryNote = "Delivery Note";
        public const string TaxInvoice   = "Tax Invoice";
        public const string Statement    = "Statement";
        public const string Reports      = "Reports";
        public const string Settings     = "Settings";
        public const string Ai           = "AI Assistant";
        public const string Updates      = "Updates";
    }
}
