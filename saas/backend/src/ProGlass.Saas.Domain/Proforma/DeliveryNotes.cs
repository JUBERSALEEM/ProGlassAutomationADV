// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/DeliveryNote/Common/DeliveryNoteStatus/DeliveryNoteStatus.cs
//     (status constants), Common/DeliveryType/DeliveryType.cs (Original/
//     Dummy/Rework/Return — stored as string, never cast).
//   Services/DeliveryNote/DeliveryNoteWorkflow.cs (IsOpenForDispatch,
//     IsConfirmedDelivery, LineKey — the stable line identity).
//   Services/Workflow/StatusTransitions.cs (the DeliveryNote table,
//     verbatim — "Cancelled" is the void target, reachable from every
//     non-terminal state + Cancelled → Draft recovery).
//   Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs
//     (BuildMasterDnIdAsync = MDN-{core}, BuildDeliveryNoteIdAsync =
//     DN-{core}-{seq:D2}).
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Delivery Note status values (WPF DeliveryNoteStatus).</summary>
public static class DeliveryNoteStatuses
{
    public const string Draft = "Draft";
    public const string Dispatching = "Dispatching";
    public const string Dispatched = "Dispatched";
    public const string Delivered = "Delivered";
    public const string Signed = "Signed";
    public const string Received = "Received";

    /// <summary>The void (soft-close) target for a delivery note.</summary>
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        new[] { Draft, Dispatching, Dispatched, Delivered, Signed, Received, Cancelled };
}

/// <summary>Delivery note classification (WPF DeliveryType).</summary>
public static class DeliveryTypes
{
    /// <summary>Normal first/partial delivery.</summary>
    public const string Original = "Original";

    /// <summary>Auto-generated from balance qty (open until confirmed).</summary>
    public const string Dummy = "Dummy";

    /// <summary>Re-delivery of reworked items from a rework JO.</summary>
    public const string Rework = "Rework";

    /// <summary>Tracking returned/rejected items.</summary>
    public const string Return = "Return";

    public static readonly IReadOnlyList<string> All =
        new[] { Original, Dummy, Rework, Return };
}

/// <summary>
/// Delivery Note workflow meaning (WPF DeliveryNoteWorkflow). The
/// workspace NEW DN / CREATE DN path may only open Dispatching (or legacy
/// Draft); confirmed deliveries consume remaining qty and are opened from
/// the God View — never from NEW DN.
/// </summary>
public static class DeliveryNoteWorkflow
{
    public static bool IsOpenForDispatch(string? status) =>
        string.Equals(status, DeliveryNoteStatuses.Dispatching, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, DeliveryNoteStatuses.Draft, StringComparison.OrdinalIgnoreCase);

    public static bool IsConfirmedDelivery(string? status) =>
        status is not null
        && (string.Equals(status, DeliveryNoteStatuses.Dispatched, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DeliveryNoteStatuses.Delivered, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DeliveryNoteStatuses.Signed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DeliveryNoteStatuses.Received, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Stable line identity (WPF LineKey, verbatim: identical to the
    /// repository's SpecKey). The SerialNumber is the JO/Master serial
    /// and must never be reassigned when creating a subsequent DN — the
    /// glass-dimension fallback that used to be here attributed
    /// identical panels to each other and zeroed remaining qty.
    /// </summary>
    public static string LineKey(int specGroupNumber, int serialNumber) =>
        $"{specGroupNumber}|{serialNumber}";
}

/// <summary>
/// The Delivery Note status machine (WPF StatusTransitions,
/// Kind.DeliveryNote — verbatim; self-transitions allowed).
/// </summary>
public static class DeliveryNoteStateMachine
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Table =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DeliveryNoteStatuses.Draft] = new[]
            {
                DeliveryNoteStatuses.Draft, DeliveryNoteStatuses.Dispatching,
                DeliveryNoteStatuses.Dispatched, DeliveryNoteStatuses.Delivered,
                DeliveryNoteStatuses.Signed, DeliveryNoteStatuses.Received,
                DeliveryNoteStatuses.Cancelled
            },
            [DeliveryNoteStatuses.Dispatching] = new[]
            {
                DeliveryNoteStatuses.Dispatching, DeliveryNoteStatuses.Dispatched,
                DeliveryNoteStatuses.Delivered, DeliveryNoteStatuses.Signed,
                DeliveryNoteStatuses.Received, DeliveryNoteStatuses.Draft,
                DeliveryNoteStatuses.Cancelled
            },
            [DeliveryNoteStatuses.Dispatched] = new[]
            {
                DeliveryNoteStatuses.Dispatched, DeliveryNoteStatuses.Delivered,
                DeliveryNoteStatuses.Signed, DeliveryNoteStatuses.Received,
                DeliveryNoteStatuses.Cancelled
            },
            [DeliveryNoteStatuses.Delivered] = new[]
            {
                DeliveryNoteStatuses.Delivered, DeliveryNoteStatuses.Signed,
                DeliveryNoteStatuses.Received, DeliveryNoteStatuses.Cancelled
            },
            [DeliveryNoteStatuses.Signed] = new[]
            {
                DeliveryNoteStatuses.Signed, DeliveryNoteStatuses.Received,
                DeliveryNoteStatuses.Cancelled
            },
            [DeliveryNoteStatuses.Received] = new[]
            {
                DeliveryNoteStatuses.Received
            },
            [DeliveryNoteStatuses.Cancelled] = new[]
            {
                DeliveryNoteStatuses.Cancelled, DeliveryNoteStatuses.Draft
            }
        };

    public static bool CanTransition(string? current, string next)
    {
        if (!Table.TryGetValue((current ?? "").Trim(), out var allowed))
        {
            return false;
        }

        return allowed.Contains(next.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string Reason(string? current, string next) =>
        $"Status change from '{(current ?? "").Trim()}' to '{next.Trim()}' is not allowed.";
}

/// <summary>
/// Delivery note numbers (WPF BuildMasterDnIdAsync / BuildDeliveryNoteIdAsync):
/// JOB-2026-0042 → MDN-2026-0042 (the balance tracker) and
/// DN-2026-0042-01 (the seq-2 dispatch note).
/// </summary>
public static class DeliveryNoteNumbers
{
    public static string MasterDnNo(string jobOrderNo)
    {
        var core = Core(jobOrderNo);
        return $"MDN-{core}";
    }

    public static string DeliveryNoteNo(string jobOrderNo, int sequence) =>
        $"DN-{Core(jobOrderNo)}-{sequence:D2}";

    private static string Core(string jobOrderNo)
    {
        var no = (jobOrderNo ?? "").Trim();
        return no.StartsWith("JOB-", StringComparison.OrdinalIgnoreCase)
            ? no[4..]
            : no;
    }
}
