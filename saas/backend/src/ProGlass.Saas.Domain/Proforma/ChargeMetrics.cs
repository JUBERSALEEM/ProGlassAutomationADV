// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/ProformaInvoice/Step6OtherCharges/
//     OtherChargeUnit.cs, MetricOption.cs, PolishSubType.cs,
//     PredefinedChargeTemplates.cs, SpecGroupMetric.cs, SpecCheckOption.cs
//   ViewModels/ProformaInvoice/Step6OtherCharges/
//     MetricDiscoveryHubViewModel.Refresh (per-spec metrics from footers),
//     OtherChargesViewModel.RecalculateTotal (the Step 6 subtotal)
//   ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/
//     WastagesSectionViewModel.SyncSourcesForRow (the pane-source model)
//   Services/ProformaInvoice/Export/ProformaChargeMetricsCalculator —
//     ComputePolishLm / ComputeWastageQty / CalcPolishSingle /
//     UsesOuterDimensions (the amount math the PDF/Excel exports share)
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;
using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Unit of measurement for an Other Charge line (WPF enum).</summary>
public enum OtherChargeUnit
{
    Sqm,
    Lm,
    Qty
}

/// <summary>
/// The metric registry (WPF MetricOption.All): which metric each charge
/// row can sum across its checked spec groups, filtered by unit.
/// </summary>
public static class MetricKeys
{
    public const string Lm1 = "LM1";
    public const string Lm2 = "LM2";
    public const string LmCombined = "LM_COMBINED";
    public const string Sqm1 = "SQM1";
    public const string Sqm2 = "SQM2";
    public const string SqmCombined = "SQM_COMBINED";
    public const string DguCavity = "DGU_CAVITY";
    public const string Qty = "QTY";
    public const string DoubleGlass = "DOUBLE_GLASS";

    public static readonly IReadOnlyList<string> All =
    [
        Lm1, Lm2, LmCombined,
        Sqm1, Sqm2, SqmCombined, DguCavity,
        Qty, DoubleGlass
    ];

    /// <summary>The unit a metric produces (WPF MetricOption.Unit).</summary>
    public static OtherChargeUnit UnitOf(string metricKey) => metricKey switch
    {
        Lm1 or Lm2 or LmCombined => OtherChargeUnit.Lm,
        Qty or DoubleGlass => OtherChargeUnit.Qty,
        _ => OtherChargeUnit.Sqm
    };

    /// <summary>
    /// Accepts legacy aliases (TOTAL_PIECES / TOTAL_QTY → QTY) — WPF
    /// MetricOption.NormalizeKey.
    /// </summary>
    public static string Normalize(string? metricKey)
    {
        if (string.IsNullOrWhiteSpace(metricKey))
        {
            return "";
        }

        return metricKey.Trim() switch
        {
            "TOTAL_PIECES" => Qty,
            "TOTAL_QTY" => Qty,
            _ => metricKey.Trim()
        };
    }
}

/// <summary>The polish sub-types (WPF PolishSubType enum).</summary>
public static class PolishSubTypes
{
    public const string AllSidePolish = "AllSidePolish";
    public const string BothHeightSide = "BothHeightSide";
    public const string BothWidthSide = "BothWidthSide";
    public const string AllSidePolishAsPerDwg = "AllSidePolishAsPerDwg";
    public const string BothHeightSideAsPerDwg = "BothHeightSideAsPerDwg";
    public const string BothWidthSideAsPerDwg = "BothWidthSideAsPerDwg";
    public const string BothHeightOneWidth = "BothHeightOneWidth";
    public const string BothHeightOneWidthAsPerDwg = "BothHeightOneWidthAsPerDwg";
    public const string OneHeightOneWidth = "OneHeightOneWidth";
    public const string OneHeightOneWidthAsPerDwg = "OneHeightOneWidthAsPerDwg";
    public const string OneHeightSide = "OneHeightSide";
    public const string OneWidthSide = "OneWidthSide";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<string> All =
    [
        AllSidePolish, BothHeightSide, BothWidthSide,
        AllSidePolishAsPerDwg, BothHeightSideAsPerDwg, BothWidthSideAsPerDwg,
        BothHeightOneWidth, BothHeightOneWidthAsPerDwg,
        OneHeightOneWidth, OneHeightOneWidthAsPerDwg,
        OneHeightSide, OneWidthSide, Custom
    ];

    /// <summary>
    /// One pane's polish LM (WPF ProformaChargeMetricsCalculator.
    /// CalcPolishSingle — AsPerDwg variants share their base formula).
    /// </summary>
    public static decimal CalcPolishSingle(int w, int h, string subType) => subType switch
    {
        AllSidePolish => ((decimal)w + h) / 1000m * 2m,
        AllSidePolishAsPerDwg => ((decimal)w + h) / 1000m * 2m,
        BothHeightSide => (decimal)h / 1000m * 2m,
        BothHeightSideAsPerDwg => (decimal)h / 1000m * 2m,
        BothWidthSide => (decimal)w / 1000m * 2m,
        BothWidthSideAsPerDwg => (decimal)w / 1000m * 2m,
        BothHeightOneWidth => ((decimal)h * 2m + w) / 1000m,
        BothHeightOneWidthAsPerDwg => ((decimal)h * 2m + w) / 1000m,
        OneHeightOneWidth => ((decimal)h + w) / 1000m,
        OneHeightOneWidthAsPerDwg => ((decimal)h + w) / 1000m,
        OneHeightSide => (decimal)h / 1000m,
        OneWidthSide => (decimal)w / 1000m,
        _ => 0m
    };
}

/// <summary>
/// The predefined Step 6 charge rows (WPF PredefinedChargeTemplates) —
/// identity + defaults only; every rate stays editable per invoice.
/// </summary>
public static class ChargeTemplates
{
    public sealed record Template(
        string Key, string ChargeName, string Description,
        OtherChargeUnit Unit, decimal DefaultRate, string DefaultSourceKey);

    public static readonly IReadOnlyList<Template> All =
    [
        new Template("ARGON_GAS", "Argon Gas (Sqm)",
            "Inert Argon gas infusion inside DGU cavity for thermal insulation.",
            OtherChargeUnit.Sqm, 45m, MetricKeys.Sqm1),
        new Template("SILICON_BED", "Silicon bed (lm)",
            "Secondary sealant structural silicone bed barrier for double glazing.",
            OtherChargeUnit.Lm, 6m, MetricKeys.Lm1),
        new Template("SILICON_BED_SQM", "Silicon bed (Sqm)",
            "Secondary sealant structural silicone bed barrier for special overlap jobs charged by area.",
            OtherChargeUnit.Sqm, 6m, MetricKeys.Sqm1),
        new Template("OVERLAP", "Overlap (lm)",
            "Stepped laminate offset or double glazed spacer overlap edge charged by perimeter.",
            OtherChargeUnit.Lm, 6m, MetricKeys.Lm1),
        new Template("OVERLAP_SQM", "Overlap (Sqm)",
            "Special overlap mode charged by glass area instead of perimeter.",
            OtherChargeUnit.Sqm, 6m, MetricKeys.Sqm1),
        new Template("SHUTTER_BONDING", "Shutter Bonding (lm)",
            "Structural bonding of glazing glass sheets into the window shutter.",
            OtherChargeUnit.Lm, 25m, MetricKeys.LmCombined),
        new Template("MITRING", "Mitring (lm)",
            "Beveled edge cutting (45 degrees) for custom joint assemblies.",
            OtherChargeUnit.Lm, 30m, MetricKeys.Lm1),
        new Template("POLISH", "Polish (lm)",
            "SGU, mixed laminate (HD Grey + Clear) or single-pane polish. Sub-type controls formula.",
            OtherChargeUnit.Lm, 6m, ""),
        new Template("POLISH_MATCHED_LAM", "Polish — same-glass laminate (2×)",
            "Auto 2× when both panes match (Clear + Clear LAM / DGU). Mixed glass stays on regular Polish.",
            OtherChargeUnit.Lm, 6m, ""),
        new Template("CUTOUT", "Cutout (Qty)",
            "Water-jet internal precision cutouts for hinges and structural patches.",
            OtherChargeUnit.Qty, 50m, MetricKeys.Qty),
        new Template("HOLES", "Holes (Qty)",
            "Structural glass hole drilling (8mm to 15mm) on CNC drillers.",
            OtherChargeUnit.Qty, 15m, MetricKeys.Qty),
        new Template("SANDBLASTING", "Sandblasting (Sqm)",
            "Surface abrasive frosting via silica sand blasting for decorative privacy.",
            OtherChargeUnit.Sqm, 45m, MetricKeys.Sqm1),
        new Template("EDGE_GRINDING", "Edge Grinding (lm)",
            "Rough edge grinding before polish — smooths raw glass cut edges.",
            OtherChargeUnit.Lm, 4m, MetricKeys.Lm1),
        new Template("COUNTERSUNK_HOLES", "Countersunk Holes (Qty)",
            "Countersunk hole drilling for flush-mounted screws (bathroom/shower fittings).",
            OtherChargeUnit.Qty, 25m, MetricKeys.Qty),
        new Template("FAN_HOLES", "Fan Holes (Qty)",
            "Large-diameter fan/vent holes for exhaust and ventilation.",
            OtherChargeUnit.Qty, 35m, MetricKeys.Qty),
        new Template("DELIVERY", "Delivery (per trip)",
            "Site delivery charge — flat rate per trip.",
            OtherChargeUnit.Qty, 150m, MetricKeys.Qty),
        new Template("U_INSERT", "U-Insert (Sqm)",
            "U-channel gasket/rubber insertion for structural glazing edge sealing. Rate editable per invoice.",
            OtherChargeUnit.Sqm, 10m, MetricKeys.Sqm1)
    ];

    public static Template? Find(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    public const string DeliveryKey = "DELIVERY";
    public const string PolishKey = "POLISH";
    public const string MatchedLamPolishKey = "POLISH_MATCHED_LAM";
}

/// <summary>
/// One Step 5 grid row, the shape the Step 6 math needs (WPF
/// GlassItemRowSnapshot).
/// </summary>
public sealed record GridRow(
    int W1, int H1, int W2, int H2, int Qty, int PerimeterFactor);

/// <summary>
/// One spec group with its grid rows and Step 4 provenance — everything
/// Step 6 derives from (WPF SpecGroup + the Phase G snapshot fields).
/// </summary>
public sealed record SpecGroupInput(
    int GroupNumber,
    string GlassType,
    string SpecName,
    string OuterGlassColor,
    string InnerGlassColor,
    IReadOnlyList<GridRow> Rows,
    int OuterSheetWidthMm = 0,
    int OuterSheetHeightMm = 0,
    int InnerSheetWidthMm = 0,
    int InnerSheetHeightMm = 0,
    int ThirdSheetWidthMm = 0,
    int ThirdSheetHeightMm = 0,
    decimal OuterSheetPricePerSqm = 0m,
    decimal InnerSheetPricePerSqm = 0m,
    decimal ThirdSheetPricePerSqm = 0m,
    // Step 4 production-cost provenance (Phase 1-AUDIT): the rates THIS
    // group was quoted with. Nullable — a legacy group reads unknown, not
    // zero-priced, and the P&L falls back the WPF way.
    decimal? Step4SpacerPricePerSqm = null,
    decimal? Step4InterlayerCostPerSqm = null,
    decimal? Step4CuttingCostPerSqm = null,
    decimal? Step4TemperingCostPerSqm = null,
    decimal? UInsertPricePerSqm = null)
{
    public string EffectiveGlassType =>
        SpecGrammar.ResolveEffectiveGlassType(GlassType, SpecName);

    /// <summary>WPF HasOuterSheet — a sheet was chosen in Step 4. The SaaS
    /// request carries sheet geometry only when Step 4 chose one, so the
    /// geometry itself is the flag (documented normalization).</summary>
    public bool HasOuterSheet => OuterSheetWidthMm > 0 && OuterSheetHeightMm > 0;

    public bool HasInnerSheet => InnerSheetWidthMm > 0 && InnerSheetHeightMm > 0;
}

/// <summary>
/// Per-spec metrics (WPF SpecGroupMetric, built by
/// MetricDiscoveryHubViewModel.Refresh from the grid footers): footer
/// totals at 3 dp, exposed to Step 6 rounded to 2 dp.
/// </summary>
public sealed record SpecGroupMetrics(
    int SpecGroupNumber,
    bool IsDoubleGlass,
    int PaneFactor,
    bool IsMatchedSameGlass,
    decimal Lm1,
    decimal Lm2,
    decimal Sqm1,
    decimal Sqm2,
    int Qty)
{
    public decimal LmCombined => Lm1 + Lm2;
    public decimal SqmCombined => Sqm1 + Sqm2;
    public int PhysicalPanes => Qty * (PaneFactor <= 0 ? 1 : PaneFactor);

    /// <summary>WPF SpecCheckOption.GetMetricValue.</summary>
    public decimal GetMetricValue(string metricKey) => metricKey switch
    {
        MetricKeys.Lm1 => Lm1,
        MetricKeys.Lm2 => Lm2,
        MetricKeys.LmCombined => LmCombined,
        MetricKeys.Sqm1 => Sqm1,
        MetricKeys.Sqm2 => Sqm2,
        MetricKeys.SqmCombined => SqmCombined,
        // The cavity area is the outer pane's area, and only a double
        // build has one.
        MetricKeys.DguCavity => IsDoubleGlass ? Sqm1 : 0m,
        MetricKeys.Qty => Qty,
        MetricKeys.DoubleGlass => PhysicalPanes,
        _ => 0m
    };
}

/// <summary>
/// Step 6 metric derivation, WPF-verbatim.
/// </summary>
public static class ChargeMetrics
{
    /// <summary>
    /// Per-spec metrics from one group's rows (the footer aggregation the
    /// WPF grid keeps live: Σ per-row math at 3 dp, then the hub's 2 dp
    /// read model).
    /// </summary>
    public static SpecGroupMetrics ForGroup(SpecGroupInput group)
    {
        var effectiveType = group.EffectiveGlassType;
        var isDoubleGlass = effectiveType is "DGU" or "LAM" or "DGU+LAM";
        var paneFactor = effectiveType == "DGU+LAM" ? 3 : isDoubleGlass ? 2 : 1;

        // Footer totals: Σ of each row's already-rounded values, rounded
        // to 3 dp (WPF SpecGroup.RecalculateFooter over non-phantom rows).
        decimal sqm1 = 0m, sqm2 = 0m, lm1 = 0m, lm2 = 0m;
        var qty = 0;
        foreach (var r in group.Rows)
        {
            if (r.Qty <= 0)
            {
                continue;
            }

            var math = SpecGroupRowMath.Calculate(
                r.W1, r.H1, r.W2, r.H2, r.Qty,
                r.PerimeterFactor <= 0 ? SpecGroupRowMath.DefaultPerimeterFactor : r.PerimeterFactor,
                0m);
            sqm1 += math.TotalSqm;
            sqm2 += math.TotalSqm2;
            lm1 += math.Lm1;
            lm2 += math.Lm2;
            qty += r.Qty;
        }

        return new SpecGroupMetrics(
            group.GroupNumber, isDoubleGlass, paneFactor,
            SpecGrammar.IsMatchedSameGlass(
                group.SpecName, effectiveType,
                group.OuterGlassColor, group.InnerGlassColor),
            lm1: Math.Round(Math.Round(lm1, 3), 2),
            lm2: Math.Round(Math.Round(lm2, 3), 2),
            sqm1: Math.Round(Math.Round(sqm1, 3), 2),
            sqm2: Math.Round(Math.Round(sqm2, 3), 2),
            qty: qty);
    }

    /// <summary>
    /// Σ of one metric across the CHECKED spec groups (WPF
    /// SpecTargetSelection.ComputedValue: sum of the per-spec 2 dp values,
    /// no further rounding).
    /// </summary>
    public static decimal TargetedMetric(
        IReadOnlyList<SpecGroupInput> groups,
        IReadOnlyCollection<int> checkedSpecNumbers,
        string metricKey)
    {
        var key = MetricKeys.Normalize(metricKey);
        if (key.Length == 0)
        {
            return 0m;
        }

        var allowed = checkedSpecNumbers.ToHashSet();
        decimal sum = 0m;
        foreach (var g in groups)
        {
            if (!allowed.Contains(g.GroupNumber))
            {
                continue;
            }

            sum += ForGroup(g).GetMetricValue(key);
        }

        return sum;
    }

    /// <summary>
    /// Polish LM across the checked specs for one sub-type (WPF
    /// ComputePolishLm): both panes of a double build, the 2× rule for a
    /// same-glass laminate that only carries W1/H1, × qty per row,
    /// 3 dp total.
    /// </summary>
    public static decimal PolishLm(
        IReadOnlyList<SpecGroupInput> groups,
        IReadOnlyCollection<int> checkedSpecNumbers,
        string polishSubType,
        bool matchedSameGlassOnly)
    {
        decimal totalLm = 0m;
        var allowed = checkedSpecNumbers.ToHashSet();

        foreach (var g in groups)
        {
            if (!allowed.Contains(g.GroupNumber))
            {
                continue;
            }

            var effectiveType = g.EffectiveGlassType;
            var matched = SpecGrammar.IsMatchedSameGlass(
                g.SpecName, effectiveType, g.OuterGlassColor, g.InnerGlassColor);
            if (matchedSameGlassOnly && !matched)
            {
                continue;
            }

            var isDoubleGlass = effectiveType is "DGU" or "LAM" or "DGU+LAM";

            foreach (var r in g.Rows)
            {
                if (r.Qty <= 0)
                {
                    continue;
                }

                var lm = 0m;
                if (r.W1 > 0 && r.H1 > 0)
                {
                    lm += PolishSubTypes.CalcPolishSingle(r.W1, r.H1, polishSubType);
                }

                if (isDoubleGlass && r.W2 > 0 && r.H2 > 0)
                {
                    lm += PolishSubTypes.CalcPolishSingle(r.W2, r.H2, polishSubType);
                }
                else if (matchedSameGlassOnly && matched && r.W1 > 0 && r.H1 > 0)
                {
                    // Same-glass LAM often only has W1/H1 filled — still
                    // polish both panes.
                    lm += PolishSubTypes.CalcPolishSingle(r.W1, r.H1, polishSubType);
                }

                totalLm += lm * r.Qty;
            }
        }

        return Math.Round(totalLm, 3);
    }

    // ════════════════════════════════════════════════════════════════
    //  Wastage pane sources (WastagesSectionViewModel.SyncSourcesForRow)
    // ════════════════════════════════════════════════════════════════

    public const string OuterSideLabel = "Outer (W1/H1)";
    public const string InnerSideLabel = "Inner (W2/H2)";
    public const string LamOuterSideLabel = "Lam Outer (W2/H2)";
    public const string LamInnerSideLabel = "Lam Inner (W2/H2)";

    public static string MiddleSideLabel(int index, int paneCount) =>
        paneCount == 3 ? "Middle (W2/H2)" : $"Middle {index} (W2/H2)";

    /// <summary>One selectable physical pane (WPF SpecGroupColorSide).</summary>
    public sealed record WastageSource(
        int SpecNumber,
        string SideLabel,
        string ColorName,
        decimal SqmValue,
        int SheetWidthMm,
        int SheetHeightMm,
        decimal PricePerSqm)
    {
        /// <summary>Stable persistence key "{spec}-{SideLabel}".</summary>
        public string Key => $"{SpecNumber}-{SideLabel}";
    }

    /// <summary>
    /// The pane sources of one group, in the WPF's fixed order. A double
    /// build's second pane is the same unit size as the first unless the
    /// grid carries an explicit W2/H2 (Σ SQM2 > 0). 3+ pane builds get a
    /// source per middle pane; DGU+LAM splits outer / lam outer / lam inner.
    /// </summary>
    public static IReadOnlyList<WastageSource> SourcesForGroup(SpecGroupInput group)
    {
        var effectiveType = group.EffectiveGlassType;
        var metrics = ForGroup(group);
        var innerSqm = metrics.Sqm2 > 0m ? metrics.Sqm2 : metrics.Sqm1;

        var sources = new List<WastageSource>();

        string OuterColor() => PaneColor(group, effectiveType, 0);
        string InnerColor() => PaneColor(group, effectiveType, -1);

        if (effectiveType == "DGU+LAM")
        {
            var panes = SpecGrammar.ResolveAllPanes(group.SpecName);
            sources.Add(new WastageSource(
                group.GroupNumber, OuterSideLabel, OuterColor(),
                metrics.Sqm1,
                group.OuterSheetWidthMm, group.OuterSheetHeightMm,
                group.OuterSheetPricePerSqm));
            sources.Add(new WastageSource(
                group.GroupNumber, LamOuterSideLabel,
                panes.Count > 1 ? panes[1].Color : group.OuterGlassColor,
                innerSqm,
                group.InnerSheetWidthMm, group.InnerSheetHeightMm,
                group.InnerSheetPricePerSqm));
            sources.Add(new WastageSource(
                group.GroupNumber, LamInnerSideLabel,
                panes.Count > 2 ? panes[2].Color : group.InnerGlassColor,
                innerSqm,
                group.InnerSheetWidthMm, group.InnerSheetHeightMm,
                group.InnerSheetPricePerSqm));
            return sources;
        }

        sources.Add(new WastageSource(
            group.GroupNumber, OuterSideLabel, OuterColor(),
            metrics.Sqm1,
            group.OuterSheetWidthMm, group.OuterSheetHeightMm,
            group.OuterSheetPricePerSqm));

        // A resolved double build offers the inner source; so does a grid
        // that physically carries a W2/H2 column even when the type field
        // is blank (manual mode must stay usable).
        if (effectiveType is "DGU" or "LAM" || metrics.Sqm2 > 0m)
        {
            var panes = SpecGrammar.ResolveAllPanes(group.SpecName);

            // FIX-2026-09-05 (3+ pane builds): every pane between the
            // first and last is a wastage side — sheet geometry mirrors
            // the inner pane.
            for (var m = 1; m < panes.Count - 1; m++)
            {
                sources.Add(new WastageSource(
                    group.GroupNumber, MiddleSideLabel(m, panes.Count),
                    panes[m].Color, innerSqm,
                    group.InnerSheetWidthMm, group.InnerSheetHeightMm,
                    group.InnerSheetPricePerSqm));
            }

            sources.Add(new WastageSource(
                group.GroupNumber, InnerSideLabel, InnerColor(),
                innerSqm,
                group.InnerSheetWidthMm, group.InnerSheetHeightMm,
                group.InnerSheetPricePerSqm));
        }

        return sources;
    }

    /// <summary>
    /// A pane's colour: the group's explicit colour wins; otherwise the
    /// colour token parsed from the spec name (WPF ResolveTwoPaneColors —
    /// parsed first, explicit second; both resolve to the same value in
    /// the SaaS, where the saved group colours are already the
    /// authoritative parse of record).
    /// </summary>
    private static string PaneColor(SpecGroupInput group, string effectiveType, int paneIndex)
    {
        var panes = SpecGrammar.ResolveAllPanes(group.SpecName);
        string parsed;
        if (paneIndex == -1)
        {
            parsed = panes.Count > 1 ? panes[^1].Color : "";
        }
        else
        {
            parsed = panes.Count > 0 ? panes[0].Color : "";
        }

        return FirstNonBlank(parsed,
            paneIndex == -1 ? group.InnerGlassColor : group.OuterGlassColor);
    }

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Σ SQM of the CHECKED sources (WPF RecalcFromSources:
    /// TotalSqm = Σ checked SqmValue — unrounded source values summed).
    /// </summary>
    public static decimal WastageSqm(
        IReadOnlyList<SpecGroupInput> groups,
        IReadOnlyCollection<string> checkedSourceKeys)
    {
        var keys = checkedSourceKeys.ToHashSet(StringComparer.Ordinal);
        decimal total = 0m;
        foreach (var g in groups)
        {
            foreach (var source in SourcesForGroup(g))
            {
                if (keys.Contains(source.Key))
                {
                    total += source.SqmValue;
                }
            }
        }

        return total;
    }

    // ════════════════════════════════════════════════════════════════
    //  Amounts (OtherChargeRow / CustomChargeRow / RecalculateTotal)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// A predefined/custom charge row's amount (WPF OtherChargeRow.
    /// Recalculate / CustomChargeRow.Recalculate): money-rounded
    /// qty × rate when active, else 0.
    /// </summary>
    public static decimal ChargeAmount(decimal metricQty, decimal rate, bool isActive) =>
        isActive ? MoneyRounding.Round(metricQty * rate) : 0m;

    /// <summary>
    /// The Step 6 subtotal (WPF OtherChargesViewModel.RecalculateTotal):
    /// Σ active predefined + Σ active wastage + Σ active custom, plain
    /// Math.Round(x, 2). The WPF deliberately uses the default
    /// (banker's) rounding here — preserved verbatim.
    /// </summary>
    public static decimal OtherChargesSubtotal(
        decimal predefinedSum, decimal wastageSum, decimal customSum) =>
        Math.Round(predefinedSum + wastageSum + customSum, 2);
}
