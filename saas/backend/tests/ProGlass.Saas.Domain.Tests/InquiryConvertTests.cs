// ═══════════════════════════════════════════════════════════════════════
// GOLDEN VALUES — the inquiry → PI conversion decisions + the duplicate
// snapshot scrub, pinned to the WPF sources (read 2026-09-27):
//   Services/ProformaInvoice/InquiryBoqConversion.cs (pure decisions,
//     pinned by WPF Tests/Inquiry/InquiryBoqConversionTests.cs)
//   Services/Inquiry/InquiryWorkflowService.cs (CanTransition table)
//   Models/Inquiry/InquiryRecord.cs (InquiryStatuses + IsImportable)
//   Services/ProformaInvoice/ProformaDuplicateSnapshotScrub.cs
//   ViewModels/…/SavedInvoicesListViewModel.ScrubIdsForDuplicate
// Expected values derived from the pinned WPF rules directly.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class InquiryConvertTests
{
    // ── the status machine (InquiryWorkflowService.CanTransition) ─────

    [Theory]
    [InlineData("Received", "Prepared", true)]
    [InlineData("Received", "Hold", true)]
    [InlineData("Received", "Sent", false)]       // must pass through Prepared
    [InlineData("Prepared", "Sent", true)]
    [InlineData("Prepared", "Cancelled", true)]
    [InlineData("Sent", "Confirmed", true)]
    [InlineData("Confirmed", "Sent", false)]      // Confirmed never goes back
    [InlineData("Confirmed", "Cancelled", true)]
    [InlineData("Hold", "Prepared", true)]
    [InlineData("Hold", "Sent", false)]
    [InlineData("Cancelled", "Prepared", false)]  // terminal
    public void CanTransition_matches_the_wpf_table(
        string from, string to, bool expected)
    {
        Assert.Equal(expected, InquiryWorkflow.CanTransition(from, to));
    }

    [Fact]
    public void Statuses_normalize_the_wpf_aliases()
    {
        Assert.Equal("Hold", InquiryStatuses.Normalize("On Hold"));
        Assert.Equal("Cancelled", InquiryStatuses.Normalize("Canceled"));
        Assert.Equal("Cancelled", InquiryStatuses.Normalize("cancelled"));
        Assert.Equal("Received", InquiryStatuses.Normalize("gibberish"));
        Assert.Equal("Received", InquiryStatuses.Normalize(""));
    }

    // ── glass-type normalization (DGU+LAM is checked FIRST) ───────────

    [Theory]
    [InlineData("DGU+LAM", "DGU+LAM")]
    [InlineData("dgu+lam", "DGU+LAM")]            // case-normalised
    [InlineData("DGU", "DGU")]
    [InlineData("LAM", "LAM")]
    [InlineData("SGU", "SGU")]
    [InlineData("Double Glazed", "")]             // unknown → refused
    [InlineData("", "")]
    public void NormalizeGlassType_maps_the_pi_vocabulary(string input, string expected)
    {
        Assert.Equal(expected, InquiryBoqConversion.NormalizeGlassType(input));
    }

    // ── the BOQ import decisions ──────────────────────────────────────

    [Fact]
    public void Importable_rows_survive_in_boq_order()
    {
        var rows = InquiryBoqConversion.ToImportRows(new List<InquiryBoqLine>
        {
            new(SortOrder: 3, GlassType: "SGU", SpecName: "6mm Clear FT Glass",
                WidthMm: 1000, HeightMm: 1000, Qty: 2, RatePerSqm: 0m),
            new(SortOrder: 1, GlassType: "DGU", SpecName: "6mm Bronze + 12mm ASP + 6mm Clear",
                OuterGlassColor: "Bronze", InnerGlassColor: "Clear",
                WidthMm: 2000, HeightMm: 1500, Qty: 3, RatePerSqm: 125.5m),
            new(SortOrder: 2, GlassType: "LAM", SpecName: "8mm Grey + 1.52 PVB + 8mm Clear",
                WidthMm: 1500, HeightMm: 1000, Qty: 1, RatePerSqm: 210m),
        });

        // BOQ order (SortOrder), not list order.
        Assert.Equal(3, rows.Count);
        Assert.Equal("6mm Bronze + 12mm ASP + 6mm Clear", rows[0].SpecName);
        Assert.Equal("8mm Grey + 1.52 PVB + 8mm Clear", rows[1].SpecName);
        Assert.Equal("6mm Clear FT Glass", rows[2].SpecName);

        // A quoted rate carries (HasRate); an unquoted line does not force 0.
        Assert.True(rows[0].HasRate);
        Assert.Equal(125.5m, rows[0].RatePerSqm);
        Assert.False(rows[2].HasRate);

        Assert.Equal("Bronze", rows[0].OuterGlassColor);
        Assert.Equal("Clear", rows[0].InnerGlassColor);
    }

    [Fact]
    public void Partial_rows_stay_on_the_inquiry_as_notes()
    {
        var rows = InquiryBoqConversion.ToImportRows(new List<InquiryBoqLine>
        {
            new(SortOrder: 1, GlassType: "SGU", SpecName: "",
                WidthMm: 1000, HeightMm: 1000, Qty: 2),          // no spec name
            new(SortOrder: 2, GlassType: "SGU", SpecName: "6mm Clear",
                WidthMm: 0, HeightMm: 1000, Qty: 2),             // no width
            new(SortOrder: 3, GlassType: "SGU", SpecName: "6mm Clear",
                WidthMm: 1000, HeightMm: 1000, Qty: 0),          // no qty
            new(SortOrder: 4, GlassType: "SGU", SpecName: "6mm Clear",
                WidthMm: 1000, HeightMm: 1000, Qty: 2),          // complete
        });

        var row = Assert.Single(rows);
        Assert.Equal("6mm Clear", row.SpecName);
    }

    [Fact]
    public void An_unknown_build_is_refused_not_defaulted()
    {
        // A complete row with an unrecognized glass type must not inherit
        // whatever default a grid group would carry.
        var rows = InquiryBoqConversion.ToImportRows(new List<InquiryBoqLine>
        {
            new(SortOrder: 1, GlassType: "Curtain Wall", SpecName: "Custom thing",
                WidthMm: 1000, HeightMm: 1000, Qty: 1),
        });

        Assert.Empty(rows);
    }

    [Fact]
    public void IsImportable_requires_spec_dimensions_and_quantity()
    {
        Assert.True(new InquiryBoqLine(
            SpecName: "x", WidthMm: 1, HeightMm: 1, Qty: 1).IsImportable);
        Assert.False(new InquiryBoqLine(
            SpecName: "x", WidthMm: 1, HeightMm: 1, Qty: 0).IsImportable);
        Assert.False(new InquiryBoqLine(
            SpecName: "", WidthMm: 1, HeightMm: 1, Qty: 1).IsImportable);
    }

    // ── the duplicate snapshot scrub ──────────────────────────────────

    [Fact]
    public void Scrub_detaches_the_inquiry_and_rewrites_the_pi_number()
    {
        const string json = """
            {"Company":{"InquiryId":42,"ProformaId":"PI-2026-0012","CompanyName":"ACME"},
             "Grid":{"Groups":[]}}
            """;

        var scrubbed = DuplicateSnapshotScrub.TryDetach(json, "PI-2026-0013");

        Assert.NotNull(scrubbed);
        Assert.Contains("\"InquiryId\":null", scrubbed);
        Assert.Contains("PI-2026-0013", scrubbed);
        // The untouched fields survive byte-for-byte.
        Assert.Contains("ACME", scrubbed);
        Assert.Contains("Grid", scrubbed);
    }

    [Fact]
    public void Scrub_refuses_a_snapshot_without_a_company_block()
    {
        // Never return the original JSON — a failed scrub must not keep
        // the source inquiry link.
        Assert.Null(DuplicateSnapshotScrub.TryDetach("{\"Grid\":{}}", "PI-1"));
        Assert.Null(DuplicateSnapshotScrub.TryDetach("not json at all", "PI-1"));
    }

    [Fact]
    public void Scrub_of_a_blank_snapshot_yields_empty()
    {
        Assert.Equal(string.Empty, DuplicateSnapshotScrub.TryDetach(null, "PI-1"));
        Assert.Equal(string.Empty, DuplicateSnapshotScrub.TryDetach("", "PI-1"));
    }
}
