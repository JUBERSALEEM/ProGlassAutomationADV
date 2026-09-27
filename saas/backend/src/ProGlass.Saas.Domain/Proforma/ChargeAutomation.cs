// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Services/ProformaInvoice/OtherChargeAutomationRules.cs (the whole
//     file — OtherChargeAutomationService.GetChargeSuggestions /
//     GetWastageSuggestions / DetectPolish / IsClearGlass, the static
//     wrapper, the PnLSpecParser adapter)
// The WPF file is the source of truth; behaviour preserved verbatim.
// The SaaS SpecGrammar IS the parser adapter (GlassMatches / Spacer /
// Interlayer / ResolveEffectiveGlassType are already its ports).
//
// SCOPE (documented in WPF_TO_SAAS_MAPPING §1.3b): this is the DECISIONS
// half — which predefined charges / wastage sides a spec implies. The
// WPF consumed it live in the Step 6 Advanced toggle; the SaaS exposes
// it as a read model the web client applies to its rows. The P&L's
// AppendAdvancedSuggestedCharges (costing unchecked-but-present rows
// when UseAdvancedAutomation is on) stays deferred until that toggle is
// persisted.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>
/// One predefined-charge activation inferred from the Step 4 / Step 5
/// specification (WPF AutoChargeSuggestion).
/// </summary>
public sealed record AutoChargeSuggestion(
    string TemplateKey,
    string MetricKey,
    string? PolishSubType,
    string Reason);

/// <summary>
/// One auto-wastage target: a Step 5 spec-group side whose SQM should be
/// preselected into Step 6's wastage grid (WPF AutoWastageSuggestion).
/// </summary>
public sealed record AutoWastageSuggestion(
    int SpecGroupNumber,
    string SourceSideLabel,
    string ColorName,
    string DisplayName,
    string Reason);

/// <summary>
/// Advanced Step 6 automation rules — pure business mapping only (WPF
/// OtherChargeAutomationService). Reads a spec name and glass type and
/// decides which predefined Step 6 rows should turn on.
/// </summary>
public static class ChargeAutomation
{
    /// <summary>
    /// Which predefined charges this specification implies. The dict
    /// semantics (last Add wins per template key) are the WPF's.
    /// </summary>
    public static IReadOnlyList<AutoChargeSuggestion> GetChargeSuggestions(
        string? glassType, string? specName, bool matchedSameGlass)
    {
        var spec = specName ?? "";
        var type = SpecGrammar.ResolveEffectiveGlassType(glassType, spec);
        var doubleGlass = IsDoubleGlass(type);
        var dguLike = type is "DGU" or "DGU+LAM";

        var suggestions = new Dictionary<string, AutoChargeSuggestion>(
            StringComparer.OrdinalIgnoreCase);

        void Add(string templateKey, string metricKey, string reason,
            string? polish = null)
        {
            suggestions[templateKey] = new AutoChargeSuggestion(
                templateKey, metricKey, polish, reason);
        }

        if (Contains(spec, "U-Insert"))
        {
            Add("U_INSERT", type is "DGU" or "DGU+LAM" ? "DGU_CAVITY" : "SQM1",
                "Spec requires U-Insert.");
        }

        if (Contains(spec, "Overlap"))
        {
            var specialOverlapSqm = Contains(spec, "Overlap (SQM)");

            if (specialOverlapSqm)
            {
                // Overlap (SQM) mode: rare case for specific customers.
                // Only charge overlap (sqm), do NOT charge silicon bed.
                // The SQM pricing already includes all overlap-related costs.
                Add("OVERLAP_SQM", "SQM1",
                    "Spec requires overlap charged by area (SQM).");
            }
            else
            {
                // Standard case. For DGU builds, overlap ALWAYS requires
                // silicon bed as a separate charge — physically overlap +
                // silicon bed are two distinct processes that are always
                // charged separately.
                Add("OVERLAP", "LM1",
                    "Spec requires overlap charged by perimeter (LM).");

                if (dguLike)
                {
                    Add("SILICON_BED", "LM1",
                        "DGU overlap requires silicon bed as a separate charge (LM).");
                }
            }
        }

        if (Contains(spec, "Argon Gas"))
        {
            Add("ARGON_GAS", "DGU_CAVITY", "Spec requires argon gas.");
        }

        if (Contains(spec, "Shutter Bonding"))
        {
            Add("SHUTTER_BONDING", "LM_COMBINED", "Spec requires shutter bonding.");
        }

        if (Contains(spec, "Fanhole"))
        {
            Add("FAN_HOLES", "QTY", "Spec requires fan holes.");
        }

        if (ContainsAny(spec,
                "Cutout And Holes", "Cutouts And Holes",
                "Holes And Cutout", "Holes And Cutouts"))
        {
            Add("CUTOUT", "QTY", "Spec requires cutouts.");
            Add("HOLES", "QTY", "Spec requires holes.");
        }
        else
        {
            if (ContainsAny(spec, "Cutout", "Cutouts"))
            {
                Add("CUTOUT", "QTY", "Spec requires cutouts.");
            }

            if (Contains(spec, "Holes"))
            {
                Add("HOLES", "QTY", "Spec requires holes.");
            }
        }

        if (Contains(spec, "Mitring") || Contains(spec, "Beveled"))
        {
            Add("MITRING", "LM1", "Spec requires mitring/beveling.");
        }

        if (Contains(spec, "Sandblasted") || Contains(spec, "Frosted"))
        {
            Add("SANDBLASTING", "SQM1", "Spec requires sandblasting/frosting.");
        }

        var polishType = DetectPolish(spec);
        if (polishType is not null)
        {
            var template = matchedSameGlass && doubleGlass
                ? ChargeTemplates.MatchedLamPolishKey
                : ChargeTemplates.PolishKey;
            Add(template,
                doubleGlass ? MetricKeys.LmCombined : MetricKeys.Lm1,
                matchedSameGlass && doubleGlass
                    ? "Matched multi-pane build uses the 2× polish row."
                    : "Spec requires polish.",
                polishType);
        }

        return suggestions.Values.ToList();
    }

    /// <summary>
    /// Which pane sides of a specification should be preselected into the
    /// wastage grid. All panes are suggested, but CLEAR glass is marked
    /// optional (offcuts often reusable across orders).
    /// </summary>
    public static IReadOnlyList<AutoWastageSuggestion> GetWastageSuggestions(
        int specGroupNumber,
        string? glassType,
        string? specName,
        string? outerColorFallback,
        string? innerColorFallback)
    {
        var spec = specName ?? "";
        var type = SpecGrammar.ResolveEffectiveGlassType(glassType, spec);
        var doubleGlass = IsDoubleGlass(type);

        var glassMatches = SpecGrammar.GlassMatches(spec);
        var outerColor = glassMatches.Count > 0
            ? (glassMatches[0].Groups["color"].Value ?? "").Trim()
            : (outerColorFallback ?? "").Trim();
        var innerColor = glassMatches.Count > 1
            ? (glassMatches[^1].Groups["color"].Value ?? "").Trim()
            : (innerColorFallback ?? "").Trim();

        var rows = new List<AutoWastageSuggestion>();

        if (type == "DGU+LAM")
        {
            var lamOuterColor = glassMatches.Count > 1
                ? (glassMatches[1].Groups["color"].Value ?? "").Trim()
                : (outerColorFallback ?? "").Trim();
            var lamInnerColor = glassMatches.Count > 2
                ? (glassMatches[2].Groups["color"].Value ?? "").Trim()
                : (innerColorFallback ?? "").Trim();

            if (string.IsNullOrWhiteSpace(outerColor)
                && string.IsNullOrWhiteSpace(lamOuterColor)
                && string.IsNullOrWhiteSpace(lamInnerColor)
                && glassMatches.Count == 0)
            {
                return rows;
            }

            if (!string.IsNullOrWhiteSpace(outerColor) || glassMatches.Count > 0)
            {
                rows.Add(Wastage(specGroupNumber, "Outer (W1/H1)",
                    outerColor, "outer"));
            }

            if (!string.IsNullOrWhiteSpace(lamOuterColor) || glassMatches.Count > 1)
            {
                rows.Add(Wastage(specGroupNumber, "Lam Outer (W2/H2)",
                    lamOuterColor, "lam outer"));
            }

            if (!string.IsNullOrWhiteSpace(lamInnerColor) || glassMatches.Count > 2)
            {
                rows.Add(Wastage(specGroupNumber, "Lam Inner (W2/H2)",
                    lamInnerColor, "lam inner"));
            }

            return rows;
        }

        if (string.IsNullOrWhiteSpace(specName)
            && string.IsNullOrWhiteSpace(outerColor)
            && string.IsNullOrWhiteSpace(innerColor)
            && glassMatches.Count == 0)
        {
            return rows;
        }

        rows.Add(Wastage(specGroupNumber, "Outer (W1/H1)", outerColor, "outer"));

        // FIX-2026-09-05 (3+ pane builds): every pane between the first
        // and last is a wastage side — a triple unit's middle sheet must
        // not silently escape its wastage charge.
        if (doubleGlass && glassMatches.Count > 2)
        {
            for (var m = 1; m < glassMatches.Count - 1; m++)
            {
                var middleColor =
                    (glassMatches[m].Groups["color"].Value ?? "").Trim();
                var middleSide = glassMatches.Count == 3
                    ? "Middle (W2/H2)"
                    : $"Middle {m} (W2/H2)";

                rows.Add(Wastage(specGroupNumber, middleSide, middleColor,
                    middleSide.Replace(" (W2/H2)", "").ToLowerInvariant()));
            }
        }

        if (doubleGlass)
        {
            rows.Add(Wastage(specGroupNumber, "Inner (W2/H2)", innerColor, "inner"));
        }

        return rows;
    }

    private static AutoWastageSuggestion Wastage(
        int specGroupNumber, string sideLabel, string colorName, string sideWords) =>
        new(specGroupNumber, sideLabel, colorName,
            string.IsNullOrWhiteSpace(colorName)
                ? $"{char.ToUpperInvariant(sideWords[0])}{sideWords[1..]} glass wastage"
                : $"{colorName} — {sideWords} glass wastage",
            IsClearGlass(colorName)
                ? "Clear glass — optional (offcuts often reusable)."
                : "Colored/coated glass — offcuts not reusable.");

    /// <summary>
    /// Clear glass offcuts are often reusable across orders, so wastage
    /// charges for clear glass are optional.
    /// </summary>
    private static bool IsClearGlass(string? colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
        {
            return false;
        }

        return colorName.Trim().Equals("Clear", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The polish sub-type a spec name declares — the "As Per DWG"
    /// variants are checked BEFORE their base phrases so the longer
    /// label is never swallowed by the shorter one.
    /// </summary>
    public static string? DetectPolish(string? specName)
    {
        var spec = specName ?? "";

        if (Contains(spec, "All Side Polish As Per DWG")) return PolishSubTypes.AllSidePolishAsPerDwg;
        if (Contains(spec, "All Side Polish")) return PolishSubTypes.AllSidePolish;
        if (Contains(spec, "Polished Edges")) return PolishSubTypes.AllSidePolish;

        if (Contains(spec, "Both Height Side Polish As Per DWG")) return PolishSubTypes.BothHeightSideAsPerDwg;
        if (Contains(spec, "Both Height Side Polish")) return PolishSubTypes.BothHeightSide;

        if (Contains(spec, "Both Width Side Polish As Per DWG")) return PolishSubTypes.BothWidthSideAsPerDwg;
        if (Contains(spec, "Both Width Side Polish")) return PolishSubTypes.BothWidthSide;

        if (Contains(spec, "Both Height + One Width Side Polish As Per DWG")) return PolishSubTypes.BothHeightOneWidthAsPerDwg;
        if (Contains(spec, "Both Height + One Width Side Polish")) return PolishSubTypes.BothHeightOneWidth;

        if (Contains(spec, "One Height + One Width Side Polish As Per DWG")) return PolishSubTypes.OneHeightOneWidthAsPerDwg;
        if (Contains(spec, "One Height + One Width Side Polish")) return PolishSubTypes.OneHeightOneWidth;

        if (Contains(spec, "One Height Side Polish")) return PolishSubTypes.OneHeightSide;
        if (Contains(spec, "One Width Side Polish")) return PolishSubTypes.OneWidthSide;

        return null;
    }

    private static bool IsDoubleGlass(string glassType) =>
        glassType is "DGU" or "LAM" or "DGU+LAM";

    private static bool Contains(string specName, string token) =>
        specName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool ContainsAny(string specName, params string[] tokens) =>
        tokens.Any(t => Contains(specName, t));
}
