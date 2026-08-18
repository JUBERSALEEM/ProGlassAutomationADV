namespace ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus
{
    /// <summary>
    /// Status flow for Delivery Notes:
    /// Dispatching → Dispatched → Signed → Received
    /// (Draft kept as a legacy alias of Dispatching.)
    ///
    /// Stored as STRING in DB — never cast to/from int.
    /// Use DeliveryNoteStatusValues constants for comparisons.
    /// </summary>
    public enum DeliveryNoteStatus
    {
        Draft,
        Dispatched,
        Delivered,
        Signed,
        Received
    }

    /// <summary>
    /// String constants for status comparisons.
    /// Use these instead of magic strings throughout the codebase.
    /// Matches enum values exactly.
    /// </summary>
    public static class DeliveryNoteStatusValues
    {
        public const string Draft = "Draft";
        public const string Dispatching = "Dispatching";
        public const string Dispatched = "Dispatched";
        public const string Delivered = "Delivered";
        public const string Signed = "Signed";
        public const string Received = "Received";
    }
}