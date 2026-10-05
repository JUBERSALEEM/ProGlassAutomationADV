// ═══════════════════════════════════════════════════════════════════════
// FILE: Diagnostics/DiagnosticsTab.cs
// PROJECT: ProGlassAutomation
//
// DiagnosticsTab extracted from DiagnosticsViewModel.cs (Phase 11/12
// God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Diagnostics
{
    public class DiagnosticsTab : ViewModelBase
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "";
        public ViewModelBase Content { get; set; } = null!;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }
    }
}
