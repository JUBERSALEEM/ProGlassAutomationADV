// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/AgingBarViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement — generator preview
//
// AgingBarViewModel split from the misleadingly-named StatementGeneratorViewModel2.cs
// (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public class AgingBarViewModel : ViewModelBase
    {
        public string Label { get; }
        public decimal Amount { get; }
        public decimal WidthPercent { get; }
        public string Color { get; }
        public bool HasAmount => Amount > 0;

        public string AmountDisplay
            => Amount > 0 ? $"AED {Amount:N0}" : "—";

        public string PercentDisplay
            => WidthPercent > 0
               ? $"{WidthPercent:0.#}%"
               : "";

        public AgingBarViewModel(
            string label, decimal amount, decimal total, string color)
        {
            Label = label;
            Amount = amount;
            Color = color;
            WidthPercent = total > 0
                ? Math.Round(amount / total * 100m, 1)
                : 0m;
        }
    }
}
