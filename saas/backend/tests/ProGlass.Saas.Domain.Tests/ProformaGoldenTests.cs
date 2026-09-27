// ═══════════════════════════════════════════════════════════════════════
// GOLDEN VALUES — ported from the WPF's own tests and in-code rules (read
// 2026-09-27). These are the business's numbers and tables; never
// re-derived here:
//   Tests/ProformaInvoice/ProformaIdHardeningTests.cs  — seed/numbering
//   Services/ProformaInvoice/InvoiceLifecycleStateMachine.cs — transition
//     table (verbatim, including the legacy "Pending"/"Active" mappings)
//   Services/ProformaInvoice/ProformaInvoiceRepository.SuggestNextIdAsync
//     — BUG-08/09/11 + V-1 overflow degradation examples
//   ViewModels/ProformaInvoice/Step5DataGrid/GlassItemRow.RecalculateAll
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class ProformaStatusesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PI-2026-000")]
    [InlineData("pi-2026-000")]
    [InlineData("PI-1999-000")]
    public void Unallocated_seed_and_blank_are_not_persistable(string? id)
    {
        Assert.True(ProformaStatuses.IsUnallocatedSeed(id));
    }

    [Theory]
    [InlineData("PI-2026-001")]
    [InlineData("PI-2026-0001")]
    [InlineData("PI-2026-00")]
    [InlineData("INV-42")]
    [InlineData("PI-2026-000-R1")]
    public void Real_or_custom_ids_are_not_treated_as_seed(string id)
    {
        Assert.False(ProformaStatuses.IsUnallocatedSeed(id));
    }

    [Fact]
    public void Canonical_All_includes_Cancelled_and_not_Void()
    {
        Assert.Contains(ProformaStatuses.Cancelled, ProformaStatuses.All);
        Assert.Contains(ProformaStatuses.Rejected, ProformaStatuses.All);
        Assert.DoesNotContain("Void", ProformaStatuses.All);
    }

    [Fact]
    public void Normalize_canonicalizes_known_and_passes_unknown_through()
    {
        Assert.Equal("Sent", ProformaStatuses.Normalize("sent"));
        Assert.Equal("Draft", ProformaStatuses.Normalize("  DRAFT "));
        // Unknown passes through as-typed — never silently re-mapped.
        Assert.Equal("Weird", ProformaStatuses.Normalize("Weird"));
        Assert.Equal("", ProformaStatuses.Normalize(null));
    }
}

public class InvoiceLifecycleTests
{
    // ── the transition table, cell by cell ─────────────────────────

    [Theory]
    [InlineData(InvoiceLifecycleState.Draft, InvoiceLifecycleState.PendingApproval)]
    [InlineData(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Approved)]
    [InlineData(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Rejected)]
    [InlineData(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceLifecycleState.Rejected, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceLifecycleState.Approved, InvoiceLifecycleState.Sent)]
    [InlineData(InvoiceLifecycleState.Sent, InvoiceLifecycleState.Confirmed)]
    [InlineData(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Cancelled)]
    [InlineData(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Cancelled)]
    [InlineData(InvoiceLifecycleState.Rejected, InvoiceLifecycleState.Cancelled)]
    [InlineData(InvoiceLifecycleState.Approved, InvoiceLifecycleState.Cancelled)]
    public void Valid_transitions(InvoiceLifecycleState from, InvoiceLifecycleState to)
    {
        Assert.True(InvoiceLifecycle.CanTransition(from, to));
    }

    [Theory]
    [InlineData(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Approved)]
    [InlineData(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Sent)]
    [InlineData(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Confirmed)]
    [InlineData(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Sent)]
    [InlineData(InvoiceLifecycleState.Approved, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceLifecycleState.Sent, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceLifecycleState.Sent, InvoiceLifecycleState.Approved)]
    [InlineData(InvoiceLifecycleState.Confirmed, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceLifecycleState.Confirmed, InvoiceLifecycleState.Cancelled)]
    [InlineData(InvoiceLifecycleState.Cancelled, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceLifecycleState.Cancelled, InvoiceLifecycleState.Sent)]
    public void Invalid_transitions(InvoiceLifecycleState from, InvoiceLifecycleState to)
    {
        Assert.False(InvoiceLifecycle.CanTransition(from, to));
    }

    [Fact]
    public void Terminal_states_have_no_transitions()
    {
        Assert.True(InvoiceLifecycle.IsTerminalState(InvoiceLifecycleState.Cancelled));
        Assert.True(InvoiceLifecycle.IsTerminalState(InvoiceLifecycleState.Confirmed));
        Assert.Empty(InvoiceLifecycle.GetValidTransitions(InvoiceLifecycleState.Cancelled));
        Assert.Empty(InvoiceLifecycle.GetValidTransitions(InvoiceLifecycleState.Confirmed));
    }

    [Fact]
    public void Only_Draft_and_Rejected_are_editable()
    {
        Assert.True(InvoiceLifecycle.IsEditable(InvoiceLifecycleState.Draft));
        Assert.True(InvoiceLifecycle.IsEditable(InvoiceLifecycleState.Rejected));
        Assert.False(InvoiceLifecycle.IsEditable(InvoiceLifecycleState.PendingApproval));
        Assert.False(InvoiceLifecycle.IsEditable(InvoiceLifecycleState.Approved));
        Assert.False(InvoiceLifecycle.IsEditable(InvoiceLifecycleState.Sent));
    }

    [Fact]
    public void Approval_requiring_transitions_are_flagged()
    {
        Assert.True(InvoiceLifecycle.RequiresApproval(
            InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Approved));
        Assert.True(InvoiceLifecycle.RequiresApproval(
            InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Rejected));
        // Cancelling an APPROVED invoice is the manager-gated cancel.
        Assert.True(InvoiceLifecycle.RequiresApproval(
            InvoiceLifecycleState.Approved, InvoiceLifecycleState.Cancelled));
        Assert.False(InvoiceLifecycle.RequiresApproval(
            InvoiceLifecycleState.Draft, InvoiceLifecycleState.PendingApproval));
        Assert.False(InvoiceLifecycle.RequiresApproval(
            InvoiceLifecycleState.Approved, InvoiceLifecycleState.Sent));
    }

    [Fact]
    public void Legacy_status_strings_parse()
    {
        Assert.True(InvoiceLifecycle.TryParseState("Pending", out var pending));
        Assert.Equal(InvoiceLifecycleState.PendingApproval, pending);

        Assert.True(InvoiceLifecycle.TryParseState("Active", out var active));
        Assert.Equal(InvoiceLifecycleState.Sent, active);

        Assert.True(InvoiceLifecycle.TryParseState("pendingapproval", out var ci));
        Assert.Equal(InvoiceLifecycleState.PendingApproval, ci);

        Assert.False(InvoiceLifecycle.TryParseState("Bogus", out _));
        Assert.False(InvoiceLifecycle.TryParseState(null, out _));
        Assert.False(InvoiceLifecycle.TryParseState("", out _));
    }

    [Fact]
    public void NormalizeStatus_maps_legacy_and_defaults_unknown_to_Draft()
    {
        Assert.Equal("PendingApproval", InvoiceLifecycle.NormalizeStatus("Pending"));
        Assert.Equal("Sent", InvoiceLifecycle.NormalizeStatus("Active"));
        Assert.Equal("Draft", InvoiceLifecycle.NormalizeStatus("Bogus"));
    }

    [Fact]
    public void Transition_descriptions_are_the_wpf_labels()
    {
        Assert.Equal("Submit for approval", InvoiceLifecycle.GetTransitionDescription(
            InvoiceLifecycleState.Draft, InvoiceLifecycleState.PendingApproval));
        Assert.Equal("Approve invoice", InvoiceLifecycle.GetTransitionDescription(
            InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Approved));
        Assert.Equal("Cancel approved invoice", InvoiceLifecycle.GetTransitionDescription(
            InvoiceLifecycleState.Approved, InvoiceLifecycleState.Cancelled));
        Assert.Null(InvoiceLifecycle.GetTransitionDescription(
            InvoiceLifecycleState.Draft, InvoiceLifecycleState.Approved));
    }
}

public class ProformaNumberSequenceTests
{
    [Fact]
    public void Seed_for_the_current_year_allocates_001()
    {
        // WPF: "PI-2026-001" → "PI-2026-002" (or 003… if those also exist)
        Assert.Equal("PI-2026-001",
            ProformaNumberSequence.SuggestNext("PI-2026-000", Array.Empty<string>()));
        Assert.Equal("PI-2026-002",
            ProformaNumberSequence.SuggestNext("PI-2026-001",
                new[] { "PI-2026-001" }));
        Assert.Equal("PI-2026-003",
            ProformaNumberSequence.SuggestNext("PI-2026-001",
                new[] { "PI-2026-001", "PI-2026-002" }));
    }

    [Fact]
    public void First_free_number_after_a_gap()
    {
        var existing = new[] { "PI-2026-001", "PI-2026-002", "PI-2026-004" };
        Assert.Equal("PI-2026-003",
            ProformaNumberSequence.SuggestNext("PI-2026-000", existing));
    }

    [Fact]
    public void Custom_prefixes_keep_their_shape()
    {
        // "INVOICE-42" → "INVOICE-43"; "ABC" → "ABC-001" (WPF examples)
        Assert.Equal("INVOICE-43",
            ProformaNumberSequence.SuggestNext("INVOICE-42", new[] { "INVOICE-42" }));
        Assert.Equal("ABC-001",
            ProformaNumberSequence.SuggestNext("ABC", Array.Empty<string>()));
    }

    [Fact]
    public void Existing_ids_match_case_insensitively()
    {
        // WPF builds the existing set with OrdinalIgnoreCase.
        Assert.Equal("PI-2026-002",
            ProformaNumberSequence.SuggestNext("PI-2026-000",
                new[] { "pi-2026-001" }));
    }

    [Fact]
    public void Overflow_digits_degrade_instead_of_crashing()
    {
        // AUDIT V-1: "PI-99999999999" (11 digits) must restart numbering
        // from that prefix, not throw OverflowException.
        var next = ProformaNumberSequence.SuggestNext("PI-99999999999",
            Array.Empty<string>());
        Assert.StartsWith("PI-", next);
        Assert.NotEqual("PI-99999999999", next);
    }

    [Fact]
    public void Blank_seed_defaults_to_PI_000()
    {
        Assert.Equal("PI-001",
            ProformaNumberSequence.SuggestNext("", Array.Empty<string>()));
        Assert.Equal("PI-001",
            ProformaNumberSequence.SuggestNext(null, Array.Empty<string>()));
    }

    [Fact]
    public void Pad_width_follows_the_seed()
    {
        Assert.Equal("PI-2026-0001",
            ProformaNumberSequence.SuggestNext("PI-2026-0000",
                new[] { "PI-2026-0000" }));
    }

    [Fact]
    public void Seed_helper_matches_the_wizard_placeholder()
    {
        Assert.Equal("PI-2026-000", ProformaNumberSequence.SeedFor(2026));
        Assert.True(ProformaStatuses.IsUnallocatedSeed(
            ProformaNumberSequence.SeedFor(2026)));
    }
}

public class SpecGroupMathTests
{
    [Fact]
    public void Primary_measurement_drives_the_money_line()
    {
        // 1000×1000 = 1 m², qty 3, rate 50 → value 150.00
        var m = SpecGroupRowMath.Calculate(1000, 1000, 0, 0, 3, 2, 50m);
        Assert.Equal(1.000m, m.Sqm1);
        Assert.Equal(3.000m, m.TotalSqm);
        Assert.Equal(0m, m.Sqm2);
        Assert.Equal(0m, m.TotalSqm2);
        Assert.Equal(12.000m, m.Lm1);   // (1000+1000)/1000 × 2 × 3
        Assert.Equal(0m, m.Lm2);
        Assert.Equal(150.00m, m.Value);
    }

    [Fact]
    public void Minimum_billable_sqm_floor_applies()
    {
        // 300×300 = 0.09 m² physically → billed as the 0.5 floor.
        var m = SpecGroupRowMath.Calculate(300, 300, 0, 0, 2, 2, 45m);
        Assert.Equal(0.500m, m.Sqm1);
        Assert.Equal(1.000m, m.TotalSqm);
        Assert.Equal(45.00m, m.Value);  // 0.5 × 2 × 45
    }

    [Fact]
    public void Dual_measurement_rows_keep_both_sides()
    {
        // DGU-style row: outer 1000×2000, inner 900×1800, qty 4, rate 60.
        var m = SpecGroupRowMath.Calculate(1000, 2000, 900, 1800, 4, 2, 60m);
        Assert.Equal(2.000m, m.Sqm1);
        Assert.Equal(8.000m, m.TotalSqm);
        Assert.Equal(1.620m, m.Sqm2);
        Assert.Equal(6.480m, m.TotalSqm2);
        Assert.Equal(24.000m, m.Lm1);   // (1000+2000)/1000 × 2 × 4
        Assert.Equal(21.600m, m.Lm2);   // (900+1800)/1000 × 2 × 4
        Assert.Equal(480.00m, m.Value); // 2 × 4 × 60 — primary only
    }

    [Fact]
    public void Zero_dimensions_zero_out_that_measurement()
    {
        var m = SpecGroupRowMath.Calculate(0, 0, 1000, 1000, 2, 2, 30m);
        Assert.Equal(0m, m.Sqm1);
        Assert.Equal(0m, m.Lm1);
        Assert.Equal(1.000m, m.Sqm2);
        Assert.Equal(0m, m.Value);      // no primary measurement → no money
    }

    [Fact]
    public void Row_validation_is_the_wpf_rule_set()
    {
        // Phantom — allowed, filtered later.
        Assert.Null(SpecGroupValidation.ValidateRowMessage(0, 0, 1, ""));

        Assert.Equal("Dimensions must be >0 for glass ref 'Spec 1'",
            SpecGroupValidation.ValidateRowMessage(0, 500, 2, "Spec 1"));
        Assert.Equal("Qty must be >0 for glass ref 'Spec 1'",
            SpecGroupValidation.ValidateRowMessage(500, 500, 0, "Spec 1"));
        Assert.Equal("Dimensions too large for 'Spec 1' — max 10000mm",
            SpecGroupValidation.ValidateRowMessage(10001, 500, 2, "Spec 1"));
        Assert.Equal("Qty too large for 'Spec 1' — max 10000",
            SpecGroupValidation.ValidateRowMessage(500, 500, 10001, "Spec 1"));
        Assert.Null(SpecGroupValidation.ValidateRowMessage(10000, 10000, 10000, "Spec 1"));
    }

    [Fact]
    public void Phantom_detection_is_the_wpf_rule()
    {
        Assert.True(SpecGroupValidation.IsPhantomRow(null, 0, 0, 0));
        Assert.True(SpecGroupValidation.IsPhantomRow("", 0, 0, 1));
        Assert.True(SpecGroupValidation.IsPhantomRow("Spec 1", 500, 500, 0)); // qty ≤ 0
        Assert.False(SpecGroupValidation.IsPhantomRow("Spec 1", 500, 500, 2));
    }

    [Fact]
    public void Glass_subtotal_is_the_sum_of_row_values()
    {
        // Step 7: GlassMaterialsSubtotal = the grid's GrandTotalValue.
        Assert.Equal(184.00m,
            SpecGroupAggregation.GlassMaterialsSubtotal(new[] { 138.00m, 46.00m }));
        Assert.Equal(3.000m,
            SpecGroupAggregation.TotalSqm(new[] { 1.000m, 2.000m }));
    }
}
