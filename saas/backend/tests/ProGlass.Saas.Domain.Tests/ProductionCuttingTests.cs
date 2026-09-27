using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// Phase 26b pure halves (WPF ground truth): material grouping, the spec
/// keyword table + thickness inference, stock selection, the deterministic
/// id builders and the 0022 vocabularies.
/// </summary>
public sealed class ProductionCuttingTests
{
    private static readonly Guid SheetA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SheetB = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid SheetC = Guid.Parse("30000000-0000-0000-0000-000000000003");

    private static CuttingRequirement Req(
        int seq, string spec, int width, int height, int thickness = 6,
        string family = "Clear", string color = "Clear",
        string coating = "", string treatment = "", bool isFoc = false,
        int replacementSequence = 0) => new(
        "JOB-1", "PI-1", 1, spec, "DGU", 1, "G-101", "Outer",
        width, height, family, color, thickness, coating, treatment,
        treatment.Contains("FT"), $"JOB-1|1|1|Outer|{width}x{height}|P{seq:D4}",
        $"G-101 · 1/1 · Outer · {seq}/1", seq, seq, isFoc, replacementSequence);

    // ── grouping (WPF MaterialGroupingService) ────────────────────────

    [Fact]
    public void Group_key_is_family_color_thickness_coating_treatment_uppercase()
    {
        var key = CuttingGrouping.BuildGroupKey(Req(1, "s", 100, 100));
        Assert.Equal("CLEAR|CLEAR|6||", key);
    }

    [Fact]
    public void Grouping_orders_family_color_thickness_coating_treatment()
    {
        var groups = CuttingGrouping.GroupByMaterial(new[]
        {
            Req(1, "s", 100, 100, thickness: 8, family: "Clear", color: "Clear"),
            Req(2, "s", 100, 100, thickness: 6, family: "Low-E", color: "Low-E", coating: "Low-E"),
            Req(3, "s", 100, 100, thickness: 6, family: "Clear", color: "Clear"),
            Req(4, "s", 100, 100, thickness: 6, family: "Clear", color: "Grey"),
        });

        Assert.Equal(4, groups.Count);
        // Clear/Clear/6 first, then Clear/Clear/8, Clear/Grey/6, Low-E last
        // (OrdinalIgnoreCase family ordering).
        Assert.Equal("CLEAR|CLEAR|6||", groups[0].GroupId);
        Assert.Equal("CLEAR|CLEAR|8||", groups[1].GroupId);
        Assert.Equal("CLEAR|GREY|6||", groups[2].GroupId);
        Assert.Equal("LOW-E|LOW-E|6|LOW-E|", groups[3].GroupId);
        Assert.Equal(6, groups[0].ThicknessMm);
        Assert.Equal(1, groups[0].TotalPieces);
    }

    [Fact]
    public void AreCompatible_matches_thickness_color_family_coating_not_treatment()
    {
        var a = Req(1, "s", 100, 100, treatment: "FT");
        var b = Req(2, "s", 100, 100, treatment: "Annealed");
        var c = Req(3, "s", 100, 100, thickness: 8);
        var d = Req(4, "s", 100, 100, color: "Grey");
        var e = Req(5, "s", 100, 100, coating: "Low-E");

        Assert.True(CuttingGrouping.AreCompatible(a, b));   // treatment rides the key, never compared
        Assert.False(CuttingGrouping.AreCompatible(a, c));  // thickness must match exactly
        Assert.False(CuttingGrouping.AreCompatible(a, d));  // color must match
        Assert.False(CuttingGrouping.AreCompatible(a, e));  // coating must match
    }

    // ── material identity (WPF ParseMaterialFromSpec + InferThickness) ─

    [Theory]
    [InlineData("6mm Low-E Clear Glass", "Low-E", "Low-E", "Low-E")]
    [InlineData("HP Neutral 70/40", "Low-E", "Low-E", "Low-E")]
    [InlineData("6mm HD Grey Glass", "HD Grey", "Grey", "")]
    [InlineData("6mm Gray tint", "HD Grey", "Grey", "")]
    [InlineData("5mm Ramly obscure", "Ramly", "Obscure", "")]
    [InlineData("5mm Frosted", "Ramly", "Obscure", "")]
    [InlineData("6mm Green Glass", "Green", "Green", "")]
    [InlineData("6mm Blue Glass", "Blue", "Blue", "")]
    [InlineData("Custom Composition X", "G-101", "Clear", "")] // GlassRef head fallback
    public void ParseFromSpec_keyword_table(
        string spec, string family, string color, string coating)
    {
        var (f, c, k) = CuttingMaterial.ParseFromSpec(spec, "G-101 · extra");
        Assert.Equal(family, f);
        Assert.Equal(color, c);
        Assert.Equal(coating, k);
    }

    [Theory]
    [InlineData("6mm Clear FT Glass + 12mm ASP + 6mm Green Annealed Glass", 6)]
    [InlineData("8mm Clear Glass", 8)]
    [InlineData("8mm Clear Glass + 12mm ASP + 6mm Clear Glass", 0)] // mixed panes never guessed
    [InlineData("Custom thing without panes", 0)]                    // unparsed never guessed
    [InlineData("", 0)]
    public void InferThickness_single_distinct_pane_or_zero(
        string spec, int expected)
    {
        Assert.Equal(expected, CuttingMaterial.InferThickness(spec));
    }

    [Theory]
    [InlineData("FT", true)]
    [InlineData("Tempered", true)]
    [InlineData("Heat Strengthened", true)]
    [InlineData("Annealed", false)]
    [InlineData("", false)]
    public void RequiresTempering_matches_wpf_keywords(
        string treatment, bool expected)
    {
        Assert.Equal(expected, CuttingMaterial.RequiresTempering(treatment));
    }

    [Fact]
    public void Piece_label_uses_the_wpf_format()
    {
        Assert.Equal(
            "G-101 · 2/3 · Inner · 2/4",
            CuttingMaterial.BuildPieceLabel("G-101", 2, 3, "Inner", 2, 4));
    }

    // ── stock selection (WPF StockSelectionService) ──────────────────

    private static MaterialGroup Group(params CuttingRequirement[] reqs) =>
        CuttingGrouping.GroupByMaterial(reqs)[0];

    private static List<(Guid, string, string, int, int, int, int, int)> Racks() => new()
    {
        (SheetA, "Clear", "Clear", 6, 3210, 2250, 10, 0),
        (SheetB, "Clear", "Clear", 6, 2250, 3210, 10, 0),
        (SheetC, "Clear", "Clear", 6, 6000, 3210, 10, 0),
    };

    [Fact]
    public void Compatibility_rejects_thickness_mismatch_and_unfitting_sheets()
    {
        var group = Group(Req(1, "s", 1000, 2000));

        Assert.True(CuttingStockSelection.IsSheetCompatible(
            "Clear", "Clear", 6, 3210, 2250, group));
        // Thickness mismatch.
        Assert.False(CuttingStockSelection.IsSheetCompatible(
            "Clear", "Clear", 8, 3210, 2250, group));
        // Largest piece must fit, rotated either way.
        Assert.False(CuttingStockSelection.IsSheetCompatible(
            "Clear", "Clear", 6, 1500, 1500, group));
        // Color fallback: category may stand in for the family.
        Assert.True(CuttingStockSelection.IsSheetCompatible(
            "Clear", "", 6, 3210, 2250, group));
    }

    [Fact]
    public void Clear_and_LowE_are_hard_incompatible()
    {
        var group = Group(Req(1, "s", 1000, 2000, family: "Low-E", color: "Low-E"));

        Assert.False(CuttingStockSelection.IsSheetCompatible(
            "Clear", "Clear", 6, 3210, 2250, group));
        Assert.True(CuttingStockSelection.IsSheetCompatible(
            "Low-E", "Low-E", 6, 3210, 2250, group));
    }

    [Fact]
    public void Evaluate_orders_by_waste_then_area_then_id_and_skips_held_out_racks()
    {
        var group = Group(Req(1, "s", 1000, 2000));
        var racks = Racks();

        // No holds: smallest waste first (3210×2250 area − 2 m² piece).
        var free = CuttingStockSelection.Evaluate(group, racks, new Dictionary<Guid, int>());
        Assert.Equal(3, free.Count);
        Assert.Equal(SheetA, free[0].GlassSheetId);
        Assert.Equal(SheetB, free[1].GlassSheetId);
        Assert.Equal(SheetC, free[2].GlassSheetId);

        // Fully held rack (balance 10, holds 10) is not a healthy pool.
        var held = CuttingStockSelection.Evaluate(
            group, racks, new Dictionary<Guid, int> { [SheetA] = 10 });
        Assert.Equal(2, held.Count);
        Assert.DoesNotContain(held, c => c.GlassSheetId == SheetA);

        // Allocations are a DIFFERENT unit — never subtracted (WPF note:
        // subtracting placed pieces treated 15 pieces as 15 sheets).
        var partiallyHeld = CuttingStockSelection.Evaluate(
            group, racks, new Dictionary<Guid, int> { [SheetA] = 3 });
        Assert.Equal(7, partiallyHeld.Single(c => c.GlassSheetId == SheetA).EffectiveAvailable);
    }

    [Fact]
    public void Sheet_count_estimate_uses_the_80pct_fill()
    {
        // 8 sqm of pieces on a 7.2225 sheet → ceil(8 / (7.2225 × 0.8)) = 2.
        var group = Group(
            Req(1, "s", 1000, 2000), Req(2, "s", 1000, 2000),
            Req(3, "s", 1000, 2000), Req(4, "s", 1000, 2000));
        Assert.Equal(8m, group.TotalAreaSqm);
        Assert.Equal(2, CuttingStockSelection.EstimateSheetCount(group, 7.2225m));
        Assert.Equal(1, CuttingStockSelection.EstimateSheetCount(group, 0m));
    }

    // ── deterministic ids (WPF builders, verbatim formats) ────────────

    [Fact]
    public void Allocation_cost_and_replacement_ids_use_the_wpf_formats()
    {
        Assert.Equal(
            "J|1|2|g|S3|LBL",
            CuttingIds.BuildAllocationId("J|1|2|g", 3, "LBL"));
        Assert.Equal(
            "JOB-1|1|2|1",
            CuttingIds.BuildCostId("JOB-1", 1, 2, ActualCostTypes.ActualMaterial));
        Assert.Equal(
            "JOB-1|1|2|1|alloc",
            CuttingIds.BuildCostId("JOB-1", 1, 2, ActualCostTypes.ActualMaterial, "alloc"));
        Assert.Equal(
            "JOB-1|1|2|1|alloc|FOC",
            CuttingIds.BuildCostId("JOB-1", 1, 2, ActualCostTypes.ActualMaterial, "alloc", isFoc: true));
        Assert.Equal(
            "JOB-1|1|2|Outer|6|RPL:2",
            CuttingIds.BuildReplacementId("JOB-1", 1, 2, "Outer", 6, 2));
        Assert.Equal(
            "orig|RPL:1",
            CuttingIds.BuildReplacementWorkItemKey("orig", 1));
    }

    [Fact]
    public void Cutting_session_sheet_and_placed_piece_ids_use_the_wpf_formats()
    {
        var stamp = new DateTime(2026, 9, 27, 8, 30, 5, DateTimeKind.Utc);
        Assert.Equal(
            "CUT-JOB-1-CLEAR-CLEAR-6--20260927083005",
            CuttingIds.BuildCuttingSessionId("JOB-1", "CLEAR|CLEAR|6||", stamp));
        Assert.Equal(
            "CUT-JOB-1-x|S2",
            CuttingIds.BuildCuttingSheetId("CUT-JOB-1-x", 2));
        Assert.Equal(
            "sheet|JOB-1|1|1|Outer|1000x2000|P0001",
            CuttingIds.BuildPlacedPieceId(
                "sheet", "JOB-1|1|1|Outer|1000x2000|P0001"));
    }

    // ── vocabularies (0022 CHECK constraints) ─────────────────────────

    [Fact]
    public void Allocation_status_rules_match_the_wpf_guards()
    {
        Assert.False(AllocationStatuses.CanAllocate(AllocationStatuses.Cancelled));
        Assert.False(AllocationStatuses.CanAllocate(AllocationStatuses.Released));
        Assert.False(AllocationStatuses.CanAllocate(AllocationStatuses.Consumed));
        Assert.True(AllocationStatuses.CanAllocate(AllocationStatuses.Reserved));
        Assert.True(AllocationStatuses.CanAllocate(AllocationStatuses.Allocated));

        Assert.False(AllocationStatuses.CanConsume(AllocationStatuses.Cancelled));
        Assert.False(AllocationStatuses.CanConsume(AllocationStatuses.Released));
        Assert.True(AllocationStatuses.CanConsume(AllocationStatuses.Allocated));
        Assert.True(AllocationStatuses.CanConsume(AllocationStatuses.Consumed));
    }

    [Fact]
    public void Cost_type_int_codes_match_the_wpf_enum()
    {
        Assert.Equal(0, ActualCostTypes.WpfCode(ActualCostTypes.Estimated));
        Assert.Equal(1, ActualCostTypes.WpfCode(ActualCostTypes.ActualMaterial));
        Assert.Equal(2, ActualCostTypes.WpfCode(ActualCostTypes.Production));
        Assert.Equal(3, ActualCostTypes.WpfCode(ActualCostTypes.Wastage));
        Assert.Equal(4, ActualCostTypes.WpfCode(ActualCostTypes.Processing));
        Assert.Equal(5, ActualCostTypes.WpfCode(ActualCostTypes.TotalActual));
    }
}
