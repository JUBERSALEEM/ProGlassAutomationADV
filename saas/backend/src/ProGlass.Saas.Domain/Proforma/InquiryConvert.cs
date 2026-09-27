// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/Inquiry/InquiryRecord.cs — InquiryStatuses (6-word vocabulary
//     + Normalize incl. "On Hold"/"Canceled" aliases)
//   Services/Inquiry/InquiryWorkflowService.cs — CanTransition (the
//     status machine: Received→Prepared/Hold/Cancelled, …)
//   Services/ProformaInvoice/InquiryBoqConversion.cs — the PURE half of
//     inquiry→PI scope import (which BOQ rows import, in which order,
//     with which normalized values — pinned by WPF tests)
//   Services/ProformaInvoice/ProformaDuplicateSnapshotScrub.cs — the
//     last-resort detach of a duplicated snapshot (JsonNode surgery)
//   ViewModels/…/SavedInvoicesListViewModel.ScrubIdsForDuplicate — the
//     duplicate rule itself: never return the original JSON, a failed
//     scrub must not keep the source inquiry link
// The WPF files are the source of truth; behaviour preserved.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json.Nodes;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>
/// WPF InquiryStatuses — the inquiry status vocabulary. Unknown values
/// normalize to Received (pass-through happens at the edge, never here).
/// </summary>
public static class InquiryStatuses
{
    public const string Received = "Received";
    public const string Prepared = "Prepared";
    public const string Sent = "Sent";
    public const string Confirmed = "Confirmed";
    public const string Hold = "Hold";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        [Received, Prepared, Sent, Confirmed, Hold, Cancelled];

    public static string Normalize(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Equals("On Hold", StringComparison.OrdinalIgnoreCase)) return Hold;
        if (text.Equals("Canceled", StringComparison.OrdinalIgnoreCase)) return Cancelled;
        return All.FirstOrDefault(x =>
                   string.Equals(x, text, StringComparison.OrdinalIgnoreCase))
               ?? Received;
    }
}

/// <summary>
/// WPF InquiryWorkflowService.CanTransition — the inquiry status machine.
/// </summary>
public static class InquiryWorkflow
{
    public static bool CanTransition(string current, string next) =>
        InquiryStatuses.Normalize(current) switch
        {
            InquiryStatuses.Received =>
                next is InquiryStatuses.Prepared or InquiryStatuses.Hold
                    or InquiryStatuses.Cancelled,
            InquiryStatuses.Prepared =>
                next is InquiryStatuses.Sent or InquiryStatuses.Hold
                    or InquiryStatuses.Cancelled,
            InquiryStatuses.Sent =>
                next is InquiryStatuses.Confirmed or InquiryStatuses.Hold
                    or InquiryStatuses.Cancelled,
            InquiryStatuses.Hold =>
                next is InquiryStatuses.Prepared or InquiryStatuses.Cancelled,
            InquiryStatuses.Confirmed => next == InquiryStatuses.Cancelled,
            _ => false
        };
}

/// <summary>One inquiry BOQ line (the boq_json row shape, WPF InquiryLineItem).</summary>
public sealed record InquiryBoqLine(
    int SortOrder = 0,
    string GlassType = "",
    string SpecName = "",
    string OuterGlassColor = "",
    string InnerGlassColor = "",
    int WidthMm = 0,
    int HeightMm = 0,
    int Qty = 0,
    decimal RatePerSqm = 0m,
    string Notes = "")
{
    /// <summary>
    /// WPF IsImportable: a row can produce a grid line only with a spec
    /// name + real dimensions + a quantity. Partial rows deliberately
    /// stay on the inquiry as notes.
    /// </summary>
    public bool IsImportable =>
        !string.IsNullOrWhiteSpace(SpecName) && WidthMm > 0 && HeightMm > 0 && Qty > 0;
}

/// <summary>One fully-validated import decision for a BOQ line.</summary>
public sealed record BoqImportRow(
    string GlassType,
    string SpecName,
    string OuterGlassColor,
    string InnerGlassColor,
    int WidthMm,
    int HeightMm,
    int Qty,
    decimal RatePerSqm,
    bool HasRate);

/// <summary>
/// WPF InquiryBoqConversion — pure inquiry-BOQ → PI-grid conversion
/// decisions (filter / order / normalize), regression-tested there and
/// goldened here.
/// </summary>
public static class InquiryBoqConversion
{
    /// <summary>
    /// Normalize an inquiry glass-type label onto the PI vocabulary.
    /// DGU+LAM is matched EXACTLY and FIRST: any starts-with check on
    /// "DGU" would swallow the compound build — the most expensive spec
    /// type must never be silently downgraded.
    /// </summary>
    public static string NormalizeGlassType(string? type)
    {
        var t = (type ?? "").Trim();
        if (t.Equals("DGU+LAM", StringComparison.OrdinalIgnoreCase)) return "DGU+LAM";
        if (t.Equals("SGU", StringComparison.OrdinalIgnoreCase)) return "SGU";
        if (t.Equals("DGU", StringComparison.OrdinalIgnoreCase)) return "DGU";
        if (t.Equals("LAM", StringComparison.OrdinalIgnoreCase)) return "LAM";
        return "";
    }

    /// <summary>
    /// Convert BOQ lines into import decisions: only IsImportable rows,
    /// ordered by SortOrder so the PI grid mirrors the BOQ grid. A
    /// complete row with an UNKNOWN build is refused (it must not
    /// inherit whatever default type a grid group would carry).
    /// </summary>
    public static IReadOnlyList<BoqImportRow> ToImportRows(
        IReadOnlyList<InquiryBoqLine>? items)
    {
        if (items is null || items.Count == 0)
        {
            return Array.Empty<BoqImportRow>();
        }

        return items
            .Where(x => x.IsImportable)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { Source = x, GlassType = NormalizeGlassType(x.GlassType) })
            .Where(x => !string.IsNullOrWhiteSpace(x.GlassType))
            .Select(x => new BoqImportRow(
                x.GlassType,
                (x.Source.SpecName ?? "").Trim(),
                (x.Source.OuterGlassColor ?? "").Trim(),
                (x.Source.InnerGlassColor ?? "").Trim(),
                x.Source.WidthMm, x.Source.HeightMm, x.Source.Qty,
                x.Source.RatePerSqm,
                x.Source.RatePerSqm > 0m))
            .ToList();
    }
}

/// <summary>
/// WPF ScrubIdsForDuplicate + ProformaDuplicateSnapshotScrub — the
/// duplicate's snapshot detach. The normalized SaaS document carries no
/// WPF-shaped snapshot, so this is the JsonNode surgery (the WPF's own
/// fallback path): clear Company.InquiryId, rewrite Company.ProformaId.
/// Never returns the original JSON — a failed scrub must not keep the
/// source inquiry link (senior audit fix, Aug 2026).
/// </summary>
public static class DuplicateSnapshotScrub
{
    /// <summary>
    /// The scrubbed snapshot, or null when the snapshot cannot be
    /// detached — the caller must REFUSE the duplicate in that case.
    /// A null/blank source scrubs to empty (a SaaS-native invoice has
    /// no snapshot blob at all).
    /// </summary>
    public static string? TryDetach(string? json, string? newProformaId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }

        if (root is null)
        {
            return null;
        }

        if (root["Company"] is not JsonObject company)
        {
            return null;
        }

        company["InquiryId"] = null;
        company["ProformaId"] = newProformaId ?? string.Empty;
        return root.ToJsonString();
    }
}
