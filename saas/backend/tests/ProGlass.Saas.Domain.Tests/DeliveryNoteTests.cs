using ProGlass.Saas.Domain.Proforma;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.DeliveryNoteStatuses;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF goldens: the DeliveryNote transition table
/// (Services/Workflow/StatusTransitions.cs, Kind.DeliveryNote — verbatim,
/// self-transitions allowed), the helpers (DeliveryNoteWorkflow) and the
/// number derivation (BuildMasterDnIdAsync / BuildDeliveryNoteIdAsync).
/// </summary>
public sealed class DeliveryNoteTests
{
    // ── transitions (WPF table, verbatim) ───────────────────────────

    [Theory]
    [InlineData(Draft, Draft)]
    [InlineData(Draft, Dispatching)]
    [InlineData(Draft, Dispatched)]
    [InlineData(Draft, Delivered)]
    [InlineData(Draft, Signed)]
    [InlineData(Draft, Received)]
    [InlineData(Draft, Cancelled)]
    [InlineData(Dispatching, Dispatching)]
    [InlineData(Dispatching, Dispatched)]
    [InlineData(Dispatching, Delivered)]
    [InlineData(Dispatching, Signed)]
    [InlineData(Dispatching, Received)]
    [InlineData(Dispatching, Draft)]          // back out before dispatch
    [InlineData(Dispatching, Cancelled)]
    [InlineData(Dispatched, Dispatched)]
    [InlineData(Dispatched, Delivered)]
    [InlineData(Dispatched, Signed)]
    [InlineData(Dispatched, Received)]
    [InlineData(Dispatched, Cancelled)]
    [InlineData(Delivered, Delivered)]
    [InlineData(Delivered, Signed)]
    [InlineData(Delivered, Received)]
    [InlineData(Delivered, Cancelled)]
    [InlineData(Signed, Signed)]
    [InlineData(Signed, Received)]
    [InlineData(Signed, Cancelled)]
    [InlineData(Received, Received)]          // terminal: self only
    [InlineData(Cancelled, Cancelled)]
    [InlineData(Cancelled, Draft)]            // recovery
    public void Allowed_transitions_follow_the_wpf_table(string from, string to)
        => Assert.True(DeliveryNoteStateMachine.CanTransition(from, to),
            $"{from} → {to} must be allowed");

    [Theory]
    [InlineData(Dispatched, Draft)]           // no going back once dispatched
    [InlineData(Dispatched, Dispatching)]
    [InlineData(Delivered, Dispatching)]
    [InlineData(Delivered, Draft)]
    [InlineData(Signed, Dispatched)]
    [InlineData(Signed, Dispatching)]
    [InlineData(Received, Draft)]             // terminal
    [InlineData(Received, Dispatching)]
    [InlineData(Received, Cancelled)]
    [InlineData(Cancelled, Dispatching)]
    [InlineData(Cancelled, Dispatched)]
    [InlineData(Cancelled, Received)]
    [InlineData("Bogus", Dispatched)]
    [InlineData("", Dispatched)]
    [InlineData(null, Dispatched)]
    public void Disallowed_transitions_are_refused(string? from, string to)
        => Assert.False(DeliveryNoteStateMachine.CanTransition(from, to),
            $"{from ?? "null"} → {to} must be refused");

    [Fact]
    public void Reason_names_both_statuses()
    {
        var reason = DeliveryNoteStateMachine.Reason("Dispatching", "Drafting");
        Assert.Equal(
            "Status change from 'Dispatching' to 'Drafting' is not allowed.",
            reason);
    }

    // ── workflow helpers (DeliveryNoteWorkflow) ─────────────────────

    [Theory]
    [InlineData(Draft, true)]
    [InlineData(Dispatching, true)]
    [InlineData("dispatching", true)]         // case-insensitive
    [InlineData(Dispatched, false)]
    [InlineData(Delivered, false)]
    [InlineData(Signed, false)]
    [InlineData(Received, false)]
    [InlineData(Cancelled, false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOpenForDispatch_matches_wpf(string? status, bool expected)
        => Assert.Equal(expected, DeliveryNoteWorkflow.IsOpenForDispatch(status));

    [Theory]
    [InlineData(Dispatched, true)]
    [InlineData(Delivered, true)]
    [InlineData(Signed, true)]
    [InlineData(Received, true)]
    [InlineData("SIGNED", true)]
    [InlineData(Draft, false)]
    [InlineData(Dispatching, false)]          // in-progress ≠ confirmed
    [InlineData(Cancelled, false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsConfirmedDelivery_matches_wpf(string? status, bool expected)
        => Assert.Equal(expected, DeliveryNoteWorkflow.IsConfirmedDelivery(status));

    [Theory]
    [InlineData(1, 3, "1|3")]
    [InlineData(12, 40, "12|40")]
    public void LineKey_is_spec_pipe_serial_verbatim(int group, int serial)
        => Assert.Equal(
            $"{group}|{serial}", DeliveryNoteWorkflow.LineKey(group, serial));

    // ── numbering (BuildMasterDnId / BuildDeliveryNoteId) ───────────

    [Theory]
    [InlineData("JOB-2026-0042", "MDN-2026-0042")]
    [InlineData("JOB-7", "MDN-7")]
    [InlineData("2026-0042", "MDN-2026-0042")]   // already bare: no-op
    public void Master_number_strips_the_job_prefix(string jobNo, string expected)
        => Assert.Equal(expected, DeliveryNoteNumbers.MasterDnNo(jobNo));

    [Theory]
    [InlineData("JOB-2026-0042", 1, "DN-2026-0042-01")]
    [InlineData("JOB-2026-0042", 9, "DN-2026-0042-09")]
    [InlineData("JOB-2026-0042", 12, "DN-2026-0042-12")]
    [InlineData("JOB-7", 3, "DN-7-03")]
    public void Delivery_number_is_core_seq_D2(string jobNo, int seq, string expected)
        => Assert.Equal(expected, DeliveryNoteNumbers.DeliveryNoteNo(jobNo, seq));

    [Fact]
    public void Vocabularies_are_the_wpf_sets()
    {
        Assert.Equal(
            new[] { "Draft", "Dispatching", "Dispatched", "Delivered", "Signed",
                    "Received", "Cancelled" },
            DeliveryNoteStatuses.All);
        Assert.Equal(
            new[] { "Original", "Dummy", "Rework", "Return" },
            DeliveryTypes.All);
    }
}
