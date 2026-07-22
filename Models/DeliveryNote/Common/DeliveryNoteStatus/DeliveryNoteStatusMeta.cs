using System.Collections.Generic;

namespace ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus
{
    /// <summary>
    /// Display metadata for each DN status.
    /// Status is stored as string in DB — compare with string constants.
    /// Brush keys reference theme-aware brushes (not hex).
    /// </summary>
    public static class DeliveryNoteStatusMeta
    {
        public static readonly IReadOnlyDictionary<string, StatusInfo> All =
            new Dictionary<string, StatusInfo>
            {
                ["Draft"] = new("Draft", "📝", "TintAmberBrush"),
                ["Dispatched"] = new("Dispatched", "🚚", "TintBlueBrush"),
                ["Delivered"] = new("Delivered", "📦", "TintIndigoBrush"),
                ["Signed"] = new("Signed", "✍️", "TintGreenBrush"),
                ["Received"] = new("Received", "✅", "AccentBrush")
            };

        public static StatusInfo Get(string status)
        {
            if (!string.IsNullOrWhiteSpace(status) && All.TryGetValue(status, out var info))
                return info;
            return new StatusInfo("Unknown", "❓", "SurfaceStrokeBrush");
        }
    }

    public sealed class StatusInfo
    {
        public string DisplayName { get; }
        public string Icon { get; }
        public string BrushKey { get; }

        public StatusInfo(string displayName, string icon, string brushKey)
        {
            DisplayName = displayName;
            Icon = icon;
            BrushKey = brushKey;
        }
    }
}