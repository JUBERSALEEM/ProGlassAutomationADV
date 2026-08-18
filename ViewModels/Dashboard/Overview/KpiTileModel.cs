// ═══════════════════════════════════════════════════════════════════════
// FILE: KpiTileModel.cs
// PROJECT: ProGlassAutomation
//
// KpiTileModel split out of OverviewDashboardViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SheetStore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    public class KpiTileModel : ViewModelBase
    {
        public string Icon { get; set; } = "";
        public string Label { get; set; } = "";
        public string DisplayValue { get; set; } = "";
        public string Accent { get; set; } = "Green";  // Green|Blue|Amber|Red
        public bool IsAlert { get; set; }
    }
}
