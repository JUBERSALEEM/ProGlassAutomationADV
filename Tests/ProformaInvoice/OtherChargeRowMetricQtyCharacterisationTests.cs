using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice;

/// <summary>
/// CHARACTERISATION tests for <see cref="OtherChargeRow.MetricQty"/>.
///
/// <para>
/// <b>These tests record what the code does today, not what it should do.</b>
/// They are step 1 of Phase 4 in <c>docs/REMEDIATION_PLAN.md</c>, which says
/// the current output must be captured for every row type <i>before</i> the
/// hardcoded delivery quantity is replaced. Nothing here endorses the
/// behaviour — the delivery branch is the only acknowledged <c>HACK</c> marker
/// in the codebase and it sits on a money path.
/// </para>
///
/// <para>
/// <b>How to use them.</b> Every test must pass unchanged before any Phase 4
/// edit. After the <c>PER_ROUND</c> change lands, the ONLY tests permitted to
/// fail are the ones explicitly marked <c>WILL CHANGE</c> below — and they
/// should be rewritten to the new intent, not deleted.
/// </para>
///
/// <para>
/// Current logic, transcribed from <c>OtherChargeRow.cs:318-330</c>:
/// <code>
/// if (IsDeliveryRow) return 1m;                       // TemplateKey == "DELIVERY"
/// if (IsPolishRow)                                    // "POLISH" | "POLISH_MATCHED_LAM"
///     return IsCustomPolish ? _customPolishQty : _calculatedPolishQty;
/// return _targetSelection?.ComputedValue ?? 0m;
/// </code>
/// </para>
/// </summary>
public class OtherChargeRowMetricQtyCharacterisationTests
{
    // ── Fixture builders ───────────────────────────────────────────────

    /// <summary>
    /// A selection whose computed value is <paramref name="sqm1"/> — one
    /// checked spec, metric <c>SQM1</c>. <c>SelectedMetric</c> is assigned
    /// directly rather than picked from <c>AvailableMetrics</c> so the fixture
    /// does not depend on what <c>MetricOption.ForUnit</c> happens to return.
    /// </summary>
    private static SpecTargetSelection Selection(decimal sqm1, bool isChecked = true)
    {
        var sel = new SpecTargetSelection(OtherChargeUnit.Sqm);
        sel.AvailableSpecs.Add(new SpecCheckOption
        {
            SpecGroupNumber = 1,
            Sqm1 = sqm1,
            IsChecked = isChecked,
        });
        sel.SelectedMetric = new MetricOption { Key = "SQM1" };
        return sel;
    }

    private static PolishSubTypeMeta CustomSubType() => new()
    {
        Type = PolishSubType.Custom,
        DisplayLabel = "Custom (Manual)",
    };

    // ── Branch 1: the delivery hack ────────────────────────────────────

    /// <summary>
    /// <b>WILL CHANGE under Phase 4.</b> This is the hack. A delivery row
    /// reports 1 trip whatever the operator selected.
    /// </summary>
    [Fact]
    public void Delivery_Row_Returns_One_Even_When_A_Target_Is_Selected()
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "DELIVERY",
            TargetSelection = Selection(47.5m),
        };

        Assert.Equal(1m, row.MetricQty);
    }

    /// <summary>
    /// <b>WILL CHANGE under Phase 4.</b> The selected value is not merely
    /// unused — changing it cannot move the result. This is the assertion
    /// that pins "ignores TargetSelection entirely" from the code comment.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12.75)]
    [InlineData(47.5)]
    [InlineData(1000)]
    public void Delivery_Row_Ignores_The_Selected_Value_Entirely(double selected)
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "DELIVERY",
            TargetSelection = Selection((decimal)selected),
        };

        Assert.Equal(1m, row.MetricQty);
    }

    [Fact]
    public void Delivery_Row_Returns_One_With_No_Selection_At_All()
    {
        var row = new OtherChargeRow { TemplateKey = "DELIVERY" };

        Assert.True(row.IsDeliveryRow);
        Assert.Equal(1m, row.MetricQty);
    }

    /// <summary>
    /// A multi-trip delivery cannot currently be billed on this row. This is
    /// the user-facing consequence Phase 4 exists to remove — recorded here
    /// so the gap is a failing-by-design assertion rather than folklore.
    /// </summary>
    [Fact]
    public void Delivery_Row_Cannot_Express_More_Than_One_Trip()
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "DELIVERY",
            TargetSelection = Selection(3m),   // operator asks for 3 trips
        };

        Assert.NotEqual(3m, row.MetricQty);
        Assert.Equal(1m, row.MetricQty);
    }

    // ── Branch 2: polish rows ──────────────────────────────────────────

    [Theory]
    [InlineData("POLISH")]
    [InlineData("POLISH_MATCHED_LAM")]
    public void Polish_Row_With_Predefined_Subtype_Uses_The_Calculated_Qty(string templateKey)
    {
        var row = new OtherChargeRow
        {
            TemplateKey = templateKey,
            SelectedPolishSubType = new PolishSubTypeMeta
            {
                Type = PolishSubType.AllSidePolish,
                DisplayLabel = "All side polish",
            },
            TargetSelection = Selection(99m),   // must be ignored on polish rows
        };
        row.SetCalculatedPolishQty(18.4m);

        Assert.True(row.IsPolishRow);
        Assert.False(row.IsCustomPolish);
        Assert.Equal(18.4m, row.MetricQty);
    }

    [Theory]
    [InlineData("POLISH")]
    [InlineData("POLISH_MATCHED_LAM")]
    public void Polish_Row_With_No_Subtype_Selected_Falls_To_The_Calculated_Qty(string templateKey)
    {
        var row = new OtherChargeRow { TemplateKey = templateKey };
        row.SetCalculatedPolishQty(7.25m);

        Assert.False(row.IsCustomPolish);
        Assert.Equal(7.25m, row.MetricQty);
    }

    [Fact]
    public void Custom_Polish_Row_Uses_The_User_Typed_Qty()
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "POLISH",
            SelectedPolishSubType = CustomSubType(),
            CustomPolishQty = 5.5m,
        };
        row.SetCalculatedPolishQty(99m);   // must lose to the custom value

        Assert.True(row.IsCustomPolish);
        Assert.Equal(5.5m, row.MetricQty);
    }

    [Fact]
    public void Custom_Polish_Defaults_To_Zero_Before_The_User_Types()
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "POLISH",
            SelectedPolishSubType = CustomSubType(),
        };

        Assert.Equal(0m, row.MetricQty);
    }

    // ── Branch 3: everything else reads the selection ──────────────────

    [Fact]
    public void Ordinary_Row_Reads_The_Target_Selection()
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "SILICONE",
            TargetSelection = Selection(23.5m),
        };

        Assert.Equal(23.5m, row.MetricQty);
    }

    [Fact]
    public void Ordinary_Row_Sums_Only_Checked_Specs()
    {
        var sel = new SpecTargetSelection(OtherChargeUnit.Sqm);
        sel.AvailableSpecs.Add(new SpecCheckOption { SpecGroupNumber = 1, Sqm1 = 10m, IsChecked = true });
        sel.AvailableSpecs.Add(new SpecCheckOption { SpecGroupNumber = 2, Sqm1 = 20m, IsChecked = false });
        sel.AvailableSpecs.Add(new SpecCheckOption { SpecGroupNumber = 3, Sqm1 = 30m, IsChecked = true });
        sel.SelectedMetric = new MetricOption { Key = "SQM1" };

        var row = new OtherChargeRow { TemplateKey = "SILICONE", TargetSelection = sel };

        Assert.Equal(40m, row.MetricQty);
    }

    [Fact]
    public void Ordinary_Row_With_Nothing_Checked_Is_Zero()
    {
        var row = new OtherChargeRow
        {
            TemplateKey = "SILICONE",
            TargetSelection = Selection(23.5m, isChecked: false),
        };

        Assert.Equal(0m, row.MetricQty);
    }

    [Fact]
    public void Ordinary_Row_With_No_Selection_Is_Zero()
    {
        var row = new OtherChargeRow { TemplateKey = "SILICONE" };

        Assert.Equal(0m, row.MetricQty);
    }

    [Fact]
    public void Blank_Template_Key_With_No_Selection_Is_Zero()
    {
        var row = new OtherChargeRow();

        Assert.False(row.IsDeliveryRow);
        Assert.False(row.IsPolishRow);
        Assert.Equal(0m, row.MetricQty);
    }

    // ── Branch precedence ──────────────────────────────────────────────

    /// <summary>
    /// The delivery check runs first, so a row cannot be both. Recorded
    /// because a <c>PER_ROUND</c> source must not accidentally make
    /// <c>IsDeliveryRow</c> false for existing documents.
    /// </summary>
    [Fact]
    public void Delivery_Check_Outranks_The_Polish_Check()
    {
        var row = new OtherChargeRow { TemplateKey = "DELIVERY" };

        Assert.True(row.IsDeliveryRow);
        Assert.False(row.IsPolishRow);
    }

    /// <summary>
    /// The predicate is an exact key match — no case folding, no trimming.
    /// A row keyed <c>"delivery"</c> or <c>"DELIVERY "</c> is NOT a delivery
    /// row and falls through to the selection. Pinned because Phase 4 must
    /// not widen this comparison while it is in there.
    /// </summary>
    [Theory]
    [InlineData("delivery")]
    [InlineData("DELIVERY ")]
    [InlineData(" Delivery")]
    [InlineData("DELIVERYTRIP")]
    public void Delivery_Predicate_Is_An_Exact_Case_Sensitive_Match(string templateKey)
    {
        var row = new OtherChargeRow
        {
            TemplateKey = templateKey,
            TargetSelection = Selection(12m),
        };

        Assert.False(row.IsDeliveryRow);
        Assert.Equal(12m, row.MetricQty);
    }

    // ── The invariant Phase 4 must preserve ────────────────────────────

    /// <summary>
    /// Whatever happens to the delivery branch, no other row type may move.
    /// This is the acceptance test for Phase 4 step 5: "the only differences
    /// permitted are on documents that explicitly opt into PER_ROUND".
    /// </summary>
    [Theory]
    [InlineData("SILICONE", 23.5)]
    [InlineData("GASKET", 0)]
    [InlineData("", 0)]
    public void Non_Delivery_Rows_Must_Not_Change_Under_Phase_4(string templateKey, double expected)
    {
        var row = new OtherChargeRow
        {
            TemplateKey = templateKey,
            TargetSelection = Selection((decimal)expected),
        };

        Assert.Equal((decimal)expected, row.MetricQty);
    }
}
