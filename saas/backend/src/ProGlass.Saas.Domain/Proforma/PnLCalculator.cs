// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   ViewModels/ProformaInvoice/Step9ProfitLoss/
//     ProfitLossStepViewModel.{Calculation,QuoteBasis,CalculationSupport}.
//     cs — the row builders + the three-margin KPI model
//   PnLMeasurement.cs — billable-SQM measurement + wastage math
//   Step6SheetAllocation.cs — shared wastage sheet-count allocation
//   Services/ProformaInvoice/SheetStorePricingService.cs — the sheet
//     lookup contract (Norm colour match, area-desc sizes, exact price)
// The WPF files are the source of truth; behaviour preserved. Differences
// (documented in WPF_TO_SAAS_MAPPING §1.3c):
//   • The live Step 4 panel fallbacks do not exist server-side: the saved
//     group's Step 4 provenance is the only rate source (the WPF itself
//     calls the panel "legacy fallback only", PHASE 1-AUDIT).
//   • Pane synthesis from the live panel (the Clear/Clear guard) is not
//     possible: a spec name that does not parse produces no glass rows —
//     the operator priced that build manually.
//   • The advanced-automation suggestion engine is a deferred sub-item;
//     only explicitly saved Step 6 rows are costed.
//   • NetExVat = gross / 1.05 verbatim (the WPF binds a net provider but
//     RecalculateAsync still uses the 5% divisor — preserved quirk).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>One Sheet Store row, the shape the P&L lookups need.</summary>
public sealed record SheetStockEntry(
    string Color, int ThicknessMm,
    int WidthMm, int HeightMm,
    decimal PurchasePerSqm, decimal SellPerSqm);

/// <summary>
/// The Sheet Store lookups (SheetStorePricingService contract): Norm()
/// colour matching on both sides, sizes ordered by area DESC, exact
/// (colour, thickness, size) price resolution. Pure, in-memory.
/// </summary>
public sealed class SheetStockIndex
{
    private readonly List<SheetStockEntry> _entries;

    public SheetStockIndex(IEnumerable<SheetStockEntry> entries) =>
        _entries = entries.ToList();

    private static string Norm(string? color) =>
        (color ?? "").Trim().ToLowerInvariant();

    /// <summary>Distinct sizes for a colour + thickness, biggest first.</summary>
    public IReadOnlyList<(int W, int H)> GetSizes(string? color, int thicknessMm)
    {
        var key = Norm(color);
        var result = new List<(int, int)>();
        if (key.Length == 0 || thicknessMm <= 0)
        {
            return result;
        }

        var seen = new HashSet<(int, int)>();
        foreach (var s in _entries
                     .Where(s => Norm(s.Color) == key
                                 && s.ThicknessMm == thicknessMm
                                 && s.WidthMm > 0 && s.HeightMm > 0)
                     .OrderByDescending(s => (long)s.WidthMm * s.HeightMm))
        {
            if (seen.Add((s.WidthMm, s.HeightMm)))
            {
                result.Add((s.WidthMm, s.HeightMm));
            }
        }

        return result;
    }

    /// <summary>
    /// Exact purchase/sell price for one stocked size. WPF contract: the
    /// size matches ROTATION-TOLERANT (3210×2250 == 2250×3210), and when
    /// several suppliers stock the same sheet the priced rows win, then
    /// the cheapest purchase price.
    /// </summary>
    public (decimal Purchase, decimal Sell)? TryGetExactPrice(
        string? color, int thicknessMm, int widthMm, int heightMm)
    {
        var key = Norm(color);
        if (key.Length == 0 || thicknessMm <= 0 || widthMm <= 0 || heightMm <= 0)
        {
            return null;
        }

        var sheet = _entries
            .Where(s => Norm(s.Color) == key && s.ThicknessMm == thicknessMm
                        && ((s.WidthMm == widthMm && s.HeightMm == heightMm)
                            || (s.WidthMm == heightMm && s.HeightMm == widthMm)))
            .OrderBy(s => s.PurchasePerSqm > 0m ? 0 : 1)
            .ThenBy(s => s.PurchasePerSqm)
            .FirstOrDefault();
        return sheet is null ? null : (sheet.PurchasePerSqm, sheet.SellPerSqm);
    }
}

    /// <summary>Step 6 charge-row inputs (saved section rows).</summary>
public sealed record PnlStep6Charge(
    string TemplateKey,
    string ChargeName,
    string MetricKey,
    string Unit,
    IReadOnlyList<int> CheckedSpecNumbers,
    decimal Rate,
    string PolishSubType,
    bool IsMatchedLamPolishRow,
    bool IsActive = true);

public sealed record PnlStep6Wastage(
    IReadOnlyList<string> CheckedSourceKeys,
    int SheetQuantity,
    decimal OptimizationPercent,
    bool IsActive);

public sealed record PnlStep6Custom(
    string ChargeName, decimal Qty, decimal Rate, string Unit,
    bool IsActive = true);

/// <summary>One P&L row (WPF ProfitLossRow).</summary>
public sealed record PnlRow(
    string Kind,
    int SpecNumber,
    string SpecName,
    string Description,
    int SheetWidthMm,
    int SheetHeightMm,
    decimal SheetSqm,
    decimal Quantity,
    decimal PricePerUnit,
    string UnitLabel,
    decimal Amount,
    string SheetSource);

/// <summary>The job-level KPIs (WPF ProfitLossSnapshot field-for-field).</summary>
public sealed record PnlKpis(
    decimal GrossVatInclusive,
    decimal NetExVat,
    decimal TotalGlassCost,
    decimal TotalProductionCost,
    decimal Profit,
    bool IsLoss,
    decimal MarginPercent,
    decimal OverallMarginPercent,
    decimal NetMarginPercent,
    decimal UInsertCost,
    decimal BasisCost);

/// <summary>The full recalculation output.</summary>
public sealed record PnlResult(
    IReadOnlyList<PnlRow> Rows,
    PnlKpis Kpis,
    IReadOnlyDictionary<int, decimal> SpecAreas,
    IReadOnlyDictionary<int, decimal> SpecBases);

/// <summary>Step 9 Profit &amp; Loss, WPF-verbatim.</summary>
public static class PnLCalculator
{
    public const int DefaultSheetWidthMm = 3210;
    public const int DefaultSheetHeightMm = 2250;

    /// <summary>WPF VatDivisor = VatCalculator.VAT_MULTIPLIER (1.05). The
    /// RecalculateAsync still divides by it verbatim even when the invoice
    /// is zero-rated — preserved quirk, documented.</summary>
    public const decimal VatDivisor = 1.05m;

    public sealed record Inputs(
        IReadOnlyList<SpecGroupInput> Groups,
        IReadOnlyList<PnlStep6Charge> Charges,
        IReadOnlyList<PnlStep6Wastage> Wastages,
        IReadOnlyList<PnlStep6Custom> Customs,
        SheetStockIndex Sheets,
        decimal WastagePercent,
        bool CostAtSellingPrice,
        decimal GrossVatInclusive);

    public static PnlResult Recalculate(Inputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var rows = new List<PnlRow>();
        var specAreas = new Dictionary<int, decimal>();
        var specBases = new Dictionary<int, decimal>();
        decimal glassCost = 0m;
        decimal productionCost = 0m;
        decimal uInsertInProduction = 0m;

        var wastagePercent = Math.Clamp(inputs.WastagePercent, 0m, 95m);
        var yield = EffectiveYieldFactor(wastagePercent);

        foreach (var group in inputs.Groups)
        {
            var cutSqm = MeasureGroup(group);
            if (cutSqm <= 0m)
            {
                continue;
            }

            specAreas[group.GroupNumber] = cutSqm;

            var specName = group.SpecName;
            var spacerMatch = SpecGrammar.Spacer(specName);
            var spacerDetected = spacerMatch.Success;
            var pvbMatch = SpecGrammar.Interlayer(specName);
            var pvbDetected = pvbMatch.Success;

            // EVERY air-spacer cavity pays the spacer field (2026-09-05,
            // owner-confirmed); a legacy DGU string clamps to one cavity.
            var spacerCavities = Math.Max(1, SpecGrammar.SpacerCount(specName));

            // WPF-verbatim: the flags read the RAW Step 4 glass-type field
            // (plus the spec-name tokens) — not the resolved type.
            var rawType = group.GlassType ?? "";
            var isDgu = rawType.Contains("DGU", StringComparison.OrdinalIgnoreCase)
                        || spacerDetected;
            var isLam = rawType.Contains("LAM", StringComparison.OrdinalIgnoreCase)
                        || pvbDetected;
            var isDguLam = spacerDetected && pvbDetected;

            specBases[group.GroupNumber] = QuoteBasisFor(
                group, isDgu, isLam, isDguLam, spacerCavities, yield);

            var panes = CollectPanes(group);
            // The display name rewrites the spec's colour tokens with the
            // authoritative Step 4 colours (WPF BuildStep9SpecDisplay).
            var specDisplay = BuildStep9SpecDisplay(group);
            var spacerInsertAt = -1;
            var interlayerInsertAt = -1;

            for (var paneIndex = 0; paneIndex < panes.Count; paneIndex++)
            {
                var pane = panes[paneIndex];
                var fallback = FallbackPaneRate(group, pane.Side, isDguLam);

                var paneRow = BuildGlassRow(
                    group, specDisplay, pane, cutSqm, fallback,
                    priceByRequiredArea: !isDgu && !isLam && !isDguLam,
                    inputs, wastagePercent);

                if (paneRow is not null)
                {
                    rows.Add(paneRow);
                    if (paneRow.Amount > 0m)
                    {
                        glassCost += paneRow.Amount;
                    }
                }

                if (isDguLam)
                {
                    if (paneIndex == 0)
                    {
                        spacerInsertAt = rows.Count;
                    }

                    if (paneIndex == 1)
                    {
                        interlayerInsertAt = rows.Count + 1;
                    }
                }
            }

            if (!isDguLam)
            {
                spacerInsertAt = rows.Count > 0 ? 1 : rows.Count;
            }

            int AddLayerRow(PnlRow layerRow, int at)
            {
                if (at >= 0 && at <= rows.Count)
                {
                    rows.Insert(at, layerRow);
                    return at + 1;
                }

                rows.Add(layerRow);
                return at;
            }

            // ── Production rows (DGU+LAM tested FIRST: it satisfies both
            //    isDgu and isLam) ────────────────────────────────────────
            if (isDguLam)
            {
                // ASP per cavity (covers cutting, tempering and production
                // on the DGU side) + the outsourced interlayer at BUY cost
                // + processing on ALL THREE panes.
                var spacerPrice = group.Step4SpacerPricePerSqm ?? 0m;
                if (spacerPrice > 0m)
                {
                    var spacerMm = int.TryParse(spacerMatch.Groups["mm"].Value, out var mm) ? mm : 0;
                    var amount = Math.Round(cutSqm * spacerPrice * spacerCavities, 2);

                    spacerInsertAt = AddLayerRow(new PnlRow(
                        "Spacer", group.GroupNumber, specDisplay,
                        $"{spacerCavities > 1
                            ? $"{spacerMm}mm ASP x {spacerCavities} cavities (includes cutting, tempering and production)"
                            : $"{spacerMm}mm ASP (includes cutting, tempering and production)"} · {cutSqm:0.##} SQM",
                        0, 0, 0m,
                        Math.Round(cutSqm * spacerCavities, 2), spacerPrice, "SQM",
                        amount, ""), spacerInsertAt);

                    if (interlayerInsertAt >= spacerInsertAt - 1)
                    {
                        interlayerInsertAt++;
                    }

                    productionCost += amount;
                }

                var interlayerBuy = group.Step4InterlayerCostPerSqm ?? 0m;
                var lamProcessing = (group.Step4CuttingCostPerSqm ?? 0m)
                                    + (group.Step4TemperingCostPerSqm ?? 0m);
                var lamRate = interlayerBuy + lamProcessing;

                if (lamRate > 0m)
                {
                    var amount = Math.Round(cutSqm * lamRate, 2);
                    var interlayerMm = pvbMatch.Groups["mm"].Value;
                    // WPF reads the interlayer type from the live panel and
                    // falls back to "PVB"; server-side there is no panel.
                    const string interlayerType = "PVB";
                    var lamLabel = interlayerMm.Length > 0
                        ? $"{interlayerMm}mm {interlayerType} interlayer (outsourced) + cutting/tempering (all three panes)"
                        : $"{interlayerType} interlayer (outsourced) + cutting/tempering (all three panes)";

                    interlayerInsertAt = AddLayerRow(new PnlRow(
                        "Production", group.GroupNumber, specDisplay,
                        $"{lamLabel} · {cutSqm:0.##} SQM",
                        0, 0, 0m,
                        Math.Round(cutSqm, 2), lamRate, "SQM", amount, ""),
                        interlayerInsertAt);

                    productionCost += amount;
                }
            }
            else if (isDgu)
            {
                // ASP is priced PER SQM (the rate's own unit) and INCLUDES
                // cutting, tempering and production — no separate line.
                var spacerPrice = group.Step4SpacerPricePerSqm ?? 0m;
                if (spacerPrice > 0m && cutSqm > 0m)
                {
                    var spacerMm = spacerDetected
                        ? (int.TryParse(spacerMatch.Groups["mm"].Value, out var mm) ? mm : 0)
                        : 0;
                    var amount = Math.Round(cutSqm * spacerPrice * spacerCavities, 2);

                    spacerInsertAt = AddLayerRow(new PnlRow(
                        "Spacer", group.GroupNumber, specDisplay,
                        $"{spacerCavities > 1
                            ? $"{spacerMm}mm ASP x {spacerCavities} cavities (includes cutting, tempering and production)"
                            : $"{spacerMm}mm ASP (includes cutting, tempering and production)"} · {cutSqm:0.##} SQM",
                        0, 0, 0m,
                        Math.Round(cutSqm * spacerCavities, 2), spacerPrice, "SQM",
                        amount, ""), spacerInsertAt);

                    productionCost += amount;
                }
            }
            else if (isLam)
            {
                // Interlayer material + cutting and tempering on BOTH panes.
                var pvbRate = group.Step4InterlayerCostPerSqm ?? 0m;
                var processing = (group.Step4CuttingCostPerSqm ?? 0m)
                                 + (group.Step4TemperingCostPerSqm ?? 0m);
                var lamRate = pvbRate + processing;

                if (lamRate > 0m)
                {
                    var amount = Math.Round(cutSqm * lamRate, 2);
                    var pvbMm = pvbDetected ? pvbMatch.Groups["mm"].Value : "";
                    var pvbType = pvbDetected
                        ? pvbMatch.Groups["type"].Value.ToUpperInvariant()
                        : "PVB";
                    var label = pvbMm.Length > 0
                        ? $"{pvbMm}mm {pvbType} interlayer + cutting/tempering (both panes)"
                        : $"{pvbType} interlayer + cutting/tempering (both panes)";

                    spacerInsertAt = AddLayerRow(new PnlRow(
                        "Production", group.GroupNumber, specDisplay,
                        $"{label} · {cutSqm:0.##} SQM",
                        0, 0, 0m,
                        Math.Round(cutSqm, 2), lamRate, "SQM", amount, ""),
                        spacerInsertAt);

                    productionCost += amount;
                }
            }
            else
            {
                // Single glazing: glass by required SQM, cutting and
                // tempering broken out as separate SQM charges.
                var cuttingRate = group.Step4CuttingCostPerSqm ?? 0m;
                var temperingRate = group.Step4TemperingCostPerSqm ?? 0m;

                if (cuttingRate > 0m)
                {
                    var amount = Math.Round(cutSqm * cuttingRate, 2);
                    rows.Add(new PnlRow(
                        "Production", group.GroupNumber, specDisplay,
                        $"Cutting · {cutSqm:0.##} SQM",
                        0, 0, 0m, Math.Round(cutSqm, 2), cuttingRate, "SQM",
                        amount, ""));
                    productionCost += amount;
                }

                if (temperingRate > 0m)
                {
                    var amount = Math.Round(cutSqm * temperingRate, 2);
                    rows.Add(new PnlRow(
                        "Production", group.GroupNumber, specDisplay,
                        $"Tempering · {cutSqm:0.##} SQM",
                        0, 0, 0m, Math.Round(cutSqm, 2), temperingRate, "SQM",
                        amount, ""));
                    productionCost += amount;
                }
            }
        }

        AppendStep6Costs(inputs, rows, ref productionCost, ref uInsertInProduction);

        // ── Totals ──────────────────────────────────────────────────────
        var gross = inputs.GrossVatInclusive;
        var netExVat = Math.Round(gross / VatDivisor, 2);
        var totalGlassCost = Math.Round(glassCost, 2);
        var totalProductionCost = Math.Round(productionCost, 2);
        var profit = Math.Round(netExVat - totalGlassCost - totalProductionCost, 2);

        // Only call it a loss when there is revenue to lose against.
        var isLoss = gross > 0m && profit < 0m;

        // Cost-based basis: Σ per-spec QuoteBasis × area, falling back to
        // the actual cost minus the U-insert double-count.
        decimal basisTotal = 0m;
        foreach (var group in inputs.Groups)
        {
            if (!specBases.TryGetValue(group.GroupNumber, out var basis)
                || !specAreas.TryGetValue(group.GroupNumber, out var area))
            {
                continue;
            }

            if (basis <= 0m || area <= 0m)
            {
                continue;
            }

            basisTotal += Math.Round(basis * area, 2);
        }

        // The Price-Based KPI's U-insert: recomputed from the spec groups
        // (the job still carries the cost even when its Step 6 row was left
        // inactive); the production-cost amount is only the fallback.
        var computedUInsert = PriceBasedUInsertCost(inputs);
        var uInsertCost = computedUInsert > 0m
            ? Math.Round(computedUInsert, 2)
            : Math.Round(uInsertInProduction, 2);
        var basisCost = basisTotal > 0m
            ? Math.Round(basisTotal, 2)
            : Math.Max(0m, Math.Round(
                totalGlassCost + totalProductionCost - uInsertCost, 2));

        // 1. COST-BASED margin — GROSS revenue against the basis.
        var baseCostProfit = Math.Round(gross - basisCost, 2);
        var marginPercent = basisCost > 0m
            ? Math.Round(baseCostProfit / basisCost * 100m, 2) : 0m;

        // 2. OVERALL margin — NET revenue against ACTUAL total cost (the
        //    decision number).
        var actualTotalCost = totalGlassCost + totalProductionCost;
        var actualProfit = Math.Round(netExVat - actualTotalCost, 2);
        var overallMarginPercent = actualTotalCost > 0m
            ? Math.Round(actualProfit / actualTotalCost * 100m, 2) : 0m;

        // 3. NET margin — profit as a share of net revenue.
        var netMarginPercent = netExVat > 0m
            ? Math.Round(actualProfit / netExVat * 100m, 2) : 0m;

        return new PnlResult(
            rows,
            new PnlKpis(
                gross, netExVat, totalGlassCost, totalProductionCost,
                profit, isLoss,
                marginPercent, overallMarginPercent, netMarginPercent,
                uInsertCost, basisCost),
            specAreas,
            specBases);
    }

    // ════════════════════════════════════════════════════════════════
    //  Measurement (PnLMeasurement)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Billable cut area: Step 5 charges every panel with the 0.5 m²
    /// minimum floor, so Step 9 must reconcile to the same SQM source —
    /// including the second pane of rows that carry W2/H2.
    /// </summary>
    public static decimal MeasureGroup(SpecGroupInput group)
    {
        decimal cutSqm = 0m;

        foreach (var row in group.Rows)
        {
            var qty = row.Qty > 0 ? row.Qty : 0;
            if (qty == 0)
            {
                continue;
            }

            var area = SqmCalculator.CalculatePanel(row.W1, row.H1);
            if (row.W2 > 0 && row.H2 > 0)
            {
                area += SqmCalculator.CalculatePanel(row.W2, row.H2);
            }

            cutSqm += area * qty;
        }

        return cutSqm;
    }

    /// <summary>Required area after wastage (4 dp).</summary>
    public static decimal AreaWithWastage(decimal cutSqm, decimal wastagePercent) =>
        cutSqm <= 0m
            ? 0m
            : Math.Round(cutSqm * (1m + Math.Max(0m, wastagePercent) / 100m), 4);

    /// <summary>Whole sheets for a pane costed by stock-sheet use.</summary>
    public static int WholeSheetsRequired(decimal requiredSqm, decimal sheetSqm)
    {
        if (requiredSqm <= 0m || sheetSqm <= 0m)
        {
            return 0;
        }

        var sheets = (int)Math.Ceiling(requiredSqm / sheetSqm);
        return sheets < 1 ? 1 : sheets;
    }

    /// <summary>yield = 1 − wastage/100, floor 0.05 (never zero).</summary>
    public static decimal EffectiveYieldFactor(decimal wastagePercent)
    {
        var yieldValue = 1m - wastagePercent / 100m;
        return yieldValue <= 0m ? 0.05m : yieldValue;
    }

    // ════════════════════════════════════════════════════════════════
    //  Panes (CollectPanes + EnforceStep4PaneIdentity)
    // ════════════════════════════════════════════════════════════════

    private static List<SpecPane> CollectPanes(SpecGroupInput group)
    {
        var panes = SpecGrammar.ResolveAllPanes(group.SpecName).ToList();

        // Step 4/Step 5 pane provenance is the authoritative identity: a
        // parsed "Clear" must never beat an explicit inner colour.
        var outerColor = group.OuterGlassColor.Trim();
        var innerColor = group.InnerGlassColor.Trim();
        if (outerColor.Length == 0 && innerColor.Length == 0)
        {
            return panes;
        }

        var fixedPanes = new List<SpecPane>(panes.Count);
        foreach (var pane in panes)
        {
            var isInner = pane.Side.Contains("Inner", StringComparison.OrdinalIgnoreCase);
            var isOuter = pane.Side.Contains("Outer", StringComparison.OrdinalIgnoreCase)
                          && !pane.Side.Contains("Lam", StringComparison.OrdinalIgnoreCase);

            var authoritative = isInner ? innerColor : isOuter ? outerColor : "";
            fixedPanes.Add(authoritative.Length > 0
                ? pane with { Color = authoritative }
                : pane);
        }

        return fixedPanes;
    }

    /// <summary>
    /// WPF BuildStep9SpecDisplay: rewrites the spec name's colour tokens
    /// with the authoritative Step 4 colours — first glass match gets the
    /// outer colour, last match gets the inner colour. Middle panes keep
    /// whatever the spec says.
    /// </summary>
    private static string BuildStep9SpecDisplay(SpecGroupInput group)
    {
        var specName = group.SpecName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(specName))
        {
            return specName;
        }

        var matches = SpecGrammar.GlassMatches(specName).ToList();
        if (matches.Count == 0)
        {
            return specName;
        }

        var replacements = new List<(int Index, int Length, string Value)>();
        void AddColourReplacement(System.Text.RegularExpressions.Match match, string colour)
        {
            if (string.IsNullOrWhiteSpace(colour))
            {
                return;
            }

            var g = match.Groups["color"];
            if (!g.Success || g.Index < 0 || g.Length <= 0)
            {
                return;
            }

            replacements.Add((g.Index, g.Length, colour.Trim()));
        }

        AddColourReplacement(matches[0], group.OuterGlassColor ?? string.Empty);
        if (matches.Count > 1)
        {
            AddColourReplacement(matches[^1], group.InnerGlassColor ?? string.Empty);
        }

        if (replacements.Count == 0)
        {
            return specName;
        }

        var sb = new System.Text.StringBuilder(specName);
        foreach (var r in replacements.OrderByDescending(r => r.Index))
        {
            sb.Remove(r.Index, r.Length).Insert(r.Index, r.Value);
        }

        return sb.ToString();
    }

    /// <summary>WPF FindSpecName — the RAW spec name, "SPEC n" fallback.</summary>
    private static string SpecNameOrFallback(SpecGroupInput group, int specNumber) =>
        string.IsNullOrEmpty(group.SpecName)
            ? (specNumber > 0 ? $"SPEC {specNumber}" : "Invoice-wide other charges")
            : group.SpecName;

    /// <summary>
    /// The pane's Step 4 box price — outer/inner/lam by side. The group's
    /// stored prices are the only source (the live Step 4 tab does not
    /// exist server-side; the WPF calls it "legacy fallback only").
    /// </summary>
    private static decimal FallbackPaneRate(SpecGroupInput group, string side, bool isDguLam)
    {
        var inner = side.Contains("Inner", StringComparison.OrdinalIgnoreCase)
                    || side.Contains("Middle", StringComparison.OrdinalIgnoreCase)
                    || side.Contains("Lam", StringComparison.OrdinalIgnoreCase);

        if (isDguLam)
        {
            if (side.Contains("Lam Inner", StringComparison.OrdinalIgnoreCase))
            {
                return group.InnerSheetPricePerSqm;
            }

            if (side.Contains("Lam Outer", StringComparison.OrdinalIgnoreCase))
            {
                return 0m; // the WPF reads the live panel for this pane only
            }

            return group.OuterSheetPricePerSqm;
        }

        return inner ? group.InnerSheetPricePerSqm : group.OuterSheetPricePerSqm;
    }

    // ════════════════════════════════════════════════════════════════
    //  Glass rows (BuildGlassRowAsync)
    // ════════════════════════════════════════════════════════════════

    private static PnlRow? BuildGlassRow(
        SpecGroupInput group,
        string specDisplay,
        SpecPane pane,
        decimal cutSqm,
        decimal fallbackRate,
        bool priceByRequiredArea,
        Inputs inputs,
        decimal wastagePercent)
    {
        var color = pane.Color.Trim();
        if (pane.ThicknessMm <= 0 || color.Length == 0)
        {
            return null;
        }

        // A middle sheet is measured against the INNER pane geometry.
        var isInner = pane.Side.Contains("Inner", StringComparison.OrdinalIgnoreCase)
                      || pane.Side.Contains("Middle", StringComparison.OrdinalIgnoreCase);

        int sheetW;
        int sheetH;
        string source;
        bool missingAuthoritativeStep4Sheet;

        var hasAuthoritativePaneHandoff = isInner
            ? group.InnerGlassColor.Trim().Length > 0 || group.InnerSheetPricePerSqm > 0m
            : group.OuterGlassColor.Trim().Length > 0 || group.OuterSheetPricePerSqm > 0m;

        if (isInner && group.HasInnerSheet)
        {
            sheetW = group.InnerSheetWidthMm;
            sheetH = group.InnerSheetHeightMm;
            source = "chosen in Step 4";
            missingAuthoritativeStep4Sheet = false;
        }
        else if (!isInner && group.HasOuterSheet)
        {
            sheetW = group.OuterSheetWidthMm;
            sheetH = group.OuterSheetHeightMm;
            source = "chosen in Step 4";
            missingAuthoritativeStep4Sheet = false;
        }
        else if (hasAuthoritativePaneHandoff)
        {
            // A pane Step 4 identified but did not give a sheet is a DATA
            // GAP — a zero row, never a catalogue-invented size.
            sheetW = 0;
            sheetH = 0;
            source = "MISSING Step 4 selected sheet — re-apply Step 4 for this spec";
            missingAuthoritativeStep4Sheet = true;
        }
        else
        {
            var sizes = inputs.Sheets.GetSizes(color, pane.ThicknessMm);
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

            missingAuthoritativeStep4Sheet = false;
        }

        var sheetSqm = Math.Round(sheetW * (decimal)sheetH / 1_000_000m, 4);
        if (sheetSqm <= 0m && !missingAuthoritativeStep4Sheet)
        {
            return null;
        }

        var price = missingAuthoritativeStep4Sheet
            ? null
            : inputs.Sheets.TryGetExactPrice(color, pane.ThicknessMm, sheetW, sheetH);

        var purchaseRate = price?.Purchase ?? 0m;
        var sellRate = price?.Sell ?? 0m;

        // The Step 4 pane box price is authoritative; Sheet Store is only
        // the fallback (and fuzzy matches must never re-colour a pane).
        decimal unitRate;
        if (fallbackRate > 0m)
        {
            unitRate = fallbackRate;
            if (!missingAuthoritativeStep4Sheet)
            {
                source = $"Step 4 {CostBasisLabel(inputs.CostAtSellingPrice)}";
            }
        }
        else
        {
            unitRate = inputs.CostAtSellingPrice
                ? (sellRate > 0m ? sellRate : purchaseRate)
                : (purchaseRate > 0m ? purchaseRate : sellRate);
        }

        var needed = AreaWithWastage(cutSqm, wastagePercent);
        var sheets = missingAuthoritativeStep4Sheet
            ? 0
            : WholeSheetsRequired(needed, sheetSqm);
        var fromStep6Setup = false;

        // The Step 6 wastage setup's SHEET QTY is authoritative for this
        // pane — a DISABLED row still owns the setup quantity.
        var setupQty = ResolveStep6WastageSheetQuantity(
            inputs, group.GroupNumber, pane.Side);
        if (setupQty > 0)
        {
            sheets = setupQty;
            fromStep6Setup = true;
        }

        var quantity = fromStep6Setup
            ? sheets
            : priceByRequiredArea ? Math.Round(needed, 2) : sheets;

        decimal amount = 0m;
        if (unitRate > 0m)
        {
            amount = fromStep6Setup || !priceByRequiredArea
                ? Math.Round(sheetSqm * sheets * unitRate, 2)
                : Math.Round(needed * unitRate, 2);
        }

        var detail = missingAuthoritativeStep4Sheet
            ? $"{cutSqm:0.##} sqm · MISSING AUTHORITATIVE STEP 4 SHEET SIZE for {pane.Side} pane · not costed from Sheet Store"
            : unitRate > 0m
                ? fromStep6Setup
                    ? $"{cutSqm:0.##} sqm → {sheets} sheet(s) · SHEET QTY from Step 6 wastage setup · {source}"
                    : priceByRequiredArea
                        ? $"{cutSqm:0.##} + {wastagePercent:0.#}% = {needed:0.##} sqm · priced by required area · {source}"
                        : $"{cutSqm:0.##} + {wastagePercent:0.#}% = {needed:0.##} sqm → {sheets} sheet(s) · {source}"
                : "UNPRICED — not in Sheet Store and no Step 4 rate · not included in profit";

        return new PnlRow(
            "Glass", group.GroupNumber, specDisplay,
            $"{pane.Side} pane · {pane.ThicknessMm}mm {color} · {detail}",
            sheetW, sheetH, sheetSqm,
            quantity, unitRate, "SQM", amount,
            unitRate > 0m ? source : "unpriced");
    }

    private static string CostBasisLabel(bool costAtSellingPrice) =>
        costAtSellingPrice ? "sell price" : "purchase price";

    /// <summary>
    /// The U-insert cost the Price-Based margin KPI must use (WPF
    /// ComputePriceBasedUInsertCost): applicable U-insert quantity × the
    /// per-SQM rate, reusing the EXACT automation detection the rest of
    /// Step 9 uses — the spec must name "U-Insert", the metric is the DGU
    /// cavity area for DGU/DGU+LAM builds and SQM1 otherwise, and the rate
    /// resolves Step 4 seed → saved Step 6 row rate → template default
    /// (AED 10/m²). No separate pricing engine, no hardcoded result.
    /// </summary>
    private static decimal PriceBasedUInsertCost(Inputs inputs)
    {
        var uInsertRow = inputs.Charges.FirstOrDefault(c =>
            string.Equals(c.TemplateKey, "U_INSERT", StringComparison.OrdinalIgnoreCase));
        var templateDefault =
            ChargeTemplates.Find("U_INSERT")?.DefaultRate ?? 0m;

        decimal total = 0m;

        foreach (var group in inputs.Groups)
        {
            var metric = ChargeMetrics.ForGroup(group);

            // The automation rule for U-insert (the only suggestion this
            // KPI consumes): spec names the extra; DGU-likes bill cavity SQM.
            var spec = group.SpecName ?? "";
            if (spec.IndexOf("U-Insert", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var metricKey = group.EffectiveGlassType is "DGU" or "DGU+LAM"
                ? MetricKeys.DguCavity
                : MetricKeys.Sqm1;

            var qty = metric.GetMetricValue(metricKey);
            if (qty <= 0m)
            {
                continue;
            }

            var rate = group.UInsertPricePerSqm is { } seed && seed > 0m
                ? seed
                : uInsertRow is not null && uInsertRow.Rate > 0m
                    ? uInsertRow.Rate
                    : templateDefault;

            if (rate <= 0m)
            {
                continue;
            }

            total += Math.Round(qty * rate, 2);
        }

        return total;
    }

    /// <summary>The Step 6 pane-side label for a P&L pane side.</summary>
    private static string? WastageSideLabelForPane(string paneSide)
    {
        if (paneSide.Contains("Lam Inner", StringComparison.OrdinalIgnoreCase))
        {
            return "Lam Inner (W2/H2)";
        }

        if (paneSide.Contains("Lam Outer", StringComparison.OrdinalIgnoreCase))
        {
            return "Lam Outer (W2/H2)";
        }

        if (paneSide.Contains("Inner", StringComparison.OrdinalIgnoreCase))
        {
            return "Inner (W2/H2)";
        }

        if (paneSide.Contains("Outer", StringComparison.OrdinalIgnoreCase))
        {
            return "Outer (W1/H1)";
        }

        return null;
    }

    private static int ResolveStep6WastageSheetQuantity(
        Inputs inputs, int specNumber, string paneSide)
    {
        var sideLabel = WastageSideLabelForPane(paneSide);
        if (sideLabel is null)
        {
            return 0;
        }

        // The row's available sources, in the Step 6 build order, with the
        // same per-source SQM the wastage section shows.
        var allSources = inputs.Groups
            .SelectMany(ChargeMetrics.SourcesForGroup)
            .ToList();

        foreach (var row in inputs.Wastages.OrderByDescending(w => w.IsActive))
        {
            if (row.SheetQuantity <= 0)
            {
                continue;
            }

            var checkedSources = allSources
                .Where(s => row.CheckedSourceKeys.Contains(s.Key))
                .ToList();
            if (checkedSources.Count == 0)
            {
                continue;
            }

            var allocation = Step6SheetAllocation.Allocate(
                row.SheetQuantity, row.OptimizationPercent, checkedSources)
                .FirstOrDefault(a =>
                    a.SpecNumber == specNumber
                    && string.Equals(a.SideLabel, sideLabel, StringComparison.OrdinalIgnoreCase));

            if (allocation is not null)
            {
                return allocation.SheetQuantity;
            }
        }

        return 0;
    }

    // ════════════════════════════════════════════════════════════════
    //  Quote basis (QuoteBasisFor)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// The pre-margin cost basis for one specification — the number a
    /// quoted rate is measured against. Any missing input returns 0 (a
    /// silently unavailable basis, never an invented one).
    /// </summary>
    public static decimal QuoteBasisFor(
        SpecGroupInput group, bool isDgu, bool isLam, bool isDguLam,
        int spacerCavities, decimal yield)
    {
        spacerCavities = Math.Max(1, spacerCavities);
        var type = group.GlassType.Trim().ToUpperInvariant();

        if (isDguLam || type == "DGU+LAM")
        {
            var outer = group.OuterSheetPricePerSqm;
            var lamInner = group.InnerSheetPricePerSqm;
            var lamOuter = 0m; // the WPF reads the live panel; no handoff column
            var spacer = group.Step4SpacerPricePerSqm ?? 0m;
            var processing = (group.Step4CuttingCostPerSqm ?? 0m)
                             + (group.Step4TemperingCostPerSqm ?? 0m)
                             + (group.Step4InterlayerCostPerSqm ?? 0m);

            if (outer <= 0m || lamInner <= 0m || spacer <= 0m || yield <= 0m)
            {
                return 0m;
            }

            return Math.Round((outer + lamOuter + lamInner) / yield
                              + processing + spacer * spacerCavities, 2);
        }

        if (isLam || type == "LAM")
        {
            var outer = group.OuterSheetPricePerSqm;
            var inner = group.InnerSheetPricePerSqm;
            var processing = (group.Step4InterlayerCostPerSqm ?? 0m)
                             + (group.Step4CuttingCostPerSqm ?? 0m)
                             + (group.Step4TemperingCostPerSqm ?? 0m);

            if (outer <= 0m || inner <= 0m || yield <= 0m)
            {
                return 0m;
            }

            return Math.Round((outer + inner) / yield + processing, 2);
        }

        if (isDgu || type == "DGU")
        {
            var outer = group.OuterSheetPricePerSqm;
            var inner = group.InnerSheetPricePerSqm;
            var spacer = group.Step4SpacerPricePerSqm ?? 0m;

            if (outer <= 0m || inner <= 0m || spacer <= 0m || yield <= 0m)
            {
                return 0m;
            }

            return Math.Round((outer + inner) / yield + spacer * spacerCavities, 2);
        }

        if (type is "SGU" or "" || type.Contains("SINGLE"))
        {
            var outer = group.OuterSheetPricePerSqm;
            var processing = (group.Step4CuttingCostPerSqm ?? 0m)
                             + (group.Step4TemperingCostPerSqm ?? 0m);

            if (outer <= 0m || yield <= 0m)
            {
                return 0m;
            }

            return Math.Round(outer / yield + processing, 2);
        }

        return 0m;
    }

    // ════════════════════════════════════════════════════════════════
    //  Step 6 charges as production costs (AppendStep6Costs)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Argon / fan holes / cutouts / holes are commercial add-ons that
    /// belong in the quoted specification — not standalone P&L cost lines.
    /// </summary>
    private static bool IsExcludedSupplementalPnLCharge(string? templateKey)
    {
        var key = (templateKey ?? "").Trim().ToUpperInvariant();
        return key is "ARGON_GAS" or "FAN_HOLES" or "CUTOUT" or "HOLES" or "COUNTERSUNK_HOLES";
    }

    private static string BuildChargeKind(string? label)
    {
        var text = (label ?? "Charge").Trim();
        var unitAt = text.IndexOf(" (", StringComparison.Ordinal);
        if (unitAt > 0)
        {
            text = text[..unitAt].Trim();
        }

        return string.IsNullOrWhiteSpace(text) ? "Charge" : text;
    }

    private static void AppendStep6Costs(
        Inputs inputs,
        List<PnlRow> rows,
        ref decimal productionCost,
        ref decimal uInsertInProduction)
    {
        foreach (var charge in inputs.Charges.Where(c => c.IsActive))
        {
            if (IsExcludedSupplementalPnLCharge(charge.TemplateKey))
            {
                continue;
            }

            // Delivery is per-invoice: one TRIP row.
            if (string.Equals(charge.TemplateKey, ChargeTemplates.DeliveryKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                AppendInvoiceWideCharge(rows, ref productionCost,
                    charge.ChargeName, 1m, charge.Rate, "TRIP");
                continue;
            }

            var isUInsert = string.Equals(
                charge.TemplateKey, "U_INSERT", StringComparison.OrdinalIgnoreCase);
            var isPolish = charge.PolishSubType.Length > 0
                           && charge.PolishSubType != PolishSubTypes.Custom;

            foreach (var spec in charge.CheckedSpecNumbers)
            {
                var group = inputs.Groups.FirstOrDefault(g => g.GroupNumber == spec);
                if (group is null)
                {
                    continue;
                }

                decimal qty;
                if (isPolish)
                {
                    qty = ChargeMetrics.PolishLm(
                        inputs.Groups, new[] { spec }, charge.PolishSubType,
                        charge.IsMatchedLamPolishRow);
                }
                else
                {
                    qty = ChargeMetrics.TargetedMetric(
                        inputs.Groups, new[] { spec }, charge.MetricKey);
                }

                if (qty <= 0m)
                {
                    continue;
                }

                var amount = Math.Round(qty * charge.Rate, 2);
                var kind = BuildChargeKind(charge.ChargeName);
                // WPF: the ROW's unit drives the label (a polish row is LM
                // even though its metric key is empty).
                var unit = (charge.Unit ?? "").Trim().ToUpperInvariant();

                rows.Add(new PnlRow(
                    kind, spec, SpecNameOrFallback(group, spec),
                    $"{kind} · {qty:0.###} {unit}",
                    0, 0, 0m, Math.Round(qty, 3), charge.Rate, unit, amount, ""));
                productionCost += amount;

                // The price-based KPI re-adds U-insert at its resolved rate,
                // so the amount inside production cost is remembered.
                if (isUInsert)
                {
                    uInsertInProduction += amount;
                }
            }
        }

        foreach (var custom in inputs.Customs.Where(c => c.IsActive))
        {
            AppendInvoiceWideCharge(rows, ref productionCost,
                string.IsNullOrWhiteSpace(custom.ChargeName) ? "Custom charge" : custom.ChargeName,
                custom.Qty, custom.Rate, custom.Unit.ToUpperInvariant());
        }

        // Step 6 wastage is a quotation/support setup only — it NEVER
        // creates P&L cost rows (2026-09-07 business rule).
    }

    private static void AppendInvoiceWideCharge(
        List<PnlRow> rows, ref decimal productionCost,
        string label, decimal qty, decimal rate, string unit)
    {
        if (qty <= 0m || rate <= 0m)
        {
            return;
        }

        var amount = Math.Round(qty * rate, 2);
        var kind = BuildChargeKind(label);
        rows.Add(new PnlRow(
            kind, 0, "Invoice-wide other charges",
            $"{kind} · {qty:0.###} {unit}",
            0, 0, 0m, Math.Round(qty, 3), rate, unit, amount, ""));
        productionCost += amount;
    }

    // ════════════════════════════════════════════════════════════════
    //  Shared Step 6 sheet allocation (Step6SheetAllocation)
    // ════════════════════════════════════════════════════════════════

    public sealed record SheetAllocation(
        int SpecNumber, string SideLabel, int SheetQuantity);

    /// <summary>
    /// Splits one wastage setup row's sheet count across its checked pane
    /// sources — weighted by useful sheet area (source SQM ÷ (sheet SQM ×
    /// utilization)), floor + largest-remainder, so a shared 17-sheet setup
    /// splits back to e.g. 16 + 1 instead of 17 on every spec.
    /// </summary>
    public static class Step6SheetAllocation
    {
        public static IReadOnlyList<SheetAllocation> Allocate(
            int sheetQuantity,
            decimal optimizationPercent,
            IReadOnlyList<ChargeMetrics.WastageSource> sources)
        {
            if (sheetQuantity <= 0 || sources.Count == 0)
            {
                return Array.Empty<SheetAllocation>();
            }

            var utilizationPercent = optimizationPercent > 0m
                ? optimizationPercent
                : ResolveActualUtilizationPercent(sheetQuantity, sources);
            var utilizationFactor = utilizationPercent / 100m;
            if (utilizationFactor <= 0m)
            {
                utilizationFactor = 1m;
            }

            var weighted = sources.Select(s =>
            {
                var sheetSqm = Math.Round(
                    s.SheetWidthMm * (decimal)s.SheetHeightMm / 1_000_000m, 4);
                if (sheetSqm <= 0m)
                {
                    return null;
                }

                var usefulSheetSqm = sheetSqm * utilizationFactor;
                var weight = usefulSheetSqm > 0m && s.SqmValue > 0m
                    ? s.SqmValue / usefulSheetSqm
                    : 1m;

                return (Source: s, Weight: weight, Allocated: 0, Remainder: 0m);
            }).Where(x => x != null).ToList()!;

            if (weighted.Count == 0)
            {
                return Array.Empty<SheetAllocation>();
            }

            // Tuples are immutable — do the math on parallel locals instead.
            var weights = weighted.Select(w => w.Weight).ToList();
            var allocated = weighted.Select(_ => 0).ToList();
            var remainders = weighted.Select(_ => 0m).ToList();

            var totalWeight = weights.Sum();
            if (totalWeight <= 0m)
            {
                for (var i = 0; i < weights.Count; i++)
                {
                    weights[i] = 1m;
                }

                totalWeight = weights.Count;
            }

            for (var i = 0; i < weighted.Count; i++)
            {
                var exact = sheetQuantity * (weights[i] / totalWeight);
                allocated[i] = (int)Math.Floor(exact);
                remainders[i] = exact - allocated[i];
            }

            var remainingSheets = sheetQuantity - allocated.Sum();

            var byRemainder = Enumerable.Range(0, weighted.Count)
                .OrderByDescending(i => remainders[i])
                .ThenByDescending(i => weights[i]);
            foreach (var i in byRemainder)
            {
                if (remainingSheets <= 0)
                {
                    break;
                }

                allocated[i]++;
                remainingSheets--;
            }

            return weighted
                .Select((w, i) => new SheetAllocation(
                    w.Source.SpecNumber, w.Source.SideLabel, allocated[i]))
                .Where(a => a.SheetQuantity > 0)
                .ToList();
        }

        private static decimal ResolveActualUtilizationPercent(
            int sheetQuantity, IReadOnlyList<ChargeMetrics.WastageSource> sources)
        {
            var valid = sources
                .Where(s => s.SheetWidthMm > 0 && s.SheetHeightMm > 0)
                .ToList();
            if (valid.Count == 0)
            {
                return 100m;
            }

            var totalSourceSqm = valid.Sum(s => Math.Max(0m, s.SqmValue));
            if (totalSourceSqm <= 0m)
            {
                return 100m;
            }

            var sheetAreas = valid
                .Select(s => Math.Round(
                    s.SheetWidthMm * (decimal)s.SheetHeightMm / 1_000_000m, 4))
                .Where(a => a > 0m)
                .Distinct()
                .ToList();

            // One glass type / one stock sheet shared across specs: infer
            // utilization from the bought sheet area — capped at 100% (a
            // shortfall is not super-efficiency).
            if (sheetAreas.Count == 1)
            {
                var boughtArea = sheetQuantity * sheetAreas[0];
                if (boughtArea > 0m)
                {
                    return Math.Round(Math.Min(100m, totalSourceSqm / boughtArea * 100m), 2);
                }
            }

            return 100m;
        }
    }
}
