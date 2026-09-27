using ProGlass.Saas.Domain.Proforma;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.JobOrderStatuses;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF goldens: the JobOrder transition table
/// (Services/Workflow/StatusTransitions.cs, Kind.JobOrder), the status
/// helpers (JobOrderStatusValues) and the number derivation
/// (JobOrderMapper.DeriveJobOrderId + SuggestNextIdAsync's string math).
/// </summary>
public sealed class JobOrderTests
{
    [Theory]
    [InlineData(Draft, Released)]
    [InlineData(Draft, InProduction)]
    [InlineData(Draft, Completed)]
    [InlineData(Draft, Delivered)]
    [InlineData(Draft, Draft)]
    [InlineData(Released, InProduction)]
    [InlineData(Released, Completed)]
    [InlineData(Released, Delivered)]
    [InlineData(InProduction, Completed)]
    [InlineData(InProduction, Delivered)]
    [InlineData(Completed, Delivered)]
    [InlineData(Completed, Completed)]
    // Rejected is the void target — reachable from every live state.
    [InlineData(Draft, Rejected)]
    [InlineData(Released, Rejected)]
    [InlineData(InProduction, Rejected)]
    [InlineData(Completed, Rejected)]
    [InlineData(Delivered, Rejected)]
    // Rejected recovers to Draft.
    [InlineData(Rejected, Draft)]
    public void Allowed_transitions(string from, string to) =>
        Assert.True(JobOrderWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData(Released, Draft)]       // no going back before production
    [InlineData(InProduction, Released)]
    [InlineData(Completed, InProduction)]
    [InlineData(Delivered, Completed)]
    [InlineData(Delivered, Draft)]
    [InlineData(Rejected, Released)]    // a void job only recovers to Draft
    [InlineData(Rejected, InProduction)]
    [InlineData(Rejected, Completed)]
    [InlineData(Rejected, Delivered)]
    [InlineData("Bogus", Draft)]
    public void Refused_transitions(string from, string to) =>
        Assert.False(JobOrderWorkflow.CanTransition(from, to));

    [Fact]
    public void Status_helpers()
    {
        Assert.True(JobOrderStatuses.IsInProduction(InProduction));
        Assert.True(JobOrderStatuses.IsFinished(Completed));
        Assert.True(JobOrderStatuses.IsFinished(Delivered));
        Assert.False(JobOrderStatuses.IsFinished(InProduction));

        // Canonical collapses the spelling split for display.
        Assert.Equal("In Production", JobOrderStatuses.Canonical(InProduction));
        Assert.Equal("Draft", JobOrderStatuses.Canonical("Draft"));
        Assert.Equal("Unknown", JobOrderStatuses.Canonical(null));
        Assert.Equal("Unknown", JobOrderStatuses.Canonical("  "));
    }

    [Theory]
    [InlineData("PI-2026-0042", "JOB-2026-0042")]
    [InlineData("PI-2026-001", "JOB-2026-001")]
    [InlineData("Q-99", "JOB-Q-99")]              // foreign prefix kept
    [InlineData("PI-", "JOB-")]                   // WPF Substring edge
    [InlineData("", "JOB-2026-001")]              // empty → year form
    [InlineData(null, "JOB-2026-001")]
    public void Job_numbers_derive_from_the_pi(string? proformaNo, string expected) =>
        Assert.Equal(expected, JobOrderNumbers.DeriveJobOrderNo(proformaNo));

    [Theory]
    [InlineData("JOB-2026-001", "JOB-2026-002")]
    [InlineData("JOB-2026-0042", "JOB-2026-0043")]
    [InlineData("JOB-2026-999", "JOB-2026-1000")] // width overflow grows
    [InlineData("JOB-2026", "JOB-2026-002")]      // trailing dash → -002
    [InlineData("JOB", "JOB-002")]                // no dash → -002
    [InlineData("JOB-ABC", "JOB-ABC-002")]        // non-numeric tail
    public void Suggest_next_uses_the_wpf_string_math(string baseNo, string expected) =>
        Assert.Equal(expected, JobOrderNumbers.SuggestNext(baseNo));
}
