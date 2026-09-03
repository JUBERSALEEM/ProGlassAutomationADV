// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Persistence.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Persistence members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions; // Match still used while production rows read spacer/pvb groups
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    public partial class ProfitLossStepViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════
        //  PERSISTENCE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the JSON-serializable form consumed by
        /// ProformaInvoiceMapper.BuildSnapshot(). Always returns an object
        /// (never null) so the mapper needs no extra guard.
        /// </summary>
        public ProfitLossSnapshot BuildSnapshot()
        {
            var snap = new ProfitLossSnapshot
            {
                GrossVatInclusive = GrossVatInclusive,
                NetExVat = NetExVat,
                TotalGlassCost = TotalGlassCost,
                TotalProductionCost = TotalProductionCost,
                Profit = Profit,
                MarginPercent = MarginPercent,
                OverallMarginPercent = OverallMarginPercent,
                NetMarginPercent = PriceBasedMarginPercent,
                WastagePercent = WastagePercent,
                CostAtSellingPrice = CostAtSellingPrice
            };

            foreach (var row in Rows)
                snap.Rows.Add(row.ToSnapshot());

            return snap;
        }
    }
}
