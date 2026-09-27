// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/JobOrder/Common/JobOrderStatus/{JobOrderStatus.cs,
//     JobOrderStatusValues.cs} — the status vocabulary + helpers. The
//     WPF's legacy "In Production" spaced spelling cannot occur in the
//     fresh SaaS schema (single canonical form — documented).
//   Models/JobOrder/JobPriority/JobPriority.cs — Low/Medium/High.
//   Services/Workflow/StatusTransitions.cs — the JobOrder transition
//     table, verbatim (incl. Rejected as the void target).
//   Services/JobOrder/Mapper/JobOrderMapper.cs — DeriveJobOrderId
//     (PI-2026-0042 → JOB-2026-0042) and the conversion-time status rule
//     (source PI Approved → Released, otherwise Draft).
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>
/// Job Order status values (WPF JobOrderStatusValues). Stored in the
/// canonical enum form — one spelling, no legacy split.
/// </summary>
public static class JobOrderStatuses
{
    public const string Draft = "Draft";
    public const string Released = "Released";
    public const string InProduction = "InProduction";
    public const string Completed = "Completed";
    public const string Delivered = "Delivered";

    /// <summary>The void (soft-close) target — WPF
    /// DocumentLifecyclePolicy.VoidTargetStatus("JobOrder").</summary>
    public const string Rejected = "Rejected";

    /// <summary>The label shown to users (WPF InProductionDisplay).</summary>
    public const string InProductionDisplay = "In Production";

    public static readonly IReadOnlyList<string> All =
        new[] { Draft, Released, InProduction, Completed, Delivered, Rejected };

    public static bool Is(string? stored, string expected) =>
        string.Equals((stored ?? "").Trim(), expected,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsInProduction(string? stored) => Is(stored, InProduction);

    /// <summary>Production is finished: Completed or already on site. A
    /// delivered job is never "overdue" even if its date has passed.</summary>
    public static bool IsFinished(string? stored) =>
        Is(stored, Completed) || Is(stored, Delivered);

    /// <summary>Canonical display label (collapses the WPF spelling
    /// split so a breakdown cannot show one state under two names).</summary>
    public static string Canonical(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return "Unknown";
        return IsInProduction(stored) ? InProductionDisplay : stored.Trim();
    }
}

/// <summary>Production priority (WPF JobPriority).</summary>
public static class JobOrderPriorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    public static readonly IReadOnlyList<string> All = new[] { Low, Medium, High };
}

/// <summary>
/// The Job Order status machine (WPF StatusTransitions, Kind.JobOrder —
/// verbatim table; case-insensitive, self-transitions allowed).
/// </summary>
public static class JobOrderWorkflow
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Table =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [JobOrderStatuses.Draft] = new[]
            {
                JobOrderStatuses.Draft, JobOrderStatuses.Released,
                JobOrderStatuses.InProduction, JobOrderStatuses.Completed,
                JobOrderStatuses.Delivered, JobOrderStatuses.Rejected
            },
            [JobOrderStatuses.Released] = new[]
            {
                JobOrderStatuses.Released, JobOrderStatuses.InProduction,
                JobOrderStatuses.Completed, JobOrderStatuses.Delivered,
                JobOrderStatuses.Rejected
            },
            [JobOrderStatuses.InProduction] = new[]
            {
                JobOrderStatuses.InProduction, JobOrderStatuses.Completed,
                JobOrderStatuses.Delivered, JobOrderStatuses.Rejected
            },
            [JobOrderStatuses.Completed] = new[]
            {
                JobOrderStatuses.Completed, JobOrderStatuses.Delivered,
                JobOrderStatuses.Rejected
            },
            [JobOrderStatuses.Delivered] = new[]
            {
                JobOrderStatuses.Delivered, JobOrderStatuses.Rejected
            },
            [JobOrderStatuses.Rejected] = new[]
            {
                JobOrderStatuses.Rejected, JobOrderStatuses.Draft
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

    /// <summary>The operator-readable reason for a refusal (the WPF
    /// Validate() result's Reason).</summary>
    public static string Reason(string? current, string next) =>
        $"Status change from '{JobOrderStatuses.Canonical(current)}' to " +
        $"'{JobOrderStatuses.Canonical(next)}' is not allowed.";
}

/// <summary>
/// Job order numbers (WPF JobOrderMapper.DeriveJobOrderId + the
/// repository's SuggestNextIdAsync increment).
/// </summary>
public static class JobOrderNumbers
{
    /// <summary>PI-2026-0042 → JOB-2026-0042; a foreign prefix is kept
    /// ("JOB-{source}"); an empty source falls back to the year form.</summary>
    public static string DeriveJobOrderNo(string? proformaNo)
    {
        if (string.IsNullOrWhiteSpace(proformaNo))
        {
            return $"JOB-{DateTime.UtcNow:yyyy}-001";
        }

        if (proformaNo.StartsWith("PI-", StringComparison.OrdinalIgnoreCase))
        {
            return "JOB-" + proformaNo[3..];
        }

        return "JOB-" + proformaNo;
    }

    /// <summary>
    /// The next number after the given one (WPF SuggestNextIdAsync's
    /// string math, verbatim): split at the LAST dash, parse + increment
    /// the tail, pad to the original width ("JOB-2026-001" →
    /// "JOB-2026-002"). No dash or a non-numeric tail appends "-002".
    /// The exists-check loop lives in the store.
    /// </summary>
    public static string SuggestNext(string jobOrderNo)
    {
        var baseId = (jobOrderNo ?? "").Trim();
        if (baseId.Length == 0)
        {
            return baseId;
        }

        var lastDash = baseId.LastIndexOf('-');
        if (lastDash < 0 || lastDash == baseId.Length - 1)
        {
            return baseId + "-002";
        }

        var prefix = baseId[..(lastDash + 1)];
        var numericPart = baseId[(lastDash + 1)..];
        if (!int.TryParse(numericPart, out var current))
        {
            return baseId + "-002";
        }

        var width = numericPart.Length;
        return prefix + (current + 1).ToString().PadLeft(width, '0');
    }
}
