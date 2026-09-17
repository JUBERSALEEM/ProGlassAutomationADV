// ════════════════════════════════════════════════════════════════════════
//  Live feed — hands the run currently on screen to Step 6 and Step 9.
//
//  A saved session is the durable contract (see OptimizationResultFeed), but
//  sessions are only written when the operator presses SAVE. Requiring a save
//  before a quote could see the real sheet count would make the bridge look
//  broken most of the time, so every finished run also publishes itself here.
//
//  Direction of dependency: ViewModels → Services, which is the allowed way
//  round. The feed never reaches back into this class.
// ════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using ProGlassAutomation.Services.Optimization;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel
    {
        /// <summary>
        /// The run currently on screen, expanded to one row per Step 5 spec
        /// pane — the shape Step 6 and Step 9 consume.
        ///
        /// Racks with no recorded pane (hand-added stock, or a job imported
        /// without Step 4 sheets) are skipped rather than attributed to a
        /// guessed spec: a number attached to the wrong pane is worse than no
        /// number at all.
        /// </summary>
        public IReadOnlyList<OptimizationPaneResult> BuildLivePaneResults()
        {
            var rows = new List<OptimizationPaneResult>();

            foreach (var c in CostingRows)
            {
                if (c.Panes.Count == 0) continue;

                foreach (var pane in c.Panes)
                {
                    if (pane == null || pane.SpecNumber <= 0) continue;

                    rows.Add(new OptimizationPaneResult
                    {
                        SpecNumber = pane.SpecNumber,
                        Side = OptimizationResultFeed.NormalizeSide(pane.Side),
                        Composition = c.Composition,
                        Thickness = c.Thickness,
                        SheetWidthMm = c.WidthMm,
                        SheetHeightMm = c.HeightMm,
                        SheetQuantity = c.SheetCount,
                        PieceCount = c.PieceCount,
                        UtilizationPercent = c.UtilizationPercent,
                        GlassSqm = c.GlassM2,
                        StockSqm = c.StockM2,
                        WasteSqm = c.WasteM2Value,
                        RatePerSqm = c.Rate,
                        HasPrice = c.HasPrice,
                        IsSharedAcrossSpecs = c.IsSharedAcrossSpecs,
                        SharedWithSpecs = c.SharedWithSpecs,
                        SessionName = LastRunStage + " run (not saved)",
                        SessionCreatedUtc = DateTime.UtcNow
                    });
                }
            }

            return rows;
        }

        /// <summary>
        /// Publishes the finished run to the quoting steps. Called from
        /// ApplyResults, so the feed always describes the plan on screen.
        /// </summary>
        private void PublishLiveFeed()
        {
            var rows = BuildLivePaneResults();

            OptimizationResultFeed.PublishLive(rows, rows.Count == 0
                ? "live run has no Step 4 pane identity — import the Step 4 sheets on the STOCKS tab to link it to a spec"
                : string.Format(CultureInfo.InvariantCulture,
                    "live {0} run · {1:yyyy-MM-dd HH:mm} · {2} pane row(s)",
                    LastRunStage, DateTime.Now, rows.Count),
                // Stamped so Step 6 of a DIFFERENT quote cannot read this run.
                // Empty for a run driven from the OPTIMIZATION tab, which has no
                // proforma identity and stays usable everywhere as before.
                DocumentKey);
        }
    }
}
