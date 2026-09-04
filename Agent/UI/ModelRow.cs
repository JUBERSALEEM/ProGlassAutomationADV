// FILE: Agent/UI/ModelRow.cs — one row in the Settings local-model picker.

using ProGlassAutomation.Agent.Models;

namespace ProGlassAutomation.Agent.UI
{
    /// <summary>
    /// Display row for one catalog model. Kept out of AgentSettingsViewModel
    /// so the big VM stays readable; contains no behaviour.
    /// </summary>
    public sealed class ModelRow
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string SizeText { get; set; } = "";
        public string RamText { get; set; } = "";
        public string LicenseName { get; set; } = "";
        public string LicenseUrl { get; set; } = "";
        public bool IsRecommended { get; set; }
        public string StatusText { get; set; } = "";
        public bool IsInstalled { get; set; }
        public LocalModelEntry CatalogEntry { get; set; } = new();

        public string Badge => IsRecommended ? " ★ recommended for this PC" : "";

        public string DisplayStatus =>
            (IsInstalled ? "✅ Installed — " : "Not installed — ") +
            StatusText;

        public override string ToString() => Title;
    }
}
