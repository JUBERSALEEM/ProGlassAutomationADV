using ProGlass.Saas.Domain.Proforma;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.ProductionDistributionEngine;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF goldens: Services/Production/ProductionDistributionEngine.cs (the
/// routing rules owner-confirmed 2026-09-11), DguDimensionSemantics and
/// the stage/status vocabularies. The engine is PURE — same inputs in,
/// identical plan out; every count below is a structural fact of the
/// routing table, and the sqm/lm values come from the already-golden
/// SqmCalculator (1000×2000 = 2.000 m²).
/// </summary>
public sealed class ProductionTests
{
    private static readonly ProductionSpecInput SguFt = new(
        1, "6mm Clear FT Glass", "SGU", new[]
        {
            new ProductionRowInput(1, "Front", 1000, 2000, 0, 0, 4, 2),
        });

    private static readonly ProductionSpecInput Dgu = new(
        2, "6mm Clear FT Glass + 12mm ASP + 6mm Green Annealed Glass",
        "DGU", new[]
        {
            new ProductionRowInput(1, "Front", 1000, 2000, 900, 1800, 4, 2),
        });

    private static ProductionJobPlan Distribute(
        IReadOnlyList<ProductionSpecInput> specs,
        IReadOnlyList<ProductionChargeInput>? charges = null,
        string jobRef = "JOB-2026-0042") =>
        ProductionDistributionEngine.Distribute(
            jobRef, "Cust", "Proj", "", specs,
            charges ?? Array.Empty<ProductionChargeInput>());

    private static IReadOnlyList<ProductionWorkItemPlan> Stage(
        ProductionJobPlan plan, string stage) =>
        plan.Items.Where(i => i.Stage == stage).ToList();

    [Fact]
    public void Sgu_annealed_cuts_once()
    {
        var spec = new ProductionSpecInput(1,
            "6mm Clear Annealed Glass", "SGU", new[]
            {
                new ProductionRowInput(1, "Front", 1000, 2000, 0, 0, 4, 2),
            });
        var plan = Distribute(new[] { spec });

        Assert.Empty(plan.Warnings);
        Assert.Single(plan.Items);
        var cut = plan.Items[0];
        Assert.Equal("Cutting", cut.Stage);
        Assert.Equal("Glass", cut.Position);
        Assert.Equal("Sgu", cut.BuildType);
        Assert.Equal(1000, cut.WidthMm);
        Assert.Equal(2000, cut.HeightMm);
        Assert.Equal(4, cut.Qty);
        Assert.Equal(2.000m, cut.UnitSqm);
        Assert.Equal(8.000m, cut.TotalSqm);
        Assert.Equal(24.000m, cut.TotalLm);       // (1000+2000)/1000×2×4
        Assert.Equal("Annealed", cut.Treatment);
        Assert.Equal(WorkItemStatuses.Pending, cut.Status);
        Assert.Equal("", cut.PredecessorStage);
        Assert.Equal("JOB-2026-0042|1|1|Glass|1", cut.UniqueKey);
    }

    [Fact]
    public void Sgu_ft_adds_a_tempering_cycle()
    {
        var plan = Distribute(new[] { SguFt });
        Assert.Empty(plan.Warnings);
        Assert.Equal(2, plan.Items.Count);
        var tempering = plan.Items.Single(i => i.Stage == "Tempering");
        Assert.Equal("FT CYCLE", tempering.Notes);
        Assert.Equal("Cutting", tempering.PredecessorStage);
        Assert.Equal("JOB-2026-0042|1|1|Glass|6", tempering.UniqueKey);
    }

    [Fact]
    public void Dgu_cuts_both_panes_then_pairs_and_assembles()
    {
        var plan = Distribute(new[] { Dgu });
        Assert.Empty(plan.Warnings);
        Assert.Equal(5, plan.Items.Count);

        var cutting = Stage(plan, "Cutting");
        Assert.Equal(2, cutting.Count);
        var outer = cutting.Single(i => i.Position == "Outer");
        var inner = cutting.Single(i => i.Position == "Inner");
        Assert.Equal(1000, outer.WidthMm);
        Assert.Equal(2000, outer.HeightMm);
        Assert.Equal(900, inner.WidthMm);
        Assert.Equal(1800, inner.HeightMm);
        Assert.Equal(6.480m, inner.TotalSqm);     // 1.62 × 4
        Assert.Equal(21.600m, inner.TotalLm);     // (900+1800)/1000×2×4

        // FT outer only — the annealed inner skips the furnace.
        var tempering = Stage(plan, "Tempering");
        Assert.Single(tempering);
        Assert.Equal("Outer", tempering[0].Position);

        var pairing = Assert.Single(Stage(plan, "Pairing"));
        Assert.Equal("2-PANE UNIT", pairing.Notes);
        var assembly = Assert.Single(Stage(plan, "DoublingLamination"));
        Assert.Equal("DGU ASSEMBLY · SPACER 12MM", assembly.Notes);
        Assert.Equal("Pairing", assembly.PredecessorStage);
        Assert.Equal("JOB-2026-0042|2|1|Unit|8", assembly.UniqueKey);
    }

    [Fact]
    public void Lam_routs_lam_positions_and_notes_the_interlayer()
    {
        var spec = new ProductionSpecInput(3,
            "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Green Annealed Glass",
            "LAM", new[]
            {
                new ProductionRowInput(1, "Side", 800, 1200, 780, 1180, 2, 2),
            });
        var plan = Distribute(new[] { spec });
        Assert.Empty(plan.Warnings);
        Assert.Equal(5, plan.Items.Count);

        var positions = Stage(plan, "Cutting").Select(i => i.Position).ToList();
        Assert.Equal(new[] { "Lam Outer", "Lam Inner" }, positions.OrderBy(p => p));
        var assembly = Assert.Single(Stage(plan, "DoublingLamination"));
        Assert.Equal("LAMINATION · PVB 1.52MM CLEAR", assembly.Notes);
    }

    [Fact]
    public void DguLam_cuts_three_panes()
    {
        var spec = new ProductionSpecInput(4,
            "6mm A FT Glass + 12mm ASP + 6mm B Annealed Glass + 1.52mm Clear PVB + 6mm C Annealed Glass",
            "DGU+LAM", new[]
            {
                new ProductionRowInput(1, "Front", 1000, 2000, 900, 1800, 3, 2),
            });
        var plan = Distribute(new[] { spec });
        Assert.Empty(plan.Warnings);
        Assert.Equal(6, plan.Items.Count);   // 3 cutting + 1 tempering + pairing + assembly
        Assert.Equal(3, Stage(plan, "Cutting").Count);
        var assembly = Assert.Single(Stage(plan, "DoublingLamination"));
        Assert.Equal(
            "DGU + LAMINATION (HYBRID) · SPACER 12MM · PVB 1.52MM CLEAR",
            assembly.Notes);
    }

    [Fact]
    public void Incomplete_inner_size_skips_the_pane_with_a_warning()
    {
        var spec = new ProductionSpecInput(1,
            "6mm Clear FT Glass + 12mm ASP + 6mm Green Annealed Glass",
            "DGU", new[]
            {
                new ProductionRowInput(1, "Front", 1000, 2000, 0, 500, 4, 2),
            });
        var plan = Distribute(new[] { spec });

        Assert.Contains(plan.Warnings, w =>
            w.Contains("incomplete W2/H2 size (0×500)") &&
            w.Contains("Inner pane was skipped"));
        Assert.Single(Stage(plan, "Cutting"));   // outer only
    }

    [Fact]
    public void Zero_qty_is_produced_as_one_with_a_warning()
    {
        var spec = new ProductionSpecInput(1,
            "6mm Clear Annealed Glass", "SGU", new[]
            {
                new ProductionRowInput(1, "Front", 1000, 2000, 0, 0, 0, 2),
            });
        var plan = Distribute(new[] { spec });

        Assert.Contains(plan.Warnings, w =>
            w.Contains("has no quantity — produced as 1 piece"));
        Assert.Equal(1, plan.Items[0].Qty);
    }

    [Fact]
    public void Glass_type_mismatch_is_visible()
    {
        // Explicit DGU on a single-pane name: the selection wins but the
        // mismatch is surfaced.
        var spec = new ProductionSpecInput(1, "6mm Clear Annealed Glass",
            "DGU", new[]
            {
                new ProductionRowInput(1, "Front", 1000, 2000, 0, 0, 2, 2),
            });
        var plan = Distribute(new[] { spec });
        Assert.Contains(plan.Warnings, w =>
            w.Contains("glass type 'DGU' selected but the spec name suggests Sgu"));
        // DGU still routes both occurrences (equal-size fallback).
        Assert.Equal(2, Stage(plan, "Cutting").Count);
    }

    [Fact]
    public void Unparsable_multi_pane_name_warns_and_routes_treatmentless()
    {
        var spec = new ProductionSpecInput(1, "some free text", "DGU", new[]
        {
            new ProductionRowInput(1, "Front", 1000, 2000, 900, 1800, 1, 2),
        });
        var plan = Distribute(new[] { spec });
        Assert.Contains(plan.Warnings, w =>
            w.Contains("could not be parsed") &&
            w.Contains("assume ANNEALED unless told otherwise"));
        Assert.Empty(Stage(plan, "Tempering"));  // no treatment info
    }

    [Fact]
    public void Stray_w2_on_sgu_is_flagged()
    {
        var spec = new ProductionSpecInput(1,
            "6mm Clear Annealed Glass", "SGU", new[]
            {
                new ProductionRowInput(1, "Front", 1000, 2000, 900, 1800, 1, 2),
            });
        var plan = Distribute(new[] { spec });
        Assert.Contains(plan.Warnings, w =>
            w.Contains("is a single glass unit (SGU) but some rows carry W2/H2"));
    }

    [Fact]
    public void Charge_work_fans_out_per_pane()
    {
        var charges = new[]
        {
            new ProductionChargeInput("FAN_HOLES", 8m, "", "", Array.Empty<int>()),
        };
        var plan = Distribute(new[] { Dgu }, charges);

        var holes = Stage(plan, "HolesCutouts");
        Assert.Equal(2, holes.Count);   // outer + inner
        Assert.All(holes, h =>
        {
            Assert.Equal("FAN HOLES", h.Notes);
            Assert.Equal("QTY", h.ChargeMetricUnit);
            Assert.Equal(8m, h.ChargeMetricQty);
            Assert.Equal(0m, h.TotalLm);
            Assert.EndsWith("|CHG:FAN_HOLES", h.UniqueKey);
        });
    }

    [Fact]
    public void Polish_carries_lm_and_mitring_routes()
    {
        var charges = new[]
        {
            new ProductionChargeInput("POLISH", 0m,
                "AllSidePolish", "", new[] { 2 }),
            new ProductionChargeInput("MITRING", 0m, "", "", Array.Empty<int>()),
        };
        var plan = Distribute(new[] { Dgu }, charges);

        var polish = Stage(plan, "Polish");
        Assert.Equal(2, polish.Count);
        Assert.All(polish, p =>
        {
            Assert.Equal("AllSidePolish", p.PolishSubTypeLabel);
            Assert.Equal("LM", p.ChargeMetricUnit);
        });
        var outerPolish = polish.Single(p => p.Position == "Outer");
        Assert.Equal(24.000m, outerPolish.TotalLm);

        Assert.Equal(2, Stage(plan, "Mitring").Count);
    }

    [Fact]
    public void Assembly_charges_decorate_the_doubling_item()
    {
        var charges = new[]
        {
            new ProductionChargeInput("ARGON_GAS", 0m, "", "", Array.Empty<int>()),
        };
        var plan = Distribute(new[] { Dgu }, charges);
        var assembly = Assert.Single(Stage(plan, "DoublingLamination"));
        Assert.Equal("DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL",
            assembly.Notes);
    }

    [Fact]
    public void Untargeted_and_unroutable_charges_warn()
    {
        var charges = new[]
        {
            new ProductionChargeInput("FAN_HOLES", 1m, "", "", new[] { 9 }),
            new ProductionChargeInput("SANDBLASTING", 1m, "", "", Array.Empty<int>()),
        };
        var plan = Distribute(new[] { Dgu }, charges);

        Assert.Contains(plan.Warnings, w =>
            w.Contains("'FAN HOLES' is active but targets no spec group"));
        Assert.Contains(plan.Warnings, w =>
            w.Contains("Sandblasting") && w.Contains("track it manually"));
        Assert.Empty(Stage(plan, "HolesCutouts"));
    }

    [Fact]
    public void Items_are_ordered_spec_serial_stage_position()
    {
        var plan = Distribute(new[] { SguFt, Dgu });
        var keys = plan.Items.Select(i => i.UniqueKey).ToList();
        var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        // Spec 1 before spec 2; within a spec, serial then stage code.
        Assert.Equal("JOB-2026-0042|1|1|Glass|1", plan.Items[0].UniqueKey);
        Assert.Equal("JOB-2026-0042|2|1|Outer|1", plan.Items[2].UniqueKey);
    }

    // ── DguDimensionSemantics (WPF verbatim) ────────────────────────

    [Theory]
    [InlineData(1000, 2000, 0, 0, 1000, 2000, true)]    // equal-size fallback
    [InlineData(1000, 2000, 900, 1800, 900, 1800, true)] // overlap
    [InlineData(1000, 2000, 0, 500, 0, 0, false)]        // one axis only
    [InlineData(1000, 2000, 500, 0, 0, 0, false)]
    [InlineData(0, 0, 0, 0, 0, 0, false)]
    public void Inner_size_semantics(
        int w1, int h1, int w2, int h2, int ew, int eh, bool expected)
    {
        var ok = DguDimensionSemantics.TryResolveInnerSize(
            w1, h1, w2, h2, out var w, out var h);
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.Equal(ew, w);
            Assert.Equal(eh, h);
        }
    }

    // ── vocabularies ────────────────────────────────────────────────

    [Fact]
    public void Vocabularies_are_the_wpf_sets()
    {
        Assert.Equal(new[]
        {
            "Cutting", "HolesCutouts", "Polish", "EdgeDeletion",
            "Mitring", "Tempering", "Pairing", "DoublingLamination"
        }, ProductionStages.All);
        Assert.Equal(new[]
        {
            "Pending", "InProgress", "Completed", "Paused", "Rework",
            "Rejected", "Cancelled", "QcPending", "QcFailed"
        }, WorkItemStatuses.All);
        Assert.Equal("Holes & Cutouts", ProductionStages.StageLabel("HolesCutouts"));
        Assert.Equal("Doubling / Lamination",
            ProductionStages.StageLabel("DoublingLamination"));
        Assert.Equal(6, ProductionStages.Code("Tempering"));
    }

    [Fact]
    public void Pane_display_name_is_the_optimizer_merge_key()
    {
        var pane = new SpecPane("Outer", 6, "hp neutral 70 low e", "FT");
        Assert.Equal("6mm hp neutral 70 low e FT glass",
            ProductionDistributionEngine.PaneDisplayName(pane));
    }


    // ── reservations (Phase 9c, WPF MaterialReservationService) ─────

    [Fact]
    public void Reservation_key_is_the_deterministic_wpf_shape()
    {
        var sheet = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var session = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.Equal(
            "JOB-2026-0042|2|5|11111111-1111-1111-1111-111111111111|" +
            "22222222-2222-2222-2222-222222222222",
            ReservationKeys.Build(" JOB-2026-0042 ", 2, 5, sheet, session));
        Assert.Equal(
            "JOB-7|1|1|11111111-1111-1111-1111-111111111111|",
            ReservationKeys.Build("JOB-7", 1, 1, sheet, null));
    }

    [Theory]
    [InlineData("Reserved", true)]
    [InlineData("Allocated", true)]
    [InlineData("reserved", true)]
    [InlineData("Consumed", false)]     // consumed material no longer holds
    [InlineData("Released", false)]
    [InlineData("Cancelled", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Active_holds_are_reserved_or_allocated(
        string? status, bool expected)
        => Assert.Equal(expected, ReservationStatuses.IsActiveHold(status));

    [Fact]
    public void Reservation_vocabulary_is_the_wpf_set()
        => Assert.Equal(
            new[] { "Available", "Reserved", "Allocated", "Consumed",
                    "Released", "Cancelled" },
            ReservationStatuses.All);
}
