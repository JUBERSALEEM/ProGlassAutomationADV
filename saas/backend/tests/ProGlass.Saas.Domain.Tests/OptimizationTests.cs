using ProGlass.Saas.Domain.Optimization;
using Xunit;
using static ProGlass.Saas.Domain.Optimization.GuillotinePacker;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF goldens for the GuillotinePacker core port. The packer is
/// deterministic, so the small cases below are hand-traced through the
/// ported algorithm (thin margins 15/15/15/15, kerf 3, usable 3180×2220
/// on a 3210×2250 rack); the bigger cases assert the structural
/// invariants the WPF's industrial safety net checks (bounds, no
/// overlap, no double placement, conservation) — never invented yield
/// figures.
/// </summary>
public sealed class OptimizationTests
{
    private static readonly StockSize Rack = new(3210, 2250, 6, "Clear");
    private static readonly MarginProfile Thin = MarginProfile.DefaultFor(6);

    private static CutPiece P(int w, int h, string ref_ = "A", bool allowRotate = true) =>
        new("JOB-1", ref_, "6mm Clear Annealed Glass", "Glass", "Clear", 6,
            w, h, allowRotate, ref_);

    [Fact]
    public void Two_pieces_trace_the_hand_computed_layout()
    {
        var result = PackQuick(
            new[] { P(1000, 2000, "A"), P(950, 1150, "B") },
            new[] { Rack }, Thin, RotationPolicy.Free90);

        Assert.Empty(result.ValidationProblems);
        Assert.True(result.GeometryVerified);
        Assert.True(result.ConservationVerified);
        Assert.Single(result.Sheets);

        var sheet = result.Sheets[0];
        Assert.Equal(15, sheet.UsableX);
        Assert.Equal(15, sheet.UsableY);
        Assert.Equal(3180, sheet.UsableWidth);
        Assert.Equal(2220, sheet.UsableHeight);

        // A: top-left of the usable rectangle, unrotated.
        var a = sheet.Pieces[0];
        Assert.Equal(15, a.X);
        Assert.Equal(15, a.Y);
        Assert.Equal(1000, a.WidthMm);
        Assert.Equal(2000, a.HeightMm);
        Assert.False(a.Rotated);

        // B: right column (short-side best fit), unrotated (rotating
        // would leave a 1027 strip vs 850).
        var b = sheet.Pieces[1];
        Assert.Equal(1018, b.X);   // 15 + 1000 + kerf 3
        Assert.Equal(15, b.Y);
        Assert.Equal(950, b.WidthMm);
        Assert.Equal(1150, b.HeightMm);
        Assert.False(b.Rotated);

        // The cut sequence: H@2015 stage 1, V@1015 stage 2, then B's cuts.
        Assert.Equal(4, sheet.Cuts.Count);
        Assert.Equal(CutAxis.Horizontal, sheet.Cuts[0].Axis);
        Assert.Equal(2015, sheet.Cuts[0].AtMm);
        Assert.Equal(1, sheet.Cuts[0].Stage);
        Assert.Equal(CutAxis.Vertical, sheet.Cuts[1].Axis);
        Assert.Equal(1015, sheet.Cuts[1].AtMm);
        Assert.Equal(2, sheet.Cuts[1].Stage);

        // (1000×2000 + 950×1150) / (3210×2250).
        Assert.Equal(42.82m, result.UtilizationPct);
        Assert.Equal(3092500m / 1_000_000m, result.TotalUsedSqm);
    }

    [Fact]
    public void Unplaceable_piece_is_reported_not_dropped()
    {
        var result = PackQuick(
            new[] { P(800, 500) },
            new[] { new StockSize(600, 400, 6) }, Thin, RotationPolicy.Free90);

        Assert.Empty(result.Sheets);
        Assert.Single(result.Unplaced);
        Assert.True(result.ConservationVerified);   // 0 placed + 1 unplaced = 1
    }

    [Fact]
    public void Adaptive_rotation_rotates_only_when_needed()
    {
        // Usable 2220×3180 (portrait rack): 2500 wide does not fit as
        // entered, 1500×2500 rotated does.
        var portrait = new StockSize(2250, 3210, 6);

        var locked = PackQuick(
            new[] { P(2500, 1500) }, new[] { portrait }, Thin,
            RotationPolicy.Locked);
        Assert.Empty(locked.Sheets);
        Assert.Single(locked.Unplaced);

        var free = PackQuick(
            new[] { P(2500, 1500) }, new[] { portrait }, Thin,
            RotationPolicy.Free90);
        Assert.Empty(free.Unplaced);
        var placed = free.Sheets[0].Pieces[0];
        Assert.True(placed.Rotated);
        Assert.Equal(1500, placed.WidthMm);
        Assert.Equal(2500, placed.HeightMm);
    }

    [Fact]
    public void Smallest_stock_that_fits_is_opened_first()
    {
        var small = new StockSize(3210, 2250, 6);
        var large = new StockSize(6000, 3210, 6);
        var result = PackQuick(
            new[] { P(3000, 2000) }, new[] { large, small }, Thin,
            RotationPolicy.Free90);

        Assert.Single(result.Sheets);
        Assert.Equal(3210, result.Sheets[0].Stock.WidthMm);
    }

    [Fact]
    public void MaxSheets_caps_the_pool()
    {
        var capped = new StockSize(600, 400, 6, MaxSheets: 1);
        var result = PackQuick(
            new[] { P(550, 350, "A"), P(550, 350, "B") },
            new[] { capped }, Thin, RotationPolicy.Free90);

        Assert.Single(result.Sheets);
        Assert.Single(result.Unplaced);
        Assert.Equal("B", result.Unplaced[0].GlassRef);
    }

    [Fact]
    public void House_margin_table_is_the_owner_confirmed_one()
    {
        var thin = MarginProfile.DefaultFor(6);
        Assert.Equal((15, 15, 15, 15, 3, 15),
            (thin.LeftMm, thin.RightMm, thin.TopMm, thin.BottomMm,
             thin.KerfMm, thin.MinBreakoutMm));

        var medium = MarginProfile.DefaultFor(7);   // rounds UP to 8mm
        Assert.Equal((30, 30, 30, 30, 3, 30),
            (medium.LeftMm, medium.RightMm, medium.TopMm, medium.BottomMm,
             medium.KerfMm, medium.MinBreakoutMm));

        // Thick: no trim on the top edge, kerf 4, breakout switchable.
        var thick = MarginProfile.DefaultFor(10);
        Assert.Equal((50, 50, 0, 50, 4, 50),
            (thick.LeftMm, thick.RightMm, thick.TopMm, thick.BottomMm,
             thick.KerfMm, thick.MinBreakoutMm));
        Assert.Equal(40, MarginProfile.DefaultFor(12, 40).MinBreakoutMm);
        Assert.Equal("Thin", MarginProfile.BucketFor(0));   // unreadable label
        Assert.True(MarginProfile.HasBreakoutChoice(12));
        Assert.False(MarginProfile.HasBreakoutChoice(6));
    }

    [Fact]
    public void Split_remainder_feeds_the_ledger_and_the_cut_list()
    {
        // HorizontalFirst: the bottom strip keeps the full width.
        var output = new List<FreeRect>();
        SplitRemainder(new FreeRect(0, 0, 1000, 1000), 400, 300, 3, 0,
            SplitMode.HorizontalFirst, output, null);
        Assert.Equal(2, output.Count);
        Assert.Equal((0, 303, 1000, 697, 1),
            (output[0].X, output[0].Y, output[0].W, output[0].H, output[0].Depth));
        Assert.Equal((403, 0, 597, 300, 2),
            (output[1].X, output[1].Y, output[1].W, output[1].H, output[1].Depth));

        // VerticalFirst: the right strip keeps the full height.
        output = new List<FreeRect>();
        SplitRemainder(new FreeRect(0, 0, 1000, 1000), 400, 300, 3, 0,
            SplitMode.VerticalFirst, output, null);
        Assert.Equal((403, 0, 597, 1000, 1),
            (output[0].X, output[0].Y, output[0].W, output[0].H, output[0].Depth));
        Assert.Equal((0, 303, 400, 697, 2),
            (output[1].X, output[1].Y, output[1].W, output[1].H, output[1].Depth));

        // MinBreakout drops sub-breakout remainders (trim waste, not
        // fictional stock): the 597×300 right column is dropped (300 <
        // 500), the 1000×697 bottom strip is kept.
        output = new List<FreeRect>();
        SplitRemainder(new FreeRect(0, 0, 1000, 1000), 400, 300, 3, 500,
            SplitMode.HorizontalFirst, output, null);
        Assert.Single(output);
        Assert.Equal((0, 303, 1000, 697),
            (output[0].X, output[0].Y, output[0].W, output[0].H));
    }

    [Fact]
    public void Leftovers_are_derived_from_the_cut_sequence()
    {
        var result = PackQuick(
            new[] { P(1000, 2000, "A"), P(950, 1150, "B") },
            new[] { Rack }, Thin, RotationPolicy.Free90);
        var sheet = result.Sheets[0];

        Assert.NotEmpty(sheet.Leftovers);
        // The bottom strip (15, 2018, 3180, 217) holds no piece and its
        // min side 217 ≥ breakout 15 → reusable.
        Assert.Contains(sheet.Leftovers,
            l => l.X == 15 && l.Y == 2018 && l.WidthMm == 3180
                 && l.HeightMm == 217 && l.IsUsable);
    }

    [Fact]
    public void Big_random_free_runs_pass_the_safety_net()
    {
        // 120 mixed pieces, deterministic sequence — assert the
        // invariants, never a specific yield.
        var rng = new Random(20260927);
        var pieces = Enumerable.Range(1, 120)
            .Select(i => P(300 + rng.Next(0, 900), 300 + rng.Next(0, 900),
                $"P{i:000}"))
            .ToList();

        var result = PackQuick(pieces, new[] { Rack }, Thin, RotationPolicy.Free90);

        Assert.True(result.GeometryVerified, string.Join("; ", result.ValidationProblems));
        Assert.True(result.ConservationVerified);
        Assert.Equal(pieces.Count,
            result.Sheets.Sum(s => s.Pieces.Count) + result.Unplaced.Count);
        Assert.All(result.Sheets, s =>
        {
            Assert.NotEmpty(s.Cuts);
            Assert.NotEmpty(s.Leftovers);
        });
        Assert.True(result.UtilizationPct > 0);
    }
}
