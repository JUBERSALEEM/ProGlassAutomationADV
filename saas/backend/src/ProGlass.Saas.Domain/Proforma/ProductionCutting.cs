// ═══════════════════════════════════════════════════════════════════════
//  PORTED from the WPF application (read 2026-09-27) — Phase 26b:
//    • Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
//      (AllocationStatus, ActualCostType enums; SavedMaterialAllocation /
//      SavedActualCost / SavedProductionReplacement id schemes).
//    • Models/Production/SavedProduction/SavedProductionPhase15Entities.cs
//      (CuttingSessionStatus, CuttingPieceStatus enums; SavedCuttingSession /
//      SavedCuttingSheet / SavedCuttingPlacedPiece id schemes).
//    • Models/Production/Cutting/CuttingRequirement.cs (requirement +
//      material-group records, MaterialKey/SizeKey/AreaSqm).
//    • Services/Production/Cutting/MaterialGroupingService.cs (group key
//      FAMILY|COLOR|THICK|COATING|TREATMENT, deterministic ordering,
//      AreCompatible).
//    • Services/Production/Cutting/CuttingRequirementService.cs
//      (ParseMaterialFromSpec keyword table, InferThicknessFromSpec —
//      single distinct pane thickness or 0, never a guess; RequiresTempering
//      FT/Tempered/Heat; piece label format).
//    • Services/Production/Cutting/StockSelectionService.cs (compatibility,
//      effective available = Balance − active holds, waste score, ordering,
//      the 80% fill estimate + unlimited fallback).
//    • Services/Production/ProductionSnapshotIntegrity.cs (Missing / Corrupt
//      / ValidEmpty / Valid).
//    • Services/Production/ProductionInventoryConsistencyScanner.cs
//      (Fact / Observation / NotDetectable finding kinds + codes).
//  The WPF files are the source of truth; behaviour preserved verbatim.
//  SaaS difference (documented, mapping §1.7d): the WPF's int enums are
//  persisted as the SaaS's TEXT vocabularies; the WPF int codes remain in
//  the deterministic string ids exactly where the WPF embeds them.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Material allocation lifecycle (WPF AllocationStatus — same
/// six states as the reservation lifecycle, tracked per placed piece).</summary>
public static class AllocationStatuses
{
    public const string Available = "Available";
    public const string Reserved = "Reserved";
    public const string Allocated = "Allocated";
    public const string Consumed = "Consumed";
    public const string Released = "Released";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Available, Reserved, Allocated, Consumed, Released, Cancelled
    };

    /// <summary>May a new allocation ride on a reservation in this state?
    /// (WPF AllocateAsync refuses Cancelled/Released/Consumed.)</summary>
    public static bool CanAllocate(string? status) =>
        !string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(status, Released, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(status, Consumed, StringComparison.OrdinalIgnoreCase);

    /// <summary>May an allocation in this state be consumed? (WPF
    /// ConsumeAsync refuses Cancelled/Released.)</summary>
    public static bool CanConsume(string? status) =>
        !string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(status, Released, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Actual cost buckets (WPF ActualCostType). The WPF enum's int
/// codes are embedded in CostId — the names map 1:1.</summary>
public static class ActualCostTypes
{
    public const string Estimated = "Estimated";
    public const string ActualMaterial = "ActualMaterial";
    public const string Production = "Production";
    public const string Wastage = "Wastage";
    public const string Processing = "Processing";
    public const string TotalActual = "TotalActual";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Estimated, ActualMaterial, Production, Wastage, Processing, TotalActual
    };

    /// <summary>The WPF int code (ActualCostType enum value) for a bucket
    /// name — used inside the deterministic CostId.</summary>
    public static int WpfCode(string? costType) => costType switch
    {
        Estimated => 0,
        ActualMaterial => 1,
        Production => 2,
        Wastage => 3,
        Processing => 4,
        TotalActual => 5,
        _ => 1
    };
}

/// <summary>Cutting session lifecycle (WPF CuttingSessionStatus).</summary>
public static class CuttingSessionStatuses
{
    public const string Draft = "Draft";
    public const string Prepared = "Prepared";
    public const string Optimized = "Optimized";
    public const string Verified = "Verified";
    public const string Allocated = "Allocated";
    public const string Cutting = "Cutting";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, Prepared, Optimized, Verified, Allocated, Cutting,
        Completed, Cancelled, Failed
    };
}

/// <summary>Cutting placed-piece lifecycle (WPF CuttingPieceStatus).</summary>
public static class CuttingPieceStatuses
{
    public const string Pending = "Pending";
    public const string Reserved = "Reserved";
    public const string Allocated = "Allocated";
    public const string Cutting = "Cutting";
    public const string Completed = "Completed";
    public const string QcPending = "QcPending";
    public const string QcPassed = "QcPassed";
    public const string QcFailed = "QcFailed";
    public const string Rework = "Rework";
    public const string Breakage = "Breakage";
    public const string Foc = "Foc";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, Reserved, Allocated, Cutting, Completed, QcPending,
        QcPassed, QcFailed, Rework, Breakage, Foc, Cancelled
    };
}

/// <summary>Replacement row lifecycle (WPF int 0..3: Pending, InProgress,
/// Completed, Cancelled).</summary>
public static class ReplacementStatuses
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, InProgress, Completed, Cancelled
    };
}

// ── deterministic identities (WPF id builders, verbatim formats) ─────

public static class CuttingIds
{
    /// <summary>WPF BuildAllocationId: ReservationId|S{sheetNo}|
    /// {placedPieceLabel}. The SaaS reservation key embeds the GUID sheet
    /// id, so the allocation key stays deterministic per tenant.</summary>
    public static string BuildAllocationId(
        string reservationKey, int sheetNo, string placedPieceLabel) =>
        string.Join("|",
            (reservationKey ?? "").Trim(),
            $"S{sheetNo}",
            (placedPieceLabel ?? "").Trim());

    /// <summary>WPF BuildCostId: JobRef|Spec|Serial|{int costType}, with
    /// the allocation id as suffix and |FOC appended for FOC rows (WPF
    /// RecordCostAsync deterministic uniqueness per allocation).</summary>
    public static string BuildCostId(
        string jobRef, int specGroup, int serial, string costType,
        string? materialAllocationId = null, bool isFoc = false)
    {
        var baseId = string.Join("|",
            (jobRef ?? "").Trim(),
            specGroup,
            serial,
            ActualCostTypes.WpfCode(costType));
        if (!string.IsNullOrWhiteSpace(materialAllocationId))
            baseId += "|" + materialAllocationId.Trim();
        if (isFoc && !string.IsNullOrWhiteSpace(materialAllocationId))
            baseId += "|FOC";
        return baseId;
    }

    /// <summary>WPF BuildReplacementId: JobRef|Spec|Serial|Position|
    /// {stage int}|RPL:{sequence}. The stage code is the SaaS
    /// ProductionStages int (Cutting=1 … DoublingLamination=8) — same
    /// code space the WPF enum numbers.</summary>
    public static string BuildReplacementId(
        string jobRef, int specGroup, int serial, string position,
        int stageCode, int sequence)
    {
        jobRef = (jobRef ?? "").Trim();
        position = (position ?? "").Trim();
        return $"{jobRef}|{specGroup}|{serial}|{position}|{stageCode}|RPL:{sequence}";
    }

    /// <summary>Replacement work-item key (WPF): the original UniqueKey
    /// with the generation appended — a replacement can never masquerade
    /// as the original row.</summary>
    public static string BuildReplacementWorkItemKey(
        string originalUniqueKey, int sequence) =>
        $"{(originalUniqueKey ?? "").Trim()}|RPL:{sequence}";

    /// <summary>WPF session id: CUT-{jobRef}-{group}-{Utc:yyyyMMddHHmmss}
    /// with the group key's pipes replaced by dashes.</summary>
    public static string BuildCuttingSessionId(
        string jobRef, string materialGroupId, DateTime utcTimestamp) =>
        $"CUT-{(jobRef ?? "").Trim()}" +
        $"-{(materialGroupId ?? "").Trim().Replace('|', '-')}" +
        $"-{utcTimestamp:yyyyMMddHHmmss}";

    /// <summary>WPF sheet id: {sessionId}|S{sheetNo}.</summary>
    public static string BuildCuttingSheetId(
        string cuttingSessionId, int sheetNo) =>
        $"{(cuttingSessionId ?? "").Trim()}|S{sheetNo}";

    /// <summary>WPF placed-piece id: {sheetId}|{pieceId}.</summary>
    public static string BuildPlacedPieceId(
        string cuttingSheetId, string pieceId) =>
        $"{(cuttingSheetId ?? "").Trim()}|{(pieceId ?? "").Trim()}";
}

// ── cutting requirements (WPF CuttingRequirement + MaterialGroup) ────

/// <summary>One cutting requirement — one physical glass piece that must
/// be cut, with full spec identity (WPF CuttingRequirement).</summary>
public sealed record CuttingRequirement(
    string JobRef,
    string SourceProformaId,
    int SpecGroupNumber,
    string SpecName,
    string BuildType,
    int SerialNumber,
    string GlassRef,
    string Position,
    int WidthMm,
    int HeightMm,
    string GlassFamily,
    string Color,
    int ThicknessMm,
    string Coating,
    string Treatment,
    bool RequiresTempering,
    string PieceId,
    string PieceLabel,
    int PieceIndex,
    int PieceSequence,
    bool IsFoc,
    int ReplacementSequence,
    string UniqueKey = "",
    Guid SourcePieceId = default,
    string Notes = "")
{
    public string MaterialKey =>
        $"{GlassFamily}|{Color}|{ThicknessMm}|{Coating}|{Treatment}".Trim('|');

    public string SizeKey => $"{WidthMm}x{HeightMm}";

    public decimal AreaSqm => (decimal)WidthMm * HeightMm / 1_000_000m;
}

/// <summary>A group of requirements sharing physically compatible
/// material (WPF MaterialGroup).</summary>
public sealed record MaterialGroup(
    string GroupId,
    string GlassFamily,
    string Color,
    int ThicknessMm,
    string Coating,
    string Treatment,
    IReadOnlyList<CuttingRequirement> Requirements)
{
    public int TotalPieces => Requirements.Count; // per-piece rows (WPF Quantity=1)

    public decimal TotalAreaSqm => Requirements.Sum(r => r.AreaSqm);

    public string DisplayLabel =>
        $"{GlassFamily} {Color} {ThicknessMm}mm {Coating} {Treatment}".Trim();
}

// ── grouping (WPF MaterialGroupingService, verbatim) ─────────────────

public static class CuttingGrouping
{
    /// <summary>Deterministic group key:
    /// FAMILY|COLOR|THICKNESS|COATING|TREATMENT (upper-invariant). Low-E
    /// never mixes with Clear, thicknesses never mix.</summary>
    public static string BuildGroupKey(CuttingRequirement req)
    {
        string family = (req.GlassFamily ?? "Clear").Trim();
        string color = (req.Color ?? "Clear").Trim();
        string coating = (req.Coating ?? "").Trim();
        string treatment = (req.Treatment ?? "").Trim();
        return $"{family}|{color}|{req.ThicknessMm}|{coating}|{treatment}"
            .ToUpperInvariant();
    }

    /// <summary>Group by material, ordered Family → Color → Thickness →
    /// Coating → Treatment (OrdinalIgnoreCase), requirements in piece
    /// order (WPF GroupByMaterial).</summary>
    public static IReadOnlyList<MaterialGroup> GroupByMaterial(
        IEnumerable<CuttingRequirement> requirements)
    {
        var groups = new Dictionary<string, List<CuttingRequirement>>(
            StringComparer.OrdinalIgnoreCase);
        var meta = new Dictionary<string, CuttingRequirement>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var req in requirements.OrderBy(r => r.PieceSequence))
        {
            string key = BuildGroupKey(req);
            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = new List<CuttingRequirement>();
                meta[key] = req;
            }
            list.Add(req);
        }

        return groups
            .Select(kv => new MaterialGroup(
                kv.Key,
                meta[kv.Key].GlassFamily,
                meta[kv.Key].Color,
                meta[kv.Key].ThicknessMm,
                meta[kv.Key].Coating,
                meta[kv.Key].Treatment,
                kv.Value))
            .OrderBy(g => g.GlassFamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Color, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.ThicknessMm)
            .ThenBy(g => g.Coating, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Treatment, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>May two requirements share a sheet? (WPF AreCompatible:
    /// thickness, color, family and coating must match; treatment rides
    /// the group key but is not compared — FT and Annealed cut the
    /// same.)</summary>
    public static bool AreCompatible(
        CuttingRequirement a, CuttingRequirement b)
    {
        if (a.ThicknessMm != b.ThicknessMm) return false;
        if (!string.Equals(a.Color?.Trim(), b.Color?.Trim(),
                StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.GlassFamily?.Trim(), b.GlassFamily?.Trim(),
                StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.Coating?.Trim(), b.Coating?.Trim(),
                StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

// ── material identity (WPF ParseMaterialFromSpec + InferThickness) ───

public static class CuttingMaterial
{
    /// <summary>Keyword table over the spec name (WPF
    /// ParseMaterialFromSpec): Low-E / neutral, grey, obscure/frost,
    /// green, blue; otherwise the GlassRef head (before '·').</summary>
    public static (string Family, string Color, string Coating) ParseFromSpec(
        string? specName, string? glassRef)
    {
        specName = (specName ?? "").Trim();
        glassRef = (glassRef ?? "").Trim();

        string lower = specName.ToLowerInvariant();
        string family = "Clear";
        string color = "Clear";
        string coating = "";

        if (lower.Contains("low-e") || lower.Contains("low e")
            || lower.Contains("hp neutral") || lower.Contains("neutral"))
        {
            family = "Low-E";
            color = "Low-E";
            coating = "Low-E";
        }
        else if (lower.Contains("hd grey") || lower.Contains("grey")
            || lower.Contains("gray"))
        {
            family = "HD Grey";
            color = "Grey";
        }
        else if (lower.Contains("ramly") || lower.Contains("obscure")
            || lower.Contains("frost"))
        {
            family = "Ramly";
            color = "Obscure";
        }
        else if (lower.Contains("green"))
        {
            family = "Green";
            color = "Green";
        }
        else if (lower.Contains("blue"))
        {
            family = "Blue";
            color = "Blue";
        }
        else if (!string.IsNullOrWhiteSpace(glassRef))
        {
            family = glassRef.Split('·')[0].Trim();
            if (string.IsNullOrWhiteSpace(family)) family = glassRef;
        }

        if (lower.Contains("low-e") || lower.Contains("low e"))
            coating = "Low-E";

        return (family, color, coating);
    }

    /// <summary>Pane thickness from the spec grammar ONLY (WPF
    /// InferThicknessFromSpec): a single distinct positive pane thickness,
    /// else 0 — mixed panes (8mm + 6mm) and unparsed names are never
    /// guessed, so stock matching cannot silently pick a wrong rack.</summary>
    public static int InferThickness(string? specName)
    {
        if (string.IsNullOrWhiteSpace(specName)) return 0;
        var mm = SpecGrammar.ResolveAllPanes(specName)
            .Where(p => p.ThicknessMm > 0)
            .Select(p => p.ThicknessMm)
            .Distinct()
            .ToList();
        return mm.Count == 1 ? mm[0] : 0;
    }

    /// <summary>Does the treatment require a furnace pass? (WPF
    /// RequiresTempering: FT / Tempered / Heat.)</summary>
    public static bool RequiresTempering(string? treatment) =>
        !string.IsNullOrWhiteSpace(treatment)
        && (treatment.Contains("FT", StringComparison.OrdinalIgnoreCase)
            || treatment.Contains("Tempered", StringComparison.OrdinalIgnoreCase)
            || treatment.Contains("Heat", StringComparison.OrdinalIgnoreCase));

    /// <summary>Human piece label (WPF):
    /// {GlassRef} · {spec}/{serial} · {position} · {index}/{qty}.</summary>
    public static string BuildPieceLabel(
        string glassRef, int specGroup, int serial,
        string position, int pieceIndex, int qty) =>
        $"{glassRef} · {specGroup}/{serial} · {position} · {pieceIndex}/{qty}";
}

// ── stock selection (WPF StockSelectionService, pure half) ───────────

/// <summary>One candidate rack evaluated for a material group (WPF
/// StockSelectionResult — sheet identity + the numbers the selection
/// orders on).</summary>
public sealed record StockCandidate(
    Guid? GlassSheetId,
    string Category,
    string Color,
    int ThicknessMm,
    int WidthMm,
    int HeightMm,
    int Balance,
    int ReservedQty,
    decimal WasteScore)
{
    /// <summary>Sheet units only (WPF): the reservation quantity holds
    /// sheets; placed-piece allocations are a different unit and are
    /// NEVER subtracted here.</summary>
    public int EffectiveAvailable => Math.Max(0, Balance - ReservedQty);

    /// <summary>WPF StockSize label: {Category} {Color} {t}mm {W×H}
    /// (Bal {n}).</summary>
    public string StockSizeLabel =>
        $"{Category} {Color} {ThicknessMm}mm {WidthMm}×{HeightMm} (Bal {Balance})";

    public decimal SheetSqm =>
        (decimal)WidthMm * HeightMm / 1_000_000m;
}

public static class CuttingStockSelection
{
    /// <summary>Is this rack physically compatible with the group? (WPF
    /// IsSheetCompatibleWithGroup: thickness exact; color exact with a
    /// category/color-vs-family fallback; family contains-both-ways with
    /// Clear↔Low-E hard-incompatible; the largest piece must fit, rotated
    /// either way.)</summary>
    public static bool IsSheetCompatible(
        string category, string color, int thicknessMm,
        int widthMm, int heightMm, MaterialGroup group)
    {
        if (thicknessMm != group.ThicknessMm) return false;

        if (!string.Equals((color ?? "").Trim(), group.Color?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals((category ?? "").Trim(), group.GlassFamily?.Trim(),
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals((color ?? "").Trim(), group.GlassFamily?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        string sheetFamily = (category ?? "").Trim();
        string groupFamily = (group.GlassFamily ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(sheetFamily)
            && !string.IsNullOrWhiteSpace(groupFamily))
        {
            bool same = sheetFamily.Equals(groupFamily, StringComparison.OrdinalIgnoreCase)
                || sheetFamily.Contains(groupFamily, StringComparison.OrdinalIgnoreCase)
                || groupFamily.Contains(sheetFamily, StringComparison.OrdinalIgnoreCase);
            if (!same)
            {
                if (sheetFamily.Equals("Clear", StringComparison.OrdinalIgnoreCase)
                    && groupFamily.Equals("Low-E", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (sheetFamily.Equals("Low-E", StringComparison.OrdinalIgnoreCase)
                    && groupFamily.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        var maxW = group.Requirements.Max(r => Math.Max(r.WidthMm, r.HeightMm));
        var maxH = group.Requirements.Max(r => Math.Min(r.WidthMm, r.HeightMm));
        return (widthMm >= maxW && heightMm >= maxH)
            || (widthMm >= maxH && heightMm >= maxW);
    }

    /// <summary>Evaluate the racks for a group (WPF
    /// GetCompatibleSheetsAsync pure half): compatible racks with balance
    /// left, ordered WasteScore → area → stable id. Racks with no
    /// EFFECTIVE stock (fully held by reservations) are skipped — a fully
    /// reserved rack is not a healthy pool.</summary>
    public static IReadOnlyList<StockCandidate> Evaluate(
        MaterialGroup group,
        IReadOnlyList<(Guid Id, string Category, string Color, int ThicknessMm,
            int WidthMm, int HeightMm, int Stock, int Used)> sheets,
        IReadOnlyDictionary<Guid, int> reservedPerSheet)
    {
        var results = new List<StockCandidate>();
        if (group.Requirements.Count == 0) return results;

        var maxPiece = group.Requirements
            .OrderByDescending(r => (long)r.WidthMm * r.HeightMm)
            .First();

        foreach (var s in sheets)
        {
            int balance = s.Stock - s.Used;
            if (balance <= 0) continue;
            if (!IsSheetCompatible(s.Category, s.Color, s.ThicknessMm,
                    s.WidthMm, s.HeightMm, group)) continue;

            int reserved = reservedPerSheet.TryGetValue(s.Id, out var r) ? r : 0;
            int effective = Math.Max(0, balance - reserved);
            if (effective <= 0) continue;

            decimal sheetArea = (decimal)s.WidthMm * s.HeightMm;
            decimal pieceArea = (decimal)maxPiece.WidthMm * maxPiece.HeightMm;

            results.Add(new StockCandidate(
                s.Id, s.Category, s.Color, s.ThicknessMm,
                s.WidthMm, s.HeightMm, balance, reserved,
                sheetArea - pieceArea));
        }

        return results
            .OrderBy(c => c.WasteScore)
            .ThenBy(c => (decimal)c.WidthMm * c.HeightMm)
            .ThenBy(c => c.GlassSheetId ?? Guid.Empty)
            .ToList();
    }

    /// <summary>How many sheets to reserve for a group (WPF workflow):
    /// ceil(groupArea / (sheetSqm × 0.8)), at least 1 — the 80% fill
    /// estimate; a zero-area sheet falls back to 1.</summary>
    public static int EstimateSheetCount(
        MaterialGroup group, decimal sheetSqm)
    {
        const decimal avgUtil = 0.8m;
        int estimated = sheetSqm > 0
            ? (int)Math.Ceiling(group.TotalAreaSqm / (sheetSqm * avgUtil))
            : 1;
        return Math.Max(1, estimated);
    }
}

// ── snapshot integrity (WPF ProductionSnapshotIntegrity) ─────────────

public enum ProductionSnapshotKind
{
    Missing,
    Corrupt,
    ValidEmpty,
    Valid
}

// ── inventory consistency (WPF ProductionInventoryConsistencyScanner) ─

public enum ConsistencyKind
{
    Fact = 0,
    Observation = 1,
    NotDetectable = 2
}

/// <summary>Finding codes (WPF scanner, verbatim).</summary>
public static class ConsistencyCodes
{
    public const string UsedExceedsStock = "UsedExceedsStock";
    public const string MissingGlassSheet = "MissingGlassSheet";
    public const string ActiveHoldExceedsPhysical = "ActiveHoldExceedsPhysical";
    public const string AllocationMissingReservation = "AllocationMissingReservation";
    public const string AllocConsumedReservationReleased = "AllocConsumedReservationReleased";
    public const string ReservationWithoutRelease = "ReservationWithoutRelease";
    public const string PlanningConsumedNoMatchingHistory = "PlanningConsumedNoMatchingHistory";
    public const string RetryVsNewConsumption = "RetryVsNewConsumption";
}
