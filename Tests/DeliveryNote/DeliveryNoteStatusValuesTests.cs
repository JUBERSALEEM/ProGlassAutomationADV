using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using Xunit;

namespace ProGlassAutomation.Tests.DeliveryNote;

/// <summary>
/// Regression net for the Draft / Dispatching status split.
///
/// <para>
/// <see cref="DeliveryNoteStatus"/> has no <c>Dispatching</c> member, but
/// <c>Services/DeliveryNote/Mapper/DeliveryNoteMapper.cs</c> writes
/// <c>Status = "Dispatching"</c> in three places. Both spellings therefore
/// live in the same table for the same state.
/// </para>
///
/// <para>
/// <c>DashboardMetricsService</c> counted <c>Preparing</c> as
/// <c>Status == "Draft"</c> only, so every mapper-written note landed in
/// <b>neither</b> Preparing nor Dispatched — the two Overview figures did not
/// sum to the total. <see cref="DeliveryNoteStatusValues.IsPreparing"/> is the
/// fix; these tests pin the behaviour.
/// </para>
/// </summary>
public class DeliveryNoteStatusValuesTests
{
    [Theory]
    [InlineData("Draft")]
    [InlineData("Dispatching")]
    [InlineData("draft")]
    [InlineData("DISPATCHING")]
    [InlineData("  Draft  ")]
    public void IsPreparing_Matches_Both_Stored_Spellings(string stored)
    {
        Assert.True(DeliveryNoteStatusValues.IsPreparing(stored));
    }

    [Theory]
    [InlineData("Dispatched")]
    [InlineData("Delivered")]
    [InlineData("Signed")]
    [InlineData("Received")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsPreparing_Is_False_For_States_That_Left_The_Building(string? stored)
    {
        Assert.False(DeliveryNoteStatusValues.IsPreparing(stored));
    }

    [Theory]
    [InlineData("Dispatched")]
    [InlineData("Delivered")]
    [InlineData("Signed")]
    [InlineData("Received")]
    public void HasLeftTheBuilding_Is_True_After_Dispatch(string stored)
    {
        Assert.True(DeliveryNoteStatusValues.HasLeftTheBuilding(stored));
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Dispatching")]
    [InlineData("")]
    [InlineData(null)]
    public void HasLeftTheBuilding_Is_False_Before_Dispatch(string? stored)
    {
        Assert.False(DeliveryNoteStatusValues.HasLeftTheBuilding(stored));
    }

    /// <summary>
    /// The whole point of the fix: every status falls in exactly one bucket,
    /// so Preparing + Dispatched + (other) accounts for the whole table.
    /// </summary>
    [Theory]
    [InlineData("Draft")]
    [InlineData("Dispatching")]
    [InlineData("Dispatched")]
    [InlineData("Delivered")]
    [InlineData("Signed")]
    [InlineData("Received")]
    public void Every_Status_Is_In_Exactly_One_Bucket(string stored)
    {
        var buckets = 0;
        if (DeliveryNoteStatusValues.IsPreparing(stored)) buckets++;
        if (DeliveryNoteStatusValues.HasLeftTheBuilding(stored)) buckets++;

        Assert.Equal(1, buckets);
    }

    [Theory]
    [InlineData("Draft", "Dispatching")]
    [InlineData("Dispatching", "Dispatching")]
    [InlineData("draft", "Dispatching")]
    [InlineData("Dispatched", "Dispatched")]
    [InlineData("Delivered", "Delivered")]
    [InlineData("Signed", "Signed")]
    [InlineData("Received", "Received")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    public void Canonical_Collapses_The_Split(string? stored, string expected)
    {
        Assert.Equal(expected, DeliveryNoteStatusValues.Canonical(stored));
    }

    [Fact]
    public void Canonical_Never_Renders_The_Same_State_Twice()
    {
        var spellings = new[] { "Draft", "Dispatching", "draft", "DISPATCHING" };

        var distinct = spellings
            .Select(DeliveryNoteStatusValues.Canonical)
            .Distinct()
            .ToList();

        Assert.Single(distinct);
    }

    [Fact]
    public void Canonical_Matches_The_Meta_Display_Label()
    {
        // DeliveryNoteStatusMeta maps BOTH "Draft" and "Dispatching" to the
        // label "Dispatching" (DeliveryNoteStatusMeta.cs:20-21). Canonical
        // must agree, or a grouped chart and a status chip would disagree.
        Assert.Equal("Dispatching", DeliveryNoteStatusValues.Canonical("Draft"));
        Assert.Equal("Dispatching", DeliveryNoteStatusValues.Canonical("Dispatching"));
    }

    [Fact]
    public void Is_Compares_Case_Insensitively_And_Trims()
    {
        Assert.True(DeliveryNoteStatusValues.Is("  DISPATCHED ", DeliveryNoteStatusValues.Dispatched));
        Assert.False(DeliveryNoteStatusValues.Is("Dispatched", DeliveryNoteStatusValues.Delivered));
        Assert.False(DeliveryNoteStatusValues.Is(null, DeliveryNoteStatusValues.Draft));
    }

    [Fact]
    public void Constants_Cover_Every_Displayed_Status()
    {
        // WORKFLOW.md lists the delivery lifecycle as:
        //   Draft · Dispatching · Dispatched · Signed · Received · Delivered
        Assert.Equal(new[] { "Draft", "Dispatching", "Dispatched", "Signed", "Received", "Delivered" },
            new[]
            {
                DeliveryNoteStatusValues.Draft,
                DeliveryNoteStatusValues.Dispatching,
                DeliveryNoteStatusValues.Dispatched,
                DeliveryNoteStatusValues.Signed,
                DeliveryNoteStatusValues.Received,
                DeliveryNoteStatusValues.Delivered,
            });
    }
}
