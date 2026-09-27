// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/
//     DguParametersViewModel.cs L454–486 (CalculateSuggestedSpacerPrice)
// The WPF file is the source of truth. Units are AED/SQM.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Pricing;

/// <summary>
/// Suggested DGU spacer price: a base 37.5 AED for a 6 mm air-space, plus
/// 2.5 AED for every additional 2 mm, plus a flat surcharge for premium
/// spacer materials. 12 mm + Normal Mill Aluminum → 45.00 AED/SQM.
/// </summary>
public static class SpacerPricing
{
    /// <summary>
    /// WPF material surcharges (AED/SQM). Case-sensitive switch, unknown
    /// material → 0 — preserved verbatim.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, decimal> Surcharges =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["Normal Mill Aluminum"] = 0m,
            ["Black Aluminum"] = 5m,
            // PHASE A: black-finish warm edge — premium over the standard
            // warm edge spacer (matched to supplier quotes).
            ["Warm Edge (Super Spacer)"] = 15m,
            ["Black Warm Edge"] = 20m,
            ["Stainless Steel"] = 10m,
            ["TGI Spacer"] = 12m,
        };

    /// <summary>Materials the WPF dropdown offers, in display order.</summary>
    public static readonly string[] MaterialOptions =
    {
        "Normal Mill Aluminum",
        "Black Aluminum",
        "Warm Edge (Super Spacer)",
        "Black Warm Edge",
        "Stainless Steel",
        "TGI Spacer",
    };

    /// <summary>
    /// Price for a width selected as an ASP option (e.g. "12mm ASP") and a
    /// material. Width parse failure falls back to 12 mm, as in the WPF.
    /// </summary>
    public static decimal SuggestedPrice(string? widthText, string? material)
    {
        var widthMm = ParseWidthMm(widthText);
        var basePrice = 37.5m + ((widthMm - 6) / 2m) * 2.5m;
        return basePrice + GetMaterialSurcharge(material);
    }

    /// <summary>"12mm ASP" → 12; "6mm ASP" → 6; anything unparsable → 12.</summary>
    public static decimal ParseWidthMm(string? widthText)
    {
        if (string.IsNullOrWhiteSpace(widthText)) return 12m;

        var mmIndex = widthText.IndexOf("mm", StringComparison.OrdinalIgnoreCase);
        if (mmIndex <= 0) return 12m;

        var digits = widthText[..mmIndex].Trim();
        return int.TryParse(digits, out var mm) ? mm : 12m;
    }

    private static decimal GetMaterialSurcharge(string? material) =>
        material is not null && Surcharges.TryGetValue(material, out var surcharge)
            ? surcharge
            : 0m;
}
