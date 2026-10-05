// ═══════════════════════════════════════════════════════════════════════
// FILE: DuplicateAction.cs
// PROJECT: ProGlassAutomation
//
// DuplicateAction split out of DuplicateResolutionViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv
{
    public enum DuplicateAction
    {
        Skip,
        AddToStock,
        UpdateAll,
        Cancel
    }
}
