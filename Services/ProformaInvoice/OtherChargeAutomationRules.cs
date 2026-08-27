using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.Services.ProformaInvoice
{
    /// <summary>
    /// One predefined-charge activation inferred from the Step 4 / Step 5
    /// specification. Pure business mapping only — no WPF, no repositories.
    /// </summary>
    public sealed class AutoChargeSuggestion
    {
        public string TemplateKey { get; init; } = "";
        public string MetricKey { get; init; } = "";
        public PolishSubType? PolishSubType { get; init; }
        public string Reason { get; init; } = "";
    }

    /// <summary>
    /// One auto-wastage target: a Step 5 spec-group side whose SQM should be
    /// preselected into Step 6's wastage grid in Advanced mode.
    /// </summary>
    public sealed class AutoWastageSuggestion
    {
        public int SpecGroupNumber { get; init; }
        public string SourceSideLabel { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Reason { get; init; } = "";
    }

    /// <summary>
    /// Pure rule engine for Advanced Step 6 automation. It reads a spec name
    /// and glass type and decides which predefined Step 6 rows should turn on.
    /// </summary>
    public static class OtherChargeAutomationRules
    {
        public static IReadOnlyList<AutoChargeSuggestion> GetChargeSuggestions(
            string? glassType,
            string? specName,
            bool matchedSameGlass)
        {
            string spec = specName ?? "";
            string type = ResolveEffectiveGlassType(glassType, spec);
            bool doubleGlass = IsDoubleGlass(type);
            bool dguLike = type is "DGU" or "DGU+LAM";

            var suggestions = new Dictionary<string, AutoChargeSuggestion>(
                StringComparer.OrdinalIgnoreCase);

            void Add(string templateKey, string metricKey, string reason,
                PolishSubType? polish = null)
            {
                suggestions[templateKey] = new AutoChargeSuggestion
                {
                    TemplateKey = templateKey,
                    MetricKey = metricKey,
                    Reason = reason,
                    PolishSubType = polish
                };
            }

            if (Contains(spec, "U-Insert"))
                Add("U_INSERT", type is "DGU" or "DGU+LAM" ? "DGU_CAVITY" : "SQM1",
                    "Spec requires U-Insert.");

            if (Contains(spec, "Overlap"))
            {
                bool specialOverlapSqm = Contains(spec, "Overlap (SQM)");
                bool explicitSiliconSqm = Contains(spec, "Silicon Bed (SQM)");
                bool explicitSiliconLm = Contains(spec, "Silicon Bed (LM)");

                if (specialOverlapSqm)
                {
                    Add("OVERLAP_SQM", "SQM1", "Special overlap is charged by area (SQM).");
                    if (dguLike && explicitSiliconSqm)
                    {
                        Add("SILICON_BED_SQM", "SQM1",
                            "Special overlap also charges silicon bed by area (SQM).");
                    }
                }
                else
                {
                    Add("OVERLAP", "LM1", "Normal overlap is charged by perimeter (LM).");
                    if (dguLike && (explicitSiliconLm || !Contains(spec, "Silicon Bed")))
                    {
                        Add("SILICON_BED", "LM1",
                            "Normal DGU overlap also charges silicon bed by perimeter (LM).");
                    }
                }
            }

            if (Contains(spec, "Argon Gas"))
                Add("ARGON_GAS", "DGU_CAVITY", "Spec requires argon gas.");

            if (Contains(spec, "Shutter Bonding"))
                Add("SHUTTER_BONDING", "LM_COMBINED", "Spec requires shutter bonding.");

            if (Contains(spec, "Fanhole"))
                Add("FAN_HOLES", "QTY", "Spec requires fan holes.");

            if (ContainsAny(spec,
                    "Cutout And Holes",
                    "Cutouts And Holes",
                    "Holes And Cutout",
                    "Holes And Cutouts"))
            {
                Add("CUTOUT", "QTY", "Spec requires cutouts.");
                Add("HOLES", "QTY", "Spec requires holes.");
            }
            else
            {
                if (ContainsAny(spec, "Cutout", "Cutouts"))
                    Add("CUTOUT", "QTY", "Spec requires cutouts.");
                if (Contains(spec, "Holes"))
                    Add("HOLES", "QTY", "Spec requires holes.");
            }

            if (Contains(spec, "Mitring"))
                Add("MITRING", "LM1", "Spec requires mitring.");

            if (Contains(spec, "Sandblasted"))
                Add("SANDBLASTING", "SQM1", "Spec requires sandblasting.");

            var polishType = DetectPolish(spec);
            if (polishType.HasValue)
            {
                string template = matchedSameGlass && doubleGlass
                    ? "POLISH_MATCHED_LAM"
                    : "POLISH";
                Add(template,
                    doubleGlass ? "LM_COMBINED" : "LM1",
                    matchedSameGlass && doubleGlass
                        ? "Matched multi-pane build uses the 2× polish row."
                        : "Spec requires polish.",
                    polishType.Value);
            }

            return suggestions.Values.ToList();
        }

        public static IReadOnlyList<AutoWastageSuggestion> GetWastageSuggestions(
            int specGroupNumber,
            string? glassType,
            string? specName,
            string? outerColorFallback,
            string? innerColorFallback)
        {
            string spec = specName ?? "";
            string type = ResolveEffectiveGlassType(glassType, spec);
            bool doubleGlass = IsDoubleGlass(type);

            var glassMatches = PnLSpecParser.GlassMatches(spec);
            string outerColor = glassMatches.Count > 0
                ? (glassMatches[0].Groups["color"].Value ?? "").Trim()
                : (outerColorFallback ?? "").Trim();
            string innerColor = glassMatches.Count > 1
                ? (glassMatches[^1].Groups["color"].Value ?? "").Trim()
                : (innerColorFallback ?? "").Trim();

            var rows = new List<AutoWastageSuggestion>();

            if (type == "DGU+LAM")
            {
                string lamOuterColor = glassMatches.Count > 1
                    ? (glassMatches[1].Groups["color"].Value ?? "").Trim()
                    : (outerColorFallback ?? "").Trim();
                string lamInnerColor = glassMatches.Count > 2
                    ? (glassMatches[2].Groups["color"].Value ?? "").Trim()
                    : (innerColorFallback ?? "").Trim();

                if (string.IsNullOrWhiteSpace(outerColor)
                    && string.IsNullOrWhiteSpace(lamOuterColor)
                    && string.IsNullOrWhiteSpace(lamInnerColor)
                    && glassMatches.Count == 0)
                    return rows;

                if (!string.IsNullOrWhiteSpace(outerColor) || glassMatches.Count > 0)
                {
                    rows.Add(new AutoWastageSuggestion
                    {
                        SpecGroupNumber = specGroupNumber,
                        SourceSideLabel = "Outer (W1/H1)",
                        DisplayName = string.IsNullOrWhiteSpace(outerColor)
                            ? "Outer glass wastage"
                            : $"{outerColor} — outer glass wastage",
                        Reason = "Primary outer pane wastage."
                    });
                }

                if (!string.IsNullOrWhiteSpace(lamOuterColor) || glassMatches.Count > 1)
                {
                    rows.Add(new AutoWastageSuggestion
                    {
                        SpecGroupNumber = specGroupNumber,
                        SourceSideLabel = "Lam Outer (W2/H2)",
                        DisplayName = string.IsNullOrWhiteSpace(lamOuterColor)
                            ? "Lam outer glass wastage"
                            : $"{lamOuterColor} — lam outer glass wastage",
                        Reason = "Laminated outer pane wastage."
                    });
                }

                if (!string.IsNullOrWhiteSpace(lamInnerColor) || glassMatches.Count > 2)
                {
                    rows.Add(new AutoWastageSuggestion
                    {
                        SpecGroupNumber = specGroupNumber,
                        SourceSideLabel = "Lam Inner (W2/H2)",
                        DisplayName = string.IsNullOrWhiteSpace(lamInnerColor)
                            ? "Lam inner glass wastage"
                            : $"{lamInnerColor} — lam inner glass wastage",
                        Reason = "Laminated inner pane wastage."
                    });
                }

                return rows;
            }

            if (string.IsNullOrWhiteSpace(specName)
                && string.IsNullOrWhiteSpace(outerColor)
                && string.IsNullOrWhiteSpace(innerColor)
                && glassMatches.Count == 0)
                return rows;

            rows.Add(new AutoWastageSuggestion
            {
                SpecGroupNumber = specGroupNumber,
                SourceSideLabel = "Outer (W1/H1)",
                DisplayName = string.IsNullOrWhiteSpace(outerColor)
                    ? "Outer glass wastage"
                    : $"{outerColor} — outer glass wastage",
                Reason = "Primary pane wastage."
            });

            if (doubleGlass)
            {
                rows.Add(new AutoWastageSuggestion
                {
                    SpecGroupNumber = specGroupNumber,
                    SourceSideLabel = "Inner (W2/H2)",
                    DisplayName = string.IsNullOrWhiteSpace(innerColor)
                        ? "Inner glass wastage"
                        : $"{innerColor} — inner glass wastage",
                    Reason = "Second pane wastage."
                });
            }

            return rows;
        }

        public static PolishSubType? DetectPolish(string? specName)
        {
            string spec = specName ?? "";

            if (Contains(spec, "All Side Polish As Per DWG")) return PolishSubType.AllSidePolishAsPerDwg;
            if (Contains(spec, "All Side Polish")) return PolishSubType.AllSidePolish;
            if (Contains(spec, "Polished Edges")) return PolishSubType.AllSidePolish;

            if (Contains(spec, "Both Height Side Polish As Per DWG")) return PolishSubType.BothHeightSideAsPerDwg;
            if (Contains(spec, "Both Height Side Polish")) return PolishSubType.BothHeightSide;

            if (Contains(spec, "Both Width Side Polish As Per DWG")) return PolishSubType.BothWidthSideAsPerDwg;
            if (Contains(spec, "Both Width Side Polish")) return PolishSubType.BothWidthSide;

            if (Contains(spec, "Both Height + One Width Side Polish As Per DWG")) return PolishSubType.BothHeightOneWidthAsPerDwg;
            if (Contains(spec, "Both Height + One Width Side Polish")) return PolishSubType.BothHeightOneWidth;

            if (Contains(spec, "One Height + One Width Side Polish As Per DWG")) return PolishSubType.OneHeightOneWidthAsPerDwg;
            if (Contains(spec, "One Height + One Width Side Polish")) return PolishSubType.OneHeightOneWidth;

            if (Contains(spec, "One Height Side Polish")) return PolishSubType.OneHeightSide;
            if (Contains(spec, "One Width Side Polish")) return PolishSubType.OneWidthSide;

            return null;
        }

        private static bool IsDoubleGlass(string glassType) =>
            glassType is "DGU" or "LAM" or "DGU+LAM";

        private static string ResolveEffectiveGlassType(string? glassType, string? specName)
        {
            string spec = specName ?? string.Empty;
            bool hasSpacer = PnLSpecParser.Spacer(spec).Success;
            bool hasInterlayer = PnLSpecParser.Interlayer(spec).Success;
            bool hasGlass = PnLSpecParser.GlassMatches(spec).Count > 0;

            if (hasSpacer && hasInterlayer) return "DGU+LAM";
            if (hasSpacer) return "DGU";
            if (hasInterlayer) return "LAM";
            if (hasGlass) return "SGU";

            return NormalizeGlassType(glassType);
        }

        private static string NormalizeGlassType(string? glassType) =>
            (glassType ?? "").Trim().ToUpperInvariant();

        private static bool Contains(string specName, string token) =>
            specName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool ContainsAny(string specName, params string[] tokens) =>
            tokens.Any(t => Contains(specName, t));
    }
}
