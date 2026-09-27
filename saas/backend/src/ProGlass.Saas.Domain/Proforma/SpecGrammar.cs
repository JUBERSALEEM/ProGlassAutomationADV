// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   ViewModels/ProformaInvoice/Step9ProfitLoss/PnLSpecParser.cs —
//     the spec-name grammar (single source of truth: GlassPattern /
//     SpacerPattern / InterlayerPattern, ResolveTwoPanes, ResolveAllPanes)
//   Services/ProformaInvoice/OtherChargeAutomationRules.cs —
//     OtherChargeAutomationService.ResolveEffectiveGlassType
//   Models/ProformaInvoice/Step6OtherCharges/LaminateGlassMatcher.cs —
//     IsMatchedSameGlass / ColorsMatch / ExtractColorsFromSpecName
// The WPF files are the source of truth; this port changes no behaviour.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.RegularExpressions;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>One glass pane recovered from a specification name.</summary>
public sealed record SpecPane(
    string Side,
    int ThicknessMm,
    string Color,
    string Treatment = "")
{
    /// <summary>
    /// True when the pane needs a furnace pass (FT / Heat Strengthened /
    /// Heat Soaked). Annealed and unknown panes skip tempering.
    /// </summary>
    public bool RequiresTempering =>
        string.Equals(Treatment, "FT", StringComparison.OrdinalIgnoreCase)
        || Treatment.StartsWith("Heat", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Parses a Step 4 / Step 5 specification name into panes, spacer and
/// interlayer. Pure and deterministic — no store, no IO.
/// </summary>
public static class SpecGrammar
{
    private const string TreatmentAlternation =
        @"(?<treat>FT|Annealed|Heat\s+Strengthened|Heat\s+Soaked)";

    /// <summary>
    /// One glass pane, e.g. "6mm HD Grey FT Glass". The colour class is a
    /// TEMPERED token: it excludes '+' (a match can never cross into the
    /// spacer layer) and refuses the fragment "\d+mm" (a colour never
    /// swallows the next pane's thickness). Digits DO belong to brand
    /// names — "Kibbing Nova 55", "Cool Lite 365" must match.
    /// </summary>
    public static readonly Regex GlassPattern = new(
        @"(?<thk>\d+)\s*mm\s+(?<color>(?:(?!\d+\s*mm\b)[^+])+?)\s+" + TreatmentAlternation + @"\s+Glass",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Air spacer — "+ 12mm Black Warm Edge ASP" or "+ 12mm ASP".</summary>
    public static readonly Regex SpacerPattern = new(
        @"\+\s*(?<mm>\d+)\s*mm\s+(?:[^+\d]*?\s+)?ASP\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Laminated interlayer — PVB, SGP or EVA.</summary>
    public static readonly Regex InterlayerPattern = new(
        @"\+\s*(?<mm>[\d\.]+)\s*mm\s+(?<color>[^+\d]+?)\s+(?<type>PVB|SGP|EVA)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<Match> GlassMatches(string? specName) =>
        GlassPattern.Matches(specName ?? "").ToList();

    public static Match Spacer(string? specName) =>
        SpacerPattern.Match(specName ?? "");

    public static Match Interlayer(string? specName) =>
        InterlayerPattern.Match(specName ?? "");

    /// <summary>
    /// Number of air-spacer cavities declared in the spec name. Every
    /// cavity pays the spacer field (owner-confirmed 2026-09-05): a
    /// triple-glazed unit carries TWO ASP charges.
    /// </summary>
    public static int SpacerCount(string? specName) =>
        specName == null ? 0 : SpacerPattern.Matches(specName).Count;

    /// <summary>
    /// Outer = first glass match, Inner = LAST glass match (with ≥2 panes
    /// the inner pane is the last one; a 3+ pane build's middle pane must
    /// not be silently relabelled "Inner").
    /// </summary>
    public static (SpecPane? Outer, SpecPane? Inner) ResolveTwoPanes(string? specName)
    {
        var matches = GlassMatches(specName);
        if (matches.Count == 0)
        {
            return (null, null);
        }

        var outer = FromMatch(matches[0], "Outer");
        var inner = matches.Count >= 2 ? FromMatch(matches[^1], "Inner") : null;
        return (outer, inner);
    }

    /// <summary>
    /// Full ordered pane list for 3+ pane builds. Panes between the first
    /// and last are labelled "Middle" (1-based when several).
    /// </summary>
    public static IReadOnlyList<SpecPane> ResolveAllPanes(string? specName)
    {
        var matches = GlassMatches(specName);
        if (matches.Count == 0)
        {
            return Array.Empty<SpecPane>();
        }

        var panes = new List<SpecPane>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var side = i == 0
                ? "Outer"
                : i == matches.Count - 1
                    ? "Inner"
                    : matches.Count == 3 ? "Middle" : $"Middle {i}";
            panes.Add(FromMatch(matches[i], side));
        }

        return panes;
    }

    private static SpecPane FromMatch(Match match, string side) => new(
        side,
        int.TryParse(match.Groups["thk"].Value, out var thk) ? thk : 0,
        (match.Groups["color"].Value ?? "").Trim(),
        NormalizeTreatment(match.Groups["treat"].Value));

    /// <summary>Collapses inner whitespace, keeps original casing.</summary>
    public static string NormalizeTreatment(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var parts = raw.Trim().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    // ════════════════════════════════════════════════════════════════
    //  Effective build (OtherChargeAutomationRules)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// The effective build of a specification. Spec-name markers win
    /// (spacer → DGU, interlayer → LAM, both → DGU+LAM); when the spec
    /// text is ambiguous the glass-type field is authoritative; "SGU" is
    /// the final fallback. The Step 4 panel is the authoritative source
    /// of the build — a stale dropdown value must not downgrade a DGU.
    /// </summary>
    public static string ResolveEffectiveGlassType(string? glassType, string? specName)
    {
        var spec = specName ?? string.Empty;
        var hasSpacer = Spacer(spec).Success;
        var hasInterlayer = Interlayer(spec).Success;
        var hasGlass = GlassMatches(spec).Count > 0;

        if (hasSpacer && hasInterlayer)
        {
            return "DGU+LAM";
        }

        if (hasSpacer)
        {
            return "DGU";
        }

        if (hasInterlayer)
        {
            return "LAM";
        }

        var type = (glassType ?? "").Trim().ToUpperInvariant();
        if (type.Length > 0)
        {
            return type;
        }

        return hasGlass ? "SGU" : type;
    }

    // ════════════════════════════════════════════════════════════════
    //  Same-glass laminate matching (LaminateGlassMatcher)
    // ════════════════════════════════════════════════════════════════

    private static readonly string[] InterlayerTokens =
    {
        "PVB", "EVA", "SGP", "ASP", "AIR", "SPACER", "ARGON", "KRYPTON"
    };

    private static readonly string[] KnownColors =
    {
        "Reflective Grey", "Ford Blue", "Dark Grey", "HD Grey",
        "Milky White", "Clear", "Bronze", "Green", "Blue",
        "Silver", "Grey", "White", "Black", "Frosted"
    };

    public static bool IsLaminateType(string? glassType)
    {
        if (string.IsNullOrWhiteSpace(glassType))
        {
            return false;
        }

        return glassType.Equals("LAM", StringComparison.OrdinalIgnoreCase)
            || glassType.Equals("DGU+LAM", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ColorsMatch(string? outerColor, string? innerColor)
    {
        var a = NormalizeColor(outerColor);
        var b = NormalizeColor(innerColor);
        return a.Length > 0 && b.Length > 0
            && string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>
    /// Same-glass laminate: outer colour == inner colour (Clear + Clear)
    /// → the 2× polish row. Different colours stay on regular polish.
    /// Explicit colours win; otherwise they are parsed from the name.
    /// </summary>
    public static bool IsMatchedSameGlass(
        string? specName, string? glassType,
        string? outerColor = null, string? innerColor = null)
    {
        if (!IsLaminateType(glassType))
        {
            return false;
        }

        if (ColorsMatch(outerColor, innerColor))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(outerColor)
            || !string.IsNullOrWhiteSpace(innerColor))
        {
            return false;
        }

        ExtractColorsFromSpecName(specName, glassType, out var parsedOuter, out var parsedInner);
        return ColorsMatch(parsedOuter, parsedInner);
    }

    public static void ExtractColorsFromSpecName(
        string? specName, string? glassType,
        out string outerColor, out string innerColor)
    {
        outerColor = "";
        innerColor = "";

        var glasses = ExtractGlassLayers(specName);
        if (glasses.Count < 2)
        {
            return;
        }

        var dguLam = !string.IsNullOrWhiteSpace(glassType)
            && glassType.Equals("DGU+LAM", StringComparison.OrdinalIgnoreCase);

        // DGU+LAM: last two glasses are the laminated pair.
        // LAM: first and last glass are the pair.
        if (dguLam && glasses.Count >= 2)
        {
            outerColor = ExtractColorToken(glasses[glasses.Count - 2]);
            innerColor = ExtractColorToken(glasses[glasses.Count - 1]);
        }
        else
        {
            outerColor = ExtractColorToken(glasses[0]);
            innerColor = ExtractColorToken(glasses[^1]);
        }
    }

    // WPF verbatim: lowercase normalisation, known-colour scan in
    // declaration order, and layer splitting that only skips the
    // interlayer/spacer token layers.
    private static string NormalizeColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? "" : color.Trim().ToLowerInvariant();

    private static List<string> ExtractGlassLayers(string? specName)
    {
        var layers = new List<string>();
        if (string.IsNullOrWhiteSpace(specName))
        {
            return layers;
        }

        foreach (var raw in specName.Split('+'))
        {
            var layer = raw.Trim();
            if (layer.Length == 0)
            {
                continue;
            }

            if (IsInterlayerToken(layer))
            {
                continue;
            }

            layers.Add(layer);
        }

        return layers;
    }

    private static bool IsInterlayerToken(string layer)
    {
        foreach (var token in InterlayerTokens)
        {
            if (layer.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractColorToken(string? layer)
    {
        if (string.IsNullOrWhiteSpace(layer))
        {
            return "";
        }

        foreach (var color in KnownColors)
        {
            if (layer.IndexOf(color, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return color;
            }
        }

        return "";
    }
}
