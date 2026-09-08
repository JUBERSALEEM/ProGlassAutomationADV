// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.QuoteBasis.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.QuoteBasis members.
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
        // ══════════════════════════════════════════════════════════════════
        //  QUOTE BASIS PER SPECIFICATION
        //
        //  Mirrors Step 4's rate formulas, stopping one step short: every
        //  Calculate*Rate is  basis x (1 + margin%),  and this returns the
        //  basis. That is the number a quoted rate has to be measured
        //  against.
        //
        //  Worked example, 6mm Clear FT + 12mm ASP + 6mm Clear FT:
        //      (25 + 25) / 0.85 + 45 = 103.82
        //      x 1.15 = 119.39     x 1.10 = 114.20
        //  So entering 114.20 reads as 10%, which is what the shop expects.
        //
        //  The build is identified from the SPEC NAME first (isDgu / isLam /
        //  isDguLam were already parsed out of it) and only falls back to the
        //  group's glass-type text. A proforma can hold several different
        //  builds at once, so using the live Step 4 selection would score
        //  every specification against whichever tab was last open.
        //
        //  Returns 0 when the owning panel is not bound, which makes the
        //  caller fall back to materials cost and say so on screen rather
        //  than silently quoting a wrong percentage.
        // ══════════════════════════════════════════════════════════════════

        private decimal QuoteBasisFor(
            string? glassType, bool isDgu, bool isLam, bool isDguLam,
            int spacerCavities = 1)
        {
            // Mirror-of-cost invariant: the basis is measured against the
            // SAME money model the cost rows use. BUSINESS DECISION
            // 2026-09-05 (owner-confirmed): each ASP cavity pays the
            // spacer field, so a triple build's basis carries the spacer
            // price twice, not once — otherwise the sanity percentage
            // would praise a rate against an understated basis.
            spacerCavities = Math.Max(1, spacerCavities);
            string type = (glassType ?? "").Trim().ToUpperInvariant();

            // Spec-name evidence wins: it describes the product actually
            // costed, whereas the type text can be blank or stale.
            if (isDguLam || type == "DGU+LAM")
            {
                var vm = _dguLamVm;
                if (vm == null) return 0m;

                decimal basis =
                    (vm.OuterBasePrice + vm.LamOuterBasePrice + vm.LamInnerBasePrice)
                        / PnLMeasurement.SafeYield(vm.YieldFactor)
                    + vm.TotalCuttingCost
                    + vm.TotalTemperingCost
                    + vm.InterlayerSellPrice
                    + vm.SpacerPrice * spacerCavities;

                return Math.Round(basis, 2);
            }

            if (isLam || type == "LAM")
            {
                var vm = _lamVm;
                if (vm == null) return 0m;

                decimal basis =
                    (vm.OuterBasePrice + vm.InnerBasePrice) / PnLMeasurement.SafeYield(vm.YieldFactor)
                    + vm.PvbMaterialRate
                    + vm.OuterCuttingCost + vm.InnerCuttingCost
                    + vm.OuterTemperingCost + vm.InnerTemperingCost;

                return Math.Round(basis, 2);
            }

            if (isDgu || type == "DGU")
            {
                var vm = _dguVm;
                if (vm == null) return 0m;

                decimal basis =
                    (vm.OuterBasePrice + vm.InnerBasePrice) / PnLMeasurement.SafeYield(vm.YieldFactor)
                    + vm.SpacerPrice * spacerCavities;

                return Math.Round(basis, 2);
            }

            // SINGLE GLAZED — the default, and the only build with no marker
            // of its own in the spec name.
            //
            // The other three are recognised from the specification text: a
            // spacer means DGU, an interlayer means LAM, both mean the
            // hybrid. Single glazing has neither, so it can only be reached
            // by elimination. Requiring the glass-type text to read exactly
            // "SGU" meant a specification with that field blank, or worded
            // "Single Glazed", got no basis at all — the rate box then fell
            // back to materials cost and reported a margin against the wrong
            // number, silently.
            //
            // Reaching here means the spec carries no spacer and no
            // interlayer, so a single pane is the correct reading.
            if (type == "SGU" || type.Length == 0 || type.Contains("SINGLE"))
            {
                var vm = _sguVm;
                if (vm == null) return 0m;

                decimal basis =
                    vm.BasePricePerSqm / PnLMeasurement.SafeYield(vm.YieldFactor)
                    + vm.CuttingCostPerSqm
                    + vm.TemperingCostPerSqm;

                return Math.Round(basis, 2);
            }

            return 0m;
        }

        /// <summary>
        /// Physical panes this specification must show. Parsed from the
        /// spec name first; missing DGU/LAM panes are synthesised from the
        /// live Step 4 panel so a Clear/Clear unit cannot collapse to
        /// "spacer only" just because a regex or the Sheet Store missed.
        ///
        /// FIX-2026-09-05 (3+ pane builds): a triple-glazed DGU carries
        /// THREE sheets of glass. The old outer+inner path took the spec's
        /// panes from ResolveTwoPanes, so the MIDDLE sheet never became a
        /// material row — the P&L silently costed a third of the glass
        /// away on every triple unit (the very mistake this method's own
        /// header says it was built to stop). Panes now come from
        /// ResolveAllPanes: outer, every middle (in spec order), inner.
        /// </summary>
        private List<PnLPane> CollectPanes(
            SpecGroup group,
            string specName, bool isDgu, bool isLam, bool isDguLam, bool hasInnerPane)
        {
            var panes = new List<PnLPane>();
            var matches = PnLSpecParser.GlassMatches(specName);

            if (isDguLam)
            {
                string[] labels = { "Outer", "Lam Outer", "Lam Inner" };
                if (matches.Count > 0)
                {
                    for (int i = 0; i < matches.Count; i++)
                    {
                        string label = i < labels.Length ? labels[i] : $"Pane {i + 1}";
                        panes.Add(PnLSpecParser.FromMatch(matches[i], label));
                    }
                }
                else if (_dguLamVm != null)
                {
                    panes.Add(PnLSpecParser.FromParts(
                        "Outer",
                        SheetStorePricingService.ParseMm(_dguLamVm.OuterThickness),
                        _dguLamVm.OuterGlassColor));
                    panes.Add(PnLSpecParser.FromParts(
                        "Lam Outer",
                        SheetStorePricingService.ParseMm(_dguLamVm.LamOuterThickness),
                        _dguLamVm.LamOuterColor));
                    panes.Add(PnLSpecParser.FromParts(
                        "Lam Inner",
                        SheetStorePricingService.ParseMm(_dguLamVm.LamInnerThickness),
                        _dguLamVm.LamInnerColor));
                }

                return EnforceStep4PaneIdentity(group, panes, isDguLam);
            }

            // FIX-2026-09-05: full ordered pane list (Outer/Middle…/Inner).
            // Two-pane builds keep the classic pair; the middle panes of
            // a 3+ pane build are material rows now, in spec order.
            var allPanes = PnLSpecParser.ResolveAllPanes(specName);
            for (int i = 0; i < allPanes.Count; i++)
                panes.Add(allPanes[i]);

            // Synthesis gaps, never synthesis replacement: when the spec
            // parse gives NOTHING for a side that the build type implies,
            // fall back to the live Step 4 panel (the Clear/Clear guard).
            if (panes.Count == 0)
            {
                if (isDgu && _dguVm != null)
                    panes.Add(PnLSpecParser.FromParts(
                        "Outer",
                        SheetStorePricingService.ParseMm(_dguVm.OuterThickness),
                        _dguVm.OuterGlassColor));
                else if (isLam && _lamVm != null)
                    panes.Add(PnLSpecParser.FromParts(
                        "Outer",
                        SheetStorePricingService.ParseMm(_lamVm.OuterThickness),
                        _lamVm.OuterGlassColor));
                else if (!hasInnerPane && _sguVm != null && matches.Count == 0)
                    panes.Add(PnLSpecParser.FromParts(
                        "Outer",
                        SheetStorePricingService.ParseMm(_sguVm.SelectedThickness),
                        _sguVm.GlassColor));
            }

            // A two-pane build with only the outer pane named must still
            // gain its synthesised inner — parsing one colour is NOT proof
            // the inner sheet is absent (FUZZY part-colours exist in the
            // wild). Middle panes are never synthesised: the spec is the
            // only source that can name a middle.
            if (hasInnerPane)
            {
                bool innerPresent = false;
                for (int i = 0; i < panes.Count; i++)
                {
                    if (string.Equals(panes[i].Side, "Inner",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        innerPresent = true;
                        break;
                    }
                }

                if (!innerPresent)
                {
                    if (isDgu && _dguVm != null)
                        panes.Add(PnLSpecParser.FromParts(
                            "Inner",
                            SheetStorePricingService.ParseMm(_dguVm.InnerThickness),
                            _dguVm.InnerGlassColor));
                    else if (isLam && _lamVm != null)
                        panes.Add(PnLSpecParser.FromParts(
                            "Inner",
                            SheetStorePricingService.ParseMm(_lamVm.InnerThickness),
                            _lamVm.InnerGlassColor));
                }
            }

            return EnforceStep4PaneIdentity(group, panes, isDguLam);
        }

        /// <summary>
        /// Step 5/Step 4 pane provenance is the authoritative identity for
        /// normal two-pane DGU/LAM costing. The human spec string remains a
        /// useful parser source, but it can be affected by wording extras or
        /// legacy imported text. Never let a parsed/fallback "Clear" replace
        /// an explicit Step 4 inner colour such as "Obscure Clear Ramly" —
        /// that also changes the Sheet Store lookup and can move the row from
        /// the 2500 × 1875 sheet to the outer 3210 × 2250 sheet.
        /// </summary>
        private static List<PnLPane> EnforceStep4PaneIdentity(
            SpecGroup group,
            List<PnLPane> panes,
            bool isDguLam)
        {
            if (group == null || panes.Count == 0 || isDguLam)
                return panes;

            string outerColor = (group.OuterGlassColor ?? string.Empty).Trim();
            string innerColor = (group.InnerGlassColor ?? string.Empty).Trim();
            if (outerColor.Length == 0 && innerColor.Length == 0)
                return panes;

            var fixedPanes = new List<PnLPane>(panes.Count);
            foreach (var pane in panes)
            {
                bool isInner = pane.Side.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isOuter = pane.Side.IndexOf("Outer", StringComparison.OrdinalIgnoreCase) >= 0
                    && pane.Side.IndexOf("Lam", StringComparison.OrdinalIgnoreCase) < 0;

                string authoritative = isInner
                    ? innerColor
                    : isOuter
                        ? outerColor
                        : string.Empty;

                fixedPanes.Add(!string.IsNullOrWhiteSpace(authoritative)
                    ? PnLSpecParser.FromParts(pane.Side, pane.ThicknessMm, authoritative)
                    : pane);
            }

            return fixedPanes;
        }

        /// <summary>
        /// Step 4 already holds a typed rate for every pane. That is what
        /// the spacer uses, and it is what glass must fall back to when
        /// Sheet Store has no matching colour/thickness — otherwise the
        /// sandwich collapses to "air spacer" on screen.
        /// </summary>
        private decimal FallbackPaneRate(
            SpecGroup group, string side, bool isDgu, bool isLam, bool isDguLam)
        {
            // Step 4 → Step 5 stores the authoritative pane box prices per
            // specification. Prefer those stored prices over the live Step 4
            // tab, because a PI can contain multiple specs and the visible tab
            // may belong to a different section.
            bool inner = side.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0
                      || side.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0
                      || side.IndexOf("Lam", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isDguLam)
            {
                if (side.IndexOf("Lam Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                    return group.InnerSheetPricePerSqm > 0m
                        ? group.InnerSheetPricePerSqm
                        : (_dguLamVm?.LamInnerBasePrice ?? 0m);
                if (side.IndexOf("Lam Outer", StringComparison.OrdinalIgnoreCase) >= 0)
                    return _dguLamVm?.LamOuterBasePrice ?? 0m;
                return group.OuterSheetPricePerSqm > 0m
                    ? group.OuterSheetPricePerSqm
                    : (_dguLamVm?.OuterBasePrice ?? 0m);
            }

            decimal grouped = inner
                ? group.InnerSheetPricePerSqm
                : group.OuterSheetPricePerSqm;
            if (grouped > 0m)
                return grouped;

            if (isDgu)
                return inner ? (_dguVm?.InnerBasePrice ?? 0m) : (_dguVm?.OuterBasePrice ?? 0m);
            if (isLam)
                return inner ? (_lamVm?.InnerBasePrice ?? 0m) : (_lamVm?.OuterBasePrice ?? 0m);
            return grouped > 0m ? grouped : (_sguVm?.BasePricePerSqm ?? 0m);
        }

        /// <summary>
        /// Prices one pane. Step 4 pane rate wins; Sheet Store is only the
        /// fallback; if both are empty the row is STILL emitted (amount 0)
        /// so the physical build stays visible. Returns null only when the
        /// pane itself cannot be identified.
        /// </summary>
        private async Task<ProfitLossRow?> BuildGlassRowAsync(
            SpecGroup group,
            string side,
            int thicknessMm,
            string color,
            decimal cutSqm,
            decimal fallbackRate,
            bool priceByRequiredArea)
        {
            int specNumber = group.GroupNumber;
            color = (color ?? "").Trim();

            if (thicknessMm <= 0 || color.Length == 0) return null;

            // FIX-2026-09-05 (3+ pane builds): a middle sheet is measured
            // against the INNER pane sheet geometry chosen in Step 4 —
            // treating it as outer picked a differently-sized sheet for
            // an identical build size.
            bool isInner = side.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0
                        || side.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0;

            int sheetW = 0;
            int sheetH = 0;
            string source;

            if (isInner && group.HasInnerSheet)
            {
                sheetW = group.InnerSheetWidthMm;
                sheetH = group.InnerSheetHeightMm;
                source = "chosen in Step 4";
            }
            else if (!isInner && group.HasOuterSheet)
            {
                sheetW = group.OuterSheetWidthMm;
                sheetH = group.OuterSheetHeightMm;
                source = "chosen in Step 4";
            }
            else
            {
                var sizes = await SheetStorePricingService
                    .GetSizesAsync(color, thicknessMm).ConfigureAwait(true);

                if (sizes.Count > 0)
                {
                    sheetW = sizes[0].W;
                    sheetH = sizes[0].H;
                    source = "from Sheet Store";
                }
                else
                {
                    sheetW = DefaultSheetWidthMm;
                    sheetH = DefaultSheetHeightMm;
                    source = "default sheet — not in Sheet Store";
                }
            }

            decimal sheetSqm = Math.Round((sheetW * (decimal)sheetH) / 1_000_000m, 4);
            if (sheetSqm <= 0m) return null;

            var price = await SheetStorePricingService
                .TryGetPriceForSizeAsync(color, thicknessMm, sheetW, sheetH)
                .ConfigureAwait(true);

            decimal purchaseRate = price?.PurchasePerSqm ?? 0m;
            decimal sellRate = price?.SellPerSqm ?? 0m;

            // Industrial rule: when Step 4 supplied an outer/inner pane price,
            // that box is authoritative. Sheet Store is only a fallback for
            // legacy/manual rows with no Step 4 handoff; fuzzy catalogue matches
            // must never turn "Obscure Clear Ramly" into generic Clear.
            decimal unitRate = 0m;
            if (fallbackRate > 0m)
            {
                unitRate = fallbackRate;
                source = $"Step 4 {CostBasisLabel}";
            }
            else
            {
                unitRate = CostAtSellingPrice
                    ? (sellRate > 0m ? sellRate : purchaseRate)
                    : (purchaseRate > 0m ? purchaseRate : sellRate);
            }

            decimal needed = PnLMeasurement.AreaWithWastage(cutSqm, WastagePercent);
            int sheets = PnLMeasurement.WholeSheetsRequired(needed, sheetSqm);
            bool fromStep6Setup = false;

            // ══════════════════════════════════════════════════════════════
            //  QTY COMES FROM THE STEP 6 WASTAGE SETUP, NOT FROM SHEET STORE
            //
            //  The operator's SHEET QTY box on the matching wastage row is
            //  authoritative for this pane's quantity (and therefore its
            //  glass cost) for EVERY pane type — SGU included. SGU panes
            //  keep the "priced by required area" basis only when no wastage
            //  row claims them; once the operator has set a sheet quantity
            //  for the pane, the row's number wins exactly as it does for
            //  DGU / LAM / DGU+LAM panes. A DISABLED wastage row still owns
            //  the setup quantity — IsActive only controls whether the
            //  wastage CHARGE is added to production cost. No match keeps
            //  the existing Sheet Store / SQM round-up as fallback.
            // ══════════════════════════════════════════════════════════════
            int setupQty = ResolveStep6WastageSheetQuantity(specNumber, side);
            if (setupQty > 0)
            {
                sheets = setupQty;
                fromStep6Setup = true;
            }

            decimal quantity = fromStep6Setup
                ? sheets
                : priceByRequiredArea
                    ? Math.Round(needed, 2)
                    : sheets;

            decimal amount = 0m;
            if (unitRate > 0m)
            {
                amount = fromStep6Setup
                    ? Math.Round(sheetSqm * sheets * unitRate, 2)
                    : priceByRequiredArea
                        ? Math.Round(needed * unitRate, 2)
                        : Math.Round(sheetSqm * sheets * unitRate, 2);
            }

            string detail = unitRate > 0m
                ? fromStep6Setup
                    ? $"{cutSqm:0.##} sqm → {sheets} sheet(s) · SHEET QTY from Step 6 wastage setup · {source}"
                    : priceByRequiredArea
                        ? $"{cutSqm:0.##} + {WastagePercent:0.#}% = {needed:0.##} sqm · priced by required area · {source}"
                        : $"{cutSqm:0.##} + {WastagePercent:0.#}% = {needed:0.##} sqm → {sheets} sheet(s) · {source}"
                : "UNPRICED — not in Sheet Store and no Step 4 rate · not included in profit";

            return new ProfitLossRow
            {
                Kind = "Glass",
                SpecNumber = specNumber,
                SpecName = group?.SelectedSpecName ?? "",
                Description = PnLMeasurement.BuildRowDescription(
                    group,
                    $"{side} pane · {thicknessMm}mm {color}",
                    detail),
                SheetSource = unitRate > 0m ? source : "unpriced",
                SheetWidthMm = sheetW,
                SheetHeightMm = sheetH,
                SheetSqm = sheetSqm,
                Quantity = quantity,
                PricePerSqm = unitRate,
                Amount = amount
            };
        }

        /// <summary>
        /// Maps a Step 9 pane side to its Step 6 wastage source label so the
        /// setup SHEET QTY is read for the EXACT pane — outer, inner, lam
        /// outer, lam inner — instead of applying one number to the build.
        /// </summary>
        private static string? WastageSideLabelForPane(string paneSide)
        {
            if (paneSide.IndexOf("Lam Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lam Inner (W2/H2)";
            if (paneSide.IndexOf("Lam Outer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lam Outer (W2/H2)";
            if (paneSide.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inner (W2/H2)";
            if (paneSide.IndexOf("Outer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Outer (W1/H1)";
            return null;
        }

        /// <summary>
        /// The sheet quantity the operator set in Step 6's wastage setup for
        /// THIS specification's pane side. A DISABLED wastage row still owns
        /// the setup quantity — IsActive only controls whether the wastage
        /// charge is added to production cost. 0 means no wastage row targets
        /// this pane, so the existing Sheet Store calculation remains the
        /// fallback.
        /// </summary>
        private int ResolveStep6WastageSheetQuantity(int specNumber, string paneSide)
        {
            string? sideLabel = WastageSideLabelForPane(paneSide);
            if (sideLabel == null) return 0;

            var rows = _otherChargesVm?.WastagesSection?.WastageRows;
            if (rows == null) return 0;

            foreach (var row in rows.OrderByDescending(r => r.IsActive))
            {
                if (row == null || row.SheetQuantity <= 0) continue;

                var sources = row.AvailableSources
                    .Where(s => s != null && s.IsChecked)
                    .ToList();
                if (sources.Count == 0) continue;

                // A single Step 6 wastage row may intentionally combine the
                // same glass across two specs. In that case the setup's sheet
                // quantity is TOTAL shared sheets, not the quantity for each
                // spec. Use the same allocator as Sheet Details/Target so the
                // actual-cost rows split 17 shared sheets into e.g. 16 + 1,
                // instead of showing 17 on every related spec.
                var allocation = Step6SheetAllocation.Allocate(row, sources)
                    .FirstOrDefault(a =>
                        a.Source.SpecNumber == specNumber
                        && string.Equals(
                            a.Source.SideLabel,
                            sideLabel,
                            StringComparison.OrdinalIgnoreCase));

                if (allocation != null)
                    return allocation.SheetQuantity;
            }

            return 0;
        }

    }
}
