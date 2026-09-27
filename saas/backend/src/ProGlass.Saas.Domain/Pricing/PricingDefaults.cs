// ═══════════════════════════════════════════════════════════════════════
// The WPF Step 4 panel defaults and dropdown lists, VERBATIM (read
// 2026-09-27 — these are business constants, not settings anyone chose):
//   ViewModels/ProformaInvoice/Step4PricingEstimator/
//     SguParametersViewModel.cs, DguParametersViewModel.cs,
//     LamParametersViewModel.cs, DguLamParametersViewModel.cs
// In the WPF these are hardcoded in the four view-model constructors; in
// the SaaS they seed tenant_pricing_config so each tenant can edit them,
// but the SEED values must never drift from the WPF (§66 — never invent).
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Pricing;

/// <summary>
/// Seed defaults for a tenant's pricing configuration (schema 0003
/// tenant_pricing_config / tenant_vat_config) plus the read-only panel
/// defaults the WPF estimator opens with.
/// </summary>
public static class PricingDefaults
{
    // ── shared numeric defaults (schema column defaults) ────────────
    // WPF: every panel opens at yield 0.85; SGU/DGU/LAM at margin 15%
    // (DGU+LAM at 20% — the business's worked chain uses 20%).

    public const decimal YieldFactorDefault = 0.85m;
    public const decimal MarginDefault = 15m;
    public const decimal VatRateDefault = 0.05m;   // 5% UAE VAT
    public const string CurrencyDefault = "AED";

    // ── catalog seeds ───────────────────────────────────────────────
    // Panel-by-panel differences are preserved below as comments; where
    // the panels differ the seed takes the documented union (see
    // WPF_TO_SAAS_MAPPING §1.11 — every merge is listed there).

    /// <summary>SGU/LAM panel list is 3–19 with 15mm; the DGU+LAM panel
    /// ships 16mm instead of 15mm (WPF quirk, kept verbatim in its own
    /// panel defaults below). The shared catalog carries both.</summary>
    public static readonly string[] ThicknessOptions =
        { "3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "16mm", "19mm" };

    /// <summary>SGU panel display form ("FT Glass"); the DGU/LAM/DGU+LAM
    /// panels spell it just "FT". Same four treatments.</summary>
    public static readonly string[] TreatmentOptions =
        { "FT Glass", "Annealed", "Heat Strengthened", "Heat Soaked" };

    /// <summary>WPF DGU SpacerWidthOptions (air-space widths), verbatim.</summary>
    public static readonly string[] AspWidths =
    {
        "6mm ASP", "8mm ASP", "10mm ASP", "12mm ASP", "14mm ASP",
        "16mm ASP", "18mm ASP", "20mm ASP", "22mm ASP", "24mm ASP",
        "26mm ASP", "28mm ASP", "30mm ASP", "32mm ASP",
    };

    /// <summary>WPF DGU+LAM InterlayerTypeOptions, verbatim.</summary>
    public static readonly string[] InterlayerTypes = { "PVB", "SGP", "EVA" };

    /// <summary>DGU+LAM ships 0.38–2.28; the LAM panel adds 3.04mm. The
    /// shared catalog carries all six.</summary>
    public static readonly string[] InterlayerThicknesses =
        { "0.38mm", "0.76mm", "1.14mm", "1.52mm", "2.28mm", "3.04mm" };

    /// <summary>WPF SGU PolishOptions (17), verbatim — edge finishes,
    /// surface treatments and fabrication in one list.</summary>
    public static readonly string[] PolishOptions =
    {
        "None (Standard)",
        "Polished Edges",
        "Beveled Edges",
        "Mitring (45° Bevel)",
        "Frosted",
        "Sandblasted",
        "With Fanhole",
        "With Fanhole As Per DWG",
        "With Holes",
        "With Holes As Per DWG",
        "With Holes And Cutouts",
        "With Holes And Cutouts As Per DWG",
        "With Cutouts",
        "With Cutouts As Per DWG",
        "With All Side Polish",
        "With Both Height Side Polish",
        "With Both Height Side Polish As Per DWG",
    };

    /// <summary>WPF DGU PanelExtraOptions (18) — a superset of the SGU
    /// panel's 13; the DGU+LAM panel uses the same list. Verbatim.</summary>
    public static readonly string[] PanelExtraOptions =
    {
        "None",
        "With Overlap",
        "With Overlap As Per DWG",
        "With Overlap As Per Details",
        "With Overlap (SQM)",
        "With Overlap (SQM) As Per DWG",
        "With Fanhole",
        "With Fanhole As Per DWG",
        "With Cutout",
        "With Cutout As Per DWG",
        "With Holes",
        "With Holes As Per DWG",
        "With Cutout And Holes",
        "With Cutout And Holes As Per DWG",
        "With Shutter Bonding",
        "With Shutter Bonding As Per DWG",
        "With Argon Gas",
        "With Argon Gas As Per DWG",
    };

    /// <summary>WPF DGU SpacerExtraOptions (U-Insert), verbatim.</summary>
    public static readonly string[] SpacerExtraOptions =
        { "None", "With U-Insert", "With U-Insert As Per DWG" };

    /// <summary>WPF LAM/DGU+LAM edge-polish list (13), verbatim. The LM
    /// formulas behind it belong to Phase 8 (Other Charges), not here.</summary>
    public static readonly string[] EdgePolishOptions =
    {
        "None",
        "All Side Polish",
        "All Side Polish As Per DWG",
        "Both Height Side Polish",
        "Both Height Side Polish As Per DWG",
        "Both Width Side Polish",
        "Both Width Side Polish As Per DWG",
        "Both Height + One Width Side Polish",
        "Both Height + One Width Side Polish As Per DWG",
        "One Height + One Width Side Polish",
        "One Height + One Width Side Polish As Per DWG",
        "One Height Side Polish",
        "One Width Side Polish",
    };

    /// <summary>WPF LAM PvbColorOptions (7), verbatim.</summary>
    public static readonly string[] PvbColorOptions =
        { "Clear", "White", "Frosted", "Milky White", "Black", "Grey", "Bronze" };

    /// <summary>WPF DGU+LAM InterlayerColorOptions (7), verbatim.</summary>
    public static readonly string[] InterlayerColorOptions =
        { "Clear", "Frosted", "Milky White", "Grey", "Bronze", "Blue", "Green" };

    // ── panel opening defaults (WPF constructor initializers) ──────
    // Read-only in the API: these are the WPF's hardcoded starting
    // numbers, exposed so the SaaS estimator UI opens identically.

    /// <summary>SGU panel: 6mm Clear "FT Glass" @ 46, cutting 15,
    /// tempering 25, yield 0.85, margin 15, polish "None (Standard)".</summary>
    public sealed record SguPanelDefaults(
        decimal BasePricePerSqm, decimal CuttingCostPerSqm, decimal TemperingCostPerSqm,
        decimal YieldFactor, decimal TargetMarginPercent)
    {
        public static SguPanelDefaults Instance { get; } = new(46m, 15m, 25m, 0.85m, 15m);
    }

    /// <summary>DGU panel: both panes 6mm Clear FT @ 46, "12mm ASP" +
    /// "Normal Mill Aluminum" → suggested spacer 45, extras "None",
    /// yield 0.85, margin 15.</summary>
    public sealed record DguPanelDefaults(
        decimal OuterBasePrice, decimal InnerBasePrice,
        string AspWidth, string SpacerMaterial,
        decimal YieldFactor, decimal MarginProfitPercent)
    {
        public static DguPanelDefaults Instance { get; } =
            new(46m, 46m, "12mm ASP", "Normal Mill Aluminum", 0.85m, 15m);
    }

    /// <summary>LAM panel: both panes 46, PVB 1.52mm Clear @ 80, polish
    /// "None" (HOTFIX-2), cutting 15+15, tempering 25+25, yield 0.85,
    /// margin 15.</summary>
    public sealed record LamPanelDefaults(
        decimal OuterBasePrice, decimal InnerBasePrice, decimal PvbMaterialRate,
        decimal OuterCuttingCost, decimal InnerCuttingCost,
        decimal OuterTemperingCost, decimal InnerTemperingCost,
        decimal YieldFactor, decimal MarginProfitPercent)
    {
        public static LamPanelDefaults Instance { get; } =
            new(46m, 46m, 80m, 15m, 15m, 25m, 25m, 0.85m, 15m);
    }

    /// <summary>DGU+LAM panel — the business's worked chain IS the shipped
    /// default (DguLamPricingTests: a fresh VM quotes 344.75 unedited):
    /// panes 48 + 25 + 25, cutting 10×3, tempering 12+10+10, interlayer
    /// PVB 110 sell (100 buy), spacer 0 (opt-in — never silently added),
    /// yield 0.85, margin 20.</summary>
    public sealed record DguLamPanelDefaults(
        decimal OuterBasePrice, decimal LamOuterBasePrice, decimal LamInnerBasePrice,
        decimal OuterCuttingCost, decimal LamOuterCuttingCost, decimal LamInnerCuttingCost,
        decimal OuterTemperingCost, decimal LamOuterTemperingCost, decimal LamInnerTemperingCost,
        decimal InterlayerBuyPrice, decimal InterlayerSellPrice, string InterlayerType,
        decimal SpacerPrice, decimal YieldFactor, decimal MarginProfitPercent)
    {
        public static DguLamPanelDefaults Instance { get; } = new(
            48m, 25m, 25m,
            10m, 10m, 10m,
            12m, 10m, 10m,
            100m, 110m, "PVB",
            0m, 0.85m, 20m);
    }

    /// <summary>
    /// Interlayer price pairs by chemistry (WPF ApplyInterlayerDefaults —
    /// the figures the business quoted). SGP is 0/0 ON PURPOSE: no price
    /// was ever supplied, and inventing one would fabricate a quote; the
    /// rate call must warn instead.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (decimal Buy, decimal Sell)>
        InterlayerPrices = new Dictionary<string, (decimal, decimal)>
        {
            ["PVB"] = (100m, 110m),
            ["EVA"] = (75m, 95m),
            ["SGP"] = (0m, 0m),
        };

    /// <summary>
    /// The whole seed as one catalog set — used by the config endpoints
    /// when a tenant has no row yet, and by TenantProvisioner when it
    /// seeds a new tenant (schema defaults + WPF lists).
    /// </summary>
    public static PricingCatalogs Catalogs() => new(
        ThicknessOptions,
        TreatmentOptions,
        AspWidths,
        InterlayerTypes,
        InterlayerThicknesses,
        PolishOptions,
        PanelExtraOptions);
}

/// <summary>The seven tenant-editable catalog lists (jsonb columns of
/// tenant_pricing_config, schema 0003).</summary>
public sealed record PricingCatalogs(
    IReadOnlyList<string> ThicknessOptions,
    IReadOnlyList<string> TreatmentOptions,
    IReadOnlyList<string> AspWidths,
    IReadOnlyList<string> InterlayerTypes,
    IReadOnlyList<string> InterlayerThicknesses,
    IReadOnlyList<string> PolishOptions,
    IReadOnlyList<string> PanelExtraOptions);
