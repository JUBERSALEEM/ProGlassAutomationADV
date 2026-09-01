// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Wizard Step 9 — Profit & Loss (PHASE B)
//
// WHAT IT DOES
//   Answers the question the estimator cannot: "after we buy the glass and
//   run production, what is actually left on this invoice?"
//
//   For every Step 5 spec group it reverse-engineers the build from the
//   generated spec name, prices the raw sheets against the Sheet Store,
//   adds the production charges, and compares the total against the
//   quoted grand total (ex-VAT).
//
// SPEC NAME GRAMMAR (produced by Step 4, parsed back here)
//   outer   : "6mm HD Grey FT Glass"
//             ^(?<thk>\d+)\s*mm\s+(?<color>.+?)\s+(FT|Annealed|
//              Heat Strengthened|Heat Soaked)\s+Glass
//   spacer  : "+ 12mm ASP"           →  \+\s*(?<mm>\d+)\s*mm\s+ASP
//   inner   : same outer pattern, matched AFTER the spacer
//
// COST MODEL
//   cutSqm       = Σ (W1·H1/1e6  [+ W2·H2/1e6 when W2/H2 > 0]) · Qty
//   perimeterLm  = Σ (2·(W1+H1)/1000) · Qty
//   sheets       = ceil( cutSqm · (1 + Wastage%/100) / sheetSqm ), min 1
//   glass amount = round(sheetSqm · sheets · purchasePricePerSqm, 2)
//                  for multi-pane builds costed on full stock-sheet use
//   SGU glass    = round((cutSqm + wastage) · sheetPricePerSqm, 2)
//                  business rule: SGU glass is priced by required SQM,
//                  while cutting and tempering remain separate rows
//   DGU spacer   = round(cutSqm · DguVM.SpacerPrice, 2)
//                  ASP is quoted PER SQM in Step 4 (37.5+((w-6)/2)*2.5,
//                  so 45.00 at 12mm) and that price already includes
//                  cutting, tempering and production for DGU.
//   SGU prod.    = round(cutSqm · CuttingCostPerSqm, 2)
//                  + round(cutSqm · TemperingCostPerSqm, 2)
//
//   Unpriced panes are still SHOWN (so a DGU never collapses to only
//   the air spacer) but their amount stays 0 and is excluded from profit.
//   Step 4 pane rates are the fallback when Sheet Store has no match.
//
// TOTALS
//   NetExVat = round(gross / 1.05, 2)   (5% UAE VAT)
//   Profit   = NetExVat − TotalGlassCost − TotalProductionCost
//   Margin   = round(Profit / NetExVat · 100, 1)
//
// SAFETY: Bind() may be called before every dependency exists and
// RecalculateAsync() never throws — the wizard must stay usable even with
// an empty Sheet Store or a half-filled Step 5 grid.
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
    // PARTIAL CLASS (senior audit — future safe extraction seam)
    // ─────────────────────────────────────────────────────────
    // Declared partial so a follow-up PR can move regions into
    // sibling files (ProfitLossStepViewModel.Cost.cs,
    // ProfitLossStepViewModel.Rollup.cs, ProfitLossStepViewModel
    // .Serialization.cs) without disturbing this file's audit
    // history. See notes on SpecGroup for the same rationale.
    public partial class ProfitLossStepViewModel : ViewModelBase
    {
        public string Title { get; } = "PROFIT & LOSS";

        public string Subtitle { get; } =
            "Real cost of this invoice — glass from Sheet Store, production from Step 4 rates.";

        // ── Fallback stock sheet when the store has no size on file ──────
        private const int DefaultSheetWidthMm = 2440;
        private const int DefaultSheetHeightMm = 1830;

        /// <summary>UAE standard VAT divisor used to strip VAT off the gross.</summary>
        private const decimal VatDivisor = 1.05m;

        // ── Bound dependencies (assigned by the shell via Bind) ──────────
        private DataGridViewModel? _dataGrid;
        private DguParametersViewModel? _dguVm;
        private SguParametersViewModel? _sguVm;
        private LamParametersViewModel? _lamVm;   // PHASE J
        private DguLamParametersViewModel? _dguLamVm;   // DGU+LAM hybrid
        private OtherChargesViewModel? _otherChargesVm;
        private Func<decimal>? _grossProvider;

        // Spec-name grammar lives in PnLSpecParser — one definition for
        // the view-model and the tests. Do not re-declare those regexes.

        public ProfitLossStepViewModel()
        {
            RecalculateCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(FireRecalculateAsync, "ProfitLoss Recalc"));

            Rows.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasRows));
        }


        /// </summary>

    }
}
