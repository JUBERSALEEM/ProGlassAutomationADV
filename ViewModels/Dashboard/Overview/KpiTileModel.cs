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

        // ═══════════════════════════════════════════════════════════════
        // KpiCardModel ALIASES — the dashboard grammar unification
        // (2026-09-08).
        //
        // The app had TWO KPI models and therefore TWO KPI templates with
        // different margins, which is why the KPI bands did not line up
        // when you switched tabs:
        //
        //     KpiCardModel  Title, SubLabel, BadgeColor (brush KEY), Icon
        //     KpiTileModel  Label,          Accent     (colour WORD), Icon
        //
        // Rather than rewrite the five ViewModels that build KpiCardModel
        // (or the two that build KpiTileModel), KpiTileModel now exposes
        // the KpiCardModel shape as computed aliases. One template —
        // DbKpiTileTemplate in Styles/DashboardKit.xaml — therefore renders
        // every KPI band in the application with identical geometry.
        //
        // These are read-only projections; nothing assigns them, so no
        // existing construction site changes and no state can drift.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>KpiCardModel-compatible label.</summary>
        public string Title => Label;

        /// <summary>
        /// KpiTileModel carries no footnote. Returning an empty string (not
        /// null) keeps the tile's third row present but zero-height, so a
        /// KpiTileModel band and a KpiCardModel band measure identically.
        /// </summary>
        public string SubLabel => string.Empty;

        /// <summary>
        /// KpiCardModel-compatible badge colour: a theme brush KEY, resolved
        /// by BrushKeyConverter. Maps the colour word this model has always
        /// used onto the same brushes the previous template's DataTriggers
        /// selected, so no tile changes colour as a result of this change.
        /// An alert always wins over the accent word.
        /// </summary>
        public string BadgeColor =>
            IsAlert ? "DangerBrush" : Accent switch
            {
                "Green" => "SuccessBrush",
                "Blue"  => "InfoBrush",
                "Amber" => "WarningBrush",
                "Red"   => "DangerBrush",
                _       => "AccentBrush",
            };
    }
}
