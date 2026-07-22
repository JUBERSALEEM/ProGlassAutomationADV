namespace ProGlassAutomation.Models.DeliveryNote.Common.DeliveryType
{
    /// <summary>
    /// Classifies each Delivery Note:
    /// Original — normal first/partial delivery
    /// Dummy    — auto-generated from balance qty (Draft until confirmed)
    /// Rework   — re-delivery of reworked items from a rework JO
    /// Return   — tracking returned/rejected items
    /// </summary>
    public enum DeliveryType
    {
        Original,
        Dummy,
        Rework,
        Return
    }
}