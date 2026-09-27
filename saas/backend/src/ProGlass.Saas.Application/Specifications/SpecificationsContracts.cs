namespace ProGlass.Saas.Application.Specifications;

/// <summary>Specifications failure codes (API §0 error contract).</summary>
public enum SpecificationErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record SpecificationResult<T>(
    bool Success, T? Value, SpecificationErrorCode ErrorCode, string Message)
{
    public static SpecificationResult<T> Ok(T value) =>
        new(true, value, SpecificationErrorCode.None, "");

    public static SpecificationResult<T> Fail(
        SpecificationErrorCode code, string message) =>
        new(false, default, code, message);
}

// ── Requests ───────────────────────────────────────────────────────────

/// <summary>
/// POST /api/v1/specifications — the Step 4 APPLY. Upserts the reusable
/// LIBRARY by (glassType, specName): the rate is ALWAYS refreshed;
/// catalogue details are refreshed only when actually supplied (a
/// valueless field must not wipe what an earlier, better-informed save
/// recorded — WPF preserve-on-blank, verbatim). Never touches usage
/// counts: those are ledger-derived from saved invoices.
/// </summary>
public sealed record ApplySpecificationRequest(
    string GlassType,
    string SpecName,
    decimal RatePerSqm,
    decimal? CostBasisPerSqm,
    string? ParamsJson,
    string? Category,
    string? Color,
    int? ThicknessMm,
    int? SheetWidthMm,
    int? SheetHeightMm,
    Guid? SheetId,
    string? PanesJson);

// ── Read models ────────────────────────────────────────────────────────

public sealed record SpecificationDto(
    Guid Id, string SpecName, string GlassType,
    decimal RatePerSqm, decimal CostBasisPerSqm,
    int UsageCount, DateTime? LastUsedAt, bool IsActive,
    string? Category, string? Color, int ThicknessMm,
    int SheetWidthMm, int SheetHeightMm, Guid? SheetId,
    string? PanesJson, string? LastProformaNo,
    DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>One verified use of a spec on a SAVED invoice (ledger-derived:
/// the invoice's number + names come from the saved row, not the draft
/// context Step 4 saw).</summary>
public sealed record SpecificationUsageDto(
    Guid Id, Guid SpecId, Guid ProformaId, string ProformaNo,
    string SpecName, string GlassType,
    string CompanyName, string CustomerName, string ProjectName,
    decimal PricePerSqm, int SheetWidthMm, int SheetHeightMm,
    DateTime UsedAt);

/// <summary>The reconcile outcome, for the audit entry and the ops
/// response.</summary>
public sealed record ReconcileResult(int UsageRows, int Purged, int Healed);
