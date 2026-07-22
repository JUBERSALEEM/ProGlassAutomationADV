namespace ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus
{
    /// <summary>
    /// Status flow for Delivery Notes:
    /// Draft → Dispatched → Delivered → Signed → Received
    /// </summary>
    public enum DeliveryNoteStatus
    {
        Draft,
        Dispatched,
        Delivered,
        Signed,
        Received
    }
}