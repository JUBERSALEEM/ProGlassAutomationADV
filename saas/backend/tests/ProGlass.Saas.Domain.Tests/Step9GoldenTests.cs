// ═══════════════════════════════════════════════════════════════════════
// GOLDEN VALUES — the Step 9 P&L, pinned to the WPF sources (read
// 2026-09-27):
//   ViewModels/…/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs
//     (row builders, three-margin KPI model, gross/1.05 net quirk)
//   …/ProfitLossStepViewModel.QuoteBasis.cs (CollectPanes,
//     EnforceStep4PaneIdentity, FallbackPaneRate, BuildGlassRowAsync,
//     ResolveStep6WastageSheetQuantity, BuildStep9SpecDisplay)
//   …/ProfitLossStepViewModel.CalculationSupport.cs (AppendStep6Costs,
//     AppendInvoiceWideCharge, ComputePriceBasedUInsertCost)
//   …/PnLMeasurement.cs (measurement + wastage math)
//   …/Step6SheetAllocation.cs (shared sheet-count allocation)
//   Services/…/OtherChargeAutomationRules.cs (U-insert suggestion)
//   Services/…/SheetStorePricingService.cs (store lookups)
// Expected numbers were computed independently with Python decimal from
// the pinned formulas (devbox/derive_step9_goldens.py) — never re-derived
// from the port itself.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class Step9GoldenTests
{
    private static SheetStockIndex Sheets(params (string Color, int Thk, int W, int H,
        decimal Buy, decimal Sell)[] rows) => new(rows.Select(r => new SheetStockEntry(
        r.Color, r.Thk, r.W, r.H, r.Buy, r.Sell)));

    // ── A. SGU: priced by required area, cutting/tempering broken out ──

    [Fact]
    public void Sgu_priced_by_required_area_with_broken_out_production()
    {
        // 1 panel 1000×2000 (2 SQM) × qty 2 → 4 SQM cut area.
        var group = new SpecGroupInput(
            1, "SGU", "6mm Clear FT Glass", "", "",
            [new GridRow(1000, 2000, 0, 0, 2, 0)],
            OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
            OuterSheetPricePerSqm: 100m,
            Step4CuttingCostPerSqm: 5m, Step4TemperingCostPerSqm: 8m);

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], [], [], [],
            Sheets(("Clear", 6, 3210, 2250, 90m, 110m)),
            WastagePercent: 15m, CostAtSellingPrice: false,
            GrossVatInclusive: 1000m));

        Assert.Equal(3, result.Rows.Count);

        // Glass: 4 + 15% = 4.6 SQM at the Step 4 box rate 100.
        var glass = result.Rows[0];
        Assert.Equal("Glass", glass.Kind);
        Assert.Equal(4.60m, glass.Quantity);
        Assert.Equal(100m, glass.PricePerUnit);
        Assert.Equal(460.00m, glass.Amount);
        Assert.Equal(3210, glass.SheetWidthMm);
        Assert.Equal(2250, glass.SheetHeightMm);
        Assert.Equal(7.2225m, glass.SheetSqm);
        Assert.Contains("priced by required area", glass.Description);
        Assert.Contains("Step 4 purchase price", glass.SheetSource);

        // Single glazing: cutting and tempering are separate SQM charges.
        Assert.Equal("Production", result.Rows[1].Kind);
        Assert.Contains("Cutting", result.Rows[1].Description);
        Assert.Equal(4m, result.Rows[1].Quantity);
        Assert.Equal(20.00m, result.Rows[1].Amount);
        Assert.Equal("Production", result.Rows[2].Kind);
        Assert.Contains("Tempering", result.Rows[2].Description);
        Assert.Equal(32.00m, result.Rows[2].Amount);

        // KPIs (gross 1000, net = gross / 1.05 verbatim).
        Assert.Equal(952.38m, result.Kpis.NetExVat);
        Assert.Equal(460.00m, result.Kpis.TotalGlassCost);
        Assert.Equal(52.00m, result.Kpis.TotalProductionCost);
        Assert.Equal(440.38m, result.Kpis.Profit);
        Assert.False(result.Kpis.IsLoss);
        // Basis: 100 / 0.85 + 5 + 8 = 130.65 per SQM × 4 SQM.
        Assert.Equal(522.60m, result.Kpis.BasisCost);
        Assert.Equal(91.35m, result.Kpis.MarginPercent);
        Assert.Equal(86.01m, result.Kpis.OverallMarginPercent);
        Assert.Equal(46.24m, result.Kpis.NetMarginPercent);
        Assert.Equal(0m, result.Kpis.UInsertCost);

        // The per-spec view: area 4, basis 130.65.
        Assert.Equal(4m, result.SpecAreas[1]);
        Assert.Equal(130.65m, result.SpecBases[1]);
    }

    // ── B. DGU: per-SQM spacer, Step 6 wastage setup owns sheet qty,
    //        polish charge, the shared-17-sheets allocation ─────────────

    [Fact]
    public void Dgu_costs_spacer_per_sqm_and_step6_setup_owns_sheet_qty()
    {
        // 3 panels 2000×1500 (3 SQM each) → 9 SQM cut area.
        var group = new SpecGroupInput(
            1, "DGU", "6mm Bronze FT Glass + 12mm ASP + 6mm Clear FT Glass",
            "Bronze", "Clear",
            [new GridRow(2000, 1500, 0, 0, 3, 0)],
            OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
            InnerSheetWidthMm: 3210, InnerSheetHeightMm: 2250,
            OuterSheetPricePerSqm: 120m, InnerSheetPricePerSqm: 95m,
            Step4SpacerPricePerSqm: 45m);

        // Step 6: one shared wastage setup of 17 sheets across BOTH pane
        // sources (same stock sheet → inferred utilization 14.66%), and a
        // BothHeightSide polish at 6/LM on spec 1.
        var wastages = new List<PnlStep6Wastage>
        {
            new(["1-Outer (W1/H1)", "1-Inner (W2/H2)"], 17, 0m, true)
        };
        var charges = new List<PnlStep6Charge>
        {
            new(ChargeTemplates.PolishKey, "Polish (lm)", "", "Lm", [1],
                6m, PolishSubTypes.BothHeightSide, false, true)
        };

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], charges, wastages, [],
            Sheets(("Bronze", 6, 3210, 2250, 100m, 130m),
                   ("Clear", 6, 3210, 2250, 80m, 100m)),
            WastagePercent: 15m, CostAtSellingPrice: false,
            GrossVatInclusive: 5000m));

        // Shared 17 sheets split 9 + 1-scaled: outer 9, inner 8 (equal
        // weights, largest-remainder tie keeps source order).
        var outer = result.Rows.Single(r =>
            r.Kind == "Glass" && r.Description.Contains("Outer pane"));
        Assert.Equal(9m, outer.Quantity);
        Assert.Equal(7800.30m, outer.Amount); // 7.2225 × 9 × 120
        Assert.Contains("SHEET QTY from Step 6 wastage setup", outer.Description);

        var inner = result.Rows.Single(r =>
            r.Kind == "Glass" && r.Description.Contains("Inner pane"));
        Assert.Equal(8m, inner.Quantity);
        Assert.Equal(5489.10m, inner.Amount); // 7.2225 × 8 × 95

        // The pane identity: Step 4 colours are authoritative.
        Assert.Contains("6mm Bronze", outer.Description);
        Assert.Contains("6mm Clear", inner.Description);

        // ASP is priced PER SQM (not perimeter), one cavity.
        var spacer = result.Rows.Single(r => r.Kind == "Spacer");
        Assert.Equal(9m, spacer.Quantity);
        Assert.Equal(45m, spacer.PricePerUnit);
        Assert.Equal(405.00m, spacer.Amount);
        Assert.Contains("12mm ASP (includes cutting, tempering and production)",
            spacer.Description);

        // Polish: BothHeightSide = 2 × H per panel → 3 LM × 3 = 9 LM.
        var polish = result.Rows.Single(r => r.Kind == "Polish");
        Assert.Equal(9m, polish.Quantity);
        Assert.Equal("LM", polish.UnitLabel);
        Assert.Equal(54.00m, polish.Amount);

        // KPIs: glass 13,289.40 + production 459.00 (spacer + polish).
        Assert.Equal(4761.90m, result.Kpis.NetExVat);
        Assert.Equal(13289.40m, result.Kpis.TotalGlassCost);
        Assert.Equal(459.00m, result.Kpis.TotalProductionCost);
        Assert.Equal(-8986.50m, result.Kpis.Profit);
        Assert.True(result.Kpis.IsLoss);
        // Basis: (120 + 95) / 0.85 + 45 = 297.94 × 9 SQM.
        Assert.Equal(2681.46m, result.Kpis.BasisCost);
        Assert.Equal(86.47m, result.Kpis.MarginPercent);
        Assert.Equal(-65.36m, result.Kpis.OverallMarginPercent);
        Assert.Equal(-188.72m, result.Kpis.NetMarginPercent);
    }

    // ── C. LAM: interlayer + cutting/tempering on both panes ──────────

    [Fact]
    public void Lam_costs_interlayer_plus_processing_on_both_panes()
    {
        var group = new SpecGroupInput(
            1, "LAM", "6mm Clear FT Glass + 1.52 PVB + 6mm Clear FT Glass",
            "Clear", "Clear",
            [new GridRow(1000, 1000, 0, 0, 2, 0)],
            OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
            InnerSheetWidthMm: 3210, InnerSheetHeightMm: 2250,
            OuterSheetPricePerSqm: 110m, InnerSheetPricePerSqm: 110m,
            Step4InterlayerCostPerSqm: 40m,
            Step4CuttingCostPerSqm: 3m, Step4TemperingCostPerSqm: 4m);

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], [], [], [],
            Sheets(("Clear", 6, 3210, 2250, 90m, 110m)),
            WastagePercent: 0m, CostAtSellingPrice: false,
            GrossVatInclusive: 1000m));

        // Two glass panes, one sheet each (2 SQM needed of a 7.2225 sheet).
        Assert.Equal(2, result.Rows.Count(r => r.Kind == "Glass"));
        Assert.All(result.Rows.Where(r => r.Kind == "Glass"),
            g => Assert.Equal(794.48m, g.Amount)); // 7.2225 × 1 × 110

        // Production: PVB 40 + cutting 3 + tempering 4 = 47/SQM × 2 SQM.
        var lam = result.Rows.Single(r => r.Kind == "Production");
        Assert.Equal(47m, lam.PricePerUnit);
        Assert.Equal(2m, lam.Quantity);
        Assert.Equal(94.00m, lam.Amount);
        Assert.Contains("1.52mm PVB interlayer + cutting/tempering (both panes)",
            lam.Description);

        Assert.Equal(1588.96m, result.Kpis.TotalGlassCost);
        Assert.Equal(-730.58m, result.Kpis.Profit);
        Assert.True(result.Kpis.IsLoss);
        // Basis: (110 + 110) / 1.0 + 47 = 267 × 2 SQM.
        Assert.Equal(534.00m, result.Kpis.BasisCost);
        Assert.Equal(87.27m, result.Kpis.MarginPercent);
        Assert.Equal(-43.41m, result.Kpis.OverallMarginPercent);
        Assert.Equal(-76.71m, result.Kpis.NetMarginPercent);
    }

    // ── D. Price-based U-insert: spec names the extra, no Step 6 row ──

    [Fact]
    public void U_insert_kpi_uses_template_default_when_no_row_exists()
    {
        var group = new SpecGroupInput(
            1, "SGU", "6mm Clear FT Glass with U-Insert", "Clear", "",
            [new GridRow(1000, 1000, 0, 0, 1, 0)],
            OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
            OuterSheetPricePerSqm: 100m);

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], [], [], [],
            Sheets(("Clear", 6, 3210, 2250, 90m, 110m)),
            WastagePercent: 0m, CostAtSellingPrice: false,
            GrossVatInclusive: 500m));

        // No U-insert row was saved, but the job still carries the cost:
        // SQM1 = 1 × the AED 10/m² template default.
        Assert.Equal(10.00m, result.Kpis.UInsertCost);

        // And no U-insert row entered production cost (nothing was saved).
        Assert.DoesNotContain(result.Rows, r => r.Kind.Contains("U-Insert"));
    }

    // ── Exclusions and invoice-wide rows ───────────────────────────────

    [Fact]
    public void Supplemental_addons_are_never_pnl_cost_rows()
    {
        var group = new SpecGroupInput(
            1, "SGU", "6mm Clear FT Glass", "Clear", "",
            [new GridRow(1000, 1000, 0, 0, 1, 0)],
            OuterSheetPricePerSqm: 100m);

        var charges = new List<PnlStep6Charge>
        {
            new("ARGON_GAS", "Argon Gas (Sqm)", MetricKeys.Sqm1, "Sqm", [1], 45m, "", false, true),
            new(ChargeTemplates.DeliveryKey, "Delivery (per trip)", "", "Qty", [], 150m, "", false, true)
        };

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], charges, [], [],
            Sheets(),
            WastagePercent: 0m, CostAtSellingPrice: false,
            GrossVatInclusive: 1000m));

        // Argon is a quoted-spec add-on, never a P&L cost line.
        Assert.DoesNotContain(result.Rows, r => r.Kind.Contains("Argon"));

        // Delivery is per-invoice: one TRIP row at spec 0.
        var delivery = result.Rows.Single(r => r.Kind == "Delivery");
        Assert.Equal(0, delivery.SpecNumber);
        Assert.Equal("Invoice-wide other charges", delivery.SpecName);
        Assert.Equal("TRIP", delivery.UnitLabel);
        Assert.Equal(150.00m, delivery.Amount);
    }

    [Fact]
    public void An_inactive_step6_row_costs_nothing()
    {
        var group = new SpecGroupInput(
            1, "SGU", "6mm Clear FT Glass", "Clear", "",
            [new GridRow(1000, 1000, 0, 0, 1, 0)],
            OuterSheetPricePerSqm: 100m);

        var charges = new List<PnlStep6Charge>
        {
            new(ChargeTemplates.DeliveryKey, "Delivery (per trip)", "", "Qty", [], 150m, "", false, false)
        };

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], charges, [], [],
            Sheets(),
            WastagePercent: 0m, CostAtSellingPrice: false,
            GrossVatInclusive: 1000m));

        Assert.DoesNotContain(result.Rows, r => r.Kind == "Delivery");
        Assert.Equal(0m, result.Kpis.TotalProductionCost);
    }

    // ── Zero-revenue is not a loss (the WPF status rule) ───────────────

    [Fact]
    public void Zero_gross_is_cost_known_not_a_loss()
    {
        var group = new SpecGroupInput(
            1, "SGU", "6mm Clear FT Glass", "Clear", "",
            [new GridRow(1000, 1000, 0, 0, 1, 0)],
            OuterSheetPricePerSqm: 100m);

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], [], [], [],
            Sheets(),
            WastagePercent: 0m, CostAtSellingPrice: false,
            GrossVatInclusive: 0m));

        Assert.True(result.Kpis.Profit < 0m);
        Assert.False(result.Kpis.IsLoss);
    }

    // ── Sheet Store lookups (SheetStorePricingService contract) ────────

    [Fact]
    public void Sheet_store_sizes_are_biggest_first_and_prices_exact()
    {
        var index = Sheets(
            ("Clear", 6, 2440, 1830, 80m, 100m),
            ("Clear", 6, 3210, 2250, 90m, 110m),
            ("Clear", 6, 2250, 3210, 91m, 111m), // rotated duplicate
            (" clear ", 6, 3210, 2250, 85m, 105m)); // Norm() colour match

        var sizes = index.GetSizes("CLEAR", 6);
        Assert.Equal(2, sizes.Count);
        Assert.Equal((3210, 2250), (sizes[0].W, sizes[0].H)); // area DESC
        Assert.Equal((2440, 1830), (sizes[1].W, sizes[1].H));

        // Rotation-tolerant, priced-first / cheapest-purchase ordering.
        var price = index.TryGetExactPrice("Clear", 6, 2250, 3210);
        Assert.NotNull(price);
        Assert.Equal(85m, price!.Value.Purchase);
        Assert.Equal(105m, price.Value.Sell);

        Assert.Null(index.TryGetExactPrice("Clear", 8, 3210, 2250));
    }

    // ── Missing authoritative sheet: a data gap, not a guess ───────────

    [Fact]
    public void A_pane_without_a_step4_sheet_is_a_data_gap_row()
    {
        // Step 4 identified the pane (price handoff) but gave no sheet:
        // no catalogue size is invented. An SGU pane still prices by its
        // required area (the Step 4 rate is authoritative), but the sheet
        // columns stay empty and the source names the gap.
        var group = new SpecGroupInput(
            1, "SGU", "6mm Clear FT Glass", "Clear", "",
            [new GridRow(1000, 1000, 0, 0, 1, 0)],
            OuterSheetPricePerSqm: 100m);

        var result = PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            [group], [], [], [],
            Sheets(("Clear", 6, 3210, 2250, 90m, 110m)),
            WastagePercent: 0m, CostAtSellingPrice: false,
            GrossVatInclusive: 1000m));

        var glass = result.Rows.Single(r => r.Kind == "Glass");
        Assert.Equal(0, glass.SheetWidthMm);
        Assert.Equal(0, glass.SheetHeightMm);
        Assert.Equal(0m, glass.SheetSqm);
        Assert.Equal(1m, glass.Quantity);         // needed SQM, no sheet math
        Assert.Equal(100.00m, glass.Amount);      // required area × Step 4 rate
        Assert.Equal("MISSING Step 4 selected sheet — re-apply Step 4 for this spec",
            glass.SheetSource);
        Assert.Contains("MISSING AUTHORITATIVE STEP 4 SHEET SIZE", glass.Description);
        Assert.Equal(100.00m, result.Kpis.TotalGlassCost);
    }
}
