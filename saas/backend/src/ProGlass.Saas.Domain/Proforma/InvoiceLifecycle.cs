// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Services/ProformaInvoice/InvoiceLifecycleStateMachine.cs
// The WPF file is the source of truth. Contract preserved verbatim:
//   • Pure function — no side effects, no persistence.
//   • CanTransition() returns true/false for any (from, to) pair.
//   • Never throws — an invalid transition is a result, not an exception.
// Only the shape changed: a frozen transition table instead of a mutable
// List, and records instead of a class.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>The lifecycle states a Proforma Invoice can be in.</summary>
public enum InvoiceLifecycleState
{
    /// <summary>Being created, not yet submitted.</summary>
    Draft = 0,

    /// <summary>Submitted for manager approval.</summary>
    PendingApproval = 1,

    /// <summary>Approved by manager, ready to send.</summary>
    Approved = 2,

    /// <summary>Sent to customer.</summary>
    Sent = 3,

    /// <summary>Customer confirmed / converted to Job Order.</summary>
    Confirmed = 4,

    /// <summary>Cancelled (any state can transition here).</summary>
    Cancelled = 5,

    /// <summary>Rejected by manager (returns to Draft).</summary>
    Rejected = 6
}

/// <summary>A valid state transition with its human-readable label.</summary>
public sealed record StateTransition(
    InvoiceLifecycleState From,
    InvoiceLifecycleState To,
    string Description,
    bool RequiresApproval);

/// <summary>
/// Enforces valid lifecycle transitions for Proforma Invoices — the WPF
/// state machine, verbatim (invalid transitions are impossible at the
/// code level).
/// </summary>
public static class InvoiceLifecycle
{
    // ── Valid transitions (WPF table, in the WPF's order) ──────────
    private static readonly StateTransition[] Transitions =
    {
        // Draft → PendingApproval (submit for review)
        new(InvoiceLifecycleState.Draft, InvoiceLifecycleState.PendingApproval,
            "Submit for approval", false),

        // PendingApproval → Approved (manager approves)
        new(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Approved,
            "Approve invoice", true),

        // PendingApproval → Rejected (manager rejects)
        new(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Rejected,
            "Reject invoice", true),

        // PendingApproval → Draft (return to draft)
        new(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Draft,
            "Return to draft", false),

        // Rejected → Draft (revise and resubmit)
        new(InvoiceLifecycleState.Rejected, InvoiceLifecycleState.Draft,
            "Revise and return to draft", false),

        // Approved → Sent (send to customer)
        new(InvoiceLifecycleState.Approved, InvoiceLifecycleState.Sent,
            "Send to customer", false),

        // Sent → Confirmed (customer confirms)
        new(InvoiceLifecycleState.Sent, InvoiceLifecycleState.Confirmed,
            "Customer confirmed", false),

        // Cancellation from any active state
        new(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Cancelled,
            "Cancel invoice", false),
        new(InvoiceLifecycleState.PendingApproval, InvoiceLifecycleState.Cancelled,
            "Cancel invoice", false),
        new(InvoiceLifecycleState.Rejected, InvoiceLifecycleState.Cancelled,
            "Cancel invoice", false),
        new(InvoiceLifecycleState.Approved, InvoiceLifecycleState.Cancelled,
            "Cancel approved invoice", true),
    };

    /// <summary>Checks if a transition from one state to another is valid.</summary>
    public static bool CanTransition(InvoiceLifecycleState from, InvoiceLifecycleState to) =>
        Transitions.Any(t => t.From == from && t.To == to);

    /// <summary>String overload — false for unrecognized state strings.</summary>
    public static bool CanTransition(string? fromState, string? toState)
    {
        if (!TryParseState(fromState, out var from)) return false;
        if (!TryParseState(toState, out var to)) return false;
        return CanTransition(from, to);
    }

    /// <summary>The list of valid next states from the current state.</summary>
    public static IReadOnlyList<InvoiceLifecycleState> GetValidTransitions(
        InvoiceLifecycleState current) =>
        Transitions.Where(t => t.From == current).Select(t => t.To).ToList();

    /// <summary>The list of valid transitions with descriptions.</summary>
    public static IReadOnlyList<StateTransition> GetValidTransitionsWithDescriptions(
        InvoiceLifecycleState current) =>
        Transitions.Where(t => t.From == current).ToList();

    /// <summary>Description for a specific transition; null if invalid.</summary>
    public static string? GetTransitionDescription(
        InvoiceLifecycleState from, InvoiceLifecycleState to) =>
        Transitions.FirstOrDefault(t => t.From == from && t.To == to)?.Description;

    /// <summary>Whether a transition requires manager approval.</summary>
    public static bool RequiresApproval(InvoiceLifecycleState from, InvoiceLifecycleState to) =>
        Transitions.FirstOrDefault(t => t.From == from && t.To == to)?.RequiresApproval ?? false;

    /// <summary>Whether a state is terminal (no further transitions).</summary>
    public static bool IsTerminalState(InvoiceLifecycleState state) =>
        state == InvoiceLifecycleState.Cancelled || state == InvoiceLifecycleState.Confirmed;

    /// <summary>Whether a state allows editing (Draft, Rejected).</summary>
    public static bool IsEditable(InvoiceLifecycleState state) =>
        state == InvoiceLifecycleState.Draft || state == InvoiceLifecycleState.Rejected;

    /// <summary>
    /// Parses a state string (case-insensitive) into an enum value.
    /// Also handles legacy status strings like "Pending" → PendingApproval
    /// and "Active" → Sent.
    /// </summary>
    public static bool TryParseState(string? stateString, out InvoiceLifecycleState state)
    {
        state = InvoiceLifecycleState.Draft;
        if (string.IsNullOrWhiteSpace(stateString))
            return false;

        var normalized = stateString.Trim();

        if (Enum.TryParse(normalized, true, out state))
            return true;

        if (normalized.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            state = InvoiceLifecycleState.PendingApproval;
            return true;
        }

        if (normalized.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            state = InvoiceLifecycleState.Sent;
            return true;
        }

        return false;
    }

    /// <summary>Converts a legacy status string to the enum format.</summary>
    public static string NormalizeStatus(string? legacyStatus)
    {
        if (TryParseState(legacyStatus, out var state))
            return state.ToString();

        return InvoiceLifecycleState.Draft.ToString();
    }
}
