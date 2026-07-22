using System.Collections.Generic;

namespace ProGlassAutomation.Models.DeliveryNote.Common.DeliveryType
{
    /// <summary>
    /// Display metadata for each delivery type.
    /// Stored as string in DB — compare with string constants.
    /// </summary>
    public static class DeliveryTypeMeta
    {
        public static readonly IReadOnlyDictionary<string, TypeInfo> All =
            new Dictionary<string, TypeInfo>
            {
                ["Original"] = new("Original", "📋", "TintGreenBrush"),
                ["Dummy"] = new("Dummy", "📌", "TintAmberBrush"),
                ["Rework"] = new("Rework", "🔧", "TintOrangeBrush"),
                ["Return"] = new("Return", "↩️", "TintRedBrush")
            };

        public static TypeInfo Get(string type)
        {
            if (!string.IsNullOrWhiteSpace(type) && All.TryGetValue(type, out var info))
                return info;
            return new TypeInfo("Unknown", "❓", "SurfaceStrokeBrush");
        }
    }

    public sealed class TypeInfo
    {
        public string DisplayName { get; }
        public string Icon { get; }
        public string BrushKey { get; }

        public TypeInfo(string displayName, string icon, string brushKey)
        {
            DisplayName = displayName;
            Icon = icon;
            BrushKey = brushKey;
        }
    }
}