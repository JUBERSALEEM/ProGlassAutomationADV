// FILE: Agent/UI/ModelRow.cs — one row in the Settings local-model picker.

using ProGlassAutomation.Agent.Models;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.Agent.UI
{
    /// <summary>
    /// Display row for one catalog model. Must notify: install/uninstall
    /// updates StatusText + IsInstalled AFTER construction, and the card's
    /// OneWay bindings (status text, button visibility) only re-read when
    /// PropertyChanged fires — a plain POCO row left the UI showing
    /// "not yet downloaded" forever after a successful install.
    /// </summary>
    public sealed class ModelRow : ViewModelBase
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string SizeText { get; set; } = "";
        public string RamText { get; set; } = "";
        public string LicenseName { get; set; } = "";
        public string LicenseUrl { get; set; } = "";
        public bool IsRecommended { get; set; }
        public LocalModelEntry CatalogEntry { get; set; } = new();

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled == value) return;
                _isInstalled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }

        public string Badge => IsRecommended ? " ★ recommended for this PC" : "";

        public string DisplayStatus =>
            (IsInstalled ? "✅ Installed — " : "Not installed — ") +
            StatusText;

        public override string ToString() => Title;
    }
}
